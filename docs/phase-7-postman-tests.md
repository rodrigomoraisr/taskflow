# Phase 7 Postman verification

Run this plan with disposable Tasks in active Projects. Automated regression tests remain deferred to Phase 8.

## Collection variables

```text
baseUrl
workspaceAId
workspaceBId
projectAId
projectBId
ownerId        ownerToken
adminId        adminToken
memberAId      memberAToken
memberBId      memberBToken
viewerId       viewerToken
outsiderId     outsiderToken
```

Task statuses:

```text
Todo       = 1
InProgress = 2
Done       = 3
```

Store generated IDs at collection scope:

```javascript
const body = pm.response.json();
pm.collectionVariables.set("todoTaskId", body.id);
```

Read expected values with `pm.collectionVariables.get(...)`, not environment variables.

## Setup

1. Use the Phase 5 endpoints to place Owner, Admin, Member A, Member B, and Viewer in Workspace A.
2. Keep Outsider outside Workspace A.
3. Owner must also own Workspace B.
4. Create active Projects A and B in their respective workspaces.
5. Create a fresh Task for each independent transition test so tests do not depend on resetting database state.

## Generic update separation

Update a Task through the existing detail endpoint:

```http
PUT {{baseUrl}}/api/workspaces/{{workspaceAId}}/tasks/{{detailTaskId}}
Authorization: Bearer {{memberAToken}}
Content-Type: application/json

{
  "title": "Updated details only",
  "description": "Workflow fields must not change here",
  "priority": 3,
  "dueDate": null,
  "status": 3,
  "assigneeUserId": "{{memberBId}}"
}
```

Expected: `204`. ASP.NET ignores the two fields that are no longer part of `UpdateTaskRequest`.

GET the Task and assert that descriptive fields changed but workflow state did not:

```javascript
const body = pm.response.json();
pm.expect(body.title).to.eql("Updated details only");
pm.expect(body.status).to.eql(1);
pm.expect(body.assigneeUserId).to.eql(null);
```

Also verify Viewer receives `403` from Task create, update, and delete while retaining `200` read access.

## Status endpoints

```text
POST /api/workspaces/{workspaceId}/tasks/{taskId}/start
POST /api/workspaces/{workspaceId}/tasks/{taskId}/complete
POST /api/workspaces/{workspaceId}/tasks/{taskId}/reopen
```

Successful commands return `204`. GET the Task afterward and assert its numeric status.

### Valid transitions

| Initial state | Command | Final state | Expected |
|---|---|---|---|
| Todo | `start` | InProgress | `204`, status `2` |
| Todo | `complete` | Done | `204`, status `3` |
| InProgress | `complete` | Done | `204`, status `3` |
| Done | `reopen` | Todo | `204`, status `1` |

Example assertion:

```javascript
pm.expect(pm.response.code).to.eql(200);
pm.expect(pm.response.json().status).to.eql(2);
```

### Invalid transitions

Use separate Tasks or continue from the known state shown:

| Current state | Command | Expected |
|---|---|---|
| InProgress | `start` | `409` |
| Done | `start` | `409` |
| Done | `complete` | `409` |
| Todo | `reopen` | `409` |
| InProgress | `reopen` | `409` |

After every `409`, GET the Task and confirm the original status is unchanged.

### Status authorization

Run a valid transition against a fresh Todo Task for each role:

| Token | Expected |
|---|---|
| Owner | `204` |
| Admin | `204` |
| Member | `204` |
| Viewer | `403` |
| Outsider | `404` |
| No token | `401` |

## Assignment endpoint

```http
PUT {{baseUrl}}/api/workspaces/{{workspaceAId}}/tasks/{{assignmentTaskId}}/assignee
Authorization: Bearer {{ownerToken}}
Content-Type: application/json

{
  "userId": "{{memberAId}}"
}
```

Expected: `204`.

GET the Task and verify:

```javascript
const body = pm.response.json();
pm.expect(body.assigneeUserId)
  .to.eql(pm.collectionVariables.get("memberAId"));
```

### Assignment matrix

Use fresh unassigned Tasks where the case requires one.

| Case | Expected |
|---|---|
| Owner assigns Member A | `204` |
| Admin assigns Member A | `204` |
| Owner reassigns Member A to Member B | `204` |
| Admin reassigns Member A to Member B | `204` |
| Member A self-assigns unassigned Task | `204` |
| Member A self-assigns Task assigned to Member B | `403` |
| Member A self-assigns Task already assigned to Member A | `403` |
| Member A assigns Member B | `403` |
| Viewer self-assigns | `403` |
| Outsider assigns | `404` |
| Empty/all-zero user ID by Owner | `400` |

For every rejected operation, GET the Task and verify its assignee did not change.

## Active membership validation

| Requested assignee | Expected |
|---|---|
| Active Workspace A member | `204` |
| Registered user outside Workspace A | `404` |
| Removed Workspace A member | `404` |
| Random user ID | `404` |
| User who belongs only to Workspace B | `404` |

Historical behavior:

1. Assign Member A to a Task.
2. Remove Member A from Workspace A using Phase 5.
3. GET the Task as Owner.
4. `assigneeUserId` must still equal `memberAId`.
5. Attempting to newly assign Member A to another Task must return `404`.

Restore Member A afterward for the remaining tests.

## Unassignment endpoint

```http
DELETE {{baseUrl}}/api/workspaces/{{workspaceAId}}/tasks/{{assignmentTaskId}}/assignee
Authorization: Bearer {{ownerToken}}
```

| Case | Expected |
|---|---|
| Owner unassigns assigned Task | `204`; assignee becomes `null` |
| Admin unassigns assigned Task | `204`; assignee becomes `null` |
| Owner/Admin unassigns already-unassigned Task | `204` idempotently |
| Member unassigns | `403` |
| Viewer unassigns | `403` |
| Outsider unassigns | `404` |

## Tenant isolation

### Caller lacks membership

Call all five workflow endpoints in Workspace A with `outsiderToken`. Every request must return `404`.

### Caller belongs to both workspaces

Owner belongs to A and B. Use a Workspace A Task ID under a Workspace B route:

```text
POST   /api/workspaces/{workspaceBId}/tasks/{workspaceATaskId}/start
POST   /api/workspaces/{workspaceBId}/tasks/{workspaceATaskId}/complete
POST   /api/workspaces/{workspaceBId}/tasks/{workspaceATaskId}/reopen
PUT    /api/workspaces/{workspaceBId}/tasks/{workspaceATaskId}/assignee
DELETE /api/workspaces/{workspaceBId}/tasks/{workspaceATaskId}/assignee
```

Every request must return `404`, and the Workspace A Task must remain unchanged.

## Archived Project boundary

1. Create a disposable Project and Task.
2. Delete the Project.
3. Call `start`, `complete`, `reopen`, `assign`, and `unassign` against its Task.

Every workflow endpoint must return `404`. This confirms all workflow operations reuse the Phase 6 active-Task query and cannot operate on archived Project Tasks.

Also repeat against a soft-deleted Task in an active Project; every command must return `404`.

## Regression pass

Rerun:

1. Registration and login.
2. Workspace listing and tenant isolation.
3. Member add/change/remove/restore and owner invariants.
4. Project CRUD and archive behavior.
5. Task create/list/get/detail-update/delete and pagination.
6. Confirm `assigneeUserId` is now present in both single-Task and Task-list responses.
7. Confirm Tasks under archived Projects remain absent from all normal Task operations.

## Phase 8 follow-up

Add optimistic concurrency protection for competing Member self-assignment requests. Phase 7 validates the rule against the state loaded by each request but intentionally does not add a concurrency token yet.
