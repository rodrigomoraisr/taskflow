# Architecture decisions

Start with the decision relevant to the behavior you are reviewing. These records
describe implemented choices and their limitations; they are not promises of future features.

| Decision | Why it matters |
| --- | --- |
| [0001 — Tenant boundary responses](0001-tenant-boundary-responses.md) | Non-members cannot distinguish inaccessible and nonexistent workspaces. |
| [0002 — Authorize before loading](0002-check-before-load.md) | Membership and role gates precede entity reads, with explicit entity-dependent exceptions. |
| [0003 — Comments and activity](0003-comments-and-task-activity.md) | Authors control comments; activity and business writes share a transaction. |
| [0004 — Query contract](0004-task-query-contract.md) | Filters, stable ordering, null placement and pagination behave consistently. |
| [0005 — Authentication sessions](0005-authentication-sessions-and-abuse-controls.md) | Refresh rotation, lockout and per-process request budgets have explicit boundaries. |
| [0006 — Observability and concurrency](0006-observability-hardening-and-concurrency.md) | Correlated errors and database concurrency protect defined failure paths. |
| [0007 — Containers and migrations](0007-container-runtime-and-migrations.md) | Schema changes happen in a separate release command with restricted runtime grants. |
| [0008 — Trusted proxy headers](0008-trusted-proxy-headers.md) | Only a verified ingress may supply the effective client address and scheme. |
| [0009 — Tenant context and persistence](0009-tenant-context-and-persistence.md) | Route-based workspace selection, repositories and soft deletion keep boundaries explicit. |
