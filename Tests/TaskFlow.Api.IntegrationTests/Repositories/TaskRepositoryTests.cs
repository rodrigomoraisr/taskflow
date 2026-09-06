using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Infrastructure.Repositories;

namespace TaskFlow.Api.IntegrationTests.Repositories;

/// <summary>
/// The tenant filter in <see cref="TaskRepository"/>, asserted without a
/// membership check in front of it.
///
/// Every test here arranges two tenants and asks tenant A's repository call for
/// tenant B's data. There is no authorization service in the picture, so the
/// only thing that can return the right answer is the <c>workspaceId</c>
/// predicate in the query itself.
/// </summary>
public sealed class TaskRepositoryTests : RepositoryTestBase
{
    public TaskRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetByIdAsync_WhenWorkspaceMatches_ShouldReturnTheTask()
    {
        // Arrange
        Guid taskId = Guid.Empty;
        Tenant tenant = default!;

        await SeedAsync(db =>
        {
            tenant = AddTenant(db, "Tenant A");
            taskId = AddTask(db, tenant, "Only task").Id;

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var task = await repository.GetByIdAsync(taskId, tenant.WorkspaceId);

        // Assert
        Assert.NotNull(task);
        Assert.Equal(taskId, task.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenWorkspaceDoesNotMatch_ShouldReturnNull()
    {
        // Arrange
        Guid taskInA = Guid.Empty;
        Tenant b = default!;

        await SeedAsync(db =>
        {
            var a = AddTenant(db, "Tenant A");
            b = AddTenant(db, "Tenant B");

            taskInA = AddTask(db, a, "Tenant A task").Id;

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var task = await repository.GetByIdAsync(taskInA, b.WorkspaceId);

        // Assert
        Assert.Null(task);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheTaskIsSoftDeleted_ShouldReturnNull()
    {
        // Arrange
        Guid taskId = Guid.Empty;
        Tenant tenant = default!;

        await SeedAsync(db =>
        {
            tenant = AddTenant(db, "Tenant A");
            taskId = AddTask(db, tenant, "Removed task", deleted: true).Id;

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var task = await repository.GetByIdAsync(taskId, tenant.WorkspaceId);

        // Assert
        Assert.Null(task);
    }

    /// <summary>
    /// Deleting a project hides its tasks even though their own
    /// <c>IsDeleted</c> stays false — the lookup joins through the active
    /// project. Documented in CLAUDE.md as a read-filter property rather than
    /// a cascade, and asserted here at the layer that implements it.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WhenTheProjectIsSoftDeleted_ShouldReturnNull()
    {
        // Arrange
        Guid taskId = Guid.Empty;
        Tenant tenant = default!;

        await SeedAsync(async db =>
        {
            tenant = AddTenant(db, "Tenant A");
            taskId = AddTask(db, tenant, "Task in a doomed project").Id;

            await db.SaveChangesAsync();

            var project = await db.Projects.FindAsync(tenant.ProjectId);
            project!.Delete();
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var task = await repository.GetByIdAsync(taskId, tenant.WorkspaceId);

        // Assert
        Assert.Null(task);
        Assert.False(
            (await db.Tasks.FindAsync(taskId))!.IsDeleted,
            "The task row itself should still be active — it is hidden by the join, not cascaded.");
    }

    [Fact]
    public async Task GetPagedAsync_WhenAnotherWorkspaceHasTasks_ShouldReturnOnlyItsOwn()
    {
        // Arrange
        Tenant a = default!;
        Tenant b = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            b = AddTenant(db, "Tenant B");

            AddTask(db, a, "A one");
            AddTask(db, a, "A two");
            AddTask(db, b, "B one");
            AddTask(db, b, "B two");
            AddTask(db, b, "B three");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var page = await repository.GetPagedAsync(a.WorkspaceId, 1, 50);

        // Assert
        Assert.Equal(2, page.Count);
        Assert.All(page, task => Assert.Equal(a.WorkspaceId, task.WorkspaceId));
    }

    [Fact]
    public async Task GetPagedAsync_WhenATaskIsSoftDeleted_ShouldExcludeIt()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");

            AddTask(db, a, "Active");
            AddTask(db, a, "Removed", deleted: true);

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var page = await repository.GetPagedAsync(a.WorkspaceId, 1, 50);

        // Assert
        Assert.Single(page);
        Assert.Equal("Active", page[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_WhenTheProjectIsSoftDeleted_ShouldExcludeItsTasks()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(async db =>
        {
            a = AddTenant(db, "Tenant A");
            AddTask(db, a, "Task in a doomed project");

            await db.SaveChangesAsync();

            var project = await db.Projects.FindAsync(a.ProjectId);
            project!.Delete();
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var page = await repository.GetPagedAsync(a.WorkspaceId, 1, 50);

        // Assert
        Assert.Empty(page);
    }

    [Fact]
    public async Task CountAsync_WhenAnotherWorkspaceHasTasks_ShouldCountOnlyItsOwn()
    {
        // Arrange
        Tenant a = default!;
        Tenant b = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            b = AddTenant(db, "Tenant B");

            AddTask(db, a, "A one");
            AddTask(db, b, "B one");
            AddTask(db, b, "B two");
            AddTask(db, b, "B three");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var count = await repository.CountAsync(a.WorkspaceId);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountAsync_WhenATaskIsSoftDeleted_ShouldExcludeIt()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");

            AddTask(db, a, "Active");
            AddTask(db, a, "Removed", deleted: true);

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var count = await repository.CountAsync(a.WorkspaceId);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountAsync_WhenTheProjectIsSoftDeleted_ShouldExcludeItsTasks()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(async db =>
        {
            a = AddTenant(db, "Tenant A");
            AddTask(db, a, "Task in a doomed project");

            await db.SaveChangesAsync();

            var project = await db.Projects.FindAsync(a.ProjectId);
            project!.Delete();
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var count = await repository.CountAsync(a.WorkspaceId);

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>
    /// The two queries are separate expressions that happen to repeat the same
    /// three predicates — there is no shared query builder to keep them
    /// honest. So they are asserted against each other rather than separately:
    /// a count that ignores a filter the page applies produces a pager that
    /// promises rows it will never show, and tells the caller how many rows
    /// another tenant has.
    /// </summary>
    [Fact]
    public async Task CountAsync_WhenComparedWithEveryPage_ShouldAgreeWithGetPagedAsync()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(async db =>
        {
            a = AddTenant(db, "Tenant A");
            var b = AddTenant(db, "Tenant B");

            for (var i = 0; i < 7; i++)
                AddTask(db, a, $"A active {i}");

            AddTask(db, a, "A removed", deleted: true);

            for (var i = 0; i < 5; i++)
                AddTask(db, b, $"B active {i}");

            // A second project in tenant A, deleted, whose tasks must be
            // invisible to both queries.
            var doomed = AddTenant(db, "Tenant A second project");
            await db.SaveChangesAsync();

            var project = await db.Projects.FindAsync(doomed.ProjectId);
            project!.Delete();
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        const int pageSize = 3;

        // Act
        var count = await repository.CountAsync(a.WorkspaceId);

        var collected = new List<Guid>();

        for (var page = 1; ; page++)
        {
            var items = await repository.GetPagedAsync(
                a.WorkspaceId,
                page,
                pageSize);

            if (items.Count == 0)
                break;

            collected.AddRange(items.Select(task => task.Id));
        }

        // Assert
        Assert.Equal(7, count);
        Assert.Equal(count, collected.Count);
        Assert.Equal(collected.Count, collected.Distinct().Count());
    }

    [Fact]
    public async Task GetPagedAsync_WhenPagingPastTheLastPage_ShouldReturnEmpty()
    {
        // Arrange
        Tenant a = default!;

        await SeedAsync(db =>
        {
            a = AddTenant(db, "Tenant A");
            AddTask(db, a, "Only task");

            return Task.CompletedTask;
        });

        await using var db = CreateDbContext();
        var repository = new TaskRepository(db);

        // Act
        var page = await repository.GetPagedAsync(a.WorkspaceId, 2, 20);

        // Assert
        Assert.Empty(page);
    }
}
