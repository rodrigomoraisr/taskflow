using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskFlow.Api.Security;

namespace TaskFlow.Api.IntegrationTests.Security;

public class ProxyConfigurationTests
{
    private static IHost Server(bool enabled, string peer)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Enabled"] = enabled.ToString(),
            ["ReverseProxy:KnownProxies:0"] = "10.20.30.40"
        }).Build();
        return new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddTrustedProxyHeaders(configuration);
            services.AddRouting();
            services.Configure<AuthRateLimitOptions>(o => { o.PermitLimit = 1; o.WindowSeconds = 60; });
            services.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = 429;
                o.AddPolicy<string, AuthRateLimitPolicy>("auth");
            });
        }).Configure(app =>
        {
            app.Use(async (context, next) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                await next(context);
            });
            app.UseForwardedHeaders();
            app.UseRouting();
            app.UseRateLimiter();
            app.UseEndpoints(endpoints => endpoints.MapGet("/", context =>
                context.Response.WriteAsync($"{context.Connection.RemoteIpAddress}|{context.Request.Scheme}|{context.Request.Host}"))
                .RequireRateLimiting("auth"));
        })).Start();
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string address)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-For", address);
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "attacker.example");
        return client.SendAsync(request);
    }

    [Theory]
    [InlineData("10.20.30.40")]
    [InlineData("::ffff:10.20.30.40")]
    public async Task Forwarding_WhenPeerTrusted_ShouldUseNearestClientAndSeparateRateBudgets(string peer)
    {
        using var server = Server(true, peer);
        using var client = server.GetTestClient();

        using var first = await Send(client, "192.0.2.99, 203.0.113.1");
        using var spoofed = await Send(client, "192.0.2.100, 203.0.113.1");
        using var other = await Send(client, "203.0.113.2");

        Assert.Equal("203.0.113.1|https|localhost", await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Theory]
    [InlineData(true, "10.20.30.41")]
    [InlineData(false, "10.20.30.40")]
    public async Task Forwarding_WhenUntrustedOrDisabled_ShouldIgnoreSpoofingAndSharePeerBudget(bool enabled, string peer)
    {
        using var server = Server(enabled, peer);
        using var client = server.GetTestClient();

        using var first = await Send(client, "203.0.113.1");
        using var second = await Send(client, "203.0.113.2");

        Assert.Equal($"{peer}|http|localhost", await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("10.0.0.0/8")]
    [InlineData("proxy.example")]
    public void Configuration_WhenEnabledWithoutSpecificProxy_ShouldReject(string? proxy)
    {
        var values = new Dictionary<string, string?> { ["ReverseProxy:Enabled"] = "true" };
        if (proxy is not null) values["ReverseProxy:KnownProxies:0"] = proxy;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddTrustedProxyHeaders(configuration));
    }

    [Fact]
    public void Configuration_WhenAutomaticTrustSwitchEnabled_ShouldReject()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ForwardedHeaders_Enabled"] = "true" }).Build();

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddTrustedProxyHeaders(configuration));
    }
}
