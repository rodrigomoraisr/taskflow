using NSubstitute;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Tests.TestSupport;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.TestSupport.Builders;

namespace TaskFlow.Application.Tests.Workspaces;

/// <summary>
/// <c>WorkspaceService</c> orchestration, with the real
/// <c>WorkspaceAuthorizationService</c> underneath.
///
/// Membership management is where this service earns direct tests. Two rules
/// live only here — an Admin may not touch an Owner or promote anyone to Owner,
/// and the last active Owner may be neither removed nor demoted — and both are
/// decided from a repository count that no other layer inspects.
/// </summary>
public sealed class WorkspaceServiceTests
{
    // ---------------------------------------------------------------
    // CreateAsync — the one method with no workspace to authorize against
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WhenCalled_ShouldMakeTheCallerTheOwnerAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext();
        var service = context.CreateWorkspaceService();

        // Act
        var response = await service.CreateAsync(
            context.CallerId,
            new CreateWorkspaceRequest { Name = "Fresh workspace" });

        // Assert
        await context.Workspaces
            .Received(1)
            .AddAsync(
                Arg.Is<Workspace>(workspace => workspace.Id == response.Id),
                Arg.Any<CancellationToken>());

        await context.WorkspaceUsers
            .Received(1)
            .AddAsync(
                Arg.Is<WorkspaceUser>(membership =>
                    membership.UserId == context.CallerId &&
                    membership.WorkspaceId == response.Id &&
                    membership.Role == WorkspaceRole.Owner),
                Arg.Any<CancellationToken>());

        await context.ShouldHaveCommittedOnceAsync();
    }

    // ---------------------------------------------------------------
    // GetByIdAsync / DeleteAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_WhenTheCallerIsAMember_ShouldReturnTheWorkspace()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Viewer);
        var workspace = ArrangeWorkspace(context);

        var service = context.CreateWorkspaceService();

        // Act
        var response = await service.GetByIdAsync(context.WorkspaceId);

        // Assert
        Assert.Equal(workspace.Id, response.Id);
        Assert.Equal(workspace.Name, response.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheCallerIsNotAMember_ShouldThrowUnauthorizedWorkspaceAccess()
    {
        // Arrange
        var context = new ServiceTestContext().AsNonMember();
        ArrangeWorkspace(context);

        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => service.GetByIdAsync(context.WorkspaceId));
    }

    [Fact]
    public async Task DeleteAsync_WhenTheCallerIsTheOwner_ShouldSoftDeleteAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var workspace = ArrangeWorkspace(context);

        var service = context.CreateWorkspaceService();

        // Act
        await service.DeleteAsync(context.WorkspaceId);

        // Assert
        Assert.True(workspace.IsDeleted);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task DeleteAsync_WhenTheCallerIsNotTheOwner_ShouldThrowAndLeaveItActive(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var workspace = ArrangeWorkspace(context);

        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.DeleteAsync(context.WorkspaceId));

        Assert.False(workspace.IsDeleted);
        await context.ShouldNotHaveCommittedAsync();
    }

    // ---------------------------------------------------------------
    // GetForUserAsync
    // ---------------------------------------------------------------

    /// <summary>
    /// The only listing in the system with no workspace in the route: it is
    /// scoped by the caller's own memberships instead. The workspaces asked for
    /// must be exactly the ones those memberships name, or a user would see a
    /// tenant they do not belong to.
    /// </summary>
    [Fact]
    public async Task GetForUserAsync_WhenCalled_ShouldFetchOnlyTheWorkspacesTheUserBelongsTo()
    {
        // Arrange
        var context = new ServiceTestContext();

        var joined = new WorkspaceBuilder().WithName("Joined").Build();

        context.WorkspaceUsers
            .GetActiveMembershipsAsync(
                context.CallerId,
                Arg.Any<CancellationToken>())
            .Returns([
                new WorkspaceUserBuilder()
                    .ForUser(context.CallerId)
                    .InWorkspace(joined.Id)
                    .AsAdmin()
                    .Build()
            ]);

        context.Workspaces
            .GetByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns([joined]);

        var service = context.CreateWorkspaceService();

        // Act
        var response = await service.GetForUserAsync(context.CallerId);

        // Assert
        Assert.Single(response);
        Assert.Equal(joined.Id, response[0].Id);
        Assert.Equal(nameof(WorkspaceRole.Admin), response[0].Role);

        await context.Workspaces
            .Received(1)
            .GetByIdsAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Count == 1 && ids.Contains(joined.Id)),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Membership management
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Admin)]
    public async Task AddMemberAsync_WhenTheCallerManagesMembers_ShouldAddThemAndCommitOnce(
        WorkspaceRole actorRole)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(actorRole);
        var invitee = new UserBuilder().WithEmail("invitee@taskflow.test").Build();

        context.Users
            .GetByEmailAsync("invitee@taskflow.test", Arg.Any<CancellationToken>())
            .Returns(invitee);

        var service = context.CreateWorkspaceService();

        // Act
        var response = await service.AddMemberAsync(
            context.WorkspaceId,
            new AddWorkspaceMemberRequest { Email = "Invitee@TaskFlow.test" });

        // Assert
        Assert.Equal(invitee.Id, response.UserId);
        Assert.Equal(nameof(WorkspaceRole.Member), response.Role);

        await context.WorkspaceUsers
            .Received(1)
            .AddAsync(
                Arg.Is<WorkspaceUser>(membership =>
                    membership.UserId == invitee.Id &&
                    membership.WorkspaceId == context.WorkspaceId),
                Arg.Any<CancellationToken>());

        await context.ShouldHaveCommittedOnceAsync();
    }

    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task AddMemberAsync_WhenTheRoleCannotManageMembers_ShouldThrowAndNotCommit(
        WorkspaceRole actorRole)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(actorRole);
        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.AddMemberAsync(
                context.WorkspaceId,
                new AddWorkspaceMemberRequest { Email = "invitee@taskflow.test" }));

        await context.ShouldNotHaveCommittedAsync();
    }

    /// <summary>
    /// Re-adding someone who was removed restores their existing row rather
    /// than inserting a second one, because (UserId, WorkspaceId) is unique.
    /// </summary>
    [Fact]
    public async Task AddMemberAsync_WhenTheMemberWasPreviouslyRemoved_ShouldRestoreTheSameMembership()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var returning = new UserBuilder().WithEmail("returning@taskflow.test").Build();

        var removed = new WorkspaceUserBuilder()
            .ForUser(returning.Id)
            .InWorkspace(context.WorkspaceId)
            .AsViewer()
            .Removed()
            .Build();

        context.Users
            .GetByEmailAsync("returning@taskflow.test", Arg.Any<CancellationToken>())
            .Returns(returning);

        context.WorkspaceUsers
            .GetByUserAndWorkspaceAsync(
                returning.Id,
                context.WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(removed);

        var service = context.CreateWorkspaceService();

        // Act
        await service.AddMemberAsync(
            context.WorkspaceId,
            new AddWorkspaceMemberRequest { Email = "returning@taskflow.test" });

        // Assert
        Assert.False(removed.IsDeleted);
        Assert.Equal(WorkspaceRole.Member, removed.Role);

        await context.WorkspaceUsers
            .DidNotReceive()
            .AddAsync(Arg.Any<WorkspaceUser>(), Arg.Any<CancellationToken>());

        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task AddMemberAsync_WhenTheMemberIsAlreadyActive_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var existing = new UserBuilder().WithEmail("existing@taskflow.test").Build();

        context.Users
            .GetByEmailAsync("existing@taskflow.test", Arg.Any<CancellationToken>())
            .Returns(existing);

        context.WorkspaceUsers
            .GetByUserAndWorkspaceAsync(
                existing.Id,
                context.WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(new WorkspaceUserBuilder()
                .ForUser(existing.Id)
                .InWorkspace(context.WorkspaceId)
                .AsMember()
                .Build());

        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<WorkspaceMemberAlreadyExistsException>(
            () => service.AddMemberAsync(
                context.WorkspaceId,
                new AddWorkspaceMemberRequest { Email = "existing@taskflow.test" }));

        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenTheRoleIsNotADefinedEnumValue_ShouldThrowBeforeAuthorizing()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidWorkspaceRoleException>(
            () => service.ChangeMemberRoleAsync(
                context.WorkspaceId,
                Guid.NewGuid(),
                new ChangeWorkspaceMemberRoleRequest { Role = (WorkspaceRole)99 }));

        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenAnOwnerPromotesAMember_ShouldChangeTheRoleAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var target = ArrangeMember(context, WorkspaceRole.Member);

        var service = context.CreateWorkspaceService();

        // Act
        await service.ChangeMemberRoleAsync(
            context.WorkspaceId,
            target.UserId,
            new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Admin });

        // Assert
        Assert.Equal(WorkspaceRole.Admin, target.Role);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenAnAdminPromotesSomeoneToOwner_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Admin);
        var target = ArrangeMember(context, WorkspaceRole.Member);

        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.ChangeMemberRoleAsync(
                context.WorkspaceId,
                target.UserId,
                new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Owner }));

        Assert.Equal(WorkspaceRole.Member, target.Role);
        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenAnAdminDemotesAnOwner_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Admin);
        var target = ArrangeMember(context, WorkspaceRole.Owner);

        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.ChangeMemberRoleAsync(
                context.WorkspaceId,
                target.UserId,
                new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Member }));

        Assert.Equal(WorkspaceRole.Owner, target.Role);
        await context.ShouldNotHaveCommittedAsync();
    }

    /// <summary>
    /// The whole last-owner rule reduces to comparing
    /// <c>CountActiveOwnersAsync</c> against one, and the count must be taken
    /// for the workspace in the request. A count that strayed across tenants
    /// would let the last owner of this workspace demote themselves as soon as
    /// any other workspace had two.
    /// </summary>
    [Fact]
    public async Task ChangeMemberRoleAsync_WhenDemotingTheLastOwner_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var target = ArrangeMember(context, WorkspaceRole.Owner);

        context.WorkspaceUsers
            .CountActiveOwnersAsync(
                context.WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(1);

        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<LastWorkspaceOwnerException>(
            () => service.ChangeMemberRoleAsync(
                context.WorkspaceId,
                target.UserId,
                new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Admin }));

        Assert.Equal(WorkspaceRole.Owner, target.Role);

        await context.WorkspaceUsers
            .Received(1)
            .CountActiveOwnersAsync(
                context.WorkspaceId,
                Arg.Any<CancellationToken>());

        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenAnotherOwnerRemains_ShouldAllowTheDemotion()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var target = ArrangeMember(context, WorkspaceRole.Owner);

        context.WorkspaceUsers
            .CountActiveOwnersAsync(
                context.WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(2);

        var service = context.CreateWorkspaceService();

        // Act
        await service.ChangeMemberRoleAsync(
            context.WorkspaceId,
            target.UserId,
            new ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Admin });

        // Assert
        Assert.Equal(WorkspaceRole.Admin, target.Role);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenTheTargetIsARegularMember_ShouldRemoveThemAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Admin);
        var target = ArrangeMember(context, WorkspaceRole.Member);

        var service = context.CreateWorkspaceService();

        // Act
        await service.RemoveMemberAsync(context.WorkspaceId, target.UserId);

        // Assert
        Assert.True(target.IsDeleted);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenRemovingTheLastOwner_ShouldThrowAndLeaveThemInPlace()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var target = ArrangeMember(context, WorkspaceRole.Owner);

        context.WorkspaceUsers
            .CountActiveOwnersAsync(
                context.WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(1);

        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<LastWorkspaceOwnerException>(
            () => service.RemoveMemberAsync(context.WorkspaceId, target.UserId));

        Assert.False(target.IsDeleted);
        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenTheMemberIsNotInThisWorkspace_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var stranger = Guid.NewGuid();

        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<WorkspaceMemberNotFoundException>(
            () => service.RemoveMemberAsync(context.WorkspaceId, stranger));

        await context.WorkspaceUsers
            .Received(1)
            .GetByUserAndWorkspaceAsync(
                stranger,
                context.WorkspaceId,
                Arg.Any<CancellationToken>());

        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task GetMembersAsync_WhenTheCallerManagesMembers_ShouldListWithTheRequestWorkspaceId()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Admin);
        var member = new UserBuilder().Build();

        context.WorkspaceUsers
            .GetActiveByWorkspaceAsync(
                context.WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns([
                new WorkspaceUserBuilder()
                    .ForUser(member.Id)
                    .InWorkspace(context.WorkspaceId)
                    .AsMember()
                    .Build()
            ]);

        context.Users
            .GetByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns([member]);

        var service = context.CreateWorkspaceService();

        // Act
        var members = await service.GetMembersAsync(context.WorkspaceId);

        // Assert
        Assert.Single(members);
        Assert.Equal(member.Id, members[0].UserId);
        Assert.Equal(member.Email, members[0].Email);

        await context.WorkspaceUsers
            .Received(1)
            .GetActiveByWorkspaceAsync(
                context.WorkspaceId,
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task GetMembersAsync_WhenTheRoleCannotManageMembers_ShouldThrow(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var service = context.CreateWorkspaceService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.GetMembersAsync(context.WorkspaceId));
    }

    // ---------------------------------------------------------------

    /// <summary>
    /// The workspace the route points at, as the repository would hand it
    /// back. Its own id is whatever the domain constructor generated — the
    /// substitute is keyed on <c>context.WorkspaceId</c>, so a service that
    /// looked the workspace up by any other id gets null and the test fails
    /// with a not-found rather than a wrong answer.
    /// </summary>
    private static Workspace ArrangeWorkspace(ServiceTestContext context)
    {
        var workspace = new WorkspaceBuilder().Build();

        context.Workspaces
            .GetByIdAsync(context.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(workspace);

        return workspace;
    }

    private static WorkspaceUser ArrangeMember(
        ServiceTestContext context,
        WorkspaceRole role)
    {
        var userId = Guid.NewGuid();

        var membership = new WorkspaceUserBuilder()
            .ForUser(userId)
            .InWorkspace(context.WorkspaceId)
            .AsRole(role)
            .Build();

        context.WorkspaceUsers
            .GetByUserAndWorkspaceAsync(
                userId,
                context.WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(membership);

        return membership;
    }
}
