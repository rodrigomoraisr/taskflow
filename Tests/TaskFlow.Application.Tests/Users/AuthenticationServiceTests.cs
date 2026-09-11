using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Security;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Tests.Users;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task LoginAsync_WhenPasswordWrong_ShouldCommitFailureBeforeReturning401()
    {
        var c = new Context();
        c.Store.LockUserByEmailAsync(c.User.Email, Arg.Any<CancellationToken>()).Returns(c.User);
        c.Passwords.Verify("wrong", c.User.PasswordHash).Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => c.Service.LoginAsync(
            new LoginRequest { Email = c.User.Email, Password = "wrong" }));

        Assert.Equal(1, c.User.FailedLoginAttempts);
        Received.InOrder(() =>
        {
            _ = c.Unit.SaveChangesAsync(Arg.Any<CancellationToken>());
            _ = c.Transaction.CommitAsync(Arg.Any<CancellationToken>());
        });
        await c.Store.DidNotReceive().AddSessionAsync(Arg.Any<RefreshSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenUsed_ShouldCommitFamilyRevocationWithoutIssuingToken()
    {
        var c = new Context();
        c.Token.Use(DateTime.UtcNow);
        c.ArrangeRefresh();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => c.Service.RefreshAsync(
            new RefreshRequest { RefreshToken = new string('A', 64) }));

        Assert.NotNull(c.Session.RevokedAt);
        await c.Unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await c.Transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await c.Store.DidNotReceive().AddTokenAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenValid_ShouldLockBeforeLoadingTokenAndCommitRotationOnce()
    {
        var c = new Context();
        c.ArrangeRefresh();
        using var source = new CancellationTokenSource();

        var response = await c.Service.RefreshAsync(new RefreshRequest { RefreshToken = new string('A', 64) }, source.Token);

        Assert.NotNull(c.Token.UsedAt);
        Assert.Equal(new string('B', 64), response.RefreshToken);
        await c.Store.Received(1).AddTokenAsync(Arg.Is<RefreshToken>(t => t.SessionId == c.Session.Id), source.Token);
        Received.InOrder(() =>
        {
            _ = c.Store.LockSessionAsync(c.Session.Id, source.Token);
            _ = c.Store.GetTokenAsync(new string('C', 64), source.Token);
            _ = c.Unit.SaveChangesAsync(source.Token);
            _ = c.Transaction.CommitAsync(source.Token);
        });
    }

    [Fact]
    public async Task LoginAsync_WhenUserUnknown_ShouldDoDummyVerificationButNotCommit()
    {
        var c = new Context();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => c.Service.LoginAsync(
            new LoginRequest { Email = "unknown@example.test", Password = "anything" }));

        c.Passwords.Received(1).Verify("anything", Arg.Any<string>());
        await c.Unit.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await c.Transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("a long lowercase passphrase", true)]
    [InlineData("short", false)]
    [InlineData("a long passphrase\n", false)]
    public void PasswordPolicy_WhenEvaluated_ShouldAllowPassphrasesWithoutCompositionRules(string password, bool expected)
    {
        Assert.Equal(expected, PasswordPolicy.IsValidNewPassword(password));
    }

    private sealed class Context
    {
        public IAuthenticationRepository Store { get; } = Substitute.For<IAuthenticationRepository>();
        public IApplicationTransaction Transaction { get; } = Substitute.For<IApplicationTransaction>();
        public IUserRepository Users { get; } = Substitute.For<IUserRepository>();
        public IPasswordHasher Passwords { get; } = Substitute.For<IPasswordHasher>();
        public IJwtTokenGenerator Jwt { get; } = Substitute.For<IJwtTokenGenerator>();
        public IRefreshTokenCodec Codec { get; } = Substitute.For<IRefreshTokenCodec>();
        public IUnitOfWork Unit { get; } = Substitute.For<IUnitOfWork>();
        public User User { get; } = new("alice@example.test", "hash");
        public RefreshSession Session { get; }
        public RefreshToken Token { get; }
        public AuthenticationService Service { get; }
        public Context()
        {
            Session = new(User.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
            Token = new(Session.Id, new string('C', 64), DateTime.UtcNow);
            Store.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Transaction);
            Codec.Generate().Returns(new string('B', 64));
            Codec.Hash(Arg.Any<string>()).Returns(new string('C', 64));
            Jwt.GenerateToken(User).Returns("access-token");
            Service = new(Store, Users, Passwords, Jwt, Codec, Unit, TimeProvider.System);
        }
        public void ArrangeRefresh()
        {
            Store.FindSessionIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Session.Id);
            Store.LockSessionAsync(Session.Id, Arg.Any<CancellationToken>()).Returns(Session);
            Store.GetTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Token);
            Users.GetByIdAsync(User.Id, Arg.Any<CancellationToken>()).Returns(User);
        }
    }
}
