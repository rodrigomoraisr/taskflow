using System.Net;
using System.Net.Http.Json;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.Workflows;

public sealed class TaskQueryApiTests(PostgreSqlFixture postgres) : IntegrationTestBase(postgres)
{
    [Theory]
    [InlineData("status=999")]
    [InlineData("status=invalid")]
    [InlineData("priority=0")]
    [InlineData("priority=999")]
    [InlineData("sortBy=passwordHash")]
    [InlineData("sortBy=title%3BDROP%20TABLE%20tasks")]
    [InlineData("sortDirection=sideways")]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("dueDateFrom=2026-02-01T00:00:00Z&dueDateTo=2026-01-01T00:00:00Z")]
    [InlineData("dueDateFrom=invalid")]
    [InlineData("projectId=00000000-0000-0000-0000-000000000000")]
    [InlineData("assigneeUserId=00000000-0000-0000-0000-000000000000")]
    [InlineData("unassigned=true&assigneeUserId=11111111-1111-1111-1111-111111111111")]
    public async Task GetTasks_WhenQueryInvalid_ShouldReturnBadRequest(string query)
    {
        var owner = await RegisterUserAsync();
        using var client = CreateClientFor(owner);

        using var response = await client.GetAsync($"/api/workspaces/{owner.WorkspaceId}/tasks?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_WhenFiltersAndSortingCombined_ShouldBindQueryAndReturnFilteredCount()
    {
        var owner = await RegisterUserAsync();
        var (projectId, _) = await SeedProjectAndTaskAsync(owner);
        using var client = CreateClientFor(owner);
        var route = $"/api/workspaces/{owner.WorkspaceId}/tasks";
        foreach (var title in new[] { "Zeta", "Alpha" })
        {
            using var created = await client.PostAsJsonAsync(route, new CreateTaskRequest
            {
                ProjectId = projectId, Title = title, Description = "query test",
                Priority = TaskPriority.High, DueDate = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        var query = $"status=Todo&priority=High&projectId={projectId}&unassigned=true"
            + "&dueDateFrom=2026-01-20T03:00:00%2B03:00&dueDateTo=2026-01-20T00:00:00Z"
            + "&sortBy=TITLE&sortDirection=ASC&pageSize=1";

        var first = await client.GetFromJsonAsync<GetTasksResponse>($"{route}?{query}&page=1");
        var second = await client.GetFromJsonAsync<GetTasksResponse>($"{route}?{query}&page=2");
        var empty = await client.GetFromJsonAsync<GetTasksResponse>($"{route}?{query}&page=3");

        Assert.Equal("Alpha", Assert.Single(first!.Items).Title);
        Assert.Equal("Zeta", Assert.Single(second!.Items).Title);
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(2, second.TotalCount);
        Assert.Equal(2, empty!.TotalCount);
        Assert.Empty(empty.Items);
        Assert.Equal(1, first.Page);
        Assert.Equal(1, first.PageSize);
    }

    [Fact]
    public async Task GetTasks_WhenFilterContainsForeignIds_ShouldReturnNoForeignData()
    {
        var t = await ArrangeTwoTenantsAsync();
        using var client = CreateClientFor(t.OwnerA);
        using var other = CreateClientFor(t.OwnerB);
        using var assignment = await other.PutAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskB}/assignee",
            new AssignTaskRequest { UserId = t.OwnerB.Id });
        Assert.Equal(HttpStatusCode.NoContent, assignment.StatusCode);

        foreach (var filter in new[] { $"projectId={t.ProjectB}", $"assigneeUserId={t.OwnerB.Id}" })
        {
            var response = await client.GetFromJsonAsync<GetTasksResponse>(
                $"/api/workspaces/{t.WorkspaceA}/tasks?{filter}");
            Assert.Empty(response!.Items);
            Assert.Equal(0, response.TotalCount);
        }
        using var foreignWorkspace = await client.GetAsync($"/api/workspaces/{t.WorkspaceB}/tasks?status=Todo");
        Assert.Equal(HttpStatusCode.NotFound, foreignWorkspace.StatusCode);
    }
}
