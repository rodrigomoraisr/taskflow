# Phase 6 Postman verification

Run this plan with disposable data after completing the Phase 5 setup. Use one `Owner`, one `Admin`, one `Member`, and one `Viewer` in Workspace A. The Owner should also own Workspace B.

## Variables

```text
baseUrl
ownerToken
adminToken
memberToken
viewerToken
outsiderToken
workspaceAId
workspaceBId
projectAId
projectBId
archiveProjectId
archiveTaskId
```

Project status request values:

```text
Active    = 1
OnHold    = 2
Completed = 3
```

Every protected request uses `Authorization: Bearer {{token}}`.

## Setup

1. Register and log in five users: Owner, Admin, Member, Viewer, and Outsider.
2. Add Admin, Member, and Viewer to Workspace A using the Phase 5 endpoints.
3. Assign their corresponding roles.
4. Create Workspace B as Owner. The identity-only Owner token works in both workspaces.

## Create projects

Request:

```http
POST {{baseUrl}}/api/workspaces/{{workspaceAId}}/projects
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "name": "Project A",
  "description": "Workspace A project"
}
```

Store the returned `id` as `projectAId`. Create another project in Workspace B and store it as `projectBId`.

| Case | Token | Expected |
|---|---|---|
| Owner creates project | Owner | `201`; response contains ID and `Location` points to the project |
| Admin creates project | Admin | `201` |
| Member creates project | Member | `403` |
| Viewer creates project | Viewer | `403` |
| Outsider creates project | Outsider | `404` |
| Missing token | none | `401` |
| Empty/whitespace name | Owner | `400` |
| Name longer than 200 characters | Owner | `400` |
| Description longer than 2000 characters | Owner | `400` |

New projects must be returned with status `Active`.

## List and view projects

```http
GET {{baseUrl}}/api/workspaces/{{workspaceAId}}/projects
GET {{baseUrl}}/api/workspaces/{{workspaceAId}}/projects/{{projectAId}}
```

| Case | Token | Expected |
|---|---|---|
| Owner lists/views | Owner | `200` |
| Admin lists/views | Admin | `200` |
| Member lists/views | Member | `200` |
| Viewer lists/views | Viewer | `200` |
| Outsider lists/views | Outsider | `404` |
| Unknown Project ID in accessible workspace | any active member | `404` |

For every returned project, assert:

```javascript
pm.expect(pm.response.json().workspaceId)
  .to.eql(pm.collectionVariables.get("workspaceAId"));
```

For the list response, perform that assertion for every item and verify deleted projects are absent.

## Update projects

```http
PUT {{baseUrl}}/api/workspaces/{{workspaceAId}}/projects/{{projectAId}}
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "name": "Project A Updated",
  "description": "Updated project description",
  "status": 2
}
```

| Case | Token | Expected |
|---|---|---|
| Owner updates details/status | Owner | `204` |
| Admin updates details/status | Admin | `204` |
| Member updates | Member | `403` |
| Viewer updates | Viewer | `403` |
| Outsider updates | Outsider | `404` |
| Unknown project in accessible workspace | Owner | `404` |
| Status `999` | Owner | `400` |
| Missing status | Owner | `400` |
| Empty name | Owner | `400` |

After successful updates, GET the project and verify its name, description, status, and `updatedAt`.

## Tenant isolation

There are two distinct boundaries to verify.

### Caller lacks workspace membership

Use `outsiderToken` against every Workspace A Project endpoint. Each request must return `404`, and no data may change.

### Caller belongs to both workspaces but uses the wrong route

The Owner belongs to Workspaces A and B. Request Project A through Workspace B:

```http
GET {{baseUrl}}/api/workspaces/{{workspaceBId}}/projects/{{projectAId}}
Authorization: Bearer {{ownerToken}}
```

Expected: `404`. Repeat for PUT and DELETE; both must return `404`. Project A must remain unchanged in Workspace A.

## Task-to-Project integrity

Create a Task under Project A:

```http
POST {{baseUrl}}/api/workspaces/{{workspaceAId}}/tasks
Authorization: Bearer {{memberToken}}
Content-Type: application/json

{
  "title": "Project A task",
  "description": "Valid tenant-scoped relationship",
  "projectId": "{{projectAId}}",
  "priority": 2,
  "dueDate": null
}
```

Expected: `201`.

Then run:

| Case | Workspace route | Project ID | Expected |
|---|---|---|---|
| Valid same-workspace project | Workspace A | Project A | `201` |
| Cross-workspace project | Workspace A | Project B | `404` |
| Random project ID | Workspace A | random GUID | `404` |
| Empty project ID | Workspace A | all-zero GUID | `404` |
| Caller lacks Workspace A membership | Workspace A | Project A | `404` |

After the rejected cross-workspace request, list Workspace A Tasks and confirm no Task was created with `projectBId`.

## Project archival behavior

1. Create a disposable project and store it as `archiveProjectId`.
2. Create a Task under it and store it as `archiveTaskId`.
3. Confirm both are visible before deletion.

Delete the project:

```http
DELETE {{baseUrl}}/api/workspaces/{{workspaceAId}}/projects/{{archiveProjectId}}
Authorization: Bearer {{ownerToken}}
```

Expected: `204` even though the Project has a Task, because deletion is soft and the FK uses `RESTRICT` only for physical deletion.

Verify all of the following:

| Verification | Expected |
|---|---|
| GET deleted project | `404` |
| Project list | deleted project absent |
| GET archived Task | `404` |
| Task list | archived Task absent |
| Task `totalCount` | excludes archived Task |
| Create Task using deleted Project ID | `404` |
| Update archived Task | `404` |
| Delete archived Task | `404` |
| Delete project again | `404` because normal repository reads exclude deleted projects |

Also verify an unrelated Project and its Tasks remain visible.

## Project deletion permissions

Use disposable projects for each successful deletion.

| Case | Token | Expected |
|---|---|---|
| Owner deletes | Owner | `204` |
| Admin deletes | Admin | `204` |
| Member deletes | Member | `403` |
| Viewer deletes | Viewer | `403` |
| Outsider deletes | Outsider | `404` |

## Regression checks

After completing Phase 6 tests, rerun these existing checks:

1. User registration and login still work.
2. Workspace listing remains tenant-scoped.
3. Phase 5 member list/add/change/remove operations still work.
4. Tasks in active Projects can still be created, listed, retrieved, updated, and soft-deleted.
5. Task pagination counts only Tasks whose Task and Project are both active.
