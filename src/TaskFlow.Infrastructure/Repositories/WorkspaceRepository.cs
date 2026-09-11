using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace TaskFlow.Infrastructure.Repositories;

public class WorkspaceRepository : IWorkspaceRepository
{
    public async Task<IApplicationTransaction> BeginMembershipChangeAsync(
        Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize membership decisions for this workspace, including owner counts.
            await _dbContext.Workspaces.FromSqlInterpolated(
                    $"SELECT * FROM \"Workspaces\" WHERE \"Id\" = {workspaceId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            return new MembershipTransaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class MembershipTransaction(IDbContextTransaction transaction) : IApplicationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

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
