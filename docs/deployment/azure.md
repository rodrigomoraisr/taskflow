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
This workflow publishes images only; it does not deploy or migrate production.

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
identity and its secret permissions remain separate.

Run **Actions → Verify Azure access → Run workflow → main** to test actual OIDC
sign-in and reads of the Web App and migration secret. The workflow suppresses
secret output and does not change the app or database. Success proves those reads,
not a deployment or migration. A serialized release workflow remains to be added.

## Remaining guided setup

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
- Verify GitHub-to-Azure federated identity and add a serialized deployment workflow.
- Automate future owner migrations and explicit runtime grants in the pipeline.
- Pin deployment to the selected image digest and repeat verification after updates.
- Use `/health/live` for recurring probes; repeated database readiness probes can
  prevent Neon from suspending. The app rejects unresolved JWT Key Vault references
  at startup instead of treating the reference text as a signing secret.

References: [GHCR visibility and authentication](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry),
[App Service Key Vault references](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references).
