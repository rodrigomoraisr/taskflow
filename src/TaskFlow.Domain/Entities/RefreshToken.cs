namespace TaskFlow.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    private RefreshToken() { }

    public RefreshToken(Guid sessionId, string tokenHash, DateTime now)
    {
        if (sessionId == Guid.Empty || string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64 || !tokenHash.All(Uri.IsHexDigit))
            throw new ArgumentException("A session and SHA-256 token hash are required.");
        Id = Guid.NewGuid();
        SessionId = sessionId;
        TokenHash = tokenHash;
        CreatedAt = now;
    }

    public void Use(DateTime now)
    {
        if (UsedAt.HasValue) throw new InvalidOperationException("Refresh token was already used.");
        UsedAt = now;
    }
}
