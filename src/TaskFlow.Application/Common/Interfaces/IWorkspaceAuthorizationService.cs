namespace TaskFlow.Application.Common.Interfaces;

public interface IWorkspaceAuthorizationService
{
    Task EnsureCanViewWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task EnsureCanManageMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task EnsureCanDeleteWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
