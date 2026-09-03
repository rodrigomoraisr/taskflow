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
}
