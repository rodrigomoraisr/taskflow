using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class WorkspaceUserRepository : IWorkspaceUserRepository
{
    private readonly TaskFlowDbContext _dbContext;

    public WorkspaceUserRepository(
        TaskFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        WorkspaceUser workspaceUser,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.WorkspaceUsers.AddAsync(
            workspaceUser,
            cancellationToken);
    }

    public async Task<WorkspaceUser?> GetActiveMembershipAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkspaceUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                wu => wu.UserId == userId &&
                      wu.WorkspaceId == workspaceId &&
                      !wu.IsDeleted &&
                      _dbContext.Workspaces.Any(
                          workspace => workspace.Id == wu.WorkspaceId &&
                                       !workspace.IsDeleted),
                cancellationToken);
    }

    public async Task<List<WorkspaceUser>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkspaceUsers
            .AsNoTracking()
            .Where(wu => wu.UserId == userId &&
                         !wu.IsDeleted &&
                         _dbContext.Workspaces.Any(
                             workspace => workspace.Id == wu.WorkspaceId &&
                                          !workspace.IsDeleted))
            .OrderBy(wu => wu.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkspaceUser>> GetActiveByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkspaceUsers
            .AsNoTracking()
            .Where(membership => membership.WorkspaceId == workspaceId &&
                                 !membership.IsDeleted)
            .OrderBy(membership => membership.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkspaceUser?> GetByUserAndWorkspaceAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkspaceUsers
            .FirstOrDefaultAsync(
                membership => membership.UserId == userId &&
                              membership.WorkspaceId == workspaceId,
                cancellationToken);
    }

    public async Task<int> CountActiveOwnersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkspaceUsers.CountAsync(
            membership => membership.WorkspaceId == workspaceId &&
                          membership.Role == WorkspaceRole.Owner &&
                          !membership.IsDeleted,
            cancellationToken);
    }
}
