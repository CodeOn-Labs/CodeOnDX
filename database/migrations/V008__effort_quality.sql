-- =============================================================================
-- V008 - Effort grid and quality metrics (EE, PCV, CSS, defect, leakage)
-- Formulas are fixed in code (see frontend/src/modules/effort/utils/
-- effort-calculations.ts and the matching BE service); parameters come from
-- master_data / tenant_rules (D12).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- effort_allocations: one line = 1 employee x 1 project x 1 position x 1 month,
-- unit = FTE-month.
--   contract_effort          effort billable under the customer contract
--   actual_effort            effort actually spent (overtime excluded)
--   overtime_effort          overtime effort, tracked separately
--   performance_level_id     -> master_data(performance_level)
--   performance_coefficient  snapshot of metadata.coefficient of that level at
--                            save time, so later edits of the category do not
--                            rewrite closed months
--   allocation_rate          % of this line charged to the project (0..100)
-- -----------------------------------------------------------------------------
CREATE TABLE dx.effort_allocations (
    id                       uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id                uuid          NOT NULL,
    employee_id              uuid          NOT NULL,
    project_id               uuid          NOT NULL,
    period_month             date          NOT NULL,
    team_id                  uuid,
    position_id              uuid          NOT NULL,
    note                     varchar(100),
    contract_effort          numeric(4,2)  NOT NULL DEFAULT 0,
    actual_effort            numeric(4,2)  NOT NULL DEFAULT 1,
    overtime_effort          numeric(4,2)  NOT NULL DEFAULT 0,
    performance_level_id     uuid,
    performance_coefficient  numeric(4,2)  NOT NULL DEFAULT 1,
    allocation_rate          smallint      NOT NULL DEFAULT 100,
    created_at               timestamptz   NOT NULL DEFAULT now(),
    created_by               uuid,
    updated_at               timestamptz   NOT NULL DEFAULT now(),
    updated_by               uuid,
    CONSTRAINT pk_effort_allocations PRIMARY KEY (id),
    CONSTRAINT uq_effort_allocations_line UNIQUE (tenant_id, employee_id, project_id, position_id, period_month),
    CONSTRAINT fk_effort_allocations_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_effort_allocations_employee_id FOREIGN KEY (employee_id) REFERENCES dx.employees (id),
    CONSTRAINT fk_effort_allocations_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id),
    CONSTRAINT fk_effort_allocations_team_id FOREIGN KEY (team_id) REFERENCES dx.teams (id),
    CONSTRAINT fk_effort_allocations_position_id FOREIGN KEY (position_id) REFERENCES dx.master_data (id),
    CONSTRAINT fk_effort_allocations_performance_level_id FOREIGN KEY (performance_level_id) REFERENCES dx.master_data (id),
    CONSTRAINT ck_effort_allocations_period_month CHECK (period_month = date_trunc('month', period_month)::date),
    CONSTRAINT ck_effort_allocations_contract_effort CHECK (contract_effort BETWEEN 0 AND 2),
    CONSTRAINT ck_effort_allocations_actual_effort CHECK (actual_effort BETWEEN 0 AND 2),
    CONSTRAINT ck_effort_allocations_overtime_effort CHECK (overtime_effort BETWEEN 0 AND 2),
    CONSTRAINT ck_effort_allocations_performance_coefficient CHECK (performance_coefficient BETWEEN 0 AND 2),
    CONSTRAINT ck_effort_allocations_allocation_rate CHECK (allocation_rate BETWEEN 0 AND 100)
);
CREATE INDEX ix_effort_allocations_tenant_id_period_month ON dx.effort_allocations (tenant_id, period_month);
CREATE INDEX ix_effort_allocations_tenant_id_project_id_period_month ON dx.effort_allocations (tenant_id, project_id, period_month);

-- -----------------------------------------------------------------------------
-- project_quality_metrics: monthly defect / leakage input.
-- rate = defect_count / product_size (thresholds in tenant_rules QUALITY_*).
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_quality_metrics (
    id                 uuid           NOT NULL DEFAULT uuidv7(),
    tenant_id          uuid           NOT NULL,
    project_id         uuid           NOT NULL,
    period_month       date           NOT NULL,
    kind               varchar(10)    NOT NULL,
    defect_count       integer        NOT NULL DEFAULT 0,
    product_size       numeric(12,2),
    product_size_unit  varchar(20),
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         uuid,
    updated_at         timestamptz    NOT NULL DEFAULT now(),
    updated_by         uuid,
    CONSTRAINT pk_project_quality_metrics PRIMARY KEY (id),
    CONSTRAINT uq_project_quality_metrics_month UNIQUE (tenant_id, project_id, kind, period_month),
    CONSTRAINT fk_project_quality_metrics_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_quality_metrics_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT ck_project_quality_metrics_period_month CHECK (period_month = date_trunc('month', period_month)::date),
    CONSTRAINT ck_project_quality_metrics_kind CHECK (kind IN ('defect', 'leakage')),
    CONSTRAINT ck_project_quality_metrics_defect_count CHECK (defect_count >= 0),
    CONSTRAINT ck_project_quality_metrics_product_size CHECK (product_size IS NULL OR product_size >= 0)
);

-- -----------------------------------------------------------------------------
-- PCV (process compliance): per-project checklist copied from
-- pcv_template_items, monthly assessment, one result per item.
-- score % = passed / (passed + failed); result NULL = not assessed / N/A.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_pcv_items (
    id                uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id         uuid          NOT NULL,
    project_id        uuid          NOT NULL,
    template_item_id  uuid,
    category          varchar(200)  NOT NULL,
    content           varchar(1000) NOT NULL,
    sort_order        integer       NOT NULL DEFAULT 0,
    created_at        timestamptz   NOT NULL DEFAULT now(),
    created_by        uuid,
    updated_at        timestamptz   NOT NULL DEFAULT now(),
    updated_by        uuid,
    is_deleted        boolean       NOT NULL DEFAULT false,
    deleted_at        timestamptz,
    deleted_by        uuid,
    CONSTRAINT pk_project_pcv_items PRIMARY KEY (id),
    CONSTRAINT fk_project_pcv_items_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_pcv_items_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_pcv_items_template_item_id FOREIGN KEY (template_item_id) REFERENCES dx.pcv_template_items (id) ON DELETE SET NULL
);
CREATE INDEX ix_project_pcv_items_tenant_id_project_id ON dx.project_pcv_items (tenant_id, project_id) WHERE NOT is_deleted;

CREATE TABLE dx.project_pcv_assessments (
    id            uuid        NOT NULL DEFAULT uuidv7(),
    tenant_id     uuid        NOT NULL,
    project_id    uuid        NOT NULL,
    period_month  date        NOT NULL,
    assessed_by   uuid,
    assessed_at   timestamptz NOT NULL DEFAULT now(),
    created_at    timestamptz NOT NULL DEFAULT now(),
    created_by    uuid,
    updated_at    timestamptz NOT NULL DEFAULT now(),
    updated_by    uuid,
    CONSTRAINT pk_project_pcv_assessments PRIMARY KEY (id),
    CONSTRAINT uq_project_pcv_assessments_month UNIQUE (tenant_id, project_id, period_month),
    CONSTRAINT fk_project_pcv_assessments_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_pcv_assessments_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_pcv_assessments_assessed_by FOREIGN KEY (assessed_by) REFERENCES dx.users (id),
    CONSTRAINT ck_project_pcv_assessments_period_month CHECK (period_month = date_trunc('month', period_month)::date)
);

CREATE TABLE dx.project_pcv_results (
    tenant_id      uuid          NOT NULL,
    assessment_id  uuid          NOT NULL,
    item_id        uuid          NOT NULL,
    result         varchar(10),
    note           varchar(2000),
    CONSTRAINT pk_project_pcv_results PRIMARY KEY (assessment_id, item_id),
    CONSTRAINT fk_project_pcv_results_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_pcv_results_assessment_id FOREIGN KEY (assessment_id) REFERENCES dx.project_pcv_assessments (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_pcv_results_item_id FOREIGN KEY (item_id) REFERENCES dx.project_pcv_items (id),
    CONSTRAINT ck_project_pcv_results_result CHECK (result IS NULL OR result IN ('pass', 'fail'))
);
CREATE INDEX ix_project_pcv_results_item_id ON dx.project_pcv_results (item_id);

-- -----------------------------------------------------------------------------
-- CSS (customer satisfaction): per-project form + questions copied from the
-- tenant template; one survey link per project per month (a new link revokes
-- the old one); answers keep the question text as asked.
-- -----------------------------------------------------------------------------
CREATE TABLE dx.project_css_forms (
    project_id     uuid          NOT NULL,
    tenant_id      uuid          NOT NULL,
    title          varchar(300)  NOT NULL,
    intro          text          NOT NULL DEFAULT '',
    scale_note     varchar(1000) NOT NULL DEFAULT '',
    comment_label  varchar(500)  NOT NULL DEFAULT '',
    created_at     timestamptz   NOT NULL DEFAULT now(),
    created_by     uuid,
    updated_at     timestamptz   NOT NULL DEFAULT now(),
    updated_by     uuid,
    CONSTRAINT pk_project_css_forms PRIMARY KEY (project_id),
    CONSTRAINT fk_project_css_forms_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_css_forms_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE
);

CREATE TABLE dx.project_css_questions (
    id           uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id    uuid          NOT NULL,
    project_id   uuid          NOT NULL,
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
    CONSTRAINT pk_project_css_questions PRIMARY KEY (id),
    CONSTRAINT fk_project_css_questions_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_css_questions_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE
);
CREATE INDEX ix_project_css_questions_tenant_id_project_id ON dx.project_css_questions (tenant_id, project_id) WHERE NOT is_deleted;

CREATE TABLE dx.project_css_surveys (
    id            uuid          NOT NULL DEFAULT uuidv7(),
    tenant_id     uuid          NOT NULL,
    project_id    uuid          NOT NULL,
    period_month  date          NOT NULL,
    token         varchar(128)  NOT NULL,
    status        varchar(10)   NOT NULL DEFAULT 'sent',
    is_revoked    boolean       NOT NULL DEFAULT false,
    sent_to       varchar(256),
    sent_by       uuid,
    sent_at       timestamptz   NOT NULL DEFAULT now(),
    respondent    varchar(200),
    comment       text,
    submitted_at  timestamptz,
    created_at    timestamptz   NOT NULL DEFAULT now(),
    created_by    uuid,
    updated_at    timestamptz   NOT NULL DEFAULT now(),
    updated_by    uuid,
    CONSTRAINT pk_project_css_surveys PRIMARY KEY (id),
    CONSTRAINT uq_project_css_surveys_token UNIQUE (token),
    CONSTRAINT fk_project_css_surveys_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_css_surveys_project_id FOREIGN KEY (project_id) REFERENCES dx.projects (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_css_surveys_sent_by FOREIGN KEY (sent_by) REFERENCES dx.users (id),
    CONSTRAINT ck_project_css_surveys_period_month CHECK (period_month = date_trunc('month', period_month)::date),
    CONSTRAINT ck_project_css_surveys_status CHECK (status IN ('sent', 'submitted')),
    CONSTRAINT ck_project_css_surveys_submitted CHECK ((status = 'submitted') = (submitted_at IS NOT NULL))
);
-- Only one live link per project per month
CREATE UNIQUE INDEX uq_project_css_surveys_live_month ON dx.project_css_surveys (tenant_id, project_id, period_month) WHERE NOT is_revoked;

CREATE TABLE dx.project_css_answers (
    tenant_id      uuid          NOT NULL,
    survey_id      uuid          NOT NULL,
    question_id    uuid          NOT NULL,
    question_text  varchar(1000) NOT NULL,
    score          smallint,
    CONSTRAINT pk_project_css_answers PRIMARY KEY (survey_id, question_id),
    CONSTRAINT fk_project_css_answers_tenant_id FOREIGN KEY (tenant_id) REFERENCES dx.tenants (id),
    CONSTRAINT fk_project_css_answers_survey_id FOREIGN KEY (survey_id) REFERENCES dx.project_css_surveys (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_css_answers_question_id FOREIGN KEY (question_id) REFERENCES dx.project_css_questions (id),
    CONSTRAINT ck_project_css_answers_score CHECK (score IS NULL OR score BETWEEN 1 AND 5)
);
CREATE INDEX ix_project_css_answers_question_id ON dx.project_css_answers (question_id);
