# ADR 0006 — Request diagnostics, API errors and optimistic concurrency

- **Status:** Accepted
- **Date:** 2026-09-07
- **Scope:** Phase 12

## Problem

Unexpected failures previously returned an opaque 500 without an application log.
Validation errors and application exceptions used different response formats.
Overlapping task/project saves could overwrite each other and record activity for
a change that was no longer current. There were no operational probes or request
limits outside authentication.

## Request diagnostics and errors

Use structured JSON console logs with scopes. Each request gets `X-Correlation-ID`:
accept one supplied value containing 1–64 ASCII letters, digits, dots, underscores
or hyphens; otherwise generate a GUID. Return the ID on success and failure, put it
in the logging scope and in the problem body's `correlationId` extension. It is
diagnostic metadata, never identity or authorization. Caller-supplied IDs need not
be unique; do not use them as database keys or metric labels.

Log request method, matched route template, status and elapsed milliseconds. Do not
log raw paths, query strings, request bodies, authorization headers or refresh
tokens in this middleware. Known exceptions log status and exception type without
their messages. Unexpected failures log the exception and correlation ID server-side,
but return only a generic detail to the client. Keep sensitive EF logging disabled
and protect operational logs: exception diagnostics can contain internal details.
Client cancellation does not become a logged 500. A failure after headers have
started is logged and rethrown; appending an error document would corrupt the body.

Replace `{ "error": "..." }` with `application/problem+json` ProblemDetails,
following RFC 9457 (which supersedes the roadmap's RFC 7807). Exception responses,
empty routing/authentication errors and rate-limit rejections use a common writer.
MVC retains its standard validation `errors` dictionary and receives the same
correlation customization. Error responses are not cacheable. Health probes have
their own intentionally minimal plain-text contract below.

Example application failure:

```json
{
  "type": "about:blank",
  "title": "Conflict",
  "status": 409,
  "detail": "The resource changed while this request was being processed. Reload it and review your changes before trying again.",
  "correlationId": "client-request-123"
}
```

The tenant 403/404 decision in ADR 0001 remains intact. A missing workspace and a
workspace hidden from the caller have identical problem fields for the same
workspace ID, except per-request metadata. Tests comparing complete bodies hold
the correlation ID constant. Logs can distinguish the two exception types; the
HTTP response cannot. Clients consuming the old `error` property must migrate to
`detail` (and validation `errors` where present).

## Probes and rate limits

| Endpoint/control | Behavior |
| --- | --- |
| `/health/live` | 200 `Healthy` when the request pipeline can respond; no database check |
| `/health/ready` | Opens a database connection through EF; 200 `Healthy` or 503 `Unhealthy` |
| Global request budget | 120 requests per remote IP per 60-second fixed window; no queue |
| Auth request budget | Existing 20 requests per remote IP per 60-second window, additional to the global budget |
| Limit rejection | 429 ProblemDetails with `Retry-After` in seconds |

Readiness has a five-second check timeout and does not expose connection details.
It checks connectivity, not schema compatibility, migration state or every SQL
permission. Both probes are anonymous, disable caching and bypass rate limiting so
budget exhaustion cannot falsely signal an unhealthy process. Restrict probe
exposure at deployment if needed. Global limits cover unmatched routes as well as
controllers. Configure `GlobalRateLimit:PermitLimit` and `WindowSeconds`; invalid
options fail startup. Auth options retain the same names under `AuthRateLimit`.

These budgets are in-memory, per instance, and reset on restart. Fixed windows can
allow a burst across a window boundary; clients behind a shared IP share a budget.
They are not distributed quotas or network-level denial-of-service protection.
The API still ignores arbitrary forwarded-IP headers. Configure trusted proxy
addresses explicitly before deployment behind a reverse proxy.

## Concurrency

Map an EF shadow `uint` property named `Version` with `IsRowVersion()` on both
`TaskItem` and `Project`. Npgsql maps it to PostgreSQL's existing `xmin` system
column, which changes on row updates. A persistence-only shadow property keeps
database metadata out of Domain and avoids introducing SQL Server's `rowversion`
type into a PostgreSQL project. All mutation lookups already track their entities.

EF includes the originally loaded version in the update predicate. If another
transaction changes that row between load and save, no row matches and EF raises
`DbUpdateConcurrencyException`. `EfUnitOfWork` translates it to the Application
exception `ConcurrencyConflictException`; the API maps that to 409. No automatic
retry occurs. The caller should reload and review the current resource before
submitting another operation. A fresh request that finds a soft-deleted entity
still receives 404 through the existing repository filters.

Task activity and task changes share the same EF save transaction, so a concurrency
failure rolls back the losing activity insertion as well. Discard the request's
context on failure; it still contains the rejected tracked changes. Services must
not catch a conflict and save that same context again.

This is **request-time concurrency protection**, not an old-form detector. The API
does not yet accept a client version or `If-Match`. If a client reads at noon and
edits later, the later request loads the then-current version; it can replace a
prior edit. A future client concurrency contract would need a supplied version and
explicit precondition semantics. Comments, memberships and cross-row invariants
are outside this token scope; comment edits retain last-write-wins behavior.

The `AddTaskAndProjectConcurrency` migration records the mapping. Npgsql skips
physical `xmin` add/drop SQL because PostgreSQL owns that column. Apply migrations
normally to record the version in migration history.

## Verification

- PostgreSQL tests load the same row in two independent contexts, commit a winner,
  and assert rejection of stale task/project updates and soft deletes.
- A task conflict leaves no losing activity entry. An HTTP test injects a second
  writer after service load and verifies 409 plus persisted winner/history state.
- Pipeline tests verify validation, authentication, missing-route and method errors,
  content type, correlation IDs, invalid-ID replacement and bounded IDs.
- A shared limiter test exhausts its budget across different routes, checks
  `Retry-After`, rejects forwarded-IP spoofing, and still reaches both probes.
- Readiness tests use real working and unreachable PostgreSQL connections.
- Middleware tests check safe 500 bodies, exception logging, request scopes,
  cancellation and failures after the response has started.

## References

- [Npgsql concurrency tokens](https://www.npgsql.org/efcore/modeling/concurrency.html)
- [EF Core concurrency conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [ASP.NET Core error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
- [ASP.NET Core health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457.html)
