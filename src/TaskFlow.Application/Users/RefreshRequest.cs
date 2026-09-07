using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Application.Users;

public sealed class RefreshRequest
{
    [Required, RegularExpression("^[A-F0-9]{64}$")]
    public string RefreshToken { get; set; } = string.Empty;
}
