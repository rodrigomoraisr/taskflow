namespace TaskFlow.Application.Common.Exceptions;

public class UserNotFoundException : Exception
{
    public UserNotFoundException(string email)
        : base($"An active user with email '{email}' was not found.")
    {
    }
}
