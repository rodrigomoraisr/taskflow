using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for tests that touch the database. Resets the data before each
/// test so ordering never matters and a failure never cascades into the next
/// test as a confusing second failure.
/// </summary>
[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTestBase : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;

    protected DatabaseTestBase(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    protected string ConnectionString => _fixture.ConnectionString;

    protected TaskFlowDbContext CreateDbContext() => _fixture.CreateDbContext();

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
