using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceUserRepository _workspaceUserRepository;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IUnitOfWork _unitOfWork;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceUserRepository workspaceUserRepository,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IUnitOfWork unitOfWork)
    {
        _workspaceRepository = workspaceRepository;
        _workspaceUserRepository = workspaceUserRepository;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateWorkspaceResponse> CreateAsync(
        Guid userId,
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = new Workspace(
            request.Name);

        await _workspaceRepository.AddAsync(
            workspace,
            cancellationToken);

        var membership = new WorkspaceUser(
            userId,
            workspace.Id,
            WorkspaceRole.Owner);

        await _workspaceUserRepository.AddAsync(
            membership,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new CreateWorkspaceResponse
        {
            Id = workspace.Id,
            Name = workspace.Name,
            OwnerId = userId
        };
    }

    public async Task<GetWorkspaceResponse> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService
            .EnsureCanViewWorkspaceAsync(id, cancellationToken);

        var workspace =
            await _workspaceRepository.GetByIdAsync(
                id,
                cancellationToken);

        if (workspace is null)
            throw new WorkspaceNotFoundException(id);

        return new GetWorkspaceResponse
        {
            Id = workspace.Id,
            Name = workspace.Name,
            CreatedAt = workspace.CreatedAt,
            UpdatedAt = workspace.UpdatedAt
        };
    }

    public async Task<List<ListWorkspaceResponse>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _workspaceUserRepository
            .GetActiveMembershipsAsync(userId, cancellationToken);

        var rolesByWorkspace = memberships.ToDictionary(
            membership => membership.WorkspaceId,
            membership => membership.Role);

        var workspaces = await _workspaceRepository.GetByIdsAsync(
            rolesByWorkspace.Keys.ToArray(),
            cancellationToken);

        return workspaces.Select(workspace => new ListWorkspaceResponse
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Role = rolesByWorkspace[workspace.Id].ToString(),
            CreatedAt = workspace.CreatedAt
        }).ToList();
    }

    public async Task DeleteAsync(
    Guid id,
    CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService
            .EnsureCanDeleteWorkspaceAsync(id, cancellationToken);

        var workspace =
            await _workspaceRepository.GetByIdAsync(
                id,
                cancellationToken);

        if (workspace is null)
            throw new WorkspaceNotFoundException(id);

        workspace.Delete();

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);
    }
}
