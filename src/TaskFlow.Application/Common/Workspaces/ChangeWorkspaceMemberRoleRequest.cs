using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Workspaces;

public class ChangeWorkspaceMemberRoleRequest
{
    public WorkspaceRole Role { get; set; }
}
