# API outline — v0.1

Base path: `/api`. All authenticated endpoints require a bearer access token. Tenant-scoped routes execute inside the current JWT tenant context.

| Method | Endpoint | Permission | Purpose |
|---|---|---|---|
| GET | `/health` | Anonymous | API health |
| POST | `/auth/login` | Anonymous | Authenticate and select initial tenant |
| POST | `/auth/switch-tenant` | Authenticated | Validate membership and issue token for another tenant |
| GET | `/auth/me` | Authenticated | Current identity, active tenant and memberships |
| GET | `/dashboard/summary` | `dashboard.read` | Tenant operational summary and recent tickets |
| GET / POST | `/sites` | `sites.read` / `sites.write` | List and create installations |
| GET | `/radio-models` | `radios.read` | List tenant radio models |
| GET / POST | `/radios` | `radios.read` / `radios.write` | List and create radios |
| GET / POST | `/groups` | `groups.read` / `groups.write` | List and create operational groups |
| GET / POST | `/tickets` | `tickets.read` / `tickets.write` | List and create tickets |
| PATCH | `/tickets/{id}/status` | `tickets.manage` | Advance ticket workflow |
| GET | `/tickets/{id}/history` | `tickets.read` | Read ticket status transitions and actors |
| GET / POST | `/tickets/{id}/comments` | `tickets.read` / `tickets.write` | Read and append ticket follow-up comments |

## Login request

```json
{
  "email": "admin@jackstaff.demo",
  "password": "JackstaffDemo!2026",
  "tenantSlug": "metro-demo"
}
```

A successful response contains `accessToken`, `expiresAtUtc`, `user`, `activeTenant` and `tenants`.

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

Invalid transitions return HTTP 400; records not belonging to the active tenant are not visible through the tenant-scoped API.
