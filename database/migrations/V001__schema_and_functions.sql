-- =============================================================================
-- V001 - Schema `dx` and shared helper functions
-- Engine: PostgreSQL 18 (uses the built-in uuidv7()).
-- Naming: English, snake_case, plural table names; constraint prefixes
--   pk_ / fk_ / uq_ / ix_ / ck_  (same style EFCore.NamingConventions emits).
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS dx;

-- Tenant of the current request. The API sets it once per connection/transaction:
--   SELECT set_config('app.tenant_id', '<tenant uuid>', true);
-- Returns NULL when not set, so row-level security (V011) hides every tenant row.
CREATE OR REPLACE FUNCTION dx.current_tenant_id()
RETURNS uuid
LANGUAGE sql
STABLE
AS $$
    SELECT nullif(current_setting('app.tenant_id', true), '')::uuid
$$;

COMMENT ON FUNCTION dx.current_tenant_id() IS
    'Tenant of the current session (setting app.tenant_id); used by row-level security policies.';
