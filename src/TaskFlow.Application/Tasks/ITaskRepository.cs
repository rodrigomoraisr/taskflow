using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Tasks;

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
}