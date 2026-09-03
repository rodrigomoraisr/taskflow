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
docker compose up -d                                   # PostgreSQL 17
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

Known debt: that middleware is a growing `switch` and has no logging. Both are on the
roadmap; if you touch it, adding `ILogger` is welcome.

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
- **Do not mock the thing under test.** In Application tests, mock the repositories;
  do *not* mock `IWorkspaceAuthorizationService` or `ITaskAuthorizationService` —
  those are the behaviour being verified.
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
- Adds a workspace or role claim to the JWT.
- Introduces a settable `Status` property or otherwise bypasses a state transition
  method.
- Adds an abstraction with exactly one implementation and no test that needs the
  seam.
- Uses `DateTime.Now` instead of `DateTime.UtcNow`.
