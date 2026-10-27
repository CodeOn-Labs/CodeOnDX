-- =============================================================================
-- V015 - Thresholds of the rule-based insights (AI Insight panel without an LLM,
-- 27/10/2026). Module 'insight', edited in Admin > Rules; switching a rule off
-- hides the insight it drives. Other insights reuse existing rules
-- (HR_*, PROJECT_ENDING_SOON_DAYS, PROJECT_BUDGET_WARNING_THRESHOLD, ATTENDANCE_*).
-- =============================================================================

INSERT INTO dx.system_rules (key, module, value_type, default_value, min_value, max_value, name, name_en, description, description_en, sort_order) VALUES
    ('INSIGHT_FTE_GAP_MIN', 'insight', 'number', '0.5'::jsonb, 0, 10, 'Thiếu FTE so với hợp đồng từ', 'FTE shortfall from',
     'Cảnh báo khi FTE đang phân bổ thấp hơn FTE hợp đồng của dự án từ mức này trở lên', 'Warn when the assigned FTE is below the contract FTE by at least this much', 34),
    ('INSIGHT_MILESTONE_UPCOMING_DAYS', 'insight', 'number', '7'::jsonb, 1, 90, 'Nhắc mốc sắp đến hạn trước (ngày)', 'Upcoming milestone reminder (days)',
     'Nhắc các mốc chưa hoàn thành có ngày dự kiến trong số ngày tới', 'Remind unfinished milestones due within this many days', 35),
    ('INSIGHT_LEAVE_REMAINING_DAYS', 'insight', 'number', '8'::jsonb, 0, 60, 'Nhắc dùng phép khi còn từ (ngày)', 'Leave reminder from (days left)',
     'Từ tháng nhắc phép, nhắc nhân sự còn số ngày phép năm từ mức này trở lên', 'From the reminder month, remind people with at least this many annual leave days left', 36),
    ('INSIGHT_LEAVE_REMINDER_MONTH', 'insight', 'number', '10'::jsonb, 1, 12, 'Tháng bắt đầu nhắc dùng phép', 'Leave reminder month',
     'Từ tháng này trong năm bắt đầu nhắc nhân sự còn nhiều phép', 'Month of the year from which the leave reminder shows', 37),
    ('INSIGHT_EFFORT_OVER_MM', 'insight', 'number', '1'::jsonb, 0.5, 3, 'Công số cảnh báo vượt (MM)', 'Effort overload from (MM)',
     'Cảnh báo nhân sự có công số thực tế + OT trong tháng vượt mức này', 'Warn when actual + overtime effort of a person in the month exceeds this', 38),
    ('INSIGHT_MAX_NAMES', 'insight', 'number', '3'::jsonb, 1, 10, 'Số tên liệt kê tối đa', 'Names listed at most',
     'Số dự án / nhân sự được nêu tên trong một thông báo', 'How many projects / people a notice names', 39)
ON CONFLICT (key) DO UPDATE SET module = EXCLUDED.module, value_type = EXCLUDED.value_type, default_value = EXCLUDED.default_value, min_value = EXCLUDED.min_value, max_value = EXCLUDED.max_value, name = EXCLUDED.name, name_en = EXCLUDED.name_en, description = EXCLUDED.description, description_en = EXCLUDED.description_en, sort_order = EXCLUDED.sort_order;
