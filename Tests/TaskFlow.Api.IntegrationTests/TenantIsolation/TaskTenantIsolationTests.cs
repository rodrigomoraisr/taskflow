using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.TenantIsolation;

/// <summary>
/// Every task operation, attempted by the owner of a different workspace.
///
/// The assertion is never merely "it failed" — it is "the response is
/// indistinguishable from the workspace not existing". A 403 here would confirm
/// the tenant is real, and confirming that is the enumeration vulnerability.
/// </summary>
public sealed class TaskTenantIsolationTests(PostgreSqlFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetTask_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.GetAsync(
            $"/api/workspaces/{t.WorkspaceA}/tasks/{t.TaskA}");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task ListTasks_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.GetAsync(
            $"/api/workspaces/{t.WorkspaceA}/tasks");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task UpdateTask_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.PutAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceA}/tasks/{t.TaskA}",
            new UpdateTaskRequest
            {
                Title = "Tampered by another tenant",
                Description = "Should never persist",
                Priority = TaskPriority.High
            });

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task DeleteTask_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.DeleteAsync(
            $"/api/workspaces/{t.WorkspaceA}/tasks/{t.TaskA}");

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("reopen")]
    public async Task ChangeTaskStatus_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound(
        string transition)
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.PostAsync(
            $"/api/workspaces/{t.WorkspaceA}/tasks/{t.TaskA}/{transition}",
            content: null);

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task AssignTask_WhenCallerBelongsToAnotherWorkspace_ShouldReturnCanonicalNotFound()
    {
        var t = await ArrangeTwoTenantsAsync();
        var intruder = CreateClientFor(t.OwnerB);

        var response = await intruder.PutAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceA}/tasks/{t.TaskA}/assignee",
            new AssignTaskRequest { UserId = t.OwnerB.Id });

        await TenantResponses
            .ShouldBeIndistinguishableFromMissingWorkspace(
                response, t.WorkspaceA);
    }

    [Fact]
    public async Task GetTask_WhenTaskBelongsToAnotherWorkspace_ShouldNotBeReachableViaOwnWorkspace()
    {
        // The subtler attack: the caller is a legitimate member of workspace B
        // and puts B's id in the route, but A's task id in the path. The
        // workspace check passes; only the tenant-scoped lookup stops this.
        var t = await ArrangeTwoTenantsAsync();
        var callerB = CreateClientFor(t.OwnerB);

        var response = await callerB.GetAsync(
            $"/api/workspaces/{t.WorkspaceB}/tasks/{t.TaskA}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListTasks_WhenTwoTenantsHaveTasks_ShouldReturnOnlyOwnTenantsTasks()
    {
        var t = await ArrangeTwoTenantsAsync();
        var callerA = CreateClientFor(t.OwnerA);

        var response = await callerA.GetAsync(
            $"/api/workspaces/{t.WorkspaceA}/tasks");
        response.EnsureSuccessStatusCode();

        var page = await response.Content
            .ReadFromJsonAsync<GetTasksResponse>();

        Assert.All(page!.Items, item => Assert.NotEqual(t.TaskB, item.Id));
        Assert.Contains(page.Items, item => item.Id == t.TaskA);
    }

    [Fact]
    public async Task CreateTask_WhenProjectBelongsToAnotherWorkspace_ShouldBeRejected()
    {
        // Caller is a legitimate owner of A, but points the new task at a
        // project inside B. Accepting this would write a row spanning two
        // tenants — the worst possible outcome in this system.
        var t = await ArrangeTwoTenantsAsync();
        var callerA = CreateClientFor(t.OwnerA);

        var response = await callerA.PostAsJsonAsync(
            $"/api/workspaces/{t.WorkspaceA}/tasks",
            new CreateTaskRequest
            {
                Title = "Cross-tenant task",
                Description = "Points at another tenant's project",
                ProjectId = t.ProjectB,
                Priority = TaskPriority.Medium
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await WithDbAsync(async db =>
            Assert.False(
                await db.Tasks.AnyAsync(x => x.Title == "Cross-tenant task")));
    }
}
