using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Users;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base for tests that drive the API over HTTP against a real database.
///
/// Every actor is a real registered user holding a real token, and every
/// arrangement step goes through the same endpoints a client would call. That
/// is slower than seeding rows directly, but it means the arrangement itself
/// exercises the authorization path — a bug that let an outsider add members
/// would break the arrangement rather than hide inside it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlFixture _postgres;

    protected readonly TaskFlowApiFactory _factory;

    protected IntegrationTestBase(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
        _factory = new TaskFlowApiFactory(postgres.ConnectionString);
    }

    /// <summary>
    /// Truncates the database before each test, so no test depends on another
    /// having run first.
    /// </summary>
    public Task InitializeAsync() => _postgres.ResetAsync();

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// A client permanently bound to one user. Tenant tests juggle two or three
    /// callers, and swapping the header on a shared client makes the test read
    /// like a sequence of mode switches rather than two actors.
    /// </summary>
    protected HttpClient CreateClientFor(TestUser user)
    {
        var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", user.Token);

        return client;
    }

    /// <summary>
    /// Registers a user and returns their identity, token and the workspace
    /// registration created for them, in which they are the Owner.
    /// </summary>
    protected async Task<TestUser> RegisterUserAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@taskflow.test";
        const string password = "Integration-Test-Password-1";

        using var client = _factory.CreateClient();

        var registration = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest
            {
                Email = email,
                Password = password
            });
        registration.EnsureSuccessStatusCode();

        var registered = await registration.Content
            .ReadFromJsonAsync<RegisterResponse>();

        var login = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = password
            });
        login.EnsureSuccessStatusCode();

        var session = await login.Content.ReadFromJsonAsync<LoginResponse>();

        return new TestUser(
            registered!.Id,
            email,
            password,
            session!.Token,
            registered.WorkspaceId);
    }

    /// <summary>
    /// Registers a user, adds them to <paramref name="workspaceId"/> and sets
    /// their role explicitly.
    ///
    /// The role is set with a follow-up PATCH rather than relying on whatever
    /// AddMember defaults to — a test that silently depends on that default
    /// starts lying the day the default changes.
    /// </summary>
    protected async Task<TestUser> AddMemberAsync(
        TestUser workspaceManager,
        Guid workspaceId,
        WorkspaceRole role)
    {
        var member = await RegisterUserAsync();
        var managerClient = CreateClientFor(workspaceManager);

        var add = await managerClient.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members",
            new AddWorkspaceMemberRequest { Email = member.Email });
        add.EnsureSuccessStatusCode();

        var changeRole = await managerClient.PatchAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members/{member.Id}/role",
            new ChangeWorkspaceMemberRoleRequest { Role = role });
        changeRole.EnsureSuccessStatusCode();

        // The token predates the membership, but it only carries identity —
        // the role is resolved per request — so it stays valid.
        return member with { WorkspaceId = workspaceId };
    }

    /// <summary>
    /// Two unrelated tenants, each with its own owner, one project and one
    /// task. This is the arrangement almost every isolation test needs, and
    /// building it once keeps the tests themselves down to act-and-assert.
    /// </summary>
    protected async Task<TwoTenants> ArrangeTwoTenantsAsync()
    {
        var ownerA = await RegisterUserAsync();
        var ownerB = await RegisterUserAsync();

        var (projectA, taskA) = await SeedProjectAndTaskAsync(ownerA, "Tenant A");
        var (projectB, taskB) = await SeedProjectAndTaskAsync(ownerB, "Tenant B");

        return new TwoTenants(
            ownerA, ownerA.WorkspaceId, projectA, taskA,
            ownerB, ownerB.WorkspaceId, projectB, taskB);
    }

    /// <summary>
    /// Creates one project and one task in the user's own workspace, through
    /// the API, as that user.
    /// </summary>
    protected async Task<(Guid ProjectId, Guid TaskId)> SeedProjectAndTaskAsync(
        TestUser owner,
        string label = "Seed")
    {
        var client = CreateClientFor(owner);

        var projectResponse = await client.PostAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects",
            new CreateProjectRequest
            {
                Name = $"{label} project",
                Description = label
            });
        projectResponse.EnsureSuccessStatusCode();

        var project = await projectResponse.Content
            .ReadFromJsonAsync<CreateProjectResponse>();

        var taskResponse = await client.PostAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks",
            new CreateTaskRequest
            {
                Title = $"{label} task",
                Description = label,
                ProjectId = project!.Id,
                Priority = TaskPriority.Medium
            });
        taskResponse.EnsureSuccessStatusCode();

        var task = await taskResponse.Content
            .ReadFromJsonAsync<CreateTaskResponse>();

        return (project.Id, task!.Id);
    }

    /// <summary>
    /// Reads or writes the database directly, bypassing the API. Used to prove
    /// a rejected request left nothing behind, and by the tests that assert the
    /// database's own constraints.
    /// </summary>
    protected async Task WithDbAsync(Func<TaskFlowDbContext, Task> action)
    {
        await using var db = _postgres.CreateDbContext();

        await action(db);
    }
}

/// <summary>
/// A registered user: identity, credentials, bearer token, and the workspace
/// the test is treating them as belonging to.
/// </summary>
public sealed record TestUser(
    Guid Id,
    string Email,
    string Password,
    string Token,
    Guid WorkspaceId);

/// <summary>
/// Two tenants that share nothing. Naming the halves A and B keeps the
/// isolation tests readable: everything with an A belongs to one owner and
/// everything with a B to the other.
/// </summary>
public sealed record TwoTenants(
    TestUser OwnerA,
    Guid WorkspaceA,
    Guid ProjectA,
    Guid TaskA,
    TestUser OwnerB,
    Guid WorkspaceB,
    Guid ProjectB,
    Guid TaskB);
