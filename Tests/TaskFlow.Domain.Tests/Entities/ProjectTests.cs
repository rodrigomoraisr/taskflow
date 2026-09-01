using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Tests.Entities;

public class ProjectTests
{
    private static Project CreateProject()
    {
        return new Project(
            Guid.NewGuid(),
            "TaskFlow",
            "TaskFlow test project");
    }

    [Fact]
    public void Constructor_WhenDataIsValid_ShouldCreateActiveProject()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var project = new Project(
            workspaceId,
            "Automated Tests",
            "Implement Phase 8 tests");

        // Assert
        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal(workspaceId, project.WorkspaceId);
        Assert.Equal("Automated Tests", project.Name);
        Assert.Equal("Implement Phase 8 tests", project.Description);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.False(project.IsDeleted);
        Assert.Null(project.DeletedAt);
    }

    [Fact]
    public void Constructor_WhenWorkspaceIdIsEmpty_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Project(
                Guid.Empty,
                "Project",
                "Description"));
    }

    [Fact]
    public void Constructor_WhenNameIsEmpty_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Project(
                Guid.NewGuid(),
                "",
                "Description"));
    }

    [Fact]
    public void Constructor_WhenNameContainsWhitespace_ShouldTrimName()
    {
        // Act
        var project = new Project(
            Guid.NewGuid(),
            "   TaskFlow   ",
            "Description");

        // Assert
        Assert.Equal("TaskFlow", project.Name);
    }

    [Fact]
    public void Constructor_WhenDescriptionIsNull_ShouldUseEmptyString()
    {
        // Act
        var project = new Project(
            Guid.NewGuid(),
            "Project",
            null!);

        // Assert
        Assert.Equal(string.Empty, project.Description);
    }

    [Fact]
    public void UpdateDetails_WhenProjectIsActive_ShouldUpdateNameAndDescription()
    {
        // Arrange
        var project = CreateProject();

        // Act
        project.UpdateDetails(
            "Updated Project",
            "Updated Description");

        // Assert
        Assert.Equal("Updated Project", project.Name);
        Assert.Equal("Updated Description", project.Description);
        Assert.NotNull(project.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_WhenNameIsEmpty_ShouldThrowArgumentException()
    {
        // Arrange
        var project = CreateProject();

        // Act + Assert
        Assert.Throws<ArgumentException>(() =>
            project.UpdateDetails("", "Description"));
    }

    [Fact]
    public void ChangeStatus_WhenStatusIsValid_ShouldUpdateStatus()
    {
        // Arrange
        var project = CreateProject();

        // Act
        project.ChangeStatus(ProjectStatus.Completed);

        // Assert
        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.NotNull(project.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_WhenStatusIsInvalid_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var project = CreateProject();

        var invalidStatus = (ProjectStatus)999;

        // Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            project.ChangeStatus(invalidStatus));
    }

    [Fact]
    public void Delete_WhenProjectIsActive_ShouldMarkProjectAsDeleted()
    {
        // Arrange
        var project = CreateProject();

        // Act
        project.Delete();

        // Assert
        Assert.True(project.IsDeleted);
        Assert.NotNull(project.DeletedAt);
        Assert.NotNull(project.UpdatedAt);
    }

    [Fact]
    public void Delete_WhenProjectIsAlreadyDeleted_ShouldThrowProjectAlreadyDeletedException()
    {
        // Arrange
        var project = CreateProject();
        project.Delete();

        // Act + Assert
        Assert.Throws<ProjectAlreadyDeletedException>(() =>
            project.Delete());
    }

    [Fact]
    public void UpdateDetails_WhenProjectIsDeleted_ShouldThrowProjectAlreadyDeletedException()
    {
        // Arrange
        var project = CreateProject();
        project.Delete();

        // Act + Assert
        Assert.Throws<ProjectAlreadyDeletedException>(() =>
            project.UpdateDetails("New Name", "New Description"));
    }

    [Fact]
    public void ChangeStatus_WhenProjectIsDeleted_ShouldThrowProjectAlreadyDeletedException()
    {
        // Arrange
        var project = CreateProject();
        project.Delete();

        // Act + Assert
        Assert.Throws<ProjectAlreadyDeletedException>(() =>
            project.ChangeStatus(ProjectStatus.Completed));
    }
}