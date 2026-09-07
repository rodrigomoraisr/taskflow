using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class TaskActivityRepository(TaskFlowDbContext db) : ITaskActivityRepository
{
    public async Task AddAsync(TaskActivity activity, CancellationToken cancellationToken = default)
        => await db.TaskActivities.AddAsync(activity, cancellationToken);

    // History deliberately includes deleted tasks/projects. Membership is
    // checked by the service; the tenant predicate remains mandatory here.
    public Task<List<TaskActivity>> GetPagedAsync(Guid workspaceId, Guid? taskId,
        int page, int pageSize, CancellationToken cancellationToken = default) =>
        db.TaskActivities.AsNoTracking()
            .Where(a => a.WorkspaceId == workspaceId && (!taskId.HasValue || a.TaskId == taskId))
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize)
            .ToListAsync(cancellationToken);
}
