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

    Task<GetTasksResponse> GetTasksAsync(
        GetTasksRequest request,
        CancellationToken cancellationToken = default
    );

    Task UpdateAsync(
    Guid id,
    UpdateTaskRequest request,
    CancellationToken cancellationToken = default);

    Task DeleteAsync(
    Guid id,
    CancellationToken cancellationToken = default);
}