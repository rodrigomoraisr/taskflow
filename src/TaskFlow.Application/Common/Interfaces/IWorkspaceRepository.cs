namespace TaskFlow.Application.Workspaces;

using TaskFlow.Domain.Entities;

public interface IWorkspaceRepository
{
    Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default);

    Task<Workspace?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}