# TaskFlow

[![CI](https://github.com/rodrigomoraisr/taskflow/actions/workflows/ci.yml/badge.svg)](https://github.com/rodrigomoraisr/taskflow/actions/workflows/ci.yml)

A multi-tenant task management API built with .NET 10, PostgreSQL and Clean Architecture.

Users belong to one or more **workspaces**. A workspace owns projects, projects own
tasks, and every read and write is scoped to the caller's workspace membership and
role. Tenant isolation is enforced at the repository boundary rather than trusted to
the caller — no query reaches the database without a workspace id.

This is a portfolio project. It is deliberately over-invested in the parts that are
usually skipped — domain invariants, authorization, tenant isolation — and
deliberately under-invested elsewhere. Both are documented below.

---

## Running it

**Requirements:** .NET 10 SDK, Docker (for PostgreSQL).

```bash
# 1. Start PostgreSQL
docker compose up -d

# 2. Configure the JWT signing key (not committed — see Security below)
cd src/TaskFlow.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"

# 3. Apply migrations
dotnet ef database update --project ../TaskFlow.Infrastructure --startup-project .

# 4. Run
dotnet run
```

OpenAPI is exposed at `/openapi/v1.json` in Development.

```bash
# Tests
dotnet test
```

---

## Architecture

Four projects, with dependencies pointing inward. `TaskFlow.Domain` references
nothing.

```
TaskFlow.Api             controllers, exception middleware, JWT wiring,
                         ICurrentUser implementation
        ↓
TaskFlow.Application     services, DTOs, authorization services,
                         repository interfaces, application exceptions
        ↓
TaskFlow.Domain          entities with invariants and state transitions,
                         enums, domain exceptions
        ↑
TaskFlow.Infrastructure  EF Core DbContext, entity configurations,
                         repositories, migrations, JWT generation, BCrypt
```

`Infrastructure` depends on `Application` (it implements its interfaces) and on
`Domain`. Nothing depends on `Api`.

### Domain model

| Entity | Notes |
| --- | --- |
| `User` | Email is unique. Passwords hashed with BCrypt. |
| `Workspace` | The tenant boundary. Soft-deleted. |
| `WorkspaceUser` | Membership join with a `WorkspaceRole`. Last owner cannot be removed or demoted. |
| `Project` | Belongs to exactly one workspace. Soft-deleted. |
| `TaskItem` | Belongs to a workspace and a project. Status transitions are enforced, not assigned. |

Entities have private setters and a private parameterless constructor for EF Core.
State changes go through methods (`Start`, `Complete`, `Reopen`, `AssignTo`,
`Unassign`, `UpdateDetails`, `Delete`), each of which validates the transition and
throws a domain exception if it is illegal. There is no path to an invalid entity
through the public surface.

### Endpoints

```
POST   /auth/register
POST   /auth/login

POST   /api/workspaces
GET    /api/workspaces
GET    /api/workspaces/{id}
DELETE /api/workspaces/{id}

GET    /api/workspaces/{workspaceId}/members
POST   /api/workspaces/{workspaceId}/members
PATCH  /api/workspaces/{workspaceId}/members/{userId}/role
DELETE /api/workspaces/{workspaceId}/members/{userId}

POST   /api/workspaces/{workspaceId}/projects
GET    /api/workspaces/{workspaceId}/projects
GET    /api/workspaces/{workspaceId}/projects/{projectId}
PUT    /api/workspaces/{workspaceId}/projects/{projectId}
DELETE /api/workspaces/{workspaceId}/projects/{projectId}

POST   /api/workspaces/{workspaceId}/tasks
GET    /api/workspaces/{workspaceId}/tasks          (paginated)
GET    /api/workspaces/{workspaceId}/tasks/{id}
PUT    /api/workspaces/{workspaceId}/tasks/{id}
DELETE /api/workspaces/{workspaceId}/tasks/{id}
POST   /api/workspaces/{workspaceId}/tasks/{id}/start
POST   /api/workspaces/{workspaceId}/tasks/{id}/complete
POST   /api/workspaces/{workspaceId}/tasks/{id}/reopen
PUT    /api/workspaces/{workspaceId}/tasks/{id}/assignee
DELETE /api/workspaces/{workspaceId}/tasks/{id}/assignee
```

Everything except `/auth/*` requires a bearer token.

---

## Decisions

**Workspace id lives in the route, not in the token.** A token proves who you are;
it does not decide which tenant a request touches. Putting the workspace in the
route means every request is explicit about its tenant, and the authorization
service resolves the caller's role in *that* workspace on every call. A workspace
claim baked into the token would go stale the moment someone's role changed.

**Tenant isolation is enforced at the repository, not the service.** Repository
methods take a `workspaceId` and filter on it. A service that forgets to check
authorization is a bug; a repository that cannot express a cross-tenant query is a
design. The two layers are belt and braces on the same invariant.

**A non-member gets 404, not 403.** If you are not a member of a workspace, every
route under it answers exactly as though that workspace did not exist — same status,
same body. A 403 would confirm the workspace is real, which lets an outsider probe for
tenants by id. 403 is reserved for a caller who *is* a member but whose role does not
permit the action; that caller already knows the workspace exists, so the precise
answer costs nothing. The two cases stay distinct inside the application
(`UnauthorizedWorkspaceAccessException` versus `InsufficientWorkspaceRoleException`)
and are collapsed at the HTTP boundary in `ExceptionMiddleware`.

**Status transitions are methods, not a settable property.** `task.Status = Done`
would let a caller skip validation. `task.Complete()` cannot — it checks the current
state and throws `InvalidTaskStatusTransitionException` if the move is illegal. The
rule lives with the data it constrains.

**Soft delete over hard delete.** Tasks and projects carry `IsDeleted` and
`DeletedAt`. Deleted entities reject further modification rather than silently
accepting it. This is the right default for anything a user might want restored, and
it keeps referential history intact.

**Repository + Unit of Work over `DbContext` in services.** The services never see
EF Core. `IUnitOfWork.SaveChangesAsync` makes the transaction boundary explicit and
visible at the call site instead of implicit in a framework type.

**Separate domain and application exceptions.** `InvalidTaskStatusTransitionException`
is a domain rule; `UnauthorizedWorkspaceAccessException` is an application concern.
The middleware maps both to status codes, but the layering stays honest.

**`CancellationToken` threaded end to end.** Every async method takes one and passes
it down. A client that disconnects should not leave a query running.

---

## Security

**The JWT signing key is not in source control.** It comes from user secrets in
development and from environment configuration elsewhere. `appsettings.json` declares
the `Jwt` section without a `Key` value; the app will fail to start if the key is
missing, which is the correct behaviour.

> An earlier commit in this repository's history contained a development-only signing
> key. It has been removed from the current tree and is not used anywhere. It was
> never a production secret.

Token validation has issuer, audience, lifetime and signing-key checks all enabled.
Passwords are hashed with BCrypt. Unhandled exceptions return a generic message
rather than the exception text.

---

## Deliberately left out

These are decisions, not omissions.

- **Refresh tokens and logout.** Access tokens are short-lived and there is no
  revocation. Real revocation needs either a token blacklist or short-lived access
  tokens plus a refresh flow — planned, not built.
- **Rate limiting.** Nothing stops a caller from hammering `/auth/login`. This is the
  most obvious hole in the current auth surface.
- **Structured logging and health checks.** There is no `ILogger` usage yet, which
  means an unexpected 500 currently leaves no trace. This is the next thing worth
  fixing.
- **Optimistic concurrency.** Two simultaneous writes to the same task will
  last-write-win. The fix is a `rowversion` column and handling the concurrency
  exception; the current model tolerates the race because nothing depends on a
  read-modify-write sequence.
- **A frontend.** This is an API. The HTTP collection in
  `src/TaskFlow.Api/TaskFlow.Api.http` and the notes in `docs/` are how it gets
  exercised by hand.
- **Comments, activity feeds, search, file attachments.** Scope, not difficulty.

---

## What's next

The working plan is in [`docs/ROADMAP.md`](docs/ROADMAP.md). The immediate queue:

1. Application-layer and API integration tests, including a tenant-isolation
   regression suite — the invariant this project cares most about currently has no
   test proving it holds.
2. `ILogger` in the exception middleware and RFC 7807 `ProblemDetails` responses in
   place of the current ad-hoc error shape.
3. Refresh tokens, logout, and rate limiting on the auth endpoints.
4. Health checks, a `Dockerfile` for the API, and a deployable compose file.

---

## Tech

.NET 10 · ASP.NET Core · EF Core 10 · PostgreSQL 17 · Npgsql · xUnit ·
BCrypt.Net · JWT bearer authentication · Docker Compose
