using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Security;
using TaskFlow.Application.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IWorkspaceUserRepository _workspaceUserRepository;
    private readonly IWorkspaceRepository _workspaceRepository;

    public UserService(
        IUserRepository userRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IWorkspaceUserRepository workspaceUserRepository,
        IWorkspaceRepository workspaceRepository)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _workspaceUserRepository = workspaceUserRepository;
        _workspaceRepository = workspaceRepository;
    }

    public async Task<LoginResponse> LoginAsync(
    LoginRequest request,
    CancellationToken cancellationToken)
    {
        string normalizedEmail = request.Email
            .Trim()
            .ToLowerInvariant();

        var user = await _userRepository.GetByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (user is null || !user.IsActive)
            throw new InvalidCredentialsException();

        var passwordMatches = _passwordHasher.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordMatches)
            throw new InvalidCredentialsException();

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email
        };
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
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
