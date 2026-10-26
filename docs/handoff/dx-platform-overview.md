> Bản sao của tài liệu `claude/dx-platform-overview.md` trong claude.ai Project "CodeOn Labs" (chép sang ngày 20/10/2026). Đây là phân tích **bản React + Supabase gốc**, nay nằm ở `D:\CodeOnLabs\DXOld`.

# DX Platform (bản gốc) — Tổng quan nghiệp vụ, công nghệ, kiến trúc

> Nguồn khảo sát: `D:\CodeOnLabs\DXOld` (nhánh `dev`, commit `ef3e302`, 14/10/2026). Khảo sát ngày 19/10/2026.
> Các nhận định về bảo mật DB dựa trên SQL trong repo — cần đối chiếu với DB thật đang chạy.

## 1. Nghiệp vụ

**Sản phẩm:** DX Platform / DXAI bản gốc — ERP nội bộ cho một công ty outsourcing IT (thị trường Việt Nam – Nhật Bản), giả định 50–100 nhân viên, 5–10 dự án, khách hàng là doanh nghiệp lớn Nhật. Điểm khác biệt: **AI Insight trên mỗi màn hình** và **Chat AI** hỏi đáp trên dữ liệu công ty.

### Các phân hệ (theo route thực tế trong `src/app/router.tsx`)

| Phân hệ | Route | Nội dung nghiệp vụ |
|---|---|---|
| Chấm công | `/timesheets` | Nhập công theo ngày / tuần; rules engine (min/max giờ ngày, max giờ tuần, số ngày được sửa, ngày khoá kỳ, cho phép ngày tương lai); trạng thái `draft → approved / rejected`; duyệt theo nhóm nhân viên; đồng bộ giờ OT / nghỉ phép từ hệ thống RON |
| Công số (Effort) | `/effort` | Lưới phân bổ công số theo tháng × Trung tâm (TT): HĐ (công số hợp đồng charge khách), TT Effort, OT Effort, Performance (0 / 0.5 / 0.8 / 1.0 theo HV/CT1/TV/CT2), Allocate rate; Actual = TT Effort × Allocate rate (kế toán dùng tính chi phí), Actual1 (đã quy đổi performance), Free, Free1 = Actual − HĐ |
| Dự án | `/projects`, `/projects/new`, `/projects/:id`, `/projects/:id/edit` | Danh sách + KPI; tạo/sửa dự án (khách hàng, cấp độ S/A/B/C, ngân sách, link Jira/Git); kế hoạch nhân sự hợp đồng (Contract Staff: role, level, số lượng, FTE) → gán người thực tế (Assignment + lịch FTE theo khoảng, replace/handover) |
| — Chi tiết dự án: Tổng quan | `?tab=overview` | KPI, bảng assignment, biểu đồ workload/FTE dạng trading chart |
| — Báo cáo tháng | `?tab=monthly-report` | 作業報告書: 1 báo cáo / dự án × tháng, mỗi nhân sự 1 sheet; giờ lấy từ timesheet đã duyệt + check-in/out từ RON; cột tuỳ biến; song ngữ vi/ja (dịch bằng AI); xuất Excel / PDF; panel Insight AI |
| — Metrics | `?tab=metrix&subtab=…` | Timeline (milestone, Timeliness), EE, PCV, CSS, Defect, Leakage |
| — Quản trị dự án | `?tab=management&subtab=…` | Quản lý tài liệu, rủi ro, change request, issue |
| Nhân sự | `/admin/employees` | Danh sách / chi tiết nhân viên, metrics phân bổ (quá tải / chờ việc), sinh tài khoản LDAP, export; dữ liệu gốc đồng bộ từ HRM |
| Chat AI | `/ai` + side panel | Agent có tool-calling đọc dữ liệu dự án / nhân sự / timesheet, render UI động trong chat |
| Quản trị | `/admin/*` | Roles & Permissions, System Rules, AI Insights + Prompt Templates, Integrations (GitHub, GitLab, Google Drive), Danh mục, Khách hàng, Cài đặt chung, Biểu mẫu tiêu chí PCV, Biểu mẫu CSS, Ngôn ngữ |
| Khảo sát CSS công khai | `/khao-sat-css/:token` | Khách hàng chấm điểm không cần đăng nhập (token 64 hex, nộp 1 lần) |

### Chỉ số nghiệp vụ

| Chỉ số | Định nghĩa |
|---|---|
| FTE | 1.0 = 1 người toàn thời gian (8h/ngày) |
| EE (Effort Efficiency) | Actual / HĐ × 100%, theo tháng và luỹ kế năm |
| PCV | Số tiêu chí Đạt / (Đạt + Không đạt) × 100%, mỗi dự án tối đa 1 đánh giá / tháng; "Chưa chọn" bị loại khỏi cả tử và mẫu |
| CSS | Điểm hài lòng khách hàng 1–5 = tổng điểm câu đã chấm / số câu đã chấm; 1 khảo sát / dự án / tháng |
| Defect rate | Defect tìm được trong test nội bộ (UT/IT/ST) / Product size (hoặc / Total billable MM) |
| Leakage | Defect tìm được sau khi bàn giao UAT / Product size (hoặc / MM) |
| Timeliness | % milestone hoàn thành đúng hạn trong khoảng ngày |
| Project grade | S/A/B/C, tính bằng DB function `calculate_project_grade` |

Quyết định thiết kế đã chốt (ADR): Insight kế thừa trang → tab → subtab (0001); Checklist PCV của dự án là bản sao độc lập của biểu mẫu (0002); Khảo sát CSS công khai bảo vệ bằng token qua RPC `SECURITY DEFINER` (0003). Thuật ngữ chuẩn: `GLOSSARY.md`.

### Phân quyền (RBAC)
1 user = 1 role; role = tập permission. 21 permission key: `timesheet.edit|team|all_view|approve`, `project.view|create|manage`, `staffing`, `employee.view|manage`, `effort.view|edit`, `pcv.manage`, `css.manage`, `report.basic|finance|quality`, `admin.roles|settings|integrations|insights`. Role tên `Admin` được bypass mọi quyền trong `has_permission`. FE chặn route bằng `PermissionRoute`, ẩn nút bằng `PermissionGate`; DB chặn bằng RLS + `has_permission()`.

## 2. Công nghệ

| Lớp | Công nghệ |
|---|---|
| Frontend | React 19, TypeScript ~6.0, Vite 8 (Rolldown), React Router 7 (khai báo route trong code, lazy-load theo page) |
| UI | Tailwind CSS 4, Radix UI, Lucide icons, framer-motion; design tokens "DXAI-V2" (tham khảo phong cách Supabase) |
| State / dữ liệu | Redux Toolkit + RTK Query (`baseApi.injectEndpoints`, tag-based invalidation); React Context cho Auth / Permission / AIConfig |
| Form | React Hook Form + Zod 4 |
| Backend | Supabase: PostgreSQL (schema `dx_platform`), Auth (SSO Keycloak), Storage, Realtime, RPC; 4 Edge Function Deno |
| AI | Vercel AI SDK v6 (`ai`, `@ai-sdk/openai-compatible`, `@ai-sdk/react`) — mọi provider đi qua giao thức OpenAI-compatible (mặc định DeepSeek; hỗ trợ Gemini/Claude/OpenAI/Groq/Ollama); `@json-render/*` render generative UI; Handlebars cho prompt template |
| i18n | i18next, vi + en đầy đủ (14 namespace); ja chỉ có 1 file |
| Test | Vitest 4 + Testing Library + jsdom (170 file test, ngưỡng coverage 80%); Deno test cho edge function |
| Hạ tầng | Docker (Node build → Nginx tĩnh), GitLab CI → GCP Artifact Registry → migrate DB + edge function → ArgoCD/Helm |

## 3. Kiến trúc

```
Browser (React SPA, Nginx)
 ├─► Supabase REST/RPC/Realtime/Storage (anon key + JWT người dùng, RLS)   ← toàn bộ nghiệp vụ
 ├─► Supabase Edge Functions (Deno): sync-hrm, sync-ron, ron-attendance, dx-hello
 │      └─► HRM API (phòng ban, team, nhân viên) · RON API (OT, nghỉ phép, check-in/out)
 ├─► LLM provider OpenAI-compatible — gọi TRỰC TIẾP từ browser (dev qua Vite proxy /ai-api)
 ├─► GitLab / GitHub / Google Drive (OAuth; Google token qua Nginx proxy /api/oauth-proxy/google)
 └─► Keycloak (SSO qua Supabase Auth)
```

- **Không có backend riêng**: business logic nằm ở `*.service.ts` phía client và ở các RPC PL/pgSQL (phần ghi quan trọng như `save_timesheet_week`, báo cáo tháng, PCV, CSS đi qua RPC).
- **Luồng dữ liệu bắt buộc**: component → hook RTK Query (`*.api.ts`) → `*.service.ts` → supabase client. Component không gọi service/supabase trực tiếp.
- **Cấu trúc thư mục**: `src/app` (router), `src/components` (layout, auth guard), `src/modules/<15 module>` (mỗi module: pages, components, services, schemas, types, hooks, utils), `src/shared` (component/hook/lib dùng chung: CommonTable, Form*, ImportWizard 5 bước, Export qua Web Worker, rulesEngine, AI lib), `src/services` (supabase client, baseApi), `src/store`, `src/i18n`.
- **Quy ước DB**: type phản chiếu cột snake_case; cột audit `created_at/updated_at/created_by/updated_by/f_delete` (xoá mềm); mã hiển thị (CR-01, R-01…) sinh max+1 kể cả bản ghi đã xoá; service UPDATE/DELETE phải `.select()` và kiểm tra số dòng (RLS chặn im lặng).
- **AI Insight**: admin cấu hình insight (màn hình áp dụng, nguồn dữ liệu, điều kiện, role, prompt) trong `ai_insights`; màn hình lấy context qua RPC `get_ai_context*` → compile prompt template → gọi LLM (JSON mode) → render `InsightPanel`; cache localStorage TTL 1h.
- **AI Chat**: `ToolLoopAgent` với 4 tool (`getDxContextSummary`, `queryProjectsView`, `queryEmployeesView`, `queryTimesheetsView`), tối đa 5 bước, instructions lấy từ `ai_prompt_templates`.

### Database (`database/dx_platform/schema/`)
Chia lớp `01_tables` (86 file, 68 bảng) · `02_functions` (43 file, ~65 function/RPC) · `03_views` (2) · `04_triggers` (9) · `05_policies` (15) · `06_indexes` · `07_seed_data` (21). `npm run migrate` chạy theo thứ tự thư mục rồi tên file, lưu lịch sử vào bảng `migrations`.

Nhóm bảng: nghiệp vụ dự án (`projects`, `contract_staff`, `assignments`, `project_assignment_schedules`, `project_milestones`, `project_documents`, `project_risks`, `project_change_requests`, `project_issue_links`, `project_quality_metrics`, `project_reports`), báo cáo tháng (`monthly_reports` + 4 bảng con), PCV (`pcv_template_items`, `project_pcv_*`), CSS (`css_template*`, `project_css_*`), công số (`effort_allocations`, `effort_project_centers`), nhân sự & tổ chức (`employees`, `departments`, `teams`, `profiles`, `user_identity`), chấm công (`timesheets`, `ron_sync_runs`), RBAC (`roles`, `permissions`, `role_permissions`, `user_roles`), hệ thống (`system_settings`, `system_rules`, `system_rule_values`, `audit_logs`, `import_jobs`, `hrm_sync_runs`), AI & tích hợp (`ai_insights`, `ai_prompt_templates`, `integrations`, `workspace_integrations`…), master data (`master_*`).

## 4. Thư viện đính kèm (package.json)

| Nhóm | Thư viện |
|---|---|
| Core | react, react-dom 19.2 · react-router-dom 7.15 · typescript ~6.0 · vite 8 |
| UI | tailwindcss 4.3 (+ @tailwindcss/vite, tailwindcss-animate) · @radix-ui/* (alert-dialog, dialog, dropdown-menu, label, popover, select, separator, slot) · lucide-react · framer-motion 12 · clsx, tailwind-merge · react-resizable-panels · react-day-picker 10 |
| Bảng / kéo thả | @tanstack/react-table 8 · @tanstack/react-virtual 3 · @dnd-kit/core, sortable, utilities |
| State & form | @reduxjs/toolkit 2.12 · react-redux 9 · react-hook-form 7 · @hookform/resolvers · zod 4 |
| Backend | @supabase/supabase-js 2.106 (edge function dùng bản 2.39.3 qua esm.sh) |
| AI | ai 6 · @ai-sdk/openai-compatible · @ai-sdk/react · @json-render/core, react, shadcn · streamdown, @streamdown/code · handlebars |
| Markdown | react-markdown · remark-gfm · rehype-raw · dompurify · @uiw/react-md-editor |
| Biểu đồ | recharts 3 · lightweight-charts 5 |
| Import/Export | exceljs · xlsx 0.18.5 · papaparse · jspdf 4 · html-to-image |
| Ngày giờ / i18n | date-fns 4 · i18next 26, react-i18next 17, i18next-browser-languagedetector |
| Dev / test | vitest 4 (+ coverage-v8) · @testing-library/react, dom, jest-dom · jsdom · eslint 10 + typescript-eslint + react-hooks/react-refresh · pg (migration) · Deno 2 (edge function) |

## 5. Quy mô & tình trạng mã nguồn
- 772 file TS/TSX, ~99.500 dòng; module `projects` chiếm ~43.000 dòng (369 file); `ProjectDetailPage.tsx` ~1.300 dòng.
- 956 commit từ 14/04/2026, 26 người đóng góp; đội phát triển dùng AI coding (CONTEXT.md, skills-lock.json, docs/plans).
- Không có lockfile (`package-lock.json` bị gitignore), `.npmrc` bật `force` + `legacy-peer-deps`.

## 6. Rủi ro & điểm cần xử lý

**Bảo mật (ưu tiên cao)**
1. Lịch sử git có chứa một JWT `service_role` của Supabase (bỏ qua toàn bộ RLS) → cần **rotate key** và cân nhắc làm sạch lịch sử. Thư mục `scratch/` và `scripts/` còn hardcode anon key.
2. Trong repo, 31/68 bảng không bật RLS (gồm `user_roles`, `role_permissions`, `roles`, `system_settings`, `employees`, `ai_insights`…), trong khi `000_init_schema.sql` cấp mặc định SELECT/INSERT/UPDATE/DELETE cho `anon` và `authenticated`. Nếu DB thật giống repo: người dùng đã đăng nhập có thể tự thêm mình vào role `Admin` (mà `has_permission` bypass theo tên role).
3. AI API key nằm ở browser (biến `VITE_AI_API_KEY` nhúng vào bundle, hoặc `system_settings.ai_api_key` đọc từ client). Kế hoạch chuyển AI sang edge function (`docs/plans/ai_edge_migration_plan.md`) chưa được triển khai.
4. OAuth client secret và access/refresh token (GitLab, Google Drive) được xử lý ở browser và lưu trong `workspace_integrations.ws_config`; comment "client_secret never in the browser" trong `GitLabExplorer.tsx` không đúng với code.
5. Một số route admin (`rules`, `categories`, `clients`, `language`) không có `PermissionRoute` riêng.
6. RPC `SECURITY DEFINER` (EE, báo cáo tháng…) chỉ kiểm tra `project.view` toàn cục, không kiểm tra thành viên dự án.

**Kỹ thuật**
7. Build không tái lập được (không lockfile, force install); Dockerfile dùng Node 20 trong khi Volta pin Node 24; `build:docker` bỏ qua `tsc`.
8. Trùng tiền tố số migration (074 ×3, 075, 076, 078, 080 ×2…); thứ tự chỉ dựa tên file.
9. Tài liệu lệch code: CONTEXT.md nhắc module `bug-report` (đã không còn), thiếu module `effort`, nói "không có backend" dù đã có edge function; ARCHITECTURE.md / HYBRID_ARCHITECTURE.md mô tả module không tồn tại và trỏ đường dẫn máy Mac cá nhân.
10. Giá trị trạng thái `submitted` của timesheet đã bỏ ở bảng nhưng vẫn còn trong một số RPC.

## 7. Khoảng cách so với mục tiêu SaaS ERP cho SME
- **Đơn tenant**: không có `tenant_id`/`organization_id`; RLS chỉ theo permission; schema cố định `dx_platform`; `workspace_id` ở bảng integration chưa có bảng workspace.
- **Gắn chặt hệ thống nội bộ của công ty gốc**: SSO Keycloak, HRM, RON, cách tính công số theo hướng dẫn nội bộ (TT, performance HV/CT1/TV/CT2), báo cáo tháng kiểu 作業報告書 cho khách Nhật, PCV/CSS theo quy trình riêng.
- **Thiếu lớp backend** cho bí mật (AI key, OAuth secret), billing/subscription, provisioning tenant, rate-limit, audit đa tenant.
- Ưu tiên khi chuyển SaaS: (1) mô hình tenant + RLS theo tenant; (2) dời AI và OAuth sang edge function/backend; (3) trừu tượng hoá nguồn nhân sự / chấm công (HRM, RON → connector cấu hình được); (4) tách các nghiệp vụ đặc thù của công ty gốc thành cấu hình hoặc module tuỳ chọn.
