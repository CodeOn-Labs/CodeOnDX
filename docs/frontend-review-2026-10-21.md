# Rà soát frontend DX — 21/10/2026

Phạm vi: `DX/frontend` (Vue 3 + TS + Vite) so với frontend bản cũ `DXOld/src` (React 19 + Supabase). Chỉ đọc code, không sửa.
Kết quả kiểm tra tĩnh tại thời điểm rà soát: `i18n:check` 0 lỗi (614 key FE, 52 key BE, 530 key đang dùng), `eslint .` sạch, `vue-tsc -b` không lỗi. Chưa có backend .NET — FE chạy với `tools/mock-api` (cổng 5080).

> Dấu vết NAL / HRM / RON / Keycloak / Supabase trong `DXOld` **không phải chuẩn** để so sánh; các mục đó được ghi là "bỏ theo thiết kế", không tính là thiếu.

---

## 1. Tổng quan nhanh

| | DXOld (React) | DX (Vue) |
|---|---|---|
| Stack | React 19, Redux Toolkit + RTK Query, react-hook-form + zod, Radix/shadcn, i18next, recharts, lightweight-charts, ai-sdk | Vue 3.5, Pinia, TanStack Vue Query, Reka UI 2, vue-i18n 11, Tailwind 4, `@lucide/vue`, exceljs |
| Dữ liệu | Supabase trực tiếp (24 file gọi `supabase.from/rpc`, 16 RPC) + một ít `/api` qua RTK Query | Chỉ gọi `/api/v1` (mock API hoặc BE thật), không tự giả lập dữ liệu |
| Ngôn ngữ | vi / en / ja, tách nhiều file JSON theo module | vi / en, mỗi ngôn ngữ 1 file (`locales/vi.json` là nguồn chuẩn) |
| Quy mô | 15 module, ~64.500 dòng (riêng `projects` ~29.400) | 4 module chức năng, ~9.700 dòng |
| Test | Vitest + Testing Library (nhiều test màn dự án, timesheet, monthly report) | **Chưa có test** (chưa cài Vitest) |

Ước lượng tiến độ chuyển đổi: **8/48 màn có giao diện thật** (`auth_login`, `project_list`, 5 màn cấu hình admin, cộng Chat AI/Insight dạng panel dùng chung). Phần còn lại là `ScreenPlaceholder` ("đang phát triển").

---

## 2. Cấu trúc thư mục `frontend/src`

```
src/
├─ main.ts                 # tạo app: Pinia, i18n, VueQueryPlugin, router, installSessionExpiredRedirect()
├─ App.vue
├─ style.css               # Tailwind 4 + class chép từ index.css bản gốc
├─ api/                    # tầng HTTP dùng chung
│  ├─ http.ts              # client fetch duy nhất (ErrorFullModel, refresh token, Accept-Language)
│  ├─ query-client.ts      # QueryClient + toast lỗi toàn cục
│  ├─ types.ts             # ErrorFullModel, PagedResult, MasterData*, ClientOption, SystemRule
│  └─ master-data.api.ts   # /master-data, /clients, /system-rules
├─ app/
│  ├─ router.ts            # route, guard đăng nhập/quyền, route mặc định theo quyền, nhớ route cuối
│  └─ screens.ts           # đọc ../shared/screens.json → ScreenKey, insightScreenChain()
├─ components/layout/      # MainLayout, AdminLayout, PageLayout (khung A/B/C/D + Insight + Chat), Sidebar, ResizeHandle
├─ i18n/                   # createI18n (vi/en, date/number formats), setLocale(), kiểu MessageSchema
├─ locales/                # vi.json, en.json
├─ stores/                 # auth (phiên), ui (bộ lọc theo màn), toast, confirm
├─ shared/
│  ├─ components/          # Button, BaseDialog, ConfirmDialog, EmptyState, PortalTooltip, AnimatedCounter,
│  │                       # RouteProgressBar, ToastContainer, ScreenPlaceholder
│  │  ├─ table/            # CommonTable, PaginationFooter, PageSizeSelect
│  │  ├─ filter/           # FilterPopover, FilterChips
│  │  ├─ form/             # BaseInput, BaseTextarea, FormItem, FormSelect, FormDatePicker, DxCalendar,
│  │  │                    # MonthYearPicker, SwitchToggle
│  │  ├─ insight/          # InsightPanel, InsightAlert, InsightSkeletonList
│  │  └─ export/           # ExportButton/Dialog/Progress, useExport, export.worker.ts (exceljs trong Web Worker)
│  ├─ composables/         # useMasterData / useClients / useSystemRules, usePagination
│  ├─ i18n/                # enum-label.ts, screen-title.ts (nơi duy nhất được ghép key động)
│  └─ utils/               # cn, color, date
└─ modules/
   ├─ auth/                # api/auth.api.ts, screens/auth_login
   ├─ project/             # api/project.api.ts, components/ (badge, cell, CssCustomerForm), screens/project_list, utils/project-view-model.ts
   ├─ admin/               # api/config.api.ts, screens/admin_{category_list,rule_list,setting,pcv_criteria,css_template}
   ├─ ai/                  # api/ai.api.ts, SideChatPanel, ChatFullOverlay, DynamicInsights, MessageBubble, MarkdownLite
   └─ effort/              # utils/effort-calculations.ts (chỉ có công thức, chưa có màn)
```

Liên quan ngoài `frontend/`: `shared/screens.json` (danh mục 48 màn — nguồn duy nhất), `tools/mock-api` (BE giả lập), `tools/i18n-check`, `mock-data/`.

So với DXOld: bản cũ chia `modules/<tên>/{pages,components,services,hooks,schemas,types,utils}`; bản mới chia `modules/<module>/screens/<screen_key>/` theo đúng `screen_key`, mỗi module chỉ một file `api/*.api.ts`. Bản mới chưa có tương đương `hooks/`, `schemas/` (zod) cho từng module.

---

## 3. Màn hình và tính năng

Ký hiệu: ✅ đã làm · 🟡 làm một phần · ⬜ placeholder (có route + quyền + tiêu đề) · ➖ không có trong bản cũ (tính năng mới theo D4) · ✂️ bỏ theo thiết kế.

### 3.1 Công khai / tài khoản

| screen_key | Route | DX | Bản cũ | Ghi chú |
|---|---|---|---|---|
| `auth_login` | `/auth/login` | ✅ | `LoginPage` | Giữ hero gốc, form email/mật khẩu; xử lý 429 khoá đăng nhập do mock trả |
| `auth_forgot_password` | `/auth/forgot-password` | ⬜ ➖ | — | Mới (D4); chưa có endpoint mock |
| `auth_reset_password` | `/auth/reset-password/:token` | ⬜ ➖ | — | Mới (D4); chưa có endpoint mock |
| `css_survey_public` | `/khao-sat-css/:token` | ⬜ | `CssSurveyPage` | Đã có component `CssCustomerForm.vue` (dùng ở màn biểu mẫu CSS) nhưng chưa có màn khảo sát + endpoint public |
| — | `/auth/logout` | — | `LogoutPage` | Bản mới đăng xuất bằng nút ở Sidebar; `NOT_TRACKED` trong router vẫn nhắc `/auth/logout` dù không có route (vô hại) |
| — | `/auth/callback` | ✂️ | `AuthCallbackPage` | Callback Supabase/Keycloak — bỏ (D2, D4) |
| `profile` | `/profile` | ⬜ | `ProfilePage` (~700 dòng) | Thông tin cá nhân, đổi mật khẩu, ngôn ngữ |

### 3.2 Nghiệp vụ chính

| screen_key | Route | DX | Bản cũ | Ghi chú |
|---|---|---|---|---|
| `ai_chat` | `/ai` | 🟡 | `ChatAIFull` | Panel chat cạnh màn + overlay toàn màn đã có (`SideChatPanel`, `ChatFullOverlay`); route `/ai` vẫn là placeholder. Chat là POST một lần, **chưa streaming SSE** (bản cũ dùng ai-sdk stream, render bằng streamdown/json-render) |
| `timesheet` (+ `_day`, `_week`, `_approval`) | `/timesheets` | ⬜ | `TimesheetContainer`, `TimesheetDayPage`, `TimesheetWeekPage`, báo cáo nhóm, từ chối | ~4.800 dòng bản cũ. Nút "Update RON" ✂️ (D4) |
| `leave_request`, `leave_approval` | `/leave` | ⬜ ➖ | — | Nghỉ phép & OT (D4): đã có seed `leave_requests`, `ot_requests`, `leave_balances` nhưng chưa có endpoint mock |
| `attendance` | `/attendance` | ⬜ ➖ | — | Chấm công vào/ra (D4): có seed `attendance_records`, chưa có endpoint |
| `effort` | `/effort` | ⬜ (chỉ có công thức) | `EffortPage` + `EffortGrid` | `effort-calculations.ts` đã port + comment công thức (mẫu D12); chưa có lưới, toolbar, thêm thành viên, ghi chú |
| `project_list` | `/projects` | ✅ (thiếu Import) | `ProjectListPage` | Lọc, chip, thống kê (`/projects/summary`), bảng, phân trang, xuất Excel/CSV. **Chưa có Import dự án** (ImportWizard 5 bước của bản cũ) |
| `project_form` | `/projects/new`, `/projects/:id/edit` | ⬜ | `ProjectNewPage`, `ProjectEditPage` | Gồm thêm nhanh vai trò, thành viên, mốc |
| `project_detail` + 16 màn con | `/projects/:id` | ⬜ | `ProjectDetailPage` + `project-detail/**` | Phần lớn nhất còn lại (xem 3.4) |

### 3.3 Quản trị (`/admin/*`)

| screen_key | DX | Bản cũ | Ghi chú |
|---|---|---|---|
| `admin_category_list` | ✅ | `CategoriesListPage` | CRUD, lọc loại, `metadata.coefficient` cho `performance_level` |
| `admin_rule_list` | ✅ | `RulesListPage` | Sửa giá trị + bật/tắt, kiểm `min`/`max` |
| `admin_setting` | ✅ | `SettingsPage` | Cài đặt chung + ngày lễ (`HolidayCard`) |
| `admin_pcv_criteria` | ✅ | `PcvCriteriaPage` | CRUD tiêu chí; mục mới xếp cuối (BE tự gán `sortSeq`). Cả hai bản đều chưa có đổi thứ tự |
| `admin_css_template` | ✅ | `CssTemplatePage` | Nội dung form, câu hỏi, xem trước |
| `admin_role_list`, `admin_role_form` | ⬜ | `RoleListPage`, `RoleDetailPage` (~3.000 dòng) | Ma trận 21 permission |
| `employee_list`, `employee_detail` | ⬜ | `EmployeeListPage`, `EmployeeDetailPage` (hr) | Bản cũ lấy từ HRM; bản mới tự quản lý (D4) — có seed `employees`, chưa có endpoint |
| `admin_department_list` | ⬜ ➖ | chỉ có service `admin-departments-teams` (dùng trong lọc nhân sự/effort) | Màn mới Phòng ban & Team |
| `admin_client_list` | ⬜ | `ClientListPage` | Mock đã có `GET /clients` (chỉ đọc) |
| `admin_insight_list`, `admin_insight_form` | ⬜ | `InsightListPage`, `InsightDetailPage` | |
| `admin_prompt_template_list`, `_form` | ⬜ | `PromptTemplateListPage`, `_DetailPage` | |
| `admin_integration_list` | ⬜ | `IntegrationsListPage` (~4.600 dòng) + `OAuthCallbackPage` | Bản mới chưa có route OAuth callback cho GitHub/GitLab/Google Drive |
| `admin_language` | ⬜ | `LanguagePage` | |

### 3.4 Chi tiết dự án — còn thiếu toàn bộ

Các màn con trong `screens.json` và phần tương ứng của bản cũ (`modules/projects/components/project-detail/`):

| screen_key | Thành phần bản cũ cần chuyển |
|---|---|
| `project_detail_overview` | `ProjectDetailHeader`, `DetailMetricCard`, `AssignmentTable`, `AddMemberModal`, `ProjectTimelineChart`, biểu đồ workload (`chart/WorkloadTradingChart`, `AggregateWorkloadChart` — lightweight-charts) |
| `project_detail_monthly_report` | `monthly-report/*` (~18 component: bảng ngày, ô giờ/nội dung, cột tuỳ biến, xem trước, đổi ngôn ngữ, insight) + `report/*` (dialog báo cáo, import) |
| `project_detail_metrics` + `_timeline`, `_ee`, `_pcv`, `_css`, `_defect`, `_leakage` | `metrix/*`: Timeline/Milestone, EE (chart, bảng, member board), PCV (checklist, lịch sử), CSS (lịch sử, điểm), Quality (defect/leakage) |
| `project_detail_management` + `_documents`, `_risk`, `_change`, `_issue` | `management/*` (tab con, tìm kiếm, phân trang, từng loại tài liệu/rủi ro/thay đổi/vấn đề) |

Bản mới **chưa có thư viện biểu đồ** (bản cũ dùng recharts + lightweight-charts) — cần chọn trước khi làm Overview/Metrics.

### 3.5 Component dùng chung còn thiếu so với bản cũ

| Bản cũ | Trạng thái ở DX |
|---|---|
| `ImportModal`, `shared/components/import/ImportWizard` (5 bước) + `hooks/import/*` (đọc Excel, map cột, validate, realtime) | **Chưa có** — cần cho Import dự án, chấm công, nhân sự |
| `Checkbox`, `SearchInput`, `SearchableSelect`, `PaginatedSelect`, `FormMultiSelect`, `FormInputNumber`, `FormCheckBox`, `TableInput` | Chưa có bản Vue riêng (FormSelect/FilterPopover hiện thay một phần) |
| `FormMarkdownEditor` (@uiw/react-md-editor) | Chưa có — cần cho prompt template, ghi chú |
| `ToggleColumnsButton`, `TableSkeleton` | Chưa có |
| `LanguageSwitcher`, `LocalizedDate/Number/Currency` | `setLocale()` đã có trong `i18n/index.ts` nhưng **chưa có nút đổi ngôn ngữ nào trên giao diện**; định dạng dùng `$d`/`$n` của vue-i18n |
| `PermissionGate` (ẩn phần tử theo quyền), `PermissionDebug` | Dùng `auth.can()` trực tiếp; chưa có component bọc |
| `useDebounce`, `useDateParser`, `useFormattedDate`, `useRouteTracking`, `useAiActions` | Một phần thay bằng `@vueuse/core`, `shared/utils/date.ts`, router `afterEach`; `useAiActions` chưa có |
| `MonthYearSelect` | ✅ `MonthYearPicker` |
| `CommonTable`, `PaginationFooter`, `PageSizeSelect`, `FilterPopover/Chips`, `Export*`, `Insight*`, `Toast`, `ConfirmProvider`, `AnimatedCounter`, `PortalTooltip`, `RouteProgressBar`, `EmptyState`, `BaseDialog`, `Button` | ✅ đã port |

---

## 4. Cách gọi và xử lý API

### 4.1 Luồng tổng quát

```
Component/màn ──useQuery/useMutation──► modules/<m>/api/*.api.ts ──request()/requestFull()──► api/http.ts ──fetch──► /api/v1/*
                                                                                                         │
                                                         Vite proxy (DX_API_TARGET, mặc định 127.0.0.1:5080) ┘ → tools/mock-api  hoặc  BE .NET 127.0.0.1:8082
```

- **Không có lớp gọi Supabase** như bản cũ; mọi dữ liệu đi qua REST `/api/v1`, đúng Q2/Q3.
- Mỗi module có đúng một file api là object hàm thuần (`projectApi`, `configApi`, `aiApi`, `authApi`, `masterDataApi`), trả Promise — không phải hook. Màn hình tự bọc bằng `useQuery`/`useMutation`. Danh mục dùng chung đã có composable `useMasterData(type)`, `useClients()`, `useSystemRules()` (khoá query có `locale` vì BE dịch tên theo `Accept-Language`).
- D7 (orval sinh type + hook từ OpenAPI) **chưa áp dụng**: type viết tay trong `api/types.ts` và từng `*.api.ts`. Khi có BE cần chạy orval và thay dần.

### 4.2 HTTP client — `src/api/http.ts`

- `API_BASE = '/api/v1'`; `buildUrl` bỏ tham số rỗng, mảng nối bằng dấu phẩy (`statusIds=a,b`).
- Header: `Accept: application/json`, `Accept-Language: <vi|en>`, `Content-Type` khi có body, `Authorization: Bearer <access token>`; `credentials: 'same-origin'` để gửi cookie `dx_rt`.
- `parse()` bắt buộc body là `ErrorFullModel { status, messageText, data }`; không phải JSON (proxy lỗi, server chưa chạy) → `ApiError(status, error.server_unreachable)`. Lỗi mạng → `ApiError(0, error.network)`. Abort giữ nguyên `AbortError`.
- `request<T>()` trả `data`; `requestFull<T>()` trả cả model để màn hình hiện `messageText` của BE sau thao tác ghi (mọi hàm ghi trong `configApi` dùng `requestFull`).
- `ApiError` có `status`, `data`, getter `fieldErrors` (400 → `{ field: [msg] }`). `firstFieldErrors()` trong `config.api.ts` chuyển sang `{ field: msg }` để gắn vào form — nên chuyển helper này lên `shared/` vì các màn sau đều cần.

### 4.3 Xác thực (D2)

- Access token chỉ nằm trong biến module `http.ts` (`setAccessToken`), không localStorage. Refresh token ở cookie httpOnly `dx_rt` do mock/BE đặt.
- `stores/auth.ts`: `bootstrap()` gọi `/auth/refresh` một lần khi khởi động (router `beforeEach` chờ), `login()`, `logout()`, `can(permission | permission[])`.
- Gặp 401: `http.ts` gọi refresh **một lần dùng chung** (biến `refreshing` gộp các request đồng thời), thử lại request; vẫn 401 → `onUnauthorized` xoá phiên, `queryClient.clear()`, router chuyển `/auth/login?redirect=…`. Các API xác thực đặt `skipAuthRefresh`.
- Router: route `public`/`guestOnly`; kiểm quyền trên **mọi** record khớp (cả `/admin`); bị chặn → route mặc định theo `ROUTE_PRIORITY` (giữ như bản cũ), tránh vòng lặp bằng `/ai`. Nhớ route cuối trong localStorage `dx_last_route`.
- Chưa có: quyền theo dự án (thành viên / PM) ở FE — hiện do mock lọc danh sách dự án; chưa có `/auth/me` được dùng (định nghĩa sẵn trong `authApi`).

### 4.4 Query client và xử lý lỗi — `src/api/query-client.ts`

- `staleTime` 30 s, không refetch khi focus; retry tối đa 2 lần chỉ cho lỗi mạng/5xx (4xx không retry).
- `QueryCache`/`MutationCache.onError` bật toast lỗi toàn cục với `messageText` của BE, **trừ** 401 (router xử lý), 400 (hiện dưới field), AbortError, và query/mutation có `meta: { silent: true }` (vd khung Insight tự hiện lỗi tại chỗ).
- 500: BE trả `data = { traceId }` — FE hiện chưa hiển thị traceId cho người dùng/hỗ trợ.

So với bản cũ: RTK Query `baseQueryWithReauth` cũng refresh khi 401 nhưng qua Supabase, không có định dạng response thống nhất, mỗi service tự `to-rtk-error`. Bản mới đơn giản và thống nhất hơn.

### 4.5 Mock API — `tools/mock-api`

- Node thuần, không thư viện; `server.mjs` + `lib/{auth,config-routes,http,localizer,store}.mjs`. Đọc `mock-data/seed/*.json`, ghi chỉ trong bộ nhớ.
- Biến môi trường: `MOCK_LATENCY_MS` (250), `MOCK_FAILURE_RATE` (thử luồng lỗi), `MOCK_TODAY`, `MOCK_API_PORT/HOST`.
- Endpoint hiện có: `auth/{login,refresh,logout,me}`, `master-data`, `clients` (đọc), `system-rules`, `categories`, `settings`, `holidays`, `pcv-template-items`, `css-template(+questions)`, `projects`, `projects/summary`, `ai/insights`, `ai/chat(+greeting)` — danh sách đầy đủ trong `tools/mock-api/README.md`.
- `mock-data/hrm`, `mock-data/ron` **đã được xoá** (ghi chú trong `CLAUDE.md` / migration plan mục 8 đã cũ).
- Seed đã có sẵn nhưng **chưa có endpoint**: `employees`, `departments`, `teams`, `roles`/`role_permissions`/`user_roles`, `timesheets`, `leave_*`, `ot_requests`, `attendance_records`, `effort_*`, `assignments`, `project_members`, `project_milestones`, `project_links`, `project_pcv_*`, `project_css_surveys`, `project_quality_metrics`, `contract_staff`, `integrations`, `ai_prompt_templates`, `ai_insights` (dạng quản trị).

---

## 5. Danh sách việc chưa xong / còn thiếu (ưu tiên đề xuất)

1. **Chi tiết dự án** (`project_detail` + 16 màn con) và **Form dự án** — khối lớn nhất bản cũ (~29.000 dòng module projects); cần chọn thư viện biểu đồ trước.
2. **Nhật ký công việc** (`timesheet` ngày/tuần/duyệt), **Nghỉ phép & OT**, **Chấm công vào/ra**, **Effort** (lưới) — kèm endpoint mock tương ứng.
3. **Quản trị còn lại**: vai trò & quyền, nhân sự (danh sách + chi tiết), phòng ban & team, khách hàng (CRUD), AI Insight, Prompt template, Tích hợp (+ OAuth callback), Ngôn ngữ.
4. **Tài khoản**: quên / đặt lại mật khẩu, hồ sơ cá nhân, mời qua email; khảo sát CSS công khai.
5. **Import Excel** (wizard dùng chung) — phục vụ Import dự án, chấm công, nhân sự.
6. **Component form còn thiếu**: Checkbox, multi-select, searchable/paginated select, input số, markdown editor, toggle cột, skeleton bảng; nút đổi ngôn ngữ trên giao diện.
7. **Chat AI streaming (SSE)** theo D5 và màn `/ai` toàn trang.
8. **Hạ tầng**: Vitest + port unit test logic cũ (effort, project view-model, monthly report…); orval sinh client từ OpenAPI khi có BE (D7); đưa `firstFieldErrors` lên `shared/`; hiển thị `traceId` khi lỗi 500.
9. **Tiếng Nhật**: bản cũ có `ja`; bản mới chỉ vi/en — theo D12 tiếng Nhật là tuỳ chọn, cần quyết định có thêm `ja.json` hay không.
10. **Cập nhật tài liệu**: `CLAUDE.md` và `docs/handoff/dx-migration-plan.md` mục 8 còn ghi "43 màn", "chưa chạy npm verify", "còn mock-data/hrm, ron" — đã lỗi thời (nay 48 màn, verify xanh, hrm/ron đã xoá).
