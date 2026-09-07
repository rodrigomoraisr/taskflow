using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkspaceUserRepository _workspaceUserRepository;
    private readonly IWorkspaceRepository _workspaceRepository;

    public UserService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IWorkspaceUserRepository workspaceUserRepository,
        IWorkspaceRepository workspaceRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _workspaceUserRepository = workspaceUserRepository;
        _workspaceRepository = workspaceRepository;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (!PasswordPolicy.IsValidNewPassword(request.Password))
            throw new ArgumentException("Password must contain at least 15 characters, at most 72 UTF-8 bytes, and no control characters.");

        var normalizedEmail = request.Email
           .Trim()
           .ToLowerInvariant();

        var existingUser = await _userRepository.GetByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (existingUser is not null)
            throw new UserAlreadyExistsException(normalizedEmail);

        var passwordHash = _passwordHasher.Hash(
            request.Password);

        var user = new User(
            normalizedEmail,
            passwordHash
        );

        await _userRepository.AddAsync(
            user,
            cancellationToken);

        var workspace = new Workspace("My Workspace");

        await _workspaceRepository.AddAsync(
            workspace,
            cancellationToken);

        var membership = new WorkspaceUser(
            user.Id,
            workspace.Id,
            WorkspaceRole.Owner);

        await _workspaceUserRepository.AddAsync(
            membership,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new RegisterResponse
        {
            Id = user.Id,
            Email = user.Email,
            WorkspaceId = workspace.Id,
            WorkspaceName = workspace.Name
        };
    }
}
