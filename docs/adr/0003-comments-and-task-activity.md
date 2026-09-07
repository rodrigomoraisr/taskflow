# ADR 0003 — Author-owned comments and transactional task activity

- **Status:** Accepted
- **Date:** 2026-09-07
- **Scope:** Phase 9, task collaboration and history

## Context

TaskFlow needs collaboration on tasks and a readable record of who changed what,
when. The project uses explicit application services and one unit of work per
operation. Introducing an event bus solely to write a second row would hide the
transaction boundary without giving us an independent consumer.

## Decision

### Comments and authorization

A comment belongs to one `(WorkspaceId, TaskId)` and has an immutable `AuthorId`.
The server derives authorship from `ICurrentUser`; the request only accepts `Body`.
Body is required, capped at 2,000 characters, and trimmed. Validation is enforced
in both the HTTP DTO and the Domain entity.

| Caller | Read comments/history | Create comment | Edit/delete comment |
| --- | --- | --- | --- |
| Active Owner/Admin/Member | Yes | Yes | Only their own |
| Active Viewer | Yes | No | No, even if they authored it |
| Non-member or removed member | 404 | 404 | 404 |

No moderation override is included. An Owner cannot rewrite another author's
words. A later moderation feature needs a separate decision and a distinct action.

Membership and role gates precede all entity reads. Edit/delete must then load
the scoped comment before checking `AuthorId`; this is an entity-dependent
ownership decision, documented as an extension to ADR 0002.

Comments are soft-deleted. Deleted comments, tasks, or parent projects make the
comment inaccessible through comment endpoints and repository reads. The rows
remain in the database; soft deletion is not erasure or a retention policy.

### Explicit recording, one transaction

`TaskService` and `CommentService` append `TaskActivity` through a repository before
calling the existing unit of work once. EF Core saves the business change and
activity insert together. If either write fails, neither should persist.

Task activity covers create, detail update, delete, start, complete, reopen,
assignment and unassignment. It stores the actor, UTC timestamp, action and JSON
before/after snapshots of the business fields. Unchanged task snapshots do not
produce an entry. Existing rows are not backfilled: the history starts when this
feature is deployed, not when the task was originally created.

Comment activity covers create, edit and delete, storing the comment ID but **no
comment body or revisions**. Editing/deleting a comment does not leave another
copy of its text in the activity payload. Task title/description snapshots do
remain in task activity; they are part of the retained task history.

Domain events were considered and deferred. They become useful when multiple
independent reactions justify the dispatch and delivery semantics. This phase has
one local persistence concern, so explicit service calls are easier to inspect.

### Readable history after deletion

`GET /api/workspaces/{workspaceId}/activity` is a paginated workspace feed of task
and comment actions, optionally filtered by `taskId`. It is not a log of workspace
or project operations. It retains entries for deleted tasks and projects, so task
deletion itself remains visible. Active workspace membership is always required.
A foreign/nonexistent task filter within one's own workspace yields an empty list.

Comments are ordered oldest first; activity is newest first. Both use ID as a
stable tie-breaker, default to 20 items, and cap page size at 100. They return page
arrays without a total count. Offset pagination is not a snapshot across concurrent
writes; cursor pagination remains a future choice if usage warrants it.

### Persistence safeguards and limits

Composite foreign keys enforce comment → task, activity → task, and optional
activity → comment workspace/task alignment. Repository reads independently apply
the route's workspace filter. Indexes support the feed and comment ordering.

`TaskActivity` has no mutation methods. Its repository exposes only add and read.
`TaskFlowDbContext` rejects modified/deleted activity entries for synchronous and
asynchronous saves. There is no public API for rewriting history.

This is application-enforced append-only history, **not** tamper-proof compliance
logging: privileged SQL, bulk SQL bypassing tracking, migrations and database
administrators are outside that guarantee. The test fixture can still reset rows
using Respawn. Database privileges/immutable external storage would require a
separate operational design.

Optimistic concurrency remains deferred to Phase 12. Simultaneous comment edits
currently use the existing last-write-wins behavior, and activity ordering uses
recording time rather than a global commit sequence.

## Verification

- Domain tests: text boundaries, invalid IDs, ownership identity preservation,
  deleted guards, and activity action/comment consistency.
- Service tests: real authorization services, author-only mutation, actor identity,
  cancellation propagation, no commit/activity on ownership rejection.
- Extended ADR 0002 audit: all six new service methods refuse non-members before
  reads; all three comment writes refuse Viewers before reads.
- PostgreSQL/HTTP tests: comment lifecycle, role changes with existing tokens,
  wrong-parent and cross-tenant IDs, repository filtering, deleted parents,
  pagination, activity payloads, task transitions and no-op/refusal behavior.
- Persistence tests: cross-tenant FK rejection, activity immutability through EF,
  and a failed activity insert leaving the task and history unchanged.
