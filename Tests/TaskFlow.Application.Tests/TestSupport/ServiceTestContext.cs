using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Users;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.TestSupport.Builders;

namespace TaskFlow.Application.Tests.TestSupport;

/// <summary>
/// Wiring shared by every service test.
///
/// The split matters more than the plumbing. **Repositories, the unit of work
/// and the current user are substitutes** — they are the seams the service
/// talks through, and faking them is what makes it possible to assert the
/// service calls them with the right workspace id and commits exactly once.
/// **The two authorization services are real.** They are the behaviour under
/// test: a suite that substituted <c>IWorkspaceAuthorizationService</c> could
/// assert that a service asks permission, but never that the answer is right,
/// and every role and tenant assertion below would pass against an
/// authorization service that returned Owner for everyone.
///
/// The consequence is that arranging a caller's role means seeding a
/// membership row into <see cref="WorkspaceUsers"/>, exactly as the real
/// authorization service would find one — see <see cref="AsRole"/>.
/// </summary>
public sealed class ServiceTestContext
{
    public ITaskRepository Tasks { get; } = Substitute.For<ITaskRepository>();

    public IProjectRepository Projects { get; } =
        Substitute.For<IProjectRepository>();

    public IWorkspaceRepository Workspaces { get; } =
        Substitute.For<IWorkspaceRepository>();

    public IWorkspaceUserRepository WorkspaceUsers { get; } =
        Substitute.For<IWorkspaceUserRepository>();

    public IUserRepository Users { get; } = Substitute.For<IUserRepository>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    /// <summary>The real thing, not a substitute. See the class remarks.</summary>
    public WorkspaceAuthorizationService WorkspaceAuthorization { get; }

    /// <summary>The real thing, not a substitute. See the class remarks.</summary>
    public TaskAuthorizationService TaskAuthorization { get; } = new();

    /// <summary>The caller every test acts as, unless it says otherwise.</summary>
    public Guid CallerId { get; } = Guid.NewGuid();

    /// <summary>The workspace in the route.</summary>
    public Guid WorkspaceId { get; } = Guid.NewGuid();

    /// <summary>
    /// A second, unrelated tenant. Present in every context so a test can ask
    /// "was the repository called with the workspace from the request, or with
    /// some other one?" and have a concrete wrong answer to check against.
    /// </summary>
    public Guid OtherWorkspaceId { get; } = Guid.NewGuid();

    public ServiceTestContext()
    {
        CurrentUser.UserId.Returns(CallerId);

        WorkspaceAuthorization = new WorkspaceAuthorizationService(
            CurrentUser,
            WorkspaceUsers);
    }

    /// <summary>
    /// Gives the caller an active membership in <see cref="WorkspaceId"/> with
    /// the given role, by arranging the lookup the real authorization service
    /// makes.
    /// </summary>
    public ServiceTestContext AsRole(WorkspaceRole role)
    {
        var membership = new WorkspaceUserBuilder()
            .ForUser(CallerId)
            .InWorkspace(WorkspaceId)
            .AsRole(role)
            .Build();

        WorkspaceUsers
            .GetActiveMembershipAsync(
                CallerId,
                WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(membership);

        return this;
    }

    /// <summary>
    /// Leaves the caller a stranger to every workspace. This is the default —
    /// an unconfigured substitute returns null — but saying it out loud makes
    /// the tenant-boundary tests read as deliberate rather than as tests that
    /// forgot to arrange anything.
    /// </summary>
    public ServiceTestContext AsNonMember()
    {
        WorkspaceUsers
            .GetActiveMembershipAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns((WorkspaceUser?)null);

        return this;
    }

    /// <summary>
    /// Arranges a member of <see cref="WorkspaceId"/> other than the caller,
    /// and returns their user id. Used by the assignment tests.
    /// </summary>
    public Guid AddOtherMember(WorkspaceRole role = WorkspaceRole.Member)
    {
        var userId = Guid.NewGuid();

        var membership = new WorkspaceUserBuilder()
            .ForUser(userId)
            .InWorkspace(WorkspaceId)
            .AsRole(role)
            .Build();

        WorkspaceUsers
            .GetActiveMembershipAsync(
                userId,
                WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(membership);

        return userId;
    }

    /// <summary>
    /// An active project in <see cref="WorkspaceId"/>, findable by the project
    /// repository at that workspace and nowhere else.
    /// </summary>
    public Project ArrangeProject()
    {
        var project = new ProjectBuilder()
            .InWorkspace(WorkspaceId)
            .Build();

        Projects
            .GetByIdAsync(
                project.Id,
                WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(project);

        return project;
    }

    /// <summary>
    /// An active task in <see cref="WorkspaceId"/>, findable by the task
    /// repository at that workspace and nowhere else — which is what lets a
    /// test assert the service passed the workspace id from the route rather
    /// than one it invented.
    /// </summary>
    public TaskItem ArrangeTask(
        TaskItemStatus status = TaskItemStatus.Todo,
        Guid? assigneeUserId = null)
    {
        var builder = new TaskItemBuilder()
            .InWorkspace(WorkspaceId)
            .InProject(Guid.NewGuid())
            .WithStatus(status);

        if (assigneeUserId is not null)
            builder = builder.AssignedTo(assigneeUserId.Value);

        var task = builder.Build();

        Tasks
            .GetByIdAsync(
                task.Id,
                WorkspaceId,
                Arg.Any<CancellationToken>())
            .Returns(task);

        return task;
    }

    public TaskService CreateTaskService()
    {
        return new TaskService(
            Tasks,
            WorkspaceAuthorization,
            Projects,
            TaskAuthorization,
            WorkspaceUsers,
            CurrentUser,
            UnitOfWork);
    }

    public ProjectService CreateProjectService()
    {
        return new ProjectService(
            Projects,
            WorkspaceAuthorization,
            UnitOfWork);
    }

    public WorkspaceService CreateWorkspaceService()
    {
        return new WorkspaceService(
            Workspaces,
            WorkspaceUsers,
            WorkspaceAuthorization,
            Users,
            UnitOfWork);
    }

    /// <summary>
    /// Arms every repository read *except* the membership lookup so that
    /// touching it throws <see cref="RepositoryReachedException"/>.
    ///
    /// This is the instrument for the check-before-load audit. A service that
    /// authorizes before it loads still refuses with an authorization
    /// exception under these conditions; a service that loads first surfaces
    /// the sentinel instead, and the test says so by name. The membership
    /// lookup is left alone because the real authorization service reads
    /// through it — arming that one would prove nothing but that the
    /// instrument works.
    /// </summary>
    public ServiceTestContext MakeEveryReadThrow()
    {
        var reached = new RepositoryReachedException();

        Tasks.GetByIdAsync(default, default, default).ThrowsAsyncForAnyArgs(reached);
        Tasks.GetPagedAsync(default, default, default, default).ThrowsAsyncForAnyArgs(reached);
        Tasks.CountAsync(default, default).ThrowsAsyncForAnyArgs(reached);

        Projects.GetByIdAsync(default, default, default).ThrowsAsyncForAnyArgs(reached);
        Projects.GetByWorkspaceAsync(default, default).ThrowsAsyncForAnyArgs(reached);

        Workspaces.GetByIdAsync(default, default).ThrowsAsyncForAnyArgs(reached);
        Workspaces.GetByIdsAsync(default!, default).ThrowsAsyncForAnyArgs(reached);

        Users.GetByEmailAsync(default!, default).ThrowsAsyncForAnyArgs(reached);
        Users.GetByIdAsync(default, default).ThrowsAsyncForAnyArgs(reached);
        Users.GetByIdsAsync(default!, default).ThrowsAsyncForAnyArgs(reached);

        WorkspaceUsers.GetActiveByWorkspaceAsync(default, default).ThrowsAsyncForAnyArgs(reached);
        WorkspaceUsers.GetByUserAndWorkspaceAsync(default, default, default).ThrowsAsyncForAnyArgs(reached);
        WorkspaceUsers.CountActiveOwnersAsync(default, default).ThrowsAsyncForAnyArgs(reached);

        return this;
    }

    /// <summary>
    /// The assertion that goes on every refusal path. A service that mutates
    /// an entity and only then throws leaves the change tracker dirty, and the
    /// next commit in the same scope persists the change the refusal was
    /// supposed to prevent — so "it threw" is only half the assertion.
    /// </summary>
    public async Task ShouldNotHaveCommittedAsync()
    {
        await UnitOfWork
            .DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    public async Task ShouldHaveCommittedOnceAsync()
    {
        await UnitOfWork
            .Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Raised by any repository read while the check-before-load audit is armed.
/// Seeing this instead of an authorization exception means the service reached
/// the database before it decided whether the caller was allowed to.
/// </summary>
public sealed class RepositoryReachedException : Exception
{
    public RepositoryReachedException()
        : base("A repository was read before authorization completed.")
    {
    }
}
