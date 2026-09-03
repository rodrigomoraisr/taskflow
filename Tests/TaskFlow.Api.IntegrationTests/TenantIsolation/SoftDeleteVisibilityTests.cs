using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.TenantIsolation;

/// <summary>
/// Soft delete is only as good as the read filters. A deleted row that still
/// answers a GET is not soft-deleted, it is just flagged — and that is worse
/// than a hard delete, because everyone believes it is gone.
///
/// Every case here is 404, and that is a settled answer rather than a guess:
/// the repositories filter deleted rows out before the entity's own
/// EnsureNotDeleted guard can fire, so the service raises
/// TaskNotFoundException / ProjectNotFoundException and never the domain's
/// "already deleted" exceptions. A deleted entity is therefore invisible
/// rather than protected, which is the better of the two behaviours: it tells
/// the caller nothing about rows they can no longer see.
/// </summary>
public sealed class SoftDeleteVisibilityTests(PostgreSqlFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetTask_WhenTaskIsSoftDeleted_ShouldReturnNotFound()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        var deletion = await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}");
        deletion.EnsureSuccessStatusCode();

        var read = await client.GetAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task ListTasks_WhenTaskIsSoftDeleted_ShouldNotIncludeIt()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}");

        var list = await client.GetAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks");
        list.EnsureSuccessStatusCode();

        var page = await list.Content.ReadFromJsonAsync<GetTasksResponse>();

        Assert.DoesNotContain(page!.Items, item => item.Id == taskId);
    }

    /// <summary>
    /// The spec left this as "not a 500" because whether the repository filter
    /// or the entity guard wins was not knowable statically. It is the filter:
    /// the row is gone from the query before TaskAlreadyDeletedException could
    /// ever be raised, so the answer is a plain 404.
    /// </summary>
    [Fact]
    public async Task UpdateTask_WhenTaskIsSoftDeleted_ShouldReturnNotFound()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}");

        var update = await client.PutAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}",
            new UpdateTaskRequest
            {
                Title = "Edit after delete",
                Description = "x",
                Priority = TaskPriority.Low
            });

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        await WithDbAsync(async db =>
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == taskId);
            Assert.NotEqual("Edit after delete", task.Title);
        });
    }

    [Fact]
    public async Task DeleteTask_WhenTaskIsAlreadySoftDeleted_ShouldReturnNotFound()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        var first = await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}");
        first.EnsureSuccessStatusCode();

        var second = await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("reopen")]
    public async Task ChangeTaskStatus_WhenTaskIsSoftDeleted_ShouldReturnNotFound(
        string transition)
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}");

        var response = await client.PostAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}/{transition}",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProject_WhenProjectIsSoftDeleted_ShouldReturnNotFound()
    {
        var owner = await RegisterUserAsync();
        var (projectId, _) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        var deletion = await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects/{projectId}");
        deletion.EnsureSuccessStatusCode();

        var read = await client.GetAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects/{projectId}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_WhenProjectIsSoftDeleted_ShouldReturnNotFound()
    {
        var owner = await RegisterUserAsync();
        var (projectId, _) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects/{projectId}");

        var update = await client.PutAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects/{projectId}",
            new UpdateProjectRequest
            {
                Name = "Edit after delete",
                Description = "x",
                Status = ProjectStatus.Active
            });

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_WhenProjectIsAlreadySoftDeleted_ShouldReturnNotFound()
    {
        var owner = await RegisterUserAsync();
        var (projectId, _) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        var first = await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects/{projectId}");
        first.EnsureSuccessStatusCode();

        var second = await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects/{projectId}");

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WhenProjectIsSoftDeleted_ShouldReturnNotFound()
    {
        // Creating a task under a deleted project would resurrect it in effect:
        // the project is invisible, but its children would not be.
        var owner = await RegisterUserAsync();
        var (projectId, _) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects/{projectId}");

        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks",
            new CreateTaskRequest
            {
                Title = "Orphan task",
                Description = "x",
                ProjectId = projectId,
                Priority = TaskPriority.Medium
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await WithDbAsync(async db =>
            Assert.False(await db.Tasks.AnyAsync(t => t.Title == "Orphan task")));
    }

    [Fact]
    public async Task GetTask_WhenItsProjectIsSoftDeleted_ShouldReturnNotFound()
    {
        // Deleting a project hides its tasks too, even though their own
        // IsDeleted flag is still false: the task lookup joins through the
        // active project. Worth pinning explicitly, because it is the
        // difference between archiving a project and leaving its contents
        // reachable by direct id — the latter would make "archived" a lie.
        var owner = await RegisterUserAsync();
        var (projectId, taskId) = await SeedProjectAndTaskAsync(owner);
        var client = CreateClientFor(owner);

        await client.DeleteAsync(
            $"/api/workspaces/{owner.WorkspaceId}/projects/{projectId}");

        var read = await client.GetAsync(
            $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        // The row itself is untouched — this is a read-filter property, not a
        // cascade. If a cascade is ever added, this assertion is the warning.
        await WithDbAsync(async db =>
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == taskId);
            Assert.False(task.IsDeleted);
        });
    }
}
