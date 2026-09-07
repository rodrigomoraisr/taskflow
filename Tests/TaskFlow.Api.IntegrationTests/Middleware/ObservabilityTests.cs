using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Middleware;

namespace TaskFlow.Api.IntegrationTests.Middleware;

public class ObservabilityTests
{
    [Fact]
    public async Task Invoke_WhenUnexpectedFailure_ShouldLogExceptionWithCorrelationAndReturnSafeProblem()
    {
        var logger = new RecordingLogger<ExceptionMiddleware>();
        var exception = new InvalidOperationException("private database failure");
        var middleware = new ExceptionMiddleware(_ => throw exception, logger);
        var context = Context();

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("correlation-test", entry.Message);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Contains("correlation-test", body);
        Assert.DoesNotContain("private database failure", body);
        Assert.Equal("application/problem+json", context.Response.ContentType);
    }

    [Fact]
    public async Task Invoke_WhenClientCancels_ShouldNotWrite500OrLogServerFailure()
    {
        var logger = new RecordingLogger<ExceptionMiddleware>();
        var middleware = new ExceptionMiddleware(_ => throw new OperationCanceledException(), logger);
        var context = Context();
        context.RequestAborted = new CancellationToken(canceled: true);

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task Invoke_WhenResponseAlreadyStarted_ShouldLogAndRethrowWithoutAppendingJson()
    {
        var logger = new RecordingLogger<ExceptionMiddleware>();
        var exception = new InvalidOperationException("stream failed");
        var middleware = new ExceptionMiddleware(_ => throw exception, logger);
        var context = Context();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        Assert.Same(exception, await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context)));

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
        Assert.Null(context.Response.ContentType);
    }

    [Fact]
    public async Task Invoke_WhenRequestFinishes_ShouldScopeLogsAndExcludeCredentialsAndRawUrl()
    {
        var logger = new RecordingLogger<RequestObservabilityMiddleware>();
        var middleware = new RequestObservabilityMiddleware(context =>
        {
            context.Response.StatusCode = 204;
            return Task.CompletedTask;
        }, logger);
        var context = Context();
        context.Request.Headers[RequestObservabilityMiddleware.HeaderName] = "client-123";
        context.Request.Headers.Authorization = "Bearer private-token";
        context.Request.Path = "/private-path";
        context.Request.QueryString = new QueryString("?password=private-password");

        await middleware.InvokeAsync(context);

        Assert.Equal("client-123", context.Response.Headers[RequestObservabilityMiddleware.HeaderName]);
        Assert.Equal("client-123", logger.Scope!["CorrelationId"]);
        var entry = Assert.Single(logger.Entries);
        Assert.Contains("204", entry.Message);
        Assert.DoesNotContain("private", entry.Message);
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "correlation-test" };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];
        public IReadOnlyDictionary<string, object>? Scope { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            Scope = state as IReadOnlyDictionary<string, object>;
            return null;
        }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception, formatter(state, exception)));
    }
}
