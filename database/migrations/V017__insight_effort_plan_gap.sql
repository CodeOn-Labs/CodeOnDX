-- =============================================================================
-- V017 - Rule-based insight on the Effort screen (30/10/2026): a person whose
-- Actual (tt_effort) differs from the Plan (hd) on an effort row of the month
-- by more than INSIGHT_EFFORT_PLAN_GAP_MM (default 0.2 MM, up or down) is
-- listed as "far off plan". Module 'insight', edited in Admin > Rules;
-- switching the rule off hides the notice.
-- =============================================================================

INSERT INTO dx.system_rules (key, module, value_type, default_value, min_value, max_value, name, name_en, description, description_en, sort_order) VALUES
    ('INSIGHT_EFFORT_PLAN_GAP_MM', 'insight', 'number', '0.2'::jsonb, 0, 2, 'Kế hoạch lệch thực tế trên (MM)', 'Plan vs actual gap above (MM)',
     'Cảnh báo nhân sự có dòng công số trong tháng mà Thực tế lệch Kế hoạch (tăng hoặc giảm) nhiều hơn mức này', 'Warn when a person has an effort row this month whose Actual differs from the Plan (up or down) by more than this', 40)
ON CONFLICT (key) DO UPDATE SET module = EXCLUDED.module, value_type = EXCLUDED.value_type, default_value = EXCLUDED.default_value, min_value = EXCLUDED.min_value, max_value = EXCLUDED.max_value, name = EXCLUDED.name, name_en = EXCLUDED.name_en, description = EXCLUDED.description, description_en = EXCLUDED.description_en, sort_order = EXCLUDED.sort_order;
-- Tenants without a tenant_rules row use default_value (RuleService), so existing tenants get the notice at 0.2 MM.
