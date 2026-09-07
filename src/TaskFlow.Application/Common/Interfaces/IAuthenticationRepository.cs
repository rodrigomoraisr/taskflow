using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

// Authentication data is account-scoped, not owned by a workspace.
public interface IAuthenticationRepository
{
    Task<IAuthTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<User?> LockUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Guid?> FindSessionIdAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<RefreshSession?> LockSessionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetTokenAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task AddSessionAsync(RefreshSession session, CancellationToken cancellationToken = default);
    Task AddTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
}
