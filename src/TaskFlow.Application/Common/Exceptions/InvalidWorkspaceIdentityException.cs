
namespace TaskFlow.Application.Common.Exceptions;

public class InvalidWorkspaceIdentityException : Exception
{
    public InvalidWorkspaceIdentityException()
        : base("The authenticated workspace identity is invalid.")
    {
    }
}