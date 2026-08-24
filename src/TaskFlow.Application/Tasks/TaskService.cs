using TaskFlow.Application.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Application.Tasks;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IUnitOfWork _unitOfWork;

    public TaskService(
        ITaskRepository taskRepository,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IUnitOfWork unitOfWork)
    {
        _taskRepository = taskRepository;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateTaskResponse> CreateAsync(
        Guid workspaceId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await _workspaceAuthorizationService.EnsureCanViewWorkspaceAsync(
            workspaceId,
            cancellationToken);

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

        await _unitOfWork.SaveChangesAsync(
            cancellationToken
        );

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
        CancellationToken cancellationToken)
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
        await _workspaceAuthorizationService.EnsureCanViewWorkspaceAsync(
            workspaceId,
            cancellationToken);

        var task = await _taskRepository.GetByIdAsync(
            id,
            workspaceId,
            cancellationToken);

        if (task is null)
            throw new TaskNotFoundException(id);

        task.UpdateDetails(
            request.Title,
            request.Description,
            request.Priority,
            request.DueDate,
            request.AssigneeUserId);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DeleteAsync(
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

        task.Delete();

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);
    }
}
