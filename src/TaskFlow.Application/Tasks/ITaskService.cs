namespace TaskFlow.Application.Tasks;

public interface ITaskService
{
    Task<CreateTaskResponse> CreateAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default
    );

    Task<GetTaskResponse> GetByIdAsync(
        Guid id, 
        CancellationToken cancellationToken = default);
}