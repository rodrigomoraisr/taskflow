# Trying TaskFlow

Use the README's local SDK setup for `/openapi/v1.json`. Import that JSON into an
OpenAPI-compatible client, or open [TaskFlow.Api.http](../src/TaskFlow.Api/TaskFlow.Api.http)
in a client that supports HTTP request files. There is no built-in Swagger UI.
The full local Docker stack uses port 8080 and Production mode; it deliberately
does not publish OpenAPI. The SDK launch profile uses HTTP port 5056.

## A short reviewer walkthrough

Run individual requests in the collection, following this order. Example IDs are
placeholders and sample passwords are for local demonstration only.

1. **Register:** `POST /auth/register` with `email` and `password`. The 201 body
   includes `id`, `workspaceId` and `workspaceName`. Registration creates a default
   workspace with you as Owner. It does not log you in. Its Location header does
   not imply a public GET /users endpoint.
2. **Log in:** `POST /auth/login`. Copy `token` into `accessToken`, and
   `refreshToken` into the collection's matching variable. Set `workspaceId` from
   registration or `GET /api/workspaces`.
3. **Create a project:** use the workspace's POST `/projects` request, then copy
   its returned `id` into `projectId`.
4. **Create a task:** use POST `/tasks` with that `projectId`. Copy its `id` into
   `taskId`. Read it, start it, complete it, then reopen it. Completion also accepts
   a Todo task directly; reopening transitions Done to Todo.
5. **Discuss and inspect history:** create a comment and copy its `id` into
   `commentId`. Read/edit it, and inspect GET `/api/workspaces/{workspaceId}/activity`.
6. **Try querying:** GET the task collection with
   `?page=1&pageSize=20&status=1&sortBy=dueDate&sortDirection=asc`. Remove filters
   when they intentionally exclude your task. The response contains pagination data.
7. **Try another role:** register a second account, then as Owner add its email
   through POST `/members`. New memberships are Member. Keep separate tokens for
   each account. Viewer cannot write; Member cannot manage projects or members.
8. **Rotate and log out:** POST `/auth/refresh` with the current refresh token,
   replace both tokens, then POST `/auth/logout` with the newly returned refresh
   token. Do not replay the old one; reuse revokes the session.

The collection also contains deletion examples. Run them after reads and updates:
deleted tasks/projects are hidden from ordinary reads. A successful 204 has no body.

## Wire conventions

- Send `Authorization: Bearer <access-token>` on `/api/...` requests. Auth endpoints
  and health probes do not require a bearer token. Access tokens last at most 15
  minutes. Logout does not invalidate an already-issued access JWT immediately.
- JSON uses camelCase. Enum-typed request fields use numbers. Priority: Low=1, Medium=2, High=3,
  Critical=4. Task status: Todo=1, InProgress=2, Done=3. Project status: Active=1,
  OnHold=2, Completed=3. Roles: Owner=1, Admin=2, Member=3, Viewer=4. Task status and
  priority responses are numeric; project-status and membership-role responses
  are strings such as `"Active"` and `"Owner"`. The generated schemas show each field's type.
- Send UTC timestamps with `Z` for task due dates. Task-filter bounds accept
  offsets and normalize to UTC. Bounds are inclusive; null due dates sort last.
- Workspace membership is resolved from the route and authenticated user for each
  request. Selecting a workspace ID does not grant access to it.
- Task lists have page/count metadata; comments and activity return paginated
  arrays. All three cap pageSize at 100. Project and workspace lists are not paginated.
- Optional `X-Correlation-ID` is bounded/validated; responses include the effective
  value. Do not put credentials or personal data in correlation IDs.

## Errors and limits

Errors use `application/problem+json`. For example, an unknown route can return:

```json
{
  "type": "about:blank",
  "title": "Not Found",
  "status": 404,
  "correlationId": "review-example"
}
```

Additional fields, including `detail` and a validation `errors` map, depend on
the failure. Use status and context to handle errors; wording is not a stable code.
401 means authentication failed. 403 means the member's role or ownership rules
refuse the action. 404 hides unavailable/deleted resources and inaccessible
workspaces. 409 includes invalid transitions, duplicates, last-owner restrictions
and overlapping task/project writes. There is no ETag/If-Match contract.

429 means the per-process request budget is exhausted; honor `Retry-After` when
present. `/auth/*` also has its own budget. The current defaults are configured in
`appsettings.json`; these are not distributed limits across multiple instances.
Health probes are exempt. `/health/live` does not query PostgreSQL;
`/health/ready` tests connectivity and can return plain-text 503 `Unhealthy`.

## Documentation boundaries

Controller metadata supplies endpoint descriptions, success/error status codes and
request examples. A document transformer adds bearer security, request-body
examples and health middleware contracts. Tests verify generated endpoint coverage,
authentication metadata, response shapes and example DTO validation. Refer to
[architecture decisions](adr/README.md) for tradeoffs and the
[deployment runbook](deployment/azure.md) for production operations.
