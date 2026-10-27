# Mock API

Đứng thay BE .NET 10 trong lúc chưa có BE, để frontend chạy với dữ liệu `mock-data/`. Frontend gọi đúng các endpoint `/api/v1/...` mà BE thật sẽ cung cấp, nên khi có BE chỉ cần đổi đích proxy.

```bash
node tools/mock-api/server.mjs                       # http://127.0.0.1:5080
MOCK_LATENCY_MS=0 MOCK_TODAY=2026-10-20 node tools/mock-api/server.mjs
```

| Biến môi trường | Mặc định | Ý nghĩa |
|---|---|---|
| `MOCK_API_PORT` / `MOCK_API_HOST` | `5080` / `127.0.0.1` | Cổng, địa chỉ nghe |
| `MOCK_LATENCY_MS` | `250` | Độ trễ giả lập mỗi request `/api` |
| `MOCK_FAILURE_RATE` | `0` | Tỷ lệ request trả lỗi 500 ngẫu nhiên (0–1), để thử luồng lỗi |
| `MOCK_TODAY` | ngày hiện tại (giờ VN) | Cố định "hôm nay" cho thống kê sắp kết thúc |
| `MOCK_DATA_ROOT` | `mock-data/` | Thư mục dữ liệu |
| `MOCK_BACKEND_JWT` / `MOCK_BACKEND_JWT_KEY` | bật / đọc `Jwt` trong `backend/src/Dx.Api/appsettings*.json` | Nhận cả access token JWT do BE .NET cấp (kiểm chữ ký HS256, issuer, audience, hạn) để frontend chạy `dev:hybrid`; `MOCK_BACKEND_JWT=0` để tắt |

Không cần cài thư viện (Node ≥ 20).

## Hợp đồng chung

- Mọi response: `{ "status": 200, "messageText": "…", "data": … }` (Q2). HTTP status bằng `status`.
- `messageText` dịch theo header `Accept-Language` (`vi` mặc định, `en`), lấy từ `backend/src/Dx.Api/Locales/*.json`.
- Lỗi 400: `data = { "<field>": ["<thông báo>"] }`.
- Danh sách phân trang: `?page=1&pageSize=20` (page bắt đầu từ 1, `pageSize=0` = lấy tất cả) → `data = { items, page, pageSize, totalCount }`.
- Xác thực: access token (15 phút) trong body, gửi lại qua `Authorization: Bearer …`; refresh token (7 ngày, xoay vòng) trong cookie `dx_rt` (`HttpOnly`, `SameSite=Strict`, `Path=/api/v1/auth`).

## Endpoint

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/health` | — | Tình trạng |
| POST | `/api/v1/auth/login` | — | `{ email, password }` → `{ accessToken, expiresIn, user, permissions }` + cookie refresh. Sai 5 lần / 5 phút / IP → 429 |
| POST | `/api/v1/auth/refresh` | cookie | Cấp access token mới, xoay refresh token |
| POST | `/api/v1/auth/logout` | — | Huỷ token, xoá cookie |
| GET | `/api/v1/auth/me` | đăng nhập | `{ user, permissions }` |
| GET | `/api/v1/master-data?type=project_status,risk_level` | đăng nhập | Danh mục, `name` theo ngôn ngữ |
| GET | `/api/v1/system-rules` | đăng nhập | Quy tắc hệ thống: `{ key, module, valueType (number/boolean), value, defaultValue, min, max, status, name, description, updatedAt }` — `name`, `description` theo ngôn ngữ |
| PUT | `/api/v1/system-rules/{key}` | `admin.settings` | `{ value, status (0/1) }` — kiểm tra kiểu + `min`/`max` của quy tắc |
| GET | `/api/v1/categories?search=&types=&page=&pageSize=` | `admin.settings` | Danh mục để quản trị (cả mục đang khoá), gồm `name`, `nameEn`, `isActive`, `isSystem`, `metadata` |
| POST | `/api/v1/categories` | `admin.settings` | `{ type, code, name, nameEn, description, color, sortSeq, metadata, isActive }` → 201. Mã `A-Z0-9_` không trùng trong cùng loại; `performance_level` bắt buộc `metadata.coefficient` 0–2 |
| PUT | `/api/v1/categories/{id}` | `admin.settings` | Như POST; không đổi được `type` |
| DELETE | `/api/v1/categories/{id}` | `admin.settings` | 409 nếu là danh mục hệ thống hoặc đang được dữ liệu khác dùng (khi đó nên tạm khoá) |
| GET | `/api/v1/settings` | đăng nhập | Cài đặt chung dạng `{ key: "value" }` |
| PUT | `/api/v1/settings` | `admin.settings` | Gửi các khoá cần đổi; khoá lạ hoặc sai định dạng → 400 theo từng khoá |
| GET | `/api/v1/holidays?year=` | đăng nhập | Ngày lễ `{ id, date, name, isPaid }` |
| POST / DELETE | `/api/v1/holidays`, `/api/v1/holidays/{id}` | `admin.settings` | Thêm `{ date, name, isPaid }` (không trùng ngày) / xoá |
| GET / POST | `/api/v1/pcv-template-items` | `admin.settings` | Biểu mẫu tiêu chí PCV `{ id, category, label, sortSeq }`; thêm `{ category, label }` (BE tự xếp cuối) |
| PUT / DELETE | `/api/v1/pcv-template-items/{id}` | `admin.settings` | Sửa / xoá (xoá mềm) |
| GET / PUT | `/api/v1/css-template` | `admin.settings` | Nội dung form CSS `{ title, intro, scaleNote, commentLabel }` |
| GET / POST | `/api/v1/css-template/questions` | `admin.settings` | Câu hỏi `{ id, label, required, sortSeq }`; thêm `{ label, required }` |
| PUT / DELETE | `/api/v1/css-template/questions/{id}` | `admin.settings` | Sửa / xoá câu hỏi |
| GET | `/api/v1/projects?search=&statusIds=&gradeIds=&clientIds=&riskLevelIds=&startFrom=&endTo=&deleted=&page=&pageSize=` | `project.view` | Dự án; không có `project.manage` thì chỉ thấy dự án mình tham gia |
| GET | `/api/v1/projects/summary?<cùng bộ lọc>` | `project.view` | `{ total, running, endingSoon, planning, risky, endingSoonDays }` |
| GET | `/api/v1/ai/insights?screen=<screen_key>&refresh=`  (BE nhận thêm `projectId`; BE tính thông báo theo quy tắc, mock đọc mock-data/ai)| đăng nhập | Insight theo `screen_key` (connector AI mock): màn hiện tại trước, rồi các màn cha theo `parent` trong `shared/screens.json` (dừng ở màn có `inherit_insights: false` — ADR-0001). `screen` không có trong danh mục → 400 |
| GET | `/api/v1/ai/chat/greeting` | đăng nhập | Câu chào của trợ lý |
| POST | `/api/v1/ai/chat` | đăng nhập | `{ module, message }` → `{ reply }` (markdown). FE đã bỏ Chat AI (27/10) — giữ endpoint làm hợp đồng tham chiếu |

### Tài khoản, hồ sơ, khảo sát CSS công khai

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/v1/auth/forgot-password` | — | `{ email }` → luôn trả thành công (không lộ email có tồn tại hay không) |
| GET | `/api/v1/auth/reset-password/{token}` | — | Thông tin token `{ kind: invite / reset, email, fullName }`; hết hạn / sai → 404 |
| POST | `/api/v1/auth/reset-password` | — | `{ token, password }`; token lời mời → tạo tài khoản (vai trò member) |
| GET / PUT | `/api/v1/profile` | đăng nhập | Hồ sơ của tôi; sửa `{ fullName, phone, preferredLocale }` |
| POST | `/api/v1/profile/change-password` | đăng nhập | `{ currentPassword, newPassword }` |
| GET / POST | `/api/v1/public/css-surveys/{token}` | — | Form khảo sát CSS của dự án theo link; nộp `{ respondent, comment, answers: [{ questionId, score 1–5 }] }` (1 lần) |

### Tổ chức (D4)

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/v1/clients?search=&countryIds=&industryIds=&status=&deleted=&page=&pageSize=` | đăng nhập | Khách hàng (kèm số dự án) |
| GET | `/api/v1/clients/summary?<cùng bộ lọc>` | đăng nhập | `{ total, active, inactive, totalProjects }` |
| POST / PUT / DELETE | `/api/v1/clients`, `/api/v1/clients/{id}` | `admin.settings` | Thêm / sửa / xoá (lần 1 vào thùng rác, lần 2 xoá hẳn; còn dự án → 409); `POST …/{id}/restore` khôi phục |
| GET | `/api/v1/departments` | đăng nhập | Phòng ban kèm team, số nhân sự |
| POST / PUT / DELETE | `/api/v1/departments[/{id}]`, `/api/v1/teams[/{id}]` | `employee.manage` | Quản lý phòng ban & team |
| GET | `/api/v1/employees?…`, `/api/v1/employees/summary`, `/api/v1/employees/{id}` | `employee.view` | Nhân sự + % phân bổ (`allocationStatus` theo quy tắc HR_*), quỹ phép ở chi tiết |
| POST / PUT / DELETE | `/api/v1/employees[/{id}]`, `…/{id}/restore`, `…/{id}/invite` | `employee.manage` | Thêm / sửa / xoá mềm / khôi phục / mời tạo tài khoản qua email |

### Quản trị hệ thống

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/v1/permissions`, `/api/v1/users` | `admin.roles` | 21 quyền theo nhóm; danh sách tài khoản |
| GET / POST / PUT / DELETE | `/api/v1/roles[/{id}]`, `/api/v1/roles/summary` | `admin.roles` | Vai trò + quyền; luôn còn ít nhất 1 vai trò có `admin.roles` |
| PUT | `/api/v1/roles/permissions` | `admin.roles` | Sửa quyền hàng loạt |
| GET / POST / DELETE | `/api/v1/roles/{id}/users[/{userId}]` | `admin.roles` | Gán / gỡ người dùng khỏi vai trò |
| GET / POST / PUT / DELETE | `/api/v1/ai/insight-configs[/{id}]`, `POST …/preview` | `admin.insights` | Cấu hình Insight theo màn; xem trước kết quả |
| GET / POST / PUT / DELETE | `/api/v1/ai/prompt-templates[/{id}]`, `POST …/preview` | `admin.insights` | Prompt template (render biến ở BE) |
| GET / PUT | `/api/v1/integrations[/{key}]` | `admin.integrations` | 3 nguồn tích hợp (Jira, Base, Bitrix — 27/10, thay Ai / GitHub / GitLab / GoogleDrive / Email): bật/tắt + cài đặt không bí mật (`baseUrl`, Jira thêm `accountEmail`, `projectKeys`) |
| POST / GET | `/api/v1/integrations/{key}/test`, `/preview` | `admin.integrations` | Kiểm tra kết nối; xem dữ liệu mẫu đọc từ `mock-data/jira|base|bitrix` |

### Công việc: nhật ký, nghỉ phép & OT, chấm công (D4)

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/v1/timesheets/employees` | `timesheet.edit` / `timesheet.team` | Người tôi được xem / sửa nhật ký, dự án của họ, các cờ quyền |
| GET / PUT | `/api/v1/timesheets?employeeId=&from=&to=` | như trên | Nhật ký theo ngày + trạng thái khoá từng ngày (quy tắc TIMESHEET_*); PUT thay các dòng nhập tay của các ngày còn sửa được |
| GET | `/api/v1/timesheets/group` | `timesheet.team` | Báo cáo nhóm |
| POST | `/api/v1/timesheets/approval` | `timesheet.approve` | Duyệt / từ chối nhật ký |
| GET / POST | `/api/v1/leave-requests?scope=mine/approval`, `/api/v1/leave-requests/summary` | `timesheet.edit` | Đơn nghỉ phép + quỹ phép; tạo đơn |
| POST | `/api/v1/leave-requests/{id}/approve`, `/reject`, `/cancel` | `timesheet.approve` (huỷ: người tạo) | Đơn đã duyệt tự sinh dòng công |
| GET / POST | `/api/v1/ot-requests`, `…/{id}/approve`, `/reject`, `/cancel` | như nghỉ phép | Đơn OT (150/200/300%, giới hạn OT_*) |
| GET / PUT | `/api/v1/attendance?month=`, `/api/v1/attendance` | `timesheet.edit` (sửa người khác: `timesheet.all_edit`) | Chấm công tháng; sửa tay |
| GET / POST | `/api/v1/attendance/today`, `/check-in`, `/check-out` | `timesheet.edit` | Nút Vào / Ra |
| POST | `/api/v1/attendance/import` | `timesheet.all_edit` | Import file máy chấm công |

### Công số

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/v1/effort?month=YYYY-MM` | `effort.view` | Lưới công số + phòng ban, team, dự án, vai trò, mức hiệu suất |
| PATCH / POST / DELETE | `/api/v1/effort/{id}`, `/api/v1/effort` | `effort.edit` | Sửa ô (đồng bộ TT effort / hiệu suất cho mọi dòng của người đó) / thêm / xoá dòng |

### Dự án — tạo, sửa, import, chi tiết

Người không có `project.manage` chỉ mở được dự án mình được phân bổ (403 `project_detail.err_no_access`) và chỉ thấy phần của mình. Số liệu tổng hợp (EE, PCV, CSS, tỷ lệ lỗi, mức rủi ro, tỷ lệ đúng hạn) do BE tính, kèm `tone` theo ngưỡng ở Quản trị → Quy tắc (module `quality`); công thức có comment trong `lib/project-routes.mjs`, `lib/project-extra-routes.mjs`.

| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/v1/projects` | `project.create` | `{ name, clientId, gradeId, description, startDate, endDate, budget, ticketSystemUrl, sourceCodeUrl, contractStaff: [{ roleCode, levelId, quantity, ftePerPerson, startDate, endDate, pricePerMonth }] }` → trạng thái Kế hoạch; lỗi dòng nhân sự trả theo `contractStaff.{i}.{field}` |
| POST | `/api/v1/projects/import` | `project.create` | `{ rows: [{ row, values }] }` — các dòng cùng tên dự án gộp thành 1 dự án; → `{ created, errors: [{ row, field, message }] }` |
| GET / PUT / DELETE | `/api/v1/projects/{id}` | xem `project.view`; sửa / xoá `project.manage` | Chi tiết (nhân sự hợp đồng, link, cảnh báo nghỉ việc, cờ quyền) / sửa (thêm `statusId`) / xoá mềm |
| POST / PATCH | `/api/v1/projects/{id}/restore`, `/api/v1/projects/{id}/dates` | `project.manage` | Khôi phục; sửa nhanh ngày bắt đầu / kết thúc |
| GET | `/api/v1/projects/{id}/staffing` | `project.view` | Nhóm vai trò × cấp độ → vị trí → người + giai đoạn (tổng HĐ / KH / TT theo FTE-ngày) + chuỗi HĐ / KH / TT theo ngày |
| GET | `/api/v1/projects/{id}/assignable-employees` | `project.manage` | Nhân sự còn làm việc + % phân bổ hiện tại |
| POST / PUT / DELETE | `/api/v1/projects/{id}/assignments[/{aid}]` | `project.manage` | Gán `{ employeeId, contractStaffId, slotIndex, intervals: [{ startDate, endDate, fte }] }`; sửa thêm `mode: update / handover / replace`, `handoverDate`; xoá cả phân bổ hoặc 1 giai đoạn (`?intervalId=`) |
| GET | `/api/v1/projects/{id}/milestones?from=&to=` | `project.view` | Mốc + trạng thái (done-ontime / done-late / overdue / next / upcoming) + tỷ lệ đúng hạn trong khoảng |
| POST / PUT / DELETE | `/api/v1/projects/{id}/milestones[/{mid}]` | `project.manage` | `{ name, plannedDate, actualDate, justification }` |
| GET | `/api/v1/projects/{id}/ee?month=` | `project.view` | EE theo tháng + luỹ kế năm; biểu đồ luỹ kế từng người của 1 tháng |
| GET / PUT | `/api/v1/projects/{id}/quality?kind=defect/leakage&year=` | sửa: `project.manage` hoặc `report.quality` | Tỷ lệ lỗi theo tháng / luỹ kế năm; PUT `{ kind, year, entries: [{ month, defects, productSize }], unit }` |
| GET / PUT | `/api/v1/projects/{id}/pcv?month=` | sửa: `pcv.manage` | Checklist + đánh giá tháng + lịch sử; PUT `{ month, results: [{ itemId, passed, note }] }` |
| POST / PUT / DELETE | `/api/v1/projects/{id}/pcv/items[/{itemId}]` | `pcv.manage` | Tiêu chí riêng của dự án `{ category, label }` |
| GET | `/api/v1/projects/{id}/css?month=` | `project.view` | Form + câu hỏi riêng của dự án, khảo sát của tháng, điểm, lịch sử |
| PUT / POST / PUT / DELETE | `/api/v1/projects/{id}/css/form`, `/api/v1/projects/{id}/css/questions[/{qid}]` | `css.manage` | Sửa form / câu hỏi của dự án |
| POST | `/api/v1/projects/{id}/css/link` | `css.manage` | `{ month }` → `{ token }` (link `/khao-sat-css/{token}`; link cũ của tháng hết hiệu lực) |
| GET | `/api/v1/projects/{id}/documents` | `project.view` | Nhóm tài liệu + tài liệu |
| POST / PUT / DELETE | `/api/v1/projects/{id}/document-groups[/{gid}]`, `/api/v1/projects/{id}/documents[/{did}]` | `project.manage` | `{ name, description }` / `{ groupId, name, url }` |
| GET / POST / PUT / DELETE | `/api/v1/projects/{id}/risks[/{rid}]` | sửa: `project.manage` | Sổ rủi ro (mã R-xx, điểm P×I, mức, quá hạn, ma trận 5×5) |
| GET / POST / PUT / DELETE | `/api/v1/projects/{id}/changes[/{cid}]` | sửa: `project.manage` | CR (mã CR-xx) `{ fileName, url, source, impactLevel, impactAreas, impactNote }` |
| GET / POST / PUT / DELETE | `/api/v1/projects/{id}/issues[/{iid}]` | sửa: `project.manage` | Link issue `{ type: parent / sprint / milestone, kind: task / bug, name, url }` |
| GET | `/api/v1/projects/{id}/monthly-reports/{YYYY-MM}` | `project.view` | Báo cáo tháng (bản nháp nếu chưa có): thông tin chung, cột, nhân sự × ngày; giờ từ Nhật ký đã duyệt, vào / ra từ Chấm công (trừ ô sửa tay) |
| PATCH | `…/monthly-reports/{YYYY-MM}/header`, `…/cells` | `project.view` | `{ field, value }` / `{ employeeId, date, field: hours / start / end / content / custom:<colId>, value }` — lần sửa đầu tạo báo cáo |
| POST | `…/monthly-reports/{YYYY-MM}/sync` | `project.view` | `{ kind: hours / time, employeeId?, date? }` — bỏ đánh dấu sửa tay |
| POST / PUT / DELETE | `…/monthly-reports/{YYYY-MM}/columns[/{colId}]` | `project.view` | Cột bổ sung `{ position, label, labelJa, type }`; cột hệ thống không xoá được |
| GET / PUT | `/api/v1/projects/{id}/status-report` | `project.manage` | Báo cáo tình trạng `{ status: GOOD / RISK / SOS, items: [{ metric, content }] }` |
| POST | `/api/v1/projects/{id}/status-report/ai-fill` | `project.manage` | Connector AI (mock) đề xuất trạng thái + nội dung từ mốc trễ, rủi ro đang mở |


Endpoint mới được thêm dần theo từng màn khi chuyển đổi; BE .NET phải giữ đúng hợp đồng này.

Thao tác ghi (POST/PUT/DELETE) chỉ sửa dữ liệu trong bộ nhớ — khởi động lại mock API là về lại `mock-data/seed`.
