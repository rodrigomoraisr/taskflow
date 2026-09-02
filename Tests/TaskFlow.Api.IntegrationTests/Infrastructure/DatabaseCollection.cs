namespace TaskFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Groups every database-backed test into one xUnit collection so the
/// container is started once rather than per test class. Tests in a collection
/// run sequentially, which is what we want here: they share one database and
/// each one truncates it on entry.
/// </summary>
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "postgres";
}
