using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface ITaskRepository
{
    Task AddAsync
    (
        TaskItem task,
        CancellationToken cancellationToken = default
    );

    Task<TaskItem?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        CancellationToken cancellationToken = default
    );

    Task<List<TaskItem>> GetPagedAsync(
        Guid workspaceId,
        GetTasksRequest query,
        CancellationToken cancellationToken = default
    );

    Task<int> CountAsync(
        Guid workspaceId,
        GetTasksRequest query,
        CancellationToken cancellationToken = default
    );
}
