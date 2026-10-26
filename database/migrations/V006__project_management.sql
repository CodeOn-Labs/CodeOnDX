-- =============================================================================
-- V006 - Project management tab: documents, risks, change requests, issue
-- links, status report. Display codes (R-01, CR-01) are max+1 per project
-- including deleted rows -> unique index covers deleted rows too.
-- =============================================================================

CREATE TABLE dx.project_document_groups (
    id           uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id    uuid          NOT NULL,
    project_id   uuid          NOT NULL,
    name         varchar(200)  NOT NULL,
    description  varchar(1000),
    sort_order   integer       NOT NULL DEFAULT 0,
    created_at   timestamptz   NOT NULL DEFAULT now(),
    created_by   uuid,
    updated_at   timestamptz   NOT NULL DEFAULT now(),
    updated_by   uuid,
    is_deleted   boolean       NOT NULL DEFAULT false,
    deleted_at   timestamptz,
    deleted_by   uuid,
    CONSTRAINT pk_project_document_groups PRIMARY KEY (id),
    CONSTRAINT fk_project_document_groups_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_document_groups_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE
);
CREATE INDEX ix_project_document_groups_tenant_id_project_id ON dx.project_document_groups (tenant_id, project_id) WHERE NOT is_deleted;

CREATE TABLE dx.project_documents (
    id          uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id   uuid          NOT NULL,
    project_id  uuid          NOT NULL,
    group_id    uuid          NOT NULL,
    name        varchar(300)  NOT NULL,
    url         varchar(2000) NOT NULL,
    created_at  timestamptz   NOT NULL DEFAULT now(),
    created_by  uuid,
    updated_at  timestamptz   NOT NULL DEFAULT now(),
    updated_by  uuid,
    is_deleted  boolean       NOT NULL DEFAULT false,
    deleted_at  timestamptz,
    deleted_by  uuid,
    CONSTRAINT pk_project_documents PRIMARY KEY (id),
    CONSTRAINT fk_project_documents_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_documents_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_documents_group_id FOREIGN KEY (group_id) REFERENCES dx.project_document_groups (id),
    CONSTRAINT ck_project_documents_url_scheme CHECK (url ~* '^https?://')
);
CREATE INDEX ix_project_documents_tenant_id_project_id ON dx.project_documents (tenant_id, project_id) WHERE NOT is_deleted;
CREATE INDEX ix_project_documents_group_id ON dx.project_documents (group_id);

-- -----------------------------------------------------------------------------
-- project_risks: risk register. score = probability x impact (1..25), computed
-- by the API (5x5 matrix, level thresholds).
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_risks (
    id                 uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id          uuid          NOT NULL,
    project_id         uuid          NOT NULL,
    code               varchar(20)   NOT NULL,
    title              varchar(300)  NOT NULL,
    description        text,
    category           varchar(20)   NOT NULL,
    probability        smallint      NOT NULL,
    impact             smallint      NOT NULL,
    strategy           varchar(20)   NOT NULL,
    response_plan      text,
    owner_employee_id  uuid,
    due_date           date          NOT NULL,
    status             varchar(20)   NOT NULL DEFAULT 'open',
    identified_date    date          NOT NULL,
    created_at         timestamptz   NOT NULL DEFAULT now(),
    created_by         uuid,
    updated_at         timestamptz   NOT NULL DEFAULT now(),
    updated_by         uuid,
    is_deleted         boolean       NOT NULL DEFAULT false,
    deleted_at         timestamptz,
    deleted_by         uuid,
    CONSTRAINT pk_project_risks PRIMARY KEY (id),
    CONSTRAINT uq_project_risks_tenant_id_project_id_code UNIQUE (tenant_id, project_id, code),
    CONSTRAINT fk_project_risks_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_risks_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_risks_owner_employee_id FOREIGN KEY (owner_employee_id) REFERENCES dx.employees (id),
    CONSTRAINT ck_project_risks_category CHECK (category IN ('customer', 'resource', 'technical', 'schedule', 'cost', 'quality', 'other')),
    CONSTRAINT ck_project_risks_probability CHECK (probability BETWEEN 1 AND 5),
    CONSTRAINT ck_project_risks_impact CHECK (impact BETWEEN 1 AND 5),
    CONSTRAINT ck_project_risks_strategy CHECK (strategy IN ('avoid', 'mitigate', 'transfer', 'accept')),
    CONSTRAINT ck_project_risks_status CHECK (status IN ('open', 'in_progress', 'resolved', 'closed'))
);
CREATE INDEX ix_project_risks_owner_employee_id ON dx.project_risks (owner_employee_id);

CREATE TABLE dx.project_change_requests (
    id            uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id     uuid          NOT NULL,
    project_id    uuid          NOT NULL,
    code          varchar(20)   NOT NULL,
    title         varchar(300)  NOT NULL,
    url           varchar(2000),
    source        varchar(20)   NOT NULL,
    impact_level  varchar(20)   NOT NULL,
    impact_areas  text[]        NOT NULL DEFAULT '{}',
    impact_note   varchar(2000),
    created_at    timestamptz   NOT NULL DEFAULT now(),
    created_by    uuid,
    updated_at    timestamptz   NOT NULL DEFAULT now(),
    updated_by    uuid,
    is_deleted    boolean       NOT NULL DEFAULT false,
    deleted_at    timestamptz,
    deleted_by    uuid,
    CONSTRAINT pk_project_change_requests PRIMARY KEY (id),
    CONSTRAINT uq_project_change_requests_tenant_id_project_id_code UNIQUE (tenant_id, project_id, code),
    CONSTRAINT fk_project_change_requests_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_change_requests_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT ck_project_change_requests_source CHECK (source IN ('customer', 'internal')),
    CONSTRAINT ck_project_change_requests_impact_level CHECK (impact_level IN ('low', 'medium', 'high')),
    CONSTRAINT ck_project_change_requests_impact_areas CHECK (impact_areas <@ ARRAY['scope', 'schedule', 'cost', 'quality', 'resource']::text[]),
    CONSTRAINT ck_project_change_requests_url_scheme CHECK (url IS NULL OR url ~* '^https?://')
);

-- -----------------------------------------------------------------------------
-- project_issue_links: links to epics / sprints / milestones in the ticket system
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_issue_links (
    id          uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id   uuid          NOT NULL,
    project_id  uuid          NOT NULL,
    category    varchar(20)   NOT NULL,
    issue_type  varchar(20)   NOT NULL,
    name        varchar(300)  NOT NULL,
    url         varchar(2000) NOT NULL,
    created_at  timestamptz   NOT NULL DEFAULT now(),
    created_by  uuid,
    updated_at  timestamptz   NOT NULL DEFAULT now(),
    updated_by  uuid,
    is_deleted  boolean       NOT NULL DEFAULT false,
    deleted_at  timestamptz,
    deleted_by  uuid,
    CONSTRAINT pk_project_issue_links PRIMARY KEY (id),
    CONSTRAINT fk_project_issue_links_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_issue_links_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT ck_project_issue_links_category CHECK (category IN ('parent', 'sprint', 'milestone')),
    CONSTRAINT ck_project_issue_links_issue_type CHECK (issue_type IN ('task', 'bug')),
    CONSTRAINT ck_project_issue_links_url_scheme CHECK (url ~* '^https?://')
);
CREATE INDEX ix_project_issue_links_tenant_id_project_id ON dx.project_issue_links (tenant_id, project_id) WHERE NOT is_deleted;

-- -----------------------------------------------------------------------------
-- project_status_reports: one current status report per project
-- items = [{ "metric": "...", "content": "..." }]
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_status_reports (
    project_id       uuid        NOT NULL,
    tenant_id        uuid        NOT NULL,
    overall_status   varchar(10) NOT NULL DEFAULT 'GOOD',
    items            jsonb       NOT NULL DEFAULT '[]'::jsonb,
    ai_summary       text,
    ai_generated_at  timestamptz,
    created_at       timestamptz NOT NULL DEFAULT now(),
    created_by       uuid,
    updated_at       timestamptz NOT NULL DEFAULT now(),
    updated_by       uuid,
    CONSTRAINT pk_project_status_reports PRIMARY KEY (project_id),
    CONSTRAINT fk_project_status_reports_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_status_reports_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT ck_project_status_reports_overall_status CHECK (overall_status IN ('GOOD', 'RISK', 'SOS')),
    CONSTRAINT ck_project_status_reports_items_array CHECK (jsonb_typeof(items) = 'array')
);
