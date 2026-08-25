namespace TaskFlow.Application.Common.Exceptions;

public class LastWorkspaceOwnerException : Exception
{
    public LastWorkspaceOwnerException()
        : base("An active workspace must have at least one active owner.")
    {
    }
}
