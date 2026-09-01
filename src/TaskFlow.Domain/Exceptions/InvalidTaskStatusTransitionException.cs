using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Exceptions;

public class InvalidTaskStatusTransitionException : Exception
{
    public InvalidTaskStatusTransitionException(
        TaskItemStatus currentStatus,
        TaskItemStatus requestedStatus)
        : base($"Task status cannot change from {currentStatus} to {requestedStatus}.")
    {
    }
}
