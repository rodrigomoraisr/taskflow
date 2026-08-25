# Phase 5 Postman verification

Run these requests against a disposable workspace. Use four registered users: `owner`, `admin`, `member`, and `secondOwner`.

## Variables

```text
baseUrl
workspaceId
ownerId       ownerToken
adminId       adminToken
memberId      memberToken
secondOwnerId secondOwnerToken
```

Workspace role request values:

```text
Owner  = 1
Admin  = 2
Member = 3
Viewer = 4
```

Every protected request uses `Authorization: Bearer {{token}}`.

## Setup

1. Register and log in all four users.
2. Use the workspace created for `owner` as `workspaceId`.
3. Keep each registration response's user ID and each login response's token.

## List members

Request:

```http
GET {{baseUrl}}/api/workspaces/{{workspaceId}}/members
```

| Case | Token | Expected |
|---|---|---|
| Owner lists members | `ownerToken` | `200`; initial list contains only Owner |
| User without membership lists members | `memberToken` before being added | `403` |
| No token | none | `401` |

## Add members

Request:

```http
POST {{baseUrl}}/api/workspaces/{{workspaceId}}/members
Content-Type: application/json

{
  "email": "the registered user's email"
}
```

| Case | Actor | Target | Expected |
|---|---|---|---|
| Add registered user | Owner | admin | `201`; returned role is `Member` |
| Add registered user | Owner | member | `201`; returned role is `Member` |
| Add duplicate active member | Owner | member | `409` |
| Add unknown email | Owner | unknown | `404` |
| Add invalid email | Owner | invalid address | `400` |
| Add member | Member | any registered user | `403` |

Verify normalization by sending an existing user's email with uppercase letters. It must resolve to that user.

## Change roles

Request:

```http
PATCH {{baseUrl}}/api/workspaces/{{workspaceId}}/members/{{targetUserId}}/role
Content-Type: application/json

{
  "role": 2
}
```

Run in this order:

| Case | Actor | Target/current role | Requested role | Expected |
|---|---|---|---|---|
| Promote Member to Admin | Owner | admin / Member | Admin | `204` |
| Change Member to Viewer | Admin | member / Member | Viewer | `204` |
| Change Viewer to Member | Admin | member / Viewer | Member | `204` |
| Promote Member to Admin | Admin | member / Member | Admin | `204` |
| Change Admin | Admin | member / Admin | Member | `403` |
| Change Admin | Owner | member / Admin | Member | `204` |
| Grant Owner | Admin | secondOwner / Member | Owner | `403` |
| Grant Owner | Owner | secondOwner / Member | Owner | `204` |
| Invalid enum value | Owner | member | `999` | `400` |
| Missing/deleted target | Owner | random/deleted user ID | Member | `404` |
| Change role | Member | another Member | Viewer | `403` |

After every successful change, list members and verify the persisted role.

## Owner invariants

Run the first two checks before promoting `secondOwner`, while the original owner is the only Owner:

| Case | Expected |
|---|---|
| Sole Owner changes their own role to Admin | `409` |
| Sole Owner removes themselves | `409` |

Then add `secondOwner`, promote them to Owner, and run:

| Case | Expected |
|---|---|
| Original Owner changes their own role to Admin | `204` |
| Second Owner promotes original user back to Owner | `204` |
| Second Owner removes original Owner while two Owners exist | `204` |
| Now-sole second Owner removes themselves | `409` |

At every step, `GET /members` must show at least one active Owner.

## Remove and restore

Request:

```http
DELETE {{baseUrl}}/api/workspaces/{{workspaceId}}/members/{{targetUserId}}
```

| Case | Actor | Target | Expected |
|---|---|---|---|
| Remove Member | Admin | Member | `204` |
| List after removal | Owner | removed Member | `200`; target absent |
| Removed user accesses workspace/tasks | removed user's token | any workspace route | `403` immediately |
| Remove missing/already removed member | Owner | removed Member | `404` |
| Re-add removed member by email | Admin | removed Member | `201`; restored as `Member` |
| Remove Admin | Admin | Admin | `403` |
| Remove Admin | Owner | Admin | `204` |
| Remove Owner | Admin | Owner | `403` |
| Remove Owner while another Owner exists | Owner | Owner | `204` |
| Remove last Owner | Owner | self | `409` |
| Remove member | Member | another Member | `403` |

After restoration, verify that the same user appears once, has role `Member`, and can immediately access workspace-scoped routes with their existing identity-only JWT.

## Tenant isolation

Using a token belonging only to a different workspace, call all four member endpoints with `workspaceId`:

```text
GET    /api/workspaces/{workspaceId}/members
POST   /api/workspaces/{workspaceId}/members
PATCH  /api/workspaces/{workspaceId}/members/{userId}/role
DELETE /api/workspaces/{workspaceId}/members/{userId}
```

Every request must return `403`, and no membership data may change.
