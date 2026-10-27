// Endpoint quản trị hệ thống: Vai trò & quyền (D3), Người dùng, AI Insight, Prompt template (D5), Tích hợp (Q3).
// Dữ liệu sửa chỉ nằm trong bộ nhớ. BE .NET phải giữ đúng hợp đồng này.
import { randomUUID } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { ApiError, listParam, notFound, paginate, readJson } from './http.mjs';
import { addError, idFromPath, matches, maxLength, nowIso, required, str, throwIfErrors } from './validate.mjs';

/** Điều kiện hiển thị Insight (mã cố định; nhãn dịch ở FE: enum.insight_condition.*). */
export const INSIGHT_CONDITIONS = ['always', 'budget_anomaly', 'fte_shortage', 'fte_overload', 'unassigned_positions', 'missing_timesheet', 'schedule_weekly_mon', 'ai_smart_detect'];
/** Nguồn dữ liệu Insight có thể chọn (port AVAILABLE_DATA_SOURCES; nhãn: enum.insight_data_source.*). */
export const INSIGHT_DATA_SOURCES = ['timesheets', 'employees', 'budget', 'milestones', 'pcv', 'css', 'defect', 'leakage', 'ee', 'issues_tracker', 'git', 'skills', 'documents', 'risks', 'changes', 'issues', 'cross_links', 'monthly_report', 'projects', 'leave', 'attendance'];
const PROMPT_SCOPES = ['insight', 'chat', 'translate'];
const PROMPT_STATUSES = ['draft', 'active', 'archived'];

/**
 * Nguồn tích hợp ở Quản trị → Tích hợp (Q3): Jira, Base, Bitrix. Chỉ trường không bí mật được trả ra/sửa ở đây;
 * khoá bí mật (API token, access token, webhook) nằm ở appsettings.Secrets.json. Connector AI và Email không nằm trong
 * danh sách này — cấu hình riêng ở Integrations:Ai / Integrations:Email trên server.
 */
const CONNECTORS = {
  Jira: { category: 'project_management', settings: ['baseUrl', 'accountEmail', 'projectKeys'], secret: 'apiToken', previewFile: 'jira/issues.json' },
  Base: { category: 'work_management', settings: ['baseUrl'], secret: 'accessToken', previewFile: 'base/tasks.json' },
  Bitrix: { category: 'crm', settings: ['baseUrl'], secret: 'webhookToken', previewFile: 'bitrix/deals.json' },
};
const DEFAULT_SETTINGS = {
  Jira: { baseUrl: 'https://your-company.atlassian.net', accountEmail: '', projectKeys: '' },
  Base: { baseUrl: 'https://your-company.base.vn' },
  Bitrix: { baseUrl: 'https://your-company.bitrix24.vn' },
};

/** Thay {{biến}} / {{a.b}} trong template bằng giá trị trong context — mô phỏng bước biên dịch prompt ở BE. */
export function renderTemplate(template, context) {
  return template.replace(/\{\{\s*([a-zA-Z_][\w.]*)\s*\}\}/g, (_, path) => {
    const value = path.split('.').reduce((obj, key) => (obj == null ? undefined : obj[key]), context);
    if (value === undefined) return `{{${path}}}`;
    return typeof value === 'object' ? JSON.stringify(value) : String(value);
  });
}
/** Kiểm tra cặp ngoặc {{ }} cân bằng; trả mã lỗi hoặc null. */
export function templateError(template) {
  const open = (template.match(/\{\{/g) || []).length;
  const close = (template.match(/\}\}/g) || []).length;
  if (open !== close) return 'prompt_template_save.err_unbalanced';
  if (/\{\{\s*\}\}/.test(template)) return 'prompt_template_save.err_empty_variable';
  return null;
}

/**
 * @param {{ store: ReturnType<import('./store.mjs').createStore>, currentUser: Function, requirePermission: Function, screens: Record<string, unknown> }} deps
 */
export function systemRoutes({ store, currentUser, requirePermission, screens }) {
  const { db } = store;
  const tenantId = () => db.tenants[0].id;
  const guard = (permission) => (req) => {
    const ctx = currentUser(req);
    requirePermission(ctx, permission);
    return ctx;
  };
  const rolesAdmin = guard('admin.roles');
  const insightsAdmin = guard('admin.insights');
  const integrationsAdmin = guard('admin.integrations');
  const permissionByKey = new Map(db.permissions.map((p) => [p.key, p]));
  const screenKeys = Object.keys(screens).filter((k) => !k.startsWith('$'));

  // ─── Vai trò ──────────────────────────────────────────────────────────────

  const permissionsOfRole = (roleId) =>
    db.role_permissions.filter((rp) => rp.role_id === roleId).map((rp) => db.permissions.find((p) => p.id === rp.permission_id)?.key).filter(Boolean).sort();
  const membersOfRole = (roleId) => db.user_roles.filter((ur) => ur.role_id === roleId).map((ur) => db.users.find((u) => u.id === ur.user_id)).filter(Boolean);

  function roleDto(r, culture) {
    return {
      id: r.id, code: r.code, name: store.localized(r, culture), description: r.description ?? null, isSystem: Boolean(r.is_system),
      permissions: permissionsOfRole(r.id), memberCount: membersOfRole(r.id).length, updatedAt: r.updated_at ?? r.created_at,
    };
  }
  const userRef = (u) => ({ id: u.id, fullName: u.full_name, email: u.email, avatarUrl: u.avatar_url ?? null, isActive: u.is_active });

  function searchRoles(url, culture) {
    const search = url.searchParams.get('search') || '';
    const perms = listParam(url, 'permissions');
    const types = listParam(url, 'types'); // system | custom
    return db.roles
      .filter((r) => !r.is_deleted)
      .map((r) => roleDto(r, culture))
      .filter((r) => matches(search, r.name, r.code))
      .filter((r) => perms.every((p) => r.permissions.includes(p)))
      .filter((r) => types.length === 0 || types.includes(r.isSystem ? 'system' : 'custom'))
      .sort((a, b) => Number(b.isSystem) - Number(a.isSystem) || a.name.localeCompare(b.name, 'vi'));
  }

  function setRolePermissions(roleId, keys) {
    db.role_permissions = db.role_permissions.filter((rp) => rp.role_id !== roleId);
    for (const key of new Set(keys)) db.role_permissions.push({ role_id: roleId, permission_id: permissionByKey.get(key).id });
  }

  /** Không để hệ thống mất người quản trị vai trò cuối cùng (tránh tự khoá mình ra ngoài). */
  function assertRoleAdminRemains() {
    const anyAdmin = db.users.some((u) => u.is_active && store.permissionsOfUser(u.id).includes('admin.roles'));
    if (!anyAdmin) throw new ApiError(409, 'role_save.err_last_admin');
  }

  function validateRole(body, current) {
    const errors = {};
    const name = str(body.name);
    const permissions = Array.isArray(body.permissions) ? body.permissions.map(str) : [];
    if (required(errors, 'name', name)) {
      maxLength(errors, 'name', name, 100);
      if (current?.is_system && name !== current.name) addError(errors, 'name', 'role_save.err_system_name_locked');
      if (db.roles.some((r) => !r.is_deleted && r.id !== current?.id && r.name.toLowerCase() === name.toLowerCase())) addError(errors, 'name', 'role_save.err_duplicate_name');
    }
    maxLength(errors, 'description', str(body.description), 500);
    if (permissions.length === 0) addError(errors, 'permissions', 'role_save.err_no_permission');
    if (permissions.some((p) => !permissionByKey.has(p))) addError(errors, 'permissions', 'validation.invalid_value');
    throwIfErrors(errors);
    return { name, description: str(body.description) || null, permissions };
  }

  const findRole = (url, index = -1) => {
    const row = db.roles.find((r) => r.id === idFromPath(url, index) && !r.is_deleted);
    if (!row) throw notFound();
    return row;
  };

  // ─── AI Insight / Prompt template ─────────────────────────────────────────

  function insightConfigDto(i) {
    return {
      id: i.id, name: i.name, screenKey: i.screen_key, triggerCondition: i.trigger_condition ?? 'always', dataSources: i.data_sources ?? [],
      allowedRoleIds: i.allowed_roles ?? [], dataPrompt: i.data_prompt, status: i.status, updatedAt: i.updated_at ?? null,
    };
  }
  function validateInsight(body) {
    const errors = {};
    const name = str(body.name);
    const screenKey = str(body.screenKey);
    const dataPrompt = str(body.dataPrompt);
    const triggerCondition = str(body.triggerCondition) || 'always';
    const dataSources = Array.isArray(body.dataSources) ? body.dataSources.map(str) : [];
    const allowedRoleIds = Array.isArray(body.allowedRoleIds) ? body.allowedRoleIds.map(str) : [];
    if (required(errors, 'name', name)) maxLength(errors, 'name', name, 150);
    if (required(errors, 'screenKey', screenKey) && !screenKeys.includes(screenKey)) addError(errors, 'screenKey', 'validation.invalid_value');
    if (!INSIGHT_CONDITIONS.includes(triggerCondition)) addError(errors, 'triggerCondition', 'validation.invalid_value');
    if (dataSources.length === 0) addError(errors, 'dataSources', 'validation.required');
    else if (dataSources.some((s) => !INSIGHT_DATA_SOURCES.includes(s))) addError(errors, 'dataSources', 'validation.invalid_value');
    if (allowedRoleIds.some((id) => !db.roles.some((r) => r.id === id))) addError(errors, 'allowedRoleIds', 'validation.invalid_value');
    if (required(errors, 'dataPrompt', dataPrompt)) maxLength(errors, 'dataPrompt', dataPrompt, 4000);
    const status = str(body.status) || 'active';
    if (!['active', 'paused'].includes(status)) addError(errors, 'status', 'validation.invalid_value');
    throwIfErrors(errors);
    return { name, screen_key: screenKey, trigger_condition: triggerCondition, data_sources: dataSources, allowed_roles: allowedRoleIds, data_prompt: dataPrompt, status };
  }

  function promptDto(p) {
    return {
      id: p.id, name: p.name, scope: p.scope, screenKeys: p.screen_keys ?? [], template: p.template, description: p.description ?? null,
      status: p.status, version: p.version ?? 1, updatedAt: p.updated_at ?? null,
    };
  }
  function validatePrompt(body) {
    const errors = {};
    const name = str(body.name);
    const template = typeof body.template === 'string' ? body.template : '';
    const scope = str(body.scope);
    const status = str(body.status) || 'draft';
    const screenKeysIn = Array.isArray(body.screenKeys) ? body.screenKeys.map(str) : [];
    if (required(errors, 'name', name)) maxLength(errors, 'name', name, 150);
    if (!PROMPT_SCOPES.includes(scope)) addError(errors, 'scope', 'validation.invalid_value');
    if (!PROMPT_STATUSES.includes(status)) addError(errors, 'status', 'validation.invalid_value');
    if (screenKeysIn.some((k) => !screenKeys.includes(k))) addError(errors, 'screenKeys', 'validation.invalid_value');
    if (required(errors, 'template', template.trim())) {
      maxLength(errors, 'template', template, 20000);
      const err = templateError(template);
      if (err) addError(errors, 'template', err);
    }
    maxLength(errors, 'description', str(body.description), 500);
    throwIfErrors(errors);
    return { name, scope, status, screen_keys: screenKeysIn, template, description: str(body.description) || null };
  }

  /** Bộ dữ liệu mẫu để xem trước prompt (BE thật dựng từ dữ liệu của màn). */
  function sampleContext(culture) {
    const live = db.projects.filter((p) => !p.is_deleted);
    return {
      company_name: db.system_settings.find((s) => s.key === 'company_name')?.value ?? db.tenants[0].name,
      locale_name: culture === 'en' ? 'English' : 'tiếng Việt',
      today: new Date().toISOString().slice(0, 10),
      screen_key: 'project_list',
      context: { projects: { total: live.length, running: live.filter((p) => store.masterById.get(p.status_id)?.code === 'RUNNING').length }, employees: { total: db.employees.length } },
      user: { full_name: 'Nguyễn Văn A' },
    };
  }

  // ─── Tích hợp ─────────────────────────────────────────────────────────────

  function integrationDto(row) {
    const def = CONNECTORS[row.key];
    const settings = { ...DEFAULT_SETTINGS[row.key], ...(row.settings ?? {}) };
    return {
      key: row.key, category: def.category, enabled: row.enabled, useMock: row.use_mock,
      settings: Object.fromEntries(def.settings.map((k) => [k, settings[k] ?? ''])),
      // Khoá bí mật chỉ cấu hình ở server (Integrations:<Key> trong appsettings.Secrets.json) — FE chỉ biết đã có hay chưa
      hasSecret: row.use_mock ? false : Boolean(row.has_secret), lastSyncAt: row.last_sync_at ?? null, lastTestAt: row.last_test_at ?? null, lastTestOk: row.last_test_ok ?? null,
    };
  }
  const findIntegration = (url, index = -1) => {
    const row = db.integrations.find((i) => i.key === idFromPath(url, index));
    if (!row) throw notFound();
    return row;
  };
  const readMock = (path) => {
    const full = join(store.root, path);
    return existsSync(full) ? JSON.parse(readFileSync(full, 'utf8')) : [];
  };

  return [
    // Quyền & vai trò
    ['GET', /^\/api\/v1\/permissions$/, async ({ req, culture }) => {
      currentUser(req);
      return { data: [...db.permissions].sort((a, b) => a.sort_seq - b.sort_seq).map((p) => ({ key: p.key, group: p.group, name: store.localized(p, culture), sortSeq: p.sort_seq })) };
    }],
    ['GET', /^\/api\/v1\/roles$/, async ({ req, url, culture }) => {
      currentUser(req);
      return { data: paginate(searchRoles(url, culture), url, 20) };
    }],
    ['GET', /^\/api\/v1\/roles\/summary$/, async ({ req, url, culture }) => {
      rolesAdmin(req);
      const rows = searchRoles(url, culture);
      return { data: { total: rows.length, system: rows.filter((r) => r.isSystem).length, custom: rows.filter((r) => !r.isSystem).length, assignedUsers: rows.reduce((s, r) => s + r.memberCount, 0) } };
    }],
    ['PUT', /^\/api\/v1\/roles\/permissions$/, async ({ req, culture }) => {
      rolesAdmin(req);
      const body = await readJson(req);
      const updates = Array.isArray(body.updates) ? body.updates : [];
      const errors = {};
      for (const u of updates) {
        if (!db.roles.some((r) => r.id === u.roleId && !r.is_deleted)) addError(errors, 'updates', 'validation.invalid_value');
        else if (!Array.isArray(u.permissions) || u.permissions.length === 0) addError(errors, u.roleId, 'role_save.err_no_permission');
        else if (u.permissions.some((p) => !permissionByKey.has(p))) addError(errors, u.roleId, 'validation.invalid_value');
      }
      throwIfErrors(errors);
      const backup = [...db.role_permissions];
      for (const u of updates) setRolePermissions(u.roleId, u.permissions);
      try {
        assertRoleAdminRemains();
      } catch (e) {
        db.role_permissions = backup;
        throw e;
      }
      return { key: 'role_bulk_update.msg_success', args: { count: updates.length }, data: updates.map((u) => roleDto(db.roles.find((r) => r.id === u.roleId), culture)) };
    }],
    ['GET', /^\/api\/v1\/roles\/[^/]+$/, async ({ req, url, culture }) => {
      rolesAdmin(req);
      return { data: roleDto(findRole(url), culture) };
    }],
    ['POST', /^\/api\/v1\/roles$/, async ({ req, culture }) => {
      rolesAdmin(req);
      const v = validateRole(await readJson(req), null);
      const row = { id: randomUUID(), tenant_id: tenantId(), code: `custom_${Date.now().toString(36)}`, name: v.name, name_en: null, description: v.description, is_system: false, created_at: nowIso(), updated_at: nowIso() };
      db.roles.push(row);
      setRolePermissions(row.id, v.permissions);
      return { status: 201, key: 'role_create.msg_success', data: roleDto(row, culture) };
    }],
    ['PUT', /^\/api\/v1\/roles\/[^/]+$/, async ({ req, url, culture }) => {
      rolesAdmin(req);
      const row = findRole(url);
      const v = validateRole(await readJson(req), row);
      const backup = [...db.role_permissions];
      setRolePermissions(row.id, v.permissions);
      try {
        assertRoleAdminRemains();
      } catch (e) {
        db.role_permissions = backup;
        throw e;
      }
      Object.assign(row, { name: v.name, name_en: row.is_system ? row.name_en : null, description: v.description, updated_at: nowIso() });
      return { key: 'role_update.msg_success', data: roleDto(row, culture) };
    }],
    ['DELETE', /^\/api\/v1\/roles\/[^/]+$/, async ({ req, url }) => {
      rolesAdmin(req);
      const row = findRole(url);
      if (row.is_system) throw new ApiError(409, 'role_delete.err_system');
      const removed = membersOfRole(row.id).length;
      row.is_deleted = true;
      db.user_roles = db.user_roles.filter((ur) => ur.role_id !== row.id);
      db.role_permissions = db.role_permissions.filter((rp) => rp.role_id !== row.id);
      return { key: 'role_delete.msg_success', args: { name: row.name, count: removed } };
    }],
    ['GET', /^\/api\/v1\/roles\/[^/]+\/users$/, async ({ req, url }) => {
      rolesAdmin(req);
      return { data: membersOfRole(findRole(url, -2).id).map(userRef) };
    }],
    ['POST', /^\/api\/v1\/roles\/[^/]+\/users$/, async ({ req, url }) => {
      rolesAdmin(req);
      const role = findRole(url, -2);
      const { userId } = await readJson(req);
      if (!db.users.some((u) => u.id === userId)) throw notFound();
      if (!db.user_roles.some((ur) => ur.user_id === userId && ur.role_id === role.id)) db.user_roles.push({ user_id: userId, role_id: role.id });
      return { key: 'role_assign.msg_success', data: membersOfRole(role.id).map(userRef) };
    }],
    ['DELETE', /^\/api\/v1\/roles\/[^/]+\/users\/[^/]+$/, async ({ req, url }) => {
      rolesAdmin(req);
      const role = findRole(url, -3);
      const userId = idFromPath(url);
      const backup = [...db.user_roles];
      db.user_roles = db.user_roles.filter((ur) => !(ur.user_id === userId && ur.role_id === role.id));
      try {
        assertRoleAdminRemains();
      } catch (e) {
        db.user_roles = backup;
        throw e;
      }
      return { key: 'role_unassign.msg_success', data: membersOfRole(role.id).map(userRef) };
    }],
    ['GET', /^\/api\/v1\/users$/, async ({ req, url }) => {
      rolesAdmin(req);
      const search = url.searchParams.get('search') || '';
      return { data: db.users.filter((u) => matches(search, u.full_name, u.email)).map(userRef) };
    }],

    // AI Insight (cấu hình theo màn hình)
    ['GET', /^\/api\/v1\/ai\/insight-configs$/, async ({ req, url }) => {
      insightsAdmin(req);
      const search = url.searchParams.get('search') || '';
      const screensIn = listParam(url, 'screens');
      const statuses = listParam(url, 'statuses');
      const rows = db.ai_insights
        .filter((i) => !i.is_deleted)
        .filter((i) => matches(search, i.name, i.data_prompt))
        .filter((i) => screensIn.length === 0 || screensIn.includes(i.screen_key))
        .filter((i) => statuses.length === 0 || statuses.includes(i.status))
        .map(insightConfigDto);
      return { data: paginate(rows, url, 20) };
    }],
    ['POST', /^\/api\/v1\/ai\/insight-configs\/preview$/, async ({ req, culture }) => {
      insightsAdmin(req);
      const body = await readJson(req);
      const errors = {};
      if (!str(body.dataPrompt)) addError(errors, 'dataPrompt', 'ai_insight_preview.err_prompt_required');
      if (!screenKeys.includes(str(body.screenKey))) addError(errors, 'screenKey', 'validation.invalid_value');
      throwIfErrors(errors);
      await new Promise((r) => setTimeout(r, 900)); // mô phỏng thời gian LLM xử lý
      const items = store.insights(str(body.screenKey), culture, screens).filter((i) => i.screenKey === str(body.screenKey));
      return { key: 'ai_insight_run.msg_success', data: items.length ? items : store.insights('project_list', culture, screens).slice(0, 1) };
    }],
    ['GET', /^\/api\/v1\/ai\/insight-configs\/[^/]+$/, async ({ req, url }) => {
      insightsAdmin(req);
      const row = db.ai_insights.find((i) => i.id === idFromPath(url) && !i.is_deleted);
      if (!row) throw notFound();
      return { data: insightConfigDto(row) };
    }],
    ['POST', /^\/api\/v1\/ai\/insight-configs$/, async ({ req }) => {
      insightsAdmin(req);
      const row = { id: randomUUID(), tenant_id: tenantId(), ...validateInsight(await readJson(req)), updated_at: nowIso() };
      db.ai_insights.push(row);
      return { status: 201, key: 'ai_insight_create.msg_success', data: insightConfigDto(row) };
    }],
    ['PUT', /^\/api\/v1\/ai\/insight-configs\/[^/]+$/, async ({ req, url }) => {
      insightsAdmin(req);
      const row = db.ai_insights.find((i) => i.id === idFromPath(url) && !i.is_deleted);
      if (!row) throw notFound();
      Object.assign(row, validateInsight(await readJson(req)), { updated_at: nowIso() });
      return { key: 'ai_insight_update.msg_success', data: insightConfigDto(row) };
    }],
    ['DELETE', /^\/api\/v1\/ai\/insight-configs\/[^/]+$/, async ({ req, url }) => {
      insightsAdmin(req);
      const row = db.ai_insights.find((i) => i.id === idFromPath(url) && !i.is_deleted);
      if (!row) throw notFound();
      row.is_deleted = true;
      return { key: 'ai_insight_delete.msg_success', args: { name: row.name } };
    }],

    // Prompt template
    ['GET', /^\/api\/v1\/ai\/prompt-templates$/, async ({ req, url }) => {
      insightsAdmin(req);
      const search = url.searchParams.get('search') || '';
      const scopes = listParam(url, 'scopes');
      const statuses = listParam(url, 'statuses');
      const rows = db.ai_prompt_templates
        .filter((p) => !p.is_deleted)
        .filter((p) => matches(search, p.name, p.description))
        .filter((p) => scopes.length === 0 || scopes.includes(p.scope))
        .filter((p) => statuses.length === 0 || statuses.includes(p.status))
        .map(promptDto);
      return { data: paginate(rows, url, 20) };
    }],
    ['POST', /^\/api\/v1\/ai\/prompt-templates\/preview$/, async ({ req, culture }) => {
      insightsAdmin(req);
      const body = await readJson(req);
      const template = typeof body.template === 'string' ? body.template : '';
      const errors = {};
      if (required(errors, 'template', template.trim())) {
        const err = templateError(template);
        if (err) addError(errors, 'template', err);
      }
      throwIfErrors(errors);
      const context = sampleContext(culture);
      return { data: { output: renderTemplate(template, context), variables: Object.keys(context) } };
    }],
    ['GET', /^\/api\/v1\/ai\/prompt-templates\/[^/]+$/, async ({ req, url }) => {
      insightsAdmin(req);
      const row = db.ai_prompt_templates.find((p) => p.id === idFromPath(url) && !p.is_deleted);
      if (!row) throw notFound();
      return { data: promptDto(row) };
    }],
    ['POST', /^\/api\/v1\/ai\/prompt-templates$/, async ({ req }) => {
      insightsAdmin(req);
      const row = { id: randomUUID(), tenant_id: tenantId(), ...validatePrompt(await readJson(req)), version: 1, updated_at: nowIso() };
      db.ai_prompt_templates.push(row);
      return { status: 201, key: 'prompt_template_create.msg_success', data: promptDto(row) };
    }],
    ['PUT', /^\/api\/v1\/ai\/prompt-templates\/[^/]+$/, async ({ req, url }) => {
      insightsAdmin(req);
      const row = db.ai_prompt_templates.find((p) => p.id === idFromPath(url) && !p.is_deleted);
      if (!row) throw notFound();
      Object.assign(row, validatePrompt(await readJson(req)), { version: (row.version ?? 1) + 1, updated_at: nowIso() });
      return { key: 'prompt_template_update.msg_success', data: promptDto(row) };
    }],
    ['DELETE', /^\/api\/v1\/ai\/prompt-templates\/[^/]+$/, async ({ req, url }) => {
      insightsAdmin(req);
      const row = db.ai_prompt_templates.find((p) => p.id === idFromPath(url) && !p.is_deleted);
      if (!row) throw notFound();
      if (row.status === 'active' && db.ai_prompt_templates.filter((p) => !p.is_deleted && p.scope === row.scope && p.status === 'active').length === 1) {
        throw new ApiError(409, 'prompt_template_delete.err_last_active');
      }
      row.is_deleted = true;
      return { key: 'prompt_template_delete.msg_success', args: { name: row.name } };
    }],

    // Tích hợp (connector Q3)
    ['GET', /^\/api\/v1\/integrations$/, async ({ req }) => {
      integrationsAdmin(req);
      return { data: db.integrations.filter((i) => CONNECTORS[i.key]).map(integrationDto) };
    }],
    ['PUT', /^\/api\/v1\/integrations\/[A-Za-z]+$/, async ({ req, url }) => {
      integrationsAdmin(req);
      const row = findIntegration(url);
      const body = await readJson(req);
      const def = CONNECTORS[row.key];
      const errors = {};
      const settings = {};
      for (const [k, raw] of Object.entries(body.settings ?? {})) {
        if (!def.settings.includes(k)) {
          addError(errors, k, 'validation.invalid_value');
          continue;
        }
        const v = str(String(raw ?? ''));
        maxLength(errors, k, v, 300);
        if (/url$/i.test(k) && v && !/^https:\/\//.test(v)) addError(errors, k, 'integration_update.err_https');
        if (k === 'accountEmail' && v && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) addError(errors, k, 'validation.invalid_email');
        settings[k] = v;
      }
      if (typeof body.enabled !== 'boolean') addError(errors, 'enabled', 'validation.invalid_value');
      throwIfErrors(errors);
      Object.assign(row, { enabled: body.enabled, settings: { ...(row.settings ?? {}), ...settings }, updated_at: nowIso() });
      return { key: 'integration_update.msg_success', data: integrationDto(row) };
    }],
    ['POST', /^\/api\/v1\/integrations\/[A-Za-z]+\/test$/, async ({ req, url }) => {
      integrationsAdmin(req);
      const row = findIntegration(url, -2);
      await new Promise((r) => setTimeout(r, 600));
      // Chế độ mock luôn kết nối được; chế độ thật cần khoá bí mật trên server
      const ok = row.enabled && (row.use_mock || row.has_secret);
      Object.assign(row, { last_test_at: nowIso(), last_test_ok: ok });
      if (!row.enabled) throw new ApiError(409, 'integration_test.err_disabled');
      if (!ok) throw new ApiError(502, 'integration_test.err_failed');
      return { key: 'integration_test.msg_success', data: integrationDto(row) };
    }],
    ['GET', /^\/api\/v1\/integrations\/[A-Za-z]+\/preview$/, async ({ req, url }) => {
      integrationsAdmin(req);
      const row = findIntegration(url, -2);
      if (!row.enabled) throw new ApiError(409, 'integration_test.err_disabled');
      const def = CONNECTORS[row.key];
      // File giả lập giữ tên trường gần API thật: Jira issue (key, summary, status, updated), Base task (name, status, updated_at),
      // Bitrix24 deal (title, stage, date_modify)
      const items = readMock(def.previewFile).map((x) => ({
        title: x.key ? `${x.key} · ${x.summary}` : (x.name ?? x.title),
        subtitle: x.status ?? x.stage ?? '',
        url: x.url ?? null,
        updatedAt: x.updated ?? x.updated_at ?? x.date_modify ?? null,
      }));
      row.last_sync_at = nowIso();
      return { data: items };
    }],
  ];
}
