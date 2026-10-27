# DX Platform (bản mới) — hướng dẫn cho Claude Code

Viết lại DX Platform (ERP quản lý dự án / chấm công / công số / chất lượng + AI Insight, Chat AI) từ bản React 19 + Supabase sang **Vue 3 + TypeScript (FE)**, **ASP.NET Core .NET 10 REST API (BE)**, **PostgreSQL 18**. Sản phẩm SaaS độc lập cho SME, **đa tenant**, không phụ thuộc hệ thống bên ngoài nào.
Đọc thêm `..\CLAUDE.md` (quy ước chung workspace) và `..\Deployment\CLAUDE.md` (máy chủ dùng chung).

## Tài liệu bắt buộc đọc trước khi code

| File | Nội dung |
|---|---|
| `docs\handoff\dx-conventions.md` | **Quy ước bắt buộc** Q1 (i18n), Q2 (`ErrorFullModel`), Q3 (connector + `mock-data/`) — có code mẫu |
| `docs\handoff\dx-migration-plan.md` | Kế hoạch: quyết định kiến trúc D1–D11, kiến trúc đích, thư viện, mock API, tính năng tự quản lý (mục 3.6), DB, lộ trình P0–P6, triển khai, tiến độ (mục 8) |
| `docs\handoff\dx-platform-overview.md` | Phân tích bản React gốc: nghiệp vụ, chỉ số (FTE, EE, PCV, CSS, Defect, Leakage), RBAC 21 permission, rủi ro |
| `README.md`, `mock-data\README.md`, `tools\mock-api\README.md` | Chạy thử, tài khoản demo, dữ liệu mẫu, danh sách endpoint mock |

Ánh xạ đường dẫn trong các tài liệu trên: `D:\CodeOnLabs\DX\codeon-dx` (repo `codeon-dx`) = **thư mục này** `D:\CodeOnLabs\DX`; bản React gốc = **`D:\CodeOnLabs\DXOld`**; `claude/<tên>.md` = `docs\handoff\<tên>.md`.

## Quyết định đã chốt (không tự ý đổi)

- **D1** đa tenant: 1 DB, cột `tenant_id` + global query filter EF Core, PostgreSQL RLS theo `app.tenant_id` làm lớp phụ.
- **D4** bỏ hẳn HRM, RON, Keycloak: Phòng ban & Team, Nhân sự, Tài khoản (mời qua email, quên/đặt mật khẩu), Nghỉ phép & OT (đơn đã duyệt tự sinh dòng công), Chấm công vào/ra (nút Vào/Ra, import file máy chấm công, sửa tay) là tính năng trong phần mềm.
- **D9** public `dx.codeonlabs.vn` qua Cloudflare Tunnel chung của máy → `127.0.0.1:8082`.
- **D11** UI: **Reka UI 2** + Tailwind CSS 4 + chép nguyên class/`index.css` của bản gốc; icon `@lucide/vue`. Không dùng PrimeVue. Mục tiêu: giao diện gần như y hệt bản React.
- D2 auth: ASP.NET Identity, JWT access 15 phút, refresh token xoay vòng trong cookie httpOnly/Secure/SameSite=Strict (`dx_rt`), không lưu token ở localStorage.
- D3 giữ 21 permission key; `[HasPermission("project.view")]`; kiểm tra thêm quyền theo dự án (thành viên / PM); **không** bypass theo tên role `Admin`.
- D5 mọi lời gọi LLM qua BE (`Microsoft.Extensions.AI`, OpenAI-compatible, SSE); khoá AI chỉ ở server.
- D6 BE Clean Architecture 4 project (`Dx.Domain`, `Dx.Application`, `Dx.Infrastructure`, `Dx.Api`) chia theo tính năng; **không MediatR / AutoMapper** (dùng Riok.Mapperly); test xUnit v3 + Shouldly + NSubstitute (không FluentAssertions 8).
- D7 contract-first: BE sinh OpenAPI → orval sinh type + hook TanStack Vue Query.
- D10 (chốt 20/10): chỉ dữ liệu hư cấu trong `mock-data/seed` trên môi trường public.
- D12 (chốt 20/10): nghiệp vụ đặc thù của bản gốc (mức performance HV/CT1/TV/CT2, công số TT/OT, báo cáo tháng kiểu Nhật, biểu mẫu PCV/CSS) **cấu hình hoá theo tenant** — không hard-code; tiếng Nhật là tuỳ chọn. Không đưa tên hay tham chiếu công ty gốc vào code/tài liệu. **Công thức giữ cố định trong code nhưng phải comment chi tiết** (định nghĩa từng đại lượng, công thức, ví dụ số, tham số lấy từ đâu) — mẫu: `frontend/src/modules/effort/utils/effort-calculations.ts`; **tham số** (hệ số, ngưỡng, biểu mẫu) sửa trên giao diện Quản trị: Danh mục (`performance_level.metadata.coefficient`…), Quy tắc, Cài đặt, Tiêu chí PCV, Biểu mẫu CSS.

## Quy ước code — tóm tắt (chi tiết ở `dx-conventions.md`)

- **i18n (Q1):** key snake_case `<screen_key>.<prefix>_<tên>` (vd `project_list.btn_create`), tối đa 3 cấp; component dùng `component.<tên_file_snake>.*`; 6 vùng chung `common menu enum validation error component`. Một màn **chỉ** dùng key của mình + vùng chung. Luôn `$t('key.cố.định')`, không ghép key động (trừ `src/shared/i18n/`), không `v-html`, không `$tc`/`v-t`/message cục bộ. Một file JSON / ngôn ngữ: `frontend/src/locales/vi.json` (nguồn chuẩn, fallback) rồi `en.json`. BE: `<chức_năng>_<hành_động>.msg_success / err_<lý_do>` trong `backend/src/Dx.Api/Locales/`.
- **Danh mục màn:** `shared/screens.json` là nguồn duy nhất (route, `extra_routes`, permission, `public`/`guest_only`, `parent`, `inherit_insights`). Màn mới → thêm vào đây **trước**, rồi thêm key i18n và component vào `PAGES` trong `frontend/src/app/router.ts` (route tự dựng từ JSON, không viết lại path/quyền). Mục Sidebar, tab Quản trị, thứ tự trang mặc định khai báo theo screen_key ở `frontend/src/app/navigation.ts`. Thư mục FE `src/modules/<module>/screens/<screen_key>/`.
- **API (Q2):** prefix `/api/v1`; mọi action có `try { return Success(...) } catch (Exception ex) { return HandleException(ex); }`; mọi response (cả lỗi) là `ErrorFullModel { status, messageText, data }`, HTTP status = `status`; lỗi 400 `data = { field: [msg] }`; 500 `data = { traceId }`; không bao giờ trả `ex.Message`. Phân trang `data = { items, page, pageSize, totalCount }`. Số liệu tổng hợp do BE tính sẵn trong DTO.
- **Connector (Q3):** 5 connector `Ai`, `GitHub`, `GitLab`, `GoogleDrive`, `Email`, mỗi cái có section `Integrations:<Key>` + `UseMock`; dữ liệu giả lập trong `mock-data/`, dữ liệu hư cấu, sinh lại bằng `node tools/mock-data-gen/generate.mjs`. Bí mật chỉ ở `appsettings.Secrets.json` (gitignore).
- **FE không tự giả lập dữ liệu**: chỉ gọi `/api/v1` — BE thật hoặc `tools/mock-api` (cổng 5080). Endpoint mock mới phải ghi vào `tools/mock-api/README.md`.

## Lệnh

```bash
node tools/mock-api/server.mjs          # API giả lập http://127.0.0.1:5080
cd frontend && npm install && npm run dev   # http://localhost:5173, /api → mock 5080
cd frontend && npm run dev:hybrid           # endpoint BE đã làm (BACKEND_ROUTES trong vite.config.ts) → BE 5090, còn lại → mock; dev:backend = chỉ BE
cd frontend && npm run verify           # i18n:check + lint + typecheck (vue-tsc) + build — phải xanh trước khi xong việc
node tools/mock-data-gen/generate.mjs   # sinh lại mock-data (DX_DEMO_PASSWORD để đổi mật khẩu demo)
node tools/db-seed/generate.mjs         # sinh database/seed/demo_tenant.sql từ mock-data/seed
cd backend && dotnet run --project src/Dx.Api -- --seed-demo   # áp lược đồ + nạp tenant demo (cần appsettings.Secrets.json)
cd backend && dotnet run --project src/Dx.Api                  # BE thật http://127.0.0.1:5090, /docs
cd backend && dotnet test --project tests/Dx.UnitTests
cd backend && dotnet test --project tests/Dx.IntegrationTests  # tự dựng cụm PostgreSQL 18 tạm (initdb), không cần DB dx
```

Tài khoản demo: `admin@`, `pm@`, `finance@`, `member@example.com` / `Demo@2026` (đổi trước khi public).

## Trạng thái (21/10/2026) — FE đủ mọi màn trên mock API

Đã có: `shared/screens.json` (43 màn), `mock-data/` (sinh bằng `generate.mjs`), `tools/mock-api` (đủ endpoint cho mọi màn, xem README), `tools/i18n-check`, khung FE và **toàn bộ màn đã chuyển từ bản React** (không còn trang "đang phát triển"): đăng nhập / quên / đặt lại mật khẩu, hồ sơ, khảo sát CSS công khai, Chat AI, Nhật ký công việc, Nghỉ phép & OT, Chấm công, Công số, Dự án (danh sách + import Excel, tạo / sửa, chi tiết 4 tab: Tổng quan, Báo cáo tháng, Metrics, Quản trị dự án), Quản trị (nhân sự, phòng ban, vai trò, quy tắc, AI Insight, prompt, tích hợp, danh mục, khách hàng, cài đặt, ngôn ngữ, tiêu chí PCV, biểu mẫu CSS). `npm run verify` xanh. **DB (25/10):** hướng DB-first, script PostgreSQL 18 ở `database/migrations/V001..V013` (62 bảng, schema `dx`, tên tiếng Anh, `tenant_id` + RLS) — quy ước và bảng đổi tên seed → DB ở `database/README.md`; schema đổi = thêm file `V0xx__*.sql` mới. **BE (25/10):** `backend/CodeOn.Dx.slnx` (Domain / Application / Infrastructure / Api + UnitTests), Clean Architecture rút gọn (service gọi EF Core qua `IAppDbContext`), entity **sinh từ DB** (`backend/scripts/scaffold-db.ps1`, không sửa `Dx.Domain/Entities`), DbUp áp `database/migrations` (`Dx.Api --migrate` / `--seed-demo`), JWT + refresh xoay vòng, filter tenant + RLS. Endpoint đã có: `auth/*`, `master-data`, `system-rules`, `settings`, `projects` (danh sách + summary) và **đợt 1 (26/10)**: `categories`, `holidays`, `pcv-template-items`, `css-template`, `clients`, `departments` / `teams`, `employees` (+ mời tài khoản); **đợt 2 (26/10)**: `permissions`, `roles` (+ ma trận quyền, người dùng của vai trò), `users`, quên / đặt lại mật khẩu, nhận lời mời, `profile`, connector Email (SMTP hoặc mock ghi log); **đợt 3 (26/10)**: dự án lõi — chi tiết, tạo / sửa / xoá / khôi phục, import Excel, staffing + phân bổ (gán, bàn giao, thay thế), mốc + đổi ngày, EE, defect / leakage; **đợt 4 (26/10)**: nhật ký công việc (rules engine khoá ngày / giới hạn giờ ở BE), nghỉ phép & OT (duyệt tự sinh dòng công, quỹ phép), chấm công vào/ra + import, công số — chi tiết và cách chạy ở `backend/README.md`. Endpoint còn lại vẫn chạy trên mock API (hợp đồng tham chiếu); danh sách và thứ tự các đợt tiếp theo ở `docs/be-api-audit.md`.

Cần để ý:
- Thư mục chưa có git. `git init` + commit trước khi sửa. **Không** chép `.git` của `DXOld` (lịch sử có khoá `service_role` Supabase).

## Việc tiếp theo cho Claude Code (theo thứ tự)

1. `cd frontend && npm install && npm run verify`; sửa lỗi build/lint/type/i18n. Chạy mock API + `npm run dev`, đăng nhập 4 tài khoản demo, so giao diện `auth_login`, `project_list` với bản React trong `DXOld`.
2. BE: làm tiếp endpoint theo `tools/mock-api/README.md` (mỗi endpoint: service ở `Dx.Application/<Tính năng>`, controller kế thừa `ApiControllerBase`, so kết quả với mock cùng request). Còn thiếu: connector `Integrations:*` + `JsonMockDataStore` (Q3), audit log, Hangfire, integration test 400–500.
3. FE theo P1–P2: Quản trị (vai trò, nhân sự, phòng ban & team, danh mục, khách hàng, quy tắc), tài khoản (mời, quên mật khẩu), Chi tiết dự án, Nhật ký công việc, Nghỉ phép & OT, Chấm công vào/ra, Import Excel.
4. Deploy: DB `dx` + role `dx_app`, service `DxApi`, public hostname `dx` (xem `..\Deployment\README.md`).

**Cách chuyển một màn:** đọc màn React + service + RPC tương ứng trong `DXOld\src` và `DXOld\database` → endpoint BE (hoặc mock) theo Q2 → `shared/screens.json` → key `vi.json` rồi `en.json` (câu chữ lấy từ locale gốc của DX, không lấy chuỗi dự phòng trong code) → chép utils/schemas TS thuần → Vue page/component trong `screens/<screen_key>/`, chép nguyên class Tailwind → `npm run verify` → chạy lại unit test logic cũ (Vitest) → test tay theo `DXOld\docs\screens\*.md`.
