# ADR 0002 — A service authorizes before it loads

- **Status:** Accepted
- **Date:** 2026-09-06
- **Applies to:** every service method that takes a workspace id

## Context

[ADR 0001](0001-tenant-boundary-responses.md) settled what a caller is *told*
about a workspace they cannot see. This one settles what the server *does*
before it decides to tell them anything.

Two orderings produce identical responses:

```csharp
// A — check, then load
var role = await _workspaceAuthorizationService.GetActiveRoleAsync(workspaceId, ct);
_taskAuthorizationService.EnsureCanEdit(role);
var task = await _taskRepository.GetByIdAsync(id, workspaceId, ct);

// B — load, then check
var task = await _taskRepository.GetByIdAsync(id, workspaceId, ct);
var role = await _workspaceAuthorizationService.GetActiveRoleAsync(workspaceId, ct);
_taskAuthorizationService.EnsureCanEdit(role);
```

Both refuse. Both refuse with the same status and the same body. No test that
goes through HTTP can tell them apart, which is precisely why the ordering
drifts: nothing pushes back when it does.

The difference is real anyway. Under B the row is fetched on behalf of a caller
who turns out to have no right to it, and the response time of a refusal starts
depending on whether the row exists — a timing oracle sitting behind a 404 that
ADR 0001 went to some trouble to make uninformative. It also puts an entity in
the change tracker before anyone has established the caller may touch it.

## Decision

**Every workspace-scoped service method resolves the caller's authorization
before it reads any entity.** The shape is:

```csharp
var role = await _workspaceAuthorizationService.GetActiveRoleAsync(workspaceId, ct);
_taskAuthorizationService.EnsureCanEdit(role);
// ... only then load or mutate anything
```

The membership gate comes first without exception. The role check comes second,
and also precedes the load — with one exception, below.

## The measured finding

Phase 8.4 audited this rather than assuming it. `CheckBeforeLoadTests` arms every
repository read to throw a sentinel exception, leaves only the membership lookup
working, and asserts each method still refuses with an *authorization* exception.
A method that loads first surfaces the sentinel instead and the test names it.

All 21 workspace-scoped methods across `TaskService`, `ProjectService` and
`WorkspaceService` refuse a non-member before reading anything. Fifteen of the
sixteen role-gated methods do the same for a member whose role is too low.

**The exception is `TaskService.AssignAsync`,** and it is structural rather than
accidental. The rule it enforces — a `Member` may claim an *unassigned* task for
themselves and nothing else — reads the task's current assignee:

```csharp
_taskAuthorizationService.EnsureCanAssign(
    role,
    _currentUser.UserId,
    userId,
    task.AssigneeUserId);   // ← the entity is an input to the decision
```

There is no ordering in which that decision precedes the load, because the
entity is one of its arguments. What still holds is the part that carries the
tenant guarantee: `GetActiveRoleAsync` runs first, so a non-member never reaches
the repository at all, and the load it precedes is already scoped to the
workspace from the route.

This is recorded as an accepted exception rather than silently tolerated.
`AssignAsync_WhenTheRoleIsTooLow_ShouldLoadTheTaskFirstBecauseTheRuleReadsIt`
asserts the current behaviour, so if the rule ever stops needing the entity, that
test fails and points at this decision.

## Consequences

**Obligations this creates**

- A new workspace-scoped service method adds itself to the `WorkspaceScopedCalls`
  theory data in `CheckBeforeLoadTests`, and to `RoleGatedCalls` if it has a role
  gate. The tests are table-driven so that adding a method is one line.
- A method that genuinely cannot check first — because the entity feeds the
  decision, as with `AssignAsync` — gets an explicit test saying so and a note
  here. It does not get a silent pass.
- The membership gate stays first in every case. That is the one ordering with no
  exceptions, because it is what ADR 0001's 404 depends on.

**Accepted costs**

- A refusal costs one membership lookup even when the entity does not exist. That
  is the point: the cost is constant either way, which is what removes the timing
  signal.

## How this is verified

The audit is only worth as much as its ability to fail, so the ordering was
deliberately broken:

| Deliberate break | Tests red | What that proves |
| --- | --- | --- |
| Move `EnsureCanEdit` after the load in `TaskService.DeleteAsync` | **1**, and only the ordering test | The assertion is real, and specific to ordering rather than incidental to a status check |
| Remove `EnsureCanEdit` from `TaskService.UpdateAsync` entirely | 2 — the ordering test and that method's role test | The service tests run against the *real* authorization service, not a substitute |

The first row is the one that matters. Removing a check breaks lots of things;
merely *reordering* it breaks nothing that existed before phase 8.4. One test
going red — and exactly the right one — is what makes the ordering an enforced
convention instead of a stylistic preference.

## Related

- [`0001-tenant-boundary-responses.md`](0001-tenant-boundary-responses.md) — the
  404 whose timing this protects
- `Tests/TaskFlow.Application.Tests/Authorization/CheckBeforeLoadTests.cs` — the audit
- `Tests/TaskFlow.Application.Tests/TestSupport/ServiceTestContext.cs` —
  `MakeEveryReadThrow`, the instrument
- `CLAUDE.md` — the rule, under "Things to push back on"
