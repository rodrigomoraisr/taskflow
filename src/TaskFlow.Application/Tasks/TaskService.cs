using TaskFlow.Application.Common;
using TaskFlow.Domain.Entities;

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

}