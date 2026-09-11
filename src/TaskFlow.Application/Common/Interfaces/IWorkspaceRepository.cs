namespace TaskFlow.Application.Workspaces;

using TaskFlow.Domain.Entities;
using TaskFlow.Application.Common.Interfaces;

public interface IWorkspaceRepository
{
    Task<IApplicationTransaction> BeginMembershipChangeAsync(
        Guid workspaceId, CancellationToken cancellationToken = default);

    Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default);

    Task<Workspace?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<Workspace>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
