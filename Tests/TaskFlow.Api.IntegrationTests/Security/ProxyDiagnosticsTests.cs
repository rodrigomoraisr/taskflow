using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Middleware;

namespace TaskFlow.Api.IntegrationTests.Security;

public class ProxyDiagnosticsTests
{
    [Theory]
    [InlineData(false, "/health/live", 0)]
    [InlineData(true, "/auth/login", 0)]
    [InlineData(true, "/health/live", 10)]
    public async Task Diagnostics_ShouldBeOptInHealthOnlyAndBounded(bool enabled, string path, int expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Diagnostics"] = enabled.ToString(),
            ["ReverseProxy:Enabled"] = "true"
        }).Build();
        var logger = new CaptureLogger();
        var calls = 0;
        var middleware = new ProxyDiagnosticsMiddleware(context =>
        {
            calls++;
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            context.Request.Scheme = "https";
            return Task.CompletedTask;
        }, config, logger);

        for (var i = 0; i < 12; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Path = path;
            context.Request.Headers.Authorization = "Bearer private-test-token";
            context.Request.Headers["X-Forwarded-For"] = "private-header-value";
            await middleware.InvokeAsync(context);
        }

        Assert.Equal(12, calls);
        Assert.Equal(expected, logger.Messages.Count);
        foreach (var message in logger.Messages)
        {
            Assert.Contains("Peer=127.0.0.1", message);
            Assert.Contains("Client=203.0.113.7", message);
            Assert.Contains("Scheme=https", message);
            Assert.DoesNotContain("private-", message);
        }
    }

    private sealed class CaptureLogger : ILogger<ProxyDiagnosticsMiddleware>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
