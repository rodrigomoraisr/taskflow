namespace TaskFlow.Application.Common.Exceptions;

public class ProjectNotFoundException : Exception
{
    public ProjectNotFoundException(Guid id)
        : base($"Project {id} was not found.")
    {
    }
}
