namespace TaskFlow.Application.Common.Exceptions;

public class WorkspaceMemberAlreadyExistsException : Exception
{
    public WorkspaceMemberAlreadyExistsException(Guid userId)
        : base($"User {userId} is already an active member of this workspace.")
    {
    }
}
