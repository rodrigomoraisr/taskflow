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

    public async Task<WorkspaceUser?> GetFirstMembershipAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkspaceUsers
        .AsNoTracking()
        .Where(wu =>
            wu.UserId == userId &&
            !wu.IsDeleted)
        .OrderBy(wu => wu.CreatedAt)
        .FirstOrDefaultAsync(cancellationToken);
    }
}