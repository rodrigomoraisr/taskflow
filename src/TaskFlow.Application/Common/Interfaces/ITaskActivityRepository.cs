using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface ITaskActivityRepository
{
    Task AddAsync(TaskActivity activity, CancellationToken cancellationToken = default);
    Task<List<TaskActivity>> GetPagedAsync(Guid workspaceId, Guid? taskId, int page, int pageSize, CancellationToken cancellationToken = default);
}
