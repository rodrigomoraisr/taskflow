using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Authorization;

public class TaskAuthorizationService : ITaskAuthorizationService
{
    public void EnsureCanEdit(WorkspaceRole role)
    {
        EnsureIsNotViewer(role);
    }

    public void EnsureCanChangeStatus(WorkspaceRole role)
    {
        EnsureIsNotViewer(role);
    }

    public void EnsureCanAssign(
        WorkspaceRole role,
        Guid currentUserId,
        Guid requestedUserId,
        Guid? currentAssigneeUserId)
    {
        if (role == WorkspaceRole.Owner || role == WorkspaceRole.Admin)
            return;

        var memberCanClaimTask =
            role == WorkspaceRole.Member &&
            requestedUserId == currentUserId &&
            currentAssigneeUserId is null;

        if (!memberCanClaimTask)
            throw new InvalidTaskAssignmentException();
    }

    public void EnsureCanUnassign(WorkspaceRole role)
    {
        if (role != WorkspaceRole.Owner && role != WorkspaceRole.Admin)
            throw new InsufficientWorkspaceRoleException();
    }

    private static void EnsureIsNotViewer(WorkspaceRole role)
    {
        if (role == WorkspaceRole.Viewer)
            throw new InsufficientWorkspaceRoleException();
    }
}
