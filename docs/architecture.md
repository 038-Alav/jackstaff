# JACKSTAFF — Architecture baseline (v0.1)

## Product boundary

JACKSTAFF is a vendor-independent, multi-tenant Fleet & Communications Operations Platform. A tenant is a customer organization. Sites, radios, operational groups, assignments, tickets, history and audit records always belong to exactly one tenant.

Customer-specific terminology is configuration/data, not a new branch in the product domain. `site_type` is intentionally extensible (station, branch, warehouse, plant, control center, yard, and so on).

## Modular monolith

The API is one deployable ASP.NET Core application. Modules own their HTTP routes, request/response models and business workflows:

- `Auth`: users, tenant memberships, platform memberships, roles, permissions, token issuance and tenant switching.
- `Dashboard`: tenant-scoped operational read models.
- `Inventory`: sites, radio models, radios, groups and effective assignments.
- `Tickets`: service desk, state transition rules and group-change orchestration.
- `Infrastructure`: database session, request tenant context, password hashing and JWT.

Shared infrastructure must not become a generic dumping ground for domain behavior. Modules can share primitives and DB session infrastructure; a module should not reach into another module's private workflow.

## Tenant identity and database isolation

The active tenant comes from a signed JWT claim, not from a client-provided `tenant_id` field or arbitrary header. Tenant switching performs a membership lookup before issuing a replacement token.

There are two authenticated workspace types, both represented by signed JWTs:

**Customer tenant**

1. JWT validation verifies signature, issuer, audience and expiry.
2. `TenantSessionMiddleware` reads `tenant_id` and `sub` from the validated principal.
3. `TenantDbSession` opens one Npgsql connection and transaction for the request.
4. It sets `app.tenant_id` and `app.is_platform_staff = false` with transaction-local `set_config(..., true)`.
5. The middleware revalidates that the user and tenant are active and that the tenant membership still exists.
6. Tenant module queries use that same connection/transaction and retain explicit `tenant_id` predicates.
7. PostgreSQL RLS enforces the active tenant context on operational tables.

**Platform staff**

1. A platform staff account has a row in `platform_memberships`; it is not inserted into every customer tenant and is not a synthetic tenant.
2. Its signed token carries `workspace_type = platform_staff` and platform permissions, but no `tenant_id` claim.
3. `TenantSessionMiddleware` revalidates the platform membership and opens the request transaction with `app.is_platform_staff = true`.
4. Additional RLS policies allow platform staff to read operational records across tenants and write only the workflow records needed by the support module. Platform-specific API routes also require platform-specific permissions.
5. Support ticket lookups resolve the ticket's own `tenant_id` from the database. Comments, history and audit events retain that tenant ID even when the actor is platform staff.

The application database role is a separate `NOSUPERUSER NOBYPASSRLS` role and is not the owner of operational tables. RLS is both enabled and forced. Global identity catalogue tables (`users`, `tenants`, `roles`, `permissions`, `tenant_memberships`, `platform_memberships`) are not tenant-scoped; membership queries are constrained to the user ID from the signed token. Platform access is expressed through narrowly scoped policies and API permissions, not a PostgreSQL superuser or `BYPASSRLS` account.

## Domain distinctions

- `operational_groups` describe a customer-defined organizational/operational grouping.
- Vendor radio talkgroups/channels are not represented as operational groups. They belong in a future provider-specific configuration module.
- `radio_group_assignments` represent effective group assignment history. At most one current assignment is allowed per radio.
- A ticket's `requested_group_id` is a requested target, not an effective assignment.
- Ticket comments, ticket status history and audit logs are append-only for the application database role.

## Transaction boundaries

The v0.1 service runs each authenticated request in one request-scoped DB transaction, with a tenant or platform access context. Group-change resolution atomically:

- closes the active assignment,
- inserts the new assignment,
- updates ticket status and resolution note,
- writes ticket history,
- writes audit log.

Any database error rolls back the request transaction.

## Provider boundary (next iteration)

Do not place vendor SDK calls in endpoint handlers. Future adapters should implement independent contracts:

```csharp
public interface IRadioProvider
{
    string ProviderKey { get; }
    Task<ProviderRadioResult> ReadRadioAsync(ProviderRadioReference reference, CancellationToken ct);
}

public interface ILocationProvider
{
    string ProviderKey { get; }
    Task<IReadOnlyList<LocationObservation>> GetRecentLocationsAsync(
        ProviderFleetReference fleet, DateTimeOffset since, CancellationToken ct);
}

public interface IGroupManagementProvider
{
    string ProviderKey { get; }
    Task<GroupChangeResult> RequestGroupChangeAsync(
        ProviderRadioReference radio, ProviderGroupReference group, CancellationToken ct);
}

public interface IDeviceEventProvider
{
    string ProviderKey { get; }
    IAsyncEnumerable<NormalizedDeviceEvent> ReadEventsAsync(CancellationToken ct);
}
```

These interfaces are a direction, not implemented integrations. Provider credentials and endpoint configuration must be tenant-scoped and encrypted at rest. Location observations should preserve source, observation time, precision, coordinate reference and type (GPS, indoor zone, dispatcher system, etc.).

## Explicitly deferred

- Real radio/vendor APIs, programming and remote group changes.
- GPS/indoor positioning ingestion and live maps.
- Talkgroups/channels, dispatch, PTT, recordings and messaging.
- Tenant billing, plans, subscription entitlements, white-label branding.
- Full platform administration, customer impersonation and enterprise dedicated database deployment. The v0.1 platform workspace is limited to the transversal ticket inbox and ticket workflow.
- Production identity hardening (HttpOnly cookies, MFA, account recovery, rate limits).
- Background workers and message broker. Add only after measured operational needs justify them.
