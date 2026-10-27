# Cơ sở dữ liệu DX Platform

Lược đồ viết tay theo hướng **DB-first**: script SQL trong `migrations/` là nguồn chuẩn, EF Core sinh entity từ DB (`dotnet ef dbcontext scaffold`) chứ không sinh migration ngược lại. Quyết định này thay cho dòng "migration bằng `dotnet ef migrations add`" ở `docs/handoff/dx-migration-plan.md` mục 4 (chốt 25/10/2026).

## Engine và quy ước

| Hạng mục | Quy ước |
|---|---|
| Engine | **PostgreSQL 18** (đã có trên máy, D8), DB `dx`, schema `dx` |
| Khoá chính | `uuid` mặc định `uuidv7()` (hàm có sẵn từ PG 18, sắp theo thời gian). Riêng `audit_logs.id` là `bigint identity` |
| Tên | Tiếng Anh, `snake_case`, bảng số nhiều; không viết tắt tiếng Việt (bỏ `hd`, `tt_effort`, `f_delete`…) |
| Ràng buộc | `pk_<bảng>`, `fk_<bảng>_<cột>`, `uq_<bảng>_<cột>`, `ix_<bảng>_<cột>`, `ck_<bảng>_<quy_tắc>` (cùng kiểu `EFCore.NamingConventions`) |
| Đa tenant (D1) | Mọi bảng nghiệp vụ có `tenant_id` (FK `tenants`), đứng đầu các index ghép. RLS bật trên 52 bảng (V011) làm lớp phụ sau global query filter của EF Core |
| Audit | `created_at`, `created_by`, `updated_at`, `updated_by` (không FK để khỏi rối, interceptor ghi). Chi tiết thay đổi ở `audit_logs` |
| Xoá mềm | `is_deleted`, `deleted_at`, `deleted_by` ở bảng có thùng rác / khôi phục. Unique index thường là partial `WHERE NOT is_deleted`; riêng mã hiển thị sinh max+1 (`PRJ-xx`, `R-xx`, `CR-xx`) unique cả bản ghi đã xoá |
| Tháng | Cột `period_month date` luôn là ngày 1 của tháng (có CHECK) |
| Giờ chấm công | `time` theo giờ địa phương của tenant (`tenants.time_zone`); mốc thời điểm dùng `timestamptz` |
| Tiền | `numeric(18,2)` + `currency_code char(3)` theo ISO 4217 |
| Trạng thái cố định | `varchar` + CHECK (vd `status IN ('pending','approved',…)`); danh sách tenant được sửa thì nằm ở `master_data` |
| Concurrency | Dùng cột hệ thống `xmin` (ETag), không thêm cột `row_version` |

## Danh sách script

| File | Nội dung |
|---|---|
| `V001__schema_and_functions.sql` | Schema `dx`, hàm `dx.current_tenant_id()` đọc `app.tenant_id` |
| `V002__tenancy_identity_access.sql` | `tenants`, `users` + `user_claims/logins/tokens` (ASP.NET Identity), `tenant_users`, `refresh_tokens`, `account_tokens` (lời mời, quên mật khẩu), `permissions`, `roles`, `role_permissions`, `user_roles` |
| `V003__tenant_configuration.sql` | `master_data`, `tenant_settings`, `system_rules` + `tenant_rules`, `holidays`, `tenant_integrations`, `pcv_template_items`, `css_templates` + `css_template_questions`, `ai_prompt_templates`, `ai_insight_configs` |
| `V004__organization.sql` | `departments`, `teams`, `employees`, `clients` |
| `V005__projects_staffing.sql` | `projects`, `project_contract_staff`, `project_assignments` + `project_assignment_periods`, `project_members`, `project_links`, `project_milestones`, `project_cost_centers` |
| `V006__project_management.sql` | `project_document_groups`, `project_documents`, `project_risks`, `project_change_requests`, `project_issue_links`, `project_status_reports` |
| `V007__time_tracking.sql` | `timesheet_entries`, `leave_requests`, `overtime_requests`, `leave_balances`, `attendance_records` |
| `V008__effort_quality.sql` | `effort_allocations`, `project_quality_metrics`, `project_pcv_items/assessments/results`, `project_css_forms/questions/surveys/answers` |
| `V009__monthly_reports.sql` | `monthly_reports`, `monthly_report_members`, `monthly_report_columns`, `monthly_report_entries` |
| `V010__ai_imports_audit.sql` | `ai_insight_results` (cache insight phía server), `import_jobs`, `audit_logs` |
| `V011__row_level_security.sql` | Policy `tenant_isolation` cho mọi bảng có `tenant_id` (trừ `tenant_users`, `refresh_tokens`, `account_tokens`); hàm `dx.find_css_survey_tenant_id(token)` cho trang khảo sát CSS công khai |
| `V012__seed_global_catalogs.sql` | 22 permission + 33 định nghĩa quy tắc, sinh từ `mock-data/seed` (cùng id), chạy lại được |
| `V013__milestone_justification_audit.sql` | `project_milestones.justification_by` / `justification_at`: ai giải trình mốc trễ, lúc nào (26/10) |
| `V016__integrations_jira_base_bitrix.sql` | `tenant_integrations`: chỉ còn khoá `Jira` / `Base` / `Bitrix` (xoá dòng cũ, đổi CHECK, thêm 3 dòng tắt + mock cho tenant đang có) (27/10) |
| `V015__insight_rules.sql` | 6 quy tắc ngưỡng nhóm `insight` cho AI Insight theo quy tắc (thiếu FTE, mốc sắp đến hạn, nhắc phép, công số vượt, số tên liệt kê) (27/10) |
| `V014__ai_admin_fields.sql` | Prompt AI: `status` / `screen_keys` / `description` / `version`, scope `translate`; `ai_insight_configs.trigger_condition`; `tenant_integrations.last_test_at` / `last_test_ok` (27/10) |

Tổng 62 bảng. Dữ liệu theo tenant (vai trò, danh mục, cài đặt, giá trị quy tắc, biểu mẫu PCV/CSS, prompt) do seeder của BE nạp khi tạo tenant hoặc từ `mock-data/seed` cho tenant demo — không nằm trong migration.

## Cấu hình hoá theo tenant (D12)

Công thức cố định trong code, tham số nằm ở DB:

| Tham số | Lưu ở |
|---|---|
| Mức hiệu suất và hệ số (Học việc 0, Hỗ trợ một phần 0,5, Thử việc 0,8, Chính thức 1…) | `master_data` type `performance_level`, `metadata.coefficient`; `effort_allocations.performance_coefficient` lưu bản chụp lúc lưu để tháng cũ không đổi khi sửa danh mục |
| Số giờ / FTE, giới hạn công, OT, phép, chấm công, ngưỡng PCV / CSS / EE / defect / leakage | `system_rules` (định nghĩa, mặc định, min/max) + `tenant_rules` (giá trị của tenant; không có dòng = dùng mặc định) |
| Giờ làm việc, tuần làm việc, báo cáo tháng song ngữ Nhật, cho phép bấm Vào/Ra, độ dài mật khẩu, hạn lời mời | `tenant_settings` |
| Loại nghỉ (có lương, trừ quỹ phép, cần đính kèm), trạng thái nhân sự (còn làm việc hay không) | `master_data.metadata` |
| Biểu mẫu PCV / CSS | `pcv_template_items`, `css_templates` + `css_template_questions`; chép sang `project_pcv_items`, `project_css_forms/questions` khi tạo dự án |

## Đổi tên so với mock-data / bản cũ

Mock API và DTO giữ tên cũ trong JSON; ánh xạ khi viết BE:

| mock-data (`seed/*.json`) | DB |
|---|---|
| `timesheets` | `timesheet_entries` (`source`: `manual` / `leave_request` / `overtime_request`) |
| `ot_requests`, `day_kind` | `overtime_requests`, `day_type` (thêm `holiday`) |
| `assignments`, `project_assignment_schedules` | `project_assignments`, `project_assignment_periods` |
| `contract_staff.role_code`, `price_per_month` | `project_contract_staff.position_id` (FK `master_data` `job_position`), `monthly_rate` |
| `assignments.role_in_project`, `project_members.role`, `effort_allocations.role` | `position_id` (FK `master_data` `job_position`) |
| `effort_project_centers` | `project_cost_centers` |
| `effort_allocations.hd / tt_effort / ot_effort / performance / allocate_rate / year_month` | `contract_effort / actual_effort / overtime_effort / performance_coefficient / allocation_rate / period_month` |
| `employees.position, position_code, level_code, status, is_active, joined_date, left_date` | `position_id`, `level_id`, `status_id` (FK `master_data` `employee_status`), `hire_date`, `termination_date` |
| `clients.country`, `default_currency` | bỏ `country` (dùng `country_id`), `default_currency_code` |
| `projects.progress_pct`, `contract_staff_fte`, `actual_staff_fte`, `actual_cost_fte` | `progress_percent`; 3 cột FTE không lưu, BE tính |
| `project_risks.name`, `owner_id`, `identified_at` | `title`, `owner_employee_id`, `identified_date` |
| `project_change_requests.file_name` | `title` |
| `project_issue_links.type`, `kind` | `category`, `issue_type` |
| `project_links.provider` | bỏ, lấy từ mã `link_type` |
| `project_css_surveys.scores` (JSON), `survey_month` | bảng `project_css_answers`, `period_month` |
| `project_pcv_results.error_note`, `assess_month` | `note`, `period_month` |
| `project_quality_metrics.metric_month` | `period_month` |
| `monthly_reports.report_month, order_no, responsible, columns, member_ids, removed_employee_ids` | `period_month`, `order_number`, `responsible_person`, bảng `monthly_report_columns`, bảng `monthly_report_members` (`is_removed`) |
| `monthly_report_cells.custom`, `source`, `time_source = attendance` | `monthly_report_entries.custom_values`, `hours_source`, `time_source` |
| `leave_requests.approver_user_id` (cả OT) | `decided_by` |
| `ai_insights.status`, `allowed_roles` | `ai_insight_configs.is_active`, `allowed_role_ids` |
| `integrations.key / enabled` | `tenant_integrations.integration_key / is_enabled` |
| `system_settings` | `tenant_settings` |
| `system_rules` (gộp định nghĩa + giá trị) | `system_rules` (định nghĩa, toàn cục) + `tenant_rules` (giá trị) |
| `css_template` | `css_templates` (1 dòng / tenant) |
| `pcv_template_items.group_name`, `sort_seq` (mọi bảng) | `category`, `sort_order` |
| `users.tenant_id`, `employee_id` | `users` toàn cục + `tenant_users`; liên kết nhân sự qua `employees.user_id` |

## Chạy script

Công cụ đã chốt: **DbUp** trong `Dx.Infrastructure` (25/10/2026). Các file `V*.sql` được nhúng vào assembly và chạy theo thứ tự tên; file đã chạy ghi vào `public.dx_schema_versions` (để ngoài schema `dx` cho scaffold bỏ qua) và không chạy lại.

```bash
cd backend
dotnet run --project src/Dx.Api -- --migrate      # chỉ áp lược đồ
dotnet run --project src/Dx.Api -- --seed-demo    # áp lược đồ + nạp tenant demo (seed/demo_tenant.sql)
```

Lệnh dùng `ConnectionStrings:DxOwner` (role sở hữu schema, vd `dx_owner`) và sau khi áp xong tự cấp quyền DML cho `Database:AppRole` (mặc định `dx_app`). App kết nối bằng `dx_app`: role này **không** được là owner và không có `BYPASSRLS`, nếu không RLS sẽ không có tác dụng. Role / DB tạo ở bước deploy (xem `backend/README.md` và `..\Deployment\README.md`).

`seed/demo_tenant.sql` sinh bằng `node tools/db-seed/generate.mjs` từ `mock-data/seed` (cùng id với mock API, ánh xạ tên theo bảng ở trên, chạy lại được). **Không sửa tay** file này.

Mỗi lần mở kết nối, BE đặt tenant: `SELECT set_config('app.tenant_id', @tenantId, false)` (`TenantConnectionInterceptor`). Job nền chạy từng tenant một theo cách đó.

Đã kiểm chứng 25/10/2026 trên cụm PostgreSQL 18.6 tạm: 12 script chạy sạch trên DB trống (bằng psql và bằng DbUp), tenant demo nạp 2 lần không lỗi, V012 chạy lại được, `uuidv7()` sinh UUID phiên bản 7, `dx_app` không đặt tenant thì không thấy dòng nào, ghi sai tenant bị RLS chặn, tenant này không thấy dữ liệu tenant kia.

## Việc còn mở

- Lịch sử Chat AI (phiên, tin nhắn) chưa có bảng vì mock API hiện không lưu phiên; thêm khi làm màn `ai_chat` ở BE.
- FK giữa các bảng chưa ép cùng tenant ở mức DB (vd `projects.client_id` trỏ sang client của tenant khác); RLS + BE kiểm tra. Nếu muốn chặt hơn thì đổi sang FK ghép `(tenant_id, id)`.
