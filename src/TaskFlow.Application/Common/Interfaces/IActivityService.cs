using TaskFlow.Application.Activity;

namespace TaskFlow.Application.Common.Interfaces;

public interface IActivityService
{
    Task<List<ActivityResponse>> GetPagedAsync(Guid workspaceId, GetActivityRequest request, CancellationToken cancellationToken = default);
}
