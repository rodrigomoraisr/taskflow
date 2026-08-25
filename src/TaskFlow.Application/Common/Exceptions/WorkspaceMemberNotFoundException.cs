namespace TaskFlow.Application.Common.Exceptions;

public class WorkspaceMemberNotFoundException : Exception
{
    public WorkspaceMemberNotFoundException(Guid userId)
        : base($"Active workspace member {userId} was not found.")
    {
    }
}
