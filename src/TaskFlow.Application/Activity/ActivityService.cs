using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Application.Activity;

public sealed class ActivityService(
    IWorkspaceAuthorizationService authorization,
    ITaskActivityRepository activities) : IActivityService
{
    public async Task<List<ActivityResponse>> GetPagedAsync(Guid workspaceId,
        GetActivityRequest request, CancellationToken cancellationToken = default)
    {
        await authorization.EnsureCanViewWorkspaceAsync(workspaceId, cancellationToken);
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
            throw new ArgumentException("Invalid pagination.");
        var entries = await activities.GetPagedAsync(workspaceId, request.TaskId,
            request.Page, request.PageSize, cancellationToken);
        return entries.Select(a => new ActivityResponse(a.Id, a.TaskId, a.ActorId,
            a.Action, a.CommentId, a.Details, a.CreatedAt)).ToList();
    }
}
