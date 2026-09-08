# TaskFlow — working notes for Claude Code

Read this before proposing changes. It records conventions that are already
established in this codebase, so suggestions stay consistent with what's here rather
than with general .NET defaults.

## What this project is

A multi-tenant task management API. It is a **portfolio project for backend
engineering interviews**, which changes the priorities: correctness, clear
layering, explicit authorization and readable decisions matter more than feature
count. Prefer the change that is easier to explain out loud over the change that is
cleverer.

## Commands

```bash
docker compose up -d postgres                          # PostgreSQL 17 for SDK development
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~TaskItem"

# EF Core (run from src/TaskFlow.Api)
dotnet ef migrations add <Name> --project ../TaskFlow.Infrastructure --startup-project .
dotnet ef database update --project ../TaskFlow.Infrastructure --startup-project .
```

Target framework is **net10.0** across all projects. Do not downgrade it.

## Architecture rules

Dependencies point inward. These are hard constraints, not preferences:

- `TaskFlow.Domain` references **nothing**. No EF Core, no ASP.NET, no MediatR, no
  external packages. If a change requires adding a package reference to Domain, the
  design is wrong.
- `TaskFlow.Application` references Domain only. It owns repository *interfaces*,
  service interfaces, DTOs and application exceptions.
- `TaskFlow.Infrastructure` implements Application's interfaces. All EF Core lives
  here — DbContext, entity configurations, repositories, migrations.
- `TaskFlow.Api` is the only project that knows about HTTP. Nothing depends on it.

Services must never see `DbContext` or `IQueryable`. They talk to repository
interfaces and call `IUnitOfWork.SaveChangesAsync` to commit.

## Multi-tenancy — the invariant that matters most

Every workspace-scoped read and write is filtered by `workspaceId` **at the
repository**, not in the service. Repository signatures take the workspace id as a
parameter, e.g. `GetByIdAsync(Guid id, Guid workspaceId, CancellationToken ct)`.

When adding any new repository method that touches a tenant-owned entity:

1. It takes `workspaceId`.
2. It filters on it in the query.
3. It filters out soft-deleted rows unless the caller explicitly wants them.

The workspace id comes from the **route**, never from a token claim. The caller's
role in that workspace is resolved per request by
`IWorkspaceAuthorizationService`. Do not add a workspace claim to the JWT.

Authorization order inside a service method is always:

```csharp
var role = await _workspaceAuthorizationService.GetActiveRoleAsync(workspaceId, ct);
_taskAuthorizationService.EnsureCanEdit(role);
// ... only then load or mutate anything
```

**Check before load, and it is enforced.** Phase 8.4 audited all 21
workspace-scoped service methods rather than assuming: every one refuses a
non-member before touching a repository, and fifteen of the sixteen role-gated
methods do the same for a role that is too low. `CheckBeforeLoadTests` arms every
repository read to throw and asserts the authorization exception surfaces
instead. A new workspace-scoped method adds one line to its theory data.

The single exception is `TaskService.AssignAsync`, which loads the task before
calling `EnsureCanAssign` because the rule it enforces reads
`task.AssigneeUserId` — the entity is an argument to the decision. That is
recorded in `docs/adr/0002` with a test that asserts it, not tolerated silently.
The membership gate still runs first there, as everywhere.

### 403 versus 404

Settled, and every tenant-isolation test asserts it:

- **Not a member of the workspace → 404**, with a body identical to a workspace that
  genuinely does not exist. Never confirm existence across a tenant boundary.
- **A member whose role is too low → 403.** That caller already knows the workspace
  exists, so naming the real reason leaks nothing.

`UnauthorizedWorkspaceAccessException` and `WorkspaceNotFoundException` stay separate
types so the application layer can tell the two apart. `ExceptionMiddleware` renders
both through a single `WorkspaceNotFound` helper, so the two responses are
identical apart from per-request correlation metadata (ADR 0006). A new workspace-scoped route inherits this for free by letting
`IWorkspaceAuthorizationService` throw — do not hand-roll a 403 for a non-member.

### Soft-deleted entities read as absent, not as conflicts

Settled by the phase 8.3 suite, which measured it rather than assuming:

- Every repository filters `IsDeleted` **before** the entity's `EnsureNotDeleted()`
  guard can fire, so a service raises `TaskNotFoundException` /
  `ProjectNotFoundException` first. Every operation on a soft-deleted task or
  project therefore answers **404**, never 409 and never 500.
- Deleting a project also hides its tasks, because the task lookup joins through
  the active project. The task rows keep `IsDeleted = false` — this is a
  read-filter property, not a cascade.

The four `*AlreadyDeletedException` domain types are all mapped to 409 anyway.
Three of them are currently unreachable through HTTP for the reason above, and
that is exactly why the mapping matters: the first repository method written
without an `IsDeleted` filter would otherwise turn a domain rule into a 500.
`ExceptionMiddlewareTests` covers all four directly, since the middleware is the
only place they can be reached.

For that safety net to work, **an entity's `EnsureNotDeleted()` guard has to
throw its own mapped type.** `TaskItem` threw a bare `InvalidOperationException`
until phase 8.4 — mapped nowhere, so it would have produced the 500 the mapping
exists to prevent, and only `Delete()` raised `TaskAlreadyDeletedException` at
all. Fixed, and now covered by one test per mutating method rather than one
representative test, since a single case would still pass if a later method
dropped its guard.

## Domain entity conventions

- Public properties have `private set`. Mutation happens through intention-revealing
  methods (`Complete()`, `AssignTo(userId)`, `UpdateDetails(...)`), never by
  assigning a property from outside.
- A `private` parameterless constructor exists for EF Core materialisation. Leave it.
- Invariants are validated in the public constructor and at the top of every mutating
  method. Entities cannot exist in an invalid state.
- State machines throw. Illegal transitions raise a domain exception
  (`InvalidTaskStatusTransitionException`), they do not return `false` or no-op.
- Soft-deleted entities reject modification via a private `EnsureNotDeleted()` guard.
- `BaseEntity` owns `Id`, `CreatedAt`, `UpdatedAt` with `protected set`.

## Exceptions

Two families, kept separate on purpose:

- **Domain** (`TaskFlow.Domain.Exceptions`) — broken business rules.
  `InvalidTaskStatusTransitionException`, `TaskAlreadyDeletedException`.
- **Application** (`TaskFlow.Application.Common.Exceptions`) — not-found,
  authorization, conflicts. `TaskNotFoundException`,
  `UnauthorizedWorkspaceAccessException`.

`ExceptionMiddleware` in the Api project maps both to status codes. When adding a new
exception type, add the mapping there in the same commit — an unmapped exception
becomes a 500.

The middleware logs unexpected exceptions and maps failures to ProblemDetails.
Add mappings for new expected exceptions; never return infrastructure diagnostics.

## Style

- `CancellationToken cancellationToken = default` on every async method, passed all
  the way down. Never drop it.
- Async suffix on async methods. `await`, never `.Result` or `.Wait()`.
- No `AutoMapper`. DTO mapping is explicit object initialisation in the service.
- Request and response DTOs live next to the service that uses them, one type per
  file, named `<Verb><Noun>Request` / `<Verb><Noun>Response`.
- Nullable reference types are enabled. Don't suppress warnings with `!` unless the
  invariant is genuinely enforced elsewhere — and say where.
- `Guid` for all entity ids, generated in the domain constructor.
- Pagination request DTOs cap page size with `[Range(1, 100)]`.

## Testing conventions

- xUnit. Three test projects mirroring the layers.
- Test names follow Method_WhenCondition_ShouldOutcome, matching the
  existing suite. e.g. Complete_WhenTaskAlreadyDone_ShouldThrow
- Arrange / Act / Assert, in that order, with blank lines between.
- `[Theory]` with `[InlineData]` for boundary sets rather than several near-identical
  `[Fact]`s.
- **Mock repositories, never the authorization services.** In Application tests
  the repositories, `IUnitOfWork` and `ICurrentUser` are NSubstitute doubles;
  `WorkspaceAuthorizationService` and `TaskAuthorizationService` are the real
  types. Substituting them would let every role and tenant assertion pass against
  an authorization service that returned `Owner` for everyone. Arrange a caller's
  role by seeding the membership row the real service looks up —
  `ServiceTestContext.AsRole(...)` does this.
- **Assert the refusal paths do not commit.** Every test of a rejected call also
  asserts `IUnitOfWork.SaveChangesAsync` was never called
  (`ServiceTestContext.ShouldNotHaveCommittedAsync`). A method that mutates an
  entity and only then throws leaves the change tracker dirty, and the next
  commit in the same scope persists exactly what the refusal was meant to
  prevent.
- **Repository tenant filters get direct tests**, in
  `Tests/TaskFlow.Api.IntegrationTests/Repositories/`, against a real database
  with no service layer above them. Reaching a repository only through a route
  does not exercise its `workspaceId` predicate: almost every cross-tenant
  request stops at the membership gate first. Both shapes are needed — the
  direct one, and the "legitimate member, own workspace in the route, foreign
  entity id" shape in `TaskLookupTenantScopeTests`, which is the only HTTP path
  that reaches the filter.
- Integration tests use a real PostgreSQL via Testcontainers, not an in-memory
  provider. The in-memory provider does not enforce constraints and would let a
  tenant-isolation bug pass.

## Git

Conventional Commits, and they are enforced by habit rather than tooling:

```
feat(tasks): add assignment and workflow management
fix(auth): enforce tenant isolation for task operations
test(domain): add unit tests for core domain entities
refactor(auth): centralize current user identity resolution
docs(readme): document architecture decisions
chore(ci): add build and test workflow
```

Scope is the area, not the layer: `tasks`, `projects`, `workspaces`, `auth`, `ci`,
`readme`. Prefer several small commits that tell a story over one large one — the
history is part of what a reviewer reads.

## Secrets

Never commit a secret. The JWT signing key comes from user secrets in development:

```bash
cd src/TaskFlow.Api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
```

`appsettings.json` declares the `Jwt` section without a `Key`. The app should fail
loudly at startup if the key is absent — a missing signing key must never silently
fall back to a default.

## Things to push back on

If a proposed change does any of these, say so rather than doing it:

- Adds a package reference to `TaskFlow.Domain`.
- Exposes `DbContext` or `IQueryable` above the Infrastructure layer.
- Adds a repository query on a tenant-owned entity without a `workspaceId` filter.
- Writes a service method that loads an entity before it authorizes the caller.
  See `docs/adr/0002` — the one accepted exception is documented there.
- Mocks `IWorkspaceAuthorizationService` or `ITaskAuthorizationService` in an
  Application test.
- Adds a workspace or role claim to the JWT.
- Introduces a settable `Status` property or otherwise bypasses a state transition
  method.
- Adds an abstraction with exactly one implementation and no test that needs the
  seam.
- Uses `DateTime.Now` instead of `DateTime.UtcNow`.

## Phase 9 — Comments and task activity

Read `docs/adr/0003-comments-and-task-activity.md` before changing collaboration.
Members/Admins/Owners may comment; only authors may edit/delete their comments.
Viewers can read only. Membership and role gates precede reads; ownership is
checked after the scoped comment load, before mutation (ADR 0002 extension).

Comment bodies are trimmed and limited to 2,000 characters. Comments are soft-deleted
and hidden if their task/project is deleted. Repositories enforce workspace and
parent-task filters; composite foreign keys enforce parent alignment in PostgreSQL.

Services append activity explicitly before the same `SaveChangesAsync` as the
business change. New task mutation paths must add activity there, not after the
commit. Do not introduce a second save or mock away activity assertions in tests.
Task activity carries before/after business snapshots; comment activity carries
only comment IDs/actions, never body copies. Unchanged task snapshots do not log.
The workspace activity feed retains deleted-task/project history for active members.

Activity has no mutation API and the DbContext refuses tracked modifications or
removals. This is an application guarantee, not protection against privileged SQL.
The ordering audit now covers 27 workspace-scoped methods (the earlier 21-method
figures above describe the Phase 8.4 measurement).

## Phase 10 — Task queries

Read `docs/adr/0004-task-query-contract.md` for filter and ordering semantics.
`GetTasksRequest` now carries filters and validated sort tokens; page/count receive
that same request plus the mandatory route workspace ID. They share `Filtered` in
TaskRepository, which builds on the same active-task query as GetByIdAsync.
The Phase 8.4 note about separately written count/page predicates is historical.

Keep sorting a switch over typed expressions, never dynamic SQL or client-built
expressions. Every order ends with task ID. Null dates stay last; status order is
Todo/InProgress/Done even though persistence stores status text. Dates are inclusive
instant bounds normalized to UTC. Retain validation in direct service calls as
well as HTTP. Adding a filter requires page/count and tenant regression coverage.

## Phase 11 — Authentication

Read `docs/adr/0005-authentication-sessions-and-abuse-controls.md` before modifying
authentication. Access JWTs last at most 15 minutes, without clock skew. Refresh
sessions expire after seven days; token rotation and logout lock the session row
in PostgreSQL. Load a refresh token only after acquiring that lock. Store only
SHA-256 hashes of random 256-bit tokens; never log raw credentials.

`AuthenticationService` owns login/refresh/logout; `UserService` owns registration.
User-row locks protect the five-failure/15-minute lockout counter. Auth persistence
is account-scoped and intentionally has no workspace ID. `IAuthTransaction` keeps
EF transactions in Infrastructure. Failure counters and replay revocations are
intentional commits on rejected requests, exceptions to the ordinary no-commit
rule; tests must verify those changes survive the error response.

Rate limits cover /auth/* by remote IP, default 20/minute per process. Do not trust
arbitrary forwarding headers. Test fixtures raise this limit for unrelated flows;
dedicated limiter tests run the real policy with a small threshold. Password policy
is minimum 15 Unicode scalar values and maximum 72 UTF-8 bytes for BCrypt, without
composition rules. Legacy shorter passwords still authenticate. Logout/replay does
not immediately revoke access JWTs; clients must serialize refresh requests.

## Phase 12 — Observability and concurrency

Read `docs/adr/0006-observability-hardening-and-concurrency.md` before changing the
request pipeline or task/project persistence. Errors use ProblemDetails with a
correlation ID, preserving the tenant 404 contract apart from request metadata.
Keep correlation IDs bounded and logs free of request bodies, credentials and raw
URLs. Health probes stay anonymous and exempt from rate limiting; readiness checks
database connectivity. Global/auth IP budgets are additive and per instance.

EF shadow `Version` properties map to PostgreSQL `xmin` on tasks/projects. Keep
mutation reads tracked, translate EF conflicts through `EfUnitOfWork`, and never
automatically retry a failed save. Activity and business changes must remain in the
same transaction. Discard the failed request context rather than saving it again.
There is no client version/If-Match contract yet, and comments have no concurrency
token. Do not claim that old edit forms are protected.

## Phase 13 — Containers and migrations

Read `docs/adr/0007-container-runtime-and-migrations.md`. Full Compose startup uses
a separate non-root migration executable before the API; never add automatic DDL
to API startup. Keep the API's signing key out of the migration host and all image
layers. Domain/Application dependencies remain unchanged by this outer executable.
The local stack binds to loopback and retains the existing PostgreSQL volume.

Run `bash scripts/smoke-containers.sh` after changing container behavior. It uses
a unique project and disposable volume; cleanup must never target development data.
CI also runs this script. Live hosting, TLS/proxy trust and restricted deployment
database roles are Phase 14 work, not properties implied by Production environment.
