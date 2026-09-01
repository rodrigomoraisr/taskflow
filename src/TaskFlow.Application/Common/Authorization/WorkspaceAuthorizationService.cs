using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Authorization;

public class WorkspaceAuthorizationService
    : IWorkspaceAuthorizationService
{
    private readonly ICurrentUser _currentUser;
    private readonly IWorkspaceUserRepository _workspaceUserRepository;

    public WorkspaceAuthorizationService(
        ICurrentUser currentUser,
        IWorkspaceUserRepository workspaceUserRepository)
    {
        _currentUser = currentUser;
        _workspaceUserRepository = workspaceUserRepository;
    }

    public async Task<WorkspaceRole> GetActiveRoleAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetActiveMembershipAsync(
            workspaceId,
            cancellationToken);

        return membership.Role;
    }

    public async Task EnsureCanViewWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await GetActiveMembershipAsync(workspaceId, cancellationToken);
    }

    public async Task<WorkspaceRole> EnsureCanManageMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetActiveMembershipAsync(
            workspaceId,
            cancellationToken);

        if (membership.Role != WorkspaceRole.Owner &&
            membership.Role != WorkspaceRole.Admin)
        {
            throw new InsufficientWorkspaceRoleException();
        }

        return membership.Role;
    }

    public async Task EnsureCanDeleteWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetActiveMembershipAsync(
            workspaceId,
            cancellationToken);

        if (membership.Role != WorkspaceRole.Owner)
        {
            throw new InsufficientWorkspaceRoleException();
        }
    }

    public async Task EnsureCanViewProjectsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await GetActiveMembershipAsync(workspaceId, cancellationToken);
    }

    public async Task EnsureCanManageProjectsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetActiveMembershipAsync(
            workspaceId,
            cancellationToken);

        if (membership.Role != WorkspaceRole.Owner &&
            membership.Role != WorkspaceRole.Admin)
        {
            throw new InsufficientWorkspaceRoleException();
        }
    }

    private async Task<WorkspaceUser> GetActiveMembershipAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var membership = await _workspaceUserRepository.GetActiveMembershipAsync(
            _currentUser.UserId,
            workspaceId,
            cancellationToken);

        if (membership is null)
        {
            throw new UnauthorizedWorkspaceAccessException();
        }

        return membership;
    }
}
