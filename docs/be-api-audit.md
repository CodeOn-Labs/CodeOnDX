# Kiểm kê API backend DX (26/10/2026)

Nguồn đối chiếu: route trong `tools/mock-api/server.mjs` + `tools/mock-api/lib/*.mjs` (hợp đồng tham chiếu, FE đang gọi) so với `backend/src/Dx.Api/Controllers/*` và `BACKEND_ROUTES` trong `frontend/vite.config.ts`.

**Tổng: mock có 175 endpoint `/api/v1`, BE thật mới có 10 (~6%).** Còn thiếu 165.

> **Tiến độ (26/10):** đợt 1 (Cấu hình + Tổ chức, 39 endpoint, module 2 và 3) và đợt 2 (Phân quyền + Tài khoản + connector Email, 18 endpoint, module 1 và 4) đã xong → BE có **67/175 (38%)**. Đợt 3 (Dự án lõi, 20 endpoint, module 8) cũng xong → **87/175 (50%)**. Đợt 4 (Chấm công, nghỉ phép, OT + Công số, 25 endpoint, module 6 và 7) cũng xong → **112/175 (64%)**, còn thiếu 63. Chi tiết và chỗ khác mock ở `backend/README.md`. Đợt tiếp theo: 5 = Dự án mở rộng + khảo sát CSS công khai (44).

## Đã có ở BE (10)

| Controller | Endpoint |
|---|---|
| `AuthController` | `POST auth/login`, `POST auth/refresh`, `POST auth/logout`, `GET auth/me` |
| `ConfigurationController` | `GET master-data`, `GET system-rules`, `PUT system-rules/{key}`, `GET settings` |
| `ProjectsController` | `GET projects`, `GET projects/summary` |

## Còn thiếu theo module (165)

| # | Module | Thiếu | Endpoint (prefix `/api/v1`) | File mock |
|---|---|---|---|---|
| 1 | Tài khoản & hồ sơ | 6 | `POST auth/forgot-password`, `GET/POST auth/reset-password[/{token}]`, `GET/PUT profile`, `POST profile/change-password` | `account-routes.mjs` |
| 2 | Cấu hình | 18 | `categories` (CRUD 4), `PUT settings`, `holidays` (GET/POST/DELETE), `pcv-template-items` (CRUD 4), `css-template` (GET/PUT), `css-template/questions` (CRUD 4) | `config-routes.mjs` |
| 3 | Tổ chức | 21 | `clients` (list, summary, POST, PUT, DELETE, restore), `departments` (CRUD 4), `teams` (POST/PUT/DELETE), `employees` (list, summary, get, POST, PUT, DELETE, restore, invite) | `org-routes.mjs` |
| 4 | Hệ thống / phân quyền | 12 | `permissions`, `roles` (list, summary, get, POST, PUT, DELETE, `PUT roles/permissions`), `roles/{id}/users` (GET/POST/DELETE), `users` | `system-routes.mjs` |
| 5 | AI cấu hình & tích hợp | 16 | `ai/insight-configs` (CRUD + preview, 6), `ai/prompt-templates` (CRUD + preview, 6), `integrations` (GET, PUT `{key}`, `{key}/test`, `{key}/preview`) | `system-routes.mjs` |
| 6 | Chấm công / nghỉ phép / OT | 21 | `timesheets` (employees, GET, PUT, group, approval), `leave-requests` (list, summary, POST, approve/reject/cancel), `ot-requests` (list, POST, approve/reject/cancel), `attendance` (GET, today, check-in/out, PUT, import) | `work-routes.mjs` |
| 7 | Công số | 4 | `effort` (GET, POST, PATCH, DELETE) | `effort-routes.mjs` |
| 8 | Dự án — lõi | 20 | `POST projects`, `POST projects/import`, `projects/{id}` (GET/PUT/DELETE, restore), `staffing`, `assignable-employees`, `assignments` (POST/PUT/DELETE), `milestones` (CRUD 4), `PATCH dates`, `GET ee`, `quality` (GET/PUT) | `project-routes.mjs` |
| 9 | Dự án — mở rộng | 42 | `pcv` (GET/PUT, items CRUD 3), `css` (GET, form, questions CRUD 3, link), `documents` (GET), CRUD 4 cho `document-groups`, `documents`, `risks`, `changes`, `issues`, `monthly-reports/{yyyy-MM}` (GET, header, cells, sync, columns CRUD 3), `status-report` (GET/PUT, ai-fill) | `project-extra-routes.mjs` |
| 10 | Khảo sát CSS công khai | 2 | `public/css-surveys/{token}` (GET/POST, không cần đăng nhập) | `account-routes.mjs` |
| 11 | AI runtime | 3 | `GET ai/insights`, `GET ai/chat/greeting`, `POST ai/chat` (SSE) | `server.mjs` |

Hạ tầng BE cũng chưa có (theo `CLAUDE.md` mục việc tiếp theo): connector `Integrations:*` + `JsonMockDataStore` (Q3, cần cho Email/AI/GitHub/GitLab/Drive), audit log, Hangfire (job nền), integration test cho mã 400–500, OpenAPI → orval (D7).

## Đề xuất thứ tự làm (theo phụ thuộc)

| Đợt | Nội dung | Số endpoint | Lý do đặt ở đây |
|---|---|---|---|
| 1 | Cấu hình (2) + Tổ chức (3) | 39 | CRUD đơn giản, là dữ liệu nền cho mọi màn khác; mở được toàn bộ tab Quản trị |
| 2 | Phân quyền (4) + Tài khoản (1) + connector Email (Q3) | 18 | Mời / quên mật khẩu cần Email; vai trò cần trước khi siết quyền theo dự án |
| 3 | Dự án lõi (8) | 20 | Luồng tạo dự án → gán người → FTE/EE |
| 4 | Chấm công, nghỉ phép, OT (6) + Công số (7) | 25 | Cần dự án + nhân sự; rules engine kiểm ở BE; đơn duyệt tự sinh dòng công |
| 5 | Dự án mở rộng (9) + CSS công khai (10) | 44 | PCV, CSS, tài liệu, rủi ro, báo cáo tháng |
| 6 | AI (5) + (11) + connector Ai | 19 | Cần Microsoft.Extensions.AI + SSE; `status-report/ai-fill` làm ở đây |

Mỗi endpoint: service ở `Dx.Application/<Tính năng>` → controller kế thừa `ApiControllerBase` → so kết quả với mock cùng request → thêm vào `BACKEND_ROUTES` → unit/integration test.
