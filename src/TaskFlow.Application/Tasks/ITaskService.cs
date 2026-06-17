namespace TaskFlow.Application.Tasks;

public interface ITaskService
{
    Task<CreateTaskResponse> CreateAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default
    );
}