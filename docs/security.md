# Security model and v0.1 limitations

## Implemented in the demo

- JWT access token validation with issuer, audience, signature and expiry checks.
- Tenant switching checks that the user has an active membership in the destination tenant. Every authenticated request revalidates the user, tenant and membership state so revocation takes effect immediately.
- Platform staff use a separate `platform_memberships` relationship; they are not assigned a synthetic tenant and do not receive a `tenant_id` claim.
- Platform login issues a distinct `workspace_type = platform_staff` token with platform-specific ticket permissions. Every request revalidates the platform membership.
- Permission claims are derived from database role-permission assignments when a token is issued.
- API routes require named permissions.
- Each authenticated request uses a transaction-local access context on a dedicated PostgreSQL session: either one customer tenant or the platform staff scope.
- PostgreSQL RLS is enabled and forced on all operational tables.
- The application database role is separate from the PostgreSQL owner and has `NOBYPASSRLS`.
- Customer operational queries include explicit tenant predicates in addition to RLS. Platform support uses separate endpoints and additional RLS policies limited to the data and workflow operations required by the ticket module.
- Platform staff can read cross-tenant tickets and their context, add comments, and advance the ticket workflow. Every affected ticket, history entry, comment and audit record retains the owning `tenant_id`.
- Composite foreign keys keep site, model, group, radio and ticket relationships inside the ticket's tenant. Actor identities reference the global `users` catalogue; application authorization and the transaction context determine whether an actor may act for that tenant.
- Ticket changes and audit/history writes share one transaction. Ticket comments, status history and audit records are append-only for the application role at the PostgreSQL privilege layer.
- Passwords are stored as PBKDF2-SHA256 hashes with per-hash salt and constant-time comparison.

## Not production-ready yet

- Demo JWT is kept in browser `sessionStorage`; replace with a secure cookie-based session design before production.
- No MFA, password reset, account lockout, email verification or authentication rate limiting.
- Development secrets and demo users are checked into the local demo configuration/SQL seed.
- No production secret manager, key rotation or certificate automation.
- No security audit, penetration test or formal threat model has been performed.
- OpenAPI is exposed in Development mode.
- No tenant provisioning workflow, customer support impersonation controls or full platform administration portal. The demo's public workspace-discovery endpoint is for the local login selector only and must be replaced by a tenant-aware identity flow before production.
- No retention/purge jobs for audit/history data.

La configuración incluida es únicamente para desarrollo local. No publiques esta configuración ni las credenciales demo en internet; prepara secretos, TLS, CORS y controles operacionales antes de cualquier despliegue.
