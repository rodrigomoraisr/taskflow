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
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default
    );

    Task<int> CountAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default
    );
}
