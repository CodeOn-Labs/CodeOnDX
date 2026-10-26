> Bản sao của tài liệu `claude/dx-conventions.md` trong claude.ai Project "CodeOn Labs" (chép sang ngày 20/10/2026). Repo `codeon-dx` = `D:\CodeOnLabs\DX`; `claude/dx-migration-plan.md` = `docs/handoff/dx-migration-plan.md`.

# DX Platform (bản .NET 10 + Vue 3) — Quy ước bắt buộc

> Chốt ngày 20/10/2026 theo yêu cầu của anh Hiền (Q1 cập nhật cùng ngày: key theo màn hình + hành động, dùng `$t` của vue-i18n). Áp dụng cho mọi code mới của dự án `codeon-dx`. Đọc kèm `claude/dx-migration-plan.md`.
> Cập nhật 20/10/2026 (3): bỏ connector HRM, RON, SSO (Keycloak) — nhân sự, tài khoản, nghỉ phép, OT, chấm công vào/ra là tính năng tự quản lý trong phần mềm (`claude/dx-migration-plan.md` mục 3.6); thêm 6 màn vào danh mục.
> Cập nhật 20/10/2026 (2) sau khi chuyển những màn FE đầu tiên: quy tắc đặt tên key cho component (theo tên file), sửa mô tả kiểm tra kiểu key (vue-tsc **không** chặn key sai — do eslint + i18n-check đảm nhận), ví dụ dùng component của dự án (Reka UI) thay PrimeVue, mock API ở Q3.

| # | Quy ước | Tóm tắt |
|---|---|---|
| Q1 | Đa ngôn ngữ | Key **snake_case theo màn hình + hành động** (`<screen_key>.<prefix>_<tên>`, vd `project_list.btn_create`), dùng bằng **`$t('…')` chuẩn của vue-i18n** với key đầy đủ; mỗi màn chỉ dùng key của mình + 6 vùng chung; mỗi ngôn ngữ **1 file JSON**; ưu tiên **vi** (mặc định + dự phòng), **en** |
| Q2 | Response API | Mọi action có **try/catch**; mọi response (thành công lẫn lỗi) có cấu trúc cố định **`ErrorFullModel { Status, MessageText, Data }`** |
| Q3 | Kết nối ngoài | Mỗi connector có **sẵn section cấu hình**; có cờ `UseMock`; dữ liệu giả lập nằm trong thư mục riêng **`mock-data/`** |

---

## Q1. Đa ngôn ngữ (i18n) — key theo màn hình + hành động

> Key tổ chức **theo màn hình** (cấp 1) và **loại thành phần / hành động** (tiền tố của key) để mỗi màn sở hữu riêng bộ key, sửa câu chữ ở màn này không ảnh hưởng màn khác.

### Cấu trúc key

```
<screen_key>.<prefix>_<tên>                 # 2 cấp — mặc định
<screen_key>.<block>.<prefix>_<tên>         # 3 cấp — khi màn có dialog / drawer / khối lớn
component.<tên_component>.<prefix>_<tên>    # component dùng ở nhiều màn
```

- Mọi đoạn chỉ gồm `a-z`, `0-9`, `_`; tối đa 3 cấp.
- **Cấp 1 = `screen_key`** lấy từ danh mục màn hình (bên dưới), hoặc một trong 6 vùng dùng chung.
- **Cấp 2/3 bắt đầu bằng tiền tố** cho biết loại thành phần / hành động. Với nút và thông báo, sau tiền tố là **động từ hành động**: `btn_create`, `btn_export_excel`, `confirm_delete_title`, `msg_link_copied`.
- `block` (khi có) kết thúc bằng `_dialog`, `_drawer`, `_panel`, `_section`, ví dụ `add_member_dialog`, `filter_panel`.
- Cùng một chữ ("Trạng thái", "Lưu thay đổi") xuất hiện ở nhiều màn thì **mỗi màn có key riêng** — chấp nhận trùng nội dung để tránh phụ thuộc chéo.
- Tham số dạng `{snake_case}`; số nhiều theo cú pháp `|` của vue-i18n.
- Ký tự đặc biệt của cú pháp message vue-i18n phải viết dạng literal: `@` → `{'@'}` (vd `"ten{'@'}congty.com"`), `|` → `{'|'}`, `{` `}` → `{'{'}` `{'}'}`.

### Tiền tố

| Tiền tố | Dùng cho | Ví dụ |
|---|---|---|
| `title` | Tiêu đề trang, khối, dialog | `title_page`, `title_summary`, `title_popover` |
| `btn` | Nút / hành động người dùng bấm — `btn_<động_từ>[_<đối_tượng>]` | `btn_create`, `btn_save_draft`, `btn_submit_week`, `btn_approve`, `btn_export_excel` |
| `tab` | Tab, subtab | `tab_overview`, `tab_metrics` |
| `lbl` | Nhãn field form, nhãn hiển thị | `lbl_project_name`, `lbl_start_date` |
| `col` | Tiêu đề cột bảng | `col_client`, `col_budget_used` |
| `ph` | Placeholder | `ph_search` |
| `opt` | Lựa chọn trong select / radio riêng của màn | `opt_view_day`, `opt_view_week` |
| `tip` | Tooltip, chú thích công thức | `tip_col_hd`, `tip_formula_ee` |
| `hint` | Dòng hướng dẫn dưới field / trong khối | `hint_import_template` |
| `stat` | Nhãn thẻ KPI / chỉ số | `stat_total`, `stat_at_risk` |
| `unit` | Đơn vị | `unit_hours`, `unit_rows` |
| `empty` | Trạng thái rỗng | `empty_no_data`, `empty_no_result` |
| `confirm` | Hộp xác nhận một hành động — `confirm_<động_từ>_<title\|body\|ok>` | `confirm_delete_title`, `confirm_delete_body`, `confirm_delete_ok` |
| `msg` | Thông báo **chỉ phía client** (copy link, thao tác cục bộ). Thông báo kết quả gọi API lấy từ `messageText` của BE, không khai báo lại ở FE | `msg_link_copied` |
| `err` | Lỗi kiểm tra **phía client** trước khi gọi API | `err_end_before_start` |

### Vùng dùng chung (được dùng ở mọi màn)

| Vùng | Nội dung | Ví dụ |
|---|---|---|
| `common` | Chữ chung không mang nghĩa nghiệp vụ, dùng y hệt ở nhiều màn | `common.btn_close`, `common.lbl_test_environment` |
| `menu` | Mục sidebar, tab quản trị, breadcrumb (không cần tiền tố) | `menu.projects`, `menu.timesheets`, `menu.admin_roles`, `menu.sign_out` |
| `enum` | Nhãn giá trị enum / master data — `enum.<tên_enum>.<mã_viết_thường>` | `enum.project_status.running`, `enum.risk_level.high` |
| `validation` | Thông báo kiểm tra field chung phía client | `validation.required`, `validation.invalid_email` |
| `error` | Lỗi hệ thống chung phía client (mất mạng, máy chủ trả sai định dạng) | `error.network`, `error.server_unreachable` |
| `component` | Component dùng ở nhiều màn — `component.<tên_file_snake_case>.<prefix>_<tên>` | `component.form_date_picker.opt_today`, `component.dynamic_insights.empty_no_insight`, `component.page_size_select.lbl_rows_per_page`, `component.side_chat_panel.btn_new_session` |

**Tên component trong key = tên file `.vue` đổi sang snake_case** (`FormDatePicker.vue` → `component.form_date_picker`, `SideChatPanel.vue` → `component.side_chat_panel`), để nhìn key là biết file.

**Luật chống xung đột:** một màn chỉ được dùng key của **chính nó** và của 6 vùng dùng chung — không bao giờ dùng key của màn khác. Text nào cần ở màn khác thì tạo key mới trong màn đó.

### Danh mục màn hình (`screen_key`)

Một nguồn duy nhất `shared/screens.json` ở gốc repo, FE, BE và mock API cùng đọc. Mỗi `screen_key` dùng cho cả 4 việc:
1. cấp 1 của key i18n (mọi màn bắt buộc có `<screen_key>.title_page` — dùng làm tiêu đề tab trình duyệt);
2. `meta.screen` của route Vue (kèm `permission`);
3. **Màn hình áp dụng của AI Insight** (`ai_insights.screen_key`), trong đó trường `parent` thể hiện quan hệ kế thừa theo ADR-0001 (BE gộp insight màn hiện tại + các màn cha, dừng ở màn `inherit_insights: false`);
4. tên file mock `mock-data/ai/insights/<screen_key>.json`.

Màn con đặt tên bắt đầu bằng tên màn cha (`project_detail_metrics_timeline` ⊂ `project_detail_metrics` ⊂ `project_detail`), nên đọc key là biết màn nằm ở đâu.

| Nhóm | `screen_key` | Route / vị trí | Màn hình áp dụng cũ ở DX |
|---|---|---|---|
| Xác thực | `auth_login` | `/auth/login` | — |
| | `auth_forgot_password`, `auth_reset_password` | `/auth/forgot-password`, `/auth/reset-password/:token` (đặt mật khẩu từ lời mời / quên mật khẩu) | — |
| Công khai | `css_survey_public` | `/khao-sat-css/:token` | — |
| Cá nhân | `profile` | `/profile` | — |
| AI | `ai_chat` | `/ai` (khung chat cạnh màn hình dùng `component.side_chat_panel`, chat toàn màn hình `component.chat_full_overlay`) | — |
| Chấm công | `timesheet` | `/timesheets` — khung chung: đổi chế độ ngày/tuần, lọc nhân sự | `Timesheet` |
| | `timesheet_day` | chế độ ngày | `TimesheetDay` |
| | `timesheet_week` | chế độ tuần | `TimesheetWeek` |
| | `timesheet_approval` | duyệt / từ chối theo nhóm | — |
| Nghỉ phép & OT | `leave_request` | `/leave` — đơn nghỉ phép, đơn OT, quỹ phép của tôi | — (mới) |
| | `leave_approval` | tab duyệt / từ chối đơn | — (mới) |
| Chấm công vào/ra | `attendance` | `/attendance` — nút Vào/Ra, bảng theo tháng, import file máy chấm công | — (mới) |
| Công số | `effort` | `/effort` | `EffortList` |
| Dự án | `project_list` | `/projects` | `ProjectList` |
| | `project_form` | `/projects/new` và `/projects/:id/edit` — dùng chung 1 form (`title_create`, `title_edit`) | — |
| | `project_detail` | `/projects/:id` — header, thanh tab | `ProjectDetail` |
| | `project_detail_overview` | `?tab=overview` | — |
| | `project_detail_monthly_report` | `?tab=monthly-report` | `ProjectMonthlyReport` |
| | `project_detail_metrics` | `?tab=metrix` — thanh subtab | `ProjectMetrix` |
| | `project_detail_metrics_timeline` · `_ee` · `_pcv` · `_css` · `_defect` · `_leakage` | 6 subtab Metrics | `ProjectTimeline`, `ProjectEE`, `ProjectPcv`, `ProjectCss`, `ProjectDefect`, `ProjectLeakage` |
| | `project_detail_management` | `?tab=management` — thanh subtab | `ProjectManagement` |
| | `project_detail_management_documents` · `_risk` · `_change` · `_issue` | 4 subtab Quản trị dự án (không kế thừa Insight của màn cha, giữ ngoại lệ hiện có) | `ProjectMgmtDocuments`, `ProjectMgmtRisk`, `ProjectMgmtChange`, `ProjectMgmtIssue` |
| Nhân sự | `employee_list`, `employee_detail` | `/admin/employees`, `/admin/employees/:id` | `HRList`, `HRDetail` |
| | `admin_department_list` | `/admin/departments` — Phòng ban & Team | — (mới) |
| Quản trị | `admin_role_list`, `admin_role_form` | `/admin/roles`, `/admin/roles/new`, `/:id` | — |
| | `admin_rule_list` | `/admin/rules` | — |
| | `admin_insight_list`, `admin_insight_form` | `/admin/insights…` | — |
| | `admin_prompt_template_list`, `admin_prompt_template_form` | `/admin/prompt-templates…` | — |
| | `admin_integration_list` | `/admin/integrations` | — |
| | `admin_category_list`, `admin_client_list` | `/admin/categories`, `/admin/clients` | — |
| | `admin_setting`, `admin_language` | `/admin/settings`, `/admin/language` | — |
| | `admin_pcv_criteria`, `admin_css_template` | `/admin/pcv-criteria`, `/admin/css-template` | — |

Thêm màn mới = thêm 1 dòng vào `shared/screens.json` trước, rồi mới thêm key.

```json
// shared/screens.json (trích)
{
  "project_list":                    { "route": "/projects",     "permission": ["project.view"] },
  "project_detail":                  { "route": "/projects/:id", "permission": ["project.view"] },
  "project_detail_metrics":          { "parent": "project_detail" },
  "project_detail_metrics_timeline": { "parent": "project_detail_metrics" },
  "project_detail_management_risk":  { "parent": "project_detail_management", "inherit_insights": false }
}
```

### Ví dụ `frontend/src/locales/vi.json` (trích file thật)

```json
{
  "common": { "btn_close": "Đóng", "lbl_test_environment": "Môi trường kiểm thử" },
  "menu": { "projects": "Dự án", "timesheets": "Nhật ký công việc", "effort": "Công số", "admin_roles": "Quản lý vai trò" },
  "error": { "network": "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại." },
  "enum": {
    "project_status": { "running": "Đang chạy", "ending": "Sắp KT", "planning": "Kế hoạch", "completed": "Hoàn thành" },
    "risk_level": { "high": "Cao", "medium": "TB", "low": "Thấp" }
  },
  "project_list": {
    "title_page": "Quản lý dự án",
    "btn_create": "Tạo dự án mới",
    "btn_export": "Export",
    "ph_search": "Tìm theo tên dự án...",
    "stat_total": "Tổng dự án",
    "stat_at_risk": "Cảnh báo rủi ro",
    "col_project_info": "Thông tin dự án",
    "col_status": "Trạng thái",
    "lbl_id": "ID: {id}",
    "empty_no_project": "Không tìm thấy dự án phù hợp.",
    "msg_import_converting": "Chức năng Import dự án đang được chuyển đổi sang phiên bản mới."
  },
  "auth_login": { "title_page": "Đăng nhập", "ph_email": "ten{'@'}congty.com", "btn_login": "Đăng nhập" },
  "component": {
    "pagination_footer": { "lbl_total_records": "trong tổng số {total} bản ghi" },
    "export_dialog": { "lbl_columns": "Cột thông tin hiển thị ({active}/{total})", "btn_start_export": "Bắt đầu xuất" }
  }
}
```

### Phía BE — key theo **chức năng + hành động**
BE không có màn hình, nên `MessageText` đặt key theo hành động của API: `<chức_năng>_<hành_động>.<prefix>_<tên>`, mỗi handler (command) có đúng 1 nhóm. Ví dụ handler `CreateProjectHandler` → nhóm `project_create`.

- `msg_success` — thông báo thành công của hành động.
- `err_<lý_do>` — lỗi nghiệp vụ (422 / 409 / 404 riêng).
- Vùng dùng chung của BE: `common` (`msg_success` mặc định), `error` (401/403/404/405/409/429/500 chung), `validation`.
- Thông báo `validation` **không chứa tên field**, vì FE hiển thị ngay dưới field đó.

```json
// backend/src/Dx.Api/Locales/vi.json (trích)
{
  "common": { "msg_success": "Thành công" },
  "error": {
    "validation_failed": "Dữ liệu không hợp lệ",
    "unauthorized": "Phiên đăng nhập đã hết hạn",
    "forbidden": "Bạn không có quyền thực hiện thao tác này",
    "not_found": "Không tìm thấy dữ liệu",
    "conflict": "Dữ liệu đã tồn tại",
    "concurrency": "Dữ liệu đã được người khác cập nhật, vui lòng tải lại",
    "too_many_requests": "Bạn thao tác quá nhanh, vui lòng thử lại sau",
    "internal": "Hệ thống đang gặp sự cố, vui lòng thử lại sau"
  },
  "validation": { "required": "Không được để trống", "max_length": "Tối đa {max} ký tự", "invalid_email": "Email không hợp lệ" },
  "auth_login": { "msg_success": "Đăng nhập thành công", "err_invalid_credentials": "Email hoặc mật khẩu không đúng" },
  "project_create": { "msg_success": "Đã tạo dự án {project_name}", "err_name_exists": "Tên dự án {project_name} đã tồn tại" },
  "timesheet_week_save": {
    "msg_success": "Đã lưu công tuần {week}",
    "err_week_locked": "Kỳ chấm công đã khoá từ ngày {lock_date}",
    "err_day_max_hours": "Ngày {date} vượt quá {max} giờ"
  },
  "css_survey_submit": { "msg_success": "Cảm ơn bạn đã gửi đánh giá", "err_already_submitted": "Khảo sát này đã được gửi", "err_link_invalid": "Link khảo sát không hợp lệ hoặc đã được tạo lại" }
}
```

### Cách dùng trong code — `$t` chuẩn của vue-i18n

Dùng thẳng API của vue-i18n (v11, Composition API), **luôn ghi đủ key dạng chuỗi cố định** `<screen_key>.<prefix>_<tên>`, không bọc hàm riêng, không ghép key từ biến:

| Ở đâu | Dùng | Ví dụ |
|---|---|---|
| Template `.vue` | `$t`, `$d`, `$n` (được inject toàn cục) | `{{ $t('project_list.title_page') }}` |
| `<script setup>` | `const { t, d, n } = useI18n()` — scope toàn cục, cùng kho message với `$t` | `toast.info(t('project_list.msg_import_converting'))` |
| Ngoài component (Pinia store, composable, router guard, client HTTP) | `i18n.global.t` (hoặc `const { t } = i18n.global`) | `i18n.global.t('error.network')` |
| Câu có thẻ / component bên trong | Component `<i18n-t keypath="…" scope="global">` + slot (không dùng `v-html`) | xem ví dụ dưới |
| Danh sách lựa chọn / cấu hình theo biến | Dựng mảng trong `computed` với `t('…')` cố định cho từng phần tử, template chỉ hiển thị chuỗi đã dịch | `[{ key: 'clientIds', label: t('project_list.lbl_client') }, …]` |

**Không dùng:** `$tc` và directive `v-t` (đã deprecated), message cục bộ (`useI18n({ messages })`, khối `<i18n>` trong SFC), `$t(bien)` / `` t(`…${x}`) `` ngoài `src/shared/i18n/` — mọi câu chữ chỉ nằm trong `vi.json` / `en.json`.

```ts
// frontend/src/i18n/index.ts (trích)
import { createI18n } from 'vue-i18n';
import vi from '@/locales/vi.json';
import en from '@/locales/en.json';

export type MessageSchema = typeof vi;          // vi.json là nguồn chuẩn của kiểu → IDE gợi ý key
export type AppLocale = 'vi' | 'en';

export const i18n = createI18n<[MessageSchema], AppLocale, false>({   // false = Composition API → i18n.global là Composer
  legacy: false,
  globalInjection: true,         // $t / $d / $n trong template
  locale: initialLocale(),       // localStorage 'dx_locale' → 'vi'
  fallbackLocale: 'vi',          // thiếu key tiếng Anh thì hiện tiếng Việt
  missingWarn: import.meta.env.DEV,
  fallbackWarn: false,
  messages: { vi, en },
  datetimeFormats: { vi: dateFormats, en: dateFormats },     // date_short, date_time, month_year, time_short
  numberFormats: { vi: numberFormats, en: numberFormats },   // integer, decimal, fte, percent, currency_usd, currency_vnd
});

// frontend/src/i18n/vue-i18n.d.ts — IDE gợi ý key theo vi.json
import type { MessageSchema } from './index';
declare module 'vue-i18n' {
  export interface DefineLocaleMessage extends MessageSchema {}
}
```

> Lưu ý: hàm `t` của vue-i18n v11 vẫn nhận **mọi chuỗi** ở mức kiểu, nên `vue-tsc` **không** báo key sai. Key không tồn tại, thiếu bản dịch, sai định dạng hay ghép động được chặn bởi `@intlify/vue-i18n/no-missing-keys` (eslint) và `tools/i18n-check` — cả hai chạy trong `npm run verify` / CI.

```vue
<!-- src/modules/project/screens/project_list/ProjectFilterBar.vue (rút gọn) -->
<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { Plus } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';

const { t } = useI18n();
const selects = computed(() => [
  { key: 'clientIds', label: t('project_list.lbl_client'), placeholder: t('project_list.ph_all_clients') },
  { key: 'statusIds', label: t('project_list.lbl_status'), placeholder: t('project_list.ph_all_statuses') },
]);
</script>

<template>
  <h1>{{ $t('project_list.title_page') }}</h1>
  <input :placeholder="$t('project_list.ph_search')" />
  <Button variant="primary" @click="emit('newProject')">
    <template #leftIcon><Plus :size="16" /></template>
    {{ $t('project_list.btn_create') }}
  </Button>
  <FormSelect v-for="s in selects" :key="s.key" :label="s.label" :placeholder="s.placeholder" :options="…" multiple searchable />

  <!-- tham số có tên -->
  <span>{{ $t('project_list.lbl_id', { id: project.id.slice(0, 8) }) }}</span>

  <!-- ngày, số theo locale -->
  <span>{{ $d(new Date(project.startDate), 'date_short') }} · {{ $n(project.budget, 'currency_vnd') }}</span>

  <!-- câu có thẻ bên trong -->
  <i18n-t keypath="component.export_dialog.lbl_selected_columns" scope="global">
    <template #active><strong>{{ activeCount }}</strong></template>
    <template #total><strong>{{ columns.length }}</strong></template>
  </i18n-t>
</template>
```

```ts
// frontend/src/shared/i18n/enum-label.ts — nơi DUY NHẤT (cùng screen-title.ts) được ghép key động
import { i18n } from '@/i18n';
export type EnumName = 'project_status' | 'risk_level' | 'timesheet_status';
/** enumLabel('project_status', 'RUNNING', status.name) → "Đang chạy"; không có key thì trả tên master data do BE gửi. */
export function enumLabel(enumName: EnumName, code: string | null | undefined, fallback = ''): string {
  if (!code) return fallback;
  const key = `enum.${enumName}.${code.toLowerCase()}`;
  return i18n.global.te(key) ? i18n.global.t(key) : fallback || code;
}

// frontend/src/shared/i18n/screen-title.ts
export const screenTitle = (screen: ScreenKey) => i18n.global.t(`${screen}.title_page`);
```

- **Đổi ngôn ngữ:** `setLocale('en')` trong `src/i18n` — đặt `i18n.global.locale.value`, `<html lang>`, lưu `localStorage`; client HTTP gửi `Accept-Language` theo locale đang chọn; query có dữ liệu đã dịch (master data, insight) đặt `locale` vào query key để tự tải lại. Khi có BE: lưu thêm `users.preferred_locale`.
- **Thông báo kết quả gọi API:** hiện `messageText` BE trả về (đã dịch theo `Accept-Language`), không gọi `$t` cho các câu này.
- **Câu chữ khi chuyển màn từ DX:** lấy đúng giá trị trong `src/i18n/locales/vi|en/*.json` của bản React (không lấy chuỗi dự phòng viết trong code), chữ còn hard-code trong bản React thì đưa vào locale với nguyên văn tiếng Việt.

```csharp
// BE: key theo hành động của handler
return Success(data, "project_create.msg_success", StatusCodes.Status201Created);
throw new BusinessException("timesheet_week_save.err_week_locked",
    args: new() { ["lock_date"] = lockDate.ToString("dd/MM/yyyy") });
```

### Thư mục FE gắn với màn
- Mỗi màn có thư mục `src/modules/<module>/screens/<screen_key>/` chứa page và các component **chỉ** màn đó dùng; tên thư mục trùng `screen_key`; các file trong đó dùng key `<screen_key>.*` + vùng chung.
- Component dùng ở nhiều màn đặt ở `src/shared/components/<nhóm>/` hoặc `src/modules/<module>/components/`, dùng `component.<tên_file_snake>.*` + `common` / `menu` / `enum` / `validation` / `error`.
- Layout (`src/components/layout/`) dùng `menu.*`, `common.*`, `component.*`.
- File `.ts` khác (store, composable, router, client HTTP) chỉ dùng vùng chung.

### Vị trí file
```
shared/screens.json           # danh mục màn hình — nguồn duy nhất
frontend/src/locales/vi.json  # mặc định, nguồn chuẩn
frontend/src/locales/en.json
backend/src/Dx.Api/Locales/vi.json
backend/src/Dx.Api/Locales/en.json
```

### Cấu hình
- **FE (vue-i18n v11, Composition API, `globalInjection`):** cấu hình ở `src/i18n/index.ts` (mục *Cách dùng trong code*); `locale` lấy theo `localStorage` → `'vi'` (sau thêm `users.preferred_locale`); `fallbackLocale: 'vi'`. Mỗi request gửi header `Accept-Language` theo locale đang chọn.
- **BE:** `RequestLocalizationMiddleware` với culture `vi` (mặc định) và `en`, đọc `Accept-Language`. `JsonStringLocalizer` (tự viết, đọc `Locales/*.json`, cache; thiếu key `en` thì lấy `vi`; thiếu cả hai thì trả nguyên key và ghi log cảnh báo) dùng cho `MessageText` và message của FluentValidation (`.WithMessage("validation.required")`). Mock API (`tools/mock-api/lib/localizer.mjs`) làm y hệt.
- Ngày giờ, số, tiền tệ format ở FE bằng `$d` / `$n` với format đặt tên snake_case (`date_short`, `date_time`, `month_year`, `time_short`, `integer`, `decimal`, `fte`, `percent`, `currency_usd`, `currency_vnd`) hoặc `formatDate()` (date-fns, mẫu `dd/MM/yyyy` như bản gốc); BE luôn trả ISO 8601 và số thô.

### Kiểm tra tự động (chạy trong CI và `npm run verify` — lỗi là fail build)

**1. ESLint với `@intlify/eslint-plugin-vue-i18n`** — phần kiểm tra chuẩn của vue-i18n:

```js
// frontend/eslint.config.js (trích)
import pluginVue from 'eslint-plugin-vue';
import vueI18n from '@intlify/eslint-plugin-vue-i18n';
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript';

export default defineConfigWithVueTs(
  pluginVue.configs['flat/essential'],
  vueTsConfigs.recommended,
  ...[vueI18n.configs['flat/recommended'] ?? vueI18n.configs.recommended].flat(),
  {
    settings: { 'vue-i18n': { localeDir: './src/locales/*.json', messageSyntaxVersion: '^11.0.0' } },
    rules: {
      '@intlify/vue-i18n/key-format-style': ['error', 'snake_case', { splitByDots: true }],
      '@intlify/vue-i18n/no-dynamic-keys': 'error',
      '@intlify/vue-i18n/no-missing-keys': 'error',
      '@intlify/vue-i18n/no-missing-keys-in-other-locales': 'error',
      '@intlify/vue-i18n/no-duplicate-keys-in-locale': 'error',
      '@intlify/vue-i18n/no-v-html': 'error',
      '@intlify/vue-i18n/no-unused-keys': ['warn', { extensions: ['.ts', '.vue'], ignores: ['/^enum\\./', '/\\.title_page$/'] }],
      '@intlify/vue-i18n/no-raw-text': ['warn', { ignorePattern: '^[-–—#:()&%/+.,|·×\\s0-9]+$' }],
    },
  },
  { files: ['src/shared/i18n/**'], rules: { '@intlify/vue-i18n/no-dynamic-keys': 'off' } },
);
```

**2. `npm run i18n:check`** (`tools/i18n-check/i18n-check.mjs`, Node thuần, không cần cài gói) — các luật riêng của dự án mà plugin không có:
1. Cấp 1 của mọi key thuộc `shared/screens.json`, hoặc là 1 trong 6 vùng dùng chung; `component` và `enum` luôn đủ 3 cấp.
2. Mọi đoạn snake_case; tối đa 3 cấp; khối cấp 2 của màn kết thúc `_dialog/_drawer/_panel/_section`; key lá bắt đầu bằng tiền tố hợp lệ (trừ vùng `menu`, `enum`, `validation`, `error`).
3. `vi.json` và `en.json` cùng bộ key, cùng tham số `{x}`, không có giá trị rỗng; mọi màn có `<screen_key>.title_page`.
4. Quét mã nguồn (`$t`, `t`, `te`, `i18n.global.t`, `<i18n-t keypath>`): key phải tồn tại trong `vi.json`; **không ghép key động** ngoài `src/shared/i18n/`; **luật chống xung đột** theo thư mục (mục *Thư mục FE gắn với màn*) — dùng key màn khác là lỗi.
5. Locale BE: snake_case, đúng 2 cấp `<nhóm>.<key>`, key lá là `msg_success` / `err_<lý_do>` (trừ `common`, `error`, `validation`), vi/en khớp key và tham số. Thêm unit test BE quét `Success(...)`, `BusinessException(...)` khi có BE.

**3. `vue-tsc -b`** — kiểm tra kiểu toàn bộ FE (prop, emit, slot, kiểu dữ liệu API); không kiểm tra key i18n (xem lưu ý ở trên).

### Chuyển locale cũ của DX
DX có 14 file / ngôn ngữ, 2.387 key, tổ chức theo **module** (`projects.json`, `timesheets.json`…), 1.918 key camelCase. Không đổi tên máy móc được vì key mới tổ chức theo màn, nên `tools/convert-locales` làm 3 bước:
1. **Tìm nơi dùng:** với mỗi key cũ, quét các file `.tsx` gọi tới nó; ánh xạ đường dẫn file → `screen_key` qua bảng `tools/convert-locales/path-to-screen.json` (ví dụ `modules/projects/pages/ProjectListPage.tsx`, `components/ProjectTable.tsx`, `ProjectFilterBar.tsx`, `ProjectStatsCards.tsx` → `project_list`).
2. **Đề xuất key mới:** đặt dưới màn dùng nó; key dùng ở nhiều màn thì nhân bản vào từng màn (hoặc đưa vào `common` nếu là chữ chung); đoán tiền tố theo hậu tố tên cũ (`…Title` → `title_`, `…Placeholder` → `ph_`, `…Tooltip` → `tip_`, `column…` → `col_`); đổi `{{var}}` → `{var}`, dạng số nhiều `_one/_other` → `|`, `@` → `{'@'}`.
3. **Xuất `mapping.csv`** (key cũ · file dùng · key mới · vi · en) để người port từng màn duyệt; sau khi duyệt mới sinh `vi.json` / `en.json`. Key không còn file nào dùng → bỏ.

Hai màn đầu (`auth_login`, `project_list`) và các component dùng chung đã chuyển tay theo đúng quy tắc trên — dùng làm mẫu đối chiếu cho script.

Tiếng Nhật (`ja`, mới có 1 file) chưa đưa vào. Ngôn ngữ của **nội dung** Báo cáo tháng (vi/ja) là dữ liệu nghiệp vụ, không thuộc i18n giao diện.

---

## Q2. Cấu trúc response cố định — `ErrorFullModel`

### Model
```csharp
namespace Dx.Api.Common;

public class ErrorFullModel
{
    public int Status { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public object? Data { get; set; }
}

/// Cùng cấu trúc JSON, chỉ dùng cho tài liệu OpenAPI để orval sinh kiểu `data` chính xác.
public sealed class ErrorFullModel<T>
{
    public int Status { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public T? Data { get; set; }
}
```
JSON trả về theo mặc định của ASP.NET Core (camelCase): `{ "status": 200, "messageText": "Thành công", "data": { … } }`.

### Bảng mã Status
HTTP status code của response **bằng** `Status` trong body (giữ ngữ nghĩa REST, tunnel/giám sát nhận đúng lỗi).

| Status | Khi nào | Key của `MessageText` | `Data` |
|---|---|---|---|
| 200 | Thành công | `<chức_năng>_<hành_động>.msg_success` (vd `project_update.msg_success`); query đọc dữ liệu dùng `common.msg_success` | Dữ liệu trả về |
| 201 | Tạo mới | `<chức_năng>_create.msg_success` (vd `project_create.msg_success`) | Bản ghi vừa tạo |
| 400 | Dữ liệu không hợp lệ (validation, model binding) | `error.validation_failed` | `{ "<field>": ["<thông báo>"] }` — tên field camelCase khớp DTO để FE gắn vào form |
| 401 | Chưa đăng nhập / token hết hạn | `error.unauthorized` | `null` |
| 403 | Không đủ quyền | `error.forbidden` | `null` |
| 404 | Không tìm thấy | `error.not_found` | `null` |
| 405 | Sai method | `error.method_not_allowed` | `null` |
| 409 | Trùng dữ liệu, xung đột cập nhật đồng thời | `error.conflict` / `error.concurrency` | `null` |
| 422 | Vi phạm quy tắc nghiệp vụ (vd: kỳ chấm công đã khoá) | `<chức_năng>_<hành_động>.err_<lý_do>`, vd `timesheet_week_save.err_week_locked` | Chi tiết nếu cần |
| 429 | Vượt rate limit | `error.too_many_requests` | `null` |
| 500 | Lỗi không lường trước | `error.internal` | `{ "traceId": "…" }` để tra log |

Danh sách có phân trang: `Data = { "items": [...], "page": 1, "pageSize": 50, "totalCount": 123 }` (`page` bắt đầu từ 1; `pageSize = 0` = lấy tất cả).

### Exception nghiệp vụ (tầng Application ném ra, không tự bắt)
```csharp
public abstract class AppException(string messageKey, object? data = null, Dictionary<string, object?>? args = null)
    : Exception(messageKey)
{
    public string MessageKey { get; } = messageKey;
    public object? ErrorData { get; } = data;
    public Dictionary<string, object?> Args { get; } = args ?? new();
}
public sealed class AppValidationException(IDictionary<string, string[]> errors) : AppException("error.validation_failed", errors);
public sealed class NotFoundException(string key = "error.not_found") : AppException(key);
public sealed class ForbiddenException(string key = "error.forbidden") : AppException(key);
public sealed class ConflictException(string key = "error.conflict") : AppException(key);
public sealed class BusinessException(string key, object? data = null, Dictionary<string, object?>? args = null)
    : AppException(key, data, args);

// ví dụ:
// throw new BusinessException("timesheet_week_save.err_week_locked", args: new() { ["lock_date"] = lockDate.ToString("dd/MM/yyyy") });
```
Tham số trong file locale BE đặt tên dạng `{snake_case}` (giống FE); `JsonStringLocalizer.Text(key, args)` thay theo tên, không dùng `string.Format` vị trí `{0}`.

### Controller — mọi action đều có try/catch
```csharp
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private JsonStringLocalizer L => HttpContext.RequestServices.GetRequiredService<JsonStringLocalizer>();
    private ILogger Logger => HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

    protected IActionResult Success(object? data = null, string messageKey = "common.msg_success", int status = StatusCodes.Status200OK)
        => StatusCode(status, new ErrorFullModel { Status = status, MessageText = L.Text(messageKey), Data = data });

    protected IActionResult HandleException(Exception ex)
    {
        (int Status, string Key, object? Data, Dictionary<string, object?>? Args) r = ex switch
        {
            AppValidationException e     => (400, e.MessageKey, e.ErrorData, e.Args),
            NotFoundException e          => (404, e.MessageKey, null, e.Args),
            ForbiddenException e         => (403, e.MessageKey, null, e.Args),
            ConflictException e          => (409, e.MessageKey, null, e.Args),
            DbUpdateConcurrencyException => (409, "error.concurrency", null, null),
            BusinessException e          => (422, e.MessageKey, e.ErrorData, e.Args),
            _                            => (500, "error.internal", new { traceId = HttpContext.TraceIdentifier }, null)
        };

        if (r.Status >= 500) Logger.LogError(ex, "Unhandled error {TraceId}", HttpContext.TraceIdentifier);
        else Logger.LogWarning("Handled {Status} {Key} {TraceId}", r.Status, r.Key, HttpContext.TraceIdentifier);

        // Không bao giờ trả ex.Message / stack trace ra ngoài
        return StatusCode(r.Status, new ErrorFullModel { Status = r.Status, MessageText = L.Text(r.Key, r.Args), Data = r.Data });
    }
}

[Route("api/v1/projects")]
public sealed class ProjectsController(IProjectService projects) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.ProjectView)]
    [ProducesResponseType<ErrorFullModel<ProjectDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var data = await projects.GetByIdAsync(id, ct);
            return Success(data);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    [HttpPost]
    [HasPermission(Permissions.ProjectCreate)]
    [ProducesResponseType<ErrorFullModel<ProjectDto>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateProjectRequest request, CancellationToken ct)
    {
        try
        {
            var data = await projects.CreateAsync(request, ct);
            return Success(data, "project_create.msg_success", StatusCodes.Status201Created);
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }
}
```

### Lưới an toàn cho lỗi xảy ra **ngoài** action (bắt buộc cấu hình trong `Program.cs`)
Các lỗi này không đi qua try/catch của action, nhưng vẫn phải trả đúng `ErrorFullModel`:

| Tình huống | Cấu hình |
|---|---|
| Model binding / `[ApiController]` tự kiểm tra ModelState | `ApiBehaviorOptions.InvalidModelStateResponseFactory` → 400 |
| JWT thiếu / hết hạn | `JwtBearerEvents.OnChallenge` → 401 |
| Policy phân quyền từ chối | `IAuthorizationMiddlewareResultHandler` tuỳ biến → 403 |
| Sai route, sai method dưới `/api` | `UseStatusCodePages` (chỉ cho `/api/*`) → 404 / 405 |
| Vượt rate limit | `RateLimiterOptions.OnRejected` → 429 |
| Exception ở middleware/filter khác | `IExceptionHandler` toàn cục → 500 |

### Phía FE
- Client HTTP `src/api/http.ts` (sau này là custom mutator của orval) luôn đọc `ErrorFullModel`: `status` 2xx → trả `data` (`request`) hoặc cả model (`requestFull`, khi cần `messageText` thành công); ngược lại ném `ApiError(status, messageText, data)` (`fieldErrors` cho lỗi 400). Mất mạng → `error.network`; response không phải `ErrorFullModel` → `error.server_unreachable`.
- 401 → tự gọi `/auth/refresh` **một lần** (gộp các request đồng thời) rồi thử lại; vẫn 401 → xoá phiên, về trang đăng nhập kèm `?redirect=`.
- TanStack Vue Query: `onError` toàn cục hiện toast bằng `messageText` (đã dịch sẵn từ BE), bỏ qua 400 (hiện dưới field), 401 (đã chuyển trang) và request bị huỷ; query/mutation tự hiển thị lỗi tại chỗ (vd khung Insight) khai báo `meta: { silent: true }`. Chỉ thử lại lỗi mạng / 5xx (tối đa 2 lần).
- Form (VeeValidate): lỗi 400 → `setErrors(error.fieldErrors)` để hiện lỗi dưới từng field.

### Kiểm soát
- Checklist review: action mới phải có `try { … return Success(…) } catch (Exception ex) { return HandleException(ex); }`; service không `catch` nuốt lỗi; key thông báo đúng nhóm `<chức_năng>_<hành_động>` của handler (Q1).
- Item template `dotnet new dx-controller` sinh sẵn khung trên.
- Integration test cho từng mã 400/401/403/404/409/422/429/500 kiểm tra body đúng 3 trường.

---

## Q3. Connector có sẵn cấu hình + thư mục `mock-data/`

### Danh sách connector

| Key | Hệ thống | Dùng cho | Interface | Mặc định môi trường test |
|---|---|---|---|---|
| `Ai` | LLM tương thích OpenAI | Insight, Chat, dịch | `IAiClient` (bọc `IChatClient`) | Bật, **mock** |
| `GitHub` | GitHub OAuth | Link repo, commit | `IGitHubClient` | Tắt, mock |
| `GitLab` | GitLab OAuth/PAT (cả bản self-host) | Link project, issue/MR | `IGitLabClient` | Tắt, mock |
| `GoogleDrive` | Google Drive OAuth | Tài liệu dự án | `IGoogleDriveClient` | Tắt, mock |
| `Email` | SMTP | Mời đăng nhập, quên mật khẩu, link khảo sát CSS, thông báo duyệt đơn | `IEmailSender` | Bật, **mock** (ghi ra `mock-data/email/outbox`) |

Không còn connector HRM, RON, SSO (bỏ theo D4): phòng ban, nhân sự, tài khoản, nghỉ phép, OT, chấm công vào/ra được quản lý ngay trong phần mềm; dữ liệu giờ vào/ra từ máy chấm công đưa vào bằng **import file** (Excel/CSV), không cần kết nối.

### Cấu hình (`appsettings.json` — commit vào git, **không chứa bí mật**)
```json
{
  "MockData": {
    "RootPath": "../../mock-data",
    "SimulatedLatencyMs": 300,
    "FailureRate": 0.0
  },
  "Integrations": {
    "Ai": {
      "Enabled": true, "UseMock": true,
      "Provider": "openai-compatible", "BaseUrl": "", "ApiKey": "",
      "ChatModel": "", "InsightModel": "", "TranslateModel": "",
      "Temperature": 0.3, "MaxTokens": 2000, "TimeoutSeconds": 60, "InsightCacheMinutes": 60
    },
    "GitHub": {
      "Enabled": false, "UseMock": true,
      "ClientId": "", "ClientSecret": "",
      "RedirectUri": "https://dx.codeonlabs.vn/api/v1/integrations/github/callback",
      "Scopes": [ "repo", "read:user" ]
    },
    "GitLab": {
      "Enabled": false, "UseMock": true,
      "BaseUrl": "https://gitlab.com", "ClientId": "", "ClientSecret": "",
      "RedirectUri": "https://dx.codeonlabs.vn/api/v1/integrations/gitlab/callback",
      "Scopes": [ "read_api" ]
    },
    "GoogleDrive": {
      "Enabled": false, "UseMock": true,
      "ClientId": "", "ClientSecret": "",
      "RedirectUri": "https://dx.codeonlabs.vn/api/v1/integrations/google-drive/callback",
      "Scopes": [ "https://www.googleapis.com/auth/drive.readonly" ]
    },
    "Email": {
      "Enabled": true, "UseMock": true,
      "Host": "", "Port": 587, "UseStartTls": true, "Username": "", "Password": "",
      "FromAddress": "no-reply@codeonlabs.vn", "FromName": "DX Platform"
    }
  }
}
```

Quy tắc:
- Bí mật thật (`ApiToken`, `ApiKey`, `ClientSecret`, `Password`) chỉ đặt trong `appsettings.Secrets.json` (gitignore, nạp bằng `AddJsonFile(..., optional: true)` — giống kế hoạch CodeOnLicense) hoặc biến môi trường `Integrations__Ai__ApiKey`.
- Mỗi section có lớp `<Key>Options` + validator chạy **lúc khởi động** (`ValidateOnStart`): `Enabled = true` và `UseMock = false` mà thiếu URL/khoá → app dừng và ghi log rõ thiếu gì.
- Chuyển sang dữ liệu thật chỉ cần đổi `UseMock: false` và điền bí mật — không sửa code.
- Giai đoạn SaaS: cấu hình theo tenant lưu DB (bảng `tenant_integrations`, bí mật mã hoá bằng Data Protection), `appsettings` là giá trị mặc định của hệ thống.

### Đăng ký Live / Mock
```csharp
// Dx.Infrastructure/Integrations/IntegrationRegistration.cs
public static IServiceCollection AddIntegration<TService, TLive, TMock, TOptions>(
    this IServiceCollection services, IConfiguration config, string key)
    where TService : class where TLive : class, TService where TMock : class, TService
    where TOptions : IntegrationOptions
{
    var section = config.GetSection($"Integrations:{key}");
    services.AddOptions<TOptions>().Bind(section).ValidateOnStart();
    var useMock = section.GetValue("UseMock", true);
    if (useMock) services.AddScoped<TService, TMock>();
    else { services.AddHttpClient<TLive>(); services.AddScoped<TService>(sp => sp.GetRequiredService<TLive>()); }
    return services;
}

// Program.cs
services.AddSingleton<IMockDataStore, JsonMockDataStore>();   // đọc mock-data/, cache, giả lập độ trễ và lỗi
services.AddIntegration<IAiClient, OpenAiCompatibleClient, MockAiClient, AiOptions>(config, "Ai");
services.AddIntegration<IEmailSender, SmtpEmailSender, MockEmailSender, EmailOptions>(config, "Email");
// … GitHub, GitLab, GoogleDrive
```
- `JsonMockDataStore`: đọc file theo đường dẫn tương đối (`ai/insights/{screen_key}.json`, `github/repos.json`…), cache theo thời gian sửa file, cộng `SimulatedLatencyMs`, ném lỗi ngẫu nhiên theo `FailureRate` để test luồng lỗi.
- Thiếu file mock cho màn / tài nguyên được hỏi → trả danh sách rỗng (giống API thật không có dữ liệu), không ném lỗi.
- Màn **Quản trị → Tích hợp** hiển thị từng connector: Tắt / Giả lập / Thật, lần đồng bộ cuối, nút "Kiểm tra kết nối" (`POST /api/v1/integrations/{key}/test`). Khi đang dùng mock, giao diện hiện nhãn "Dữ liệu giả lập".
- **FE không tự giả lập dữ liệu:** FE chỉ gọi API `/api/v1` — BE thật, hoặc **`tools/mock-api`** khi BE chưa có endpoint đó. Mock API là một "BE tạm" cùng hợp đồng (ErrorFullModel, `Locales` của BE, `mock-data/seed`, `mock-data/ai`), chạy bằng `node tools/mock-api/server.mjs` (cổng 5080; Vite proxy `/api` qua biến `DX_API_TARGET`). Mọi endpoint mới viết ở mock phải ghi vào `tools/mock-api/README.md` để BE làm theo.

### Thư mục `mock-data/` (gốc repo, tách khỏi code)
```
mock-data/
├─ README.md                         # mô tả từng file, định dạng, tài khoản demo, cách thêm dữ liệu
├─ ai/
│  ├─ insights/<screen_key>.json     # theo screen_key trong shared/screens.json: project_list, project_detail, project_detail_metrics_timeline, timesheet_week, effort…
│  ├─ chat/scenarios.json            # lời chào, câu trả lời theo từ khoá, câu trả lời mặc định (vi/en)
│  └─ translate/vi-ja.json
├─ github/   repos.json · commits.json
├─ gitlab/   projects.json · issues.json · merge-requests.json
├─ google-drive/ files.json
├─ email/outbox/                     # MockEmailSender ghi mỗi email thành 1 file .json (thư mục này gitignore)
└─ seed/                             # dữ liệu demo nạp vào DB cho tenant demo (mock API đọc trực tiếp)
   ├─ tenants.json · users.json · roles.json · permissions.json · user_roles.json · role_permissions.json
   ├─ master_data.json · clients.json · departments.json · teams.json · employees.json
   ├─ projects.json · contract_staff.json · assignments.json · project_members.json · project_milestones.json …
   ├─ timesheets.json · effort_allocations.json · system_rules.json · system_settings.json
   ├─ leave_requests.json · ot_requests.json · leave_balances.json · attendance_records.json · holidays.json
   └─ pcv / css / quality_metrics / ai_insights / ai_prompt_templates / integrations
```

Định dạng mock của connector **giống hệt** response của dịch vụ thật (`ai/**` theo `src/modules/ai-chat/services/ai.mock.ts`, `ai.mock.data.ts` của DX; `github/`, `gitlab/`, `google-drive/` theo API công khai của từng dịch vụ, rút gọn trường).

Quy tắc dữ liệu:
- Toàn bộ là **dữ liệu hư cấu** (tên, email `@example.com`, số điện thoại giả) — không chép dữ liệu thật của bất kỳ tổ chức nào.
- Giờ trong `attendance_records` là `HH:mm` giờ Việt Nam; dòng công sinh từ đơn nghỉ / OT đã duyệt có `source` và `source_request_id` trỏ về đơn.
- Sinh lại toàn bộ bằng `node tools/mock-data-gen/generate.mjs` (tất định — cùng seed cho cùng kết quả); mật khẩu demo đặt bằng biến `DX_DEMO_PASSWORD` (mặc định `Demo@2026`, **phải đổi trước khi mở ra Internet**); hash theo chuẩn ASP.NET Identity V3 để BE nạp thẳng.
- Seeder BE: `Dx.Api --seed-demo` (hoặc cờ `Seed:DemoTenant = true`) nạp `seed/` vào tenant demo; chạy lại được (upsert theo id).
