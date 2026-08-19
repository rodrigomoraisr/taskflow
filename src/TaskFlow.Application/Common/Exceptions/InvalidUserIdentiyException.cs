namespace TaskFlow.Application.Common.Exceptions;

public class InvalidUserIdentityException : Exception
{
    public InvalidUserIdentityException()
        : base("The authenticated user identity is invalid.")
    {
    }
}