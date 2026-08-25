using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface IWorkspaceUserRepository
{
    Task AddAsync(
        WorkspaceUser workspaceUser,
        CancellationToken cancellationToken = default);
        
    Task<WorkspaceUser?> GetActiveMembershipAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<List<WorkspaceUser>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<WorkspaceUser>> GetActiveByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceUser?> GetByUserAndWorkspaceAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveOwnersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
