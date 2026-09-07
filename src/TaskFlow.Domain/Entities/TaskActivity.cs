using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

// No mutation methods: activity is appended, never edited or soft-deleted.
public sealed class TaskActivity
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid ActorId { get; private set; }
    public TaskActivityAction Action { get; private set; }
    public Guid? CommentId { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private TaskActivity() { }

    public TaskActivity(Guid workspaceId, Guid taskId, Guid actorId,
        TaskActivityAction action, string details, Guid? commentId = null)
    {
        if (workspaceId == Guid.Empty || taskId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Workspace, task and actor IDs are required.");
        if (!Enum.IsDefined(action))
            throw new ArgumentException("Unknown activity action.");
        if (string.IsNullOrWhiteSpace(details))
            throw new ArgumentException("Activity details are required.");
        var isComment = action is TaskActivityAction.CommentCreated
            or TaskActivityAction.CommentEdited or TaskActivityAction.CommentDeleted;
        if (isComment != commentId.HasValue || commentId == Guid.Empty)
            throw new ArgumentException("Only comment actions require a comment ID.");

        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        TaskId = taskId;
        ActorId = actorId;
        Action = action;
        Details = details;
        CommentId = commentId;
        CreatedAt = DateTime.UtcNow;
    }
}
