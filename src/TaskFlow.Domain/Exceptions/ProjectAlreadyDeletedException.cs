namespace TaskFlow.Domain.Exceptions;

public class ProjectAlreadyDeletedException : Exception
{
    public ProjectAlreadyDeletedException(Guid id)
        : base($"Project {id} is already deleted.")
    {
    }
}
