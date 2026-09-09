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
The runtime role still needs table permissions after the first migration.

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

## Remaining guided setup

- Create the Linux B1 App Service and configure the main container on port 8080.
- Enable managed identity and grant Key Vault secret read access scoped to the
  JWT and runtime secrets. Keep the migration secret restricted to deployment.
- Configure `Jwt__Key` and `ConnectionStrings__DefaultConnection` with versionless
  references to the JWT and runtime secrets. Never give the API the migration secret.
- Configure HTTPS and trusted proxy handling before accepting public traffic.
- Set up GitHub-to-Azure federated identity and a serialized deployment workflow.
- Run migrations with the owner connection, grant the runtime role only required
  table/sequence privileges, and verify those privileges before starting the API.
- Deploy the selected digest and verify `/health/ready` plus an authenticated flow.
- Use `/health/live` for recurring probes; repeated database readiness probes can
  prevent Neon from suspending. The app rejects unresolved JWT Key Vault references
  at startup instead of treating the reference text as a signing secret.

References: [GHCR visibility and authentication](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry),
[App Service Key Vault references](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references).
