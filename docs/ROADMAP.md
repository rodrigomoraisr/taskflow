# TaskFlow — roadmap

Working plan. Kept in the repo so any session — mine, an AI assistant's, or a
reviewer's — starts from the real state rather than from memory.

**Status:** phase 0 and phases 1-7 complete. In phase 8 — 8.1 and 8.2 done, 8.3 next.

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
| 8.4 | Application-layer tests | Mock repositories; **do not** mock the authorization services — those are what's under test. |
| 8.5 | Domain tests — fill the gaps | 38 exist. Audit for uncovered transitions and the last-owner rule. |
| 8.6 | API integration tests | End-to-end through the real HTTP pipeline: register → login → create workspace → create project → create task → transition it. |
| 8.7 | Full suite green, CI wired | `dotnet test` clean, running in the workflow from 0.3. |

### Why 8.3 comes third

Tenant isolation is the one invariant in this system whose failure is a security
incident rather than a bug. There is a commit in the history that fixed a tenant
isolation hole in task operations — and nothing currently prevents it recurring.

The suite should prove, at minimum:

- A user in workspace A cannot read, update or delete a task in workspace B.
- The same for projects, and for workspace membership.
- A `Viewer` cannot mutate anything; a `Member` cannot change roles.
- A non-member gets 403 or 404, never 200 — and the choice between 403 and 404 is
  deliberate and consistent.
- The last owner of a workspace cannot be removed or demoted.
- A soft-deleted entity is invisible to reads and rejects writes.

Each of these is a named test. When one fails, the name says what broke.

### Moved out of phase 8

**Optimistic concurrency** was originally 8.7. It is a *feature* — a `rowversion`
column, a concurrency exception, and handling in the services — not test coverage.
Building production code inside a testing phase is how a one-week phase becomes
three. Moved to phase 12.

---

## Phase 9 — Comments & activity

Collaboration surface and an audit trail.

- `Comment` entity scoped to a task, soft-deleted, author-owned.
- Activity log: who changed what, when. Append-only.
- Decide early whether activity is derived from domain events or written explicitly
  by the services. Explicit is simpler and honest; events are the better story if
  the plumbing stays small.

## Phase 10 — Querying, filtering & sorting

- Filter tasks by status, priority, assignee, project, due-date range.
- Sorting with an allow-list of sortable fields — never interpolate a client string
  into an `OrderBy`.
- Keep the `[Range(1, 100)]` page-size cap. Consider cursor pagination if offset
  depth becomes a real problem; document the choice either way.

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
