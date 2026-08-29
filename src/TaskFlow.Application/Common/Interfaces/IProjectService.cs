using TaskFlow.Application.Projects;

namespace TaskFlow.Application.Common.Interfaces;

public interface IProjectService
{
    Task<CreateProjectResponse> CreateAsync(
        Guid workspaceId,
        CreateProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<GetProjectResponse> GetByIdAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<GetProjectsResponse> GetProjectsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}
