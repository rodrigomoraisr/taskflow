using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Users;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests.Workflows;

public sealed class TaskLifecycleTests(PostgreSqlFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task TaskLifecycle_WhenNewUserCreatesTask_ShouldPersistEachTransition()
    {
        // Keep registration and login visible: their response contracts are
        // part of this journey, not just arrangement hidden in a helper.
        using var client = _factory.CreateClient();
        var email = $"journey-{Guid.NewGuid():N}@taskflow.test";
        const string password = "Integration-Test-Password-1";

        using var registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest { Email = email, Password = password });

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var user = await registration.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(user);
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.NotEqual(Guid.Empty, user.WorkspaceId);
        Assert.Equal(email, user.Email);

        using var login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest { Email = email, Password = password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        Assert.Equal(user.Id, session.UserId);
        Assert.Equal(email, session.Email);
        Assert.False(string.IsNullOrWhiteSpace(session.Token));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.Token);

        // Use an explicitly created workspace, not registration's default.
        // The existing identity token must work for this new membership too.
        using var workspaceCreation = await client.PostAsJsonAsync("/api/workspaces",
            new CreateWorkspaceRequest { Name = "Delivery team" });

        Assert.Equal(HttpStatusCode.Created, workspaceCreation.StatusCode);
        var workspace = await workspaceCreation.Content
            .ReadFromJsonAsync<CreateWorkspaceResponse>();
        Assert.NotNull(workspace);
        Assert.NotEqual(Guid.Empty, workspace.Id);
        Assert.NotEqual(user.WorkspaceId, workspace.Id);
        Assert.Equal(user.Id, workspace.OwnerId);
        Assert.Equal("Delivery team", workspace.Name);

        var workspaceRoute = $"/api/workspaces/{workspace.Id}";
        using var projectCreation = await client.PostAsJsonAsync(
            $"{workspaceRoute}/projects",
            new CreateProjectRequest { Name = "API delivery", Description = "Phase 8" });

        Assert.Equal(HttpStatusCode.Created, projectCreation.StatusCode);
        var project = await projectCreation.Content.ReadFromJsonAsync<CreateProjectResponse>();
        Assert.NotNull(project);
        Assert.NotEqual(Guid.Empty, project.Id);

        using var taskCreation = await client.PostAsJsonAsync(
            $"{workspaceRoute}/tasks",
            new CreateTaskRequest
            {
                Title = "Deliver the API",
                Description = "Exercise the complete HTTP journey",
                ProjectId = project.Id,
                Priority = TaskPriority.High
            });

        Assert.Equal(HttpStatusCode.Created, taskCreation.StatusCode);
        var task = await taskCreation.Content.ReadFromJsonAsync<CreateTaskResponse>();
        Assert.NotNull(task);
        Assert.NotEqual(Guid.Empty, task.Id);
        var taskRoute = $"{workspaceRoute}/tasks/{task.Id}";
        Assert.Equal(taskRoute, taskCreation.Headers.Location?.OriginalString);

        var created = await ReadTaskAsync(client, taskRoute);
        Assert.Equal(task.Id, created.Id);
        Assert.Equal(workspace.Id, created.WorkspaceId);
        Assert.Equal(project.Id, created.ProjectId);
        Assert.Equal("Deliver the API", created.Title);
        Assert.Equal(TaskPriority.High, created.Priority);
        Assert.Equal(TaskItemStatus.Todo, created.Status);

        await ShouldPersistTransitionAsync(client, taskRoute, task.Id,
            "start", TaskItemStatus.InProgress);
        await ShouldPersistTransitionAsync(client, taskRoute, task.Id,
            "complete", TaskItemStatus.Done);
        await ShouldPersistTransitionAsync(client, taskRoute, task.Id,
            "reopen", TaskItemStatus.Todo);
    }

    [Fact]
    public async Task StartTask_WhenAlreadyInProgress_ShouldReturnConflictWithoutChangingStoredTask()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        using var client = CreateClientFor(owner);
        var route = $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}";
        await ShouldPersistTransitionAsync(client, route, taskId,
            "start", TaskItemStatus.InProgress);
        var before = await ReadTaskAsync(client, route);

        using var response = await client.PostAsync($"{route}/start", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await WithDbAsync(async db =>
        {
            var stored = await db.Tasks.FindAsync(taskId);
            Assert.NotNull(stored);
            Assert.Equal(TaskItemStatus.InProgress, stored.Status);
            Assert.Equal(before.UpdatedAt, stored.UpdatedAt);
        });
    }

    private async Task ShouldPersistTransitionAsync(
        HttpClient client, string route, Guid taskId,
        string transition, TaskItemStatus expected)
    {
        using var response = await client.PostAsync($"{route}/{transition}", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(expected, (await ReadTaskAsync(client, route)).Status);

        // A fresh database context proves persistence independently of the
        // GET endpoint and of the request's EF change tracker.
        await WithDbAsync(async db =>
        {
            var stored = await db.Tasks.FindAsync(taskId);
            Assert.NotNull(stored);
            Assert.Equal(expected, stored.Status);
            Assert.NotNull(stored.UpdatedAt);
        });
    }

    private static async Task<GetTaskResponse> ReadTaskAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<GetTaskResponse>();
        Assert.NotNull(task);
        return task;
    }
}
