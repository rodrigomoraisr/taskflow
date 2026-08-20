using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Authorization;

public class WorkspaceAuthorizationService
    : IWorkspaceAuthorizationService
{
    private readonly ICurrentWorkspace _currentWorkspace;

    public WorkspaceAuthorizationService(
        ICurrentWorkspace currentWorkspace)
    {
        _currentWorkspace = currentWorkspace;
    }

    public void EnsureCanViewWorkspace(
        Guid workspaceId)
    {
        EnsureSameWorkspace(workspaceId);
    }

    public void EnsureCanManageMembers(
        Guid workspaceId)
    {
        EnsureSameWorkspace(workspaceId);

        if (_currentWorkspace.Role != WorkspaceRole.Owner &&
            _currentWorkspace.Role != WorkspaceRole.Admin)
        {
            throw new InsufficientWorkspaceRoleException();
        }
    }

    public void EnsureCanDeleteWorkspace(
        Guid workspaceId)
    {
        EnsureSameWorkspace(workspaceId);

        if (_currentWorkspace.Role != WorkspaceRole.Owner)
        {
            throw new InsufficientWorkspaceRoleException();
        }
    }

    private void EnsureSameWorkspace(
        Guid workspaceId)
    {
        if (_currentWorkspace.WorkspaceId != workspaceId)
        {
            throw new UnauthorizedWorkspaceAccessException();
        }
    }
}