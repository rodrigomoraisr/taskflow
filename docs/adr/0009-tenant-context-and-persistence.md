# ADR 0009 — Tenant context and persistence boundaries

- **Status:** Accepted; records existing implementation
- **Date:** 2026-09-10
- **Scope:** Phase 15 documentation review

## Workspace selection belongs in the route

A user may belong to several workspaces, with different roles in each. The access
JWT identifies the user; `/api/workspaces/{workspaceId}/...` selects the workspace.
Services resolve active membership and role on each request. Putting a selected
workspace and role into the JWT would require issuing another token when switching
workspaces and could preserve stale membership decisions until expiration.

The route value is untrusted input, not proof of access. Authorization precedes
entity reads (ADR 0002), and repositories filter tenant-owned records by workspace
and active parent records. Database composite relationships reinforce tenant
alignment where configured. Authentication queries are account-scoped; they do
not pretend to require a workspace ID. This costs a membership query per scoped
request and keeps revocation behavior independent of cached role claims.

## Application services use repository interfaces

Application owns use cases, repository interfaces and the unit-of-work interface.
Infrastructure owns EF Core, tracked queries, SQL and transactions. Services do
not receive `DbContext` or `IQueryable`: allowing arbitrary query composition there
would spread tenant filtering across layers and make authorization audits harder.

Direct DbContext use would require less wrapper code, but would couple use cases
to EF and make the query boundary less explicit. The accepted cost is additional
interfaces and concrete repository methods. These are business-specific contracts,
not a generic repository exposing every possible operation. Domain references no
other project. API is the composition root, and the migration executable is a
separate outer host.

Repository doubles make service refusal paths observable. Real PostgreSQL
integration tests remain necessary to verify SQL filtering and relational behavior;
a mocked repository cannot prove tenant isolation (see the repository test suite).

## Soft deletion preserves records and activity context

Workspaces, memberships, projects, tasks and comments have explicit deletion
semantics. Normal repository reads hide deleted records. A deleted project also
hides its tasks through the active-parent predicate; it does not set every child's
deletion flag. Workspace activity can retain the history of deleted tasks/projects
for an active member. Business mutations and activity append in one commit.

Hard deletion would simplify storage retention, but would remove useful history
and require a separate audit/retention design. Soft deletion requires consistent
filters and parent checks, retains storage, and is not data erasure or a recovery
feature. There is no public restore endpoint. Runtime database grants intentionally
exclude DELETE; privileged migration credentials remain a separate trust boundary.

Existing service and PostgreSQL tests verify deleted reads, hidden descendants,
tenant isolation and activity behavior. The final v1.0 review must preserve these
contracts instead of treating a deletion flag as an automatic global filter.
