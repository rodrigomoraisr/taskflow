using NSubstitute;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Tests.TestSupport;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Tasks;

/// <summary>
/// <c>TaskService</c> orchestration, one layer below HTTP.
///
/// The regression suite in the integration project proves what a caller sees.
/// It cannot tell a service-layer bug from a controller-layer one, and it
/// reaches each service method by a single route, so a method is guarded by
/// however many tests happen to take that route. These tests address the
/// service directly, with the real authorization services underneath it.
///
/// Four things are asserted for each method: that authorization happens, that
/// the repository is called with the workspace id from the request, that a
/// successful call commits exactly once, and that a refused one does not
/// commit at all.
/// </summary>
public sealed class TaskServiceTests
{
    // ---------------------------------------------------------------
    // CreateAsync
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    public async Task CreateAsync_WhenTheRoleCanEdit_ShouldAddTheTaskAndCommitOnce(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var project = context.ArrangeProject();
        var service = context.CreateTaskService();

        // Act
        var response = await service.CreateAsync(
            context.WorkspaceId,
            new CreateTaskRequest
            {
                Title = "New task",
                Description = "Created by a service test",
                ProjectId = project.Id,
                Priority = TaskPriority.High
            });

        // Assert
        await context.Tasks
            .Received(1)
            .AddAsync(
                Arg.Is<TaskItem>(task =>
                    task.WorkspaceId == context.WorkspaceId &&
                    task.ProjectId == project.Id &&
                    task.Id == response.Id),
                Arg.Any<CancellationToken>());

        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task CreateAsync_WhenTheCallerIsAViewer_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Viewer);
        var project = context.ArrangeProject();
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.CreateAsync(
                context.WorkspaceId,
                new CreateTaskRequest
                {
                    Title = "Rejected task",
                    Description = "A viewer cannot create this",
                    ProjectId = project.Id,
                    Priority = TaskPriority.Low
                }));

        await context.Tasks
            .DidNotReceive()
            .AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());

        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task CreateAsync_WhenTheCallerIsNotAMember_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsNonMember();
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => service.CreateAsync(
                context.WorkspaceId,
                new CreateTaskRequest
                {
                    Title = "Rejected task",
                    Description = "An outsider cannot create this",
                    ProjectId = Guid.NewGuid(),
                    Priority = TaskPriority.Low
                }));

        await context.ShouldNotHaveCommittedAsync();
    }

    /// <summary>
    /// The project repository is arranged to answer only at
    /// <c>context.WorkspaceId</c>. A service that looked the project up in any
    /// other workspace gets null and fails this test with a
    /// <c>ProjectNotFoundException</c>, which is exactly the assertion — the
    /// workspace id has to come from the request.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenTheProjectBelongsToAnotherWorkspace_ShouldThrowProjectNotFound()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var foreignProjectId = Guid.NewGuid();
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<ProjectNotFoundException>(
            () => service.CreateAsync(
                context.WorkspaceId,
                new CreateTaskRequest
                {
                    Title = "Rejected task",
                    Description = "Its project lives in another tenant",
                    ProjectId = foreignProjectId,
                    Priority = TaskPriority.Low
                }));

        await context.Projects
            .Received(1)
            .GetByIdAsync(
                foreignProjectId,
                context.WorkspaceId,
                Arg.Any<CancellationToken>());

        await context.ShouldNotHaveCommittedAsync();
    }

    // ---------------------------------------------------------------
    // GetByIdAsync / GetTasksAsync
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task GetByIdAsync_WhenTheCallerIsAnyMember_ShouldReadWithTheRequestWorkspaceId(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var task = context.ArrangeTask();
        var service = context.CreateTaskService();

        // Act
        var response = await service.GetByIdAsync(context.WorkspaceId, task.Id);

        // Assert
        Assert.Equal(task.Id, response.Id);

        await context.Tasks
            .Received(1)
            .GetByIdAsync(
                task.Id,
                context.WorkspaceId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenTheCallerIsNotAMember_ShouldThrowUnauthorizedWorkspaceAccess()
    {
        // Arrange
        var context = new ServiceTestContext().AsNonMember();
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => service.GetByIdAsync(context.WorkspaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetTasksAsync_WhenTheCallerIsAMember_ShouldPageAndCountWithTheSameWorkspaceId()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Viewer);

        context.Tasks
            .GetPagedAsync(
                context.WorkspaceId,
                2,
                10,
                Arg.Any<CancellationToken>())
            .Returns([]);

        context.Tasks
            .CountAsync(context.WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(37);

        var service = context.CreateTaskService();

        // Act
        var response = await service.GetTasksAsync(
            context.WorkspaceId,
            new GetTasksRequest { Page = 2, PageSize = 10 });

        // Assert
        Assert.Equal(37, response.TotalCount);
        Assert.Equal(2, response.Page);
        Assert.Equal(10, response.PageSize);

        await context.Tasks
            .Received(1)
            .GetPagedAsync(
                context.WorkspaceId,
                2,
                10,
                Arg.Any<CancellationToken>());

        await context.Tasks
            .Received(1)
            .CountAsync(context.WorkspaceId, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // UpdateAsync / DeleteAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_WhenTheRoleCanEdit_ShouldUpdateTheTaskAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Member);
        var task = context.ArrangeTask();
        var service = context.CreateTaskService();

        // Act
        await service.UpdateAsync(
            context.WorkspaceId,
            task.Id,
            new UpdateTaskRequest
            {
                Title = "Renamed",
                Description = "Updated by a service test",
                Priority = TaskPriority.Critical
            });

        // Assert
        Assert.Equal("Renamed", task.Title);
        Assert.Equal(TaskPriority.Critical, task.Priority);

        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenTheCallerIsAViewer_ShouldThrowAndLeaveTheTaskUntouched()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Viewer);
        var task = context.ArrangeTask();
        var originalTitle = task.Title;
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.UpdateAsync(
                context.WorkspaceId,
                task.Id,
                new UpdateTaskRequest
                {
                    Title = "Should not stick",
                    Description = "A viewer cannot write this",
                    Priority = TaskPriority.Low
                }));

        Assert.Equal(originalTitle, task.Title);
        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenTheTaskIsInAnotherWorkspace_ShouldThrowTaskNotFound()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var foreignTaskId = Guid.NewGuid();
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<TaskNotFoundException>(
            () => service.UpdateAsync(
                context.WorkspaceId,
                foreignTaskId,
                new UpdateTaskRequest
                {
                    Title = "Should not stick",
                    Description = "Belongs to another tenant",
                    Priority = TaskPriority.Low
                }));

        await context.Tasks
            .Received(1)
            .GetByIdAsync(
                foreignTaskId,
                context.WorkspaceId,
                Arg.Any<CancellationToken>());

        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task DeleteAsync_WhenTheRoleCanEdit_ShouldSoftDeleteAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Admin);
        var task = context.ArrangeTask();
        var service = context.CreateTaskService();

        // Act
        await service.DeleteAsync(context.WorkspaceId, task.Id);

        // Assert
        Assert.True(task.IsDeleted);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task DeleteAsync_WhenTheCallerIsAViewer_ShouldThrowAndLeaveTheTaskActive()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Viewer);
        var task = context.ArrangeTask();
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.DeleteAsync(context.WorkspaceId, task.Id));

        Assert.False(task.IsDeleted);
        await context.ShouldNotHaveCommittedAsync();
    }

    // ---------------------------------------------------------------
    // Status transitions
    // ---------------------------------------------------------------

    [Fact]
    public async Task StartAsync_WhenTheRoleCanChangeStatus_ShouldMoveToInProgressAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Member);
        var task = context.ArrangeTask();
        var service = context.CreateTaskService();

        // Act
        await service.StartAsync(context.WorkspaceId, task.Id);

        // Assert
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task CompleteAsync_WhenTheRoleCanChangeStatus_ShouldMoveToDoneAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Member);
        var task = context.ArrangeTask(TaskItemStatus.InProgress);
        var service = context.CreateTaskService();

        // Act
        await service.CompleteAsync(context.WorkspaceId, task.Id);

        // Assert
        Assert.Equal(TaskItemStatus.Done, task.Status);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task ReopenAsync_WhenTheRoleCanChangeStatus_ShouldMoveToTodoAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Member);
        var task = context.ArrangeTask(TaskItemStatus.Done);
        var service = context.CreateTaskService();

        // Act
        await service.ReopenAsync(context.WorkspaceId, task.Id);

        // Assert
        Assert.Equal(TaskItemStatus.Todo, task.Status);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTheCallerIsAViewer_ShouldThrowAndLeaveTheStatusAlone()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Viewer);
        var task = context.ArrangeTask();
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.StartAsync(context.WorkspaceId, task.Id));

        Assert.Equal(TaskItemStatus.Todo, task.Status);
        await context.ShouldNotHaveCommittedAsync();
    }

    /// <summary>
    /// The entity refuses the transition after the service has already loaded
    /// it. Nothing was mutated, so nothing must be committed — this is the
    /// refusal path where a stray <c>SaveChangesAsync</c> would be easiest to
    /// miss.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenTheTaskIsAlreadyDone_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Member);
        var task = context.ArrangeTask(TaskItemStatus.Done);
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert
            .ThrowsAsync<TaskFlow.Domain.Exceptions.InvalidTaskStatusTransitionException>(
                () => service.StartAsync(context.WorkspaceId, task.Id));

        Assert.Equal(TaskItemStatus.Done, task.Status);
        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task CompleteAsync_WhenTheTaskIsInAnotherWorkspace_ShouldThrowTaskNotFound()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<TaskNotFoundException>(
            () => service.CompleteAsync(context.WorkspaceId, Guid.NewGuid()));

        await context.ShouldNotHaveCommittedAsync();
    }

    // ---------------------------------------------------------------
    // Assignment
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Admin)]
    public async Task AssignAsync_WhenTheCallerManagesTheWorkspace_ShouldAssignAnyoneAndCommitOnce(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var assignee = context.AddOtherMember();
        var task = context.ArrangeTask();
        var service = context.CreateTaskService();

        // Act
        await service.AssignAsync(context.WorkspaceId, task.Id, assignee);

        // Assert
        Assert.Equal(assignee, task.AssigneeUserId);
        await context.ShouldHaveCommittedOnceAsync();
    }

    /// <summary>
    /// A Member may claim an unassigned task for themselves and nothing else.
    /// Asserted through the real <c>TaskAuthorizationService</c>, which is the
    /// only place that rule exists.
    /// </summary>
    [Fact]
    public async Task AssignAsync_WhenAMemberClaimsAnUnassignedTask_ShouldAssignThemAndCommitOnce()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Member);
        var task = context.ArrangeTask();
        var service = context.CreateTaskService();

        // Act
        await service.AssignAsync(
            context.WorkspaceId,
            task.Id,
            context.CallerId);

        // Assert
        Assert.Equal(context.CallerId, task.AssigneeUserId);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Fact]
    public async Task AssignAsync_WhenAMemberAssignsSomeoneElse_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Member);
        var someoneElse = context.AddOtherMember();
        var task = context.ArrangeTask();
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidTaskAssignmentException>(
            () => service.AssignAsync(
                context.WorkspaceId,
                task.Id,
                someoneElse));

        Assert.Null(task.AssigneeUserId);
        await context.ShouldNotHaveCommittedAsync();
    }

    [Fact]
    public async Task AssignAsync_WhenAMemberClaimsAnAlreadyAssignedTask_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Member);
        var incumbent = context.AddOtherMember();
        var task = context.ArrangeTask(assigneeUserId: incumbent);
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidTaskAssignmentException>(
            () => service.AssignAsync(
                context.WorkspaceId,
                task.Id,
                context.CallerId));

        Assert.Equal(incumbent, task.AssigneeUserId);
        await context.ShouldNotHaveCommittedAsync();
    }

    /// <summary>
    /// The assignee has to be a member of *this* workspace. The membership
    /// lookup must therefore carry the workspace id from the request, or an
    /// owner could assign a task to somebody from another tenant.
    /// </summary>
    [Fact]
    public async Task AssignAsync_WhenTheAssigneeIsNotAMember_ShouldThrowAndNotCommit()
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(WorkspaceRole.Owner);
        var task = context.ArrangeTask();
        var outsider = Guid.NewGuid();
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<WorkspaceMemberNotFoundException>(
            () => service.AssignAsync(context.WorkspaceId, task.Id, outsider));

        await context.WorkspaceUsers
            .Received(1)
            .GetActiveMembershipAsync(
                outsider,
                context.WorkspaceId,
                Arg.Any<CancellationToken>());

        Assert.Null(task.AssigneeUserId);
        await context.ShouldNotHaveCommittedAsync();
    }

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Admin)]
    public async Task UnassignAsync_WhenTheCallerManagesTheWorkspace_ShouldClearTheAssigneeAndCommitOnce(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var task = context.ArrangeTask(assigneeUserId: Guid.NewGuid());
        var service = context.CreateTaskService();

        // Act
        await service.UnassignAsync(context.WorkspaceId, task.Id);

        // Assert
        Assert.Null(task.AssigneeUserId);
        await context.ShouldHaveCommittedOnceAsync();
    }

    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Viewer)]
    public async Task UnassignAsync_WhenTheRoleCannotManageMembers_ShouldThrowAndNotCommit(
        WorkspaceRole role)
    {
        // Arrange
        var context = new ServiceTestContext().AsRole(role);
        var assignee = Guid.NewGuid();
        var task = context.ArrangeTask(assigneeUserId: assignee);
        var service = context.CreateTaskService();

        // Act + Assert
        await Assert.ThrowsAsync<InsufficientWorkspaceRoleException>(
            () => service.UnassignAsync(context.WorkspaceId, task.Id));

        Assert.Equal(assignee, task.AssigneeUserId);
        await context.ShouldNotHaveCommittedAsync();
    }
}
