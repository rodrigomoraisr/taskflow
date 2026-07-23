using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface IWorkspaceUserRepository
{
    Task AddAsync(
        WorkspaceUser workspaceUser,
        CancellationToken cancellationToken = default);
        
    Task<WorkspaceUser?> GetFirstMembershipAsync(
    Guid userId,
    CancellationToken cancellationToken = default);
}