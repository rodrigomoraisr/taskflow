using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.IntegrationTests.Workflows;

public class ApiHardeningTests(PostgreSqlFixture fixture) : IntegrationTestBase(fixture)
{
    [Theory]
    [InlineData("GET", "/does-not-exist", 404)]
    [InlineData("GET", "/api/workspaces", 401)]
    [InlineData("POST", "/auth/login", 400)]
    [InlineData("PUT", "/auth/login", 405)]
    public async Task Request_WhenRejectedByPipeline_ShouldReturnCorrelatedProblem(string method, string path, int status)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("X-Correlation-ID", "client-request_123.abc");
        // Error responses remain JSON even if the client's preferred representation is unsupported.
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        if (method == "POST") request.Content = JsonContent.Create(new { });

        using var response = await client.SendAsync(request);

        var body = await AssertProblemAsync(response, status);
        Assert.Equal("client-request_123.abc", body.GetProperty("correlationId").GetString());
        if (status == 400) Assert.True(body.TryGetProperty("errors", out _));
        if (status == 401) Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("has spaces")]
    [InlineData("bad/id")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("one,two")]
    public async Task Request_WhenCorrelationMissingOrInvalid_ShouldGenerateUniqueSafeId(string? supplied)
    {
        using var client = _factory.CreateClient();
        if (supplied is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("X-Correlation-ID", supplied);
        using var first = await client.GetAsync("/missing");
        using var second = await client.GetAsync("/missing");
        var body = await AssertProblemAsync(first, 404);
        var id = body.GetProperty("correlationId").GetString();
        Assert.True(Guid.TryParseExact(id, "N", out _));
        Assert.NotEqual(id, second.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task GlobalLimiter_WhenDifferentRoutesShareBudget_ShouldRejectWithRetryAfterAndKeepProbesAvailable()
    {
        _factory.GlobalPermitLimit = 2;
        using var client = _factory.CreateClient();
        using var first = await client.GetAsync("/missing");
        using var second = await client.GetAsync("/api/workspaces");
        using var limited = await client.PostAsJsonAsync("/auth/login", new { });

        Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        await AssertProblemAsync(limited, 429);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        // Forwarding headers cannot create a new partition for an untrusted caller.
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.4");
        using var spoofed = await client.GetAsync("/another-missing-route");
        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
        foreach (var path in new[] { "/health/live", "/health/ready" })
        {
            using var probe = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
            Assert.Equal("Healthy", await probe.Content.ReadAsStringAsync());
            Assert.Single(probe.Headers.GetValues("X-Correlation-ID"));
        }
    }

    [Fact]
    public async Task Readiness_WhenDatabaseUnavailable_ShouldReturn503WhileLivenessRemainsHealthy()
    {
        using var broken = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TaskFlowDbContext>>();
            services.AddDbContext<TaskFlowDbContext>(options => options.UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=unavailable;Username=health;Password=private-test-value;Timeout=1"));
        }));
        using var client = broken.CreateClient();
        using var ready = await client.GetAsync("/health/ready");
        using var live = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.True(ready.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Update_WhenAnotherWriterCommitsAfterLoad_ShouldReturn409AndPreserveWinnerAndActivity()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var interceptor = new ConcurrentWriter(async () =>
        {
            await WithDbAsync(async db =>
            {
                var task = await db.Tasks.SingleAsync(t => t.Id == taskId);
                task.Start();
                await db.SaveChangesAsync();
            });
        });
        using var racing = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<TaskFlowDbContext>(options => options.AddInterceptors(interceptor))));
        using var client = racing.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);

        using var response = await client.PutAsJsonAsync($"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}",
            new UpdateTaskRequest { Title = "Losing edit" });

        await AssertProblemAsync(response, 409);
        await WithDbAsync(async db =>
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == taskId);
            Assert.NotEqual("Losing edit", task.Title);
            Assert.Equal(TaskFlow.Domain.Enums.TaskItemStatus.InProgress, task.Status);
            Assert.Single(await db.TaskActivities.Where(a => a.TaskId == taskId).ToListAsync());
        });
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, int status)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.True(Uri.TryCreate(body.GetProperty("type").GetString(), UriKind.Absolute, out _));
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(),
            body.GetProperty("correlationId").GetString());
        Assert.False(body.TryGetProperty("error", out _));
        return body;
    }

    private sealed class ConcurrentWriter(Func<Task> write) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<TaskItem>().Any(e => e.State == EntityState.Modified))
                await write();
            return result;
        }
    }
}
