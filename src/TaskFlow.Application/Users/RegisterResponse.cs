namespace TaskFlow.Application.Users;

public class RegisterResponse
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public Guid WorkspaceId { get; set; }

    public string WorkspaceName { get; set; } = string.Empty;
}
