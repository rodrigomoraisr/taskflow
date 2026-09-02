using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskFlow.TestSupport.Builders;

namespace TaskFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Proves the harness itself works before any suite is built on it: the
/// migrations run, Respawn clears data between tests without dropping the
/// schema, and the constraints an in-memory provider would ignore are really
/// enforced.
/// </summary>
public class DatabaseInfrastructureTests : DatabaseTestBase
{
    public DatabaseInfrastructureTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Migrations_WhenTheFixtureStarts_ShouldLeaveNoPendingMigrations()
    {
        // Arrange
        await using var context = CreateDbContext();

        // Act
        var pending = await context.Database.GetPendingMigrationsAsync();

        // Assert
        Assert.Empty(pending);
    }

    [Theory]
    [InlineData("Users")]
    [InlineData("Workspaces")]
    [InlineData("WorkspaceUsers")]
    [InlineData("Projects")]
    [InlineData("tasks")]
    public async Task Migrations_WhenTheFixtureStarts_ShouldCreateTheExpectedTable(
        string tableName)
    {
        // Arrange
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "select exists (select 1 from information_schema.tables "
            + "where table_schema = 'public' and table_name = @name);";
        command.Parameters.AddWithValue("name", tableName);

        // Act
        var exists = (bool)(await command.ExecuteScalarAsync())!;

        // Assert
        Assert.True(exists, "Expected table to exist: " + tableName);
    }

    [Fact]
    public async Task Reset_WhenAPreviousTestWroteRows_ShouldLeaveTheDatabaseEmpty()
    {
        // Arrange
        await using var context = CreateDbContext();

        // Act
        var users = await context.Users.CountAsync();
        var workspaces = await context.Workspaces.CountAsync();
        var tasks = await context.Tasks.CountAsync();

        // Assert
        Assert.Equal(0, users);
        Assert.Equal(0, workspaces);
        Assert.Equal(0, tasks);

        // Leave a row behind. Paired with the test below, which asserts an
        // empty table on entry, so a broken reset fails one of the two.
        context.Users.Add(new UserBuilder().Build());
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SeedingRows_WhenTheDatabaseWasResetFirst_ShouldStartFromAnEmptyUsersTable()
    {
        // Arrange
        await using var context = CreateDbContext();

        // Act
        var usersBefore = await context.Users.CountAsync();

        context.Users.Add(new UserBuilder().Build());
        await context.SaveChangesAsync();

        var usersAfter = await context.Users.CountAsync();

        // Assert
        Assert.Equal(0, usersBefore);
        Assert.Equal(1, usersAfter);
    }

    [Fact]
    public async Task Reset_WhenItRuns_ShouldKeepTheMigrationHistory()
    {
        // Arrange
        await using var context = CreateDbContext();

        // Act
        var applied = await context.Database.GetAppliedMigrationsAsync();

        // Assert
        Assert.NotEmpty(applied);
    }

    [Fact]
    public async Task Users_WhenTwoRowsShareAnEmail_ShouldViolateTheUniqueIndex()
    {
        // Arrange
        await using var context = CreateDbContext();

        const string email = "duplicate@taskflow.test";

        context.Users.Add(new UserBuilder().WithEmail(email).Build());
        await context.SaveChangesAsync();

        context.Users.Add(new UserBuilder().WithEmail(email).Build());

        // Act + Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            postgresException.SqlState);
    }

    [Fact]
    public async Task Projects_WhenTheWorkspaceDoesNotExist_ShouldViolateTheForeignKey()
    {
        // Arrange
        await using var context = CreateDbContext();

        var orphan = new ProjectBuilder()
            .InWorkspace(Guid.NewGuid())
            .Build();

        context.Projects.Add(orphan);

        // Act + Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.ForeignKeyViolation,
            postgresException.SqlState);
    }

    /// <summary>
    /// The task-to-project foreign key is composite — (WorkspaceId, ProjectId)
    /// against the project's (WorkspaceId, Id) alternate key — so the database
    /// itself refuses a task that claims one tenant while pointing at a project
    /// owned by another. This is exactly the constraint an in-memory provider
    /// would not enforce, and the reason phase 8.2 uses a real server.
    /// </summary>
    [Fact]
    public async Task Tasks_WhenPointingAtAProjectInAnotherWorkspace_ShouldViolateTheForeignKey()
    {
        // Arrange
        await using var context = CreateDbContext();

        var workspaceA = new WorkspaceBuilder().WithName("Workspace A").Build();
        var workspaceB = new WorkspaceBuilder().WithName("Workspace B").Build();

        context.Workspaces.AddRange(workspaceA, workspaceB);
        await context.SaveChangesAsync();

        var projectInA = new ProjectBuilder()
            .InWorkspace(workspaceA.Id)
            .Build();

        context.Projects.Add(projectInA);
        await context.SaveChangesAsync();

        // A task claiming workspace B while pointing at workspace A's project.
        var crossTenantTask = new TaskItemBuilder()
            .InWorkspace(workspaceB.Id)
            .InProject(projectInA.Id)
            .Build();

        context.Tasks.Add(crossTenantTask);

        // Act + Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.ForeignKeyViolation,
            postgresException.SqlState);
    }

    [Fact]
    public async Task Tasks_WhenTheProjectIsInTheSameWorkspace_ShouldPersist()
    {
        // Arrange
        await using var context = CreateDbContext();

        var workspace = new WorkspaceBuilder().Build();

        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();

        var project = new ProjectBuilder()
            .InWorkspace(workspace.Id)
            .Build();

        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var task = new TaskItemBuilder()
            .InWorkspace(workspace.Id)
            .InProject(project.Id)
            .Build();

        // Act
        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        // Assert — read back through a separate context so the assertion comes
        // from the database rather than the change tracker.
        await using var verification = CreateDbContext();

        var persisted = await verification.Tasks
            .SingleAsync(t => t.Id == task.Id);

        Assert.Equal(workspace.Id, persisted.WorkspaceId);
        Assert.Equal(project.Id, persisted.ProjectId);
    }

    [Fact]
    public async Task WorkspaceUsers_WhenTheSameUserJoinsTwice_ShouldViolateTheUniqueIndex()
    {
        // Arrange
        await using var context = CreateDbContext();

        var user = new UserBuilder().Build();
        var workspace = new WorkspaceBuilder().Build();

        context.Users.Add(user);
        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();

        context.WorkspaceUsers.Add(
            new WorkspaceUserBuilder()
                .ForUser(user.Id)
                .InWorkspace(workspace.Id)
                .AsOwner()
                .Build());

        await context.SaveChangesAsync();

        context.WorkspaceUsers.Add(
            new WorkspaceUserBuilder()
                .ForUser(user.Id)
                .InWorkspace(workspace.Id)
                .AsMember()
                .Build());

        // Act + Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            postgresException.SqlState);
    }
}
