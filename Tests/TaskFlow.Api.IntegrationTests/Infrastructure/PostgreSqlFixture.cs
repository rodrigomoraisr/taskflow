using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Respawn.Graph;
using TaskFlow.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace TaskFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Owns a real PostgreSQL instance for the integration suite.
///
/// Deliberately not the in-memory provider: it does not enforce foreign keys,
/// unique indexes or check constraints, so a tenant-isolation bug that the
/// database would reject could pass in memory. The whole point of these tests
/// is to exercise the constraints that actually protect the data.
///
/// The container starts once per test collection and is shared. Between tests
/// Respawn deletes the rows and leaves the schema alone, which is far cheaper
/// than recreating the database and keeps the migration history intact.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    // Pinned to the same major version docker-compose.yml runs, so tests and
    // local development do not disagree about server behaviour.
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase("taskflow_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private Respawner? _respawner;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        // Build the schema the same way production does — by running the
        // migrations. EnsureCreated would bypass them and could drift from
        // what actually ships.
        await using (var context = CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],

                // Wiping the migrations history would make the next
                // MigrateAsync try to re-apply everything.
                TablesToIgnore = [new Table("__EFMigrationsHistory")]
            });
    }

    /// <summary>
    /// Truncates every table so each test starts from an empty database.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_respawner is null)
            throw new InvalidOperationException(
                "The fixture has not been initialised.");

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await _respawner.ResetAsync(connection);
    }

    /// <summary>
    /// A context pointed at the container. Callers own the instance and should
    /// dispose it; a fresh context per act/assert step also avoids reading a
    /// value straight out of the change tracker instead of the database.
    /// </summary>
    public TaskFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TaskFlowDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new TaskFlowDbContext(options);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
