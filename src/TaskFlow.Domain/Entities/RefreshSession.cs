namespace TaskFlow.Domain.Entities;

public sealed class RefreshSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    private RefreshSession() { }

    public RefreshSession(Guid userId, DateTime now, DateTime expiresAt)
    {
        if (userId == Guid.Empty || expiresAt <= now)
            throw new ArgumentException("A user and future session expiry are required.");
        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAt = now;
        ExpiresAt = expiresAt;
    }

    public bool IsActiveAt(DateTime now) => RevokedAt is null && now < ExpiresAt;
    public void Revoke(DateTime now) => RevokedAt ??= now;
}
