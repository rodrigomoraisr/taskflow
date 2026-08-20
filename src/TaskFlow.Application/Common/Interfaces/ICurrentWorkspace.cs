using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Interfaces;

public interface ICurrentWorkspace
{
    Guid WorkspaceId { get; }

    WorkspaceRole Role { get; }
}