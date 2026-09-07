using TaskFlow.Application.Comments;

namespace TaskFlow.Application.Common.Interfaces;

public interface ICommentService
{
    Task<CommentResponse> CreateAsync(Guid workspaceId, Guid taskId, WriteCommentRequest request, CancellationToken cancellationToken = default);
    Task<CommentResponse> GetByIdAsync(Guid workspaceId, Guid taskId, Guid id, CancellationToken cancellationToken = default);
    Task<List<CommentResponse>> GetPagedAsync(Guid workspaceId, Guid taskId, GetCommentsRequest request, CancellationToken cancellationToken = default);
    Task EditAsync(Guid workspaceId, Guid taskId, Guid id, WriteCommentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid workspaceId, Guid taskId, Guid id, CancellationToken cancellationToken = default);
}
