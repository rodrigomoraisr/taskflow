namespace TaskFlow.Domain.Entities;

using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Exceptions;

public class TaskItem : BaseEntity
{
    private TaskItem(){}
    public string  Title { get; private set; }
    public string Description { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public TaskItemStatus Status{get; private set; }
    public TaskPriority Priority{get; private set; }
    public DateTime? DueDate { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt{get; private set;}

    public TaskItem(
        string title,
        string description,
        Guid workspaceId,
        Guid projectId,
        TaskPriority priority,
        DateTime? dueDate = null
    )
    {
        if(string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.");

        if(workspaceId == Guid.Empty)
            throw new ArgumentOutOfRangeException
                ("Workspace Id is required.");

        if(projectId == Guid.Empty)
            throw new ArgumentOutOfRangeException
                ("Project Id is required.");

        Title = title;
        Description = description;
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        Priority = priority;
        DueDate = dueDate;
        Status = TaskItemStatus.Todo;
        CreatedAt = DateTime.UtcNow;

        Id = Guid.NewGuid();
    }

    public void AssignTo(Guid userId)
    {
        EnsureNotDeleted();

        if(userId == Guid.Empty)
            throw new ArgumentException("Assignee id must not be empty.");

        AssigneeUserId = userId;

        UpdatedAt = DateTime.UtcNow;
    }

    public void Unassign()
    {
        EnsureNotDeleted();

        if (AssigneeUserId is null)
            return;

        AssigneeUserId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        EnsureNotDeleted();

        if(Status != TaskItemStatus.Todo)
            throw new InvalidTaskStatusTransitionException(
                Status,
                TaskItemStatus.InProgress);

        Status = TaskItemStatus.InProgress;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        EnsureNotDeleted();

        if(Status != TaskItemStatus.Todo &&
           Status != TaskItemStatus.InProgress)
        {
            throw new InvalidTaskStatusTransitionException(
                Status,
                TaskItemStatus.Done);
        }
        
        Status = TaskItemStatus.Done;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        EnsureNotDeleted();

        if(Status != TaskItemStatus.Done)
            throw new InvalidTaskStatusTransitionException(
                Status,
                TaskItemStatus.Todo);

        Status = TaskItemStatus.Todo;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        if(IsDeleted)
            throw new TaskAlreadyDeletedException(Id);

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    // TaskAlreadyDeletedException, not a bare InvalidOperationException:
    // ExceptionMiddleware maps this type to 409 and anything unrecognised to
    // 500. The mapping exists precisely so that a repository method written
    // without an IsDeleted filter surfaces the domain rule rather than an
    // unexplained server error, and a guard that threw an unmapped type would
    // hand that back. Matches how Project guards itself.
    private void EnsureNotDeleted()
    {
        if(IsDeleted)
            throw new TaskAlreadyDeletedException(Id);
    }

    public void UpdateDetails(
        string title,
        string description,
        TaskPriority priority,
        DateTime? dueDate
    )
    {
        EnsureNotDeleted();

        if(string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "Title is required.");

        Title = title;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
        UpdatedAt = DateTime.UtcNow;
    }
}
