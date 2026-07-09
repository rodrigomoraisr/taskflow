namespace TaskFlow.Domain.Exceptions;
public class TaskAlreadyDeletedException : Exception
{
    public TaskAlreadyDeletedException(Guid id)
        : base($"Task {id} is already deleted.")
    {
    }
}