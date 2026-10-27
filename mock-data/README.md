# mock-data — dữ liệu giả lập

Toàn bộ là **dữ liệu hư cấu** (D10), sinh tự động — không sửa tay, sửa `tools/mock-data-gen/generate.mjs` rồi chạy lại:

```bash
node tools/mock-data-gen/generate.mjs                       # mật khẩu demo mặc định Demo@2026
DX_DEMO_PASSWORD='...' node tools/mock-data-gen/generate.mjs # đổi mật khẩu demo (bắt buộc trước khi public)
```

Lệnh trên xoá và ghi lại `seed/`, `ai/`, `jira/`, `base/`, `bitrix/` (và xoá thư mục cũ `github/`, `gitlab/`, `google-drive/` nếu còn). Id sinh theo khoá cố định (uuid v5), chạy lại vẫn ra cùng id. Sau khi sinh lại phải khởi động lại mock API (dữ liệu nạp vào bộ nhớ lúc khởi động).

| Thư mục | Ai đọc | Nội dung |
|---|---|---|
| `seed/` | `tools/mock-api` (DB giả); sau này BE seed tenant demo | Mỗi file = 1 bảng, cột snake_case, có `tenant_id` |
| `ai/insights/<screen_key>.json` | connector `Ai` (mock) | Insight theo màn, tên file trùng `screen_key` trong `shared/screens.json` |
| `ai/chat/scenarios.json`, `ai/translate/vi-ja.json` | connector `Ai` (mock) | Kịch bản Chat AI, bản dịch báo cáo tháng |
| `jira/issues.json`, `base/tasks.json`, `bitrix/deals.json` | nguồn tích hợp Jira / Base / Bitrix (mock) | Issue, công việc, deal — xem ở Quản trị › Tích hợp › Xem dữ liệu |
| `email/outbox/` | connector `Email` (mock) | Email "gửi đi" được ghi ra đây (gitignore) |

Bảng `seed/` mock API đang nạp: `tenants users roles permissions role_permissions user_roles employees departments teams clients master_data projects contract_staff assignments project_members system_rules system_settings ai_insights integrations`. Các bảng còn lại (timesheets, effort, PCV/CSS, nghỉ phép/OT, chấm công, ngày lễ…) để sẵn cho các màn sắp chuyển.

Tài khoản demo: `admin@`, `pm@`, `finance@`, `member@example.com`.
