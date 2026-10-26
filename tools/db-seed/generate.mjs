#!/usr/bin/env node
/**
 * Sinh script nạp tenant demo vào DB thật từ mock-data/seed (cùng id với mock API).
 *
 *   node tools/db-seed/generate.mjs        # ghi database/seed/demo_tenant.sql
 *
 * - Chạy lại sau mỗi lần `node tools/mock-data-gen/generate.mjs`.
 * - Script dùng ON CONFLICT DO NOTHING nên chạy lại được; BE nạp bằng `Dx.Api --seed-demo`
 *   (chạy bằng role owner, không bị RLS chặn).
 * - Ánh xạ tên cột seed → DB theo bảng "Đổi tên so với mock-data" trong database/README.md.
 */
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const REPO = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const SEED = join(REPO, 'mock-data/seed');
const OUT = join(REPO, 'database/seed/demo_tenant.sql');

const load = (name) => JSON.parse(readFileSync(join(SEED, `${name}.json`), 'utf8'));
const db = new Proxy({}, { get: (cache, name) => (cache[name] ??= load(name)) });

/** UUID tất định (dạng v5) cho bản ghi seed không có id sẵn (câu hỏi CSS của dự án, cột báo cáo tháng…). */
function uid(name) {
  const h = createHash('sha1').update(`dx-db-seed:${name}`).digest();
  h[6] = (h[6] & 0x0f) | 0x50;
  h[8] = (h[8] & 0x3f) | 0x80;
  const x = h.subarray(0, 16).toString('hex');
  return `${x.slice(0, 8)}-${x.slice(8, 12)}-${x.slice(12, 16)}-${x.slice(16, 20)}-${x.slice(20)}`;
}

// ─── Ghi giá trị SQL ────────────────────────────────────────────────────────

const str = (v) => `'${String(v).replace(/'/g, "''")}'`;
const lit = (v) => {
  if (v === null || v === undefined) return 'NULL';
  if (typeof v === 'boolean') return v ? 'true' : 'false';
  if (typeof v === 'number') return Number.isFinite(v) ? String(v) : 'NULL';
  return str(v);
};
const json = (v) => `${str(JSON.stringify(v ?? {}))}::jsonb`;
const textArray = (arr) => (arr && arr.length ? `ARRAY[${arr.map(str).join(', ')}]::text[]` : `'{}'::text[]`);
const uuidArray = (arr) => (arr && arr.length ? `ARRAY[${arr.map(str).join(', ')}]::uuid[]` : `'{}'::uuid[]`);
/** Đánh dấu giá trị đã là biểu thức SQL (không quote lại). */
const raw = (sql) => ({ raw: sql });
const val = (v) => (v && typeof v === 'object' && 'raw' in v ? v.raw : lit(v));

const out = [];
function insert(table, rows, columns) {
  if (rows.length === 0) return;
  out.push(`-- ${table}: ${rows.length}`);
  out.push(`INSERT INTO dx.${table} (${columns.join(', ')}) VALUES`);
  out.push(rows.map((r) => `    (${columns.map((c) => val(r[c])).join(', ')})`).join(',\n'));
  out.push('ON CONFLICT DO NOTHING;');
  out.push('');
}
const pickCols = (row, map) => Object.fromEntries(Object.entries(map).map(([col, f]) => [col, typeof f === 'function' ? f(row) : row[f]]));

// ─── Tra cứu ────────────────────────────────────────────────────────────────

const master = (type, code) => {
  if (!code) return null;
  const m = db.master_data.find((x) => x.type === type && x.code === code);
  if (!m) throw new Error(`Không thấy master_data ${type}/${code}`);
  return m.id;
};
const position = (code) => master('job_position', code);
/** effort_allocations.role của bản cũ ('Dev BE'…) → mã job_position */
const EFFORT_ROLE = { PO: 'PO', 'Dev BE': 'DEV_BE', 'Dev FE': 'DEV_FE', 'Dev Mobile': 'DEV_MOBILE', 'Dev AI': 'DEV_AI', Tester: 'TESTER', Infra: 'INFRA', Comtor: 'COMTOR' };
const tenantOfProject = new Map(db.projects.map((p) => [p.id, p.tenant_id]));
const tenantOfUser = new Map(db.users.map((u) => [u.id, u.tenant_id]));
const tenantOfRole = new Map(db.roles.map((r) => [r.id, r.tenant_id]));

const audit = { created_at: 'created_at', created_by: 'created_by', updated_at: (r) => r.updated_at ?? r.created_at, updated_by: 'updated_by' };
const auditCols = ['created_at', 'created_by', 'updated_at', 'updated_by'];
const withDefaults = (rows) => rows.map((r) => ({ ...r, created_at: r.created_at ?? raw('now()'), updated_at: r.updated_at ?? raw('now()') }));

// ─── Bảng ───────────────────────────────────────────────────────────────────

out.push('-- =============================================================================');
out.push('-- Tenant demo — SINH TỰ ĐỘNG bởi tools/db-seed/generate.mjs từ mock-data/seed.');
out.push('-- Không sửa tay. Chạy bằng role owner (bỏ qua RLS); chạy lại được.');
out.push('-- =============================================================================');
out.push('');

insert('tenants', db.tenants.map((t) => pickCols(t, { id: 'id', code: 'code', name: 'name', default_locale: 'default_locale', time_zone: 'time_zone', is_active: 'is_active', created_at: 'created_at', updated_at: 'created_at' })),
  ['id', 'code', 'name', 'default_locale', 'time_zone', 'is_active', 'created_at', 'updated_at']);

insert('users', db.users.map((u) => pickCols(u, {
  id: 'id', user_name: 'email', normalized_user_name: 'normalized_email', email: 'email', normalized_email: 'normalized_email',
  email_confirmed: 'email_confirmed', password_hash: 'password_hash', security_stamp: (r) => uid(`stamp:${r.id}`), concurrency_stamp: (r) => uid(`cs:${r.id}`),
  full_name: 'full_name', preferred_locale: 'preferred_locale', avatar_url: 'avatar_url', must_change_password: 'must_change_password',
  access_failed_count: 'access_failed_count', lockout_end: 'lockout_end', last_login_at: 'last_login_at', is_active: 'is_active', created_at: 'created_at', updated_at: 'created_at',
})), ['id', 'user_name', 'normalized_user_name', 'email', 'normalized_email', 'email_confirmed', 'password_hash', 'security_stamp', 'concurrency_stamp',
  'full_name', 'preferred_locale', 'avatar_url', 'must_change_password', 'access_failed_count', 'lockout_end', 'last_login_at', 'is_active', 'created_at', 'updated_at']);

insert('tenant_users', db.users.map((u) => ({ tenant_id: u.tenant_id, user_id: u.id, is_default: true, is_active: u.is_active })), ['tenant_id', 'user_id', 'is_default', 'is_active']);

insert('roles', db.roles.map((r) => pickCols(r, { id: 'id', tenant_id: 'tenant_id', code: 'code', name: 'name', name_en: 'name_en', is_system: 'is_system', created_at: 'created_at', updated_at: 'created_at' })),
  ['id', 'tenant_id', 'code', 'name', 'name_en', 'is_system', 'created_at', 'updated_at']);
insert('role_permissions', db.role_permissions.map((rp) => ({ tenant_id: tenantOfRole.get(rp.role_id), role_id: rp.role_id, permission_id: rp.permission_id })), ['tenant_id', 'role_id', 'permission_id']);
insert('user_roles', db.user_roles.map((ur) => ({ tenant_id: tenantOfUser.get(ur.user_id), user_id: ur.user_id, role_id: ur.role_id })), ['tenant_id', 'user_id', 'role_id']);

insert('master_data', db.master_data.map((m) => pickCols(m, {
  id: 'id', tenant_id: 'tenant_id', type: 'type', code: 'code', name: 'name', name_en: 'name_en', description: 'description',
  metadata: (r) => raw(json(r.metadata)), sort_order: 'sort_seq', is_system: (r) => r.metadata?.system === true, is_active: 'is_active', updated_at: 'updated_at',
})), ['id', 'tenant_id', 'type', 'code', 'name', 'name_en', 'description', 'metadata', 'sort_order', 'is_system', 'is_active', 'updated_at']);

insert('tenant_settings', db.system_settings.map((s) => ({ tenant_id: s.tenant_id, key: s.key, value: String(s.value ?? '') })), ['tenant_id', 'key', 'value']);
insert('tenant_rules', db.system_rules.map((r) => ({ tenant_id: r.tenant_id, rule_key: r.key, value: raw(json(r.value)), is_enabled: r.status === 1, updated_at: r.updated_at })),
  ['tenant_id', 'rule_key', 'value', 'is_enabled', 'updated_at']);
insert('holidays', db.holidays.map((h) => pickCols(h, { id: 'id', tenant_id: 'tenant_id', holiday_date: 'holiday_date', name: 'name', is_paid: 'is_paid' })), ['id', 'tenant_id', 'holiday_date', 'name', 'is_paid']);
insert('tenant_integrations', db.integrations.map((i) => ({ tenant_id: i.tenant_id, integration_key: i.key, is_enabled: i.enabled, use_mock: i.use_mock, last_sync_at: i.last_sync_at })),
  ['tenant_id', 'integration_key', 'is_enabled', 'use_mock', 'last_sync_at']);

insert('pcv_template_items', db.pcv_template_items.map((p) => pickCols(p, { id: 'id', tenant_id: 'tenant_id', category: 'group_name', content: 'content', sort_order: 'sort_seq', is_deleted: 'is_deleted' })),
  ['id', 'tenant_id', 'category', 'content', 'sort_order', 'is_deleted']);
insert('css_templates', db.css_template.map((t) => pickCols(t, { id: 'id', tenant_id: 'tenant_id', title: 'title', intro: 'intro', scale_note: 'scale_note', comment_label: 'comment_label' })),
  ['id', 'tenant_id', 'title', 'intro', 'scale_note', 'comment_label']);
insert('css_template_questions', db.css_template_questions.map((q) => pickCols(q, { id: 'id', tenant_id: 'tenant_id', template_id: 'template_id', content: 'content', is_required: 'is_required', sort_order: 'sort_seq' })),
  ['id', 'tenant_id', 'template_id', 'content', 'is_required', 'sort_order']);
insert('ai_prompt_templates', db.ai_prompt_templates.map((t) => pickCols(t, { id: 'id', tenant_id: 'tenant_id', scope: 'scope', name: 'name', template: 'template', is_active: (r) => r.status === 'active' })),
  ['id', 'tenant_id', 'scope', 'name', 'template', 'is_active']);
insert('ai_insight_configs', db.ai_insights.map((a, i) => pickCols(a, {
  id: 'id', tenant_id: 'tenant_id', screen_key: 'screen_key', name: 'name', data_sources: (r) => raw(textArray(r.data_sources)),
  allowed_role_ids: (r) => raw(uuidArray(r.allowed_roles)), data_prompt: 'data_prompt', sort_order: () => i + 1, is_active: (r) => r.status === 'active',
})), ['id', 'tenant_id', 'screen_key', 'name', 'data_sources', 'allowed_role_ids', 'data_prompt', 'sort_order', 'is_active']);

// Phòng ban / team trước, gán trưởng phòng / trưởng nhóm sau khi có nhân sự (khoá vòng)
insert('departments', db.departments.map((d) => pickCols(d, { id: 'id', tenant_id: 'tenant_id', code: 'code', name: 'name', description: 'description', sort_order: 'sort_seq', is_active: 'is_active', created_at: 'created_at', updated_at: 'created_at' })),
  ['id', 'tenant_id', 'code', 'name', 'description', 'sort_order', 'is_active', 'created_at', 'updated_at']);
insert('teams', db.teams.map((t) => pickCols(t, { id: 'id', tenant_id: 'tenant_id', department_id: 'department_id', code: 'code', name: 'name', sort_order: 'sort_seq', is_active: 'is_active', created_at: 'created_at', updated_at: 'created_at' })),
  ['id', 'tenant_id', 'department_id', 'code', 'name', 'sort_order', 'is_active', 'created_at', 'updated_at']);
insert('employees', db.employees.map((e) => pickCols(e, {
  id: 'id', tenant_id: 'tenant_id', user_id: 'user_id', code: 'code', full_name: 'full_name', email: 'email', phone: 'phone',
  position_id: (r) => position(r.position_code), level_id: 'level_id', department_id: 'department_id', team_id: 'team_id',
  status_id: (r) => master('employee_status', r.status), hire_date: 'joined_date', termination_date: 'left_date', avatar_url: 'avatar_url',
  skills: (r) => raw(textArray(r.skills)), created_at: 'created_at', updated_at: 'created_at',
})), ['id', 'tenant_id', 'user_id', 'code', 'full_name', 'email', 'phone', 'position_id', 'level_id', 'department_id', 'team_id', 'status_id',
  'hire_date', 'termination_date', 'avatar_url', 'skills', 'created_at', 'updated_at']);
for (const d of db.departments.filter((x) => x.manager_employee_id)) out.push(`UPDATE dx.departments SET manager_employee_id = ${lit(d.manager_employee_id)} WHERE id = ${lit(d.id)} AND manager_employee_id IS NULL;`);
for (const t of db.teams.filter((x) => x.leader_employee_id)) out.push(`UPDATE dx.teams SET leader_employee_id = ${lit(t.leader_employee_id)} WHERE id = ${lit(t.id)} AND leader_employee_id IS NULL;`);
out.push('');

insert('clients', db.clients.map((c) => pickCols(c, {
  id: 'id', tenant_id: 'tenant_id', code: 'code', name: 'name', short_name: 'short_name', country_id: 'country_id', industry_id: 'industry_id',
  contact_name: 'contact_name', contact_email: 'contact_email', default_currency_code: 'default_currency', is_active: 'is_active', is_deleted: 'is_deleted', created_at: 'created_at', updated_at: 'created_at',
})), ['id', 'tenant_id', 'code', 'name', 'short_name', 'country_id', 'industry_id', 'contact_name', 'contact_email', 'default_currency_code', 'is_active', 'is_deleted', 'created_at', 'updated_at']);

insert('projects', db.projects.map((p) => pickCols(p, {
  id: 'id', tenant_id: 'tenant_id', code: 'code', name: 'name', client_id: 'client_id', grade_id: 'grade_id', status_id: 'status_id', risk_level_id: 'risk_level_id',
  description: 'description', start_date: 'start_date', end_date: 'end_date', budget: 'budget', currency_code: 'currency_code', progress_percent: 'progress_pct',
  is_deleted: 'is_deleted', created_at: 'created_at', updated_at: 'updated_at',
})), ['id', 'tenant_id', 'code', 'name', 'client_id', 'grade_id', 'status_id', 'risk_level_id', 'description', 'start_date', 'end_date', 'budget', 'currency_code',
  'progress_percent', 'is_deleted', 'created_at', 'updated_at']);

insert('project_contract_staff', db.contract_staff.map((c) => pickCols(c, {
  id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', position_id: (r) => position(r.role_code), level_id: 'level_id', quantity: 'quantity',
  fte_per_person: 'fte_per_person', start_date: 'start_date', end_date: 'end_date', monthly_rate: 'price_per_month', is_deleted: 'is_deleted',
})), ['id', 'tenant_id', 'project_id', 'position_id', 'level_id', 'quantity', 'fte_per_person', 'start_date', 'end_date', 'monthly_rate', 'is_deleted']);
insert('project_assignments', db.assignments.map((a) => pickCols(a, {
  id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', employee_id: 'employee_id', contract_staff_id: 'contract_staff_id', slot_index: 'slot_index',
  position_id: (r) => position(r.role_in_project), level_id: 'level_id', fte_allocated: 'fte_allocated', start_date: 'start_date', end_date: 'end_date', is_deleted: 'is_deleted',
})), ['id', 'tenant_id', 'project_id', 'employee_id', 'contract_staff_id', 'slot_index', 'position_id', 'level_id', 'fte_allocated', 'start_date', 'end_date', 'is_deleted']);
insert('project_assignment_periods', db.project_assignment_schedules.map((s) => pickCols(s, { id: 'id', tenant_id: 'tenant_id', assignment_id: 'assignment_id', start_date: 'start_date', end_date: 'end_date', fte_allocated: 'fte_allocated' })),
  ['id', 'tenant_id', 'assignment_id', 'start_date', 'end_date', 'fte_allocated']);
insert('project_members', db.project_members.map((m) => ({ tenant_id: tenantOfProject.get(m.project_id), project_id: m.project_id, employee_id: m.employee_id, position_id: position(m.role), is_manager: m.role === 'PM' })),
  ['tenant_id', 'project_id', 'employee_id', 'position_id', 'is_manager']);
insert('project_links', db.project_links.map((l, i) => pickCols(l, { id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', link_type_id: 'link_type_id', name: 'name', url: 'url', sort_order: () => i % 2 })),
  ['id', 'tenant_id', 'project_id', 'link_type_id', 'name', 'url', 'sort_order']);
insert('project_milestones', db.project_milestones.map((m) => pickCols(m, {
  id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', name: 'name', sort_order: 'sort_seq', planned_date: 'planned_date', actual_date: 'actual_date', justification: 'justification', is_deleted: 'is_deleted',
})), ['id', 'tenant_id', 'project_id', 'name', 'sort_order', 'planned_date', 'actual_date', 'justification', 'is_deleted']);
insert('project_cost_centers', db.effort_project_centers.map((c) => pickCols(c, { project_id: 'project_id', tenant_id: 'tenant_id', department_id: 'department_id' })), ['project_id', 'tenant_id', 'department_id']);

insert('project_document_groups', withDefaults(db.project_document_groups.map((g) => pickCols(g, { id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', name: 'name', description: 'description', sort_order: 'sort_seq', ...audit, is_deleted: 'is_deleted' }))),
  ['id', 'tenant_id', 'project_id', 'name', 'description', 'sort_order', ...auditCols, 'is_deleted']);
insert('project_documents', withDefaults(db.project_documents.map((d) => pickCols(d, { id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', group_id: 'group_id', name: 'name', url: 'url', ...audit, is_deleted: 'is_deleted' }))),
  ['id', 'tenant_id', 'project_id', 'group_id', 'name', 'url', ...auditCols, 'is_deleted']);
insert('project_risks', withDefaults(db.project_risks.map((r) => pickCols(r, {
  id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', code: 'code', title: 'name', description: 'description', category: 'category', probability: 'probability', impact: 'impact',
  strategy: 'strategy', response_plan: 'response_plan', owner_employee_id: 'owner_id', due_date: 'due_date', status: 'status', identified_date: 'identified_at', ...audit, is_deleted: 'is_deleted',
}))), ['id', 'tenant_id', 'project_id', 'code', 'title', 'description', 'category', 'probability', 'impact', 'strategy', 'response_plan', 'owner_employee_id', 'due_date', 'status', 'identified_date', ...auditCols, 'is_deleted']);
insert('project_change_requests', withDefaults(db.project_change_requests.map((c) => pickCols(c, {
  id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', code: 'code', title: 'file_name', url: 'url', source: 'source', impact_level: 'impact_level',
  impact_areas: (r) => raw(textArray(r.impact_areas)), impact_note: 'impact_note', ...audit, is_deleted: 'is_deleted',
}))), ['id', 'tenant_id', 'project_id', 'code', 'title', 'url', 'source', 'impact_level', 'impact_areas', 'impact_note', ...auditCols, 'is_deleted']);
insert('project_issue_links', withDefaults(db.project_issue_links.map((l) => pickCols(l, { id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', category: 'type', issue_type: 'kind', name: 'name', url: 'url', ...audit, is_deleted: 'is_deleted' }))),
  ['id', 'tenant_id', 'project_id', 'category', 'issue_type', 'name', 'url', ...auditCols, 'is_deleted']);

const SOURCE = { manual: 'manual', leave_request: 'leave_request', ot_request: 'overtime_request' };
insert('timesheet_entries', db.timesheets.map((t) => pickCols(t, {
  id: 'id', tenant_id: 'tenant_id', employee_id: 'employee_id', project_id: 'project_id', task_type_id: 'task_type_id', work_date: 'work_date', hours: 'hours',
  description: 'description', status: 'status', source: (r) => SOURCE[r.source], source_request_id: 'source_request_id',
})), ['id', 'tenant_id', 'employee_id', 'project_id', 'task_type_id', 'work_date', 'hours', 'description', 'status', 'source', 'source_request_id']);
insert('leave_requests', db.leave_requests.map((l) => pickCols(l, {
  id: 'id', tenant_id: 'tenant_id', code: 'code', employee_id: 'employee_id', leave_type_id: 'leave_type_id', from_date: 'from_date', to_date: 'to_date', duration_type: 'duration_type',
  total_hours: 'total_hours', reason: 'reason', attachment_url: 'attachment_url', status: 'status', decided_by: 'approver_user_id', decided_at: 'decided_at', decision_note: 'decision_note',
  created_at: 'created_at', updated_at: (r) => r.decided_at ?? r.created_at,
})), ['id', 'tenant_id', 'code', 'employee_id', 'leave_type_id', 'from_date', 'to_date', 'duration_type', 'total_hours', 'reason', 'attachment_url', 'status', 'decided_by', 'decided_at', 'decision_note', 'created_at', 'updated_at']);
insert('overtime_requests', db.ot_requests.map((o) => pickCols(o, {
  id: 'id', tenant_id: 'tenant_id', code: 'code', employee_id: 'employee_id', project_id: 'project_id', work_date: 'work_date', start_time: 'start_time', end_time: 'end_time', hours: 'hours',
  day_type: 'day_kind', rate_percent: 'rate_percent', reason: 'reason', status: 'status', decided_by: 'approver_user_id', decided_at: 'decided_at', decision_note: 'decision_note',
  created_at: 'created_at', updated_at: (r) => r.decided_at ?? r.created_at,
})), ['id', 'tenant_id', 'code', 'employee_id', 'project_id', 'work_date', 'start_time', 'end_time', 'hours', 'day_type', 'rate_percent', 'reason', 'status', 'decided_by', 'decided_at', 'decision_note', 'created_at', 'updated_at']);
insert('leave_balances', db.leave_balances.map((b) => pickCols(b, { id: 'id', tenant_id: 'tenant_id', employee_id: 'employee_id', year: 'year', entitled_days: 'entitled_days', carried_over_days: 'carried_over_days', adjusted_days: 'adjusted_days', note: 'note' })),
  ['id', 'tenant_id', 'employee_id', 'year', 'entitled_days', 'carried_over_days', 'adjusted_days', 'note']);
insert('attendance_records', db.attendance_records.map((a) => pickCols(a, { id: 'id', tenant_id: 'tenant_id', employee_id: 'employee_id', work_date: 'work_date', check_in: 'check_in', check_out: 'check_out', source: 'source', note: 'note', updated_by: 'updated_by' })),
  ['id', 'tenant_id', 'employee_id', 'work_date', 'check_in', 'check_out', 'source', 'note', 'updated_by']);

insert('effort_allocations', db.effort_allocations.map((e) => pickCols(e, {
  id: 'id', tenant_id: 'tenant_id', employee_id: 'employee_id', project_id: 'project_id', period_month: 'year_month', team_id: 'team_id',
  position_id: (r) => position(EFFORT_ROLE[r.role] ?? r.role), note: 'note', contract_effort: 'hd', actual_effort: 'tt_effort', overtime_effort: 'ot_effort',
  performance_level_id: 'performance_level_id', performance_coefficient: 'performance', allocation_rate: 'allocate_rate',
})), ['id', 'tenant_id', 'employee_id', 'project_id', 'period_month', 'team_id', 'position_id', 'note', 'contract_effort', 'actual_effort', 'overtime_effort',
  'performance_level_id', 'performance_coefficient', 'allocation_rate']);

insert('project_quality_metrics', db.project_quality_metrics.map((q) => pickCols(q, {
  id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', period_month: 'metric_month', kind: 'kind', defect_count: 'defect_count', product_size: 'product_size', product_size_unit: 'product_size_unit',
})), ['id', 'tenant_id', 'project_id', 'period_month', 'kind', 'defect_count', 'product_size', 'product_size_unit']);
insert('project_pcv_items', db.project_pcv_items.map((p) => pickCols(p, { id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', category: 'group_name', content: 'content', sort_order: 'sort_seq', is_deleted: 'is_deleted' })),
  ['id', 'tenant_id', 'project_id', 'category', 'content', 'sort_order', 'is_deleted']);
insert('project_pcv_assessments', db.project_pcv_assessments.map((a) => pickCols(a, { id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', period_month: 'assess_month', assessed_by: 'assessed_by', assessed_at: 'assessed_at' })),
  ['id', 'tenant_id', 'project_id', 'period_month', 'assessed_by', 'assessed_at']);
const tenantOfAssessment = new Map(db.project_pcv_assessments.map((a) => [a.assessment_id ?? a.id, a.tenant_id]));
insert('project_pcv_results', db.project_pcv_results.map((r) => ({ tenant_id: tenantOfAssessment.get(r.assessment_id), assessment_id: r.assessment_id, item_id: r.item_id, result: r.result, note: r.error_note })),
  ['tenant_id', 'assessment_id', 'item_id', 'result', 'note']);

// CSS: form + câu hỏi riêng của từng dự án chép từ biểu mẫu tenant (BE làm việc này khi tạo dự án)
const cssTemplate = db.css_template[0];
const cssForms = [];
const cssQuestions = [];
for (const p of db.projects) {
  cssForms.push({ project_id: p.id, tenant_id: p.tenant_id, title: cssTemplate.title, intro: cssTemplate.intro, scale_note: cssTemplate.scale_note, comment_label: cssTemplate.comment_label });
  for (const q of db.css_template_questions) {
    cssQuestions.push({ id: uid(`css-question:${p.id}:${q.id}`), tenant_id: p.tenant_id, project_id: p.id, content: q.content, is_required: q.is_required, sort_order: q.sort_seq });
  }
}
insert('project_css_forms', cssForms, ['project_id', 'tenant_id', 'title', 'intro', 'scale_note', 'comment_label']);
insert('project_css_questions', cssQuestions, ['id', 'tenant_id', 'project_id', 'content', 'is_required', 'sort_order']);
insert('project_css_surveys', db.project_css_surveys.map((s) => pickCols(s, {
  id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', period_month: 'survey_month', token: 'token', status: 'status', sent_to: 'sent_to',
  sent_at: (r) => r.sent_at ?? `${r.survey_month.slice(0, 7)}-28T09:00:00+07:00`, respondent: 'respondent', comment: 'comment', submitted_at: 'submitted_at',
})), ['id', 'tenant_id', 'project_id', 'period_month', 'token', 'status', 'sent_to', 'sent_at', 'respondent', 'comment', 'submitted_at']);
const cssAnswers = [];
for (const s of db.project_css_surveys.filter((x) => x.status === 'submitted')) {
  for (const sc of s.scores ?? []) {
    const q = cssQuestions.find((x) => x.project_id === s.project_id && x.content === sc.question);
    if (!q) throw new Error(`Không khớp câu hỏi CSS "${sc.question}" của dự án ${s.project_id}`);
    cssAnswers.push({ tenant_id: s.tenant_id, survey_id: s.id, question_id: q.id, question_text: sc.question, score: sc.score });
  }
}
insert('project_css_answers', cssAnswers, ['tenant_id', 'survey_id', 'question_id', 'question_text', 'score']);

// Báo cáo tháng
const COLUMN_SOURCE = { date: 'date', checkIn: 'check_in', checkOut: 'check_out', hours: 'hours', content: 'content' };
const tenantOfReport = new Map(db.monthly_reports.map((r) => [r.id, r.tenant_id]));
insert('monthly_reports', db.monthly_reports.map((r) => pickCols(r, {
  id: 'id', tenant_id: 'tenant_id', project_id: 'project_id', period_month: 'report_month', client_name: 'client_name', company_name: 'company_name', order_number: 'order_no',
  work_name: 'work_name', responsible_person: 'responsible', language: 'language', updated_at: 'updated_at', updated_by: 'updated_by',
})), ['id', 'tenant_id', 'project_id', 'period_month', 'client_name', 'company_name', 'order_number', 'work_name', 'responsible_person', 'language', 'updated_at', 'updated_by']);
insert('monthly_report_members', db.monthly_reports.flatMap((r) => [
  ...(r.member_ids ?? []).map((e, i) => ({ tenant_id: r.tenant_id, report_id: r.id, employee_id: e, sort_order: i, is_removed: false })),
  ...(r.removed_employee_ids ?? []).filter((e) => !(r.member_ids ?? []).includes(e)).map((e, i) => ({ tenant_id: r.tenant_id, report_id: r.id, employee_id: e, sort_order: 1000 + i, is_removed: true })),
]), ['tenant_id', 'report_id', 'employee_id', 'sort_order', 'is_removed']);
insert('monthly_report_columns', db.monthly_reports.flatMap((r) => (r.columns ?? []).map((c, i) => ({
  id: uid(`mr-column:${r.id}:${c.id}`), tenant_id: r.tenant_id, report_id: r.id, column_key: c.id, source: c.source ? COLUMN_SOURCE[c.source] : null,
  label: c.label, label_ja: c.label_ja ?? null, data_type: c.type ?? 'text', sort_order: i,
}))), ['id', 'tenant_id', 'report_id', 'column_key', 'source', 'label', 'label_ja', 'data_type', 'sort_order']);
insert('monthly_report_entries', db.monthly_report_cells.map((c) => ({
  id: uid(`mr-entry:${c.report_id}:${c.employee_id}:${c.work_date}`), tenant_id: tenantOfReport.get(c.report_id), report_id: c.report_id, employee_id: c.employee_id,
  work_date: c.work_date, hours: c.hours, hours_source: c.source ?? null, start_time: c.start_time, end_time: c.end_time, time_source: c.time_source ?? null,
  content: c.content, custom_values: raw(json(c.custom ?? {})),
})), ['id', 'tenant_id', 'report_id', 'employee_id', 'work_date', 'hours', 'hours_source', 'start_time', 'end_time', 'time_source', 'content', 'custom_values']);

mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, `${out.join('\n')}\n`);
console.log(`Đã ghi ${OUT}`);
