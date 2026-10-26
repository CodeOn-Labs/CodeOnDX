-- =============================================================================
-- V005 - Projects and staffing: projects, contracted staff, assignments,
-- members, links, milestones, cost centers
-- =============================================================================

-- -----------------------------------------------------------------------------
-- projects. code PRJ-xx is generated max+1 including deleted rows, so the unique
-- index covers deleted rows too. Aggregates (contract FTE, actual FTE, budget
-- used %) are computed by the API, not stored.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.projects (
    id                uuid           NOT NULL DEFAULT uuidv7(),
    tenant_id         uuid           NOT NULL,
    code              varchar(30)    NOT NULL,
    name              varchar(300)   NOT NULL,
    client_id         uuid,
    grade_id          uuid,
    status_id         uuid           NOT NULL,
    risk_level_id     uuid,
    description       text,
    start_date        date,
    end_date          date,
    budget            numeric(18,2),
    currency_code     char(3),
    progress_percent  smallint       NOT NULL DEFAULT 0,
    created_at        timestamptz    NOT NULL DEFAULT now(),
    created_by        uuid,
    updated_at        timestamptz    NOT NULL DEFAULT now(),
    updated_by        uuid,
    is_deleted        boolean        NOT NULL DEFAULT false,
    deleted_at        timestamptz,
    deleted_by        uuid,
    CONSTRAINT pk_projects PRIMARY KEY (id),
    CONSTRAINT uq_projects_tenant_id_code UNIQUE (tenant_id, code),
    CONSTRAINT fk_projects_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_projects_client_id FOREIGN KEY (client_id) REFERENCES dx.clients (id),
    CONSTRAINT fk_projects_grade_id FOREIGN KEY (grade_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_projects_status_id FOREIGN KEY (status_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_projects_risk_level_id FOREIGN KEY (risk_level_id) REFERENCES dx.master_data (id),
    CONSTRAINT ck_projects_dates CHECK (end_date IS NULL OR start_date IS NULL OR end_date >= start_date),
    CONSTRAINT ck_projects_budget CHECK (budget IS NULL OR budget >= 0),
    CONSTRAINT ck_projects_currency_code CHECK (currency_code ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_projects_progress_percent CHECK (progress_percent BETWEEN 0 AND 100)
);
CREATE INDEX ix_projects_tenant_id_client_id ON dx.projects (tenant_id, client_id);
CREATE INDEX ix_projects_tenant_id_status_id ON dx.projects (tenant_id, status_id) WHERE NOT is_deleted;
CREATE INDEX ix_projects_tenant_id_end_date ON dx.projects (tenant_id, end_date) WHERE NOT is_deleted;

-- -----------------------------------------------------------------------------
-- project_contract_staff: staffing agreed in the contract
-- (position x level x quantity x FTE per person over a period)
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_contract_staff (
    id              uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id       uuid          NOT NULL,
    project_id      uuid          NOT NULL,
    position_id     uuid          NOT NULL,
    level_id        uuid,
    quantity        smallint      NOT NULL DEFAULT 1,
    fte_per_person  numeric(4,2)  NOT NULL DEFAULT 1,
    start_date      date          NOT NULL,
    end_date        date          NOT NULL,
    monthly_rate    numeric(18,2),
    created_at      timestamptz   NOT NULL DEFAULT now(),
    created_by      uuid,
    updated_at      timestamptz   NOT NULL DEFAULT now(),
    updated_by      uuid,
    is_deleted      boolean       NOT NULL DEFAULT false,
    deleted_at      timestamptz,
    deleted_by      uuid,
    CONSTRAINT pk_project_contract_staff PRIMARY KEY (id),
    CONSTRAINT fk_project_contract_staff_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_contract_staff_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_contract_staff_position_id FOREIGN KEY (position_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_project_contract_staff_level_id FOREIGN KEY (level_id) REFERENCES dx.master_data (id),
    CONSTRAINT ck_project_contract_staff_quantity CHECK (quantity > 0),
    CONSTRAINT ck_project_contract_staff_fte_per_person CHECK (fte_per_person > 0 AND fte_per_person <= 2),
    CONSTRAINT ck_project_contract_staff_dates CHECK (end_date >= start_date),
    CONSTRAINT ck_project_contract_staff_monthly_rate CHECK (monthly_rate IS NULL OR monthly_rate >= 0)
);
CREATE INDEX ix_project_contract_staff_tenant_id_project_id ON dx.project_contract_staff (tenant_id, project_id);

-- -----------------------------------------------------------------------------
-- project_assignments: a person filling slot slot_index of a contract staff line.
-- Overall range + FTE here; FTE changes over time are in project_assignment_periods.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_assignments (
    id                 uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id          uuid          NOT NULL,
    project_id         uuid          NOT NULL,
    employee_id        uuid          NOT NULL,
    contract_staff_id  uuid,
    slot_index         integer       NOT NULL DEFAULT 0,
    position_id        uuid          NOT NULL,
    level_id           uuid,
    fte_allocated      numeric(4,2)  NOT NULL DEFAULT 1,
    start_date         date          NOT NULL,
    end_date           date          NOT NULL,
    created_at         timestamptz   NOT NULL DEFAULT now(),
    created_by         uuid,
    updated_at         timestamptz   NOT NULL DEFAULT now(),
    updated_by         uuid,
    is_deleted         boolean       NOT NULL DEFAULT false,
    deleted_at         timestamptz,
    deleted_by         uuid,
    CONSTRAINT pk_project_assignments PRIMARY KEY (id),
    CONSTRAINT fk_project_assignments_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_assignments_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_assignments_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id),
    CONSTRAINT fk_project_assignments_contract_staff_id FOREIGN KEY (contract_staff_id) REFERENCES dx.project_contract_staff (id),
    CONSTRAINT fk_project_assignments_position_id FOREIGN KEY (position_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_project_assignments_level_id FOREIGN KEY (level_id) REFERENCES dx.master_data (id),
    CONSTRAINT ck_project_assignments_slot_index CHECK (slot_index >= 0),
    CONSTRAINT ck_project_assignments_fte_allocated CHECK (fte_allocated >= 0 AND fte_allocated <= 2),
    CONSTRAINT ck_project_assignments_dates CHECK (end_date >= start_date)
);
CREATE INDEX ix_project_assignments_tenant_id_project_id ON dx.project_assignments (tenant_id, project_id) WHERE NOT is_deleted;
CREATE INDEX ix_project_assignments_tenant_id_employee_id ON dx.project_assignments (tenant_id, employee_id) WHERE NOT is_deleted;
CREATE INDEX ix_project_assignments_contract_staff_id ON dx.project_assignments (contract_staff_id);

CREATE TABLE dx.project_assignment_periods (
    id             uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id      uuid          NOT NULL,
    assignment_id  uuid          NOT NULL,
    start_date     date          NOT NULL,
    end_date       date          NOT NULL,
    fte_allocated  numeric(4,2)  NOT NULL,
    created_at     timestamptz   NOT NULL DEFAULT now(),
    created_by     uuid,
    updated_at     timestamptz   NOT NULL DEFAULT now(),
    updated_by     uuid,
    CONSTRAINT pk_project_assignment_periods PRIMARY KEY (id),
    CONSTRAINT fk_project_assignment_periods_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_assignment_periods_assignment_id FOREIGN KEY (assignment_id) REFERENCES dx.project_assignments (id) ON DELETE CASCADE,
    CONSTRAINT ck_project_assignment_periods_fte_allocated CHECK (fte_allocated >= 0 AND fte_allocated <= 2),
    CONSTRAINT ck_project_assignment_periods_dates CHECK (end_date >= start_date)
);
CREATE INDEX ix_project_assignment_periods_assignment_id ON dx.project_assignment_periods (assignment_id);

-- -----------------------------------------------------------------------------
-- project_members: who belongs to a project and in which position. Used for
-- per-project authorization (D3: member / project manager), kept in sync with
-- assignments by the application.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_members (
    tenant_id    uuid        NOT NULL,
    project_id   uuid        NOT NULL,
    employee_id  uuid        NOT NULL,
    position_id  uuid,
    is_manager   boolean     NOT NULL DEFAULT false,
    created_at   timestamptz NOT NULL DEFAULT now(),
    created_by   uuid,
    updated_at   timestamptz NOT NULL DEFAULT now(),
    updated_by   uuid,
    CONSTRAINT pk_project_members PRIMARY KEY (project_id, employee_id),
    CONSTRAINT fk_project_members_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_members_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_members_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id),
    CONSTRAINT fk_project_members_position_id FOREIGN KEY (position_id) REFERENCES dx.master_data (id)
);
CREATE INDEX ix_project_members_tenant_id_employee_id ON dx.project_members (tenant_id, employee_id);

-- -----------------------------------------------------------------------------
-- project_links: ticket system / source code / storage links.
-- link_type_id -> master_data(link_type); provider comes from the link type code.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_links (
    id            uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id     uuid          NOT NULL,
    project_id    uuid          NOT NULL,
    link_type_id  uuid          NOT NULL,
    name          varchar(200)  NOT NULL,
    url           varchar(2000) NOT NULL,
    sort_order    integer       NOT NULL DEFAULT 0,
    created_at    timestamptz   NOT NULL DEFAULT now(),
    created_by    uuid,
    updated_at    timestamptz   NOT NULL DEFAULT now(),
    updated_by    uuid,
    CONSTRAINT pk_project_links PRIMARY KEY (id),
    CONSTRAINT fk_project_links_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_links_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_links_link_type_id FOREIGN KEY (link_type_id) REFERENCES dx.master_data (id),
    CONSTRAINT ck_project_links_url_scheme CHECK (url ~* '^https?://')
);
CREATE INDEX ix_project_links_tenant_id_project_id ON dx.project_links (tenant_id, project_id);

CREATE TABLE dx.project_milestones (
    id             uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id      uuid          NOT NULL,
    project_id     uuid          NOT NULL,
    name           varchar(300)  NOT NULL,
    sort_order     integer       NOT NULL DEFAULT 0,
    planned_date   date          NOT NULL,
    actual_date    date,
    justification  varchar(2000),
    created_at     timestamptz   NOT NULL DEFAULT now(),
    created_by     uuid,
    updated_at     timestamptz   NOT NULL DEFAULT now(),
    updated_by     uuid,
    is_deleted     boolean       NOT NULL DEFAULT false,
    deleted_at     timestamptz,
    deleted_by     uuid,
    CONSTRAINT pk_project_milestones PRIMARY KEY (id),
    CONSTRAINT fk_project_milestones_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_milestones_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE
);
CREATE INDEX ix_project_milestones_tenant_id_project_id ON dx.project_milestones (tenant_id, project_id, planned_date) WHERE NOT is_deleted;

-- -----------------------------------------------------------------------------
-- project_cost_centers: department that owns a project's effort (Effort grid
-- groups projects by this department)
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_cost_centers (
    project_id     uuid        NOT NULL,
    tenant_id      uuid        NOT NULL,
    department_id  uuid        NOT NULL,
    created_at     timestamptz NOT NULL DEFAULT now(),
    created_by     uuid,
    updated_at     timestamptz NOT NULL DEFAULT now(),
    updated_by     uuid,
    CONSTRAINT pk_project_cost_centers PRIMARY KEY (project_id),
    CONSTRAINT fk_project_cost_centers_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_cost_centers_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_cost_centers_department_id FOREIGN KEY (department_id) REFERENCES dx.departments (id)
);
CREATE INDEX ix_project_cost_centers_tenant_id_department_id ON dx.project_cost_centers (tenant_id, department_id);
