using TaskFlow.Application.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Application.Exceptions;
using System.Net.Cache;
using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Application.Tasks;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TaskService(
        ITaskRepository taskRepository,
        IUnitOfWork unitOfWork
    )
    {
        _taskRepository = taskRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateTaskResponse> CreateAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var task = new TaskItem(
            request.Title,
            request.Description,
            request.WorkspaceId,
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

    public async Task<GetTaskResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(id,cancellationToken);

        if(task is null)
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
            DueDate = task.DueDate
        };
    }

    public async Task<GetTasksResponse> GetTasksAsync(
        GetTasksRequest request, 
        CancellationToken cancellationToken)
    {
        var tasks = await _taskRepository.GetPagedAsync(request.Page, request.PageSize, cancellationToken);

        var totalCount = await _taskRepository.CountAsync(cancellationToken);

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
                DueDate = task.DueDate
            }).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }
}