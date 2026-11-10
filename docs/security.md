# Security model and v0.1 limitations

## Implemented in the demo

- JWT access token validation with issuer, audience, signature and expiry checks.
- Tenant switching checks that the user has an active membership in the destination tenant. Every authenticated request also revalidates the user, tenant and membership state so revocation takes effect immediately.
- Permission claims are derived from database role-permission assignments when a token is issued.
- API routes require named permissions.
- Each authenticated request uses a transaction-local tenant context on a dedicated request-scoped PostgreSQL session.
- PostgreSQL RLS is enabled and forced on all operational tables.
- The application database role is separate from the PostgreSQL owner and has `NOBYPASSRLS`.
- Operational queries include explicit tenant predicates in addition to RLS.
- Composite foreign keys prevent records from referencing a site, model, group, radio or membership in another tenant.
- Ticket changes and audit/history writes share one transaction. Ticket comments, status history and audit records are append-only for the application role at the PostgreSQL privilege layer.
- Passwords are stored as PBKDF2-SHA256 hashes with per-hash salt and constant-time comparison.

## Not production-ready yet

- Demo JWT is kept in browser `sessionStorage`; replace with a secure cookie-based session design before production.
- No MFA, password reset, account lockout, email verification or authentication rate limiting.
- Development secrets and demo users are checked into the local demo configuration/SQL seed.
- No production secret manager, key rotation or certificate automation.
- No security audit, penetration test or formal threat model has been performed.
- OpenAPI is exposed in Development mode.
- No tenant provisioning workflow, customer support impersonation controls or platform administration portal.
- No retention/purge jobs for audit/history data.

La configuración incluida es únicamente para desarrollo local. No publiques esta configuración ni las credenciales demo en internet; prepara secretos, TLS, CORS y controles operacionales antes de cualquier despliegue.
