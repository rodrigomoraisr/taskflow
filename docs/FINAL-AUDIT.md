# v1.0 final review — 2026-09-10

## Findings and changes

The final source review found a last-owner write-skew race: two requests could
count two owners and then remove or demote different memberships. Membership
changes now hold a workspace row lock through authorization recheck, owner count
and commit. ADR 0010 explains the boundary; three real PostgreSQL regression tests
cover removal, demotion and revoked caller authority. No migration is needed.

The review also checked repository tenant/soft-delete predicates, service role
gates, refresh-session locks and replay handling, task/activity atomicity, task and
project concurrency conflicts, bounded error/log metadata, explicit proxy trust,
the separate migrator, runtime SQL grants and deployment workflow permissions.
Existing integration tests exercise these boundaries, including refusal paths.

## Repeatable release checks

Local results: 541 tests passed (129 Domain, 147 Application, 265 API integration),
the Release analyzer build passed without warnings, and both container targets
passed the isolated smoke script, including migration/runtime grant checks.

```sh
dotnet restore -warnaserror
dotnet list package --vulnerable --include-transitive
dotnet build --configuration Release --no-restore -warnaserror
dotnet test --configuration Release --no-build
bash scripts/smoke-containers.sh
git diff --check
```

NuGet's advisory source reported no known vulnerable packages in all nine projects,
including transitive dependencies, on the audit date. This is an advisory-based
NuGet check, not an OS image vulnerability scan or penetration test. It can change
as advisories are published. Shared build settings enable .NET 10's default analyzer
rule set and code-style build analysis, without claiming every optional rule is on.
Warnings fail builds; audit warnings fail restores. CI runs the full suite and
isolated container smoke checks before publishing release images.

NuGet audit behavior: [Microsoft documentation](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).

## Explicit limits

- Task/project concurrency detects overlapping database writes, not stale client
  forms. Comments have no concurrency token. Authorization is generally evaluated
  per request; it does not revoke every operation already in flight.
- Task pages and counts are separate queries and can differ during concurrent writes.
  Workspace/project/member lists are unpaginated; this release targets low traffic.
- Login and refresh have transaction locks. Access-token revocation is bounded by
  expiry; clients must serialize refresh. MFA, account recovery and token-row cleanup
  remain absent.
- Logs and probes are available; managed tracing, alerting, load testing and a
  database recovery drill are not certified by this review.
- The NuGet audit excludes OS packages and GitHub Action internals. Dependencies,
  base images and action major tags require ongoing maintenance.
- Runtime grants limit ordinary API SQL; privileged SQL can bypass application
  invariants. The release pipeline deliberately has no automatic schema rollback.

The sixteen-phase portfolio scope is complete when the release checks and tag
publication succeed. This does not mean the service needs no future maintenance.
