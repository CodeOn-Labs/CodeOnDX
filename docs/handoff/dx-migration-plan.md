> Bản sao của tài liệu `claude/dx-migration-plan.md` trong claude.ai Project "CodeOn Labs" (chép sang ngày 20/10/2026). Ánh xạ đường dẫn: `D:\CodeOnLabs\DX\codeon-dx` = `D:\CodeOnLabs\DX`; `claude/<tên>.md` = `docs/handoff/<tên>.md` (tài liệu License/Support nằm trong `D:\CodeOnLabs\License\docs\handoff\`, `D:\CodeOnLabs\Support\docs\handoff\`). Khi chép đã sửa một chỗ lỗi thời: dòng Support trong bảng cổng mục 6.1 (Support nay chạy native bằng scheduled task, cổng 8081 đã chốt).

# Kế hoạch chuyển đổi DX Platform → Vue 3 + .NET 10 + PostgreSQL

> Lập ngày 19/10/2026. Đầu vào: mã nguồn bản gốc `D:\CodeOnLabs\DXOld` (xem `claude/dx-platform-overview.md`), stack sẵn có của CodeOn trong `claude/codeon-license-overview.md`.
> Mục tiêu: viết lại DX Platform trên **Vue 3 + TypeScript (FE)**, **ASP.NET Core .NET 10 REST API (BE)**, **PostgreSQL**; chạy trên máy `phamthehien` (Windows x64) và public tại **https://dx.codeonlabs.vn** để khách hàng kiểm thử.
> Các mục đánh dấu **[Chốt]** là quyết định cần xác nhận trước khi bắt đầu.
> **Cập nhật 20/10/2026:** đã chốt 3 quy ước (i18n snake_case theo màn hình + hành động, response `ErrorFullModel`, connector + `mock-data/`) — đặc tả và code mẫu ở `claude/dx-conventions.md`; DX dùng cổng `127.0.0.1:8082` và tunnel dùng chung với License/Support trên cùng máy (mục 6).
> **Cập nhật 20/10/2026 (4):** chốt D4 — **bỏ hẳn kết nối HRM, RON, Keycloak**; nhân sự, phòng ban/team, tài khoản đăng nhập, nghỉ phép, OT, chấm công vào/ra thành tính năng tự quản lý trong phần mềm (mục 3.6). Sản phẩm không phụ thuộc hệ thống bên ngoài nào.
> **Cập nhật 20/10/2026 (3):** chốt D1 = đa tenant, D9 = Cloudflare Tunnel.
> **Cập nhật 20/10/2026 (2):** đã bắt đầu chuyển FE tại `D:\CodeOnLabs\DX\codeon-dx` — thư viện UI chốt **Reka UI** thay PrimeVue để giữ nguyên giao diện (mục 3.3), FE chạy trước với **mock API** cùng hợp đồng `/api/v1` (mục 3.5), tiến độ ở mục 8.

---

## 1. Hiện trạng cần chuyển

| Hạng mục | Quy mô | Cách chuyển |
|---|---|---|
| UI React (`.tsx`) | ~48.700 dòng | Viết lại thành Vue SFC |
| TS thuần, không phụ thuộc React (utils, lib, schemas, types, constants) | 155/158 file, ~11.000 dòng | **Chép sang gần như nguyên vẹn** (công thức EE, effort, rules engine, monthly-report summary, Zod schema…) |
| Unit test logic (`*.test.ts`) | 115 file | Chạy lại bằng Vitest trong dự án Vue |
| Service gọi Supabase (`*.service.ts`) | ~8.400 dòng | Chuyển logic sang BE C#; FE chỉ còn API client sinh tự động |
| Endpoint RTK Query | 166 (67 query, 99 mutation) | Gom thành ~120–150 REST endpoint |
| PL/pgSQL (RPC, trigger) | ~5.400 dòng, ~65 function, 9 trigger | Chuyển sang Application service C#; query tổng hợp nặng giữ dạng SQL |
| Bảng DB | 68 | Thiết kế lại có `tenant_id`, migration bằng EF Core |
| Edge function Deno | 4 (sync-hrm, sync-ron, ron-attendance, dx-hello) | **Bỏ** — thay bằng tính năng tự quản lý trong phần mềm (mục 3.6) |
| Locale | vi, en đủ (14 namespace theo module, 2.387 key/ngôn ngữ, 1.918 key camelCase); ja 1 file | Script tìm màn đang dùng từng key → đề xuất key mới theo `screen_key` + tiền tố hành động, duyệt qua `mapping.csv` (Q1) |

---

## 2. Quyết định kiến trúc

| # | Quyết định | Đề xuất mặc định | Lý do |
|---|---|---|---|
| D1 ✅ | Đa tenant ngay từ đầu? | **Có** (chốt 20/10) — 1 DB, chung schema, cột `tenant_id` + global query filter EF Core; bật thêm PostgreSQL RLS theo `app.tenant_id` làm lớp bảo vệ thứ hai | Mục tiêu dự án là SaaS cho SME; thêm tenant về sau tốn hơn nhiều lần so với làm ngay |
| D2 | Xác thực | ASP.NET Core Identity — tài khoản quản lý ngay trong phần mềm (mời qua email, quên mật khẩu, khoá khi nghỉ việc); JWT access 15 phút, refresh token xoay vòng lưu **cookie httpOnly/Secure/SameSite=Strict**; không dùng SSO ngoài (có thể thêm đăng nhập Google / Microsoft sau nếu khách yêu cầu) | Bỏ Supabase Auth + Keycloak; tránh lưu token ở localStorage (lỗi đang có ở CodeOnLicense) |
| D3 | Phân quyền | Giữ 21 permission key hiện có; role theo tenant; `[HasPermission("project.view")]` (policy provider tự viết); **kiểm tra thêm quyền theo dự án** (thành viên / PM) cho tài nguyên của dự án | Sửa lỗ hổng: RPC hiện chỉ kiểm tra `project.view` toàn cục, role tên `Admin` được bypass |
| D4 ✅ | Phạm vi tính năng | Tương đương nhánh `dev` hiện tại; **bỏ hẳn** đồng bộ HRM, RON và đăng nhập Keycloak — thay bằng Phòng ban & Team, Nhân sự, Tài khoản, Nghỉ phép & OT, Chấm công vào/ra tự quản lý (mục 3.6); nút bug-report GitLab đã bị gỡ từ bản cũ (chốt 20/10) | Sản phẩm SaaS độc lập cho SME, không phụ thuộc hệ thống bên ngoài |
| D5 | AI | Mọi lời gọi LLM đi qua BE (`Microsoft.Extensions.AI`, endpoint tương thích OpenAI); khoá API chỉ ở server; stream bằng SSE; cache insight phía server | Hiện khoá AI lộ ở trình duyệt |
| D6 | Cấu trúc BE | Clean Architecture 4 project (giống CodeOnLicense) + chia thư mục theo tính năng; **không dùng MediatR / AutoMapper** (bản mới chuyển giấy phép thương mại; AutoMapper 14 dính CVE-2026-32933) | Đội đã quen cấu trúc này; tránh rủi ro giấy phép |
| D7 | Hợp đồng API | Contract-first: BE sinh OpenAPI → **orval** sinh type TS + hook TanStack Vue Query cho FE | FE/BE luôn khớp kiểu dữ liệu, giảm code tay |
| D8 | Triển khai thử nghiệm | Chạy native trên Windows: PostgreSQL service + API chạy dạng Windows Service (phục vụ luôn SPA) + tunnel ra Internet | Tự khởi động sau reboot, không cần đăng nhập Windows; máy chưa có Docker |
| D9 ✅ | Cách public `dx.codeonlabs.vn` | **Cloudflare Tunnel** (phương án A mục 6.3, chốt 20/10) — chuyển nameserver `codeonlabs.vn` từ iNET sang Cloudflare (gói Free), một tunnel cho cả máy | Miễn phí, không mở cổng router, ẩn IP máy, có Cloudflare Access cho môi trường test |
| D10 ✅ | Dữ liệu cho môi trường kiểm thử | **Chỉ** dữ liệu hư cấu trong `mock-data/seed` (Q3, chốt 20/10); không đưa dữ liệu nhân sự thật lên máy public | Máy cá nhân mở ra Internet; dữ liệu HRM là dữ liệu cá nhân |
| D11 ✅ | Thư viện UI FE | **Reka UI 2** (bản Vue của Radix UI) + Tailwind CSS 4 + class gốc của DX; icon `@lucide/vue` | Bản React dùng Radix + Tailwind: giữ nguyên cấu trúc `data-state` và hiệu ứng → giao diện gần như y hệt; PrimeVue buộc dựng lại toàn bộ style (chốt 20/10) |
| D12 ✅ | Nghiệp vụ đặc thù của bản gốc | **Cấu hình hoá theo tenant**: giữ công thức, nhưng tên mức performance (HV/CT1/TV/CT2…) và hệ số, cách tính công số TT/OT, mẫu báo cáo tháng (song ngữ vi/ja là tuỳ chọn), biểu mẫu PCV/CSS là danh mục / cài đặt do quản trị tenant chỉnh; seed tenant demo dùng bộ mặc định (chốt 20/10) | Sản phẩm SaaS cho nhiều SME, không gắn quy trình của một công ty |
| Q1 ✅ | Đa ngôn ngữ | Key snake_case **theo màn hình + hành động**: FE `<screen_key>.<prefix>_<tên>` (vd `project_list.btn_create`), BE `<chức_năng>_<hành_động>.msg_success / err_<lý_do>`; mỗi màn chỉ dùng key của mình + 6 vùng chung; danh mục màn ở `shared/screens.json`; 1 file JSON / ngôn ngữ mỗi phía; `vi` mặc định và dự phòng | Đã chốt 20/10 — tránh xung đột key giữa các màn |
| Q2 ✅ | Response API | Mọi action có try/catch; mọi response dùng `ErrorFullModel { Status, MessageText, Data }`; HTTP status = `Status`; `MessageText` dịch theo `Accept-Language` | Đã chốt 20/10 — thay cho ProblemDetails |
| Q3 ✅ | Kết nối ngoài | 5 connector (AI, GitHub, GitLab, Google Drive, Email) có sẵn section `Integrations:*` + cờ `UseMock`; dữ liệu giả lập ở `mock-data/` | Đã chốt 20/10 (HRM, RON, SSO bỏ theo D4) |

---

## 3. Kiến trúc đích

```
Internet (khách kiểm thử)
   │ https://dx.codeonlabs.vn
   ▼
Tunnel dùng chung của máy (Cloudflare Tunnel) ── Windows Service, chỉ nối ra ngoài
   │ http://127.0.0.1:8082   (8080: CodeOnLicense, 8081: CodeOn Support)
   ▼
Dx.Api (ASP.NET Core .NET 10, Kestrel, Windows Service)
 ├─ /            → SPA Vue 3 (wwwroot, MapFallbackToFile)
 ├─ /api/v1/*    → REST controllers  ── Application services ── EF Core 10 / Npgsql
 ├─ /hubs/*      → SignalR (tiến độ import, thông báo)
 ├─ /jobs        → Hangfire dashboard (chỉ admin)
 ├─ AI gateway   → LLM provider (DeepSeek / OpenAI / Gemini qua OpenAI-compatible)
 └─ Connector AI · GitHub · GitLab · Drive · Email ── UseMock=true → đọc mock-data/
   │
   ▼
PostgreSQL 18 (Windows service, chỉ nghe localhost)     File storage: D:\dx-data\files (IFileStorage)
```

### 3.1 Backend — solution `CodeOn.Dx`

| Project | Nội dung |
|---|---|
| `Dx.Domain` | Entity, value object, enum, domain event; không phụ thuộc gì |
| `Dx.Application` | Theo tính năng: `Identity` (đăng nhập, mời, quên/đặt mật khẩu, khoá tài khoản), `Tenancy`, `Organization` (phòng ban, team, nhân viên), `Leave` (đơn nghỉ phép, đơn OT, quỹ phép, ngày lễ), `Attendance` (giờ vào/ra, import file máy chấm công), `MasterData`, `Clients`, `Projects` (core, staffing, milestones), `ProjectManagement` (tài liệu, rủi ro, CR, issue), `Timesheets`, `Effort`, `Quality` (EE, PCV, CSS, defect, leakage), `MonthlyReports`, `AiInsights`, `AiChat`, `Integrations`, `Settings`, `SystemRules`, `Imports`. Mỗi tính năng: `Commands/`, `Queries/`, `Dtos/`, `Validators/` — handler là class thường, đăng ký DI |
| `Dx.Infrastructure` | `DxDbContext` (EF Core 10, snake_case), cấu hình Fluent API, migrations, interceptor audit + soft delete, tenant filter, Identity store, file storage, `JsonStringLocalizer`, Hangfire job; `Integrations/<Key>/` gồm `Options`, client thật, client mock và `JsonMockDataStore` (Q3) |
| `Dx.Api` | `Program.cs`, `ApiControllerBase` + controllers (try/catch → `ErrorFullModel`, Q2), lưới an toàn cho lỗi ngoài action (401/403/404/429/500), middleware tenant + security headers, `Locales/vi.json`, `en.json`, rate limit, OpenAPI + Scalar, phục vụ SPA |
| `Dx.UnitTests`, `Dx.IntegrationTests` | xUnit v3; integration test bằng `WebApplicationFactory` + PostgreSQL thật (DB test riêng) |

**Thư viện BE (NuGet)**

| Nhóm | Thư viện |
|---|---|
| Nền tảng | ASP.NET Core 10 (controllers, rate limiting, health checks, request localization, OpenAPI có sẵn), Scalar.AspNetCore |
| Dữ liệu | Npgsql.EntityFrameworkCore.PostgreSQL 10, EFCore.NamingConventions (snake_case), Dapper (cho truy vấn báo cáo nặng) |
| Auth | Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.AspNetCore.Authentication.JwtBearer |
| Validation / mapping | FluentValidation, Riok.Mapperly (sinh code lúc build, thay AutoMapper) |
| Job / realtime | Hangfire + Hangfire.PostgreSql; SignalR (có sẵn) |
| AI | Microsoft.Extensions.AI + Microsoft.Extensions.AI.OpenAI (endpoint tuỳ chỉnh), Handlebars.Net (dùng lại cú pháp prompt template đang lưu trong DB) |
| File | ClosedXML (đọc/ghi Excel), QuestPDF (PDF — giấy phép Community miễn phí khi doanh thu < 1 triệu USD/năm) |
| Log | Serilog (console + file xoay vòng), tuỳ chọn Seq |
| Test | xUnit v3, Shouldly (FluentAssertions 8 đã chuyển giấy phép thương mại), NSubstitute |

**Ánh xạ từ Supabase sang .NET**

| Cũ | Mới |
|---|---|
| Client gọi thẳng bảng qua PostgREST + anon key | REST controller, không lộ bảng |
| RLS + `has_permission()` | Policy authorization + kiểm tra quyền theo dự án + tenant filter (RLS chỉ là lớp phụ) |
| RPC PL/pgSQL (`save_timesheet_week`, `*_monthly_report_*`, `save_pcv_assessment`, `submit_css_survey`, `get_project_ee_input`…) | Application service trong transaction EF Core |
| `get_ai_context*` (9 phiên bản chồng nhau) | Một `AiContextBuilder` theo màn hình áp dụng |
| Màn hình áp dụng của Insight dạng PascalCase (`ProjectList`, `ProjectTimeline`, `ProjectMgmtRisk`, `HRList`, `EffortList`…) | `screen_key` snake_case trong `shared/screens.json` (`project_list`, `project_detail_metrics_timeline`, `project_detail_management_risk`, `employee_list`, `effort`), quan hệ cha–con khai báo bằng `parent`; BE tự gộp insight màn hiện tại + màn cha (ADR-0001), FE chỉ gửi `screen` |
| Trigger: copy biểu mẫu PCV/CSS khi tạo dự án, sync FTE dự án, audit | Domain event + `SaveChangesInterceptor` |
| Edge function `sync-hrm` (phòng ban, team, nhân viên từ HRM) | Bỏ — quản trị tự quản lý ở màn Phòng ban & Team và Nhân sự (thêm/sửa/import Excel) |
| Edge function `sync-ron` (OT, nghỉ phép ghi thẳng vào công) | Bỏ — đơn Nghỉ phép / OT trong phần mềm, đơn đã duyệt tự sinh dòng công |
| Edge function `ron-attendance` (giờ vào/ra cho Báo cáo tháng) | Bỏ — bảng Chấm công vào/ra trong phần mềm (nút Vào/Ra, import file máy chấm công, sửa tay có ghi chú) |
| Supabase Auth + Keycloak SSO, trigger `handle_new_user`, RPC `sync_auth_users` | ASP.NET Identity: tài khoản tạo từ hồ sơ nhân sự bằng lời mời qua email |
| `VITE_AI_MOCK_MODE` + `ai.mock.ts` ở FE | `Integrations:Ai:UseMock` ở BE, kịch bản trong `mock-data/ai` |
| Supabase Realtime (`import_jobs`) | SignalR |
| Supabase Storage (avatar, file import) | `IFileStorage` → thư mục local; sau này MinIO/S3 |
| Nginx proxy OAuth Google, secret ở trình duyệt | OAuth flow hoàn toàn ở BE; token mã hoá bằng ASP.NET Data Protection |

### 3.2 Quy ước REST API

- Prefix `/api/v1`; tài nguyên số nhiều, lồng theo sở hữu: `/projects/{id}/milestones`, `/projects/{id}/monthly-reports/{yyyy-MM}`, `/projects/{id}/pcv-assessments/{yyyy-MM}`, `/effort/allocations?month=2026-10&departmentId=…`.
- Hành động nghiệp vụ là sub-resource: `POST /timesheets/approvals`, `PUT /timesheets/weeks/{yyyy-Www}` (lưu cả tuần 1 transaction), `POST /projects/{id}/css-surveys/{month}/link`, trang công khai `GET/POST /public/css-surveys/{token}`.
- **Mọi response** (kể cả lỗi) có dạng `ErrorFullModel`: `{ "status": 200, "messageText": "…", "data": … }` — bảng mã Status, exception nghiệp vụ, `ApiControllerBase` và lưới an toàn xem `claude/dx-conventions.md` (Q2).
- Phân trang `?page=1&pageSize=50` → `data = { items, page, pageSize, totalCount }` (`pageSize=0` = lấy tất cả, dùng cho xuất file); lọc/sắp xếp `?statusIds=…&sort=-startDate`.
- Lỗi validation: `status = 400`, `data = { field: [message] }` để FE gắn vào form; không bao giờ trả `ex.Message` hay stack trace.
- Concurrency: `ETag` / `If-Match` dựa trên `xmin` cho các bản ghi sửa đồng thời (ô báo cáo tháng, lưới công số).
- Mã hiển thị (`CR-01`, `R-01`) giữ quy tắc max+1 tính cả bản ghi đã xoá + unique index `(tenant_id, project_id, code)`.
- Số liệu tính từ nhiều bảng (vd `% budget đã dùng`, thống kê thẻ KPI) do **BE tính** và trả sẵn trong DTO (`budgetUsedPercent`, `GET /projects/summary`), FE không tự cộng dồn.

### 3.3 Frontend — Vue 3

| Nhóm | Thư viện | Thay cho |
|---|---|---|
| Core | Vue 3.5+, TypeScript 5.9, Vite 7, Vue Router 4 (lazy route, `meta.screen` + `meta.permission` + guard) | React 19, React Router 7 |
| State | Pinia (auth, toast, bộ lọc theo màn); **@tanstack/vue-query** (server state; hook sinh bởi orval khi có BE) | Redux Toolkit + RTK Query |
| UI | **Reka UI 2** (Dialog, Popover, Select…) + **Tailwind CSS 4** + `tailwindcss-animate`, chép nguyên class và `index.css` của DX; icon **`@lucide/vue`** (tên mới của lucide-vue-next từ Lucide v1, cùng tên icon với lucide-react) | Radix UI, lucide-react |
| Bảng | `CommonTable` tự viết (giữ nguyên class bản gốc, ô render qua slot `cell-<id>`); thêm @tanstack/vue-table / vue-virtual khi màn cần sắp xếp phía client hoặc ảo hoá danh sách dài | @tanstack/react-table, react-virtual |
| Form | VeeValidate + @vee-validate/zod, **dùng lại Zod schema** hiện có; `FormItem`, `BaseInput`, `FormSelect` (tìm kiếm, chọn nhiều), `FormDatePicker` (lịch riêng thay react-day-picker) | react-hook-form |
| i18n | vue-i18n v11 (Composition API): `$t('<screen_key>.<prefix>_<tên>')` trong template, `t` từ `useI18n()` trong script, `$d` / `$n` cho ngày và số; IDE gợi ý key từ `vi.json`; key sai / thiếu bản dịch / ghép key động bị chặn bởi `@intlify/eslint-plugin-vue-i18n` + `tools/i18n-check`; `src/locales/vi.json` + `en.json`, `fallbackLocale: 'vi'` (Q1) | i18next |
| Biểu đồ | lightweight-charts (dùng lại), vue-echarts | recharts |
| Kéo thả | vue-draggable-plus | @dnd-kit |
| Markdown | Câu trả lời Chat AI: bộ phân tích Markdown tối giản tự viết, render bằng thẻ Vue (không `v-html`); tài liệu dài: markdown-it + DOMPurify | react-markdown, rehype-raw |
| Excel/CSV | exceljs trong Web Worker (port nguyên module export của DX), papaparse (dùng lại); **bỏ `xlsx` 0.18.5** (bản npm còn lỗ hổng prototype pollution và ReDoS) | xlsx |
| Khác | @vueuse/core, date-fns 4, clsx + tailwind-merge (`cn`), @microsoft/signalr; hiệu ứng chuyển trang bằng `<Transition>` | framer-motion |

Cấu trúc: `src/app` (router, guards, `screens.ts` đọc `shared/screens.json`) · `src/components/layout` (MainLayout, Sidebar, PageLayout A/B/C/D, AdminLayout) · `src/modules/<module>/screens/<screen_key>/` (page + component riêng của màn, tên thư mục trùng `screen_key`) · `src/modules/<module>/{components,composables,api,utils}` · `src/shared` (components: Button, BaseDialog, CommonTable, Form*, filter/, insight/, export/, ScreenPlaceholder; composables; `i18n/enumLabel`, `screenTitle`) · `src/api` (client HTTP đọc `ErrorFullModel`; sau thay bằng `generated/` của orval) · `src/locales`.
AI Chat: BE trả JSON spec, FE có registry component Vue để render (thay `@json-render/react`).

### 3.4 Cấu trúc repo `codeon-dx`

```
codeon-dx/
├─ backend/        CodeOn.Dx.sln — src/Dx.Domain, Dx.Application, Dx.Infrastructure, Dx.Api (Locales/vi.json, en.json); tests/
├─ frontend/       Vue 3 + Vite (src/locales/vi.json, en.json)
├─ mock-data/      dữ liệu giả lập connector + seed tenant demo (Q3) — tách khỏi code
├─ deploy/         deploy.ps1, backup.ps1, cloudflared config mẫu, docker-compose.yml (cho Linux sau này)
├─ tools/          mock-api, mock-data-gen, i18n-check, convert-locales, port-checklist (bảng theo dõi RPC/màn đã chuyển)
├─ shared/         screens.json — danh mục màn hình (i18n, route, Màn hình áp dụng của Insight), FE và BE cùng đọc
└─ docs/           ADR, GLOSSARY, quy ước (bản sao của dx-conventions.md)
```

### 3.5 Mock API — FE chạy trước khi có BE

`tools/mock-api/server.mjs` (Node ≥ 20, không cần thư viện) đứng thay `Dx.Api` ở cổng `127.0.0.1:5080`; Vite proxy `/api` sang đây (`DX_API_TARGET` để đổi đích). Cùng hợp đồng với BE thật:

- Mọi response là `ErrorFullModel`, `messageText` lấy từ `backend/src/Dx.Api/Locales/*.json` theo `Accept-Language`; lỗi 400 trả `{ field: [message] }`.
- Đăng nhập email + mật khẩu kiểm tra hash ASP.NET Identity V3 (PBKDF2-HMACSHA512, 100.000 vòng) có trong `mock-data/seed/users.json`; access token 15 phút, refresh token xoay vòng trong cookie `dx_rt`; khoá 5 lần sai / 5 phút / IP (429).
- Đọc `mock-data/seed` (DB giả) và `mock-data/ai` (connector AI ở chế độ mock); người không có `project.manage` chỉ thấy dự án mình tham gia.
- Endpoint hiện có: `auth/login|refresh|logout|me`, `master-data`, `clients`, `system-rules`, `projects`, `projects/summary`, `ai/insights` (gộp insight theo cây màn — ADR-0001), `ai/chat`, `ai/chat/greeting`. Danh sách đầy đủ: `tools/mock-api/README.md`.
- Khi có BE: đổi `DX_API_TARGET` sang `http://127.0.0.1:8082`, FE không phải sửa. Mỗi endpoint BE viết xong được đối chiếu với mock (cùng request → cùng cấu trúc `data`).

### 3.6 Tính năng tự quản lý thay cho HRM / RON / Keycloak (D4)

| Tính năng | Màn (`screen_key`, route) | Quyền | Nội dung |
|---|---|---|---|
| **Phòng ban & Team** | `admin_department_list` — `/admin/departments` (tab Quản trị) | xem `employee.view`, sửa `employee.manage` | Cây phòng ban → team; mã, tên, mô tả, trưởng phòng / trưởng nhóm, thứ tự, bật/tắt; không xoá được khi còn nhân sự |
| **Nhân sự** (đã có) | `employee_list`, `employee_detail` | như trên | Mã `NV0001…` (tự sinh, sửa được), họ tên, email, điện thoại, chức danh, cấp bậc, phòng ban/team, ngày vào/nghỉ, trạng thái (`employee_status`: thử việc, đang làm, nghỉ thai sản, nghỉ không lương dài hạn, đã nghỉ việc); thêm tay hoặc import Excel 5 bước. Chuyển "đã nghỉ việc" → khoá tài khoản, gỡ khỏi phân bổ từ ngày nghỉ |
| **Tài khoản & đăng nhập** | `auth_login`, `auth_forgot_password` (`/auth/forgot-password`), `auth_reset_password` (`/auth/reset-password/:token`), `profile` | quản trị: `employee.manage` + `admin.roles` | Từ hồ sơ nhân sự bấm "Mời đăng nhập" → email chứa link đặt mật khẩu (hết hạn sau `invitation_expiry_hours` = 48h, dùng 1 lần); quên mật khẩu qua email; đổi mật khẩu ở Hồ sơ; độ dài tối thiểu `password_min_length`; khoá 15 phút sau 5 lần sai; quản trị khoá/mở khoá, buộc đổi mật khẩu. Email gửi qua connector `Email` (mock ghi ra `mock-data/email/outbox`) |
| **Nghỉ phép & OT** | `leave_request` — `/leave`; tab duyệt `leave_approval` | tạo đơn `timesheet.edit`; duyệt `timesheet.approve` | Đơn nghỉ: loại (`leave_type`: phép năm, ốm, nghỉ bù, việc riêng có lương, không lương), từ–đến ngày, cả ngày / nửa ngày sáng / chiều, lý do, đính kèm (nghỉ ốm). Đơn OT: ngày, giờ bắt đầu–kết thúc, dự án, lý do; hệ số 150% ngày thường / 200% cuối tuần / 300% ngày lễ. Trạng thái chờ duyệt → đã duyệt / từ chối (bắt buộc lý do) / người tạo huỷ. **Đơn đã duyệt tự sinh dòng công** (task `LEAVE` / `OT`, `status = approved`, `source = leave_request / ot_request`) — thay cho việc RON ghi thẳng vào công ở bản cũ; huỷ / từ chối sau khi duyệt thì xoá dòng công tương ứng. Quỹ phép năm: `LEAVE_ANNUAL_DAYS` (12) + 1 ngày mỗi `LEAVE_SENIORITY_STEP_YEARS` (5) năm + phép cũ chuyển sang tối đa `LEAVE_CARRY_OVER_MAX_DAYS`; giới hạn OT `OT_MAX_HOURS_PER_DAY / MONTH / YEAR` (mặc định 4 / 40 / 200, quản trị chỉnh ở Quy tắc); ngày lễ khai báo ở Cài đặt (không tính vào số ngày nghỉ) |
| **Chấm công vào/ra** | `attendance` — `/attendance` | xem của mình `timesheet.edit`; xem tất cả `timesheet.all_view`; sửa / import `timesheet.all_edit` | Bảng theo tháng × nhân sự: giờ vào, giờ ra, đi muộn (sau `work_start_time` + `ATTENDANCE_LATE_GRACE_MINUTES`), thiếu giờ ra, đang nghỉ phép. 3 nguồn ghi: nút **Vào / Ra** trên phần mềm (giờ server, bật/tắt bằng `allow_app_check_in`), **import Excel/CSV** xuất từ máy chấm công (ghép theo mã nhân viên), **sửa tay** của quản lý (bắt buộc ghi chú, lưu người sửa). Nhân viên tự bổ sung giờ thiếu trong `ATTENDANCE_EDIT_LIMIT_DAYS` ngày. **Báo cáo tháng** lấy cột Bắt đầu / Kết thúc từ bảng này (giữ luật cũ: không ghi đè ô đã sửa tay trừ khi chọn ghi đè) |

Endpoint dự kiến: `GET/POST /departments`, `/teams`; `POST /employees/{id}/invite`, `POST /auth/forgot-password`, `POST /auth/reset-password`, `POST /auth/change-password`; `GET/POST /leave-requests`, `POST /leave-requests/{id}/approve|reject|cancel`, `GET /leave-balances?year=`; `GET/POST /ot-requests` (+ approve/reject/cancel); `GET /attendance?month=&employeeId=`, `POST /attendance/check-in|check-out`, `PUT /attendance/{id}`, `POST /attendance/import`; `GET/POST /holidays`. Dữ liệu mẫu đã có trong `mock-data/seed` (`departments`, `teams`, `employees`, `users`, `leave_requests`, `ot_requests`, `leave_balances`, `attendance_records`, `holidays`, rule và setting liên quan); endpoint mock thêm cùng lúc với màn hình.

---

## 4. Thiết kế database

- PostgreSQL 18, schema `dx`, khoá chính `uuid` (v7), tên snake_case như hiện tại.
- Bảng mới: `tenants`, `tenant_users`, bảng Identity (`users`, `user_roles`, `user_tokens`…), `refresh_tokens`, `user_invitations`, `ai_insight_results` (cache insight phía server); tính năng tự quản lý (mục 3.6): `leave_requests`, `ot_requests`, `leave_balances`, `attendance_records`, `holidays`. Bảng `timesheets` thay cột `ron_type` bằng `source` (`manual` / `leave_request` / `ot_request`) + `source_request_id`; bỏ `ron_sync_runs`.
- Mọi bảng nghiệp vụ thêm `tenant_id` (FK, index đầu tiên trong index ghép); giữ cột audit `created_at/updated_at/created_by/updated_by` + `is_deleted` (thay `f_delete`).
- Bỏ: `profiles`, `user_identity`, `migrations` (thay bằng `__EFMigrationsHistory`), các cột phục vụ riêng Supabase/Keycloak.
- Migration bằng `dotnet ef migrations add` + **migration bundle** (`efbundle.exe`) khi deploy — kiểm tra trên DB trống trong CI (tránh lỗi thiếu `.Designer.cs` đã gặp ở CodeOnLicense).
- Seed: permission, role hệ thống, master data, rule mặc định, biểu mẫu PCV/CSS, prompt template, 1 tenant demo + dữ liệu mẫu (nạp từ `mock-data/seed`, cùng id với mock API).
- Chuyển dữ liệu cũ (nếu cần, giai đoạn 6): `pg_dump` schema `dx_platform` → nạp vào schema `staging` → script `INSERT … SELECT` gán `tenant_id`, đổi tên cột, map user, đổi Màn hình áp dụng của `ai_insights` sang `screen_key` mới.

---

## 5. Lộ trình

Giả định đội 2 BE + 2 FE + 1 QA/BA (bán thời gian), sprint 2 tuần, có dùng trợ lý AI. **Cuối mỗi sprint deploy lên `dx.codeonlabs.vn`** để khách kiểm thử dần.

| Giai đoạn | Thời lượng | Nội dung | Kết quả nghiệm thu |
|---|---|---|---|
| **P0 — Khởi động** | 1–2 tuần | Chốt D1/D4/D9; repo `codeon-dx` theo mục 3.4; skeleton BE + FE **đã có sẵn Q1–Q3**: `ErrorFullModel` + `ApiControllerBase` + lưới an toàn, `JsonStringLocalizer` + `vi/en.json` 2 phía, section `Integrations:*` + `JsonMockDataStore` + khung `mock-data/`; CI build + test + `i18n:check`; cài đặt máy, tunnel, script deploy; "walking skeleton": đăng nhập + `/health` | Khách mở được https://dx.codeonlabs.vn và đăng nhập; mọi API trả đúng `ErrorFullModel` |
| **P1 — Nền tảng** | 3 tuần | Tenant, Identity + tài khoản tự quản lý (mời qua email, quên/đặt mật khẩu, khoá tài khoản), RBAC (role, permission, policy), audit/soft delete, master data & danh mục, khách hàng, **Phòng ban & Team**, nhân viên (CRUD + import Excel 5 bước + SignalR tiến độ), cài đặt chung (giờ làm, ngày lễ), system rules; layout/sidebar/design token; chạy `convert-locales` chuyển toàn bộ locale DX sang snake_case; seed tenant demo từ `mock-data/seed` | Màn quản trị dùng được; mời một người dùng mới và người đó tự đặt mật khẩu đăng nhập được |
| **P2 — Dự án & chấm công** | 5 tuần | Danh sách/tạo/sửa/chi tiết dự án (tab Tổng quan), contract staff, assignment + lịch FTE, tính FTE & grade, biểu đồ workload; timesheet ngày/tuần, rules engine kiểm tra ở **cả BE** (không chỉ client), lưu tuần 1 transaction, duyệt/từ chối theo nhóm; **Nghỉ phép & OT** (đơn, duyệt, quỹ phép, tự sinh dòng công) và **Chấm công vào/ra** (nút Vào/Ra, import file máy chấm công, sửa tay) | Luồng tạo dự án → gán người → chấm công → duyệt chạy trọn vẹn; đơn nghỉ/OT đã duyệt hiện đúng trong nhật ký công việc |
| **P3 — Công số & Metrics** | 3 tuần | Lưới công số theo TT (HĐ, TT/OT effort, performance, allocate rate, Actual/Actual1/Free/Free1); milestone + Timeliness; EE; PCV (copy biểu mẫu khi tạo dự án, đánh giá tháng); CSS (link token, trang khảo sát công khai, rate limit); Defect / Leakage | Tab Metrics đủ 6 subtab |
| **P4 — Báo cáo tháng & Quản trị dự án** | 3 tuần | Báo cáo tháng (thành viên, ngày, cột tuỳ biến, đồng bộ giờ từ timesheet, giờ Bắt đầu/Kết thúc từ Chấm công vào/ra, khoá ô/concurrency, xuất Excel/PDF, song ngữ vi/ja); tài liệu, rủi ro, change request, issue; project report | Tab Báo cáo tháng và Quản trị dự án |
| **P5 — AI & tích hợp** | 3 tuần | AI gateway; cấu hình insight + prompt template; context builder theo màn hình, kế thừa insight trang → tab → subtab (ADR-0001); chat agent tool-calling + SSE; dịch báo cáo tháng; tích hợp GitHub/GitLab/Google Drive (OAuth phía server, token mã hoá); client thật cho AI, GitHub, GitLab, Google Drive, Email (SMTP) và nút "Kiểm tra kết nối"; mọi connector vẫn chạy được ở chế độ mock | InsightPanel và Chat AI chạy, không còn khoá AI ở trình duyệt; đổi `UseMock` không cần sửa code |
| **P6 — Dữ liệu, cứng hoá, UAT** | 2 tuần | Chuyển dữ liệu (nếu có), test hiệu năng (50+ user đồng thời), rà soát bảo mật theo OWASP ASVS mức 1, diễn tập backup/restore, UAT với khách, sửa lỗi | Biên bản UAT |

**Tổng: khoảng 20–21 tuần (~5 tháng)** (P2 thêm 1 tuần cho Nghỉ phép & OT, Chấm công vào/ra; bù lại không còn làm đồng bộ HRM/RON); FE và BE chạy song song trong từng giai đoạn. Có thể rút còn ~3,5 tháng nếu để P5 (AI) và Báo cáo tháng sang bản sau.

**Cách làm từng màn (lặp lại cho mọi module):** đọc màn React + service + RPC tương ứng → viết endpoint theo khung try/catch + `ErrorFullModel` và test BE (trước khi có BE: thêm endpoint vào mock API) → khai báo `screen_key` trong `shared/screens.json` (nếu là màn mới) → thêm key FE dưới `screen_key` và key BE dưới nhóm `<chức_năng>_<hành_động>` vào `vi.json` rồi `en.json` (câu chữ lấy từ `vi.json` gốc của DX) → thêm dữ liệu vào `mock-data/` nếu màn cần connector → sinh client orval → chép utils/schemas thuần TS → viết Vue page/component trong `screens/<screen_key>/`, chép nguyên class Tailwind của bản gốc → `npm run verify` → chạy lại unit test logic cũ → test tay theo `docs/screens/*.md` → demo.

---

## 6. Triển khai trên máy `phamthehien` (Windows x64)

### 6.1 Hiện trạng máy và cài đặt một lần
Theo khảo sát cùng máy (ghi trong `claude/codeon-license-deploy-plan.md` và `claude/ezcloud-support-deploy-plan.md`): đã có **.NET SDK 10.0.402**, ASP.NET Core Runtime 10.0.13, **PostgreSQL 18** native, IIS, Node.js, Git; **chưa có Docker**; có **Cloudflare WARP** (VPN client) nhưng chưa có `cloudflared`; email của domain dùng Zoho (MX `mx/mx2/mx3.zoho.com`).

1. **Máy luôn chạy:** `powercfg /change standby-timeout-ac 0`, `powercfg /change hibernate-timeout-ac 0`; đặt Active hours của Windows Update ngoài giờ kiểm thử; bật "Restore on AC power loss" trong BIOS nếu có.
2. **PostgreSQL 18 (đã có):** tạo user `dx_app` + DB `dx`; giữ `listen_addresses = 'localhost'`, không mở 5432 ra ngoài; không dùng user `postgres` cho ứng dụng.
3. **.NET:** dùng SDK 10 sẵn có để build; runtime ASP.NET Core 10 đã có. **Node.js ≥ 22.12** cho FE (Vite 7).
4. **Tunnel:** theo phương án chốt ở 6.3 — **một tunnel cho cả máy**, nhiều hostname.
5. Thư mục: `C:\Services\dx-api\` (bản chạy), `D:\dx-data\files\` (file upload), `D:\dx-data\backups\`, `D:\dx-data\logs\`, `D:\dx-data\mock-data\` (bản sao `mock-data/` của repo, đặt `MockData:RootPath` trỏ vào đây).

**Phân bổ cổng trên máy (tránh xung đột giữa 3 ứng dụng):**

| Ứng dụng | Hostname | Cổng nội bộ | Cách chạy |
|---|---|---|---|
| CodeOnLicense | `license.codeonlabs.vn` (hoặc `license-test`) | `127.0.0.1:8080` | IIS site |
| CodeOn Support | `support.codeonlabs.vn` | `127.0.0.1:8081` | Scheduled task `CodeOnSupport` (Node native, không Docker) |
| DX Platform | `dx.codeonlabs.vn` | `127.0.0.1:8082` | Windows Service (Kestrel) |
| DX — dev | — | `5173` (Vite), `127.0.0.1:5080` (mock API) | Chỉ khi phát triển |
| PostgreSQL 18 | — | `localhost:5432` | Windows service, dùng chung, mỗi app 1 user + 1 DB |

**Vì sao DX chạy Windows Service thay vì IIS như CodeOnLicense:** IIS trên Windows bản client giới hạn số request xử lý đồng thời, trong khi DX giữ kết nối lâu (SignalR cho tiến độ import, SSE cho Chat AI) nên sẽ nhanh chạm giới hạn. Kestrel chạy dạng service không bị giới hạn này và vẫn tự khởi động sau reboot.

### 6.2 Chạy ứng dụng
- `dotnet publish -c Release -r win-x64 --self-contained false -o C:\Services\dx-api`, FE `npm run build` chép vào `C:\Services\dx-api\wwwroot`.
- API dùng `UseWindowsService()`, Kestrel chỉ nghe `http://127.0.0.1:8082` (không mở cổng ra LAN). Đăng ký: `sc.exe create DxApi binPath= "C:\Services\dx-api\Dx.Api.exe" start= delayed-auto`, chạy dưới tài khoản service riêng (không phải Administrator).
- Bí mật (connection string, JWT key ≥ 256 bit, khoá AI, OAuth secret) đặt ở `appsettings.Secrets.json` cạnh bản chạy (gitignore, không bị ghi đè khi deploy) hoặc biến môi trường của service, ACL chỉ cho tài khoản service. **API dừng khởi động nếu bí mật còn giá trị mặc định, hoặc connector có `UseMock: false` mà thiếu cấu hình.**
- Môi trường test: mọi connector để `UseMock: true`; muốn thử dữ liệu thật thì bật từng connector một. Mật khẩu tài khoản demo đổi bằng `DX_DEMO_PASSWORD` trước khi mở ra Internet.
- Forwarded headers (`X-Forwarded-Proto/For`) bật cho proxy của tunnel; cookie `Secure`; HSTS; CSP.
- `deploy.ps1`: kéo code → build FE/BE → chạy test → dừng service → backup DB → chạy `efbundle.exe` → thay thư mục → khởi động → gọi `/health` → rollback nếu fail.
- Tuỳ chọn: cài GitLab/GitHub runner trên máy để push nhánh `release` là tự deploy.

### 6.3 Public `dx.codeonlabs.vn` — **đã chốt phương án A (Cloudflare Tunnel)**

Hiện trạng: nameserver `codeonlabs.vn` là iNET; `dx.codeonlabs.vn` chưa có bản ghi DNS.

| Phương án | Cách làm | Ưu | Nhược |
|---|---|---|---|
| **A. Cloudflare Tunnel (đã chọn)** | Chuyển nameserver `codeonlabs.vn` sang Cloudflare (gói Free) sau khi chép đủ bản ghi hiện có (`@`/`license` → `203.171.31.180`, MX/SPF/DKIM/DMARC của Zoho…); tạo **một** tunnel cho máy, thêm public hostname `dx.codeonlabs.vn` → `http://127.0.0.1:8082` (cùng tunnel với `license`, `support`); `cloudflared service install <token>`; nếu không kết nối được thì tạm tắt Cloudflare WARP | Miễn phí, không mở cổng router, ẩn IP máy, chạy được sau CGNAT, TLS tự động; có **Cloudflare Access** để chỉ cho email khách được mời vào môi trường test; về sau dùng `*.codeonlabs.vn` cho tenant SaaS | Thay đổi DNS toàn domain — làm cẩn thận để không gián đoạn email và `license.codeonlabs.vn`. Giữ DNS ở iNET mà vẫn dùng Cloudflare Tunnel thì phải dùng partial setup, chỉ có ở gói Business/Enterprise |
| B. ngrok custom domain | Thêm domain trong ngrok → thêm 1 bản ghi **CNAME** `dx` trỏ về target ngrok tại iNET; ngrok tự cấp chứng chỉ Let's Encrypt; chạy ngrok dạng Windows service, endpoint `dx.codeonlabs.vn` → `127.0.0.1:8082` | Nhanh nhất, không động tới nameserver | Custom domain chỉ có ở gói trả phí (Pay-as-you-go) |
| C. Mở cổng router | NAT 443 → máy; bản ghi A `dx` → IP public; IIS/Caddy làm reverse proxy theo hostname cho cả 3 app, win-acme lấy chứng chỉ | Không phụ thuộc dịch vụ ngoài | Cần IP tĩnh, ISP không chặn/CGNAT, lộ IP máy, phải tự lo tường lửa |

### 6.4 Vận hành
- Backup: Task Scheduler chạy `pg_dump -Fc` mỗi đêm vào `D:\dx-data\backups`, giữ 14 bản, chép thêm 1 bản ra ngoài máy (Drive/NAS); diễn tập restore mỗi tháng.
- Giám sát: `/health` (liveness công khai, chi tiết cần đăng nhập); UptimeRobot hoặc health check của tunnel; Serilog ghi file theo ngày.
- Môi trường test hiển thị banner "Môi trường kiểm thử" (`VITE_APP_ENV=test`); mỗi khách 1 tenant demo riêng; dữ liệu mẫu.
- Khi lên production: cùng artifact chạy trên Windows Server/IIS (như `license.codeonlabs.vn`) hoặc Linux VPS; viết sẵn `docker-compose.yml` cho phương án Linux.

---

## 7. Rủi ro & biện pháp

| Rủi ro | Biện pháp |
|---|---|
| Viết lại lớn, kéo dài, lệch nghiệp vụ | Chuyển theo module, demo mỗi sprint; dùng `docs/screens/*.md`, GLOSSARY, ADR làm tiêu chí nghiệm thu; chạy lại 115 file unit test logic cũ |
| Giao diện bản mới lệch bản cũ | Reka UI + chép nguyên class Tailwind, `index.css`, icon và câu chữ `vi.json` gốc; so ảnh chụp màn cũ/mới khi demo |
| Logic nằm rải ở client + PL/pgSQL, dễ sót quy tắc | Mỗi RPC/trigger có 1 dòng trong bảng theo dõi chuyển đổi (cũ → service mới → test) |
| Mock API và BE thật lệch hợp đồng | Mock và BE cùng đọc `Locales`, `screens.json`, `mock-data/seed`; integration test BE so cấu trúc `data` với mock cho từng endpoint |
| Máy cá nhân: mất điện, mất mạng, reboot | Service tự khởi động; UPS nếu có; thông báo khung giờ kiểm thử; backup ra ngoài máy |
| Máy cá nhân mở ra Internet | Chỉ tunnel tới `127.0.0.1:8082`; DB chỉ nghe localhost; tài khoản service riêng; BitLocker; cập nhật Windows; Cloudflare Access (phương án A); rate limit đăng nhập theo IP; đổi mật khẩu demo mặc định |
| Lịch sử git của bản gốc có khoá bí mật cũ | Repo mới bắt đầu lịch sử sạch, không chép `.git` của `DXOld` |
| Giấy phép thư viện | Tránh MediatR ≥13 / AutoMapper ≥15 (thương mại), lưu ý giấy phép QuestPDF; có lockfile (`package-lock.json`, `packages.lock.json`) |

---

## 8. Việc cần làm & tiến độ

**Đã chốt 20/10:** D1 đa tenant, D4 bỏ HRM/RON/Keycloak → tính năng tự quản lý (mục 3.6), D9 Cloudflare Tunnel.

**P0 — đã xong (20/10/2026)** — repo `D:\CodeOnLabs\DX\codeon-dx` (nay là `D:\CodeOnLabs\DX`):

| Hạng mục | Trạng thái |
|---|---|
| `shared/screens.json` (43 màn, route, quyền, cây kế thừa Insight) | Xong |
| `mock-data/` (56 file, dữ liệu hư cấu, sinh lại bằng `tools/mock-data-gen`) + 4 tài khoản demo; đã bỏ `hrm/`, `ron/`, thêm dữ liệu Nghỉ phép & OT, Chấm công vào/ra, ngày lễ, quỹ phép | Xong (lưu ý: trên máy vẫn còn thư mục `mock-data/hrm`, `mock-data/ron` cũ — cần xoá) |
| `tools/mock-api` (hợp đồng `/api/v1`, ErrorFullModel, Identity V3, refresh cookie) | Xong |
| Locale BE `backend/src/Dx.Api/Locales/{vi,en}.json` | Khung (auth, AI, lỗi chung, validation) |
| FE khung: router + phân quyền, phiên đăng nhập, toast, i18n, query client, layout (Sidebar, MainLayout, PageLayout A/B/C/D, AdminLayout) | Xong |
| FE dùng chung: Button, BaseDialog, PortalTooltip, EmptyState, AnimatedCounter, CommonTable + phân trang, FilterPopover/Chips, FormSelect, FormDatePicker, Insight (panel, thẻ, skeleton, DynamicInsights), Chat AI (cạnh màn + toàn màn), xuất Excel/CSV | Xong |
| Màn `auth_login` (giữ hero gốc + form email/mật khẩu) và `project_list` (lọc, chip, thống kê, bảng, xuất file) | Xong (Import dự án: chưa) |
| `tools/i18n-check` + `eslint.config.js` (vue-i18n) | Xong — 321 key FE, 0 lỗi |
| Màn mới (Phòng ban & Team, Nghỉ phép & OT, Chấm công vào/ra, Quên / đặt mật khẩu) | Có route + quyền + menu + dữ liệu mẫu; giao diện làm ở P1–P2 |
| Các màn còn lại | Có route + quyền + tiêu đề, hiện trang "đang phát triển" |
| Build/typecheck/lint trên máy thật (`npm install && npm run verify`) | **Chưa** — môi trường dựng code không tải được gói npm; đã kiểm tra tĩnh cú pháp SFC, biểu thức template, biến chưa khai báo, prop/emit giữa các component |

**Tiếp theo:**
1. Chạy `npm install && npm run verify` trên máy, sửa lỗi build nếu có; chụp so sánh 2 màn đã chuyển với bản React.
2. Tạo DB `dx` + user `dx_app`; kiểm kê bản ghi DNS `codeonlabs.vn` tại iNET (A `@`/`license`, MX/SPF/DKIM/DMARC Zoho…) → tạo zone Cloudflare Free → đổi nameserver → cài `cloudflared` service, hostname `dx.codeonlabs.vn` → `127.0.0.1:8082`.
3. Skeleton BE `CodeOn.Dx` (Q1–Q3) thay dần mock API — bắt đầu từ `auth/*`, `master-data`, `projects`.
4. Chuyển tiếp FE theo P1–P2: Quản trị (vai trò, nhân sự, **phòng ban & team**, danh mục, khách hàng, quy tắc), tài khoản (mời, quên mật khẩu), Chi tiết dự án, Nhật ký công việc, **Nghỉ phép & OT**, **Chấm công vào/ra**, Import Excel.
5. Dựng tunnel, deploy walking skeleton lên `dx.codeonlabs.vn`, gửi link cho 1 khách thử.
