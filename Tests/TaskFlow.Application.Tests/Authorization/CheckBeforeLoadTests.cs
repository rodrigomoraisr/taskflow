using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Tests.TestSupport;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Authorization;

/// <summary>
/// An audit, not a confirmation: does every workspace-scoped service method
/// decide whether the caller is allowed **before** it reads anything?
///
/// From outside, a method that loads first and checks second is
/// indistinguishable from one that checks first — both return the same status
/// with the same body, so no amount of HTTP testing can tell them apart. The
/// difference is real anyway. Loading first means the row was fetched on behalf
/// of someone with no right to it, and the timing difference between "loaded
/// then refused" and "refused immediately" is observable to a caller who
/// measures it.
///
/// The instrument is <see cref="ServiceTestContext.MakeEveryReadThrow"/>: every
/// repository read is armed to throw <see cref="RepositoryReachedException"/>,
/// leaving only the membership lookup working. A method that authorizes first
/// still refuses with an authorization exception. A method that loads first
/// surfaces the sentinel, and the assertion names it.
/// </summary>
public sealed class CheckBeforeLoadTests
{
    // ---------------------------------------------------------------
    // The tenant boundary: a non-member must be refused before any read
    // ---------------------------------------------------------------

    public static TheoryData<string, Func<ServiceTestContext, Task>> WorkspaceScopedCalls()
    {
        return new TheoryData<string, Func<ServiceTestContext, Task>>
        {
            { "CommentService.CreateAsync", c => c.CreateCommentService().CreateAsync(c.WorkspaceId, Guid.NewGuid(), new TaskFlow.Application.Comments.WriteCommentRequest { Body = "hello" }) },
            { "CommentService.GetByIdAsync", c => c.CreateCommentService().GetByIdAsync(c.WorkspaceId, Guid.NewGuid(), Guid.NewGuid()) },
            { "CommentService.GetPagedAsync", c => c.CreateCommentService().GetPagedAsync(c.WorkspaceId, Guid.NewGuid(), new TaskFlow.Application.Comments.GetCommentsRequest()) },
            { "CommentService.EditAsync", c => c.CreateCommentService().EditAsync(c.WorkspaceId, Guid.NewGuid(), Guid.NewGuid(), new TaskFlow.Application.Comments.WriteCommentRequest { Body = "hello" }) },
            { "CommentService.DeleteAsync", c => c.CreateCommentService().DeleteAsync(c.WorkspaceId, Guid.NewGuid(), Guid.NewGuid()) },
            { "ActivityService.GetPagedAsync", c => c.CreateActivityService().GetPagedAsync(c.WorkspaceId, new TaskFlow.Application.Activity.GetActivityRequest()) },
            { "TaskService.CreateAsync", c => c.CreateTaskService().CreateAsync(
                c.WorkspaceId,
                new CreateTaskRequest
                {
                    Title = "t",
                    Description = "d",
                    ProjectId = Guid.NewGuid(),
                    Priority = TaskPriority.Low
                }) },

            { "TaskService.GetByIdAsync", c => c.CreateTaskService()
                .GetByIdAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "TaskService.GetTasksAsync", c => c.CreateTaskService()
                .GetTasksAsync(c.WorkspaceId, new GetTasksRequest()) },

            { "TaskService.UpdateAsync", c => c.CreateTaskService().UpdateAsync(
                c.WorkspaceId,
                Guid.NewGuid(),
                new UpdateTaskRequest
                {
                    Title = "t",
                    Description = "d",
                    Priority = TaskPriority.Low
                }) },

            { "TaskService.DeleteAsync", c => c.CreateTaskService()
                .DeleteAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "TaskService.StartAsync", c => c.CreateTaskService()
                .StartAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "TaskService.CompleteAsync", c => c.CreateTaskService()
                .CompleteAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "TaskService.ReopenAsync", c => c.CreateTaskService()
                .ReopenAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "TaskService.AssignAsync", c => c.CreateTaskService()
                .AssignAsync(c.WorkspaceId, Guid.NewGuid(), Guid.NewGuid()) },

            { "TaskService.UnassignAsync", c => c.CreateTaskService()
                .UnassignAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "ProjectService.CreateAsync", c => c.CreateProjectService().CreateAsync(
                c.WorkspaceId,
                new CreateProjectRequest { Name = "n", Description = "d" }) },

            { "ProjectService.GetByIdAsync", c => c.CreateProjectService()
                .GetByIdAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "ProjectService.GetProjectsAsync", c => c.CreateProjectService()
                .GetProjectsAsync(c.WorkspaceId) },

            { "ProjectService.UpdateAsync", c => c.CreateProjectService().UpdateAsync(
                c.WorkspaceId,
                Guid.NewGuid(),
                new UpdateProjectRequest
                {
                    Name = "n",
                    Description = "d",
                    Status = ProjectStatus.Active
                }) },

            { "ProjectService.DeleteAsync", c => c.CreateProjectService()
                .DeleteAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "WorkspaceService.GetByIdAsync", c => c.CreateWorkspaceService()
                .GetByIdAsync(c.WorkspaceId) },

            { "WorkspaceService.DeleteAsync", c => c.CreateWorkspaceService()
                .DeleteAsync(c.WorkspaceId) },

            { "WorkspaceService.GetMembersAsync", c => c.CreateWorkspaceService()
                .GetMembersAsync(c.WorkspaceId) },

            { "WorkspaceService.AddMemberAsync", c => c.CreateWorkspaceService()
                .AddMemberAsync(
                    c.WorkspaceId,
                    new AddWorkspaceMemberRequest { Email = "x@taskflow.test" }) },

            { "WorkspaceService.ChangeMemberRoleAsync", c => c.CreateWorkspaceService()
                .ChangeMemberRoleAsync(
                    c.WorkspaceId,
                    Guid.NewGuid(),
                    new ChangeWorkspaceMemberRoleRequest
                    {
                        Role = WorkspaceRole.Member
                    }) },

            { "WorkspaceService.RemoveMemberAsync", c => c.CreateWorkspaceService()
                .RemoveMemberAsync(c.WorkspaceId, Guid.NewGuid()) }
        };
    }

    [Theory]
    [MemberData(nameof(WorkspaceScopedCalls))]
    public async Task ServiceMethod_WhenTheCallerIsNotAMember_ShouldRefuseBeforeReadingAnything(
        string method,
        Func<ServiceTestContext, Task> call)
    {
        // Arrange
        var context = new ServiceTestContext()
            .AsNonMember()
            .MakeEveryReadThrow();

        // Act + Assert
        var exception = await Record.ExceptionAsync(() => call(context));

        Assert.IsType<UnauthorizedWorkspaceAccessException>(
            exception,
            exactMatch: false);

        await context.ShouldNotHaveCommittedAsync();

        Assert.False(
            exception is RepositoryReachedException,
            $"{method} read from a repository before establishing membership.");
    }

    // ---------------------------------------------------------------
    // The role gate: a member whose role is too low, likewise
    // ---------------------------------------------------------------

    public static TheoryData<string, WorkspaceRole, Func<ServiceTestContext, Task>> RoleGatedCalls()
    {
        return new TheoryData<string, WorkspaceRole, Func<ServiceTestContext, Task>>
        {
            { "CommentService.CreateAsync", WorkspaceRole.Viewer, c => c.CreateCommentService().CreateAsync(c.WorkspaceId, Guid.NewGuid(), new TaskFlow.Application.Comments.WriteCommentRequest { Body = "hello" }) },
            { "CommentService.EditAsync", WorkspaceRole.Viewer, c => c.CreateCommentService().EditAsync(c.WorkspaceId, Guid.NewGuid(), Guid.NewGuid(), new TaskFlow.Application.Comments.WriteCommentRequest { Body = "hello" }) },
            { "CommentService.DeleteAsync", WorkspaceRole.Viewer, c => c.CreateCommentService().DeleteAsync(c.WorkspaceId, Guid.NewGuid(), Guid.NewGuid()) },
            { "TaskService.CreateAsync", WorkspaceRole.Viewer, c => c.CreateTaskService().CreateAsync(
                c.WorkspaceId,
                new CreateTaskRequest
                {
                    Title = "t",
                    Description = "d",
                    ProjectId = Guid.NewGuid(),
                    Priority = TaskPriority.Low
                }) },

            { "TaskService.UpdateAsync", WorkspaceRole.Viewer, c => c.CreateTaskService().UpdateAsync(
                c.WorkspaceId,
                Guid.NewGuid(),
                new UpdateTaskRequest
                {
                    Title = "t",
                    Description = "d",
                    Priority = TaskPriority.Low
                }) },

            { "TaskService.DeleteAsync", WorkspaceRole.Viewer, c => c.CreateTaskService()
                .DeleteAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "TaskService.StartAsync", WorkspaceRole.Viewer, c => c.CreateTaskService()
                .StartAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "TaskService.CompleteAsync", WorkspaceRole.Viewer, c => c.CreateTaskService()
                .CompleteAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "TaskService.ReopenAsync", WorkspaceRole.Viewer, c => c.CreateTaskService()
                .ReopenAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "TaskService.UnassignAsync", WorkspaceRole.Viewer, c => c.CreateTaskService()
                .UnassignAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "ProjectService.CreateAsync", WorkspaceRole.Viewer, c => c.CreateProjectService().CreateAsync(
                c.WorkspaceId,
                new CreateProjectRequest { Name = "n", Description = "d" }) },

            { "ProjectService.UpdateAsync", WorkspaceRole.Member, c => c.CreateProjectService().UpdateAsync(
                c.WorkspaceId,
                Guid.NewGuid(),
                new UpdateProjectRequest
                {
                    Name = "n",
                    Description = "d",
                    Status = ProjectStatus.Active
                }) },

            { "ProjectService.DeleteAsync", WorkspaceRole.Member, c => c.CreateProjectService()
                .DeleteAsync(c.WorkspaceId, Guid.NewGuid()) },

            { "WorkspaceService.DeleteAsync", WorkspaceRole.Admin, c => c.CreateWorkspaceService()
                .DeleteAsync(c.WorkspaceId) },

            { "WorkspaceService.GetMembersAsync", WorkspaceRole.Member, c => c.CreateWorkspaceService()
                .GetMembersAsync(c.WorkspaceId) },

            { "WorkspaceService.AddMemberAsync", WorkspaceRole.Member, c => c.CreateWorkspaceService()
                .AddMemberAsync(
                    c.WorkspaceId,
                    new AddWorkspaceMemberRequest { Email = "x@taskflow.test" }) },

            { "WorkspaceService.ChangeMemberRoleAsync", WorkspaceRole.Member, c => c.CreateWorkspaceService()
                .ChangeMemberRoleAsync(
                    c.WorkspaceId,
                    Guid.NewGuid(),
                    new ChangeWorkspaceMemberRoleRequest
                    {
                        Role = WorkspaceRole.Member
                    }) },

            { "WorkspaceService.RemoveMemberAsync", WorkspaceRole.Member, c => c.CreateWorkspaceService()
                .RemoveMemberAsync(c.WorkspaceId, Guid.NewGuid()) }
        };
    }

    [Theory]
    [MemberData(nameof(RoleGatedCalls))]
    public async Task ServiceMethod_WhenTheRoleIsTooLow_ShouldRefuseBeforeReadingAnything(
        string method,
        WorkspaceRole role,
        Func<ServiceTestContext, Task> call)
    {
        // Arrange
        var context = new ServiceTestContext()
            .AsRole(role)
            .MakeEveryReadThrow();

        // Act + Assert
        var exception = await Record.ExceptionAsync(() => call(context));

        await context.ShouldNotHaveCommittedAsync();

        Assert.False(
            exception is RepositoryReachedException,
            $"{method} read from a repository before checking the caller's role.");

        Assert.IsType<InsufficientWorkspaceRoleException>(
            exception,
            exactMatch: false);
    }

    /// <summary>
    /// The one deliberate exception, documented rather than skipped.
    ///
    /// <c>TaskService.AssignAsync</c> loads the task before it calls
    /// <c>EnsureCanAssign</c>, because the rule being enforced — a Member may
    /// claim an *unassigned* task for themselves and nothing else — reads the
    /// task's current assignee. The entity is an input to the decision, so
    /// there is no ordering in which the decision precedes the load.
    ///
    /// What still holds is the part that matters for tenant isolation: the
    /// membership gate runs first, and the load it precedes is already scoped
    /// to the workspace from the route. A caller who is not a member never
    /// reaches the repository at all — the non-member case above covers
    /// <c>AssignAsync</c> alongside every other method, and it passes.
    /// </summary>
    [Fact]
    public async Task AssignAsync_WhenTheRoleIsTooLow_ShouldLoadTheTaskFirstBecauseTheRuleReadsIt()
    {
        // Arrange
        var context = new ServiceTestContext()
            .AsRole(WorkspaceRole.Viewer)
            .MakeEveryReadThrow();

        var service = context.CreateTaskService();

        // Act
        var exception = await Record.ExceptionAsync(
            () => service.AssignAsync(
                context.WorkspaceId,
                Guid.NewGuid(),
                Guid.NewGuid()));

        // Assert
        Assert.IsType<RepositoryReachedException>(exception);
    }
}
