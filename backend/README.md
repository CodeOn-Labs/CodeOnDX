# Backend DX — `CodeOn.Dx.slnx` (.NET 10)

REST API thay cho `tools/mock-api`, cùng hợp đồng (`/api/v1`, `ErrorFullModel`, câu chữ trong `src/Dx.Api/Locales`). Hướng **DB-first**: lược đồ là script SQL trong `../database/migrations`, entity sinh từ DB.

## Cấu trúc (Clean Architecture rút gọn)

| Project | Nội dung |
|---|---|
| `Dx.Domain` | `Entities/` (**sinh tự động** từ DB, không sửa tay), `Constants/` (permission, loại danh mục, khoá quy tắc) |
| `Dx.Application` | Service theo tính năng (`Auth`, `MasterData`, `Settings`, `Projects`…), DTO, exception nghiệp vụ (`Common/Exceptions.cs`). Service truy vấn EF Core trực tiếp qua `IAppDbContext`, không có tầng repository, không MediatR / AutoMapper (D6) |
| `Dx.Infrastructure` | `DxDbContext` (sinh tự động) + `DxDbContext.Custom.cs` (filter tenant / xoá mềm, đặt `app.tenant_id` cho RLS, tự điền cột audit), DbUp (`Database/DatabaseMigrator.cs`), JWT, hash mật khẩu Identity V3 |
| `Dx.Api` | `Program.cs`, `Common/` (`ErrorFullModel`, `ApiControllerBase`, `JsonStringLocalizer`, lưới an toàn), `Security/` (`[HasPermission]`, tenant / user hiện tại), `Controllers/` |
| `tests/Dx.UnitTests` | xUnit v3 + Shouldly + NSubstitute, chạy bằng Microsoft.Testing.Platform. Logic không cần DB: `QueryParams`, `PageRequest`, `Localized`, `ValidationErrors`, map exception → `ErrorFullModel`, `JsonStringLocalizer`, JWT, hash mật khẩu, `RequestContext` |
| `tests/Dx.IntegrationTests` | Gọi HTTP thật vào API (`WebApplicationFactory`) trên một cụm PostgreSQL 18 **tạm** do test tự `initdb` + `pg_ctl` (cổng ngẫu nhiên, xoá khi xong; không cần Docker, không đụng DB `dx` đang dùng). Áp `database/migrations` + seed demo + tenant thứ hai `test-beta` có số liệu biết trước (`Infrastructure/BetaTenant.cs`). App kết nối bằng `dx_app` nên kiểm được cả RLS. Tìm `initdb` ở biến môi trường `DX_PG_BIN`, rồi `C:\Program Files\PostgreSQL\18\bin`, rồi PATH |

Đa tenant (D1): mọi truy vấn lọc theo `tenant_id` của access token (filter tên `tenant`), và mỗi lần mở kết nối API đặt `app.tenant_id` để RLS của PostgreSQL chặn thêm một lớp. App kết nối bằng `dx_app` (không phải owner); migration chạy bằng `dx_owner`.

## Chạy lần đầu trên máy

1. Tạo role + DB (một lần, trong `psql` bằng tài khoản `postgres`):
   ```sql
   CREATE ROLE dx_owner LOGIN PASSWORD '<mật khẩu owner>';
   CREATE ROLE dx_app LOGIN PASSWORD '<mật khẩu app>';
   CREATE DATABASE dx OWNER dx_owner;
   ```
2. Chép `src/Dx.Api/appsettings.Secrets.example.json` thành `appsettings.Secrets.json` (đã gitignore), điền 2 chuỗi kết nối và `Jwt:SigningKey` (chuỗi ngẫu nhiên ≥ 32 byte).
3. Áp lược đồ và nạp tenant demo (dữ liệu hư cấu, D10):
   ```bash
   cd backend && dotnet run --project src/Dx.Api -- --seed-demo
   ```
   Chỉ áp lược đồ: `-- --migrate`. DbUp ghi script đã chạy vào `public.dx_schema_versions` và cấp quyền DML cho `dx_app`.
4. Chạy API: `dotnet run --project src/Dx.Api` → http://127.0.0.1:5090 (Development), tài liệu API ở `/docs` (Scalar), OpenAPI ở `/openapi/v1.json`. Bản chạy thật (Windows Service `DxApi`) nghe `127.0.0.1:8082`.
5. Frontend dùng BE thật: chạy thêm mock API (`node tools/mock-api/server.mjs`) rồi `cd frontend && npm run dev:hybrid` — endpoint đã có ở BE (danh sách `BACKEND_ROUTES` trong `frontend/vite.config.ts`, thêm vào khi làm xong endpoint mới) đi BE, còn lại đi mock; mock nhận JWT của BE nhờ cùng `Jwt:SigningKey`. Chỉ BE: `npm run dev:backend`.

Tài khoản demo giống mock: `admin@`, `pm@`, `finance@`, `member@example.com` / `Demo@2026`.

## Visual Studio 2026

- Mở `backend/CodeOn.Dx.slnx`, đặt `Dx.Api` làm startup project, F5 mở `/docs`.
- Chạy `--seed-demo` / `--migrate`: *Project Properties › Debug › Command line arguments*, hoặc lệnh ở trên.
- Test: Test Explorer (dự án test dùng Microsoft.Testing.Platform, khai báo trong `global.json`).

## Đổi lược đồ (quy trình DB-first)

1. Thêm file mới `database/migrations/V013__<mo_ta>.sql`. **Không sửa** file đã áp vì DbUp không chạy lại file cũ.
2. `dotnet run --project src/Dx.Api -- --migrate`
3. Sinh lại entity: `powershell -ExecutionPolicy Bypass -File backend\scripts\scaffold-db.ps1` (đọc `ConnectionStrings:DxOwner` trong `appsettings.Secrets.json`). Script ghi đè `Dx.Domain/Entities/*.cs` và `DxDbContext.cs`; code viết tay để ở file partial khác. Bảng bị xoá thì tự xoá file entity tương ứng. Muốn dùng giao diện VS thì EF Core Power Tools (*Reverse Engineer*) cho cùng kết quả, với các lựa chọn: schema `dx`, entity vào `Dx.Domain/Entities` namespace `Dx.Domain.Entities`, context `DxDbContext` vào `Dx.Infrastructure/Persistence`, không sinh `OnConfiguring`.
4. Đổi dữ liệu demo: sửa `tools/mock-data-gen`, chạy `node tools/mock-data-gen/generate.mjs` rồi `node tools/db-seed/generate.mjs` (sinh `database/seed/demo_tenant.sql`).

## Endpoint đã có

| Method | Đường dẫn | Quyền |
|---|---|---|
| POST | `/api/v1/auth/login`, `/refresh`, `/logout` | — (giới hạn 30 request/phút/IP; 5 lần sai mật khẩu / 5 phút / IP → 429; 5 lần sai liên tiếp của một tài khoản → khoá 15 phút) |
| GET | `/api/v1/auth/me` | đăng nhập |
| GET | `/api/v1/master-data?type=` | đăng nhập |
| GET / PUT | `/api/v1/system-rules`, `/api/v1/system-rules/{key}` | xem: đăng nhập; sửa: `admin.settings` |
| GET / PUT | `/api/v1/settings` | xem: đăng nhập; sửa: `admin.settings` |
| GET / POST / PUT / DELETE | `/api/v1/categories[/{id}]` | `admin.settings` |
| GET / POST / DELETE | `/api/v1/holidays[/{id}]?year=` | xem: đăng nhập; sửa: `admin.settings` |
| GET / POST / PUT / DELETE | `/api/v1/pcv-template-items[/{id}]` | `admin.settings` |
| GET / PUT | `/api/v1/css-template`; GET / POST / PUT / DELETE `/api/v1/css-template/questions[/{id}]` | `admin.settings` |
| GET | `/api/v1/clients`, `/api/v1/clients/summary` | đăng nhập |
| POST / PUT / DELETE | `/api/v1/clients[/{id}]`, POST `/api/v1/clients/{id}/restore` | `admin.settings` (xoá lần 1 vào thùng rác, xoá trong thùng rác là xoá hẳn) |
| GET | `/api/v1/departments` (kèm team) | đăng nhập |
| POST / PUT / DELETE | `/api/v1/departments[/{id}]`, `/api/v1/teams[/{id}]` | `employee.manage` |
| GET | `/api/v1/employees`, `/summary`, `/{id}` | `employee.view` |
| POST / PUT / DELETE | `/api/v1/employees[/{id}]`, POST `/{id}/restore`, `/{id}/invite` | `employee.manage` |
| GET | `/api/v1/permissions`, `/api/v1/roles` | đăng nhập |
| GET / POST / PUT / DELETE | `/api/v1/roles/summary`, `/roles/{id}`, `/roles`, PUT `/roles/permissions` (ma trận quyền), `/roles/{id}/users[/{userId}]`, GET `/users?search=` | `admin.roles` (không được làm mất người cuối cùng có `admin.roles` → 409) |
| POST / GET | `/api/v1/auth/forgot-password`, GET `/auth/reset-password/{token}`, POST `/auth/reset-password` | — (giới hạn 30 request/phút/IP; link sai 404, hết hạn / đã dùng 410) |
| GET / PUT | `/api/v1/profile`, POST `/api/v1/profile/change-password` | đăng nhập |
| GET | `/api/v1/projects`, `/api/v1/projects/summary` | `project.view` (không có `project.manage` thì chỉ thấy dự án mình tham gia) |
| POST | `/api/v1/projects`, `/api/v1/projects/import` | `project.create` |
| GET | `/api/v1/projects/{id}`, `/staffing`, `/milestones?from=&to=`, `/ee?month=`, `/quality?kind=&year=` | `project.view` + được phân bổ vào dự án (hoặc `project.manage`; người không quản lý chỉ thấy phần của mình) |
| PUT / DELETE / POST / PATCH | `/api/v1/projects/{id}`, GET `/assignable-employees`, `/assignments[/{aid}]`, `/milestones[/{mid}]`, PATCH `/dates` | `project.manage`; dự án trong thùng rác → 409 |
| POST | `/api/v1/projects/{id}/restore` | `project.manage` |
| PUT | `/api/v1/projects/{id}/quality` | `project.manage` hoặc `report.quality` |
| GET / PUT / POST | `/api/v1/timesheets` (`?employeeId=&from=&to=`, tối đa 62 ngày), `/timesheets/employees`, `/timesheets/group`; POST `/timesheets/approval` | đăng nhập (xem: bản thân; `timesheet.team` / `timesheet.approve` thêm người mình lead / quản lý / làm PM; `timesheet.all_view` tất cả; sửa người khác cần `timesheet.all_edit`); duyệt: `timesheet.approve` |
| GET / POST | `/api/v1/leave-requests[/summary]`, `/ot-requests` (`?scope=mine|approval`), POST `/{id}/approve|reject|cancel` | tạo: `timesheet.edit`; duyệt: `timesheet.approve` |
| GET / POST / PUT | `/api/v1/attendance?month=`, `/attendance/today`, POST `/attendance/check-in|check-out` (`timesheet.edit`), PUT `/attendance`, POST `/attendance/import` (`timesheet.all_edit`) | đăng nhập |
| GET / POST / PATCH / DELETE | `/api/v1/effort?month=`, `/effort/{id}` | xem `effort.view`, sửa `effort.edit` |
| GET | `/health` | — |

Đợt 1 (26/10, Cấu hình + Tổ chức, 39 endpoint) đã đối chiếu với mock trên tenant demo: 16/16 GET cho `data` giống hệt (trừ `lastLoginAt` theo giờ đăng nhập), 16 ca lỗi 400/404/409 cùng status và cùng field lỗi. Khác cố ý:
- Ngày không tồn tại (vd `2026-02-30`) BE báo `validation.invalid_date`; mock (JS `Date.parse`) cho qua.
- Phòng ban / team không có thùng rác trong DB nên xoá là xoá hẳn; vẫn chặn khi còn nhân sự chưa nghỉ việc (hoặc phòng ban còn team), nhân sự đã nghỉ / đã xoá được gỡ liên kết.
- Mời tài khoản (`/employees/{id}/invite`) lưu SHA-256 của token trong `account_tokens` và gửi email qua `IEmailSender`.

Đợt 2 (26/10, Phân quyền + Tài khoản + Email, 18 endpoint): 8/9 GET giống hệt mock, 11/11 ca lỗi cùng status, field và câu thông báo. Khác cố ý:
- `GET /users` sắp theo tên; mock giữ thứ tự seed.
- Vai trò không có thùng rác trong DB nên xoá là xoá hẳn (user_roles / role_permissions xoá theo); xoá vai trò cũng bị chặn nếu làm mất người quản trị vai trò cuối cùng.
- Đặt lại mật khẩu thành công thì thu hồi mọi refresh token của tài khoản (đăng xuất mọi nơi). Nhận lời mời: tạo tài khoản (hoặc gắn tài khoản đã có ở tenant khác), vào tenant với vai trò `member` nếu tenant có, gắn hồ sơ nhân sự.
- Email (đợt 2): `Integrations:Email:UseMock = true` (mặc định) chỉ ghi email ra log; `false` gửi SMTP (`SmtpHost`, `SmtpPort`, `SmtpUser`, `EnableSsl` trong `appsettings.json`, `SmtpPassword` trong `appsettings.Secrets.json`). Tiêu đề / nội dung là key `email_<mẫu>.subject` / `.body` trong `Locales/*.json`.

Đợt 3 (26/10, Dự án lõi, 20 endpoint): trên 3 dự án demo × 7 GET (chi tiết, staffing, milestones, EE, quality defect/leakage, assignable-employees), cả tài khoản admin và member, mọi màn đều giống hệt mock trừ 2 trường đã biết của chi tiết (`budgetUsedPercent` như đã nêu ở trên, `createdAt` cùng thời điểm nhưng ghi theo UTC); 6/8 ca lỗi giống hệt. Khác cố ý:
- Vị trí hợp đồng phải có ngày bắt đầu / kết thúc (lấy theo dự án nếu bỏ trống); dự án chưa có ngày thì BE báo `required` ở `contractStaff.N.startDate/endDate` (cột DB NOT NULL), mock cho qua.
- `GET /projects/{id}/ee?month=2026-13` → 400; mock chỉ kiểm định dạng `yyyy-MM`.
- Mốc dự án lưu người / thời điểm giải trình ở cột mới `justification_by` / `justification_at` (migration `V013`).
- Thứ tự vị trí hợp đồng / phân bổ theo `created_at`; seed demo (`tools/db-seed`) gán `created_at` tăng dần theo thứ tự trong `mock-data` để giống mock.

Đợt 4 (26/10, Nhật ký công việc + Nghỉ phép / OT + Chấm công + Công số, 25 endpoint): 14 GET × 3 tài khoản demo (admin, pm, member) cho `data` giống hệt mock khi so không theo thứ tự (khác thứ tự: BE sắp dòng công theo ngày, danh sách team theo phòng ban; mock giữ thứ tự seed); 11/12 ca lỗi giống hệt. Khác cố ý:
- Đơn nghỉ có ngày kết thúc trước ngày bắt đầu: BE chỉ báo `err_to_before_from`, không báo thêm "không có ngày làm việc".
- `timesheet_entries.source` trong DB là `overtime_request`, API vẫn trả `ot_request` như hợp đồng.
- Chấm công sửa tay lưu `note = ''` khi nhân viên tự bổ sung không ghi chú (cột NOT NULL với dòng manual), API trả `null`; `updatedBy` không hiện với dòng tự bấm Vào/Ra.
- Công số: cột "vai trò" là tên vị trí (`job_position.name`), lưu `position_id`; ghi chú tối đa 100 ký tự (cột DB); xoá dòng là xoá hẳn (bảng không có thùng rác).
- Giờ Vào/Ra và "hôm nay" lấy theo múi giờ tenant (`tenants.time_zone`).

Kiểm kê toàn bộ endpoint còn thiếu và thứ tự làm: `docs/be-api-audit.md`.

Đã đối chiếu với mock API (cùng request, cùng tài khoản): 25/27 trường hợp giống hệt `status`, `messageText` và `data`. Hai chỗ khác là cố ý:
- `statusIds` không phải GUID thì BE báo lỗi 400 ở field đó; mock bỏ qua.
- `budgetUsedPercent` BE tính từ giờ công đã duyệt (`Σ giờ ÷ (TIMESHEET_FTE_HOURS × 20)`), còn mock lấy số có sẵn trong seed. Vì seed chỉ có giờ công khoảng 2 tháng nên % của BE thấp hơn mock.

Endpoint còn lại làm tiếp theo danh sách trong `tools/mock-api/README.md`.
