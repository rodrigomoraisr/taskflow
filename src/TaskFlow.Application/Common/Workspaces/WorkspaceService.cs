using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Application.Users;

public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceUserRepository _workspaceUserRepository;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceUserRepository workspaceUserRepository,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _workspaceRepository = workspaceRepository;
        _workspaceUserRepository = workspaceUserRepository;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _userRepository = userRepository;
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

    public async Task<List<GetWorkspaceMemberResponse>> GetMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureCanManageMembersAsync(
            workspaceId,
            cancellationToken);

        var memberships = await _workspaceUserRepository
            .GetActiveByWorkspaceAsync(workspaceId, cancellationToken);

        var users = await _userRepository.GetByIdsAsync(
            memberships.Select(membership => membership.UserId).ToArray(),
            cancellationToken);

        var usersById = users.ToDictionary(user => user.Id);

        return memberships.Select(membership => MapMember(
            membership,
            usersById[membership.UserId])).ToList();
    }

    public async Task<GetWorkspaceMemberResponse> AddMemberAsync(
        Guid workspaceId,
        AddWorkspaceMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureCanManageMembersAsync(
            workspaceId,
            cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (user is null || !user.IsActive)
            throw new UserNotFoundException(normalizedEmail);

        var membership = await _workspaceUserRepository
            .GetByUserAndWorkspaceAsync(
                user.Id,
                workspaceId,
                cancellationToken);

        if (membership is not null && !membership.IsDeleted)
            throw new WorkspaceMemberAlreadyExistsException(user.Id);

        if (membership is null)
        {
            membership = new WorkspaceUser(
                user.Id,
                workspaceId,
                WorkspaceRole.Member);

            await _workspaceUserRepository.AddAsync(
                membership,
                cancellationToken);
        }
        else
        {
            membership.RestoreMembership();
            membership.ChangeRole(WorkspaceRole.Member);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapMember(membership, user);
    }

    public async Task ChangeMemberRoleAsync(
        Guid workspaceId,
        Guid userId,
        ChangeWorkspaceMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Role))
            throw new InvalidWorkspaceRoleException();

        var actorRole = await _workspaceAuthorizationService
            .EnsureCanManageMembersAsync(workspaceId, cancellationToken);

        var membership = await GetActiveMemberAsync(
            workspaceId,
            userId,
            cancellationToken);

        EnsureCanManageTarget(actorRole, membership.Role, request.Role);

        if (membership.Role == WorkspaceRole.Owner &&
            request.Role != WorkspaceRole.Owner)
        {
            await EnsureAnotherOwnerExistsAsync(
                workspaceId,
                cancellationToken);
        }

        membership.ChangeRole(request.Role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actorRole = await _workspaceAuthorizationService
            .EnsureCanManageMembersAsync(workspaceId, cancellationToken);

        var membership = await GetActiveMemberAsync(
            workspaceId,
            userId,
            cancellationToken);

        EnsureCanManageTarget(actorRole, membership.Role);

        if (membership.Role == WorkspaceRole.Owner)
        {
            await EnsureAnotherOwnerExistsAsync(
                workspaceId,
                cancellationToken);
        }

        membership.RemoveFromWorkspace();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<WorkspaceUser> GetActiveMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await _workspaceUserRepository
            .GetByUserAndWorkspaceAsync(
                userId,
                workspaceId,
                cancellationToken);

        if (membership is null || membership.IsDeleted)
            throw new WorkspaceMemberNotFoundException(userId);

        return membership;
    }

    private static void EnsureCanManageTarget(
        WorkspaceRole actorRole,
        WorkspaceRole targetRole,
        WorkspaceRole? requestedRole = null)
    {
        if (actorRole == WorkspaceRole.Owner)
            return;

        var targetIsRegularMember =
            targetRole == WorkspaceRole.Member ||
            targetRole == WorkspaceRole.Viewer;

        var requestedRoleIsAllowedForAdmin =
            requestedRole is null ||
            requestedRole == WorkspaceRole.Admin ||
            requestedRole == WorkspaceRole.Member ||
            requestedRole == WorkspaceRole.Viewer;

        if (!targetIsRegularMember || !requestedRoleIsAllowedForAdmin)
            throw new InsufficientWorkspaceRoleException();
    }

    private async Task EnsureAnotherOwnerExistsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var ownerCount = await _workspaceUserRepository
            .CountActiveOwnersAsync(workspaceId, cancellationToken);

        if (ownerCount <= 1)
            throw new LastWorkspaceOwnerException();
    }

    private static GetWorkspaceMemberResponse MapMember(
        WorkspaceUser membership,
        User user)
    {
        return new GetWorkspaceMemberResponse
        {
            UserId = membership.UserId,
            Email = user.Email,
            Role = membership.Role.ToString(),
            JoinedAt = membership.CreatedAt
        };
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
