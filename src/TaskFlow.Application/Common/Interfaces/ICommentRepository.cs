using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface ICommentRepository
{
    Task AddAsync(Comment comment, CancellationToken cancellationToken = default);
    Task<Comment?> GetByIdAsync(Guid id, Guid taskId, Guid workspaceId, CancellationToken cancellationToken = default);
    Task<List<Comment>> GetPagedAsync(Guid taskId, Guid workspaceId, int page, int pageSize, CancellationToken cancellationToken = default);
}
