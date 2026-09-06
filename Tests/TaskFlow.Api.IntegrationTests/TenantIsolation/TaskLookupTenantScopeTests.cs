using System.Net;
using System.Net.Http.Json;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.TenantIsolation;

/// <summary>
/// The attack that gets past the membership gate.
///
/// Most cross-tenant requests put someone else's workspace id in the route, and
/// those never reach a repository: <c>IWorkspaceAuthorizationService</c> finds
/// no membership and refuses. This suite does the opposite. The caller is a
/// genuine owner of workspace B and puts **B's own id** in the route — so the
/// membership check passes — and then supplies a task id belonging to
/// workspace A. Nothing is left between the request and the data except the
/// <c>workspaceId</c> predicate inside <c>TaskRepository.GetByIdAsync</c>.
///
/// Every route that resolves a task by id gets its own test, because every one
/// of them is a separate call site of that lookup. Before 8.4 only the GET
/// route was covered this way, which is why deliberately dropping that filter
/// used to turn a single test red: the other seven routes had no test that
/// reached far enough to notice.
/// </summary>
public sealed class TaskLookupTenantScopeTests(PostgreSqlFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetTask_WhenTheTaskIdBelongsToAnotherWorkspace_ShouldReturnNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.GetAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskA}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_WhenTheTaskIdBelongsToAnotherWorkspace_ShouldReturnNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.PutAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskA}",
            new UpdateTaskRequest
            {
                Title = "Tampered from the neighbouring tenant",
                Description = "Should never persist",
                Priority = TaskPriority.High
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ShouldStillBelongToTenantAAsync(t);
    }

    [Fact]
    public async Task DeleteTask_WhenTheTaskIdBelongsToAnotherWorkspace_ShouldReturnNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.DeleteAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskA}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ShouldStillBelongToTenantAAsync(t);
    }

    [Fact]
    public async Task StartTask_WhenTheTaskIdBelongsToAnotherWorkspace_ShouldReturnNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.PostAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskA}/start",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ShouldStillBelongToTenantAAsync(t);
    }

    [Fact]
    public async Task CompleteTask_WhenTheTaskIdBelongsToAnotherWorkspace_ShouldReturnNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.PostAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskA}/complete",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ShouldStillBelongToTenantAAsync(t);
    }

    [Fact]
    public async Task ReopenTask_WhenTheTaskIdBelongsToAnotherWorkspace_ShouldReturnNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.PostAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskA}/reopen",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ShouldStillBelongToTenantAAsync(t);
    }

    [Fact]
    public async Task AssignTask_WhenTheTaskIdBelongsToAnotherWorkspace_ShouldReturnNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.PutAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskA}/assignee",
            new AssignTaskRequest { UserId = t.OwnerB.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ShouldStillBelongToTenantAAsync(t);
    }

    [Fact]
    public async Task UnassignTask_WhenTheTaskIdBelongsToAnotherWorkspace_ShouldReturnNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.DeleteAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskA}/assignee");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ShouldStillBelongToTenantAAsync(t);
    }

    /// <summary>
    /// A refused request must also leave nothing behind. Read straight from the
    /// database rather than through the API, so a second authorization bug
    /// cannot hide the first one.
    /// </summary>
    private async Task ShouldStillBelongToTenantAAsync(TwoTenants t)
    {
        await WithDbAsync(async db =>
        {
            var task = await db.Tasks.FindAsync(t.TaskA);

            Assert.NotNull(task);
            Assert.Equal(t.WorkspaceA, task.WorkspaceId);
            Assert.False(task.IsDeleted);
            Assert.Equal(TaskItemStatus.Todo, task.Status);
            Assert.Null(task.AssigneeUserId);
            Assert.Equal("Tenant A task", task.Title);
        });
    }
}
