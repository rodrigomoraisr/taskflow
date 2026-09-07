using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class TaskRepository(TaskFlowDbContext db) : ITaskRepository
{
    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
        => await db.Tasks.AddAsync(task, cancellationToken);

    private IQueryable<TaskItem> Active(Guid workspaceId) => db.Tasks.Where(t =>
        t.WorkspaceId == workspaceId && !t.IsDeleted && db.Projects.Any(p =>
            p.Id == t.ProjectId && p.WorkspaceId == t.WorkspaceId && !p.IsDeleted));

    public Task<TaskItem?> GetByIdAsync(Guid id, Guid workspaceId,
        CancellationToken cancellationToken = default) =>
        Active(workspaceId).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    private IQueryable<TaskItem> Filtered(Guid workspaceId, GetTasksRequest query)
    {
        query.EnsureValid();
        var tasks = Active(workspaceId);
        if (query.Status.HasValue) tasks = tasks.Where(t => t.Status == query.Status.Value);
        if (query.Priority.HasValue) tasks = tasks.Where(t => t.Priority == query.Priority.Value);
        if (query.ProjectId.HasValue) tasks = tasks.Where(t => t.ProjectId == query.ProjectId.Value);
        if (query.AssigneeUserId.HasValue) tasks = tasks.Where(t => t.AssigneeUserId == query.AssigneeUserId.Value);
        if (query.Unassigned) tasks = tasks.Where(t => t.AssigneeUserId == null);
        if (query.DueDateFrom.HasValue)
        {
            var from = query.DueDateFrom.Value.UtcDateTime;
            tasks = tasks.Where(t => t.DueDate >= from);
        }
        if (query.DueDateTo.HasValue)
        {
            var to = query.DueDateTo.Value.UtcDateTime;
            tasks = tasks.Where(t => t.DueDate <= to);
        }
        return tasks;
    }

    public Task<int> CountAsync(Guid workspaceId, GetTasksRequest query,
        CancellationToken cancellationToken = default) =>
        Filtered(workspaceId, query).CountAsync(cancellationToken);

    public async Task<List<TaskItem>> GetPagedAsync(Guid workspaceId, GetTasksRequest query,
        CancellationToken cancellationToken = default)
    {
        var tasks = Filtered(workspaceId, query).AsNoTracking();
        var descending = query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        // Expressions are chosen here, never constructed from client text.
        IOrderedQueryable<TaskItem> sorted = query.SortBy.ToLowerInvariant() switch
        {
            "createdat" => Order(tasks, t => t.CreatedAt, descending),
            "updatedat" => Order(tasks.OrderBy(t => t.UpdatedAt == null), t => t.UpdatedAt, descending, true),
            "title" => Order(tasks, t => t.Title, descending),
            "priority" => Order(tasks, t => t.Priority, descending),
            // Status is stored as text; workflow order is not alphabetic order.
            "status" => Order(tasks, t => t.Status == TaskItemStatus.Todo ? 0
                : t.Status == TaskItemStatus.InProgress ? 1 : 2, descending),
            "duedate" => Order(tasks.OrderBy(t => t.DueDate == null), t => t.DueDate, descending, true),
            _ => throw new ArgumentException("Unsupported sort field.")
        };
        // Use long arithmetic before narrowing to EF's Skip(int), avoiding overflow.
        var offset = (long)(query.Page - 1) * query.PageSize;
        if (offset > int.MaxValue) return [];
        return await sorted.ThenBy(t => t.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(cancellationToken);
    }

    private static IOrderedQueryable<TaskItem> Order<TKey>(IQueryable<TaskItem> tasks,
        Expression<Func<TaskItem, TKey>> key, bool descending, bool secondary = false)
    {
        if (secondary)
        {
            var ordered = (IOrderedQueryable<TaskItem>)tasks;
            return descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }
        return descending ? tasks.OrderByDescending(key) : tasks.OrderBy(key);
    }
}
