public class CreateWorkspaceResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }
}