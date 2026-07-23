namespace TaskFlow.Domain.Exceptions;
public class WorkspaceUserAlreadyDeletedException : Exception
{
    public WorkspaceUserAlreadyDeletedException(Guid id)
        : base($"Workspace - User {id} is already deleted.")
    {
    }
}