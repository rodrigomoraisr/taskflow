namespace TaskFlow.Application.Common.Exceptions;

public class InvalidTaskAssignmentException : Exception
{
    public InvalidTaskAssignmentException()
        : base("The current user cannot perform this task assignment.")
    {
    }
}
