-- =============================================================================
-- V012 - Global catalogs shared by every tenant: permissions and rule
-- definitions. Generated from mock-data/seed/permissions.json and
-- system_rules.json (same ids as the mock API). Re-runnable (upsert).
-- Per-tenant data (roles, master_data, settings, rule values...) is seeded by
-- the API seeder when a tenant is created, not here.
-- =============================================================================

INSERT INTO dx.permissions (id, key, group_key, name, name_en, sort_order) VALUES
    ('cd0caf0b-dc13-5b69-9fcc-29e8bdf6f7de', 'timesheet.edit', 'timesheet', 'Ghi công cá nhân', 'Log own timesheet', 1),
    ('2fe7cfa6-7f77-5b5f-9418-b178281cdf2d', 'timesheet.team', 'timesheet', 'Xem công số của nhóm', 'View team timesheets', 2),
    ('739f2a4b-7185-54cf-8511-9954c6c047fd', 'timesheet.all_view', 'timesheet', 'Xem tất cả công số', 'View all timesheets', 3),
    ('f3fe0121-b302-5b66-a10f-9abe948f72b4', 'timesheet.all_edit', 'timesheet', 'Sửa công số của người khác', 'Edit others'' timesheets', 4),
    ('2dc6b27e-ecfb-5b67-8710-be1b7a236a80', 'timesheet.approve', 'timesheet', 'Duyệt công số', 'Approve timesheets', 5),
    ('6089dd60-5756-5483-9b3b-6edc9a9637fa', 'project.view', 'project', 'Xem dự án', 'View projects', 6),
    ('d3fc60eb-18f7-51c0-82df-1f6753784cc5', 'project.create', 'project', 'Tạo dự án', 'Create projects', 7),
    ('72312800-bf0f-5cbb-a660-58b32d84f78e', 'project.manage', 'project', 'Quản lý dự án', 'Manage projects', 8),
    ('00590fae-1a44-5bd5-ab0f-9be368648d9c', 'staffing', 'project', 'Phân bổ nhân sự', 'Staffing', 9),
    ('44937fe0-9a9a-50be-9a8f-cb4d619fcc6c', 'employee.view', 'employee', 'Xem nhân sự', 'View employees', 10),
    ('23272434-5a35-55f8-ad1b-21f3a51a950d', 'employee.manage', 'employee', 'Quản lý nhân sự', 'Manage employees', 11),
    ('99ba7b10-bd79-58c8-a61b-59f7965c6b96', 'effort.view', 'effort', 'Xem công số', 'View effort', 12),
    ('ff96f5c3-124b-5645-a4aa-6ef344203dfe', 'effort.edit', 'effort', 'Sửa công số', 'Edit effort', 13),
    ('998c1623-0fd8-5c31-9c0e-68057593a93f', 'pcv.manage', 'quality', 'Quản lý PCV', 'Manage PCV', 14),
    ('4ebdf871-f1e1-5d2e-ae94-b4b4f8ef1966', 'css.manage', 'quality', 'Quản lý CSS', 'Manage CSS', 15),
    ('fdc6c0dc-98d3-51df-ab66-53f911aaa6a0', 'report.basic', 'report', 'Báo cáo tổng quan', 'Basic reports', 16),
    ('498904f3-8b4d-5ca3-abc5-d69fa7bca70c', 'report.finance', 'report', 'Báo cáo tài chính', 'Finance reports', 17),
    ('dabad080-33b0-5604-bc9c-bbeba99ee952', 'report.quality', 'report', 'Báo cáo chất lượng', 'Quality reports', 18),
    ('dfaa2cb9-74a6-552e-a04f-fd84dcf904f6', 'admin.roles', 'admin', 'Quản lý vai trò', 'Manage roles', 19),
    ('414c26dc-aa52-51fe-ab15-af86a9de51ab', 'admin.settings', 'admin', 'Cài đặt hệ thống', 'System settings', 20),
    ('7f92f20e-a101-54e9-81b3-d45265968ad4', 'admin.integrations', 'admin', 'Quản lý tích hợp', 'Manage integrations', 21),
    ('de04f94b-9d76-5b20-8b4b-0272de68f7ef', 'admin.insights', 'admin', 'Quản lý AI Insight', 'Manage AI insights', 22)
ON CONFLICT (key) DO UPDATE SET group_key = EXCLUDED.group_key, name = EXCLUDED.name, name_en = EXCLUDED.name_en, sort_order = EXCLUDED.sort_order;

INSERT INTO dx.system_rules (key, module, value_type, default_value, min_value, max_value, name, name_en, description, description_en, sort_order) VALUES
    ('TIMESHEET_MIN_HOURS', 'timesheet', 'number', '0.25'::jsonb, 0, 24, 'Công số tối thiểu', 'Minimum hours', 'Số giờ tối thiểu của một dòng công', 'Minimum hours of one timesheet line', 1),
    ('TIMESHEET_MAX_HOURS', 'timesheet', 'number', '12'::jsonb, 0, 24, 'Công số tối đa', 'Maximum hours', 'Số giờ tối đa được ghi trong một ngày', 'Maximum hours that can be logged in one day', 2),
    ('TIMESHEET_WEEKLY_MAX_HOURS', 'timesheet', 'number', '60'::jsonb, 1, 168, 'Công số tối đa trong tuần', 'Weekly maximum hours', 'Giới hạn số giờ chấm công tối đa trong một tuần (bao gồm cả OT)', 'Maximum hours logged in a week (including overtime)', 3),
    ('TIMESHEET_EDIT_LIMIT_DAYS', 'timesheet', 'number', '7'::jsonb, 0, 365, 'Giới hạn ngày chỉnh sửa', 'Edit limit (days)', 'Số ngày tối đa cho phép nhân sự chỉnh sửa công số trong quá khứ', 'How many past days employees may still edit their timesheet', 4),
    ('TIMESHEET_LOCK_DATE_OF_MONTH', 'timesheet', 'number', '5'::jsonb, 1, 31, 'Ngày khóa công tháng trước', 'Previous month lock day', 'Khóa hoàn toàn chỉnh sửa công số của tháng trước từ ngày này của tháng hiện tại', 'From this day of the current month, the previous month timesheet is locked', 5),
    ('TIMESHEET_ALLOW_FUTURE', 'timesheet', 'boolean', 'false'::jsonb, NULL, NULL, 'Cho phép nhập công tương lai', 'Allow future entries', 'Cho phép nhân sự điền công số cho các ngày trong tương lai', 'Allow employees to log time for future dates', 6),
    ('TIMESHEET_FTE_HOURS', 'timesheet', 'number', '8'::jsonb, 1, 24, 'Số giờ quy đổi 1 FTE', 'Hours per FTE', 'Số giờ làm việc tương đương 1 FTE (Full-Time Equivalent) trong một ngày', 'Working hours equal to 1 FTE (full-time equivalent) per day', 7),
    ('PROJECT_ENDING_SOON_DAYS', 'project', 'number', '30'::jsonb, 1, 365, 'Ngày sắp kết thúc dự án', 'Ending soon (days)', 'Số ngày trước khi kết thúc để coi dự án là "sắp kết thúc"', 'Days before the end date at which a project is considered "ending soon"', 8),
    ('PROJECT_BUDGET_WARNING_THRESHOLD', 'project', 'number', '75'::jsonb, 1, 100, 'Ngưỡng cảnh báo ngân sách', 'Budget warning threshold', 'Phần trăm ngân sách đã sử dụng để kích hoạt cảnh báo (VD: 75 = 75%)', 'Percentage of budget used that triggers a warning (e.g. 75 = 75%)', 9),
    ('HR_OVERLOAD_ALLOCATION_THRESHOLD', 'hr', 'number', '100'::jsonb, 1, 1000, 'Ngưỡng cảnh báo quá tải', 'Overload threshold', 'Tỷ lệ phân bổ dự án (%) bắt đầu kích hoạt cảnh báo quá tải nhân sự', 'Allocation percentage at which an employee is flagged as overloaded', 10),
    ('HR_MIN_ALLOCATION_THRESHOLD', 'hr', 'number', '20'::jsonb, 1, 1000, 'Ngưỡng định mức tối thiểu', 'Minimum allocation threshold', 'Tỷ lệ phân bổ (%) dưới mức này coi là nhân sự đang chờ việc', 'Allocation percentage below which an employee is considered available', 11),
    ('HR_UTILIZATION_TARGET_PERCENT', 'hr', 'number', '85'::jsonb, 1, 100, 'Tỷ lệ sử dụng mục tiêu', 'Utilization target', 'Tỷ lệ sử dụng nhân sự mục tiêu của công ty (%)', 'Company target for staff utilization (%)', 12),
    ('LEAVE_ANNUAL_DAYS', 'leave', 'number', '12'::jsonb, 0, 60, 'Số ngày phép năm', 'Annual leave days', 'Số ngày phép năm cơ bản của một nhân viên', 'Base annual leave days per employee', 13),
    ('LEAVE_SENIORITY_STEP_YEARS', 'leave', 'number', '5'::jsonb, 1, 50, 'Thâm niên cộng phép', 'Seniority step (years)', 'Cứ đủ số năm làm việc này được cộng thêm 1 ngày phép', 'One extra leave day for every this many years of service', 14),
    ('LEAVE_CARRY_OVER_MAX_DAYS', 'leave', 'number', '5'::jsonb, 0, 60, 'Phép năm cũ chuyển sang', 'Carry-over days', 'Số ngày phép năm cũ tối đa được chuyển sang năm mới', 'Maximum unused leave days carried over to the next year', 15),
    ('LEAVE_MIN_NOTICE_DAYS', 'leave', 'number', '1'::jsonb, 0, 60, 'Báo trước khi nghỉ', 'Notice period (days)', 'Số ngày báo trước tối thiểu khi xin nghỉ phép năm', 'Minimum days of notice for an annual leave request', 16),
    ('OT_MAX_HOURS_PER_DAY', 'overtime', 'number', '4'::jsonb, 0, 24, 'OT tối đa mỗi ngày', 'Max overtime per day', 'Số giờ OT tối đa mỗi ngày', 'Maximum overtime hours per day', 17),
    ('OT_MAX_HOURS_PER_MONTH', 'overtime', 'number', '40'::jsonb, 0, 300, 'OT tối đa mỗi tháng', 'Max overtime per month', 'Số giờ OT tối đa mỗi tháng', 'Maximum overtime hours per month', 18),
    ('OT_MAX_HOURS_PER_YEAR', 'overtime', 'number', '200'::jsonb, 0, 1000, 'OT tối đa mỗi năm', 'Max overtime per year', 'Số giờ OT tối đa mỗi năm', 'Maximum overtime hours per year', 19),
    ('ATTENDANCE_LATE_GRACE_MINUTES', 'attendance', 'number', '15'::jsonb, 0, 240, 'Phút đi muộn bỏ qua', 'Late grace (minutes)', 'Số phút đi muộn được bỏ qua sau giờ bắt đầu làm việc', 'Minutes after the start time that are not counted as late', 20),
    ('ATTENDANCE_EDIT_LIMIT_DAYS', 'attendance', 'number', '7'::jsonb, 0, 365, 'Hạn tự bổ sung giờ vào/ra', 'Self-correction limit (days)', 'Số ngày nhân viên được tự bổ sung giờ vào/ra đã qua', 'How many past days employees may add missing check-in/out times', 21),
    ('QUALITY_PCV_GREEN_MIN', 'quality', 'number', '90'::jsonb, 0, 100, 'PCV đạt (xanh) từ', 'PCV good (green) from', 'Điểm PCV (%) từ mức này trở lên tô xanh', 'PCV score (%) at or above this is shown green', 22),
    ('QUALITY_PCV_ORANGE_MIN', 'quality', 'number', '70'::jsonb, 0, 100, 'PCV cảnh báo (cam) từ', 'PCV warning (orange) from', 'Điểm PCV (%) từ mức này đến dưới mức xanh tô cam; thấp hơn tô đỏ', 'PCV score (%) from this up to the green level is orange; below is red', 23),
    ('QUALITY_CSS_GREEN_MIN', 'quality', 'number', '4.5'::jsonb, 1, 5, 'CSS đạt (xanh) từ', 'CSS good (green) from', 'Điểm CSS (thang 1–5) từ mức này trở lên tô xanh', 'CSS score (1–5) at or above this is shown green', 24),
    ('QUALITY_CSS_ORANGE_MIN', 'quality', 'number', '3.5'::jsonb, 1, 5, 'CSS cảnh báo (cam) từ', 'CSS warning (orange) from', 'Điểm CSS từ mức này đến dưới mức xanh tô cam; thấp hơn tô đỏ', 'CSS score from this up to the green level is orange; below is red', 25),
    ('QUALITY_EE_GREEN_MAX', 'quality', 'number', '100'::jsonb, 1, 500, 'EE tốt (xanh) đến', 'EE good (green) up to', 'EE (%) không vượt mức này tô xanh', 'EE (%) not above this is shown green', 26),
    ('QUALITY_EE_ORANGE_MAX', 'quality', 'number', '110'::jsonb, 1, 500, 'EE cảnh báo (cam) đến', 'EE warning (orange) up to', 'EE (%) trên mức xanh đến mức này tô cam; cao hơn tô đỏ', 'EE (%) above green up to this is orange; above is red', 27),
    ('QUALITY_DEFECT_RATE_GOOD_MIN', 'quality', 'number', '4'::jsonb, 0, 100, 'Defect/MM tốt từ', 'Defect/MM good from', 'Tỷ lệ defect/MM dưới mức này tô cam nhạt (test chưa đủ)', 'Defects per MM below this are light orange (too little testing)', 28),
    ('QUALITY_DEFECT_RATE_GOOD_MAX', 'quality', 'number', '5'::jsonb, 0, 100, 'Defect/MM tốt đến', 'Defect/MM good up to', 'Tỷ lệ defect/MM trong khoảng tốt tô xanh', 'Defects per MM within the good range are green', 29),
    ('QUALITY_DEFECT_RATE_WARN_MAX', 'quality', 'number', '6'::jsonb, 0, 100, 'Defect/MM cảnh báo đến', 'Defect/MM warning up to', 'Trên mức tốt đến mức này tô vàng; cao hơn tô đỏ', 'Above the good range up to this is yellow; higher is red', 30),
    ('QUALITY_LEAKAGE_RATE_GOOD_MIN', 'quality', 'number', '0.45'::jsonb, 0, 10, 'Leakage/MM tốt từ', 'Leakage/MM good from', 'Tỷ lệ lỗi lọt/MM dưới mức này tô cam nhạt', 'Leaked defects per MM below this are light orange', 31),
    ('QUALITY_LEAKAGE_RATE_GOOD_MAX', 'quality', 'number', '0.5'::jsonb, 0, 10, 'Leakage/MM tốt đến (không gồm)', 'Leakage/MM good below', 'Từ mức "tốt từ" đến dưới mức này tô xanh', 'From the good-from level up to (not including) this is green', 32),
    ('QUALITY_LEAKAGE_RATE_WARN_MAX', 'quality', 'number', '0.55'::jsonb, 0, 10, 'Leakage/MM cảnh báo đến', 'Leakage/MM warning up to', 'Đến mức này tô vàng; cao hơn tô đỏ', 'Up to this is yellow; higher is red', 33)
ON CONFLICT (key) DO UPDATE SET module = EXCLUDED.module, value_type = EXCLUDED.value_type, default_value = EXCLUDED.default_value, min_value = EXCLUDED.min_value, max_value = EXCLUDED.max_value, name = EXCLUDED.name, name_en = EXCLUDED.name_en, description = EXCLUDED.description, description_en = EXCLUDED.description_en, sort_order = EXCLUDED.sort_order;
