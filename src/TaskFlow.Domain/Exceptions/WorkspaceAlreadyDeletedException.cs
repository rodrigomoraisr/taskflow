namespace TaskFlow.Domain.Exceptions;
public class WorkspaceAlreadyDeletedException : Exception
{
    public WorkspaceAlreadyDeletedException(Guid id)
        : base($"Workspace {id} is already deleted.")
    {
    }
}