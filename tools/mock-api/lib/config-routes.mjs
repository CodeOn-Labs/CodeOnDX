// Endpoint cấu hình theo tenant (D12): Danh mục, Quy tắc, Cài đặt chung, Ngày lễ, Biểu mẫu PCV, Biểu mẫu CSS.
// Dữ liệu sửa chỉ nằm trong bộ nhớ — khởi động lại mock API là về lại mock-data/seed.
// BE .NET phải giữ đúng hợp đồng này (cùng request → cùng cấu trúc `data`, cùng key lỗi).
import { randomUUID } from 'node:crypto';
import { ApiError, badRequest, listParam, notFound, paginate, readJson } from './http.mjs';

/** Loại danh mục quản trị được sửa ở màn admin_category_list (cùng thứ tự hiển thị trong bộ lọc). */
export const CATEGORY_TYPES = [
  'project_status', 'project_level', 'risk_level', 'hr_level', 'job_position', 'performance_level',
  'task_type', 'tech_request_type', 'employee_status', 'leave_type', 'link_type', 'currency', 'country', 'industry',
];

const TIME_ZONES = ['Asia/Ho_Chi_Minh', 'Asia/Tokyo', 'Asia/Singapore', 'UTC'];
const WORK_WEEKS = ['MON-FRI', 'MON-SAT', 'MON-SAT_HALF'];
const LOCALES = ['vi', 'en'];
const CODE_PATTERN = /^[A-Z0-9_]{1,30}$/;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const MAX_LOGO_LENGTH = 400_000; // ~300 KB ảnh sau khi mã hoá base64

const nowIso = () => new Date().toISOString();
const idFromPath = (url) => decodeURIComponent(url.pathname.split('/').pop() || '');
const str = (value) => (typeof value === 'string' ? value.trim() : '');

function addError(errors, field, error) {
  (errors[field] ??= []).push(error);
}
const required = (errors, field, value) => {
  if (!value) addError(errors, field, 'validation.required');
  return Boolean(value);
};
const maxLength = (errors, field, value, max) => {
  if (value && value.length > max) addError(errors, field, { key: 'validation.max_length', args: { max } });
};
function numberInRange(errors, field, value, min, max, { integer = false } = {}) {
  const n = typeof value === 'number' ? value : Number(value);
  if (value === null || value === undefined || value === '' || !Number.isFinite(n)) {
    addError(errors, field, 'validation.invalid_number');
    return null;
  }
  if (integer && !Number.isInteger(n)) addError(errors, field, 'validation.integer');
  if (min !== null && min !== undefined && n < min) addError(errors, field, { key: 'validation.min_value', args: { min } });
  if (max !== null && max !== undefined && n > max) addError(errors, field, { key: 'validation.max_value', args: { max } });
  return n;
}
function throwIfErrors(errors) {
  if (Object.keys(errors).length > 0) throw badRequest(errors);
}

/**
 * @param {{ store: ReturnType<import('./store.mjs').createStore>, currentUser: Function, requirePermission: Function }} deps
 * @returns {Array<[string, RegExp, Function]>}
 */
export function configRoutes({ store, currentUser, requirePermission }) {
  const { db } = store;
  const manager = (req) => {
    const ctx = currentUser(req);
    requirePermission(ctx, 'admin.settings');
    return ctx;
  };

  // ─── Danh mục (master data) ───────────────────────────────────────────────

  function categoryDto(m) {
    return {
      id: m.id, type: m.type, code: m.code, name: m.name, nameEn: m.name_en ?? null, description: m.description ?? null,
      color: m.metadata?.color ?? null, sortSeq: m.sort_seq, metadata: m.metadata ?? {}, isActive: m.is_active,
      isSystem: Boolean(m.metadata?.system), updatedAt: m.updated_at ?? null,
    };
  }

  /** Danh mục còn được bảng nghiệp vụ tham chiếu (theo id) thì không xoá được — chỉ tạm khoá. */
  function categoryInUse(id) {
    return Object.entries(db).some(([table, rows]) => table !== 'master_data' && JSON.stringify(rows).includes(id));
  }

  function validateCategory(body, current) {
    const errors = {};
    const type = str(body.type);
    const code = str(body.code).toUpperCase();
    const name = str(body.name);
    const nameEn = str(body.nameEn);
    const description = str(body.description);
    const color = str(body.color);
    const metadata = body.metadata && typeof body.metadata === 'object' && !Array.isArray(body.metadata) ? { ...body.metadata } : {};

    if (required(errors, 'type', type) && !CATEGORY_TYPES.includes(type)) addError(errors, 'type', 'validation.invalid_value');
    if (current && type && type !== current.type) addError(errors, 'type', 'category_save.err_type_locked');
    if (required(errors, 'code', code)) {
      if (!CODE_PATTERN.test(code)) addError(errors, 'code', 'category_save.err_code_format');
      else if (db.master_data.some((m) => m.type === type && m.code === code && m.id !== current?.id)) addError(errors, 'code', 'category_save.err_duplicate_code');
    }
    required(errors, 'name', name);
    maxLength(errors, 'name', name, 100);
    maxLength(errors, 'nameEn', nameEn, 100);
    maxLength(errors, 'description', description, 500);
    if (color && !/^#[0-9a-fA-F]{6}$/.test(color) && !/^[a-z]{2,10}$/.test(color)) addError(errors, 'color', 'validation.invalid_value');
    const sortSeq = numberInRange(errors, 'sortSeq', body.sortSeq, 1, 999, { integer: true });
    // Mức hiệu suất phải có hệ số — dùng trong công thức Actual1 của lưới Công số (D12)
    if (type === 'performance_level') numberInRange(errors, 'coefficient', metadata.coefficient, 0, 2);
    for (const key of Object.keys(metadata)) {
      if (!/^[a-z][a-z0-9_]{0,39}$/.test(key)) addError(errors, 'metadata', 'category_save.err_metadata_key');
    }
    throwIfErrors(errors);

    if (color) metadata.color = color;
    else delete metadata.color;
    if (current?.metadata?.system) metadata.system = true; // danh mục hệ thống tự sinh (vd task OT/LEAVE) giữ cờ
    return {
      type, code, name, name_en: nameEn || null, description: description || null, sort_seq: sortSeq, metadata,
      is_active: body.isActive !== false,
    };
  }

  function findCategory(url) {
    const row = db.master_data.find((m) => m.id === idFromPath(url));
    if (!row) throw notFound();
    return row;
  }

  // ─── Quy tắc ──────────────────────────────────────────────────────────────

  function findRule(url) {
    const row = db.system_rules.find((r) => r.key === idFromPath(url));
    if (!row) throw notFound();
    return row;
  }

  // ─── Cài đặt chung ────────────────────────────────────────────────────────

  const settingsObject = () => Object.fromEntries(db.system_settings.map((s) => [s.key, s.value]));

  /** Mỗi khoá có luật riêng; khoá lạ → 400. Giá trị lưu dạng chuỗi như bảng system_settings. */
  const SETTING_VALIDATORS = {
    company_name: (v, e) => { required(e, 'company_name', v); maxLength(e, 'company_name', v, 150); },
    company_logo: (v, e) => {
      if (v && (!v.startsWith('data:image/') || v.length > MAX_LOGO_LENGTH)) addError(e, 'company_logo', 'admin_setting_update.err_logo_invalid');
    },
    default_locale: (v, e) => { if (!LOCALES.includes(v)) addError(e, 'default_locale', 'validation.invalid_value'); },
    time_zone: (v, e) => { if (!TIME_ZONES.includes(v)) addError(e, 'time_zone', 'validation.invalid_value'); },
    currency: (v, e) => {
      if (!db.master_data.some((m) => m.type === 'currency' && m.is_active && m.code === v)) addError(e, 'currency', 'validation.invalid_value');
    },
    work_week: (v, e) => { if (!WORK_WEEKS.includes(v)) addError(e, 'work_week', 'validation.invalid_value'); },
    fiscal_year_start_month: (v, e) => numberInRange(e, 'fiscal_year_start_month', v, 1, 12, { integer: true }),
    work_start_time: (v, e) => { if (!TIME_PATTERN.test(v)) addError(e, 'work_start_time', 'validation.invalid_time'); },
    work_end_time: (v, e) => { if (!TIME_PATTERN.test(v)) addError(e, 'work_end_time', 'validation.invalid_time'); },
    lunch_break_minutes: (v, e) => numberInRange(e, 'lunch_break_minutes', v, 0, 240, { integer: true }),
    notification_email: (v, e) => { if (required(e, 'notification_email', v) && !EMAIL_PATTERN.test(v)) addError(e, 'notification_email', 'validation.invalid_email'); },
    monthly_report_ja_enabled: (v, e) => { if (!['true', 'false'].includes(v)) addError(e, 'monthly_report_ja_enabled', 'validation.invalid_value'); },
    allow_app_check_in: (v, e) => { if (!['true', 'false'].includes(v)) addError(e, 'allow_app_check_in', 'validation.invalid_value'); },
    password_min_length: (v, e) => numberInRange(e, 'password_min_length', v, 6, 64, { integer: true }),
    invitation_expiry_hours: (v, e) => numberInRange(e, 'invitation_expiry_hours', v, 1, 720, { integer: true }),
  };

  // ─── Biểu mẫu PCV / CSS ───────────────────────────────────────────────────

  const pcvDto = (p) => ({ id: p.id, category: p.group_name, label: p.content, sortSeq: p.sort_seq });
  const cssQuestionDto = (q) => ({ id: q.id, label: q.content, required: q.is_required, sortSeq: q.sort_seq });
  const cssTemplate = () => db.css_template[0];
  const cssContentDto = (c) => ({ title: c.title, intro: c.intro ?? '', scaleNote: c.scale_note ?? '', commentLabel: c.comment_label ?? '' });
  const nextSortSeq = (rows) => Math.max(0, ...rows.map((r) => r.sort_seq)) + 1;

  function validatePcv(body) {
    const errors = {};
    const category = str(body.category);
    const label = str(body.label);
    required(errors, 'category', category);
    maxLength(errors, 'category', category, 100);
    required(errors, 'label', label);
    maxLength(errors, 'label', label, 500);
    throwIfErrors(errors);
    return { group_name: category, content: label };
  }

  function validateCssQuestion(body) {
    const errors = {};
    const label = str(body.label);
    required(errors, 'label', label);
    maxLength(errors, 'label', label, 500);
    throwIfErrors(errors);
    return { content: label, is_required: body.required !== false };
  }

  const holidayDto = (h) => ({ id: h.id, date: h.holiday_date, name: h.name, isPaid: h.is_paid });

  return [
    // Danh mục
    ['GET', /^\/api\/v1\/categories$/, async ({ req, url }) => {
      manager(req);
      const search = (url.searchParams.get('search') || '').trim().toLowerCase();
      const types = listParam(url, 'types');
      const rows = db.master_data
        .filter((m) => CATEGORY_TYPES.includes(m.type) && (types.length === 0 || types.includes(m.type)))
        .filter((m) => !search || [m.name, m.name_en, m.code].some((v) => v?.toLowerCase().includes(search)))
        .sort((a, b) => CATEGORY_TYPES.indexOf(a.type) - CATEGORY_TYPES.indexOf(b.type) || a.sort_seq - b.sort_seq)
        .map(categoryDto);
      return { data: paginate(rows, url, 20) };
    }],
    ['POST', /^\/api\/v1\/categories$/, async ({ req }) => {
      manager(req);
      const values = validateCategory(await readJson(req), null);
      const row = { id: randomUUID(), tenant_id: db.tenants[0].id, ...values, updated_at: nowIso() };
      db.master_data.push(row);
      store.masterById.set(row.id, row);
      return { status: 201, key: 'category_create.msg_success', data: categoryDto(row) };
    }],
    ['PUT', /^\/api\/v1\/categories\/[^/]+$/, async ({ req, url }) => {
      manager(req);
      const row = findCategory(url);
      Object.assign(row, validateCategory(await readJson(req), row), { updated_at: nowIso() });
      return { key: 'category_update.msg_success', data: categoryDto(row) };
    }],
    ['DELETE', /^\/api\/v1\/categories\/[^/]+$/, async ({ req, url }) => {
      manager(req);
      const row = findCategory(url);
      if (row.metadata?.system) throw new ApiError(409, 'category_delete.err_system');
      if (categoryInUse(row.id)) throw new ApiError(409, 'category_delete.err_in_use', null, { name: row.name });
      db.master_data.splice(db.master_data.indexOf(row), 1);
      store.masterById.delete(row.id);
      return { key: 'category_delete.msg_success', args: { name: row.name } };
    }],

    // Quy tắc (GET /system-rules nằm ở server.mjs vì màn khác cũng đọc)
    ['PUT', /^\/api\/v1\/system-rules\/[A-Z0-9_]+$/, async ({ req, url, culture }) => {
      manager(req);
      const rule = findRule(url);
      const body = await readJson(req);
      const errors = {};
      let value = body.value;
      if (rule.value_type === 'boolean') {
        if (typeof value !== 'boolean') addError(errors, 'value', 'validation.invalid_value');
      } else {
        value = numberInRange(errors, 'value', value, rule.min, rule.max);
      }
      if (![0, 1].includes(body.status)) addError(errors, 'status', 'validation.invalid_value');
      throwIfErrors(errors);
      Object.assign(rule, { value, status: body.status, updated_at: nowIso() });
      return { key: 'system_rule_update.msg_success', data: store.ruleDto(rule, culture) };
    }],

    // Cài đặt chung
    ['GET', /^\/api\/v1\/settings$/, async ({ req }) => {
      currentUser(req);
      return { data: settingsObject() };
    }],
    ['PUT', /^\/api\/v1\/settings$/, async ({ req }) => {
      manager(req);
      const body = await readJson(req);
      const errors = {};
      const changes = {};
      for (const [key, raw] of Object.entries(body)) {
        const validate = SETTING_VALIDATORS[key];
        if (!validate) {
          addError(errors, key, 'validation.invalid_value');
          continue;
        }
        const value = raw === null || raw === undefined ? '' : String(raw).trim();
        validate(value, errors);
        changes[key] = value;
      }
      const merged = { ...settingsObject(), ...changes };
      if (TIME_PATTERN.test(merged.work_start_time) && TIME_PATTERN.test(merged.work_end_time) && merged.work_end_time <= merged.work_start_time) {
        addError(errors, 'work_end_time', 'admin_setting_update.err_end_before_start');
      }
      throwIfErrors(errors);
      for (const [key, value] of Object.entries(changes)) {
        const row = db.system_settings.find((s) => s.key === key);
        if (row) row.value = value;
        else db.system_settings.push({ tenant_id: db.tenants[0].id, key, value });
      }
      return { key: 'admin_setting_update.msg_success', data: settingsObject() };
    }],

    // Ngày lễ (không tính vào số ngày nghỉ phép)
    ['GET', /^\/api\/v1\/holidays$/, async ({ req, url }) => {
      currentUser(req);
      const year = url.searchParams.get('year');
      const rows = db.holidays
        .filter((h) => !year || h.holiday_date.startsWith(`${year}-`))
        .sort((a, b) => a.holiday_date.localeCompare(b.holiday_date))
        .map(holidayDto);
      return { data: rows };
    }],
    ['POST', /^\/api\/v1\/holidays$/, async ({ req }) => {
      manager(req);
      const body = await readJson(req);
      const errors = {};
      const date = str(body.date);
      const name = str(body.name);
      if (required(errors, 'date', date) && (!DATE_PATTERN.test(date) || Number.isNaN(Date.parse(date)))) addError(errors, 'date', 'validation.invalid_date');
      else if (db.holidays.some((h) => h.holiday_date === date)) addError(errors, 'date', 'holiday_create.err_duplicate_date');
      required(errors, 'name', name);
      maxLength(errors, 'name', name, 100);
      throwIfErrors(errors);
      const row = { id: randomUUID(), tenant_id: db.tenants[0].id, holiday_date: date, name, is_paid: body.isPaid !== false };
      db.holidays.push(row);
      return { status: 201, key: 'holiday_create.msg_success', data: holidayDto(row) };
    }],
    ['DELETE', /^\/api\/v1\/holidays\/[^/]+$/, async ({ req, url }) => {
      manager(req);
      const index = db.holidays.findIndex((h) => h.id === idFromPath(url));
      if (index < 0) throw notFound();
      db.holidays.splice(index, 1);
      return { key: 'holiday_delete.msg_success' };
    }],

    // Biểu mẫu tiêu chí PCV (dự án tạo sau mới nhận bản sao — dự án đã có giữ nguyên)
    ['GET', /^\/api\/v1\/pcv-template-items$/, async ({ req }) => {
      manager(req);
      return { data: db.pcv_template_items.filter((p) => !p.is_deleted).sort((a, b) => a.sort_seq - b.sort_seq).map(pcvDto) };
    }],
    ['POST', /^\/api\/v1\/pcv-template-items$/, async ({ req }) => {
      manager(req);
      const values = validatePcv(await readJson(req));
      const row = { id: randomUUID(), tenant_id: db.tenants[0].id, ...values, sort_seq: nextSortSeq(db.pcv_template_items), is_deleted: false };
      db.pcv_template_items.push(row);
      return { status: 201, key: 'pcv_template_create.msg_success', data: pcvDto(row) };
    }],
    ['PUT', /^\/api\/v1\/pcv-template-items\/[^/]+$/, async ({ req, url }) => {
      manager(req);
      const row = db.pcv_template_items.find((p) => p.id === idFromPath(url) && !p.is_deleted);
      if (!row) throw notFound();
      Object.assign(row, validatePcv(await readJson(req)));
      return { key: 'pcv_template_update.msg_success', data: pcvDto(row) };
    }],
    ['DELETE', /^\/api\/v1\/pcv-template-items\/[^/]+$/, async ({ req, url }) => {
      manager(req);
      const row = db.pcv_template_items.find((p) => p.id === idFromPath(url) && !p.is_deleted);
      if (!row) throw notFound();
      row.is_deleted = true;
      return { key: 'pcv_template_delete.msg_success' };
    }],

    // Biểu mẫu khảo sát CSS
    ['GET', /^\/api\/v1\/css-template$/, async ({ req }) => {
      manager(req);
      return { data: cssContentDto(cssTemplate()) };
    }],
    ['PUT', /^\/api\/v1\/css-template$/, async ({ req }) => {
      manager(req);
      const body = await readJson(req);
      const errors = {};
      const title = str(body.title);
      required(errors, 'title', title);
      maxLength(errors, 'title', title, 200);
      for (const field of ['intro', 'scaleNote', 'commentLabel']) maxLength(errors, field, str(body[field]), 2000);
      throwIfErrors(errors);
      Object.assign(cssTemplate(), { title, intro: str(body.intro), scale_note: str(body.scaleNote), comment_label: str(body.commentLabel) });
      return { key: 'css_template_update.msg_success', data: cssContentDto(cssTemplate()) };
    }],
    ['GET', /^\/api\/v1\/css-template\/questions$/, async ({ req }) => {
      manager(req);
      return { data: [...db.css_template_questions].sort((a, b) => a.sort_seq - b.sort_seq).map(cssQuestionDto) };
    }],
    ['POST', /^\/api\/v1\/css-template\/questions$/, async ({ req }) => {
      manager(req);
      const values = validateCssQuestion(await readJson(req));
      const row = { id: randomUUID(), tenant_id: db.tenants[0].id, template_id: cssTemplate().id, ...values, sort_seq: nextSortSeq(db.css_template_questions) };
      db.css_template_questions.push(row);
      return { status: 201, key: 'css_question_create.msg_success', data: cssQuestionDto(row) };
    }],
    ['PUT', /^\/api\/v1\/css-template\/questions\/[^/]+$/, async ({ req, url }) => {
      manager(req);
      const row = db.css_template_questions.find((q) => q.id === idFromPath(url));
      if (!row) throw notFound();
      Object.assign(row, validateCssQuestion(await readJson(req)));
      return { key: 'css_question_update.msg_success', data: cssQuestionDto(row) };
    }],
    ['DELETE', /^\/api\/v1\/css-template\/questions\/[^/]+$/, async ({ req, url }) => {
      manager(req);
      const index = db.css_template_questions.findIndex((q) => q.id === idFromPath(url));
      if (index < 0) throw notFound();
      db.css_template_questions.splice(index, 1);
      return { key: 'css_question_delete.msg_success' };
    }],
  ];
}
