namespace TaskFlow.Application.Workspaces;

public class ListWorkspaceResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
