# ADR 0005 — Short-lived access and rotating refresh sessions

- **Status:** Accepted
- **Date:** 2026-09-07
- **Scope:** Phase 11 authentication hardening

## Decision and defaults

Use stateless access JWTs plus server-side refresh sessions. Ordinary API calls do
not query a revocation list. Workspace authorization continues to resolve active
membership per request; JWTs carry identity, not workspace roles.

| Control | Default |
| --- | --- |
| Access JWT lifetime | 15 minutes; configuration accepts 1–15 |
| Validation clock skew | Zero; hosts must keep clocks synchronized |
| Refresh session lifetime | 7 days absolute, starting at login |
| Refresh credential | 32 random bytes, encoded as 64 uppercase hex characters |
| Stored credential | SHA-256 hash only, unique index |
| Lockout | Five failed passwords → 15 minutes |
| Auth IP rate limit | 20 requests per 60-second fixed window; no queue |
| New password | At least 15 Unicode scalar values, at most 72 UTF-8 bytes, no control characters |

These are application choices, not claims that these exact thresholds fit every
product. Low traffic makes a small single-instance limiter practical; the central
reason for the token design is the tradeoff between stateless requests and delayed
access-token revocation.

## API contract

Registration remains separate from login and creates the user's default workspace.
`UserService` handles registration; `AuthenticationService` owns sessions.

Login's existing `token`, `userId` and `email` fields remain. It also returns
`refreshToken` and `refreshTokenExpiresAt` (UTC). Access expiry is the JWT `exp`.

```http
POST /auth/refresh
Content-Type: application/json

{ "refreshToken": "<64-character refresh credential>" }
```

Returns the same shape as login, with a new access token and a new refresh token.
The absolute refresh expiry does not slide forward. Old tokens are retained so
reuse can be detected. Existing tasks/accounts need no token backfill: a new login
creates a session. Existing access tokens issued before this change retain their
original signed expiry; configuration cannot retroactively shorten them.

`POST /auth/logout` accepts the same body and returns 204. Possession of any token
in the session, including a consumed one, allows revoking that session. Unknown
well-formed tokens also return 204, so logout is idempotent. Invalid format returns
400. Logout does not require an unexpired access token and does not revoke other
independent login sessions.

Refresh tokens are bearer secrets. Send them only in JSON bodies over HTTPS in
deployment, never query strings. Auth responses use `Cache-Control: no-store`.
No automatic browser cookie or token storage is introduced; a future browser client
needs its own secure storage/cookie and CSRF design. Do not log credential bodies.

## Rotation, replay and races

Each login creates a `RefreshSession` (the token family) and its first token hash.
Refreshing acquires a PostgreSQL row lock on the session, then loads the token,
checks expiry/revocation and user activity, marks it used, adds its successor and
commits all changes in one transaction.

The token must be read **after** acquiring the session lock. Reading it first could
leave a waiting request with a stale tracked copy whose `UsedAt` still looks empty.
A session lookup before locking selects only its ID without tracking token state.

Reusing a consumed token revokes the entire session and commits that revocation
before returning the same generic 401 used for invalid credentials. A separate
login session stays valid. Expired/revoked tokens cannot produce new credentials.
A deactivated user's refresh is rejected and its session revoked.

Simultaneous refreshes have one successful rotation; the loser detects reuse and
revokes the family, including the winner's new refresh token. This is deliberately
strict. Clients must serialize refresh calls and replace the stored token after
success. Retrying after a lost response may require login; no replay grace window
is implemented. Logout uses the same session lock, so it cannot lose a race to
rotation and leave an active descendant behind.

EF saves and explicit transaction commits are inside Infrastructure-backed
interfaces. Services never receive DbContext, IQueryable or a database transaction
type. Auth tables are account-scoped, not tenant-owned, and have no workspace ID.
Timestamp precision is normalized to PostgreSQL microseconds before returning
expiry values to clients.

## Login abuse controls

Login locks the user row while checking/updating its persistent failure state.
Concurrent failures therefore cannot overwrite each other's counts. A valid login
resets the counter; after lockout expires, the next failure starts a new count.
Requests during lockout do not extend it. Temporary lockout blocks new logins,
not already-authenticated refresh sessions; access JWTs also remain valid until
expiry. Lockout can be abused to inconvenience a known user, which is why it is
bounded rather than permanent.

Unknown, inactive, locked and wrong-password attempts use a generic 401. An unknown
user still performs BCrypt verification against a fixed dummy hash to reduce the
obvious fast-return difference; this is not a constant-time guarantee. Registration
retains its existing duplicate-email 409 behavior.

The named ASP.NET rate policy covers all four auth endpoints together, partitioned
by the connection's remote IP. It returns 429 with Retry-After. It ignores arbitrary
X-Forwarded-For headers. A deployed trusted proxy needs explicit trusted-forwarder
configuration before its client addresses are used. Shared NATs share a limit.
The limiter is in-memory and per instance; it is neither a distributed limit nor
DDoS protection. Account counters and refresh-session locking live in PostgreSQL
and work across instances.

**Exception to the usual no-commit-on-refusal convention:** invalid-password
attempts intentionally commit the counter, and replay detection commits revocation.
These security-state updates are tested explicitly. Other failed operations roll
back their transactions.

## Password and lifecycle limitations

Keep the established BCrypt hasher. Its 72-byte limit is enforced explicitly;
passwords are not truncated, trimmed or normalized. New passwords use a minimum
length without mandatory symbol/uppercase rules. Existing shorter passwords may
still log in; control characters and inputs beyond the BCrypt byte limit are
rejected. A future recovery flow is needed for affected legacy accounts.

MFA, breached-password screening, password reset, email verification, all-device
logout, and a background expired-session cleanup job are not implemented here.
Old token rows must be retained at least for the active session's lifetime to keep
replay detection intact; expired sessions can later be purged with their tokens.

Logout, replay detection and account deactivation cannot invalidate an already
issued access JWT instantly. The remaining validity is bounded by its signed
expiry. Database revocation checks or a token denylist would change that tradeoff.

## Verification

Real PostgreSQL/HTTP tests cover login, hash-only storage, response cache headers,
rotation, replay-family revocation, isolated sessions, simultaneous refreshes,
logout/refresh races, absolute expiry, expired access rejection, deactivation,
lockout expiry/reset/concurrent failures, password boundaries, rollback after a
failed replacement insert, and 429 behavior on every auth endpoint. Clock-driven
expiry tests do not sleep. Application tests assert lock/load ordering, cancellation
and intentional commits on security refusals; Domain tests assert lifecycle rules.

The general integration fixture raises the IP limit to 1,000 for unrelated test
arrangement. Dedicated rate-limit tests explicitly set it to two and verify real
middleware rejection; production defaults remain 20.

## References

- [RFC 9700, refresh token protection](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14)
- [OWASP authentication guidance](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [OWASP password storage guidance](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
