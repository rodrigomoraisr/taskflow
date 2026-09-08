# ADR 0007 — Container runtime and a separate migration command

- **Status:** Accepted
- **Date:** 2026-09-07
- **Scope:** Phase 13; live hosting and continuous deployment remain Phase 14

## Decision

Build API and migration images from one multi-stage Dockerfile. Restore project
dependencies before copying source to reuse Docker's dependency cache. Publish in
the .NET 10 SDK image and copy only published output into ASP.NET Core runtime
images. Both final targets use the image's non-root `APP_UID`. There is no SDK,
EF CLI or signing key in either final image. The migration host references
Infrastructure as an outer executable; Domain/Application dependencies stay intact.

`api` is the default Docker target and listens on port 8080. It includes curl for
the readiness health check. `migrations` runs `TaskFlow.Migrator` and exits. Using
the official multi-architecture images allows native ARM64 and AMD64 builds without
hard-coded runtime identifiers. The `10.0` tags follow servicing updates; Phase 14
should record deployed image digests for reproducibility and rollback.

The build context excludes local environment files, certificates, keys, user
secrets, build output and tests. Supply secrets at runtime. Compose reads the API
key from `TASKFLOW_JWT_KEY`; a missing key still fails the API's existing startup
guard. Database-only development remains possible without a JWT key.

## Migration lifecycle

Compose expresses this dependency chain:

```text
PostgreSQL healthy → migration command exits 0 → API starts and becomes healthy
```

The migration command requires `ConnectionStrings__DefaultConnection` and calls
EF's `MigrateAsync` against the existing Infrastructure migrations. It needs no
JWT key and does not start an HTTP server. Repeated runs are safe: migration
history records what was applied. A failure returns a nonzero exit code and stops
initial API startup. Its output reports pending count and failure type without
printing credentials; inspect PostgreSQL logs or use the local EF command for
detailed migration troubleshooting.

This was chosen over migrations in API startup because schema changes are an
explicit deployment operation. Restarting or scaling the API should not initiate
DDL. EF migration locking is still useful, but it does not replace release
coordination, backups or review of destructive migrations. A standalone published
host avoids shipping an SDK or adding a design-time factory solely to build an EF
bundle. EF Core Relational is explicitly aligned with Core at 10.0.9 so the new
host does not inherit an older transitive runtime assembly from the provider.

For future deployment, run the matching migration image as a release job and
only roll out the API after success. Use separate DDL and runtime database roles
there. Local Compose uses the existing development PostgreSQL role for both;
non-root Linux execution does not imply restricted database privileges. Running
API instances are not stopped automatically if a later migration fails, and
`docker compose restart api` does not rerun migrations. Schema changes must be
compatible with any still-running version during a rollout.

## Local runtime boundaries

Compose publishes API and database ports only on `127.0.0.1`, defaults 8080/5432.
`API_PORT` and `POSTGRES_PORT` may change host ports; container connections always
use `postgres:5432`. The existing `postgres-data` volume name is preserved, and the
fixed container name is removed so isolated test projects can run concurrently.
Changing the Compose project name selects a different volume. Changing
`POSTGRES_PASSWORD` does not rotate credentials in an already-initialized volume.

API and migrator use a read-only root filesystem, writable temporary filesystem,
dropped Linux capabilities and no-new-privileges. Compose supplies an init process
for signal handling and child reaping; this also made the observed unhandled
startup failure terminate correctly in the Docker Desktop runtime. API core dumps
are disabled to avoid large dump files. Use `--init` when running an image directly
with Docker. The local database uses password authentication; Compose disables
Npgsql's optional GSS encryption negotiation to avoid its missing-Kerberos-library
warning in the minimal runtime. TLS behavior is separate from that setting.

`ASPNETCORE_ENVIRONMENT=Production` disables development OpenAPI and user secrets.
It does not create a TLS certificate: this local stack serves HTTP on loopback.
Before public hosting, configure HTTPS at a trusted ingress, trusted forwarding
addresses, database TLS and runtime secrets. Those settings depend on the Phase 14
host and are intentionally not guessed here. Do not expose this local Compose
configuration directly as a public production deployment.

The API image health check calls `/health/ready`; PostgreSQL uses `pg_isready`.
Liveness remains independent of database availability. Compose does not restart
an unhealthy but running container automatically; readiness is a signal for an
operator or orchestrator, not a recovery policy. `restart: unless-stopped` handles
process exits. Probes remain available without authentication.

## Verification

`bash scripts/smoke-containers.sh` creates a unique Compose project with generated
test credentials, ephemeral loopback ports and its own database volume. Its cleanup
removes only those temporary resources. It verifies:

- A real migration authentication failure exits 1 and prevents API startup.
- A fresh database is migrated and a second run reports zero pending migrations.
- Both executables run non-root; the API runtime contains no SDK.
- A missing JWT key exits with an error within a bounded wait.
- Production hides OpenAPI and returns correlated ProblemDetails.
- Registration, login, project/task creation and subsequent task reads work over HTTP.
- Database outage produces readiness 503 while liveness remains 200.
- Restarting the database and API preserves login and task data.

CI runs this script on Linux in a separate job alongside the 516-test Release
suite. No image is published and no host is deployed in this phase.

## References

- [Microsoft container guidance](https://learn.microsoft.com/en-us/dotnet/core/docker/build-container)
- [EF Core migration deployment choices](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
- [Compose startup conditions](https://docs.docker.com/compose/how-tos/startup-order/)
- [Npgsql GSS/TLS settings](https://www.npgsql.org/doc/security.html)
