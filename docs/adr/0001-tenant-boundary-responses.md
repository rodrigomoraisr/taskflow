# ADR 0001 — What a caller is told about a workspace they cannot see

- **Status:** Accepted
- **Date:** 2026-09-03
- **Applies to:** every workspace-scoped endpoint

## Context

Every resource in TaskFlow lives inside a workspace, and the workspace id
travels in the route:

```
GET /api/workspaces/{workspaceId}/tasks/{taskId}
```

So any caller can put any workspace id in that route. Three distinct situations
arrive at the same handler:

1. The workspace does not exist.
2. The workspace exists, but the caller is not a member.
3. The workspace exists, the caller is a member, but their role does not permit
   the operation.

The application layer already distinguished these — `WorkspaceNotFoundException`,
`UnauthorizedWorkspaceAccessException` and `InsufficientWorkspaceRoleException`
respectively. The question was what each should look like to the caller.

The obvious mapping is 404 / 403 / 403: "no such thing" for the first, "not
allowed" for the other two. That mapping leaks.

If case 2 returns 403 and case 1 returns 404, then an outsider can distinguish a
real workspace from a fictional one by the status code alone. Iterate over
workspace ids and the 403s are the real tenants. That is enumeration: it reveals
how many customers exist, and combined with any other signal it starts revealing
who they are.

## Decision

**Case 1 and case 2 return responses that are byte-identical.** Both are 404,
and both render their body through a single helper in `ExceptionMiddleware`:

```csharp
private static string WorkspaceNotFound(Guid workspaceId)
    => $"The workspace {workspaceId} was not found.";
```

`UnauthorizedWorkspaceAccessException` and `WorkspaceNotFoundException` both map
through it. The exception types stay distinct, because the application layer
genuinely needs the difference — only the response is collapsed.

**Case 3 stays 403.** That caller is an established member of the workspace.
Telling them their role is insufficient reveals nothing they did not already
know, and a 404 there would be actively unhelpful: it would hide a resource they
can legitimately see the existence of.

The rule in one line: **never confirm the existence of a tenant to someone
outside it; be candid with someone inside it.**

## The part that was not obvious

Setting the status codes correctly was not enough.

After both cases returned 404, the leak was still open. The two bodies differed:

```
"User does not belong to this workspace."     ← case 2
"The workspace {id} was not found!"           ← case 1
```

Same status, different string. An outsider reading the response body could still
tell a real tenant from a fictional one, and the fix that appeared to close the
hole had only moved it from the status line into the payload.

This is why the decision is expressed as a **single helper** rather than as two
mappings that happen to use the same status. Two call sites that must produce
identical output will eventually stop producing identical output. One call site
cannot drift.

## Consequences

**Accepted costs**

- A 404 for case 2 is, strictly, a lie: the workspace does exist. This is a
  deliberate trade of literal accuracy for non-disclosure, and it is the
  standard practice for multi-tenant systems.
- Debugging a legitimate access problem is slightly harder, because "you are not
  a member" and "it does not exist" look the same from outside. Server-side
  logging is the right place to recover that distinction (roadmap phase 12).

**Obligations this creates**

- Any new exception representing "cannot see this workspace" must route through
  `WorkspaceNotFound`, not add its own 404. `UserWithoutWorkspaceException` was
  deleted for exactly this reason: it was dead code still mapped to 403, and it
  would have handed the leak back to whoever wrote the first thrower.
- Any new workspace-scoped endpoint inherits this behaviour automatically,
  because it comes from the middleware rather than from each controller.

## How this is verified

Twenty-two tests pin the case-2 behaviour, and eight middleware tests cover the
mapping directly. But a passing test proves nothing about a test's strength, so
the mapping was deliberately broken three ways to confirm the suite detects it:

| Deliberate break | Tests red | What that proves |
| --- | --- | --- |
| Map case 2 back to 403 | 22 | Every cross-boundary test checks the status |
| Make the two 404 bodies differ | 20 | Failures are string comparisons on the body — so body and status are asserted independently |
| Drop the `workspaceId` filter in `TaskRepository.GetByIdAsync` | 1 | Only one test reaches the repository; the rest stop at the membership gate |

The second row is the one that matters for this ADR. Twenty tests fail on body
content alone while two continue to pass on status — which is what demonstrates
the body assertion is real and not incidental to the status check.

The third row is a finding rather than a confirmation: the repository's
tenant filter is a load-bearing layer currently guarded by a single test,
because every other scenario is caught earlier by the membership check.
Direct repository tests are planned for phase 8.4 to thicken it.

## Related

- `src/TaskFlow.Api/Middleware/ExceptionMiddleware.cs` — the mapping and the helper
- `Tests/TaskFlow.Api.IntegrationTests/TenantIsolation/` — the regression suite
- `Tests/TaskFlow.Api.IntegrationTests/Infrastructure/TenantResponses.cs` —
  the single place the rule is asserted, so a future change breaks one helper
  rather than fifty tests
- `CLAUDE.md` — records the rule for anyone extending the API
