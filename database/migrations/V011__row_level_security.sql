-- =============================================================================
-- V011 - Row-level security, second line of defence behind the EF Core global
-- tenant filter (D1).
--
-- How it works
--   * Migrations run as the schema owner (e.g. dx_owner); the API connects as
--     dx_app, which is NOT the owner and has no BYPASSRLS, so policies apply.
--   * Per request the API runs: SELECT set_config('app.tenant_id', '<id>', true)
--     inside the transaction (or false for the whole pooled session, then reset).
--   * No app.tenant_id -> dx.current_tenant_id() is NULL -> no tenant rows visible.
--
-- Not covered (looked up before the tenant is known, or global):
--   tenants, users, user_claims, user_logins, user_tokens, permissions,
--   system_rules, tenant_users, refresh_tokens, account_tokens.
-- =============================================================================

DO $$
DECLARE
    t text;
    excluded text[] := ARRAY['tenant_users', 'refresh_tokens', 'account_tokens'];
BEGIN
    FOR t IN
        SELECT c.table_name
        FROM information_schema.columns c
        JOIN information_schema.tables tb
          ON tb.table_schema = c.table_schema AND tb.table_name = c.table_name
        WHERE c.table_schema = 'dx'
          AND c.column_name = 'tenant_id'
          AND tb.table_type = 'BASE TABLE'
          AND c.table_name <> ALL (excluded)
        ORDER BY c.table_name
    LOOP
        EXECUTE format('ALTER TABLE dx.%I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format(
            'CREATE POLICY tenant_isolation ON dx.%I '
            'USING (tenant_id = dx.current_tenant_id()) '
            'WITH CHECK (tenant_id = dx.current_tenant_id())', t);
    END LOOP;
END
$$;

-- -----------------------------------------------------------------------------
-- Public CSS survey page (/khao-sat-css/:token) has no logged-in tenant: resolve
-- the tenant from the token first, then set app.tenant_id and continue normally.
-- SECURITY DEFINER runs as the owner, which is not subject to RLS.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION dx.find_css_survey_tenant_id(p_token text)
RETURNS uuid
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = dx, pg_temp
AS $$
    SELECT s.tenant_id
    FROM dx.project_css_surveys s
    WHERE s.token = p_token
      AND NOT s.is_revoked
$$;

REVOKE ALL ON FUNCTION dx.find_css_survey_tenant_id(text) FROM PUBLIC;

COMMENT ON FUNCTION dx.find_css_survey_tenant_id(text) IS
    'Returns the tenant of a live CSS survey link, bypassing RLS. Grant EXECUTE to dx_app.';
