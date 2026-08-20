namespace TaskFlow.Application.Common.Exceptions;

public class InsufficientWorkspaceRoleException : Exception
{
    public InsufficientWorkspaceRoleException()
    :base("User does not have permission to execute this operation in this workspace."){}
}

