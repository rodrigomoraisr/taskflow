using TaskFlow.Application.Workspaces;

public interface IWorkspaceService
{
    Task<CreateWorkspaceResponse> CreateAsync(
        Guid userId,
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default);

    Task<List<ListWorkspaceResponse>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    
    Task<GetWorkspaceResponse> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<GetWorkspaceMemberResponse>> GetMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<GetWorkspaceMemberResponse> AddMemberAsync(
        Guid workspaceId,
        AddWorkspaceMemberRequest request,
        CancellationToken cancellationToken = default);

    Task ChangeMemberRoleAsync(
        Guid workspaceId,
        Guid userId,
        ChangeWorkspaceMemberRoleRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
