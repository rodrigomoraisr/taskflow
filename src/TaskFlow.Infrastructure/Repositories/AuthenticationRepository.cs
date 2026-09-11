using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class AuthenticationRepository(TaskFlowDbContext db) : IAuthenticationRepository
{
    public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => new AuthTransaction(await db.Database.BeginTransactionAsync(cancellationToken));

    public Task<User?> LockUserByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Email\" = {email} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Guid?> FindSessionIdAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.AsNoTracking().Where(t => t.TokenHash == tokenHash)
            .Select(t => (Guid?)t.SessionId).SingleOrDefaultAsync(cancellationToken);

    public Task<RefreshSession?> LockSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.RefreshSessions.FromSqlInterpolated($"SELECT * FROM refresh_sessions WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<RefreshToken?> GetTokenAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task AddSessionAsync(RefreshSession session, CancellationToken cancellationToken = default)
        => await db.RefreshSessions.AddAsync(session, cancellationToken);

    public async Task AddTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
        => await db.RefreshTokens.AddAsync(token, cancellationToken);

    private sealed class AuthTransaction(IDbContextTransaction transaction) : IApplicationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
