# TaskFlow — roadmap

Working plan. Kept in the repo so any session — mine, an AI assistant's, or a
reviewer's — starts from the real state rather than from memory.

**Status:** phase 0 and phases 1-10 complete locally. Phase 11 next. Phase 10 has 455 passing Release tests. Phase 9 is committed as `0a07baa`. Completed phases are committed and pushed as they finish; LinkedIn posts follow a separate publishing schedule. Remote CI for Phase 10 is pending. Phase 8 remote CI passed on `6c54d24`.

---

## Principle

This project's job is to be **shown**, not to be finished. Anything that blocks it
from being presentable in an interview outranks anything that makes it more
complete. That is why documentation, CI and secrets handling were pulled forward out
of their original late positions.

---

## Phase 0 — Presentable (do first, ~1 week)

Not in the original plan. Pulled forward because these four items gate everything
else: they are what a reviewer sees before reading any code.

| # | Item | Status |
| --- | --- | --- |
| 0.1 | `README.md` — architecture, decisions, deliberate omissions | ☑ |
| 0.2 | `CLAUDE.md` — conventions for AI-assisted work | ☑ |
| 0.3 | CI: GitHub Actions running build + test on push and PR | ☑ |
| 0.4 | JWT signing key out of `appsettings.json` and into user secrets | ☑ |
| 0.5 | Delete the placeholder `UnitTest1.cs` files | ☑ |

After phase 0 the project is interview-ready. Everything below is improvement, not
prerequisite.

---

## Phase 8 — Automated tests

Locks down what phases 1–7 built. Reordered from the original so the highest-value
test comes early rather than sixth.

| # | Item | Notes |
| --- | --- | --- |
| 8.1 | Test infrastructure | Shared fixtures, builders for `TaskItem` / `Project` / `WorkspaceUser`. Remove placeholder tests. |
| 8.2 | PostgreSQL integration infrastructure | **Testcontainers**, not the in-memory provider — the in-memory provider does not enforce constraints and would let an isolation bug pass. Respawn to reset state between tests. |
| 8.3 | **Tenant / security regression suite** | The most valuable work in this entire roadmap. See below. |
| 8.4 | Application-layer and repository tests | ☑ Done. Mock repositories; **do not** mock the authorization services — those are what's under test. See below. |
| 8.5 | Domain tests — fill the gaps | ☑ Done, folded into 8.4 as its group 4. |
| 8.6 | API integration tests | ☑ Done. Register → login → create workspace → create project → create task → start → complete → reopen. API and database assertions after each transition, plus a rejected-transition persistence check. |
| 8.7 | Full suite green, CI wired | ☑ Done locally: `dotnet test --configuration Release --verbosity quiet` — 353 passed, 0 failed, 0 skipped on 2026-09-07. Existing workflow includes Release build/test and test-result artifacts. [Remote CI passed](https://github.com/rodrigomoraisr/taskflow/actions/runs/34140065601) for `6c54d24`. |

### Why 8.3 comes third

Tenant isolation is the one invariant in this system whose failure is a security
incident rather than a bug. There is a commit in the history that fixed a tenant
isolation hole in task operations — and nothing currently prevents it recurring.

The suite should prove, at minimum:

- A user in workspace A cannot read, update or delete a task in workspace B.
- The same for projects, and for workspace membership.
- A `Viewer` cannot mutate anything; a `Member` cannot change roles.
- A non-member gets 404, never 403 and never 200: the response is identical to the one
  for a workspace that does not exist. A member whose role is too low gets 403.
  Settled — see "403 versus 404" in CLAUDE.md.
- The last owner of a workspace cannot be removed or demoted.
- A soft-deleted entity is invisible to reads and rejects writes.

Each of these is a named test. When one fails, the name says what broke.

### What 8.4 found

Three layers were guarded by 22, 0 and 1 tests respectively before it started.
The suite went from 144 tests to 351, and the measurements that drove it are
worth keeping:

- **The repository tenant filter was the thin layer, and the reason was the
  route, not the repository.** Dropping the `workspaceId` predicate from
  `TaskRepository.GetByIdAsync` turned one test red, because every cross-tenant
  scenario in 8.3 stopped at the membership gate first. The shape that reaches
  the filter is a *legitimate member* using their own workspace id in the route
  with a foreign entity id in the path, and only the GET route had that test.
  Adding it for the other seven routes, plus direct repository tests, takes the
  same break to **10** red.
- **`CountAsync` and `GetPagedAsync` do apply the same filters**, but as two
  separately written expressions with no shared query builder. They are now
  asserted against each other rather than separately, so a divergence that would
  make the pager promise rows it will never show fails a test that names it.
- **`CountActiveOwnersAsync` is correctly scoped to one workspace.** The
  last-owner rule depends on it entirely, so it now has direct tests.
- **Check-before-load holds.** All 21 workspace-scoped methods refuse a
  non-member before reading; 15 of the 16 role-gated ones also refuse a
  too-low role before reading. The
  exception is `TaskService.AssignAsync`, and it is structural. Recorded in
  `docs/adr/0002`.
- **`TaskItem`'s deleted guard threw an unmapped exception type**, which would
  have produced a 500 rather than the 409 the middleware is set up for. Fixed.

### Moved out of phase 8

**Optimistic concurrency** was originally 8.7. It is a *feature* — a `rowversion`
column, a concurrency exception, and handling in the services — not test coverage.
Building production code inside a testing phase is how a one-week phase becomes
three. Moved to phase 12.

---

## Phase 9 — Comments & activity

**Complete locally on 2026-09-07.** Scope and tradeoffs: [ADR 0003](adr/0003-comments-and-task-activity.md).

- ☑ Author-owned comments: create/read/edit/soft-delete, 2,000-character limit.
- ☑ Member/Admin/Owner may comment; Viewer reads only; no admin ownership override.
- ☑ Task/comment activity written explicitly in application services in the same
  unit-of-work save as the business change. No event bus introduced.
- ☑ Paginated comments and workspace task-activity feed with optional task filter.
- ☑ Tenant/parent filters and composite FKs; history retained after task deletion.
- ☑ Domain, service, HTTP and PostgreSQL tests, including rollback and append-only
  persistence checks. 410 tests pass: 119 Domain, 135 Application, 156 integration.
- ☑ ADR 0002 extended for ownership checks; README and working notes updated.

History starts at deployment; existing tasks are not backfilled. Comment content
is not copied into activity. Append-only enforcement covers the application and
tracked EF saves, not privileged SQL. Concurrency remains in Phase 12.

## Phase 10 — Querying, filtering & sorting

**Complete locally on 2026-09-07.** Contract and tradeoffs:
[ADR 0004](adr/0004-task-query-contract.md).

- ☑ AND-combined status, priority, assignee, project and inclusive due-date filters.
- ☑ Explicit unassigned filter; date offsets normalized to UTC.
- ☑ Six allowed sort fields, both directions, stable ID tie-breaker and null dates last.
- ☑ Shared repository predicates for filtered pages/count and mandatory tenant scope.
- ☑ Existing page-number API and 100-item cap retained; overflow-safe offsets.
- ☑ Query validation through HTTP and direct service calls.
- ☑ Full Release suite: 455 passed, 0 failed, 0 skipped — 119 Domain,
  140 Application, 196 integration. No migration required.

Offset pagination remains appropriate for the current page/total-count contract;
concurrent writes can change pages/counts between queries. Revisit cursor pagination
and indexes when measured usage justifies them. Remote CI is verified after push; publication follows a separate schedule.

## Phase 11 — Authentication hardening

- Refresh tokens with rotation and reuse detection.
- Logout / revocation. Decide and document: short-lived access tokens plus a refresh
  flow, or a revocation list. Both are defensible; the trade-off is instant
  revocation versus a database round trip per request.
- Password rules and account lockout on repeated failures.
- Rate limiting on `/auth/*` specifically — currently nothing stops credential
  stuffing.

## Phase 12 — Observability, API hardening & concurrency

- `ILogger` in `ExceptionMiddleware`. Right now an unexpected 500 leaves no trace
  anywhere, which is the most serious operational gap in the project.
- RFC 7807 `ProblemDetails` responses replacing the current `{ error: "..." }` shape.
- Correlation id per request, surfaced in error responses and logs.
- Health checks: `/health/live` and `/health/ready`, with readiness actually checking
  the database.
- Global rate limiting.
- **Optimistic concurrency** (moved from 8.7): `rowversion` on `TaskItem` and
  `Project`, mapped in the EF configuration, with `DbUpdateConcurrencyException`
  translated to a 409.

## Phase 13 — Docker & deployment

- `Dockerfile` for the API, multi-stage, non-root user.
- `docker-compose.yml` that actually runs the whole system, not just PostgreSQL.
  Today `docker compose up` starts a database and nothing else.
- Migrations on startup versus a separate migration step — pick one and write down
  why.

## Phase 14 — CD

CI already exists from phase 0.3. This adds deployment.

- Build and publish a container image on tag.
- Deploy to a free-tier host. A live URL in the README is worth more than any
  amount of local setup instructions.

## Phase 15 — Documentation & portfolio polish

README exists from phase 0.1; this is the polish pass.

- Architecture diagram — one image beats three paragraphs.
- OpenAPI descriptions and examples on every endpoint.
- An `ADR/` folder for the decisions worth their own page: why workspace id in the
  route, why soft delete, why repository over `DbContext`.

## Phase 16 — Final review & v1.0

- Dependency audit, warnings as errors, analyser pass.
- Re-read the whole thing as a reviewer would, in one sitting.
- Tag `v1.0.0` and write release notes.

---

## Deferred indefinitely

Named so they stop feeling like gaps:

- Frontend of any kind.
- File attachments and object storage.
- Full-text search.
- Notifications and email.
- Real-time updates.
- Multi-region anything.
