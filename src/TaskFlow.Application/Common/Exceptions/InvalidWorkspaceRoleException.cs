namespace TaskFlow.Application.Common.Exceptions;

public class InvalidWorkspaceRoleException : Exception
{
    public InvalidWorkspaceRoleException()
        : base("The requested workspace role is invalid.")
    {
    }
}
