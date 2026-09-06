using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.TestSupport.Builders;

namespace TaskFlow.Api.IntegrationTests.Repositories;

/// <summary>
/// Base for tests that call a repository directly, with no HTTP pipeline and
/// no service layer above it.
///
/// The isolation suite in <c>TenantIsolation/</c> reaches the repositories only
/// through a route, and almost every cross-tenant request is refused by the
/// membership gate before a repository is ever consulted. Break-testing
/// measured the consequence: dropping the <c>workspaceId</c> filter from
/// <c>TaskRepository.GetByIdAsync</c> turned exactly one test red out of 144.
///
/// These tests remove the gate from the picture entirely. They ask a
/// repository for a row belonging to another tenant with nothing standing in
/// the way, which is the only way to assert that the filter in the query — not
/// the check in front of it — is what says no.
/// </summary>
public abstract class RepositoryTestBase : DatabaseTestBase
{
    protected RepositoryTestBase(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// Writes seed rows through a context that is then thrown away, so the
    /// repository under test reads from the database rather than from a change
    /// tracker that already holds the entity.
    /// </summary>
    protected async Task SeedAsync(Func<TaskFlowDbContext, Task> seed)
    {
        await using var db = CreateDbContext();

        await seed(db);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A workspace with an owner and one active project — the smallest tenant
    /// that can legally hold a task, given tasks are keyed to
    /// (workspaceId, projectId) by a composite foreign key.
    /// </summary>
    protected static Tenant AddTenant(
        TaskFlowDbContext db,
        string label)
    {
        var user = new UserBuilder().Build();
        var workspace = new WorkspaceBuilder().WithName(label).Build();

        var membership = new WorkspaceUserBuilder()
            .ForUser(user.Id)
            .InWorkspace(workspace.Id)
            .AsOwner()
            .Build();

        var project = new ProjectBuilder()
            .InWorkspace(workspace.Id)
            .WithName($"{label} project")
            .Build();

        db.Users.Add(user);
        db.Workspaces.Add(workspace);
        db.WorkspaceUsers.Add(membership);
        db.Projects.Add(project);

        return new Tenant(user.Id, workspace.Id, project.Id);
    }

    protected static TaskItem AddTask(
        TaskFlowDbContext db,
        Tenant tenant,
        string title,
        bool deleted = false)
    {
        var builder = new TaskItemBuilder()
            .WithTitle(title)
            .InWorkspace(tenant.WorkspaceId)
            .InProject(tenant.ProjectId);

        if (deleted)
            builder = builder.Deleted();

        var task = builder.Build();
        db.Tasks.Add(task);

        return task;
    }

    protected static User AddMember(
        TaskFlowDbContext db,
        Tenant tenant,
        WorkspaceRole role,
        bool removed = false)
    {
        var user = new UserBuilder().Build();

        var builder = new WorkspaceUserBuilder()
            .ForUser(user.Id)
            .InWorkspace(tenant.WorkspaceId)
            .AsRole(role);

        if (removed)
            builder = builder.Removed();

        db.Users.Add(user);
        db.WorkspaceUsers.Add(builder.Build());

        return user;
    }

    /// <summary>
    /// One tenant's ids. Named rather than a tuple because every test in this
    /// folder arranges two of them and reads better saying <c>a.WorkspaceId</c>
    /// than <c>a.Item2</c>.
    /// </summary>
    protected sealed record Tenant(
        Guid OwnerId,
        Guid WorkspaceId,
        Guid ProjectId);
}
