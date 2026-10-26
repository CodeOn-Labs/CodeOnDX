-- =============================================================================
-- V003 - Per-tenant configuration (D12): lookups, settings, business rules,
-- holidays, connectors, PCV / CSS form templates, AI prompts and insights.
-- Formulas live in code; every parameter they use lives here.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- master_data: tenant-editable lookup lists (Admin > Categories).
-- Known types (validated by the API, not by the DB, so new types need no migration):
--   project_status, project_level, risk_level, hr_level, job_position, task_type,
--   employee_status, leave_type, link_type, country, industry, currency,
--   performance_level
-- metadata carries type-specific parameters, e.g.
--   performance_level -> {"coefficient": 0.8, "color": "blue"}   (effort formula)
--   leave_type        -> {"paid": true, "deduct_balance": true, "need_attachment": false}
--   employee_status   -> {"working": true}
--   task_type         -> {"system": true}  (OT / LEAVE rows generated from requests)
-- -----------------------------------------------------------------------------
CREATE TABLE dx.master_data (
    id           uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id    uuid          NOT NULL,
    type         varchar(50)   NOT NULL,
    code         varchar(50)   NOT NULL,
    name         varchar(200)  NOT NULL,
    name_en      varchar(200),
    description  varchar(1000),
    metadata     jsonb         NOT NULL DEFAULT '{}'::jsonb,
    sort_order   integer       NOT NULL DEFAULT 0,
    is_system    boolean       NOT NULL DEFAULT false,
    is_active    boolean       NOT NULL DEFAULT true,
    created_at   timestamptz   NOT NULL DEFAULT now(),
    created_by   uuid,
    updated_at   timestamptz   NOT NULL DEFAULT now(),
    updated_by   uuid,
    CONSTRAINT pk_master_data PRIMARY KEY (id),
    CONSTRAINT uq_master_data_tenant_id_type_code UNIQUE (tenant_id, type, code),
    CONSTRAINT fk_master_data_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT ck_master_data_type_format CHECK (type ~ '^[a-z][a-z_]*$'),
    CONSTRAINT ck_master_data_code_format CHECK (code ~ '^[A-Z0-9_]+$'),
    CONSTRAINT ck_master_data_metadata_object CHECK (jsonb_typeof(metadata) = 'object')
);

-- -----------------------------------------------------------------------------
-- tenant_settings: general key/value settings (Admin > Settings), e.g.
--   company_name, company_logo, default_locale, time_zone, currency, work_week,
--   fiscal_year_start_month, work_start_time, work_end_time, lunch_break_minutes,
--   notification_email, monthly_report_ja_enabled, allow_app_check_in,
--   password_min_length, invitation_expiry_hours
-- -----------------------------------------------------------------------------
CREATE TABLE dx.tenant_settings (
    tenant_id   uuid        NOT NULL,
    key         varchar(64) NOT NULL,
    value       text        NOT NULL DEFAULT '',
    updated_at  timestamptz NOT NULL DEFAULT now(),
    updated_by  uuid,
    CONSTRAINT pk_tenant_settings PRIMARY KEY (tenant_id, key),
    CONSTRAINT fk_tenant_settings_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT ck_tenant_settings_key_format CHECK (key ~ '^[a-z][a-z0-9_]*$')
);

-- -----------------------------------------------------------------------------
-- system_rules: global catalog of rule definitions (type, range, default, labels).
-- tenant_rules: the value each tenant actually uses (Admin > Rules).
-- A missing tenant_rules row means "use system_rules.default_value".
-- -----------------------------------------------------------------------------
CREATE TABLE dx.system_rules (
    key             varchar(64)   NOT NULL,
    module          varchar(32)   NOT NULL,
    value_type      varchar(16)   NOT NULL,
    default_value   jsonb         NOT NULL,
    min_value       numeric,
    max_value       numeric,
    name            varchar(200)  NOT NULL,
    name_en         varchar(200),
    description     varchar(1000),
    description_en  varchar(1000),
    sort_order      integer       NOT NULL DEFAULT 0,
    CONSTRAINT pk_system_rules PRIMARY KEY (key),
    CONSTRAINT ck_system_rules_key_format CHECK (key ~ '^[A-Z][A-Z0-9_]*$'),
    CONSTRAINT ck_system_rules_value_type CHECK (value_type IN ('number', 'boolean', 'string')),
    CONSTRAINT ck_system_rules_range CHECK (min_value IS NULL OR max_value IS NULL OR min_value <= max_value)
);

CREATE TABLE dx.tenant_rules (
    tenant_id   uuid        NOT NULL,
    rule_key    varchar(64) NOT NULL,
    value       jsonb       NOT NULL,
    is_enabled  boolean     NOT NULL DEFAULT true,
    updated_at  timestamptz NOT NULL DEFAULT now(),
    updated_by  uuid,
    CONSTRAINT pk_tenant_rules PRIMARY KEY (tenant_id, rule_key),
    CONSTRAINT fk_tenant_rules_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_tenant_rules_rule_key FOREIGN KEY (rule_key) REFERENCES dx.system_rules (key)
);

-- -----------------------------------------------------------------------------
-- holidays: public holidays (not counted as leave days; OT on them uses the
-- holiday rate)
-- -----------------------------------------------------------------------------
CREATE TABLE dx.holidays (
    id            uuid         NOT NULL DEFAULT uuidv7(),
    tenant_id     uuid         NOT NULL,
    holiday_date  date         NOT NULL,
    name          varchar(200) NOT NULL,
    is_paid       boolean      NOT NULL DEFAULT true,
    created_at    timestamptz  NOT NULL DEFAULT now(),
    created_by    uuid,
    updated_at    timestamptz  NOT NULL DEFAULT now(),
    updated_by    uuid,
    CONSTRAINT pk_holidays PRIMARY KEY (id),
    CONSTRAINT uq_holidays_tenant_id_holiday_date UNIQUE (tenant_id, holiday_date),
    CONSTRAINT fk_holidays_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id)
);

-- -----------------------------------------------------------------------------
-- tenant_integrations: the 5 connectors (Q3). Non-secret options in settings;
-- secrets encrypted with ASP.NET Data Protection before they reach the DB.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.tenant_integrations (
    tenant_id          uuid        NOT NULL,
    integration_key    varchar(32) NOT NULL,
    is_enabled         boolean     NOT NULL DEFAULT false,
    use_mock           boolean     NOT NULL DEFAULT true,
    settings           jsonb       NOT NULL DEFAULT '{}'::jsonb,
    secrets_encrypted  text,
    last_sync_at       timestamptz,
    created_at         timestamptz NOT NULL DEFAULT now(),
    created_by         uuid,
    updated_at         timestamptz NOT NULL DEFAULT now(),
    updated_by         uuid,
    CONSTRAINT pk_tenant_integrations PRIMARY KEY (tenant_id, integration_key),
    CONSTRAINT fk_tenant_integrations_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT ck_tenant_integrations_key CHECK (integration_key IN ('Ai', 'GitHub', 'GitLab', 'GoogleDrive', 'Email')),
    CONSTRAINT ck_tenant_integrations_settings_object CHECK (jsonb_typeof(settings) = 'object')
);

-- -----------------------------------------------------------------------------
-- PCV checklist template (Admin > PCV criteria). Copied into project_pcv_items
-- when a project is created, so later template edits do not change old projects.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.pcv_template_items (
    id          uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id   uuid          NOT NULL,
    category    varchar(200)  NOT NULL,
    content     varchar(1000) NOT NULL,
    sort_order  integer       NOT NULL DEFAULT 0,
    created_at  timestamptz   NOT NULL DEFAULT now(),
    created_by  uuid,
    updated_at  timestamptz   NOT NULL DEFAULT now(),
    updated_by  uuid,
    is_deleted  boolean       NOT NULL DEFAULT false,
    deleted_at  timestamptz,
    deleted_by  uuid,
    CONSTRAINT pk_pcv_template_items PRIMARY KEY (id),
    CONSTRAINT fk_pcv_template_items_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id)
);
CREATE INDEX ix_pcv_template_items_tenant_id_sort_order ON dx.pcv_template_items (tenant_id, sort_order) WHERE NOT is_deleted;

-- -----------------------------------------------------------------------------
-- CSS survey template (Admin > CSS template): one form per tenant + questions.
-- Copied into project_css_forms / project_css_questions per project.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.css_templates (
    id             uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id      uuid          NOT NULL,
    title          varchar(300)  NOT NULL,
    intro          text          NOT NULL DEFAULT '',
    scale_note     varchar(1000) NOT NULL DEFAULT '',
    comment_label  varchar(500)  NOT NULL DEFAULT '',
    created_at     timestamptz   NOT NULL DEFAULT now(),
    created_by     uuid,
    updated_at     timestamptz   NOT NULL DEFAULT now(),
    updated_by     uuid,
    CONSTRAINT pk_css_templates PRIMARY KEY (id),
    CONSTRAINT uq_css_templates_tenant_id UNIQUE (tenant_id),
    CONSTRAINT fk_css_templates_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id)
);

CREATE TABLE dx.css_template_questions (
    id           uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id    uuid          NOT NULL,
    template_id  uuid          NOT NULL,
    content      varchar(1000) NOT NULL,
    is_required  boolean       NOT NULL DEFAULT true,
    sort_order   integer       NOT NULL DEFAULT 0,
    created_at   timestamptz   NOT NULL DEFAULT now(),
    created_by   uuid,
    updated_at   timestamptz   NOT NULL DEFAULT now(),
    updated_by   uuid,
    is_deleted   boolean       NOT NULL DEFAULT false,
    deleted_at   timestamptz,
    deleted_by   uuid,
    CONSTRAINT pk_css_template_questions PRIMARY KEY (id),
    CONSTRAINT fk_css_template_questions_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_css_template_questions_template_id FOREIGN KEY (template_id) REFERENCES dx.css_templates (id) ON DELETE CASCADE
);
CREATE INDEX ix_css_template_questions_template_id ON dx.css_template_questions (template_id) WHERE NOT is_deleted;

-- -----------------------------------------------------------------------------
-- AI: prompt templates (Handlebars syntax, rendered by the API) and
-- insight configurations per screen_key (shared/screens.json).
-- -----------------------------------------------------------------------------
CREATE TABLE dx.ai_prompt_templates (
    id          uuid         NOT NULL DEFAULT uuidv7(),
    tenant_id   uuid         NOT NULL,
    scope       varchar(20)  NOT NULL,
    name        varchar(200) NOT NULL,
    template    text         NOT NULL,
    is_active   boolean      NOT NULL DEFAULT true,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid,
    CONSTRAINT pk_ai_prompt_templates PRIMARY KEY (id),
    CONSTRAINT fk_ai_prompt_templates_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT ck_ai_prompt_templates_scope CHECK (scope IN ('chat', 'insight'))
);
-- At most one active template per scope
CREATE UNIQUE INDEX uq_ai_prompt_templates_tenant_id_scope_active ON dx.ai_prompt_templates (tenant_id, scope) WHERE is_active;

CREATE TABLE dx.ai_insight_configs (
    id                uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id         uuid          NOT NULL,
    screen_key        varchar(100)  NOT NULL,
    name              varchar(300)  NOT NULL,
    data_sources      text[]        NOT NULL DEFAULT '{}',
    allowed_role_ids  uuid[]        NOT NULL DEFAULT '{}',
    data_prompt       text          NOT NULL,
    sort_order        integer       NOT NULL DEFAULT 0,
    is_active         boolean       NOT NULL DEFAULT true,
    created_at        timestamptz   NOT NULL DEFAULT now(),
    created_by        uuid,
    updated_at        timestamptz   NOT NULL DEFAULT now(),
    updated_by        uuid,
    CONSTRAINT pk_ai_insight_configs PRIMARY KEY (id),
    CONSTRAINT fk_ai_insight_configs_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT ck_ai_insight_configs_screen_key_format CHECK (screen_key ~ '^[a-z][a-z0-9_]*$')
);
CREATE INDEX ix_ai_insight_configs_tenant_id_screen_key ON dx.ai_insight_configs (tenant_id, screen_key) WHERE is_active;

COMMENT ON COLUMN dx.ai_insight_configs.allowed_role_ids IS 'Empty array = visible to every role.';
