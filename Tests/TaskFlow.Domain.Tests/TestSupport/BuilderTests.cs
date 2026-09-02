using TaskFlow.Domain.Enums;
using TaskFlow.TestSupport.Builders;

namespace TaskFlow.Domain.Tests.TestSupport;

/// <summary>
/// The builders are shared by all three test projects, so a wrong default or a
/// transition applied in the wrong order would quietly weaken every suite built
/// on top of them. These tests pin the parts other tests rely on.
/// </summary>
public class BuilderTests
{
    [Fact]
    public void TaskItemBuilder_WithNoConfiguration_ShouldProduceAnActiveTodoTask()
    {
        // Arrange
        var builder = new TaskItemBuilder();

        // Act
        var task = builder.Build();

        // Assert
        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.False(task.IsDeleted);
        Assert.Null(task.AssigneeUserId);
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.NotEqual(Guid.Empty, task.WorkspaceId);
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done)]
    public void TaskItemBuilder_WithStatus_ShouldReachThatStatus(
        TaskItemStatus status)
    {
        // Arrange
        var builder = new TaskItemBuilder().WithStatus(status);

        // Act
        var task = builder.Build();

        // Assert
        Assert.Equal(status, task.Status);
    }

    [Fact]
    public void TaskItemBuilder_WithBlockedStatus_ShouldThrowBecauseTheDomainHasNoSuchTransition()
    {
        // Arrange
        var builder = new TaskItemBuilder()
            .WithStatus(TaskItemStatus.Blocked);

        // Act + Assert
        Assert.Throws<NotSupportedException>(() => builder.Build());
    }

    [Fact]
    public void TaskItemBuilder_WhenAssignedAndDeleted_ShouldKeepTheAssigneeItWasGiven()
    {
        // Arrange
        var assignee = Guid.NewGuid();

        // Act
        var task = new TaskItemBuilder()
            .AssignedTo(assignee)
            .Deleted()
            .Build();

        // Assert
        Assert.Equal(assignee, task.AssigneeUserId);
        Assert.True(task.IsDeleted);
    }

    [Fact]
    public void TaskItemBuilder_InWorkspace_ShouldPlaceTheTaskInThatTenant()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var task = new TaskItemBuilder()
            .InWorkspace(workspaceId)
            .Build();

        // Assert
        Assert.Equal(workspaceId, task.WorkspaceId);
    }

    [Fact]
    public void ProjectBuilder_WithNoConfiguration_ShouldProduceAnActiveProject()
    {
        // Arrange
        var builder = new ProjectBuilder();

        // Act
        var project = builder.Build();

        // Assert
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.False(project.IsDeleted);
        Assert.NotEqual(Guid.Empty, project.Id);
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.OnHold)]
    [InlineData(ProjectStatus.Completed)]
    public void ProjectBuilder_WithStatus_ShouldReachThatStatus(
        ProjectStatus status)
    {
        // Arrange
        var builder = new ProjectBuilder().WithStatus(status);

        // Act
        var project = builder.Build();

        // Assert
        Assert.Equal(status, project.Status);
    }

    [Fact]
    public void ProjectBuilder_WhenStatusChangedAndDeleted_ShouldApplyTheStatusBeforeDeleting()
    {
        // Arrange
        var builder = new ProjectBuilder()
            .WithStatus(ProjectStatus.OnHold)
            .Deleted();

        // Act
        var project = builder.Build();

        // Assert
        Assert.Equal(ProjectStatus.OnHold, project.Status);
        Assert.True(project.IsDeleted);
    }

    [Fact]
    public void WorkspaceUserBuilder_WithNoConfiguration_ShouldProduceAnActiveMember()
    {
        // Arrange
        var builder = new WorkspaceUserBuilder();

        // Act
        var membership = builder.Build();

        // Assert
        Assert.Equal(WorkspaceRole.Member, membership.Role);
        Assert.False(membership.IsDeleted);
    }

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public void WorkspaceUserBuilder_AsRole_ShouldProduceThatRole(
        WorkspaceRole role)
    {
        // Arrange
        var builder = new WorkspaceUserBuilder().AsRole(role);

        // Act
        var membership = builder.Build();

        // Assert
        Assert.Equal(role, membership.Role);
    }

    [Fact]
    public void WorkspaceUserBuilder_Removed_ShouldSoftDeleteTheMembership()
    {
        // Arrange
        var builder = new WorkspaceUserBuilder().AsOwner().Removed();

        // Act
        var membership = builder.Build();

        // Assert
        Assert.True(membership.IsDeleted);
        Assert.NotNull(membership.DeletedAt);
        Assert.Equal(WorkspaceRole.Owner, membership.Role);
    }

    [Fact]
    public void UserBuilder_WhenBuiltTwice_ShouldProduceDistinctEmails()
    {
        // Arrange
        var first = new UserBuilder();
        var second = new UserBuilder();

        // Act
        var firstUser = first.Build();
        var secondUser = second.Build();

        // Assert
        Assert.NotEqual(firstUser.Email, secondUser.Email);
    }

    [Fact]
    public void WorkspaceBuilder_Deleted_ShouldSoftDeleteTheWorkspace()
    {
        // Arrange
        var builder = new WorkspaceBuilder().Deleted();

        // Act
        var workspace = builder.Build();

        // Assert
        Assert.True(workspace.IsDeleted);
        Assert.NotNull(workspace.DeletedAt);
    }
}
