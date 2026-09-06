using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Infrastructure.Repositories;

namespace TaskFlow.Api.IntegrationTests.Repositories;

/// <summary>
/// The tenant filter in <see cref="ProjectRepository"/>, asserted directly.
///
/// Same shape as <see cref="TaskRepositoryTests"/>: two tenants, and every
/// query asked for the other one's rows with no membership check in the way.
/// </summary>
public sealed class ProjectRepositoryTests : RepositoryTestBase
{
    public ProjectRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetByIdAsync_WhenWorkspaceMatches_ShouldReturnTheProject()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new ProjectRepository(db);

        // Act
        var project = await repository.GetByIdAsync(a.ProjectId, a.WorkspaceId);

        // Assert
        Assert.NotNull(project);
        Assert.Equal(a.ProjectId, project.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenWorkspaceDoesNotMatch_ShouldReturnNull()
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
        var repository = new ProjectRepository(db);

        // Act
        var project = await repository.GetByIdAsync(a.ProjectId, b.WorkspaceId);

        // Assert
        Assert.Null(project);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheProjectIsSoftDeleted_ShouldReturnNull()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(async db =>
        {
            a = AddTenant(db, "Tenant A");
            await db.SaveChangesAsync();

            var project = await db.Projects.FindAsync(a.ProjectId);
            project!.Delete();
        });

        await using var db = CreateDbContext();
        var repository = new ProjectRepository(db);

        // Act
        var project = await repository.GetByIdAsync(a.ProjectId, a.WorkspaceId);

        // Assert
        Assert.Null(project);
    }

    [Fact]
    public async Task GetByWorkspaceAsync_WhenAnotherWorkspaceHasProjects_ShouldReturnOnlyItsOwn()
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
        var repository = new ProjectRepository(db);

        // Act
        var projects = await repository.GetByWorkspaceAsync(a.WorkspaceId);

        // Assert
        Assert.Single(projects);
        Assert.All(
            projects,
            project => Assert.Equal(a.WorkspaceId, project.WorkspaceId));
    }

    [Fact]
    public async Task GetByWorkspaceAsync_WhenAProjectIsSoftDeleted_ShouldExcludeIt()
    {
        // Arrange
        Guid workspaceId = Guid.Empty;

        await SeedAsync(async db =>
        {
            var a = AddTenant(db, "Tenant A");
            workspaceId = a.WorkspaceId;

            await db.SaveChangesAsync();

            var project = await db.Projects.FindAsync(a.ProjectId);
            project!.Delete();
        });

        await using var db = CreateDbContext();
        var repository = new ProjectRepository(db);

        // Act
        var projects = await repository.GetByWorkspaceAsync(workspaceId);

        // Assert
        Assert.Empty(projects);
    }
}
