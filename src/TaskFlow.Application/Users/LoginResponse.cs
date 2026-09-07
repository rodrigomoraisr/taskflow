public class LoginResponse
{
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }

    public string Token { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;
}
