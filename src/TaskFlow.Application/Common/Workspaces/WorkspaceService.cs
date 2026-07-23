using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceUserRepository _workspaceUserRepository;
    private readonly IUnitOfWork _unitOfWork;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceUserRepository workspaceUserRepository,
        IUnitOfWork unitOfWork)
    {
        _workspaceRepository = workspaceRepository;
        _workspaceUserRepository = workspaceUserRepository;
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
}