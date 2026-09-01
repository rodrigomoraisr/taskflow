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

    Task StartAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task ReopenAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task AssignAsync(
        Guid workspaceId,
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UnassignAsync(
        Guid workspaceId,
        Guid id,
        CancellationToken cancellationToken = default);
}
