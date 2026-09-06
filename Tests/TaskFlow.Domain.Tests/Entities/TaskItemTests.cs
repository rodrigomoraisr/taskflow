using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Tests.Entities;

public class TaskItemTests
{
    private static TaskItem CreateTask()
    {
        return new TaskItem(
            "Automated test task",
            "Task used by domain unit tests",
            Guid.NewGuid(),
            Guid.NewGuid(),
            TaskPriority.Medium,
            null);
    }

    [Fact]
    public void Start_WhenTaskIsTodo_ShouldChangeStatusToInProgress()
    {
        // Arrange
        var task = CreateTask();

        // Act
        task.Start();

        // Assert
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
    }

    [Fact]
    public void Start_WhenTaskIsAlreadyInProgress_ShouldThrowInvalidTaskStatusTransitionException()
    {
        // Arrange
        var task = CreateTask();

        task.Start();

        // Act + Assert
        Assert.Throws<InvalidTaskStatusTransitionException>(
            () => task.Start());
    }

    [Fact]
    public void Complete_WhenTaskIsTodo_ShouldChangeStatusToDone()
    {
        // Arrange
        var task = CreateTask();

        // Act
        task.Complete();

        // Assert
        Assert.Equal(TaskItemStatus.Done, task.Status);
    }

    [Fact]
    public void Complete_WhenTaskIsInProgress_ShouldChangeStatusToDone()
    {
        // Arrange
        var task = CreateTask();
        task.Start();

        // Act
        task.Complete();

        // Assert
        Assert.Equal(TaskItemStatus.Done, task.Status);
    }

    [Fact]
    public void Complete_WhenTaskIsAlreadyDone_ShouldThrowInvalidTaskStatusTransitionException()
    {
        // Arrange
        var task = CreateTask();
        task.Complete();

        // Act + Assert
        Assert.Throws<InvalidTaskStatusTransitionException>(
            () => task.Complete());
    }

    [Fact]
    public void Reopen_WhenTaskIsDone_ShouldChangeStatusToTodo()
    {
        // Arrange
        var task = CreateTask();
        task.Complete();

        // Act
        task.Reopen();

        // Assert
        Assert.Equal(TaskItemStatus.Todo, task.Status);
    }

    [Fact]
    public void Reopen_WhenTaskIsTodo_ShouldThrowInvalidTaskStatusTransitionException()
    {
        // Arrange
        var task = CreateTask();

        // Act + Assert
        Assert.Throws<InvalidTaskStatusTransitionException>(
            () => task.Reopen());
    }

    [Fact]
    public void Reopen_WhenTaskIsInProgress_ShouldThrowInvalidTaskStatusTransitionException()
    {
        // Arrange
        var task = CreateTask();
        task.Start();

        // Act + Assert
        Assert.Throws<InvalidTaskStatusTransitionException>(
            () => task.Reopen());
    }

    [Fact]
    public void AssignTo_WhenUserIdIsValid_ShouldAssignUser()
    {
        // Arrange
        var task = CreateTask();
        var userId = Guid.NewGuid();

        // Act
        task.AssignTo(userId);

        // Assert
        Assert.Equal(userId, task.AssigneeUserId);
    }

    [Fact]
    public void AssignTo_WhenUserIdIsEmpty_ShouldThrowArgumentException()
    {
        // Arrange
        var task = CreateTask();

        // Act + Assert
        Assert.Throws<ArgumentException>(
            () => task.AssignTo(Guid.Empty));
    }

    [Fact]
    public void Unassign_WhenTaskHasAssignee_ShouldRemoveAssignee()
    {
        // Arrange
        var task = CreateTask();
        task.AssignTo(Guid.NewGuid());

        // Act
        task.Unassign();

        // Assert
        Assert.Null(task.AssigneeUserId);
    }

    [Fact]
    public void Unassign_WhenTaskIsAlreadyUnassigned_ShouldRemainUnassigned()
    {
        // Arrange
        var task = CreateTask();

        // Act
        task.Unassign();

        // Assert
        Assert.Null(task.AssigneeUserId);
    }

    [Fact]
    public void Delete_WhenTaskIsActive_ShouldMarkTaskAsDeleted()
    {
        // Arrange
        var task = CreateTask();

        // Act
        task.Delete();

        // Assert
        Assert.True(task.IsDeleted);
        Assert.NotNull(task.DeletedAt);
    }

    [Fact]
    public void Delete_WhenTaskIsAlreadyDeleted_ShouldThrowTaskAlreadyDeletedException()
    {
        // Arrange
        var task = CreateTask();
        task.Delete();

        // Act + Assert
        Assert.Throws<TaskAlreadyDeletedException>(
            () => task.Delete());
    }

    [Fact]
    public void Constructor_WhenWorkspaceIdIsEmpty_ShouldThrowArgumentOutOfRangeException()
    {
        // Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TaskItem(
                "Task",
                "Description",
                Guid.Empty,
                Guid.NewGuid(),
                TaskPriority.Medium,
                null));
    }

    [Fact]
    public void Constructor_WhenProjectIdIsEmpty_ShouldThrowArgumentOutOfRangeException()
    {
        // Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TaskItem(
                "Task",
                "Description",
                Guid.NewGuid(),
                Guid.Empty,
                TaskPriority.Medium,
                null));
    }

    [Fact]
    public void Start_WhenTaskIsDone_ShouldThrowInvalidTaskStatusTransitionException()
    {
        // Arrange
        var task = CreateTask();

        task.Complete();

        // Act + Assert
        Assert.Throws<InvalidTaskStatusTransitionException>(
            () => task.Start());
    }

    [Fact]
    public void Complete_WhenTaskIsAlreadyDoneAfterReopen_ShouldChangeStatusToDone()
    {
        // Arrange
        var task = CreateTask();

        task.Complete();
        task.Reopen();

        // Act
        task.Complete();

        // Assert
        Assert.Equal(TaskItemStatus.Done, task.Status);
    }

    [Fact]
    public void AssignTo_WhenTaskIsAlreadyAssigned_ShouldReplaceTheAssignee()
    {
        // Arrange
        var task = CreateTask();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        task.AssignTo(first);

        // Act
        task.AssignTo(second);

        // Assert
        Assert.Equal(second, task.AssigneeUserId);
    }

    [Fact]
    public void UpdateDetails_WhenTaskIsActive_ShouldReplaceEveryDetail()
    {
        // Arrange
        var task = CreateTask();
        var dueDate = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        task.UpdateDetails(
            "Renamed task",
            "Rewritten description",
            TaskPriority.Critical,
            dueDate);

        // Assert
        Assert.Equal("Renamed task", task.Title);
        Assert.Equal("Rewritten description", task.Description);
        Assert.Equal(TaskPriority.Critical, task.Priority);
        Assert.Equal(dueDate, task.DueDate);
        Assert.NotNull(task.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WhenTitleIsBlank_ShouldThrowArgumentException(
        string title)
    {
        // Arrange
        var task = CreateTask();

        // Act + Assert
        Assert.Throws<ArgumentException>(
            () => task.UpdateDetails(
                title,
                "Description",
                TaskPriority.Low,
                null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenTitleIsBlank_ShouldThrowArgumentException(
        string title)
    {
        Assert.Throws<ArgumentException>(() =>
            new TaskItem(
                title,
                "Description",
                Guid.NewGuid(),
                Guid.NewGuid(),
                TaskPriority.Medium,
                null));
    }

    // -----------------------------------------------------------------
    // Guards on a soft-deleted task.
    //
    // One test per mutating method rather than one representative test: a
    // single case would still pass if a later method dropped its
    // EnsureNotDeleted call, which is exactly the regression worth catching.
    //
    // Every one of them expects TaskAlreadyDeletedException specifically.
    // ExceptionMiddleware maps that type to 409 and anything it does not
    // recognise to 500, so a guard throwing a bare InvalidOperationException
    // would turn a stated domain rule into an unexplained server error the
    // first time a repository forgot its IsDeleted filter.
    // -----------------------------------------------------------------

    [Fact]
    public void AssignTo_WhenTaskIsDeleted_ShouldThrowTaskAlreadyDeletedException()
    {
        // Arrange
        var task = CreateDeletedTask();

        // Act + Assert
        Assert.Throws<TaskAlreadyDeletedException>(
            () => task.AssignTo(Guid.NewGuid()));
    }

    [Fact]
    public void Unassign_WhenTaskIsDeleted_ShouldThrowTaskAlreadyDeletedException()
    {
        // Arrange
        var task = CreateDeletedTask();

        // Act + Assert
        Assert.Throws<TaskAlreadyDeletedException>(
            () => task.Unassign());
    }

    [Fact]
    public void Start_WhenTaskIsDeleted_ShouldThrowTaskAlreadyDeletedException()
    {
        // Arrange
        var task = CreateDeletedTask();

        // Act + Assert
        Assert.Throws<TaskAlreadyDeletedException>(
            () => task.Start());
    }

    [Fact]
    public void Complete_WhenTaskIsDeleted_ShouldThrowTaskAlreadyDeletedException()
    {
        // Arrange
        var task = CreateDeletedTask();

        // Act + Assert
        Assert.Throws<TaskAlreadyDeletedException>(
            () => task.Complete());
    }

    [Fact]
    public void Reopen_WhenTaskIsDeleted_ShouldThrowTaskAlreadyDeletedException()
    {
        // Arrange
        var task = CreateDeletedTask();

        // Act + Assert
        Assert.Throws<TaskAlreadyDeletedException>(
            () => task.Reopen());
    }

    [Fact]
    public void UpdateDetails_WhenTaskIsDeleted_ShouldThrowTaskAlreadyDeletedException()
    {
        // Arrange
        var task = CreateDeletedTask();

        // Act + Assert
        Assert.Throws<TaskAlreadyDeletedException>(
            () => task.UpdateDetails(
                "Renamed",
                "Rewritten",
                TaskPriority.Low,
                null));
    }

    /// <summary>
    /// The deleted guard fires before the transition check, so a deleted task
    /// reports being deleted rather than reporting an illegal transition. The
    /// two rules are both real; this pins which one answers first.
    /// </summary>
    [Fact]
    public void Reopen_WhenTaskIsDeletedAndTheTransitionIsAlsoIllegal_ShouldReportTheDeletion()
    {
        // Arrange
        var task = CreateTask();

        task.Delete();

        // Act + Assert
        Assert.Throws<TaskAlreadyDeletedException>(
            () => task.Reopen());
    }

    private static TaskItem CreateDeletedTask()
    {
        var task = CreateTask();

        task.Delete();

        return task;
    }
}
