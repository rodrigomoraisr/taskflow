namespace TaskFlow.Application.Exceptions;

public class TaskNotFoundException : Exception
{
    public TaskNotFoundException(Guid id)
        :base($"The task {id} was not found."){}
}