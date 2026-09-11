# 0010 — Serialize membership changes within a workspace

Status: accepted, 2026-09-10.

## Context

Checking that another owner exists and then saving a removal or demotion in
separate database statements permits write skew. With two owners, concurrent
requests can each observe two owners and remove different rows, leaving none.
A concurrency token on each membership would not detect that conflict.

## Decision

Membership additions/restorations, role changes and removals acquire a PostgreSQL
`FOR UPDATE` lock on the workspace row in an explicit transaction. The initial
membership/role gate runs before this repository call. Once the lock is acquired,
the service checks current authority again, then loads the target, counts owners,
mutates and commits. Waiting requests therefore see the previous writer's commit.
Disposal rolls back failed operations and releases the lock; cancellation propagates.

`IApplicationTransaction` is the application-owned boundary shared with auth.
The workspace repository owns the EF transaction and parameterized lock query.
No schema migration or additional runtime database privilege is required.

## Consequences

Only membership writes in the same workspace serialize. This is acceptable for
the expected low traffic; task/project operations retain their existing concurrency
behavior. Privileged direct SQL can bypass this application protocol. Ordinary
requests still authorize at request time, rather than providing global transactional
revocation of every in-flight operation.

Three tests gate real PostgreSQL transactions through the real service and
authorization layer: simultaneous owner removals, simultaneous demotions, and a
queued caller whose membership the first writer removes. Exactly one owner remains.
