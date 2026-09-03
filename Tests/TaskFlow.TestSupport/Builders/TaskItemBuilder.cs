using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.TestSupport.Builders;

/// <summary>
/// Builds <see cref="TaskItem"/> instances for tests.
///
/// Requested state is always reached by calling the entity's own transition
/// methods, never by reflecting over the private setters. A builder that wrote
/// state directly could produce combinations the domain forbids, and a test
/// asserting against one of those would be verifying fiction.
/// </summary>
public sealed class TaskItemBuilder
{
    private string _title = "Task title";
    private string _description = "Task description";
    private Guid _workspaceId = Guid.NewGuid();
    private Guid _projectId = Guid.NewGuid();
    private TaskPriority _priority = TaskPriority.Medium;
    private DateTime? _dueDate;
    private Guid? _assigneeUserId;
    private TaskItemStatus _status = TaskItemStatus.Todo;
    private bool _isDeleted;

    public TaskItemBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TaskItemBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Tenant the task belongs to. Tenant-isolation tests set this explicitly on
    /// both sides of the boundary; everything else can take the random default.
    /// </summary>
    public TaskItemBuilder InWorkspace(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    public TaskItemBuilder InProject(Guid projectId)
    {
        _projectId = projectId;
        return this;
    }

    public TaskItemBuilder WithPriority(TaskPriority priority)
    {
        _priority = priority;
        return this;
    }

    public TaskItemBuilder DueOn(DateTime? dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public TaskItemBuilder AssignedTo(Guid assigneeUserId)
    {
        _assigneeUserId = assigneeUserId;
        return this;
    }

    public TaskItemBuilder WithStatus(TaskItemStatus status)
    {
        _status = status;
        return this;
    }

    public TaskItemBuilder Deleted()
    {
        _isDeleted = true;
        return this;
    }

    public TaskItem Build()
    {
        var task = new TaskItem(
            _title,
            _description,
            _workspaceId,
            _projectId,
            _priority,
            _dueDate);

        // Assign before any transition, and before deleting: a deleted task
        // rejects every mutation.
        if (_assigneeUserId is not null)
            task.AssignTo(_assigneeUserId.Value);

        MoveTo(task, _status);

        if (_isDeleted)
            task.Delete();

        return task;
    }

    private static void MoveTo(
        TaskItem task,
        TaskItemStatus status)
    {
        switch (status)
        {
            case TaskItemStatus.Todo:
                break;

            case TaskItemStatus.InProgress:
                task.Start();
                break;

            case TaskItemStatus.Done:
                task.Complete();
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "Unknown task status.");
        }
    }
}
