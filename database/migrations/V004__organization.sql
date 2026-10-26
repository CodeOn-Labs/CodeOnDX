-- =============================================================================
-- V004 - Organization: departments, teams, employees, clients (D4: managed
-- inside the product, no HRM sync)
-- =============================================================================

CREATE TABLE dx.departments (
    id                   uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id            uuid          NOT NULL,
    code                 varchar(50)   NOT NULL,
    name                 varchar(200)  NOT NULL,
    description          varchar(1000),
    manager_employee_id  uuid,
    sort_order           integer       NOT NULL DEFAULT 0,
    is_active            boolean       NOT NULL DEFAULT true,
    created_at           timestamptz   NOT NULL DEFAULT now(),
    created_by           uuid,
    updated_at           timestamptz   NOT NULL DEFAULT now(),
    updated_by           uuid,
    CONSTRAINT pk_departments PRIMARY KEY (id),
    CONSTRAINT uq_departments_tenant_id_code UNIQUE (tenant_id, code),
    CONSTRAINT fk_departments_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id)
);

CREATE TABLE dx.teams (
    id                  uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id           uuid          NOT NULL,
    department_id       uuid          NOT NULL,
    code                varchar(50)   NOT NULL,
    name                varchar(200)  NOT NULL,
    description         varchar(1000),
    leader_employee_id  uuid,
    sort_order          integer       NOT NULL DEFAULT 0,
    is_active           boolean       NOT NULL DEFAULT true,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    created_by          uuid,
    updated_at          timestamptz   NOT NULL DEFAULT now(),
    updated_by          uuid,
    CONSTRAINT pk_teams PRIMARY KEY (id),
    CONSTRAINT uq_teams_tenant_id_code UNIQUE (tenant_id, code),
    CONSTRAINT fk_teams_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_teams_department_id FOREIGN KEY (department_id) REFERENCES dx.departments (id)
);
CREATE INDEX ix_teams_tenant_id_department_id ON dx.teams (tenant_id, department_id);

-- -----------------------------------------------------------------------------
-- employees: HR record. A login account (users) is optional and created by
-- invitation. status_id -> master_data(employee_status); its metadata.working
-- decides whether the person can be assigned / logs time.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.employees (
    id                uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id         uuid          NOT NULL,
    user_id           uuid,
    code              varchar(30)   NOT NULL,
    full_name         varchar(200)  NOT NULL,
    email             varchar(256)  NOT NULL,
    phone             varchar(32),
    position_id       uuid,
    level_id          uuid,
    department_id     uuid,
    team_id           uuid,
    status_id         uuid          NOT NULL,
    hire_date         date,
    termination_date  date,
    avatar_url        varchar(1000),
    skills            text[]        NOT NULL DEFAULT '{}',
    created_at        timestamptz   NOT NULL DEFAULT now(),
    created_by        uuid,
    updated_at        timestamptz   NOT NULL DEFAULT now(),
    updated_by        uuid,
    is_deleted        boolean       NOT NULL DEFAULT false,
    deleted_at        timestamptz,
    deleted_by        uuid,
    CONSTRAINT pk_employees PRIMARY KEY (id),
    CONSTRAINT fk_employees_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_employees_user_id FOREIGN KEY (user_id) REFERENCES dx.users (id) ON DELETE SET NULL,
    CONSTRAINT fk_employees_position_id FOREIGN KEY (position_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_employees_level_id FOREIGN KEY (level_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_employees_department_id FOREIGN KEY (department_id) REFERENCES dx.departments (id),
    CONSTRAINT fk_employees_team_id FOREIGN KEY (team_id) REFERENCES dx.teams (id),
    CONSTRAINT fk_employees_status_id FOREIGN KEY (status_id) REFERENCES dx.master_data (id),
    CONSTRAINT ck_employees_dates CHECK (termination_date IS NULL OR hire_date IS NULL OR termination_date >= hire_date)
);
CREATE UNIQUE INDEX uq_employees_tenant_id_code ON dx.employees (tenant_id, code) WHERE NOT is_deleted;
CREATE UNIQUE INDEX uq_employees_tenant_id_email ON dx.employees (tenant_id, lower(email)) WHERE NOT is_deleted;
CREATE UNIQUE INDEX uq_employees_tenant_id_user_id ON dx.employees (tenant_id, user_id) WHERE user_id IS NOT NULL;
CREATE INDEX ix_employees_tenant_id_department_id ON dx.employees (tenant_id, department_id);
CREATE INDEX ix_employees_tenant_id_team_id ON dx.employees (tenant_id, team_id);
CREATE INDEX ix_employees_status_id ON dx.employees (status_id);

ALTER TABLE dx.departments
    ADD CONSTRAINT fk_departments_manager_employee_id FOREIGN KEY (manager_employee_id) REFERENCES dx.employees (id) ON DELETE SET NULL;
ALTER TABLE dx.teams
    ADD CONSTRAINT fk_teams_leader_employee_id FOREIGN KEY (leader_employee_id) REFERENCES dx.employees (id) ON DELETE SET NULL;
ALTER TABLE dx.account_tokens
    ADD CONSTRAINT fk_account_tokens_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id) ON DELETE CASCADE;

-- -----------------------------------------------------------------------------
-- clients: customers of the tenant (Admin > Clients). Delete = soft delete
-- (trash), second delete = hard delete only when no project references it.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.clients (
    id                     uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id              uuid          NOT NULL,
    code                   varchar(50)   NOT NULL,
    name                   varchar(300)  NOT NULL,
    short_name             varchar(100),
    country_id             uuid,
    industry_id            uuid,
    contact_name           varchar(200),
    contact_email          varchar(256),
    default_currency_code  char(3),
    is_active              boolean       NOT NULL DEFAULT true,
    created_at             timestamptz   NOT NULL DEFAULT now(),
    created_by             uuid,
    updated_at             timestamptz   NOT NULL DEFAULT now(),
    updated_by             uuid,
    is_deleted             boolean       NOT NULL DEFAULT false,
    deleted_at             timestamptz,
    deleted_by             uuid,
    CONSTRAINT pk_clients PRIMARY KEY (id),
    CONSTRAINT fk_clients_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_clients_country_id FOREIGN KEY (country_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_clients_industry_id FOREIGN KEY (industry_id) REFERENCES dx.master_data (id),
    CONSTRAINT ck_clients_default_currency_code CHECK (default_currency_code ~ '^[A-Z]{3}$')
);
CREATE UNIQUE INDEX uq_clients_tenant_id_code ON dx.clients (tenant_id, code) WHERE NOT is_deleted;
CREATE INDEX ix_clients_tenant_id_name ON dx.clients (tenant_id, name);
