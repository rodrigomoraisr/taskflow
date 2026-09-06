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

    [Fact]
    public void Constructor_WhenCalledTwice_ShouldGiveEachMembershipItsOwnId()
    {
        // Arrange + Act
        var first = new WorkspaceUser(
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkspaceRole.Member);

        var second = new WorkspaceUser(
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkspaceRole.Member);

        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public void ChangeRole_WhenTheRoleIsAnyDefinedValue_ShouldApplyIt(
        WorkspaceRole role)
    {
        // Arrange
        var membership = new WorkspaceUser(
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkspaceRole.Member);

        // Act
        membership.ChangeRole(role);

        // Assert
        Assert.Equal(role, membership.Role);
        Assert.NotNull(membership.UpdatedAt);
    }

    /// <summary>
    /// The last-owner rule lives in <c>WorkspaceService</c>, not here — the
    /// entity cannot see its siblings, so it cannot know whether it is the
    /// last owner. Demoting an owner is therefore legal at this level, and
    /// this test pins that division of responsibility: if the entity ever
    /// started refusing, the service's own protection would become
    /// unreachable and its tests would keep passing for the wrong reason.
    /// </summary>
    [Fact]
    public void ChangeRole_WhenDemotingAnOwner_ShouldSucceedBecauseTheRuleLivesInTheService()
    {
        // Arrange
        var membership = new WorkspaceUser(
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkspaceRole.Owner);

        // Act
        membership.ChangeRole(WorkspaceRole.Member);

        // Assert
        Assert.Equal(WorkspaceRole.Member, membership.Role);
    }

    /// <summary>
    /// Likewise for removal: the entity soft-deletes itself without asking
    /// whether it was the last owner. <c>WorkspaceService.RemoveMemberAsync</c>
    /// is the only place that check exists.
    /// </summary>
    [Fact]
    public void RemoveFromWorkspace_WhenTheMemberIsAnOwner_ShouldSucceedBecauseTheRuleLivesInTheService()
    {
        // Arrange
        var membership = new WorkspaceUser(
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkspaceRole.Owner);

        // Act
        membership.RemoveFromWorkspace();

        // Assert
        Assert.True(membership.IsDeleted);
    }

    [Fact]
    public void RestoreMembership_WhenMembershipIsRestored_ShouldClearTheDeletionStamp()
    {
        // Arrange
        var membership = new WorkspaceUser(
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkspaceRole.Member);

        membership.RemoveFromWorkspace();

        // Act
        membership.RestoreMembership();

        // Assert
        Assert.False(membership.IsDeleted);
        Assert.Null(membership.DeletedAt);
    }
}
