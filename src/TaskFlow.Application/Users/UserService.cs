using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Security;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IWorkspaceUserRepository _workspaceUserRepository;

    public UserService(
        IUserRepository userRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IWorkspaceUserRepository workspaceUserRepository)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _workspaceUserRepository = workspaceUserRepository;

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

        var membership =
            await _workspaceUserRepository
                .GetFirstMembershipAsync(
                    user.Id,
                    cancellationToken);

        if (membership is null)
            throw new UserWithoutWorkspaceException();

        var token =
            _jwtTokenGenerator.GenerateToken(
                user,
                membership.WorkspaceId,
                membership.Role);

        return new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            WorkspaceId = membership.WorkspaceId,
            Role = membership.Role.ToString()
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

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new RegisterResponse
        {
            Id = user.Id,
            Email = user.Email
        };
    }
}