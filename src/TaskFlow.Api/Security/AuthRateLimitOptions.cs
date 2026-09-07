using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Security;

public sealed class AuthRateLimitOptions
{
    [Range(1, 10000)]
    public int PermitLimit { get; set; } = 20;
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;
}
