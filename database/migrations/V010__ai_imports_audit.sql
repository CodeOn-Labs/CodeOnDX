-- =============================================================================
-- V010 - Server-side AI insight cache, import jobs, audit log
-- =============================================================================

-- -----------------------------------------------------------------------------
-- ai_insight_results: cached LLM output per insight config. scope_key narrows
-- the cache to the data shown (e.g. a project id, a month); '' = whole screen.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.ai_insight_results (
    id            uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id     uuid          NOT NULL,
    config_id     uuid          NOT NULL,
    scope_key     varchar(200)  NOT NULL DEFAULT '',
    locale        varchar(10)   NOT NULL,
    content       text          NOT NULL,
    model         varchar(100),
    input_hash    varchar(128),
    generated_at  timestamptz   NOT NULL DEFAULT now(),
    expires_at    timestamptz,
    CONSTRAINT pk_ai_insight_results PRIMARY KEY (id),
    CONSTRAINT uq_ai_insight_results_cache_key UNIQUE (tenant_id, config_id, scope_key, locale),
    CONSTRAINT fk_ai_insight_results_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_ai_insight_results_config_id FOREIGN KEY (config_id) REFERENCES dx.ai_insight_configs (id) ON DELETE CASCADE
);
CREATE INDEX ix_ai_insight_results_config_id ON dx.ai_insight_results (config_id);

-- -----------------------------------------------------------------------------
-- import_jobs: Excel / CSV imports (projects, employees, attendance...). Progress
-- is pushed over SignalR; row errors kept for download.
-- errors = [{ "row": 5, "field": "email", "message": "..." }]
-- -----------------------------------------------------------------------------
CREATE TABLE dx.import_jobs (
    id              uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id       uuid          NOT NULL,
    kind            varchar(30)   NOT NULL,
    status          varchar(20)   NOT NULL DEFAULT 'pending',
    file_name       varchar(300)  NOT NULL,
    file_path       varchar(1000),
    total_rows      integer       NOT NULL DEFAULT 0,
    succeeded_rows  integer       NOT NULL DEFAULT 0,
    failed_rows     integer       NOT NULL DEFAULT 0,
    errors          jsonb         NOT NULL DEFAULT '[]'::jsonb,
    started_at      timestamptz,
    finished_at     timestamptz,
    created_at      timestamptz   NOT NULL DEFAULT now(),
    created_by      uuid,
    CONSTRAINT pk_import_jobs PRIMARY KEY (id),
    CONSTRAINT fk_import_jobs_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT ck_import_jobs_kind CHECK (kind IN ('projects', 'employees', 'attendance', 'timesheets', 'effort')),
    CONSTRAINT ck_import_jobs_status CHECK (status IN ('pending', 'running', 'succeeded', 'failed', 'cancelled')),
    CONSTRAINT ck_import_jobs_rows CHECK (total_rows >= 0 AND succeeded_rows >= 0 AND failed_rows >= 0),
    CONSTRAINT ck_import_jobs_errors_array CHECK (jsonb_typeof(errors) = 'array')
);
CREATE INDEX ix_import_jobs_tenant_id_created_at ON dx.import_jobs (tenant_id, created_at DESC);

-- -----------------------------------------------------------------------------
-- audit_logs: written by the EF Core SaveChanges interceptor.
-- changes = { "column": { "old": ..., "new": ... } }
-- -----------------------------------------------------------------------------
CREATE TABLE dx.audit_logs (
    id           bigint        GENERATED ALWAYS AS IDENTITY,
    tenant_id    uuid          NOT NULL,
    user_id      uuid,
    entity_type  varchar(100)  NOT NULL,
    entity_id    varchar(100)  NOT NULL,
    action       varchar(10)   NOT NULL,
    changes      jsonb,
    ip_address   varchar(64),
    user_agent   varchar(500),
    trace_id     varchar(64),
    occurred_at  timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_audit_logs PRIMARY KEY (id),
    CONSTRAINT fk_audit_logs_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT ck_audit_logs_action CHECK (action IN ('insert', 'update', 'delete', 'restore'))
);
CREATE INDEX ix_audit_logs_tenant_id_entity ON dx.audit_logs (tenant_id, entity_type, entity_id);
CREATE INDEX ix_audit_logs_tenant_id_occurred_at ON dx.audit_logs (tenant_id, occurred_at DESC);
