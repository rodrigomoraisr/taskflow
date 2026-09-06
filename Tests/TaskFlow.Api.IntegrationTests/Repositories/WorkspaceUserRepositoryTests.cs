using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Repositories;

namespace TaskFlow.Api.IntegrationTests.Repositories;

/// <summary>
/// <see cref="WorkspaceUserRepository"/> is the membership gate's only source
/// of truth: <c>WorkspaceAuthorizationService</c> resolves every caller's role
/// through <c>GetActiveMembershipAsync</c>, and refuses when it returns null.
/// A missing filter here does not produce a wrong answer somewhere downstream —
/// it hands a caller a role in a workspace they do not belong to.
///
/// <c>CountActiveOwnersAsync</c> gets its own attention for the same reason in
/// reverse: the last-owner rule is a single comparison against that number, so
/// a count that strays across workspaces either blocks a legitimate demotion or
/// lets the last owner of a workspace demote themselves out of it.
/// </summary>
public sealed class WorkspaceUserRepositoryTests : RepositoryTestBase
{
    public WorkspaceUserRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetActiveMembershipAsync_WhenTheMembershipIsActive_ShouldReturnIt()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var membership = await repository.GetActiveMembershipAsync(
            a.OwnerId,
            a.WorkspaceId);

        // Assert
        Assert.NotNull(membership);
        Assert.Equal(WorkspaceRole.Owner, membership.Role);
    }

    [Fact]
    public async Task GetActiveMembershipAsync_WhenWorkspaceDoesNotMatch_ShouldReturnNull()
    {
        // Arrange
        Tenant a = default!;
        Tenant b = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            b = AddTenant(db, "Tenant B");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var membership = await repository.GetActiveMembershipAsync(
            a.OwnerId,
            b.WorkspaceId);

        // Assert
        Assert.Null(membership);
    }

    [Fact]
    public async Task GetActiveMembershipAsync_WhenTheMembershipWasRemoved_ShouldReturnNull()
    {
        // Arrange
        Tenant a = default!;
        Guid removedUserId = Guid.Empty;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            removedUserId = AddMember(
                db,
                a,
                WorkspaceRole.Member,
                removed: true).Id;

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var membership = await repository.GetActiveMembershipAsync(
            removedUserId,
            a.WorkspaceId);

        // Assert
        Assert.Null(membership);
    }

    [Fact]
    public async Task GetActiveMembershipAsync_WhenTheWorkspaceIsSoftDeleted_ShouldReturnNull()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(async db =>
        {
            a = AddTenant(db, "Tenant A");
            await db.SaveChangesAsync();

            var workspace = await db.Workspaces.FindAsync(a.WorkspaceId);
            workspace!.Delete();
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var membership = await repository.GetActiveMembershipAsync(
            a.OwnerId,
            a.WorkspaceId);

        // Assert
        Assert.Null(membership);
    }

    [Fact]
    public async Task GetActiveMembershipsAsync_WhenTheUserBelongsToSeveralWorkspaces_ShouldReturnOnlyTheirOwn()
    {
        // Arrange
        Guid userId = Guid.Empty;

        await SeedAsync(db =>
        {
            var a = AddTenant(db, "Tenant A");
            var b = AddTenant(db, "Tenant B");
            AddTenant(db, "Tenant C");

            userId = AddMember(db, a, WorkspaceRole.Member).Id;

            db.WorkspaceUsers.Add(
                new TaskFlow.TestSupport.Builders.WorkspaceUserBuilder()
                    .ForUser(userId)
                    .InWorkspace(b.WorkspaceId)
                    .AsViewer()
                    .Build());

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var memberships = await repository.GetActiveMembershipsAsync(userId);

        // Assert
        Assert.Equal(2, memberships.Count);
        Assert.All(memberships, m => Assert.Equal(userId, m.UserId));
    }

    [Fact]
    public async Task GetActiveMembershipsAsync_WhenAMembershipWasRemoved_ShouldExcludeIt()
    {
        // Arrange
        Guid userId = Guid.Empty;

        await SeedAsync(db =>
        {
            var a = AddTenant(db, "Tenant A");
            userId = AddMember(db, a, WorkspaceRole.Member, removed: true).Id;

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var memberships = await repository.GetActiveMembershipsAsync(userId);

        // Assert
        Assert.Empty(memberships);
    }

    [Fact]
    public async Task GetActiveMembershipsAsync_WhenTheWorkspaceIsSoftDeleted_ShouldExcludeThatMembership()
    {
        // Arrange
        Guid userId = Guid.Empty;

        await SeedAsync(async db =>
        {
            var a = AddTenant(db, "Tenant A");
            userId = AddMember(db, a, WorkspaceRole.Member).Id;

            await db.SaveChangesAsync();

            var workspace = await db.Workspaces.FindAsync(a.WorkspaceId);
            workspace!.Delete();
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var memberships = await repository.GetActiveMembershipsAsync(userId);

        // Assert
        Assert.Empty(memberships);
    }

    [Fact]
    public async Task GetActiveByWorkspaceAsync_WhenAnotherWorkspaceHasMembers_ShouldReturnOnlyItsOwn()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            var b = AddTenant(db, "Tenant B");

            AddMember(db, a, WorkspaceRole.Member);
            AddMember(db, b, WorkspaceRole.Member);
            AddMember(db, b, WorkspaceRole.Viewer);

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var members = await repository.GetActiveByWorkspaceAsync(a.WorkspaceId);

        // Assert
        Assert.Equal(2, members.Count);
        Assert.All(
            members,
            m => Assert.Equal(a.WorkspaceId, m.WorkspaceId));
    }

    [Fact]
    public async Task GetActiveByWorkspaceAsync_WhenAMemberWasRemoved_ShouldExcludeThem()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            AddMember(db, a, WorkspaceRole.Member, removed: true);

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var members = await repository.GetActiveByWorkspaceAsync(a.WorkspaceId);

        // Assert
        Assert.Single(members);
        Assert.Equal(a.OwnerId, members[0].UserId);
    }

    [Fact]
    public async Task GetByUserAndWorkspaceAsync_WhenWorkspaceDoesNotMatch_ShouldReturnNull()
    {
        // Arrange
        Tenant a = default!;
        Tenant b = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            b = AddTenant(db, "Tenant B");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var membership = await repository.GetByUserAndWorkspaceAsync(
            a.OwnerId,
            b.WorkspaceId);

        // Assert
        Assert.Null(membership);
    }

    /// <summary>
    /// This is the one lookup that deliberately ignores <c>IsDeleted</c>:
    /// re-adding a previously removed member restores the existing row rather
    /// than inserting a second one, and the unique index on
    /// (UserId, WorkspaceId) means a filtered lookup here would make that
    /// re-add fail on a constraint violation instead.
    /// </summary>
    [Fact]
    public async Task GetByUserAndWorkspaceAsync_WhenTheMembershipWasRemoved_ShouldStillReturnIt()
    {
        // Arrange
        Tenant a = default!;
        Guid removedUserId = Guid.Empty;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            removedUserId = AddMember(
                db,
                a,
                WorkspaceRole.Member,
                removed: true).Id;

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var membership = await repository.GetByUserAndWorkspaceAsync(
            removedUserId,
            a.WorkspaceId);

        // Assert
        Assert.NotNull(membership);
        Assert.True(membership.IsDeleted);
    }

    [Fact]
    public async Task CountActiveOwnersAsync_WhenAnotherWorkspaceHasOwners_ShouldCountOnlyItsOwn()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            var b = AddTenant(db, "Tenant B");

            // Three more owners, all in the other tenant. A cross-workspace
            // count would report four here and silently disable the last-owner
            // protection for tenant A.
            AddMember(db, b, WorkspaceRole.Owner);
            AddMember(db, b, WorkspaceRole.Owner);
            AddMember(db, b, WorkspaceRole.Owner);

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var owners = await repository.CountActiveOwnersAsync(a.WorkspaceId);

        // Assert
        Assert.Equal(1, owners);
    }

    [Fact]
    public async Task CountActiveOwnersAsync_WhenAnOwnerWasRemoved_ShouldExcludeThem()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            AddMember(db, a, WorkspaceRole.Owner, removed: true);

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var owners = await repository.CountActiveOwnersAsync(a.WorkspaceId);

        // Assert
        Assert.Equal(1, owners);
    }

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task CountActiveOwnersAsync_WhenTheWorkspaceHasOtherRoles_ShouldCountOnlyOwners(
        WorkspaceRole role)
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            AddMember(db, a, role);

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceUserRepository(db);

        // Act
        var owners = await repository.CountActiveOwnersAsync(a.WorkspaceId);

        // Assert
        Assert.Equal(1, owners);
    }
}
