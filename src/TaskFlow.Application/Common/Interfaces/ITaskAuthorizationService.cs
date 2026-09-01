using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Interfaces;

public interface ITaskAuthorizationService
{
    void EnsureCanEdit(WorkspaceRole role);

    void EnsureCanChangeStatus(WorkspaceRole role);

    void EnsureCanAssign(
        WorkspaceRole role,
        Guid currentUserId,
        Guid requestedUserId,
        Guid? currentAssigneeUserId);

    void EnsureCanUnassign(WorkspaceRole role);
}
