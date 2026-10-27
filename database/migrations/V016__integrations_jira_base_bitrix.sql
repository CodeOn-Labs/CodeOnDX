-- =============================================================================
-- V016 - Admin > Integrations now lists Jira, Base and Bitrix (27/10/2026)
-- instead of Ai, GitHub, GitLab, GoogleDrive and Email. The AI and e-mail
-- connectors stay configured on the server only (Integrations:Ai /
-- Integrations:Email in appsettings), so their rows here are no longer used.
-- =============================================================================

DELETE FROM dx.tenant_integrations WHERE integration_key NOT IN ('Jira', 'Base', 'Bitrix');

ALTER TABLE dx.tenant_integrations DROP CONSTRAINT ck_tenant_integrations_key;
ALTER TABLE dx.tenant_integrations
    ADD CONSTRAINT ck_tenant_integrations_key CHECK (integration_key IN ('Jira', 'Base', 'Bitrix'));

-- Existing tenants get the three sources switched off, in mock mode
INSERT INTO dx.tenant_integrations (tenant_id, integration_key, is_enabled, use_mock)
SELECT t.id, k.integration_key, false, true
FROM dx.tenants t
CROSS JOIN (VALUES ('Jira'), ('Base'), ('Bitrix')) AS k (integration_key)
ON CONFLICT (tenant_id, integration_key) DO NOTHING;
