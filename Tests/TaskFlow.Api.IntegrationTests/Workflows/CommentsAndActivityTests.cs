using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Activity;
using TaskFlow.Application.Comments;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Repositories;

namespace TaskFlow.Api.IntegrationTests.Workflows;

public sealed class CommentsAndActivityTests(PostgreSqlFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task Comments_WhenAuthorCreatesEditsAndDeletes_ShouldKeepHistoryWithoutCopyingBody()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        using var client = CreateClientFor(owner);
        var route = Route(owner.WorkspaceId, taskId);
        var comment = await CreateCommentAsync(client, route);
        using var edit = await client.PutAsJsonAsync($"{route}/{comment.Id}",
            new WriteCommentRequest { Body = "revised private text" });
        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
        var revised = await client.GetFromJsonAsync<CommentResponse>($"{route}/{comment.Id}");
        Assert.Equal("revised private text", revised!.Body);

        using var deletion = await client.DeleteAsync($"{route}/{comment.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        using var hidden = await client.GetAsync($"{route}/{comment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<CommentResponse>>(route))!);
        var history = await HistoryAsync(client, owner.WorkspaceId, taskId);
        Assert.Equal(new[] { TaskActivityAction.CommentDeleted, TaskActivityAction.CommentEdited,
            TaskActivityAction.CommentCreated, TaskActivityAction.TaskCreated }, history.Select(a => a.Action));
        Assert.All(history, a => Assert.Equal(owner.Id, a.ActorId));
        Assert.All(history.Where(a => a.CommentId.HasValue), a =>
        {
            Assert.Equal(comment.Id, a.CommentId);
            using var details = JsonDocument.Parse(a.Details);
            Assert.Empty(details.RootElement.EnumerateObject());
        });
        await WithDbAsync(async db =>
        {
            var stored = await db.Comments.FindAsync(comment.Id);
            Assert.True(stored!.IsDeleted);
            Assert.NotNull(stored.DeletedAt);
        });
    }

    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Owner)]
    public async Task Comments_WhenAnotherMemberIsAuthor_ShouldForbidEditAndDelete(WorkspaceRole role)
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var other = await AddMemberAsync(owner, owner.WorkspaceId, role);
        using var author = CreateClientFor(owner);
        using var caller = CreateClientFor(other);
        var route = Route(owner.WorkspaceId, taskId);
        var comment = await CreateCommentAsync(author, route);

        using var edit = await caller.PutAsJsonAsync($"{route}/{comment.Id}", new WriteCommentRequest { Body = "stolen" });
        using var deletion = await caller.DeleteAsync($"{route}/{comment.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deletion.StatusCode);
        Assert.Equal("private comment text", (await author.GetFromJsonAsync<CommentResponse>($"{route}/{comment.Id}"))!.Body);
        Assert.Equal(2, (await HistoryAsync(author, owner.WorkspaceId, taskId)).Count);
    }

    [Fact]
    public async Task Comments_WhenAuthorBecomesViewer_ShouldAllowReadsButRejectAllWrites()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        var member = await AddMemberAsync(owner, owner.WorkspaceId, WorkspaceRole.Member);
        using var client = CreateClientFor(member);
        using var manager = CreateClientFor(owner);
        var route = Route(owner.WorkspaceId, taskId);
        var comment = await CreateCommentAsync(client, route);
        using var demotion = await manager.PatchAsJsonAsync(
            $"/api/workspaces/{owner.WorkspaceId}/members/{member.Id}/role",
            new TaskFlow.Application.Workspaces.ChangeWorkspaceMemberRoleRequest { Role = WorkspaceRole.Viewer });
        Assert.Equal(HttpStatusCode.NoContent, demotion.StatusCode);

        using var create = await client.PostAsJsonAsync(route, new WriteCommentRequest { Body = "blocked" });
        using var edit = await client.PutAsJsonAsync($"{route}/{comment.Id}", new WriteCommentRequest { Body = "blocked" });
        using var deletion = await client.DeleteAsync($"{route}/{comment.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deletion.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<List<CommentResponse>>(route))!);
        Assert.Equal(2, (await HistoryAsync(client, owner.WorkspaceId, taskId)).Count);
    }

    [Fact]
    public async Task Comments_WhenRouteOrTenantDoesNotMatch_ShouldHideRowsAndPreserveHistory()
    {
        var tenants = await ArrangeTwoTenantsAsync();
        using var a = CreateClientFor(tenants.OwnerA);
        using var b = CreateClientFor(tenants.OwnerB);
        var originalRoute = Route(tenants.WorkspaceA, tenants.TaskA);
        var comment = await CreateCommentAsync(a, originalRoute);
        var (_, otherTask) = await SeedProjectAndTaskAsync(tenants.OwnerA, "second");
        foreach (var (client, route) in new[]
        {
            (b, originalRoute), // not a member
            (b, Route(tenants.WorkspaceB, tenants.TaskA)), // own tenant, foreign task
            (a, Route(tenants.WorkspaceA, otherTask)) // right tenant, wrong parent task
        })
        {
            using var get = await client.GetAsync($"{route}/{comment.Id}");
            using var edit = await client.PutAsJsonAsync($"{route}/{comment.Id}", new WriteCommentRequest { Body = "blocked" });
            using var deletion = await client.DeleteAsync($"{route}/{comment.Id}");
            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, deletion.StatusCode);
        }
        using var foreignFeed = await b.GetAsync($"/api/workspaces/{tenants.WorkspaceA}/activity");
        Assert.Equal(HttpStatusCode.NotFound, foreignFeed.StatusCode);
        Assert.Empty(await HistoryAsync(b, tenants.WorkspaceB, tenants.TaskA));
        Assert.Equal(2, (await HistoryAsync(a, tenants.WorkspaceA, tenants.TaskA)).Count);

        // Direct calls prove repository filters, independently of the gates.
        await WithDbAsync(async db =>
        {
            var comments = new CommentRepository(db);
            Assert.Null(await comments.GetByIdAsync(comment.Id, tenants.TaskA, tenants.WorkspaceB));
            Assert.Null(await comments.GetByIdAsync(comment.Id, otherTask, tenants.WorkspaceA));
            Assert.Empty(await comments.GetPagedAsync(tenants.TaskA, tenants.WorkspaceB, 1, 20));
            Assert.Empty(await new TaskActivityRepository(db).GetPagedAsync(tenants.WorkspaceB, tenants.TaskA, 1, 20));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Comments_WhenTaskOrProjectDeleted_ShouldHideCommentsButRetainActivity(bool deleteTask)
    {
        var owner = await RegisterUserAsync();
        var (projectId, taskId) = await SeedProjectAndTaskAsync(owner);
        using var client = CreateClientFor(owner);
        var route = Route(owner.WorkspaceId, taskId);
        var comment = await CreateCommentAsync(client, route);
        using var deletion = await client.DeleteAsync(deleteTask
            ? $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}"
            : $"/api/workspaces/{owner.WorkspaceId}/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);

        using var get = await client.GetAsync(route);
        using var create = await client.PostAsJsonAsync(route, new WriteCommentRequest { Body = "hidden" });
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        var history = await HistoryAsync(client, owner.WorkspaceId, taskId);
        Assert.Equal(deleteTask ? 3 : 2, history.Count);
        if (deleteTask) Assert.Equal(TaskActivityAction.TaskDeleted, history[0].Action);
        await WithDbAsync(async db =>
        {
            var repository = new CommentRepository(db);
            Assert.Null(await repository.GetByIdAsync(comment.Id, taskId, owner.WorkspaceId));
            Assert.Empty(await repository.GetPagedAsync(taskId, owner.WorkspaceId, 1, 20));
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateComment_WhenBodyBlank_ShouldReturnBadRequestWithoutActivity(string? body)
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        using var client = CreateClientFor(owner);

        using var response = await client.PostAsJsonAsync(Route(owner.WorkspaceId, taskId), new { Body = body });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(await HistoryAsync(client, owner.WorkspaceId, taskId));
    }

    [Fact]
    public async Task Lists_WhenPaged_ShouldReturnStableDisjointPagesAndRejectInvalidBounds()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        using var client = CreateClientFor(owner);
        var route = Route(owner.WorkspaceId, taskId);
        await CreateCommentAsync(client, route);
        await CreateCommentAsync(client, route);
        var first = await client.GetFromJsonAsync<List<CommentResponse>>($"{route}?pageSize=1&page=1");
        var second = await client.GetFromJsonAsync<List<CommentResponse>>($"{route}?pageSize=1&page=2");
        Assert.Single(first!);
        Assert.Single(second!);
        Assert.NotEqual(first![0].Id, second![0].Id);
        var feed = $"/api/workspaces/{owner.WorkspaceId}/activity";
        var h1 = await client.GetFromJsonAsync<List<ActivityResponse>>($"{feed}?pageSize=1&page=1");
        var h2 = await client.GetFromJsonAsync<List<ActivityResponse>>($"{feed}?pageSize=1&page=2");
        Assert.Single(h1!);
        Assert.Single(h2!);
        Assert.NotEqual(h1![0].Id, h2![0].Id);
        foreach (var url in new[] { route, feed })
        {
            using var bad = await client.GetAsync($"{url}?pageSize=101");
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            using var zero = await client.GetAsync($"{url}?page=0");
            Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        }
    }

    [Fact]
    public async Task TaskChanges_WhenSuccessful_ShouldRecordBeforeAfterAndSkipRejectedOrNoOpActions()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        using var client = CreateClientFor(owner);
        var route = $"/api/workspaces/{owner.WorkspaceId}/tasks/{taskId}";
        using var update = await client.PutAsJsonAsync(route,
            new UpdateTaskRequest { Title = "updated", Description = "details", Priority = TaskPriority.High });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        using var assign = await client.PutAsJsonAsync($"{route}/assignee", new AssignTaskRequest { UserId = owner.Id });
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        using var unassign = await client.DeleteAsync($"{route}/assignee");
        using var noop = await client.DeleteAsync($"{route}/assignee");
        Assert.Equal(HttpStatusCode.NoContent, unassign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, noop.StatusCode);
        foreach (var action in new[] { "start", "complete", "reopen" })
        {
            using var transition = await client.PostAsync($"{route}/{action}", null);
            Assert.Equal(HttpStatusCode.NoContent, transition.StatusCode);
        }
        using var rejected = await client.PostAsync($"{route}/reopen", null);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        var history = await HistoryAsync(client, owner.WorkspaceId, taskId);

        Assert.Equal(new[] { TaskActivityAction.TaskReopened, TaskActivityAction.TaskCompleted,
            TaskActivityAction.TaskStarted, TaskActivityAction.TaskUnassigned, TaskActivityAction.TaskAssigned,
            TaskActivityAction.TaskUpdated, TaskActivityAction.TaskCreated }, history.Select(x => x.Action));
        var updated = JsonDocument.Parse(history.Single(x => x.Action == TaskActivityAction.TaskUpdated).Details);
        Assert.Equal("Seed task", updated.RootElement.GetProperty("Before").GetProperty("Title").GetString());
        Assert.Equal("updated", updated.RootElement.GetProperty("After").GetProperty("Title").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_WhenExistingActivityMutated_ShouldRejectIt(bool delete)
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        await WithDbAsync(async db =>
        {
            var activity = await db.TaskActivities.SingleAsync(a => a.TaskId == taskId);
            if (delete) db.TaskActivities.Remove(activity);
            else db.Entry(activity).Property(a => a.Details).CurrentValue = "{}";

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        });
        await WithDbAsync(async db => Assert.Single(await db.TaskActivities.Where(a => a.TaskId == taskId).ToListAsync()));
    }

    [Fact]
    public async Task SaveChanges_WhenActivityInsertFails_ShouldRollBackTaskChange()
    {
        var owner = await RegisterUserAsync();
        var (_, taskId) = await SeedProjectAndTaskAsync(owner);
        await WithDbAsync(async db =>
        {
            var task = await db.Tasks.FindAsync(taskId);
            task!.Start();
            db.TaskActivities.Add(new TaskActivity(owner.WorkspaceId, taskId,
                Guid.NewGuid(), TaskActivityAction.TaskStarted, "{}")); // nonexistent actor violates FK

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
        await WithDbAsync(async db =>
        {
            Assert.Equal(TaskItemStatus.Todo, (await db.Tasks.FindAsync(taskId))!.Status);
            Assert.Single(await db.TaskActivities.Where(a => a.TaskId == taskId).ToListAsync());
        });
    }

    [Fact]
    public async Task SaveChanges_WhenCommentReferencesForeignTask_ShouldRejectCompositeForeignKey()
    {
        var t = await ArrangeTwoTenantsAsync();
        await WithDbAsync(async db =>
        {
            db.Comments.Add(new Comment(t.WorkspaceB, t.TaskA, t.OwnerB.Id, "foreign"));

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        });
    }

    private static string Route(Guid workspaceId, Guid taskId) =>
        $"/api/workspaces/{workspaceId}/tasks/{taskId}/comments";

    private static async Task<CommentResponse> CreateCommentAsync(HttpClient client, string route)
    {
        using var response = await client.PostAsJsonAsync(route, new WriteCommentRequest { Body = "private comment text" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comment = await response.Content.ReadFromJsonAsync<CommentResponse>();
        Assert.NotNull(comment);
        using var location = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        return comment;
    }

    private static async Task<List<ActivityResponse>> HistoryAsync(HttpClient client, Guid workspaceId, Guid taskId) =>
        (await client.GetFromJsonAsync<List<ActivityResponse>>(
            $"/api/workspaces/{workspaceId}/activity?taskId={taskId}&pageSize=100"))!;
}
