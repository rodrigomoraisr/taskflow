using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Tests.Entities;

public class WorkspaceUserTests
{
    [Fact]
    public void Constructor_WhenDataIsValid_ShouldCreateActiveMembership()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        // Act
        var membership = new WorkspaceUser(
            userId,
            workspaceId,
            WorkspaceRole.Member);

        // Assert
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(workspaceId, membership.WorkspaceId);
        Assert.Equal(WorkspaceRole.Member, membership.Role);
        Assert.False(membership.IsDeleted);
        Assert.Null(membership.DeletedAt);
    }

    [Fact]
    public void Constructor_WhenUserIdIsEmpty_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkspaceUser(
                Guid.Empty,
                Guid.NewGuid(),
                WorkspaceRole.Member));
    }

    [Fact]
    public void Constructor_WhenWorkspaceIdIsEmpty_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkspaceUser(
                Guid.NewGuid(),
                Guid.Empty,
                WorkspaceRole.Member));
    }

    [Fact]
    public void RemoveFromWorkspace_WhenMembershipIsActive_ShouldMarkAsDeleted()
    {
        // Arrange
        var membership = CreateMembership();

        // Act
        membership.RemoveFromWorkspace();

        // Assert
        Assert.True(membership.IsDeleted);
        Assert.NotNull(membership.DeletedAt);
        Assert.NotNull(membership.UpdatedAt);
    }

    [Fact]
    public void RemoveFromWorkspace_WhenMembershipIsAlreadyDeleted_ShouldThrowWorkspaceUserAlreadyDeletedException()
    {
        // Arrange
        var membership = CreateMembership();
        membership.RemoveFromWorkspace();

        // Act + Assert
        Assert.Throws<WorkspaceUserAlreadyDeletedException>(
            () => membership.RemoveFromWorkspace());
    }

    [Fact]
    public void RestoreMembership_WhenMembershipIsDeleted_ShouldRestoreMembership()
    {
        // Arrange
        var membership = CreateMembership();
        membership.RemoveFromWorkspace();

        // Act
        membership.RestoreMembership();

        // Assert
        Assert.False(membership.IsDeleted);
        Assert.Null(membership.DeletedAt);
        Assert.NotNull(membership.UpdatedAt);
    }

    [Fact]
    public void RestoreMembership_WhenMembershipIsAlreadyActive_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var membership = CreateMembership();

        // Act + Assert
        Assert.Throws<InvalidOperationException>(
            () => membership.RestoreMembership());
    }

    [Fact]
    public void ChangeRole_WhenMembershipIsActive_ShouldChangeRole()
    {
        // Arrange
        var membership = CreateMembership();

        // Act
        membership.ChangeRole(WorkspaceRole.Admin);

        // Assert
        Assert.Equal(WorkspaceRole.Admin, membership.Role);
        Assert.NotNull(membership.UpdatedAt);
    }

    [Fact]
    public void ChangeRole_WhenMembershipIsDeleted_ShouldThrowWorkspaceUserAlreadyDeletedException()
    {
        // Arrange
        var membership = CreateMembership();
        membership.RemoveFromWorkspace();

        // Act + Assert
        Assert.Throws<WorkspaceUserAlreadyDeletedException>(
            () => membership.ChangeRole(WorkspaceRole.Admin));
    }

    private static WorkspaceUser CreateMembership()
    {
        return new WorkspaceUser(
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkspaceRole.Member);
    }
}