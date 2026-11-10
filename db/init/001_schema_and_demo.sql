-- JACKSTAFF v0.1 bootstrap. Apply once to a fresh local database using a PostgreSQL administrator account.
-- Production deployments must replace the development credentials and seed identities.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'jackstaff_app') THEN
        CREATE ROLE jackstaff_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
    END IF;
END
$$;

CREATE EXTENSION IF NOT EXISTS postgis;

CREATE TABLE IF NOT EXISTS schema_migrations (
    version         varchar(40) PRIMARY KEY,
    applied_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE tenants (
    id              uuid PRIMARY KEY,
    slug            varchar(80) NOT NULL UNIQUE,
    name            varchar(160) NOT NULL,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE users (
    id              uuid PRIMARY KEY,
    email           varchar(254) NOT NULL UNIQUE,
    name            varchar(160) NOT NULL,
    password_hash   text NOT NULL,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE roles (
    id              uuid PRIMARY KEY,
    name            varchar(60) NOT NULL UNIQUE,
    description     varchar(240)
);

CREATE TABLE permissions (
    id              uuid PRIMARY KEY,
    code            varchar(80) NOT NULL UNIQUE,
    description     varchar(240) NOT NULL
);

CREATE TABLE role_permissions (
    role_id         uuid NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id   uuid NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE tenant_memberships (
    tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_id         uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id         uuid NOT NULL REFERENCES roles(id),
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, user_id)
);

CREATE TABLE sites (
    id              uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name            varchar(160) NOT NULL,
    code            varchar(40) NOT NULL,
    site_type       varchar(60) NOT NULL,
    address         varchar(300),
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, code)
);

CREATE TABLE radio_models (
    id              uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    manufacturer    varchar(120) NOT NULL,
    model           varchar(120) NOT NULL,
    family          varchar(80) NOT NULL DEFAULT 'portable',
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, manufacturer, model)
);

CREATE TABLE operational_groups (
    id              uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name            varchar(120) NOT NULL,
    description     varchar(300),
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, name)
);

CREATE TABLE radios (
    id              uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    radio_model_id  uuid NOT NULL,
    site_id         uuid NOT NULL,
    asset_tag       varchar(80) NOT NULL,
    serial_number   varchar(120) NOT NULL,
    alias           varchar(160) NOT NULL,
    status          varchar(24) NOT NULL DEFAULT 'offline' CHECK (status IN ('online','offline','maintenance')),
    battery_percent smallint CHECK (battery_percent BETWEEN 0 AND 100),
    last_seen_at    timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, asset_tag),
    UNIQUE (tenant_id, serial_number),
    FOREIGN KEY (tenant_id, radio_model_id) REFERENCES radio_models(tenant_id, id),
    FOREIGN KEY (tenant_id, site_id) REFERENCES sites(tenant_id, id)
);

CREATE TABLE radio_group_assignments (
    id              uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    radio_id        uuid NOT NULL,
    group_id        uuid NOT NULL,
    assigned_by     uuid NOT NULL,
    assigned_at     timestamptz NOT NULL DEFAULT now(),
    ended_at        timestamptz,
    FOREIGN KEY (tenant_id, radio_id) REFERENCES radios(tenant_id, id),
    FOREIGN KEY (tenant_id, group_id) REFERENCES operational_groups(tenant_id, id),
    FOREIGN KEY (tenant_id, assigned_by) REFERENCES tenant_memberships(tenant_id, user_id)
);
CREATE UNIQUE INDEX ux_radio_current_group ON radio_group_assignments(tenant_id, radio_id) WHERE ended_at IS NULL;
CREATE INDEX ix_radio_assignments_group ON radio_group_assignments(tenant_id, group_id, ended_at);

CREATE TABLE tickets (
    id                  uuid PRIMARY KEY,
    tenant_id           uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    ticket_number       varchar(32) NOT NULL,
    title               varchar(180) NOT NULL,
    description         text NOT NULL DEFAULT '',
    type                varchar(30) NOT NULL CHECK (type IN ('group_change','incident','maintenance','other')),
    priority            varchar(16) NOT NULL CHECK (priority IN ('low','normal','high','critical')),
    status              varchar(20) NOT NULL CHECK (status IN ('open','in_review','approved','in_progress','resolved','rejected')),
    radio_id            uuid,
    site_id             uuid,
    requested_group_id  uuid,
    created_by          uuid NOT NULL,
    resolution_note     text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, ticket_number),
    FOREIGN KEY (tenant_id, radio_id) REFERENCES radios(tenant_id, id),
    FOREIGN KEY (tenant_id, site_id) REFERENCES sites(tenant_id, id),
    FOREIGN KEY (tenant_id, requested_group_id) REFERENCES operational_groups(tenant_id, id),
    FOREIGN KEY (tenant_id, created_by) REFERENCES tenant_memberships(tenant_id, user_id)
);
CREATE INDEX ix_tickets_tenant_status_created ON tickets(tenant_id, status, created_at DESC);

CREATE TABLE ticket_history (
    id              uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    ticket_id       uuid NOT NULL,
    actor_id        uuid NOT NULL,
    from_status     varchar(20),
    to_status       varchar(20) NOT NULL,
    note            text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (tenant_id, ticket_id) REFERENCES tickets(tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, actor_id) REFERENCES tenant_memberships(tenant_id, user_id)
);
CREATE INDEX ix_ticket_history_tenant_ticket ON ticket_history(tenant_id, ticket_id, created_at);

CREATE TABLE ticket_comments (
    id              uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    ticket_id       uuid NOT NULL,
    author_id       uuid NOT NULL,
    body            text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (tenant_id, ticket_id) REFERENCES tickets(tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, author_id) REFERENCES tenant_memberships(tenant_id, user_id)
);
CREATE INDEX ix_ticket_comments_tenant_ticket ON ticket_comments(tenant_id, ticket_id, created_at);

CREATE TABLE audit_logs (
    id              uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    actor_id        uuid NOT NULL,
    action          varchar(100) NOT NULL,
    entity_type     varchar(80) NOT NULL,
    entity_id       uuid NOT NULL,
    payload         jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at      timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (tenant_id, actor_id) REFERENCES tenant_memberships(tenant_id, user_id)
);
CREATE INDEX ix_audit_tenant_created ON audit_logs(tenant_id, created_at DESC);

-- Demo identities. Password for all three accounts: JackstaffDemo!2026
INSERT INTO tenants (id, slug, name) VALUES
('10000000-0000-4000-8000-000000000001', 'metro-demo', 'Metro Demo'),
('10000000-0000-4000-8000-000000000002', 'security-demo', 'Security Demo'),
('10000000-0000-4000-8000-000000000003', 'logistics-demo', 'Logistics Demo');

INSERT INTO users (id, email, name, password_hash) VALUES
('20000000-0000-4000-8000-000000000001', 'admin@jackstaff.demo', 'Alex Morgan', 'pbkdf2-sha256$210000$kcK45E4byy2fbHxaK04Ncw==$z31vPMDT3eqWp/Az1+cfQ65VYR/4rpf/b1zcqLYsNB0='),
('20000000-0000-4000-8000-000000000002', 'ops@jackstaff.demo', 'Jordan Lee', 'pbkdf2-sha256$210000$kcK45E4byy2fbHxaK04Ncw==$z31vPMDT3eqWp/Az1+cfQ65VYR/4rpf/b1zcqLYsNB0='),
('20000000-0000-4000-8000-000000000003', 'support@jackstaff.demo', 'Sam Taylor', 'pbkdf2-sha256$210000$kcK45E4byy2fbHxaK04Ncw==$z31vPMDT3eqWp/Az1+cfQ65VYR/4rpf/b1zcqLYsNB0=');

INSERT INTO roles (id, name, description) VALUES
('21000000-0000-4000-8000-000000000001', 'tenant_admin', 'Administración completa de la organización'),
('21000000-0000-4000-8000-000000000002', 'operator', 'Operación e inventario'),
('21000000-0000-4000-8000-000000000003', 'support', 'Mesa de ayuda y cambios operativos'),
('21000000-0000-4000-8000-000000000004', 'viewer', 'Solo lectura');

INSERT INTO permissions (id, code, description) VALUES
('22000000-0000-4000-8000-000000000001', 'dashboard.read', 'Consultar centro de operaciones'),
('22000000-0000-4000-8000-000000000002', 'sites.read', 'Consultar instalaciones'),
('22000000-0000-4000-8000-000000000003', 'sites.write', 'Administrar instalaciones'),
('22000000-0000-4000-8000-000000000004', 'radios.read', 'Consultar inventario de radios'),
('22000000-0000-4000-8000-000000000005', 'radios.write', 'Administrar inventario de radios'),
('22000000-0000-4000-8000-000000000006', 'groups.read', 'Consultar grupos operativos'),
('22000000-0000-4000-8000-000000000007', 'groups.write', 'Administrar grupos operativos'),
('22000000-0000-4000-8000-000000000008', 'tickets.read', 'Consultar mesa de ayuda'),
('22000000-0000-4000-8000-000000000009', 'tickets.write', 'Crear solicitudes y tickets'),
('22000000-0000-4000-8000-000000000010', 'tickets.manage', 'Gestionar estados y resolución de tickets');

-- Tenant admins receive all permissions.
INSERT INTO role_permissions (role_id, permission_id)
SELECT '21000000-0000-4000-8000-000000000001', id FROM permissions;
-- Operators may create operational records and tickets, but cannot resolve support workflows.
INSERT INTO role_permissions (role_id, permission_id) VALUES
('21000000-0000-4000-8000-000000000002', '22000000-0000-4000-8000-000000000001'),
('21000000-0000-4000-8000-000000000002', '22000000-0000-4000-8000-000000000002'),
('21000000-0000-4000-8000-000000000002', '22000000-0000-4000-8000-000000000003'),
('21000000-0000-4000-8000-000000000002', '22000000-0000-4000-8000-000000000004'),
('21000000-0000-4000-8000-000000000002', '22000000-0000-4000-8000-000000000005'),
('21000000-0000-4000-8000-000000000002', '22000000-0000-4000-8000-000000000006'),
('21000000-0000-4000-8000-000000000002', '22000000-0000-4000-8000-000000000007'),
('21000000-0000-4000-8000-000000000002', '22000000-0000-4000-8000-000000000008'),
('21000000-0000-4000-8000-000000000002', '22000000-0000-4000-8000-000000000009');
-- Support can review and resolve tickets; inventory is read-only.
INSERT INTO role_permissions (role_id, permission_id) VALUES
('21000000-0000-4000-8000-000000000003', '22000000-0000-4000-8000-000000000001'),
('21000000-0000-4000-8000-000000000003', '22000000-0000-4000-8000-000000000002'),
('21000000-0000-4000-8000-000000000003', '22000000-0000-4000-8000-000000000004'),
('21000000-0000-4000-8000-000000000003', '22000000-0000-4000-8000-000000000006'),
('21000000-0000-4000-8000-000000000003', '22000000-0000-4000-8000-000000000008'),
('21000000-0000-4000-8000-000000000003', '22000000-0000-4000-8000-000000000009'),
('21000000-0000-4000-8000-000000000003', '22000000-0000-4000-8000-000000000010');
-- Viewer receives read-only access.
INSERT INTO role_permissions (role_id, permission_id)
SELECT '21000000-0000-4000-8000-000000000004', id FROM permissions WHERE code IN
('dashboard.read','sites.read','radios.read','groups.read','tickets.read');

INSERT INTO tenant_memberships (tenant_id, user_id, role_id) VALUES
('10000000-0000-4000-8000-000000000001', '20000000-0000-4000-8000-000000000001', '21000000-0000-4000-8000-000000000001'),
('10000000-0000-4000-8000-000000000002', '20000000-0000-4000-8000-000000000001', '21000000-0000-4000-8000-000000000001'),
('10000000-0000-4000-8000-000000000003', '20000000-0000-4000-8000-000000000001', '21000000-0000-4000-8000-000000000001'),
('10000000-0000-4000-8000-000000000001', '20000000-0000-4000-8000-000000000002', '21000000-0000-4000-8000-000000000002'),
('10000000-0000-4000-8000-000000000001', '20000000-0000-4000-8000-000000000003', '21000000-0000-4000-8000-000000000003');

-- METRO DEMO tenant data
INSERT INTO sites (id, tenant_id, name, code, site_type, address) VALUES
('31000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','Centro de Operaciones','CO-01','control_center','Santiago, Chile'),
('31000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','Taller Central','TC-02','workshop','Santiago, Chile');
INSERT INTO radio_models (id, tenant_id, manufacturer, model, family) VALUES
('32000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','Motorola Solutions','MOTOTRBO R7','portable'),
('32000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','Motorola Solutions','MOTOTRBO XPR 7550e','portable');
INSERT INTO operational_groups (id, tenant_id, name, description) VALUES
('33000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','Operaciones','Coordinación operativa'),
('33000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','Mantenimiento','Equipos y cuadrillas técnicas');
INSERT INTO radios (id, tenant_id, radio_model_id, site_id, asset_tag, serial_number, alias, status, battery_percent, last_seen_at) VALUES
('34000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','32000000-0000-4000-8000-000000000001','31000000-0000-4000-8000-000000000001','RAD-0421','MTR-7A-00421','Control Central 01','online',86,now()-interval '2 minutes'),
('34000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','32000000-0000-4000-8000-000000000001','31000000-0000-4000-8000-000000000001','RAD-0188','MTR-7A-00188','Supervisor Línea 2','offline',32,now()-interval '3 hours'),
('34000000-0000-4000-8000-000000000003','10000000-0000-4000-8000-000000000001','32000000-0000-4000-8000-000000000002','31000000-0000-4000-8000-000000000002','RAD-0932','MTR-XP-00932','Taller Móvil 03','maintenance',null,now()-interval '1 day');
INSERT INTO radio_group_assignments (id, tenant_id, radio_id, group_id, assigned_by) VALUES
('35000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','34000000-0000-4000-8000-000000000001','33000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001'),
('35000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','34000000-0000-4000-8000-000000000002','33000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001'),
('35000000-0000-4000-8000-000000000003','10000000-0000-4000-8000-000000000001','34000000-0000-4000-8000-000000000003','33000000-0000-4000-8000-000000000002','20000000-0000-4000-8000-000000000001');
INSERT INTO tickets (id, tenant_id, ticket_number, title, description, type, priority, status, radio_id, site_id, requested_group_id, created_by) VALUES
('36000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','JSF-261110-001A2B','Cambio de grupo para Supervisor Línea 2','Solicitar traslado desde Operaciones a Mantenimiento. Requiere revisión de soporte.','group_change','normal','in_review','34000000-0000-4000-8000-000000000002','31000000-0000-4000-8000-000000000001','33000000-0000-4000-8000-000000000002','20000000-0000-4000-8000-000000000002'),
('36000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','JSF-261110-003C4D','Revisión de batería de radio','Autonomía inferior a la esperada durante el turno.','maintenance','high','open','34000000-0000-4000-8000-000000000003','31000000-0000-4000-8000-000000000002',NULL,'20000000-0000-4000-8000-000000000003');
INSERT INTO ticket_history (id, tenant_id, ticket_id, actor_id, from_status, to_status, note) VALUES
('37000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','36000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000002',NULL,'open','Solicitud creada'),
('37000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','36000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000003','open','in_review','En revisión por soporte'),
('37000000-0000-4000-8000-000000000003','10000000-0000-4000-8000-000000000001','36000000-0000-4000-8000-000000000002','20000000-0000-4000-8000-000000000003',NULL,'open','Solicitud creada');

-- SECURITY DEMO tenant data
INSERT INTO sites (id, tenant_id, name, code, site_type, address) VALUES
('31100000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000002','Base Norte','BN-01','branch','Zona Norte'),
('31100000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000002','Centro de Monitoreo','CM-02','control_center','Zona Centro');
INSERT INTO radio_models (id, tenant_id, manufacturer, model, family) VALUES
('32100000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000002','Hytera','HP785','portable');
INSERT INTO operational_groups (id, tenant_id, name, description) VALUES
('33100000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000002','Patrullas','Equipos en terreno'),
('33100000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000002','Central','Coordinación y despacho');
INSERT INTO radios (id, tenant_id, radio_model_id, site_id, asset_tag, serial_number, alias, status, battery_percent, last_seen_at) VALUES
('34100000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000002','32100000-0000-4000-8000-000000000001','31100000-0000-4000-8000-000000000001','SEC-0101','HY-HP-00101','Patrulla Norte 01','online',91,now()-interval '1 minute'),
('34100000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000002','32100000-0000-4000-8000-000000000001','31100000-0000-4000-8000-000000000002','SEC-0102','HY-HP-00102','Central Seguridad 02','online',73,now()-interval '4 minutes');
INSERT INTO radio_group_assignments (id, tenant_id, radio_id, group_id, assigned_by) VALUES
('35100000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000002','34100000-0000-4000-8000-000000000001','33100000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001'),
('35100000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000002','34100000-0000-4000-8000-000000000002','33100000-0000-4000-8000-000000000002','20000000-0000-4000-8000-000000000001');
INSERT INTO tickets (id, tenant_id, ticket_number, title, description, type, priority, status, radio_id, site_id, created_by) VALUES
('36100000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000002','JSF-261110-SEC001','Revisión de cobertura en Base Norte','Validar comunicación en el perímetro de la instalación.','incident','high','open','34100000-0000-4000-8000-000000000001','31100000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001');
INSERT INTO ticket_history (id, tenant_id, ticket_id, actor_id, from_status, to_status, note) VALUES
('37100000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000002','36100000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001',NULL,'open','Solicitud creada');

-- LOGISTICS DEMO tenant data
INSERT INTO sites (id, tenant_id, name, code, site_type, address) VALUES
('31200000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000003','Centro de Distribución','CD-01','warehouse','Zona Poniente'),
('31200000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000003','Patio de Transporte','PT-02','yard','Zona Sur');
INSERT INTO radio_models (id, tenant_id, manufacturer, model, family) VALUES
('32200000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000003','Motorola Solutions','MOTOTRBO R2','portable');
INSERT INTO operational_groups (id, tenant_id, name, description) VALUES
('33200000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000003','Bodega','Operación de centro de distribución'),
('33200000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000003','Transporte','Conductores y patio');
INSERT INTO radios (id, tenant_id, radio_model_id, site_id, asset_tag, serial_number, alias, status, battery_percent, last_seen_at) VALUES
('34200000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000003','32200000-0000-4000-8000-000000000001','31200000-0000-4000-8000-000000000001','LOG-2001','MTR-R2-02001','Bodega Turno A','online',65,now()-interval '3 minutes'),
('34200000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000003','32200000-0000-4000-8000-000000000001','31200000-0000-4000-8000-000000000002','LOG-2002','MTR-R2-02002','Patio Transporte 02','offline',18,now()-interval '5 hours');
INSERT INTO radio_group_assignments (id, tenant_id, radio_id, group_id, assigned_by) VALUES
('35200000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000003','34200000-0000-4000-8000-000000000001','33200000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001'),
('35200000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000003','34200000-0000-4000-8000-000000000002','33200000-0000-4000-8000-000000000002','20000000-0000-4000-8000-000000000001');
INSERT INTO tickets (id, tenant_id, ticket_number, title, description, type, priority, status, radio_id, site_id, created_by) VALUES
('36200000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000003','JSF-261110-LOG001','Mantención programada','Revisión preventiva de equipo del patio.','maintenance','normal','resolved','34200000-0000-4000-8000-000000000002','31200000-0000-4000-8000-000000000002','20000000-0000-4000-8000-000000000001');
INSERT INTO ticket_history (id, tenant_id, ticket_id, actor_id, from_status, to_status, note) VALUES
('37200000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000003','36200000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001',NULL,'open','Solicitud creada'),
('37200000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000003','36200000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001','open','in_review','Revisión iniciada'),
('37200000-0000-4000-8000-000000000003','10000000-0000-4000-8000-000000000003','36200000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001','in_review','in_progress','Trabajo ejecutado'),
('37200000-0000-4000-8000-000000000004','10000000-0000-4000-8000-000000000003','36200000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001','in_progress','resolved','Mantención completada');

-- RLS is enforced by PostgreSQL, not merely by application query filters.
DO $$
DECLARE table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
        'sites','radio_models','operational_groups','radios','radio_group_assignments',
        'tickets','ticket_history','ticket_comments','audit_logs'
    ] LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', table_name);
        EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', table_name);
        EXECUTE format('CREATE POLICY tenant_isolation ON %I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', table_name);
    END LOOP;
END
$$;

GRANT CONNECT ON DATABASE jackstaff TO jackstaff_app;
GRANT USAGE ON SCHEMA public TO jackstaff_app;
GRANT SELECT ON tenants, users, roles, permissions, role_permissions, tenant_memberships TO jackstaff_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON sites, radio_models, operational_groups, radios,
    radio_group_assignments, tickets TO jackstaff_app;
-- Histories, comments and audit records are append-only at the database privilege layer.
GRANT SELECT, INSERT ON ticket_history, ticket_comments, audit_logs TO jackstaff_app;
GRANT SELECT ON schema_migrations TO jackstaff_app;

INSERT INTO schema_migrations (version) VALUES ('001_initial_v0_1');
