using TaskFlow.Domain.Entities;

namespace TaskFlow.TestSupport.Builders;

/// <summary>
/// Builds <see cref="Workspace"/> instances for tests. Not listed in the
/// roadmap alongside the other three, but integration tests need a real
/// workspace row for the membership and project foreign keys to resolve.
/// </summary>
public sealed class WorkspaceBuilder
{
    private string _name = "Workspace name";
    private bool _isDeleted;

    public WorkspaceBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public WorkspaceBuilder Deleted()
    {
        _isDeleted = true;
        return this;
    }

    public Workspace Build()
    {
        var workspace = new Workspace(_name);

        if (_isDeleted)
            workspace.Delete();

        return workspace;
    }
}
