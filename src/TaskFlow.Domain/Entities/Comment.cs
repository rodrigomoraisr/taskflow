using TaskFlow.Domain.Common;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public sealed class Comment : BaseEntity
{
    public const int MaxBodyLength = 2000;

    public Guid WorkspaceId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private Comment() { }

    public Comment(Guid workspaceId, Guid taskId, Guid authorId, string body)
    {
        if (workspaceId == Guid.Empty || taskId == Guid.Empty || authorId == Guid.Empty)
            throw new ArgumentException("Workspace, task and author IDs are required.");

        Body = ValidateBody(body);
        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        TaskId = taskId;
        AuthorId = authorId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Edit(string body)
    {
        EnsureNotDeleted();
        Body = ValidateBody(body);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        EnsureNotDeleted();
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DeletedAt;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new CommentAlreadyDeletedException(Id);
    }

    private static string ValidateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
            throw new ArgumentException($"Comment body must contain 1–{MaxBodyLength} characters.");
        return body.Trim();
    }
}
