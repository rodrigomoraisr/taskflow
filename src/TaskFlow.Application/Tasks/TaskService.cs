using System.Text.Json;
using TaskFlow.Domain.Enums;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Exceptions;

namespace TaskFlow.Application.Tasks;

public class TaskService : ITaskService
{
    private readonly ITaskActivityRepository _activities;
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IProjectRepository _projectRepository;
    private readonly ITaskAuthorizationService _taskAuthorizationService;
    private readonly IWorkspaceUserRepository _workspaceUserRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public TaskService(
        ITaskRepository taskRepository,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IProjectRepository projectRepository,
        ITaskAuthorizationService taskAuthorizationService,
        IWorkspaceUserRepository workspaceUserRepository,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ITaskActivityRepository activities)
    {
        _activities = activities;
        _taskRepository = taskRepository;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _projectRepository = projectRepository;
        _taskAuthorizationService = taskAuthorizationService;
        _workspaceUserRepository = workspaceUserRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateTaskResponse> CreateAsync(
        Guid workspaceId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var role = await _workspaceAuthorizationService.GetActiveRoleAsync(
            workspaceId,
            cancellationToken);

        _taskAuthorizationService.EnsureCanEdit(role);

        var project = await _projectRepository.GetByIdAsync(
            request.ProjectId,
            workspaceId,
            cancellationToken);

        if (project is null)
            throw new ProjectNotFoundException(request.ProjectId);

        var task = new TaskItem(
            request.Title,
            request.Description,
            workspaceId,
            request.ProjectId,
            request.Priority,
            request.DueDate
        );

        await _taskRepository.AddAsync(
            task,
            cancellationToken
        );

        await RecordAndSaveAsync(task, TaskActivityAction.TaskCreated, null, cancellationToken);

        return new CreateTaskResponse
        {
            Id = task.Id
        };
    }

    public async Task<GetTaskResponse> GetByIdAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureCanViewWorkspaceAsync(
            workspaceId,
            cancellationToken);

        var task = await _taskRepository.GetByIdAsync(
            id,
            workspaceId,
            cancellationToken);

        if (task is null)
            throw new TaskNotFoundException(id);

        return new GetTaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            WorkspaceId = task.WorkspaceId,
            ProjectId = task.ProjectId,
            AssigneeUserId = task.AssigneeUserId,
            Status = task.Status,
            Priority = task.Priority,
            CreatedAt = task.CreatedAt,
            DueDate = task.DueDate,
            UpdatedAt = task.UpdatedAt
        };
    }

    public async Task<GetTasksResponse> GetTasksAsync(
        Guid workspaceId,
        GetTasksRequest request,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureCanViewWorkspaceAsync(
            workspaceId,
            cancellationToken);

        var tasks = await _taskRepository.GetPagedAsync(
            workspaceId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalCount = await _taskRepository.CountAsync(
            workspaceId,
            cancellationToken);
            
        return new GetTasksResponse
        {
            Items = tasks.Select(task => new GetTaskResponse
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                WorkspaceId = task.WorkspaceId,
                ProjectId = task.ProjectId,
                AssigneeUserId = task.AssigneeUserId,
                Status = task.Status,
                Priority = task.Priority,
                CreatedAt = task.CreatedAt,
                DueDate = task.DueDate,
                UpdatedAt = task.UpdatedAt
            }).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task UpdateAsync(
        Guid workspaceId,
        Guid id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await _workspaceAuthorizationService.GetActiveRoleAsync(
            workspaceId,
            cancellationToken);

        _taskAuthorizationService.EnsureCanEdit(role);

        var task = await _taskRepository.GetByIdAsync(
            id,
            workspaceId,
            cancellationToken);

        if (task is null)
            throw new TaskNotFoundException(id);

        var before = Snapshot(task);
        task.UpdateDetails(
            request.Title,
            request.Description,
            request.Priority,
            request.DueDate);

        await RecordAndSaveAsync(task, TaskActivityAction.TaskUpdated, before, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var role = await _workspaceAuthorizationService.GetActiveRoleAsync(
            workspaceId,
            cancellationToken);

        _taskAuthorizationService.EnsureCanEdit(role);

        var task = await _taskRepository.GetByIdAsync(
            id,
            workspaceId,
            cancellationToken);

        if (task is null)
            throw new TaskNotFoundException(id);

        var before = Snapshot(task);
        task.Delete();

        await RecordAndSaveAsync(task, TaskActivityAction.TaskDeleted, before, cancellationToken);
    }

    public async Task StartAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var task = await GetTaskForStatusChangeAsync(
            workspaceId,
            id,
            cancellationToken);

        var before = Snapshot(task);
        task.Start();
        await RecordAndSaveAsync(task, TaskActivityAction.TaskStarted, before, cancellationToken);
    }

    public async Task CompleteAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var task = await GetTaskForStatusChangeAsync(
            workspaceId,
            id,
            cancellationToken);

        var before = Snapshot(task);
        task.Complete();
        await RecordAndSaveAsync(task, TaskActivityAction.TaskCompleted, before, cancellationToken);
    }

    public async Task ReopenAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var task = await GetTaskForStatusChangeAsync(
            workspaceId,
            id,
            cancellationToken);

        var before = Snapshot(task);
        task.Reopen();
        await RecordAndSaveAsync(task, TaskActivityAction.TaskReopened, before, cancellationToken);
    }

    public async Task AssignAsync(
        Guid workspaceId,
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var role = await _workspaceAuthorizationService.GetActiveRoleAsync(
            workspaceId,
            cancellationToken);

        var task = await GetActiveTaskAsync(
            workspaceId,
            id,
            cancellationToken);

        _taskAuthorizationService.EnsureCanAssign(
            role,
            _currentUser.UserId,
            userId,
            task.AssigneeUserId);

        if (userId == Guid.Empty)
            throw new ArgumentException("Assignee user ID is required.");

        var assigneeMembership = await _workspaceUserRepository
            .GetActiveMembershipAsync(
                userId,
                workspaceId,
                cancellationToken);

        if (assigneeMembership is null)
            throw new WorkspaceMemberNotFoundException(userId);

        var before = Snapshot(task);
        task.AssignTo(userId);
        await RecordAndSaveAsync(task, TaskActivityAction.TaskAssigned, before, cancellationToken);
    }

    public async Task UnassignAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var role = await _workspaceAuthorizationService.GetActiveRoleAsync(
            workspaceId,
            cancellationToken);

        _taskAuthorizationService.EnsureCanUnassign(role);

        var task = await GetActiveTaskAsync(
            workspaceId,
            id,
            cancellationToken);

        var before = Snapshot(task);
        task.Unassign();
        await RecordAndSaveAsync(task, TaskActivityAction.TaskUnassigned, before, cancellationToken);
    }

    private async Task RecordAndSaveAsync(TaskItem task, TaskActivityAction action,
        string? before, CancellationToken cancellationToken)
    {
        var after = Snapshot(task);
        // Idempotent operations that change nothing should not invent history.
        if (before != after)
        {
            var details = JsonSerializer.Serialize(new
            {
                Before = before is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(before),
                After = JsonSerializer.Deserialize<JsonElement>(after)
            });
            await _activities.AddAsync(new TaskActivity(task.WorkspaceId, task.Id,
                _currentUser.UserId, action, details), cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string Snapshot(TaskItem task) => JsonSerializer.Serialize(new
    {
        task.Title, task.Description, task.Priority, task.DueDate,
        task.Status, task.AssigneeUserId, task.IsDeleted
    });

    private async Task<TaskItem> GetTaskForStatusChangeAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var role = await _workspaceAuthorizationService.GetActiveRoleAsync(
            workspaceId,
            cancellationToken);

        _taskAuthorizationService.EnsureCanChangeStatus(role);

        return await GetActiveTaskAsync(
            workspaceId,
            id,
            cancellationToken);
    }

    private async Task<TaskItem> GetActiveTaskAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(
            id,
            workspaceId,
            cancellationToken);

        if (task is null)
            throw new TaskNotFoundException(id);

        return task;
    }
}
