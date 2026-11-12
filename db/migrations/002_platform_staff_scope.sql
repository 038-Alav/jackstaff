-- JACKSTAFF v0.1 - separate platform staff from customer tenant memberships.
-- Safe to apply to an existing v0.1 demo database; operational tenant IDs remain stable.

BEGIN;

-- Replace demo labels/slugs without changing tenant IDs or their operational data.
UPDATE tenants
SET name = 'Metro', slug = 'metro'
WHERE id = '10000000-0000-4000-8000-000000000001';

UPDATE tenants
SET name = 'Empresa Y', slug = 'empresa-y'
WHERE id = '10000000-0000-4000-8000-000000000002';

UPDATE tenants
SET name = 'Empresa X', slug = 'empresa-x'
WHERE id = '10000000-0000-4000-8000-000000000003';

-- Platform staff is a platform-level membership, not a fourth tenant.
INSERT INTO roles (id, name, description)
VALUES (
    '21000000-0000-4000-8000-000000000005',
    'platform_support',
    'Personal interno de JACKSTAFF con acceso transversal autorizado a tickets'
)
ON CONFLICT (name) DO NOTHING;

INSERT INTO permissions (id, code, description) VALUES
('22000000-0000-4000-8000-000000000011', 'platform.tickets.read', 'Consultar tickets de todos los tenants autorizados'),
('22000000-0000-4000-8000-000000000012', 'platform.tickets.manage', 'Gestionar tickets de todos los tenants autorizados')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'platform_support'
  AND p.code IN ('platform.tickets.read', 'platform.tickets.manage')
ON CONFLICT DO NOTHING;

CREATE TABLE IF NOT EXISTS platform_memberships (
    user_id     uuid PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    role_id     uuid NOT NULL REFERENCES roles(id),
    is_active   boolean NOT NULL DEFAULT TRUE,
    created_at  timestamptz NOT NULL DEFAULT now()
);

-- The seeded support identity is promoted from a Metro tenant member to platform staff.
INSERT INTO platform_memberships (user_id, role_id, is_active)
SELECT u.id, r.id, TRUE
FROM users u
CROSS JOIN roles r
WHERE lower(u.email) = 'support@jackstaff.demo'
  AND r.name = 'platform_support'
ON CONFLICT (user_id) DO UPDATE
SET role_id = EXCLUDED.role_id, is_active = TRUE;

GRANT SELECT ON platform_memberships TO jackstaff_app;

-- A platform staff member can be an actor on a tenant-owned record without becoming
-- a tenant member. The tenant_id remains on every event and continues to identify ownership.
ALTER TABLE tickets
    DROP CONSTRAINT IF EXISTS tickets_tenant_id_created_by_fkey;
ALTER TABLE tickets
    ADD CONSTRAINT fk_tickets_created_by_user
    FOREIGN KEY (created_by) REFERENCES users(id);

ALTER TABLE radio_group_assignments
    DROP CONSTRAINT IF EXISTS radio_group_assignments_tenant_id_assigned_by_fkey;
ALTER TABLE radio_group_assignments
    ADD CONSTRAINT fk_radio_group_assignments_assigned_by_user
    FOREIGN KEY (assigned_by) REFERENCES users(id);

ALTER TABLE ticket_history
    DROP CONSTRAINT IF EXISTS ticket_history_tenant_id_actor_id_fkey;
ALTER TABLE ticket_history
    ADD CONSTRAINT fk_ticket_history_actor_user
    FOREIGN KEY (actor_id) REFERENCES users(id);

ALTER TABLE ticket_comments
    DROP CONSTRAINT IF EXISTS ticket_comments_tenant_id_author_id_fkey;
ALTER TABLE ticket_comments
    ADD CONSTRAINT fk_ticket_comments_author_user
    FOREIGN KEY (author_id) REFERENCES users(id);

ALTER TABLE audit_logs
    DROP CONSTRAINT IF EXISTS audit_logs_tenant_id_actor_id_fkey;
ALTER TABLE audit_logs
    ADD CONSTRAINT fk_audit_logs_actor_user
    FOREIGN KEY (actor_id) REFERENCES users(id);

DELETE FROM tenant_memberships
WHERE user_id = (SELECT id FROM users WHERE lower(email) = 'support@jackstaff.demo');

-- Only the workflow-owned fields may be updated through the application role.
REVOKE UPDATE ON tickets FROM jackstaff_app;
GRANT UPDATE (status, updated_at, resolution_note) ON tickets TO jackstaff_app;

REVOKE UPDATE, DELETE ON radio_group_assignments FROM jackstaff_app;
GRANT UPDATE (ended_at) ON radio_group_assignments TO jackstaff_app;

-- Explicit read scope for platform staff. Tenant requests remain constrained by
-- the original tenant_isolation policy and the transaction-local tenant_id.
DO $$
DECLARE table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
        'sites','radio_models','operational_groups','radios','radio_group_assignments',
        'tickets','ticket_history','ticket_comments','audit_logs'
    ] LOOP
        EXECUTE format(
            'CREATE POLICY platform_staff_read ON %I FOR SELECT USING (current_setting(''app.is_platform_staff'', true) = ''true'')',
            table_name
        );
    END LOOP;
END
$$;

-- Platform staff may perform ticket workflow updates, but not rewrite ticket content.
CREATE POLICY platform_staff_ticket_update ON tickets
    FOR UPDATE
    USING (current_setting('app.is_platform_staff', true) = 'true')
    WITH CHECK (current_setting('app.is_platform_staff', true) = 'true');

-- Resolving an approved group-change ticket closes/creates assignment history.
CREATE POLICY platform_staff_assignment_insert ON radio_group_assignments
    FOR INSERT
    WITH CHECK (current_setting('app.is_platform_staff', true) = 'true');

CREATE POLICY platform_staff_assignment_update ON radio_group_assignments
    FOR UPDATE
    USING (current_setting('app.is_platform_staff', true) = 'true')
    WITH CHECK (current_setting('app.is_platform_staff', true) = 'true');

CREATE POLICY platform_staff_history_insert ON ticket_history
    FOR INSERT
    WITH CHECK (current_setting('app.is_platform_staff', true) = 'true');

CREATE POLICY platform_staff_comments_insert ON ticket_comments
    FOR INSERT
    WITH CHECK (current_setting('app.is_platform_staff', true) = 'true');

CREATE POLICY platform_staff_audit_insert ON audit_logs
    FOR INSERT
    WITH CHECK (current_setting('app.is_platform_staff', true) = 'true');

INSERT INTO schema_migrations (version) VALUES ('002_platform_staff_scope');

COMMIT;
