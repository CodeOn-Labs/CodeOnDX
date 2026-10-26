# DX Platform — bản chuyển đổi (Vue 3 + .NET 10 + PostgreSQL)

Chuyển đổi từ DX (React 19 + Supabase) theo kế hoạch `claude/dx-migration-plan.md` và quy ước `claude/dx-conventions.md` trong Project **CodeOn Labs**.

Sản phẩm độc lập, đa tenant: nhân sự, phòng ban/team, tài khoản đăng nhập, nghỉ phép, OT và chấm công vào/ra được **quản lý ngay trong phần mềm** — không phụ thuộc hệ thống HRM / chấm công / SSO bên ngoài.

## Cấu trúc

```
codeon-dx/
├─ frontend/                 # Vue 3.5 + TypeScript + Vite 7 + Tailwind 4 + Reka UI + vue-i18n 11 + TanStack Vue Query
│  └─ src/
│     ├─ app/                # router, danh mục màn (screens.ts)
│     ├─ api/                # client HTTP (ErrorFullModel), query client, danh mục dùng chung
│     ├─ components/layout/  # MainLayout, Sidebar, PageLayout (A/B/C/D), AdminLayout, ResizeHandle
│     ├─ i18n/ · locales/    # vue-i18n, vi.json (mặc định) + en.json — key theo màn hình (Q1)
│     ├─ modules/<module>/
│     │  ├─ api/ · components/ · composables/ · utils/
│     │  └─ screens/<screen_key>/   # page + component chỉ màn đó dùng (key i18n: <screen_key>.*)
│     ├─ shared/components/  # Button, BaseDialog, CommonTable, FormSelect, FormDatePicker, export/…  (key: component.<tên>.*)
│     └─ stores/             # Pinia: auth, toast, ui
├─ backend/src/Dx.Api/Locales/   # câu thông báo của BE (MessageText) — vi/en
├─ shared/screens.json       # danh mục màn hình: route, quyền, cha–con Insight (FE + BE + mock cùng đọc)
├─ mock-data/                # dữ liệu giả lập (hư cấu) — xem mock-data/README.md
└─ tools/
   ├─ mock-api/              # API giả lập đứng thay BE .NET (cùng hợp đồng /api/v1)
   ├─ mock-data-gen/         # sinh lại mock-data
   └─ i18n-check/            # kiểm tra luật i18n riêng của dự án
```

## Chạy thử (máy dev)

Yêu cầu: Node.js ≥ 22.12.

```bash
# (Tuỳ chọn) API giả lập http://127.0.0.1:5080 — npm run dev tự bật nếu cổng 5080 còn trống (DX_MOCK_AUTOSTART=0 để tắt)
node tools/mock-api/server.mjs

# Cửa sổ 2 — frontend (http://localhost:5173, /api được proxy sang 5080)
cd frontend
npm install
npm run dev
```

Chế độ API của frontend (proxy `/api` lúc dev, chọn bằng script hoặc biến `DX_API_MODE` trong `frontend/.env`, xem `frontend/.env.example`):

| Lệnh | `DX_API_MODE` | /api đi đâu |
|---|---|---|
| `npm run dev` | `mock` (mặc định) | Mọi endpoint → mock API 5080 |
| `npm run dev:hybrid` | `hybrid` | Endpoint BE .NET đã làm (`BACKEND_ROUTES` trong `frontend/vite.config.ts`) → BE 5090, còn lại → mock 5080. Cần chạy cả BE lẫn mock; mock tự nhận JWT do BE cấp (đọc `Jwt:SigningKey` trong `backend/src/Dx.Api/appsettings.Secrets.json`) |
| `npm run dev:backend` | `backend` | Mọi endpoint → BE 5090 (màn chưa có endpoint sẽ lỗi 404) |

Làm xong endpoint BE mới → thêm method + path vào `BACKEND_ROUTES`.

Tài khoản demo (mật khẩu mặc định `Demo@2026`):

| Email | Vai trò | Thấy gì |
|---|---|---|
| `admin@example.com` | Admin | Toàn bộ chức năng, cả 12 tab Quản trị |
| `pm@example.com` | Quản trị Hệ thống/Dự án | Quản lý + tạo dự án, công số, nhật ký, duyệt công; Quản trị chỉ có tab Nhân sự |
| `finance@example.com` | Quản lý Tài nguyên & Ngân sách | Quản lý dự án (không tạo mới), công số, duyệt công, Nhân sự |
| `member@example.com` | Member | Chỉ dự án mình tham gia (ẩn cột FTE/budget/rủi ro), nhật ký công việc, xin nghỉ phép/OT, chấm công vào/ra |

> **Trước khi mở ra Internet (dx.codeonlabs.vn):** đổi mật khẩu demo — `DX_DEMO_PASSWORD='…' node tools/mock-data-gen/generate.mjs` rồi khởi động lại mock API.

## Kiểm tra

```bash
cd frontend
npm run i18n:check   # luật key i18n theo màn hình, vi/en khớp nhau, không ghép key động, chống dùng key màn khác
npm run lint         # ESLint + @intlify/eslint-plugin-vue-i18n
npm run typecheck    # vue-tsc
npm run build
npm run verify       # cả 4 bước trên
```

## Tiến độ chuyển đổi frontend

| Phần | Trạng thái |
|---|---|
| Khung ứng dụng: router + phân quyền route, phiên đăng nhập (access token trong bộ nhớ, refresh token cookie httpOnly), toast, thanh tiến trình chuyển trang | Xong |
| Layout: Sidebar, MainLayout, PageLayout (A/B/C/D, kéo đổi độ rộng, dải thu gọn Insights / Chat AI), AdminLayout (12 tab) | Xong |
| Component dùng chung: Button, BaseDialog, PortalTooltip, EmptyState, AnimatedCounter, CommonTable + phân trang, FilterPopover/FilterChips, FormItem/BaseInput/FormSelect/FormDatePicker (lịch riêng thay react-day-picker) | Xong |
| AI: khung Insight (DynamicInsights, kế thừa màn cha theo ADR-0001), Chat AI cạnh màn hình + toàn màn hình (nhiều phiên) | Xong (gọi mock AI) |
| Xuất dữ liệu Excel/CSV (Web Worker + ExcelJS, chọn phạm vi + cột) | Xong |
| Màn Đăng nhập (`auth_login`) | Xong — giữ hero gốc, thêm form email/mật khẩu |
| Màn Quản lý dự án (`project_list`) | Xong — lọc, chip, thống kê, bảng, xuất file; Import đang chuyển đổi |
| Màn mới thay hệ thống ngoài: Phòng ban & Team, Nghỉ phép & OT (+ duyệt), Chấm công vào/ra, Quên / đặt mật khẩu | Có route + quyền + menu + dữ liệu mẫu; giao diện đang phát triển |
| Cấu hình theo tenant (D12): Quy tắc (`admin_rule_list`), Danh mục (`admin_category_list`, gồm Mức hiệu suất + hệ số), Cài đặt chung + Ngày lễ (`admin_setting`), Tiêu chí PCV (`admin_pcv_criteria`), Biểu mẫu CSS (`admin_css_template`, có xem trước form khách hàng) | Xong (chạy với mock API) |
| Công thức lưới Công số (`modules/effort/utils/effort-calculations.ts`) | Đã chép, comment chi tiết; màn `effort` chưa làm |
| Các màn còn lại | Có route + quyền + tiêu đề, hiển thị trang "đang phát triển" |

Nguyên tắc giữ giao diện: chép nguyên class Tailwind và CSS toàn cục của bản gốc; Radix UI → Reka UI (cùng `data-state`, cùng hiệu ứng); lucide-react → `@lucide/vue` (cùng tên icon); câu chữ lấy từ `vi.json` gốc.
