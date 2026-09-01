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
}