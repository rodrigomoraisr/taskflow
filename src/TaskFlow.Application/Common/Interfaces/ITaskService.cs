namespace TaskFlow.Application.Tasks;

public interface ITaskService
{
    Task<CreateTaskResponse> CreateAsync(
        Guid workspaceId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default
    );

    Task<GetTaskResponse> GetByIdAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<GetTasksResponse> GetTasksAsync(
        Guid workspaceId,
        GetTasksRequest request,
        CancellationToken cancellationToken = default
    );

    Task UpdateAsync(
        Guid workspaceId,
        Guid id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default);
}
