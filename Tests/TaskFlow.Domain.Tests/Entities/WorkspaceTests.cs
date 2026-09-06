using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Tests.Entities;

/// <summary>
/// <see cref="Workspace"/> is the tenant boundary itself, and until 8.4 it was
/// the only entity with no unit tests at all — covered only indirectly, through
/// suites that were really asserting something else.
/// </summary>
public class WorkspaceTests
{
    [Fact]
    public void Constructor_WhenNameIsValid_ShouldCreateAnActiveWorkspace()
    {
        // Arrange
        const string name = "Engineering";

        // Act
        var workspace = new Workspace(name);

        // Assert
        Assert.Equal(name, workspace.Name);
        Assert.False(workspace.IsDeleted);
        Assert.Null(workspace.DeletedAt);
        Assert.Null(workspace.UpdatedAt);
        Assert.NotEqual(default, workspace.CreatedAt);
    }

    /// <summary>
    /// The id is generated in the constructor rather than left to EF Core's
    /// key-generation convention. Anything that never reaches the database —
    /// the service tests, which mock every repository — would otherwise hold a
    /// workspace whose id is <c>Guid.Empty</c>, making two tenants
    /// indistinguishable and every isolation assertion built on them
    /// meaningless.
    /// </summary>
    [Fact]
    public void Constructor_WhenCalledTwice_ShouldGiveEachWorkspaceItsOwnId()
    {
        // Arrange + Act
        var first = new Workspace("First");
        var second = new Workspace("Second");

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Constructor_WhenNameContainsWhitespace_ShouldTrimName()
    {
        // Arrange + Act
        var workspace = new Workspace("  Engineering  ");

        // Assert
        Assert.Equal("Engineering", workspace.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenNameIsBlank_ShouldThrowArgumentException(
        string name)
    {
        Assert.Throws<ArgumentException>(() => new Workspace(name));
    }

    [Fact]
    public void Rename_WhenWorkspaceIsActive_ShouldChangeTheNameAndStamp()
    {
        // Arrange
        var workspace = new Workspace("Engineering");

        // Act
        workspace.Rename("  Platform  ");

        // Assert
        Assert.Equal("Platform", workspace.Name);
        Assert.NotNull(workspace.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WhenNameIsBlank_ShouldThrowArgumentException(string name)
    {
        // Arrange
        var workspace = new Workspace("Engineering");

        // Act + Assert
        Assert.Throws<ArgumentException>(() => workspace.Rename(name));
    }

    [Fact]
    public void Rename_WhenWorkspaceIsDeleted_ShouldThrowWorkspaceAlreadyDeletedException()
    {
        // Arrange
        var workspace = new Workspace("Engineering");

        workspace.Delete();

        // Act + Assert
        Assert.Throws<WorkspaceAlreadyDeletedException>(
            () => workspace.Rename("Platform"));
    }

    /// <summary>
    /// The deleted guard fires before the blank-name check, so a deleted
    /// workspace reports the deletion rather than the invalid argument.
    /// </summary>
    [Fact]
    public void Rename_WhenWorkspaceIsDeletedAndNameIsAlsoBlank_ShouldReportTheDeletion()
    {
        // Arrange
        var workspace = new Workspace("Engineering");

        workspace.Delete();

        // Act + Assert
        Assert.Throws<WorkspaceAlreadyDeletedException>(
            () => workspace.Rename("   "));
    }

    [Fact]
    public void Delete_WhenWorkspaceIsActive_ShouldMarkItAsDeleted()
    {
        // Arrange
        var workspace = new Workspace("Engineering");

        // Act
        workspace.Delete();

        // Assert
        Assert.True(workspace.IsDeleted);
        Assert.NotNull(workspace.DeletedAt);
        Assert.NotNull(workspace.UpdatedAt);
    }

    [Fact]
    public void Delete_WhenWorkspaceIsAlreadyDeleted_ShouldThrowWorkspaceAlreadyDeletedException()
    {
        // Arrange
        var workspace = new Workspace("Engineering");

        workspace.Delete();

        // Act + Assert
        Assert.Throws<WorkspaceAlreadyDeletedException>(
            () => workspace.Delete());
    }
}
