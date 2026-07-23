using TaskFlow.Application.Workspaces;

public interface IWorkspaceService
{
    Task<CreateWorkspaceResponse> CreateAsync(
        Guid userId,
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default);
}