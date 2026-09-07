using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace TaskFlow.Api.Security;

public sealed class AuthRateLimitPolicy(IOptions<AuthRateLimitOptions> options) : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.Value.PermitLimit,
                Window = TimeSpan.FromSeconds(options.Value.WindowSeconds),
                QueueLimit = 0, AutoReplenishment = true
            });
}
