using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
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
}
