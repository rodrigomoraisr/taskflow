using TaskFlow.Domain.Common;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public class Workspace : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    public bool IsDeleted { get; private set; } = false;

    public DateTime? DeletedAt { get; private set; }

    private Workspace() { }

    public Workspace(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Workspace name is required.");

        Name = name.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        if (IsDeleted)
            throw new WorkspaceAlreadyDeletedException(Id);

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Rename(string name)
    {
        if (IsDeleted)
            throw new WorkspaceAlreadyDeletedException(Id);

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Workspace name is required.");

        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}