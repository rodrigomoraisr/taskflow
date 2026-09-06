using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Repositories;
using TaskFlow.TestSupport.Builders;

namespace TaskFlow.Api.IntegrationTests.Repositories;

/// <summary>
/// The two repositories that are not themselves workspace-scoped.
///
/// <c>Workspace</c> and <c>User</c> sit above the tenant boundary rather than
/// inside it, so neither takes a <c>workspaceId</c>. Their bulk <c>GetByIdsAsync</c>
/// methods are covered here anyway, because a bulk fetch called from a context
/// that has already checked authorization is a classic place for a filter to go
/// missing unnoticed — and what each one does and does not filter is worth
/// pinning down rather than assuming.
/// </summary>
public sealed class WorkspaceAndUserRepositoryTests : RepositoryTestBase
{
    public WorkspaceAndUserRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheWorkspaceIsActive_ShouldReturnIt()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceRepository(db);

        // Act
        var workspace = await repository.GetByIdAsync(a.WorkspaceId);

        // Assert
        Assert.NotNull(workspace);
        Assert.Equal(a.WorkspaceId, workspace.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheWorkspaceIsSoftDeleted_ShouldReturnNull()
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
        var repository = new WorkspaceRepository(db);

        // Act
        var workspace = await repository.GetByIdAsync(a.WorkspaceId);

        // Assert
        Assert.Null(workspace);
    }

    [Fact]
    public async Task GetByIdsAsync_WhenOtherWorkspacesExist_ShouldReturnOnlyTheOnesAskedFor()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            AddTenant(db, "Tenant B");
            AddTenant(db, "Tenant C");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceRepository(db);

        // Act
        var workspaces = await repository.GetByIdsAsync([a.WorkspaceId]);

        // Assert
        Assert.Single(workspaces);
        Assert.Equal(a.WorkspaceId, workspaces[0].Id);
    }

    /// <summary>
    /// The caller — <c>WorkspaceService.GetForUserAsync</c> — indexes its role
    /// dictionary by the ids it asked for, so a workspace returned here must be
    /// one of them. A soft-deleted workspace must also drop out, or a user
    /// whose membership survived a workspace deletion would see it listed.
    /// </summary>
    [Fact]
    public async Task GetByIdsAsync_WhenAWorkspaceIsSoftDeleted_ShouldExcludeIt()
    {
        // Arrange
        Tenant a = default!;
        Tenant b = default!;

        await SeedAsync(async db =>
        {
            a = AddTenant(db, "Tenant A");
            b = AddTenant(db, "Tenant B");

            await db.SaveChangesAsync();

            var workspace = await db.Workspaces.FindAsync(b.WorkspaceId);
            workspace!.Delete();
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceRepository(db);

        // Act
        var workspaces = await repository.GetByIdsAsync(
            [a.WorkspaceId, b.WorkspaceId]);

        // Assert
        Assert.Single(workspaces);
        Assert.Equal(a.WorkspaceId, workspaces[0].Id);
    }

    [Fact]
    public async Task GetByIdsAsync_WhenNoIdsAreGiven_ShouldReturnEmpty()
    {
        // Arrange
        await SeedAsync(db =>
        {
            AddTenant(db, "Tenant A");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new WorkspaceRepository(db);

        // Act
        var workspaces = await repository.GetByIdsAsync([]);

        // Assert
        Assert.Empty(workspaces);
    }

    [Fact]
    public async Task GetByEmailAsync_WhenTheUserExists_ShouldReturnThem()
    {
        // Arrange
        const string email = "known-user@taskflow.test";

        await SeedAsync(db =>
        {
            db.Users.Add(new UserBuilder().WithEmail(email).Build());
            db.Users.Add(new UserBuilder().Build());

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new UserRepository(db);

        // Act
        var user = await repository.GetByEmailAsync(email);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_WhenNoUserHasThatEmail_ShouldReturnNull()
    {
        // Arrange
        await SeedAsync(db =>
        {
            db.Users.Add(new UserBuilder().Build());

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new UserRepository(db);

        // Act
        var user = await repository.GetByEmailAsync("nobody@taskflow.test");

        // Assert
        Assert.Null(user);
    }

    /// <summary>
    /// <c>WorkspaceService.GetMembersAsync</c> builds a dictionary from this
    /// result and then indexes it by every membership's user id, so a missing
    /// row would surface as a <c>KeyNotFoundException</c> — a 500 — rather than
    /// as a partial list. The ids it passes come from memberships the caller
    /// is already authorized to see, which is why no tenant filter belongs
    /// here.
    /// </summary>
    [Fact]
    public async Task GetByIdsAsync_WhenOtherUsersExist_ShouldReturnOnlyTheOnesAskedFor()
    {
        // Arrange
        Tenant a = default!;
        Guid memberId = Guid.Empty;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            memberId = AddMember(db, a, WorkspaceRole.Member).Id;

            var b = AddTenant(db, "Tenant B");
            AddMember(db, b, WorkspaceRole.Member);

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new UserRepository(db);

        // Act
        var users = await repository.GetByIdsAsync([a.OwnerId, memberId]);

        // Assert
        Assert.Equal(2, users.Count);
        Assert.Contains(users, user => user.Id == a.OwnerId);
        Assert.Contains(users, user => user.Id == memberId);
    }
}
