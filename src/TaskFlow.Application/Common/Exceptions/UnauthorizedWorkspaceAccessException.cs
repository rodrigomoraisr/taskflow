namespace TaskFlow.Application.Common.Exceptions;

public class UnauthorizedWorkspaceAccessException : Exception
{
    public UnauthorizedWorkspaceAccessException()
    :base("User does not belong to this workspace."){}
}

