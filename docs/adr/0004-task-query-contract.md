# ADR 0004 — Filtered task queries and explicit sorting

- **Status:** Accepted
- **Date:** 2026-09-07
- **Scope:** Phase 10, the existing paginated task-list endpoint

## Decision

Extend `GET /api/workspaces/{workspaceId}/tasks` without changing its response
shape (`items`, `page`, `pageSize`, `totalCount`). All supplied filters combine
with AND; omitted filters leave that dimension unrestricted.

| Parameter | Meaning |
| --- | --- |
| `status` | Todo, InProgress, Done (or their defined numeric values) |
| `priority` | Low, Medium, High, Critical (or their defined numeric values) |
| `assigneeUserId` | Exact assignee ID |
| `unassigned=true` | Only tasks without an assignee; incompatible with assigneeUserId |
| `projectId` | Exact project ID |
| `dueDateFrom` / `dueDateTo` | Inclusive timestamp bounds; use ISO 8601 with Z or an explicit offset |
| `sortBy` | createdAt (default), updatedAt, title, priority, status, dueDate |
| `sortDirection` | desc (default) or asc |
| `page` / `pageSize` | Existing defaults 1/20; pageSize remains capped at 100 |

Due-date timestamps bind as `DateTimeOffset` and are converted to UTC before EF
constructs the database predicate. They are instant boundaries, not whole-day
ranges. A date-only value is not expanded to the end of that day; clients should
send explicit offsets. Tasks without a due date do not match a supplied date bound.
An inverted range, undefined enum, empty filter ID, invalid sort or pagination
value returns 400. Service-level validation also protects non-HTTP callers.

Sorting tokens are case-insensitive. A switch selects typed LINQ expressions;
client text is never used as SQL or a dynamic expression. Status order is workflow
order (Todo, InProgress, Done), priority order is severity order (Low to Critical).
Title ordering follows the database collation. Nullable due/update timestamps
sort last in both directions. Every sort adds ascending task ID as a tie-breaker.

## Tenant and count guarantees

Authorization stays before repository reads. `workspaceId` remains a mandatory,
separate repository parameter taken from the route; query parameters cannot
replace it. A private active-task query enforces workspace ownership, task soft
delete and active parent project for both single lookups and lists.

Page and count methods accept the same `GetTasksRequest` and use one private
filtered query. Count is taken before paging and ignores sorting. Foreign project
or assignee filters yield only matching rows from the authorized workspace, never
rows from another tenant. A foreign workspace remains 404.

No service sees `IQueryable` or EF Core. The query request lives in Application,
and its interpretation as SQL remains in Infrastructure.

## Pagination tradeoff

Retain offset pagination because the existing API supports page-number navigation
and a total count. Stable tie-breaking fixes ambiguous ordering for equal values;
it does not provide a snapshot across concurrent writes. Page and count are two
queries and may observe different committed states during concurrent changes.
We do not add transaction isolation merely to make a browsing count exact.

Very deep offsets can become expensive. Arithmetic uses long before conversion to
EF's integer Skip; offsets beyond int.MaxValue return an empty page while keeping
the filtered total. Cursor pagination remains a future API change if usage shows
that deep navigation or concurrent-feed continuity matters. No speculative indexes
or database migration are added in this phase; measure real query plans first.

## Verification

- PostgreSQL tests independently exercise every filter, combined filters, UTC
  offset equivalence, inclusive bounds, all six sorts in both directions,
  nullable ordering, tied-page ordering, count/page agreement and huge offsets.
- HTTP tests validate model binding and 400 responses, combined pagination and
  tenant safety when project/assignee IDs belong to another workspace.
- Application tests prove the same query and cancellation token reach both
  repository calls, and invalid direct calls are refused before repository reads.
- Existing tenant, soft-delete and authorization-order tests remain in force.
