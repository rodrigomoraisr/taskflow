namespace TaskFlow.Api.Middleware;

// Temporary operator diagnostics, disabled by default. Never echo these details to clients.
public sealed class ProxyDiagnosticsMiddleware(
    RequestDelegate next, IConfiguration configuration, ILogger<ProxyDiagnosticsMiddleware> logger)
{
    private int _samples;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!configuration.GetValue<bool>("ReverseProxy:Diagnostics")
            || context.Request.Path != "/health/live"
            || Interlocked.Increment(ref _samples) > 10)
        {
            await next(context);
            return;
        }

        var peer = context.Connection.RemoteIpAddress?.ToString();
        var hasFor = context.Request.Headers.ContainsKey("X-Forwarded-For");
        var hasProto = context.Request.Headers.ContainsKey("X-Forwarded-Proto");
        await next(context);
        logger.LogInformation(
            "Proxy diagnostic: Enabled={Enabled}, Peer={Peer}, Client={Client}, Scheme={Scheme}, HasForwardedFor={HasForwardedFor}, HasForwardedProto={HasForwardedProto}",
            configuration.GetValue<bool>("ReverseProxy:Enabled"), peer,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Scheme == "https" ? "https" : context.Request.Scheme == "http" ? "http" : "other",
            hasFor, hasProto);
    }
}
