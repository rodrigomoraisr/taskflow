using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface IProjectRepository
{
    Task AddAsync(
        Project project,
        CancellationToken cancellationToken = default);

    Task<Project?> GetByIdAsync(
        Guid projectId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<List<Project>> GetByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
