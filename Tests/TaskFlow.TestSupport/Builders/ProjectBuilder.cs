using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.TestSupport.Builders;

/// <summary>
/// Builds <see cref="Project"/> instances for tests. Like the other builders it
/// only drives the entity through its public methods.
/// </summary>
public sealed class ProjectBuilder
{
    private Guid _workspaceId = Guid.NewGuid();
    private string _name = "Project name";
    private string _description = "Project description";
    private ProjectStatus _status = ProjectStatus.Active;
    private bool _isDeleted;

    public ProjectBuilder InWorkspace(Guid workspaceId)
    {
        _workspaceId = workspaceId;
        return this;
    }

    public ProjectBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ProjectBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public ProjectBuilder WithStatus(ProjectStatus status)
    {
        _status = status;
        return this;
    }

    public ProjectBuilder Deleted()
    {
        _isDeleted = true;
        return this;
    }

    public Project Build()
    {
        var project = new Project(
            _workspaceId,
            _name,
            _description);

        // ChangeStatus guards against a deleted project, so status comes first.
        if (_status != ProjectStatus.Active)
            project.ChangeStatus(_status);

        if (_isDeleted)
            project.Delete();

        return project;
    }
}
