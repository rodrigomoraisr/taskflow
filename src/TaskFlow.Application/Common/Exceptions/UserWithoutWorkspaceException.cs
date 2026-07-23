using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Exceptions;

public class UserWithoutWorkspaceException : Exception
{
    public UserWithoutWorkspaceException()
    :base("User does not belong to any active workspace."){}
}

