using TaskFlow.Application.Workspaces;

public interface IWorkspaceService
{
    Task<CreateWorkspaceResponse> CreateAsync(
        Guid userId,
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default);
    
    Task<GetWorkspaceResponse> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
