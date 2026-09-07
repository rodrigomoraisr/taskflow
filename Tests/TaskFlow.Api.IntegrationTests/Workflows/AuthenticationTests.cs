using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.IntegrationTests.Infrastructure;
using TaskFlow.Application.Users;
using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Api.IntegrationTests.Workflows;

public sealed class AuthenticationTests(PostgreSqlFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task Login_WhenValid_ShouldReturnShortLivedAccessAndHashedRefreshCredentials()
    {
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        using var valid = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest { Email = owner.Email.ToUpperInvariant(), Password = owner.Password });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.True(valid.Headers.CacheControl!.NoStore);
        var session = await valid.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.Token);
        Assert.InRange(jwt.ValidTo - DateTime.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        Assert.DoesNotContain(jwt.Claims, c => c.Type is "role" or "workspaceId");
        Assert.Equal(64, session.RefreshToken.Length);
        Assert.InRange(session.RefreshTokenExpiresAt - DateTime.UtcNow, TimeSpan.FromDays(6.9), TimeSpan.FromDays(7));
        await WithDbAsync(async db =>
        {
            var hash = new RefreshTokenCodec().Hash(session.RefreshToken);
            Assert.True(await db.RefreshTokens.AnyAsync(t => t.TokenHash == hash));
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.TokenHash == session.RefreshToken));
        });
    }

    [Fact]
    public async Task Refresh_WhenRotatedTokenReplayed_ShouldRevokeItsFamilyButNotAnotherLogin()
    {
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        var first = await LoginAsync(client, owner);
        var independent = await LoginAsync(client, owner);
        using var rotated = await RefreshAsync(client, first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = (await rotated.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(first.RefreshTokenExpiresAt, second.RefreshTokenExpiresAt);

        using var replay = await RefreshAsync(client, first.RefreshToken);
        using var descendant = await RefreshAsync(client, second.RefreshToken);
        using var other = await RefreshAsync(client, independent.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, descendant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task Refresh_WhenTwoRequestsUseSameToken_ShouldHaveOneWinnerAndRevokeFamily()
    {
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        var session = await LoginAsync(client, owner);

        var responses = await Task.WhenAll(RefreshAsync(client, session.RefreshToken), RefreshAsync(client, session.RefreshToken));

        try
        {
            var success = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
            var winner = (await success.Content.ReadFromJsonAsync<LoginResponse>())!;
            using var subsequent = await RefreshAsync(client, winner.RefreshToken);
            Assert.Equal(HttpStatusCode.Unauthorized, subsequent.StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Logout_WhenCalledWithConsumedToken_ShouldRevokeCurrentRefreshButLeaveAccessUntilExpiry()
    {
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        var first = await LoginAsync(client, owner);
        using var rotation = await RefreshAsync(client, first.RefreshToken);
        var second = (await rotation.Content.ReadFromJsonAsync<LoginResponse>())!;

        using var logout = await client.PostAsJsonAsync("/auth/logout", new RefreshRequest { RefreshToken = first.RefreshToken });
        using var repeated = await client.PostAsJsonAsync("/auth/logout", new RefreshRequest { RefreshToken = first.RefreshToken });
        using var refresh = await RefreshAsync(client, second.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.Token);
        using var access = await client.GetAsync("/api/workspaces");
        Assert.Equal(HttpStatusCode.OK, access.StatusCode);
    }

    [Fact]
    public async Task Refresh_WhenSessionExpires_ShouldRejectWithoutExtendingAbsoluteLifetime()
    {
        var clock = new AdjustableClock();
        _factory.Clock = clock;
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        var session = await LoginAsync(client, owner);
        clock.Advance(TimeSpan.FromDays(6));
        using var rotated = await RefreshAsync(client, session.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var current = (await rotated.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(session.RefreshTokenExpiresAt, current.RefreshTokenExpiresAt);
        clock.Advance(TimeSpan.FromDays(1));

        using var expired = await RefreshAsync(client, current.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    public async Task Access_WhenExpired_ShouldReturnUnauthorizedWithoutDefaultClockSkew()
    {
        _factory.Clock = new AdjustableClock(DateTimeOffset.UtcNow.AddMinutes(-15).AddSeconds(-1));
        var owner = await RegisterUserAsync();
        using var client = CreateClientFor(owner);

        using var response = await client.GetAsync("/api/workspaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WhenFiveFailuresOccur_ShouldLockPersistentlyAndRecoverAfterTimeout()
    {
        var clock = new AdjustableClock();
        _factory.Clock = clock;
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            using var failure = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = owner.Email, Password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }
        using var locked = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = owner.Email, Password = owner.Password });
        using var unknown = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = "unknown@taskflow.test", Password = owner.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await locked.Content.ReadAsStringAsync());
        await WithDbAsync(async db => Assert.Equal(5, (await db.Users.FindAsync(owner.Id))!.FailedLoginAttempts));
        clock.Advance(TimeSpan.FromMinutes(15));

        await LoginAsync(client, owner);

        await WithDbAsync(async db =>
        {
            var user = await db.Users.FindAsync(owner.Id);
            Assert.Equal(0, user!.FailedLoginAttempts);
            Assert.Null(user.LockoutEndsAt);
        });
    }

    [Fact]
    public async Task Login_WhenFailuresArriveConcurrently_ShouldNotLoseCounterUpdates()
    {
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = owner.Email, Password = "incorrect" })));
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            response.Dispose();
        }
        await WithDbAsync(async db =>
        {
            var user = await db.Users.FindAsync(owner.Id);
            Assert.Equal(5, user!.FailedLoginAttempts);
            Assert.NotNull(user.LockoutEndsAt);
        });
    }

    [Theory]
    [InlineData("short")]
    [InlineData("12345678901234")]
    [InlineData("a-password-with\nnewline")]
    public async Task Register_WhenPasswordViolatesPolicy_ShouldRejectIt(string password)
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest { Email = "new@taskflow.test", Password = password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await WithDbAsync(async db => Assert.False(await db.Users.AnyAsync()));
    }

    [Theory]
    [InlineData("a", 73)]
    [InlineData("é", 37)]
    public async Task Register_WhenPasswordExceedsBcryptByteLimit_ShouldRejectIt(string character, int count)
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = "new@taskflow.test", Password = string.Concat(Enumerable.Repeat(character, count))
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/auth/register")]
    [InlineData("/auth/login")]
    [InlineData("/auth/refresh")]
    [InlineData("/auth/logout")]
    public async Task Auth_WhenIpLimitExceeded_ShouldReturn429WithRetryAfter(string path)
    {
        _factory.AuthPermitLimit = 2;
        using var client = _factory.CreateClient();
        for (var i = 0; i < 2; i++)
        {
            using var allowed = await client.PostAsJsonAsync(path, new { });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        // An arbitrary forwarding header must not change the client identity.
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.9");
        using var rejected = await client.PostAsJsonAsync(path, new { });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter!.Delta > TimeSpan.Zero);
        Assert.True(rejected.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task Refresh_WhenUnknownToken_ShouldReturn401AndLogoutShouldRemainIdempotent()
    {
        using var client = _factory.CreateClient();
        var token = new string('A', 64);
        using var refresh = await RefreshAsync(client, token);
        using var logout = await client.PostAsJsonAsync("/auth/logout", new RefreshRequest { RefreshToken = token });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task Refresh_WhenUserDeactivated_ShouldRevokeSessionAndRejectLogin()
    {
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        var session = await LoginAsync(client, owner);
        await WithDbAsync(async db =>
        {
            var user = await db.Users.FindAsync(owner.Id);
            db.Entry(user!).Property(u => u.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
        });

        using var refresh = await RefreshAsync(client, session.RefreshToken);
        using var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = owner.Email, Password = owner.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        await WithDbAsync(async db =>
        {
            var hash = new RefreshTokenCodec().Hash(session.RefreshToken);
            var token = await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash);
            Assert.NotNull((await db.RefreshSessions.FindAsync(token.SessionId))!.RevokedAt);
        });
    }

    [Fact]
    public async Task Login_WhenSuccessfulBeforeThreshold_ShouldResetFailureCounter()
    {
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        for (var i = 0; i < 4; i++)
        {
            using var failure = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = owner.Email, Password = "wrong" });
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }
        await LoginAsync(client, owner);
        using var nextFailure = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = owner.Email, Password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, nextFailure.StatusCode);
        await WithDbAsync(async db => Assert.Equal(1, (await db.Users.FindAsync(owner.Id))!.FailedLoginAttempts));
    }

    [Fact]
    public async Task Logout_WhenRacingRefresh_ShouldLeaveNoUsableRefreshToken()
    {
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        var session = await LoginAsync(client, owner);

        var refreshTask = RefreshAsync(client, session.RefreshToken);
        var logoutTask = client.PostAsJsonAsync("/auth/logout", new RefreshRequest { RefreshToken = session.RefreshToken });
        await Task.WhenAll(refreshTask, logoutTask);
        using var refresh = await refreshTask;
        using var logout = await logoutTask;

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(refresh.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        if (refresh.IsSuccessStatusCode)
        {
            var issued = (await refresh.Content.ReadFromJsonAsync<LoginResponse>())!;
            using var reuse = await RefreshAsync(client, issued.RefreshToken);
            Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        }
        using var original = await RefreshAsync(client, session.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, original.StatusCode);
    }

    [Fact]
    public async Task Rotation_WhenReplacementInsertFails_ShouldRollBackTokenConsumption()
    {
        var owner = await RegisterUserAsync();
        using var client = _factory.CreateClient();
        var session = await LoginAsync(client, owner);
        var hash = new RefreshTokenCodec().Hash(session.RefreshToken);
        await WithDbAsync(async db =>
        {
            var repository = new TaskFlow.Infrastructure.Repositories.AuthenticationRepository(db);
            await using var transaction = await repository.BeginTransactionAsync();
            var sessionId = await repository.FindSessionIdAsync(hash);
            await repository.LockSessionAsync(sessionId!.Value);
            var token = await repository.GetTokenAsync(hash);
            token!.Use(DateTime.UtcNow);
            // Deliberately duplicate the unique token hash: the entire save must fail.
            await repository.AddTokenAsync(new TaskFlow.Domain.Entities.RefreshToken(sessionId.Value, hash, DateTime.UtcNow));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });

        using var retry = await RefreshAsync(client, session.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Theory]
    [InlineData("/auth/refresh")]
    [InlineData("/auth/logout")]
    public async Task RefreshEndpoints_WhenTokenMalformed_ShouldReturnBadRequest(string path)
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(path, new RefreshRequest { RefreshToken = "not-a-token" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, TestUser user)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = user.Email, Password = user.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/auth/refresh", new RefreshRequest { RefreshToken = token });
}

internal sealed class AdjustableClock : TimeProvider
{
    private DateTimeOffset _now;
    public AdjustableClock(DateTimeOffset? now = null) => _now = now ?? DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan amount) => _now += amount;
}
