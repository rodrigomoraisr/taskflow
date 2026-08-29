using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public class Project : BaseEntity
{
    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public ProjectStatus Status { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    private Project()
    {
    }

    public Project(
        Guid workspaceId,
        string name,
        string description)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("WorkspaceId is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.");

        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Status = ProjectStatus.Active;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(
        string name,
        string description)
    {
        EnsureNotDeleted();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.");

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(ProjectStatus status)
    {
        EnsureNotDeleted();

        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(
                nameof(status),
                "Project status is invalid.");

        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        if (IsDeleted)
            throw new ProjectAlreadyDeletedException(Id);

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new ProjectAlreadyDeletedException(Id);
    }
}
