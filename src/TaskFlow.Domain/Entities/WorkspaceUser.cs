
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public class WorkspaceUser : BaseEntity
{
    public Guid UserId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public bool IsDeleted { get; private set; } = false;

    public DateTime? DeletedAt { get; private set; }

    private WorkspaceUser() { }

    public WorkspaceUser(
        Guid userId,
        Guid workspaceId,
        WorkspaceRole role)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "UserId is required.");

        if (workspaceId == Guid.Empty)
            throw new ArgumentException(
                "WorkspaceId is required.");

        UserId = userId;
        WorkspaceId = workspaceId;
        Role = role;
        CreatedAt = DateTime.UtcNow;
    }

    public void RemoveFromWorkspace()
    {
        if (IsDeleted)
            throw new WorkspaceUserAlreadyDeletedException(Id);

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RestoreMembership()
    {
        if (!IsDeleted)
            throw new InvalidOperationException(
               "Membership is already active.");

        IsDeleted = false;
        DeletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeRole(WorkspaceRole role)
    {
        if (IsDeleted)
            throw new WorkspaceUserAlreadyDeletedException(Id);

        Role = role;
        UpdatedAt = DateTime.UtcNow;
    }
}