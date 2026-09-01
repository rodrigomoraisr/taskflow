using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Interfaces;

public interface IWorkspaceAuthorizationService
{
    Task<WorkspaceRole> GetActiveRoleAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task EnsureCanViewWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceRole> EnsureCanManageMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task EnsureCanDeleteWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task EnsureCanViewProjectsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task EnsureCanManageProjectsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
