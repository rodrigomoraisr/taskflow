using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Security;

public sealed class GlobalRateLimitOptions
{
    [Range(1, 100000)]
    public int PermitLimit { get; set; } = 120;

    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;
}
