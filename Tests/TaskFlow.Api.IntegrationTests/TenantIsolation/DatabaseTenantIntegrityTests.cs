using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.TestSupport.Builders;

namespace TaskFlow.Api.IntegrationTests.TenantIsolation;

/// <summary>
/// Defence in depth: these tests bypass the API and the service layer entirely
/// and write straight to the database, to prove PostgreSQL itself refuses to
/// store a cross-tenant row.
///
/// This is the strongest form of the isolation claim. A service-layer check is
/// code someone can refactor away by accident; a composite foreign key has to
/// be dropped on purpose, in a migration, in a diff someone reviews.
///
/// It is also the suite that cannot exist against the in-memory provider, which
/// enforces no constraints at all — so it doubles as the justification for the
/// Testcontainers setup in 8.2.
///
/// Extends DatabaseTestBase rather than IntegrationTestBase: nothing here makes
/// an HTTP request, so there is no reason to start a web host for it.
/// </summary>
public sealed class DatabaseTenantIntegrityTests(PostgreSqlFixture postgres)
    : DatabaseTestBase(postgres)
{
    /// <summary>
    /// The task-to-project foreign key is composite — (WorkspaceId, ProjectId)
    /// against the project's (WorkspaceId, Id) alternate key — so the database
    /// refuses a task that claims one tenant while pointing at a project owned
    /// by another. The service layer would never construct such a row; that is
    /// exactly why it is worth proving the storage layer would not accept it.
    /// </summary>
    [Fact]
    public async Task InsertTask_WhenWorkspaceAndProjectBelongToDifferentTenants_ShouldBeRejectedByDatabase()
    {
        // Arrange
        await using var db = CreateDbContext();

        var workspaceA = new WorkspaceBuilder().WithName("Tenant A").Build();
        var workspaceB = new WorkspaceBuilder().WithName("Tenant B").Build();

        db.Workspaces.AddRange(workspaceA, workspaceB);
        await db.SaveChangesAsync();

        var projectInB = new ProjectBuilder()
            .InWorkspace(workspaceB.Id)
            .WithName("B's project")
            .Build();

        db.Projects.Add(projectInB);
        await db.SaveChangesAsync();

        var crossTenantTask = new TaskItemBuilder()
            .InWorkspace(workspaceA.Id)
            .InProject(projectInB.Id)
            .WithTitle("Cross-tenant task")
            .Build();

        db.Tasks.Add(crossTenantTask);

        // Act + Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.ForeignKeyViolation,
            postgresException.SqlState);
    }

    [Fact]
    public async Task InsertTask_WhenWorkspaceAndProjectMatch_ShouldBeAccepted()
    {
        // The control. Without it, a constraint that rejected every insert
        // would pass the test above.

        // Arrange
        await using var db = CreateDbContext();

        var workspace = new WorkspaceBuilder().WithName("Tenant A").Build();

        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();

        var project = new ProjectBuilder()
            .InWorkspace(workspace.Id)
            .WithName("A's project")
            .Build();

        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var task = new TaskItemBuilder()
            .InWorkspace(workspace.Id)
            .InProject(project.Id)
            .WithTitle("Well-formed task")
            .Build();

        // Act
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        // Assert — read through a separate context so the assertion comes from
        // the database rather than the change tracker.
        await using var verification = CreateDbContext();

        var persisted = await verification.Tasks.SingleAsync(t => t.Id == task.Id);

        Assert.Equal(workspace.Id, persisted.WorkspaceId);
        Assert.Equal(project.Id, persisted.ProjectId);
    }

    [Fact]
    public async Task InsertProject_WhenTheWorkspaceDoesNotExist_ShouldBeRejectedByDatabase()
    {
        // Arrange
        await using var db = CreateDbContext();

        var orphan = new ProjectBuilder()
            .InWorkspace(Guid.NewGuid())
            .Build();

        db.Projects.Add(orphan);

        // Act + Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.ForeignKeyViolation,
            postgresException.SqlState);
    }

    /// <summary>
    /// A duplicate active membership would give one user two roles in one
    /// workspace, and whichever row the query returned first would win — a
    /// silent privilege escalation. The unique index on (UserId, WorkspaceId)
    /// is what prevents it; this test is what proves the index is really there.
    /// </summary>
    [Fact]
    public async Task InsertMembership_WhenUserAlreadyBelongsToWorkspace_ShouldBeRejectedByDatabase()
    {
        // Arrange
        await using var db = CreateDbContext();

        var workspace = new WorkspaceBuilder().Build();
        var user = new UserBuilder().Build();

        db.Workspaces.Add(workspace);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.WorkspaceUsers.Add(
            new WorkspaceUserBuilder()
                .ForUser(user.Id)
                .InWorkspace(workspace.Id)
                .AsOwner()
                .Build());

        await db.SaveChangesAsync();

        db.WorkspaceUsers.Add(
            new WorkspaceUserBuilder()
                .ForUser(user.Id)
                .InWorkspace(workspace.Id)
                .AsViewer()
                .Build());

        // Act + Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            postgresException.SqlState);
    }

    [Fact]
    public async Task InsertUser_WhenEmailAlreadyExists_ShouldBeRejectedByDatabase()
    {
        // Arrange
        await using var db = CreateDbContext();

        const string email = "duplicate@taskflow.test";

        db.Users.Add(new UserBuilder().WithEmail(email).Build());
        await db.SaveChangesAsync();

        db.Users.Add(new UserBuilder().WithEmail(email).Build());

        // Act + Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            postgresException.SqlState);
    }
}
