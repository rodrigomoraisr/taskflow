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
        CancellationToken cancellationToken = default
    );

    Task<List<TaskItem>> GetPagedAsync(
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default
    );

    Task<int> CountAsync(
        CancellationToken cancellationToken = default
    );
}