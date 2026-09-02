using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.TestSupport.Builders;

/// <summary>
/// Builds <see cref="WorkspaceUser"/> membership rows for tests.
///
/// Role and tenant are the two things authorization tests vary, so both are
/// explicit here rather than hidden behind a default that reads as arbitrary.
/// </summary>
public sealed class WorkspaceUserBuilder
{
    private Guid _userId = Guid.NewGuid();
    private Guid _workspaceId = Guid.NewGuid();
    private WorkspaceRole _role = WorkspaceRole.Member;
    private bool _isRemoved;

    public WorkspaceUserBuilder ForUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public WorkspaceUserBuilder InWorkspace(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    public WorkspaceUserBuilder AsRole(WorkspaceRole role)
    {
        _role = role;
        return this;
    }

    public WorkspaceUserBuilder AsOwner() => AsRole(WorkspaceRole.Owner);

    public WorkspaceUserBuilder AsAdmin() => AsRole(WorkspaceRole.Admin);

    public WorkspaceUserBuilder AsMember() => AsRole(WorkspaceRole.Member);

    public WorkspaceUserBuilder AsViewer() => AsRole(WorkspaceRole.Viewer);

    /// <summary>
    /// Soft-removes the membership, the state a revoked member is left in.
    /// </summary>
    public WorkspaceUserBuilder Removed()
    {
        _isRemoved = true;
        return this;
    }

    public WorkspaceUser Build()
    {
        var membership = new WorkspaceUser(
            _userId,
            _workspaceId,
            _role);

        if (_isRemoved)
            membership.RemoveFromWorkspace();

        return membership;
    }
}
