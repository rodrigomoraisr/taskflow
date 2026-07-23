public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;
    
    public Guid WorkspaceId { get; set; }

    public string Role { get; set; } = string.Empty;
}