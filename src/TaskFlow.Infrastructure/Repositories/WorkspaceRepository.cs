using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class WorkspaceRepository : IWorkspaceRepository
{
    private readonly TaskFlowDbContext _dbContext;

    public WorkspaceRepository(
        TaskFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Workspaces.AddAsync(
            workspace,
            cancellationToken);
    }

    public async Task<Workspace?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Workspaces
            .FirstOrDefaultAsync(
                w => w.Id == id &&
                     !w.IsDeleted,
                cancellationToken);
    }

    public async Task<List<Workspace>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Workspaces
            .AsNoTracking()
            .Where(workspace => ids.Contains(workspace.Id) &&
                                !workspace.IsDeleted)
            .OrderBy(workspace => workspace.Name)
            .ToListAsync(cancellationToken);
    }
}
