namespace TaskFlow.Application.Common.Interfaces;

public interface IWorkspaceAuthorizationService
{
    void EnsureCanViewWorkspace(
        Guid workspaceId);

    void EnsureCanManageMembers(
        Guid workspaceId);

    void EnsureCanDeleteWorkspace(
        Guid workspaceId);
}