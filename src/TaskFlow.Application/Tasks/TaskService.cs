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

    public async Task<GetTasksResponse> GetTasksAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if(page <= 0)
        {
            throw new ValidationException("Page must be greater than zero");
        }
        
        var tasks = await _taskRepository.GetPagedAsync(page, pageSize, cancellationToken);

        return new GetTasksResponse
        {
            
        };
    }
}