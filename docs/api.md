# API outline — v0.1

Base path: `/api`. All authenticated endpoints require a bearer access token. Customer routes execute inside the current JWT tenant context. Platform support routes require a platform staff token and use a distinct request context; they never accept a client-supplied tenant ID.

| Method | Endpoint | Permission | Purpose |
|---|---|---|---|
| GET | `/health` | Anonymous | API health |
| GET | `/auth/workspaces` | Anonymous | Demo login selector; returns active tenant workspaces and whether platform support is available |
| POST | `/auth/login` | Anonymous | Authenticate into a tenant or the platform staff workspace |
| POST | `/auth/switch-tenant` | Authenticated | Validate membership and issue token for another tenant |
| GET | `/auth/me` | Authenticated | Current identity, active tenant and memberships |
| GET | `/dashboard/summary` | `dashboard.read` | Tenant operational summary and recent tickets |
| GET / POST | `/sites` | `sites.read` / `sites.write` | List and create installations |
| GET | `/radio-models` | `radios.read` | List tenant radio models |
| GET / POST | `/radios` | `radios.read` / `radios.write` | List and create radios |
| GET / POST | `/groups` | `groups.read` / `groups.write` | List and create operational groups |
| GET / POST | `/tickets` | `tickets.read` / `tickets.write` | List and create tickets within the active tenant |
| PATCH | `/tickets/{id}/status` | `tickets.manage` | Advance ticket workflow |
| GET | `/tickets/{id}/history` | `tickets.read` | Read ticket status transitions and actors |
| GET / POST | `/tickets/{id}/comments` | `tickets.read` / `tickets.write` | Read and append ticket follow-up comments within the active tenant |
| GET | `/support/tickets` | `platform.tickets.read` | Transversal ticket inbox for platform staff |
| GET | `/support/tickets/{id}/comments` | `platform.tickets.read` | Read comments across customer tenants |
| GET | `/support/tickets/{id}/history` | `platform.tickets.read` | Read ticket history across customer tenants |
| POST | `/support/tickets/{id}/comments` | `platform.tickets.manage` | Add a platform-staff comment to a customer ticket |
| PATCH | `/support/tickets/{id}/status` | `platform.tickets.manage` | Manage a ticket and execute its workflow across customer tenants |

## Login request

```json
{
  "email": "admin@jackstaff.demo",
  "password": "JackstaffDemo!2026",
  "workspaceType": "tenant",
  "tenantSlug": "metro"
}
```

Platform staff login uses the same endpoint, with a platform workspace and no tenant slug:

```json
{
  "email": "support@jackstaff.demo",
  "password": "JackstaffDemo!2026",
  "workspaceType": "platform_staff",
  "tenantSlug": null
}
```

A successful response contains `accessToken`, `expiresAtUtc`, `workspaceType`, `user`, `activeTenant` and `tenants`. For `platform_staff`, `activeTenant` is `null` and `tenants` is empty.

## Create a radio

```json
{
  "radioModelId": "<model-id-from-GET-radio-models>",
  "siteId": "<site-id-from-GET-sites>",
  "assetTag": "RAD-1001",
  "serialNumber": "SERIAL-1001",
  "alias": "Supervisor 01",
  "initialStatus": "offline",
  "initialGroupId": null
}
```

## Create a group-change ticket

```json
{
  "title": "Mover radio al grupo de mantenimiento",
  "description": "Cambio solicitado por el jefe de turno.",
  "type": "group_change",
  "priority": "normal",
  "radioId": "<radio-id>",
  "siteId": "<site-id>",
  "requestedGroupId": "<operational-group-id>"
}
```

A group-change ticket must include both `radioId` and `requestedGroupId`. The effective assignment is changed only when the ticket moves from `in_progress` to `resolved`.

## Ticket status transitions

- `open` → `in_review` or `rejected`
- `in_review` → `approved`, `in_progress`, `rejected` or `open`
- `approved` → `in_progress` or `rejected`
- `in_progress` → `resolved` or `rejected`
- `resolved` and `rejected` are terminal in v0.1

Invalid transitions return HTTP 400; records not belonging to the active tenant are not visible through the tenant-scoped API. Platform support routes can see tickets across tenants only with platform-specific permissions; each ticket response includes its owning `tenantId`, `tenantName` and `tenantSlug` so support can identify the customer context.
