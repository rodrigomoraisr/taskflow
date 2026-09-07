namespace TaskFlow.Domain.Enums;

public enum TaskActivityAction
{
    TaskCreated, TaskUpdated, TaskDeleted, TaskStarted, TaskCompleted,
    TaskReopened, TaskAssigned, TaskUnassigned,
    CommentCreated, CommentEdited, CommentDeleted
}
