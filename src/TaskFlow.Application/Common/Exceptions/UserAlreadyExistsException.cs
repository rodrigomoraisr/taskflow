namespace TaskFlow.Application.Common.Exceptions;

public class UserAlreadyExistsException : Exception
{
    public UserAlreadyExistsException(string email)
        : base($"User with email '{email}' already exists.")
    {
    }
}