namespace TaskFlow.Application.Comments;

public sealed record CommentResponse(Guid Id, Guid TaskId, Guid AuthorId,
    string Body, DateTime CreatedAt, DateTime? UpdatedAt);
