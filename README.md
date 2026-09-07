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
# Tests — 410 of them; the integration suite starts a PostgreSQL container,
# so Docker must be running.
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
| `Comment` | Author-owned task discussion, soft-deleted, maximum 2,000 characters. |
| `TaskActivity` | Append-only task/comment history with actor and timestamp. |
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

Comments and task activity:

```text
POST   /api/workspaces/{workspaceId}/tasks/{taskId}/comments
GET    /api/workspaces/{workspaceId}/tasks/{taskId}/comments?page=1&pageSize=20
GET    /api/workspaces/{workspaceId}/tasks/{taskId}/comments/{id}
PUT    /api/workspaces/{workspaceId}/tasks/{taskId}/comments/{id}
DELETE /api/workspaces/{workspaceId}/tasks/{taskId}/comments/{id}
GET    /api/workspaces/{workspaceId}/activity?taskId={taskId}&page=1&pageSize=20
```

Comment create/edit bodies use `{ "body": "Ready for review" }`. Members, Admins
and Owners may create comments, but only the author may edit/delete one; Viewers
can read. Comment lists are oldest first, activity newest first; both return arrays
and cap page size at 100. `taskId` is optional on the workspace activity feed.
Activity `details` is a JSON-encoded string containing task before/after snapshots;
comment entries use an empty object and a comment ID, without copying comment text.

Task changes and activity entries commit together. History retains deleted tasks,
while their comments become inaccessible. Existing tasks have no fabricated history:
recording begins when the feature is deployed. Apply the new EF migration before
running the updated API. See [ADR 0003](docs/adr/0003-comments-and-task-activity.md)
for authorization, transaction, retention and append-only guarantees.

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

**Authorization happens before anything is loaded.** A method that loads the
entity and then checks permission returns exactly the same response as one that
checks first, so no test going through HTTP can tell them apart — which is why
the ordering drifts. It matters anyway: loading first fetches a row on behalf of
someone with no right to it, and makes the cost of a refusal depend on whether
the row exists, which is a timing signal sitting behind a 404 that is otherwise
careful to say nothing. Audited across all 21 workspace-scoped service methods
in [`docs/adr/0002`](docs/adr/0002-check-before-load.md), with the single
structural exception documented there rather than quietly tolerated.

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

## Tests

410 tests across three projects, mirroring the layers.

| Project | Count | What it covers |
| --- | --- | --- |
| `TaskFlow.Domain.Tests` | 119 | Entity invariants, every status transition, guards on soft-deleted entities — one test per mutating method rather than one representative test |
| `TaskFlow.Application.Tests` | 135 | Service orchestration with substituted repositories and the **real** authorization services, plus the check-before-load audit |
| `TaskFlow.Api.IntegrationTests` | 156 | The tenant regression suite over real HTTP, repository tenant filters, database constraints, the registration-to-task-lifecycle journey, comments and transactional activity |

The lifecycle journey registers and logs in a user, creates a new workspace,
project and task, then starts, completes and reopens the task. Each transition
is verified through an API read and a fresh database context. A separate test
proves that starting an already started task returns 409 without changing its
stored status or update timestamp.

Integration tests run against PostgreSQL in Testcontainers, not the in-memory
provider — the in-memory provider does not enforce constraints, so a tenant
isolation bug the database would reject could pass in memory.

**The authorization services are never substituted.** Repositories,
`IUnitOfWork` and `ICurrentUser` are, but `WorkspaceAuthorizationService` and
`TaskAuthorizationService` are the real types in every test. Substituting them
would leave every role and tenant assertion passing against an authorization
service that returned `Owner` for everyone.

**Each suite is checked for its ability to fail.** A passing test proves nothing
about its strength, so the invariants are deliberately broken and the resulting
failures counted — the tables in [`docs/adr/0001`](docs/adr/0001-tenant-boundary-responses.md)
and [`docs/adr/0002`](docs/adr/0002-check-before-load.md) record which breaks
turn which tests red, and why the count matters. That exercise is what found the
real gap in the previous suite: the repository's `workspaceId` filter was
load-bearing and guarded by a single test, because every other cross-tenant
scenario was caught earlier by the membership check.

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
  fixing — and the one place the 404 in ADR 0001 costs something, since "not a
  member" and "does not exist" are indistinguishable server-side too.
- **Optimistic concurrency.** Simultaneous task or comment edits currently use
  last-write-wins behavior. A concurrency token and conflict handling are planned
  for Phase 12; activity snapshots do not provide concurrency protection.
- **A frontend.** This is an API. The HTTP collection in
  `src/TaskFlow.Api/TaskFlow.Api.http` and the notes in `docs/` are how it gets
  exercised by hand.
- **Search and file attachments.** Outside the current scope.

---

## What's next

The working plan is in [`docs/ROADMAP.md`](docs/ROADMAP.md). The immediate queue:

1. Phase 10: task filtering and sorting, with an allow-list of sortable fields.
2. Phase 11: refresh tokens, logout, and authentication rate limiting.
3. Phase 12: logging, `ProblemDetails`, health checks and optimistic concurrency.
4. Phases 13–14: containerize the API and add deployment.

---

## Tech

.NET 10 · ASP.NET Core · EF Core 10 · PostgreSQL 17 · Npgsql · xUnit ·
BCrypt.Net · JWT bearer authentication · Docker Compose
