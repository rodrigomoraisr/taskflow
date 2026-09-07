using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace TaskFlow.Api.Middleware;

public sealed class RequestObservabilityMiddleware(
    RequestDelegate next, ILogger<RequestObservabilityMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var values = context.Request.Headers[HeaderName];
        var supplied = values.Count == 1 ? values[0] : null;
        context.TraceIdentifier = IsValid(supplied) ? supplied! : Guid.NewGuid().ToString("N");
        context.Response.Headers[HeaderName] = context.TraceIdentifier;
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = context.TraceIdentifier
        });
        var start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            // Templates avoid logging query strings, identifiers and arbitrary URL input.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            logger.LogInformation("HTTP {Method} {Route} returned {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method, route, context.Response.StatusCode,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    private static bool IsValid(string? value) => value is { Length: > 0 and <= 64 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
