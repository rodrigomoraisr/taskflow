using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class CommentRepository(TaskFlowDbContext db) : ICommentRepository
{
    public async Task AddAsync(Comment comment, CancellationToken cancellationToken = default)
        => await db.Comments.AddAsync(comment, cancellationToken);

    private IQueryable<Comment> Active(Guid workspaceId, Guid taskId) =>
        db.Comments.Where(c => c.WorkspaceId == workspaceId && c.TaskId == taskId
            && !c.IsDeleted && db.Tasks.Any(t => t.Id == c.TaskId
                && t.WorkspaceId == c.WorkspaceId && !t.IsDeleted
                && db.Projects.Any(p => p.Id == t.ProjectId
                    && p.WorkspaceId == t.WorkspaceId && !p.IsDeleted)));

    public Task<Comment?> GetByIdAsync(Guid id, Guid taskId, Guid workspaceId,
        CancellationToken cancellationToken = default) =>
        Active(workspaceId, taskId).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<List<Comment>> GetPagedAsync(Guid taskId, Guid workspaceId,
        int page, int pageSize, CancellationToken cancellationToken = default) =>
        Active(workspaceId, taskId).AsNoTracking()
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize)
            .ToListAsync(cancellationToken);
}
