-- =============================================================================
-- V009 - Monthly work report per project (customer-facing timesheet report).
-- Bilingual (Japanese) columns are optional: tenant setting
-- monthly_report_ja_enabled. Hours come from approved timesheet entries and
-- start/end from attendance_records unless a cell was edited by hand
-- (*_source = 'manual'); "sync" resets the source back to the automatic one.
-- =============================================================================

CREATE TABLE dx.monthly_reports (
    id                  uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id           uuid          NOT NULL,
    project_id          uuid          NOT NULL,
    period_month        date          NOT NULL,
    client_name         varchar(255),
    company_name        varchar(255),
    order_number        varchar(100),
    work_name           varchar(255),
    responsible_person  varchar(255),
    language            varchar(5)    NOT NULL DEFAULT 'vi',
    created_at          timestamptz   NOT NULL DEFAULT now(),
    created_by          uuid,
    updated_at          timestamptz   NOT NULL DEFAULT now(),
    updated_by          uuid,
    CONSTRAINT pk_monthly_reports PRIMARY KEY (id),
    CONSTRAINT uq_monthly_reports_tenant_id_project_id_period_month UNIQUE (tenant_id, project_id, period_month),
    CONSTRAINT fk_monthly_reports_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_monthly_reports_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT ck_monthly_reports_period_month CHECK (period_month = date_trunc('month', period_month)::date),
    CONSTRAINT ck_monthly_reports_language CHECK (language IN ('vi', 'en', 'ja'))
);

-- Employees shown in the report (is_removed = hidden by the user but kept so
-- that automatic re-sync does not add them back)
CREATE TABLE dx.monthly_report_members (
    tenant_id    uuid        NOT NULL,
    report_id    uuid        NOT NULL,
    employee_id  uuid        NOT NULL,
    sort_order   integer     NOT NULL DEFAULT 0,
    is_removed   boolean     NOT NULL DEFAULT false,
    created_at   timestamptz NOT NULL DEFAULT now(),
    created_by   uuid,
    CONSTRAINT pk_monthly_report_members PRIMARY KEY (report_id, employee_id),
    CONSTRAINT fk_monthly_report_members_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_monthly_report_members_report_id FOREIGN KEY (report_id) REFERENCES dx.monthly_reports (id) ON DELETE CASCADE,
    CONSTRAINT fk_monthly_report_members_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id)
);
CREATE INDEX ix_monthly_report_members_employee_id ON dx.monthly_report_members (employee_id);

-- Columns: system columns have a source (date, check_in, check_out, hours,
-- content) and cannot be deleted; custom columns (source NULL) store their
-- values in monthly_report_entries.custom_values under column_key.
CREATE TABLE dx.monthly_report_columns (
    id          uuid         NOT NULL DEFAULT uuidv7(),
    tenant_id   uuid         NOT NULL,
    report_id   uuid         NOT NULL,
    column_key  varchar(50)  NOT NULL,
    source      varchar(20),
    label       varchar(50)  NOT NULL,
    label_ja    varchar(50),
    data_type   varchar(10)  NOT NULL DEFAULT 'text',
    sort_order  integer      NOT NULL DEFAULT 0,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    created_by  uuid,
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    updated_by  uuid,
    CONSTRAINT pk_monthly_report_columns PRIMARY KEY (id),
    CONSTRAINT uq_monthly_report_columns_report_id_column_key UNIQUE (report_id, column_key),
    CONSTRAINT fk_monthly_report_columns_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_monthly_report_columns_report_id FOREIGN KEY (report_id) REFERENCES dx.monthly_reports (id) ON DELETE CASCADE,
    CONSTRAINT ck_monthly_report_columns_source CHECK (source IS NULL OR source IN ('date', 'check_in', 'check_out', 'hours', 'content')),
    CONSTRAINT ck_monthly_report_columns_data_type CHECK (data_type IN ('text', 'number')),
    CONSTRAINT ck_monthly_report_columns_label CHECK (char_length(btrim(label)) > 0)
);
CREATE UNIQUE INDEX uq_monthly_report_columns_report_id_label ON dx.monthly_report_columns (report_id, lower(btrim(label)));
CREATE UNIQUE INDEX uq_monthly_report_columns_report_id_source ON dx.monthly_report_columns (report_id, source) WHERE source IS NOT NULL;

-- One cell row per employee per day
CREATE TABLE dx.monthly_report_entries (
    id                 uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id          uuid          NOT NULL,
    report_id          uuid          NOT NULL,
    employee_id        uuid          NOT NULL,
    work_date          date          NOT NULL,
    hours              numeric(4,2),
    hours_source       varchar(10),
    start_time         time,
    end_time           time,
    time_source        varchar(10),
    content            varchar(2000),
    content_ja         varchar(2000),
    content_ja_source  varchar(10),
    custom_values      jsonb         NOT NULL DEFAULT '{}'::jsonb,
    created_at         timestamptz   NOT NULL DEFAULT now(),
    created_by         uuid,
    updated_at         timestamptz   NOT NULL DEFAULT now(),
    updated_by         uuid,
    CONSTRAINT pk_monthly_report_entries PRIMARY KEY (id),
    CONSTRAINT uq_monthly_report_entries_cell UNIQUE (report_id, employee_id, work_date),
    CONSTRAINT fk_monthly_report_entries_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_monthly_report_entries_report_id FOREIGN KEY (report_id) REFERENCES dx.monthly_reports (id) ON DELETE CASCADE,
    CONSTRAINT fk_monthly_report_entries_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id),
    CONSTRAINT ck_monthly_report_entries_hours CHECK (hours IS NULL OR (hours >= 0 AND hours <= 24 AND hours * 4 = trunc(hours * 4))),
    CONSTRAINT ck_monthly_report_entries_hours_source CHECK (hours_source IS NULL OR hours_source IN ('timesheet', 'manual')),
    CONSTRAINT ck_monthly_report_entries_time_source CHECK (time_source IS NULL OR time_source IN ('attendance', 'manual')),
    CONSTRAINT ck_monthly_report_entries_content_ja_source CHECK (content_ja_source IS NULL OR content_ja_source IN ('auto', 'manual')),
    CONSTRAINT ck_monthly_report_entries_custom_values_object CHECK (jsonb_typeof(custom_values) = 'object')
);
CREATE INDEX ix_monthly_report_entries_employee_id ON dx.monthly_report_entries (employee_id);
