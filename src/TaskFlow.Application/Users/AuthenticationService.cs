using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Security;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Users;

public sealed class AuthenticationService(
    IAuthenticationRepository authentication,
    IUserRepository users,
    IPasswordHasher passwords,
    IJwtTokenGenerator jwt,
    IRefreshTokenCodec tokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IAuthenticationService
{
    // A precomputed BCrypt hash makes unknown-user attempts perform password
    // verification too. The value is a dummy, never an account credential.
    private const string DummyHash = "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !PasswordPolicy.HasSafeEncoding(request.Password)) throw new InvalidCredentialsException();
        var email = request.Email.Trim().ToLowerInvariant();
        await using var transaction = await authentication.BeginTransactionAsync(cancellationToken);
        var user = await authentication.LockUserByEmailAsync(email, cancellationToken);
        var matches = passwords.Verify(request.Password, user?.PasswordHash ?? DummyHash);
        var now = UtcNow();
        if (user is null || !user.IsActive || user.IsLockedAt(now)) throw new InvalidCredentialsException();
        if (!matches)
        {
            user.RecordFailedLogin(now);
            // This failure deliberately commits the failed-attempt counter.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new InvalidCredentialsException();
        }
        user.ResetFailedLogins();
        var session = new RefreshSession(user.Id, now, now.AddDays(7));
        await authentication.AddSessionAsync(session, cancellationToken);
        var response = await IssueAsync(user, session, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<LoginResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        ValidateToken(request.RefreshToken);
        var hash = tokens.Hash(request.RefreshToken);
        await using var transaction = await authentication.BeginTransactionAsync(cancellationToken);
        var session = await FindAndLockSessionAsync(hash, cancellationToken);
        var now = UtcNow();
        if (session is null || !session.IsActiveAt(now)) throw new InvalidCredentialsException();
        // Load only AFTER acquiring the session lock, so a waiting request
        // observes the first request's committed consumption of this token.
        var token = await authentication.GetTokenAsync(hash, cancellationToken);
        if (token is null) throw new InvalidCredentialsException();
        var user = await users.GetByIdAsync(session.UserId, cancellationToken);
        if (token.UsedAt.HasValue || user is null || !user.IsActive)
        {
            session.Revoke(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new InvalidCredentialsException();
        }
        token.Use(now);
        var response = await IssueAsync(user, session, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        ValidateToken(request.RefreshToken);
        await using var transaction = await authentication.BeginTransactionAsync(cancellationToken);
        var session = await FindAndLockSessionAsync(tokens.Hash(request.RefreshToken), cancellationToken);
        if (session is not null)
        {
            session.Revoke(UtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<RefreshSession?> FindAndLockSessionAsync(string hash, CancellationToken cancellationToken)
    {
        var id = await authentication.FindSessionIdAsync(hash, cancellationToken);
        return id.HasValue ? await authentication.LockSessionAsync(id.Value, cancellationToken) : null;
    }

    private async Task<LoginResponse> IssueAsync(User user, RefreshSession session, DateTime now,
        CancellationToken cancellationToken)
    {
        var raw = tokens.Generate();
        await authentication.AddTokenAsync(new RefreshToken(session.Id, tokens.Hash(raw), now), cancellationToken);
        return new LoginResponse
        {
            Token = jwt.GenerateToken(user), UserId = user.Id, Email = user.Email,
            RefreshToken = raw, RefreshTokenExpiresAt = session.ExpiresAt
        };
    }

    private DateTime UtcNow()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        // PostgreSQL stores microseconds; normalize before returning expiry
        // so it remains identical when read back during rotation.
        return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }

    private static void ValidateToken(string token)
    {
        if (token is null || token.Length != 64 || !token.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            throw new ArgumentException("Invalid refresh token format.");
    }
}
