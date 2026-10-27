-- =============================================================================
-- V014 - Fields the AI admin screens and the integrations screen need
--   ai_prompt_templates : lifecycle draft / active / archived (is_active kept in
--                         sync = status 'active'), screens it applies to,
--                         description, version (+1 on every save), scope 'translate'
--   ai_insight_configs  : when the insight is shown (trigger_condition)
--   tenant_integrations : result of the last "test connection"
-- =============================================================================

ALTER TABLE dx.ai_prompt_templates
    ADD COLUMN status       varchar(10)  NOT NULL DEFAULT 'active',
    ADD COLUMN screen_keys  text[]       NOT NULL DEFAULT '{}',
    ADD COLUMN description  varchar(500),
    ADD COLUMN version      integer      NOT NULL DEFAULT 1;

UPDATE dx.ai_prompt_templates SET status = CASE WHEN is_active THEN 'active' ELSE 'draft' END;

ALTER TABLE dx.ai_prompt_templates
    ADD CONSTRAINT ck_ai_prompt_templates_status CHECK (status IN ('draft', 'active', 'archived')),
    ADD CONSTRAINT ck_ai_prompt_templates_version CHECK (version >= 1),
    DROP CONSTRAINT ck_ai_prompt_templates_scope,
    ADD CONSTRAINT ck_ai_prompt_templates_scope CHECK (scope IN ('chat', 'insight', 'translate'));

-- Several active templates per scope are allowed (e.g. one per screen); the API keeps at least one.
DROP INDEX dx.uq_ai_prompt_templates_tenant_id_scope_active;
CREATE INDEX ix_ai_prompt_templates_tenant_id_scope ON dx.ai_prompt_templates (tenant_id, scope) WHERE status = 'active';

ALTER TABLE dx.ai_insight_configs
    ADD COLUMN trigger_condition  varchar(30)  NOT NULL DEFAULT 'always';

ALTER TABLE dx.tenant_integrations
    ADD COLUMN last_test_at  timestamptz,
    ADD COLUMN last_test_ok  boolean;
