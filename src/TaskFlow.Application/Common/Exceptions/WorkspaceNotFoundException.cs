using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Exceptions;

public class WorkspaceNotFoundException : Exception
{
    public WorkspaceNotFoundException(Guid id)
    :base($"The workspace {id} was not found!"){}
}

