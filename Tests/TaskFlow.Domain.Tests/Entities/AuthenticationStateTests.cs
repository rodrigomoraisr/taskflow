using TaskFlow.Domain.Entities;

namespace TaskFlow.Domain.Tests.Entities;

public sealed class AuthenticationStateTests
{
    private static readonly DateTime Now = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RecordFailedLogin_WhenThresholdReached_ShouldLockForFifteenMinutesWithoutExtendingIt()
    {
        var user = new User("alice@example.test", "hash");
        for (var i = 0; i < 4; i++) user.RecordFailedLogin(Now);
        Assert.False(user.IsLockedAt(Now));

        user.RecordFailedLogin(Now);
        user.RecordFailedLogin(Now.AddMinutes(1));

        Assert.Equal(5, user.FailedLoginAttempts);
        Assert.Equal(Now.AddMinutes(15), user.LockoutEndsAt);
        Assert.True(user.IsLockedAt(Now.AddMinutes(14)));
        Assert.False(user.IsLockedAt(Now.AddMinutes(15)));
    }

    [Fact]
    public void RecordFailedLogin_WhenOldLockExpired_ShouldStartNewCounter()
    {
        var user = new User("alice@example.test", "hash");
        for (var i = 0; i < 5; i++) user.RecordFailedLogin(Now);

        user.RecordFailedLogin(Now.AddMinutes(15));

        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndsAt);
    }

    [Fact]
    public void ResetFailedLogins_WhenCalled_ShouldClearLockoutState()
    {
        var user = new User("alice@example.test", "hash");
        for (var i = 0; i < 5; i++) user.RecordFailedLogin(Now);
        user.ResetFailedLogins();
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndsAt);
    }

    [Fact]
    public void Session_WhenExpiredOrRevoked_ShouldBeInactive()
    {
        var session = new RefreshSession(Guid.NewGuid(), Now, Now.AddDays(7));
        Assert.True(session.IsActiveAt(Now));
        Assert.False(session.IsActiveAt(Now.AddDays(7)));
        session.Revoke(Now.AddMinutes(1));
        session.Revoke(Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(1), session.RevokedAt);
        Assert.False(session.IsActiveAt(Now.AddMinutes(1)));
    }

    [Fact]
    public void Session_WhenExpiryIsNotFuture_ShouldRejectIt()
    {
        Assert.Throws<ArgumentException>(() => new RefreshSession(Guid.NewGuid(), Now, Now));
    }

    [Fact]
    public void Session_WhenUserIsEmpty_ShouldRejectIt()
    {
        Assert.Throws<ArgumentException>(() => new RefreshSession(Guid.Empty, Now, Now.AddDays(1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void Token_WhenHashInvalid_ShouldRejectIt(string? hash)
    {
        Assert.Throws<ArgumentException>(() => new RefreshToken(Guid.NewGuid(), hash!, Now));
    }

    [Fact]
    public void Use_WhenTokenAlreadyUsed_ShouldRejectSecondUse()
    {
        var token = new RefreshToken(Guid.NewGuid(), new string('A', 64), Now);
        token.Use(Now);
        Assert.Throws<InvalidOperationException>(() => token.Use(Now.AddMinutes(1)));
        Assert.Equal(Now, token.UsedAt);
    }
}
