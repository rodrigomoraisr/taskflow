using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly TaskFlowDbContext _dbContext;

    public TaskRepository(
        TaskFlowDbContext dbContext
    )
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        await _dbContext.Tasks.AddAsync(task, cancellationToken);
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tasks
            .FirstOrDefaultAsync(
                t => t.Id == id &&
                t.WorkspaceId == workspaceId &&
                !t.IsDeleted,
         cancellationToken);
    }

    public async Task<List<TaskItem>> GetPagedAsync( Guid workspaceId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tasks
            .AsNoTracking()
            .Where(t => 
                t.WorkspaceId == workspaceId && 
                !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return await _dbContext.Tasks
            .CountAsync(
            t =>  t.WorkspaceId == workspaceId && !t.IsDeleted,
            cancellationToken);
    }
}