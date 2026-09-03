namespace TaskFlow.Application.Common.Exceptions;

public class WorkspaceNotFoundException : Exception
{
    public WorkspaceNotFoundException(Guid workspaceId)
        : base($"The workspace {workspaceId} was not found.")
    {
        WorkspaceId = workspaceId;
    }

    public Guid WorkspaceId { get; }
}
