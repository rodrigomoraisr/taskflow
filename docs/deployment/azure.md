# Azure deployment — Phase 14 in progress

The selected setup is Linux App Service B1 in East US, Neon Free PostgreSQL,
public GitHub Container Registry images and Azure Key Vault Standard. The monthly
budget is R$120; Azure budget alerts do not impose a spending cap.

## Current setup

- Resource group: `rg-taskflow-prod`.
- Key Vault: `kv-taskflow-rodrigo`.
- Secrets created: `taskflow-jwt-key`, `taskflow-db-runtime`, `taskflow-db-migrations`.
- Neon project: `old-flower-43806075`, production branch, database `neondb`.
- Runtime role: `taskflow_app`; migration role: `neondb_owner`.

Connection strings use the direct Neon hostname, `SSL Mode=VerifyFull` and
`GSS Encryption Mode=Disable`. Values belong only in secret storage.
The initial migrations and explicit runtime table permissions have been applied.

The Web App `taskflow-rodrigo-api` has now been created with a system-assigned
identity. Its JWT and runtime Key Vault references resolve successfully, and both
health endpoints returned HTTP 200. This checks connectivity, not schema readiness.
Public hostname: `taskflow-rodrigo-api-etgcbgg6a0bseybd.eastus-01.azurewebsites.net`.

## Initial database setup

For the first guided deployment, generate an idempotent SQL script with:

```bash
dotnet ef migrations script --idempotent \
  --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.Api \
  --output /tmp/taskflow-production-migrations.sql
```

Run it in Neon's production SQL Editor, database `neondb`, as `neondb_owner`,
then run [runtime-permissions.sql](runtime-permissions.sql). The generated script
records the same EF migration history used by the migration runner, so future
pipeline runs skip applied migrations. Keep one migration operator at a time.

The runtime role can read, insert and update the eight mutable application tables;
task activity allows only reads and inserts. No physical deletes, schema creation,
migration history access or sequence privileges are granted. Future tables require
explicit permission updates. The initial script and grants were tested twice on
disposable PostgreSQL 17, including runtime row locks and forbidden-operation checks.
On 2026-09-09, the user confirmed successful execution in Neon. Live HTTPS
verification then passed registration (201), login and refresh (200), project/task
creation (201), task retrieval (200), project soft deletion (204), hidden-task
lookup (404), and logout (204). These checks exercise runtime writes, authentication
row locks and task activity inserts. A dedicated randomly credentialed test account
and workspace remain; the test project was soft-deleted and the session logged out.
No test passwords or tokens were retained.

## Publish images

1. Open the repository's **Actions → Publish containers**.
2. Select **Run workflow**, branch **main**, then **Run workflow**.
3. Wait for validation (Release tests and disposable container smoke tests), then
   both image publication jobs to succeed.
4. Open the owner's GitHub **Packages** and set each new package's visibility to
   **Public** in its package settings. New GHCR packages default to private.
5. Record the image digest from each job summary for deployment.

The workflow also runs for `v*` tags. It publishes Linux AMD64 images:

- `ghcr.io/rodrigomoraisr/taskflow-api:sha-<full-commit-sha>`
- `ghcr.io/rodrigomoraisr/taskflow-migrations:sha-<full-commit-sha>`

Publication uses the job's `GITHUB_TOKEN` with `packages: write`; no registry PAT
or production secrets are required. Commit tags identify source revisions; use
the digest for an exact artifact because rerunning a build can update a tag.
On `main`, publication now calls the production deployment job after both images
succeed. Tag runs publish only. Starting **Publish containers** on `main` is an
explicit production release; ordinary pushes run CI without deploying.

## GitHub deployment identity

The user created `id-taskflow-github` with federated credential
`github-taskflow-production`. Its issuer is
`https://token.actions.githubusercontent.com`, audience is
`api://AzureADTokenExchange`, and expected subject is
`repo:rodrigomoraisr@53228228/taskflow@1271757589:environment:production`.
GitHub's repository OIDC settings now enable `use_immutable_subject` with the
default template. The `production` environment permits only branch `main` and
contains `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` variables.
Use the user-assigned identity's client ID, not its principal ID.

The user confirmed Website Contributor on this Web App and Key Vault Secrets User
on the individual `taskflow-db-migrations` secret for that identity. The runtime
identity and its secret permissions remain separate. The deploy job reads the
migration secret directly from Key Vault after Azure sign-in; no database secret
is copied into GitHub.

Run **Actions → Verify Azure access → Run workflow → main** to test actual OIDC
sign-in and reads of the Web App and migration secret. The workflow suppresses
secret output and does not change the app or database. Success proves those reads,
not a deployment or migration. Run 34374863408 verified all three operations on
2026-09-09. A serialized release job is called by `Publish containers` on `main`.
It consumes digest artifacts returned by the two builds in that run and attempt,
refuses a source revision that is no longer the head of `main`, and reads the
migration secret from Key Vault. The separate migration container applies EF
migrations and the embedded `runtime-permissions.sql` when
`TASKFLOW_APPLY_RUNTIME_GRANTS=true`; a failure in either stops the rollout.
Local Compose omits this flag because it uses its development database role.

The release then records the previous image, updates the `main` site container
to the API digest, restarts the app, verifies the configured image, and waits for
public liveness/readiness HTTP 200 responses. Readiness checks here are bounded
release checks, not recurring monitoring. Health confirms the service and database
are reachable; it is not an authenticated business-flow test or a runtime revision
attestation. Review App Service container logs if rollout identity is uncertain.

If a run fails, inspect its failed step. Rerun all jobs (or start a new publication)
so both digest artifacts belong to the current run attempt. Schema changes must
remain compatible with the API serving during migration. There is no automatic
database downgrade or API rollback: if necessary, redeploy the recorded previous
image after checking schema compatibility. B1 has no deployment slot here, so a
restart can interrupt requests. Concurrency prevents overlapping releases; GitHub
may replace a pending release with a newer one.

## Verified release and operating checks

On 2026-09-09, [release 34399595283](https://github.com/rodrigomoraisr/taskflow/actions/runs/34399595283)
successfully deployed source revision `806ca7a` through the entire pipeline:
Release tests, disposable container checks, both image builds, OIDC sign-in,
migrations and runtime grants, digest rollout, restart, and public health checks.
Independent requests after the run also returned `Healthy`/HTTP 200 from both
`/health/live` and `/health/ready`. The earlier release stopped before production
changes because Azure CLI returns the container's `image` at the top level;
the deployment now queries that shape rather than the ARM `properties.image` shape.

Proxy diagnosis: the deployed `6f3d07e` image and the three temporary settings
(`ReverseProxy__Enabled=true`, `ReverseProxy__KnownProxies__0=127.0.0.1`,
`Logging__LogLevel__Microsoft.AspNetCore.HttpOverrides=Debug`) were confirmed, but
the marked application request produced no unknown-proxy log. This does not prove
that forwarding is correct. After deploying the diagnostics change, temporarily
set `ReverseProxy__Diagnostics=true`: the first ten `/health/live` requests per
process log the socket peer, effective client IP/scheme and header-presence flags.
No raw header values, tokens, bodies or query strings are logged or returned.
On 2026-09-09, live diagnostics identified the immediate peer as
`::ffff:169.254.130.1`. Configuring `ReverseProxy__KnownProxies__0=169.254.130.1`
restored the caller address and HTTPS scheme. A baseline request and another with
forged `X-Forwarded-For: 192.0.2.123` and `X-Forwarded-Proto: http` both produced
the same effective caller IP and `https` in the logs at 15:13:50–51 UTC. No caller
IP is retained in this runbook. This verifies the observed Azure ingress path;
recheck peer trust after hosting/network changes.
The user confirmed removal of the diagnostic and temporary debug settings.

- HTTPS-only and minimum TLS 1.2 for both app/SCM confirmed in the portal;
  an external HTTP request returned a 301 HTTPS redirect.
- Keep `ReverseProxy__Enabled=true` and
  `ReverseProxy__KnownProxies__0=169.254.130.1` for the verified deployment.
  Remove `ReverseProxy__Diagnostics` and the temporary HttpOverrides debug setting.
  Never use the automatic
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED`/`DOTNET_FORWARDEDHEADERS_ENABLED` shortcut.
- For future releases, run **Publish containers** on `main` and verify its
  production deployment job. The initial automated release is verified above.
- Update the explicit grant script whenever a migration adds a runtime table.
- Repeat live verification after updates.
- Use `/health/live` for recurring probes; repeated database readiness probes can
  prevent Neon from suspending. The app rejects unresolved JWT Key Vault references
  at startup instead of treating the reference text as a signing secret.

References: [GHCR visibility and authentication](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry),
[App Service Key Vault references](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references).
