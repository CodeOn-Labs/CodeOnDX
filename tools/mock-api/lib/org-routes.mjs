// Endpoint tổ chức (D4 — tự quản lý trong phần mềm): Khách hàng, Phòng ban & Team, Nhân sự (+ mời tạo tài khoản).
// Dữ liệu sửa chỉ nằm trong bộ nhớ. BE .NET phải giữ đúng hợp đồng này (cùng request → cùng cấu trúc `data`, cùng key lỗi).
import { randomUUID } from 'node:crypto';
import { ApiError, listParam, notFound, paginate, readJson } from './http.mjs';
import { today } from './store.mjs';
import {
  addError, idFromPath, matches, maxLength, nowIso, required, str, throwIfErrors, validDate, validEmail, EMAIL_PATTERN,
} from './validate.mjs';

/** Trạng thái phân bổ nhân sự theo % FTE đang được gán (port getEmployeeStatus của employee.service.ts bản cũ). */
export function allocationStatus(percent, working, rules) {
  if (!working) return 'resigned';
  if (percent > rules.overload) return 'overloaded';
  if (percent <= 0) return 'bench';
  if (percent < rules.min) return 'low';
  return 'normal';
}

/**
 * @param {{ store: ReturnType<import('./store.mjs').createStore>, currentUser: Function, requirePermission: Function }} deps
 */
export function orgRoutes({ store, currentUser, requirePermission }) {
  const { db } = store;
  const tenantId = () => db.tenants[0].id;
  const guard = (permission) => (req) => {
    const ctx = currentUser(req);
    requirePermission(ctx, permission);
    return ctx;
  };
  const settingsAdmin = guard('admin.settings');
  const employeeViewer = guard('employee.view');
  const employeeManager = guard('employee.manage');

  // ─── Khách hàng ───────────────────────────────────────────────────────────

  const projectsOfClient = (id) => db.projects.filter((p) => p.client_id === id && !p.is_deleted).length;

  function clientDto(c, culture) {
    return {
      id: c.id, code: c.code, name: c.name, shortName: c.short_name ?? null,
      country: store.masterRef(c.country_id, culture), industry: store.masterRef(c.industry_id, culture),
      contactName: c.contact_name ?? null, contactEmail: c.contact_email ?? null, defaultCurrency: c.default_currency ?? null,
      isActive: c.is_active, isDeleted: Boolean(c.is_deleted), projectsCount: projectsOfClient(c.id), createdAt: c.created_at,
      // giữ cho các màn cũ đang đọc `country` dạng mã
      countryCode: store.masterById.get(c.country_id)?.code ?? c.country ?? null,
    };
  }

  function searchClients(url) {
    const search = url.searchParams.get('search') || '';
    const countryIds = listParam(url, 'countryIds');
    const industryIds = listParam(url, 'industryIds');
    const status = url.searchParams.get('status'); // active | inactive
    const deleted = url.searchParams.get('deleted') === 'true';
    return db.clients
      .filter((c) => Boolean(c.is_deleted) === deleted)
      .filter((c) => matches(search, c.name, c.code, c.contact_name, c.contact_email))
      .filter((c) => countryIds.length === 0 || countryIds.includes(c.country_id))
      .filter((c) => industryIds.length === 0 || industryIds.includes(c.industry_id))
      .filter((c) => !status || (status === 'active' ? c.is_active : !c.is_active))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  }

  function validateClient(body, current) {
    const errors = {};
    const name = str(body.name);
    const code = str(body.code).toUpperCase();
    const contactEmail = str(body.contactEmail);
    if (required(errors, 'name', name)) {
      maxLength(errors, 'name', name, 200);
      if (db.clients.some((c) => !c.is_deleted && c.name.toLowerCase() === name.toLowerCase() && c.id !== current?.id)) addError(errors, 'name', 'client_save.err_duplicate_name');
    }
    if (code && !/^[A-Z0-9_]{1,30}$/.test(code)) addError(errors, 'code', 'client_save.err_code_format');
    else if (code && db.clients.some((c) => c.code === code && c.id !== current?.id)) addError(errors, 'code', 'client_save.err_duplicate_code');
    maxLength(errors, 'contactName', str(body.contactName), 200);
    if (contactEmail && !EMAIL_PATTERN.test(contactEmail)) addError(errors, 'contactEmail', 'validation.invalid_email');
    for (const [field, type] of [['countryId', 'country'], ['industryId', 'industry']]) {
      const id = str(body[field]);
      if (id && store.masterById.get(id)?.type !== type) addError(errors, field, 'validation.invalid_value');
    }
    const currency = str(body.defaultCurrency);
    if (currency && !db.master_data.some((m) => m.type === 'currency' && m.code === currency)) addError(errors, 'defaultCurrency', 'validation.invalid_value');
    throwIfErrors(errors);
    const countryId = str(body.countryId) || null;
    return {
      name, code: code || current?.code || name.replace(/[^A-Za-z0-9]/g, '').toUpperCase().slice(0, 12) || randomUUID().slice(0, 8).toUpperCase(),
      short_name: str(body.shortName) || null, country_id: countryId, country: countryId ? store.masterById.get(countryId).code : null,
      industry_id: str(body.industryId) || null, contact_name: str(body.contactName) || null, contact_email: contactEmail || null,
      default_currency: currency || null, is_active: body.isActive !== false,
    };
  }

  const findClient = (url, index = -1) => {
    const row = db.clients.find((c) => c.id === idFromPath(url, index));
    if (!row) throw notFound();
    return row;
  };

  // ─── Phòng ban & Team ─────────────────────────────────────────────────────

  const employeeRef = (id) => {
    const e = id ? db.employees.find((x) => x.id === id) : null;
    return e ? { id: e.id, fullName: e.full_name, email: e.email, avatarUrl: e.avatar_url ?? null } : null;
  };
  const activeEmployees = () => db.employees.filter((e) => !e.is_deleted && e.is_active);

  function teamDto(t) {
    return {
      id: t.id, departmentId: t.department_id, code: t.code, name: t.name, description: t.description ?? null,
      leader: employeeRef(t.leader_employee_id), sortSeq: t.sort_seq, isActive: t.is_active,
      memberCount: activeEmployees().filter((e) => e.team_id === t.id).length,
    };
  }
  function departmentDto(d) {
    return {
      id: d.id, code: d.code, name: d.name, description: d.description ?? null, manager: employeeRef(d.manager_employee_id),
      sortSeq: d.sort_seq, isActive: d.is_active, memberCount: activeEmployees().filter((e) => e.department_id === d.id).length,
      teams: db.teams.filter((t) => t.department_id === d.id && !t.is_deleted).sort((a, b) => a.sort_seq - b.sort_seq).map(teamDto),
    };
  }

  function validateUnit(body, rows, current, extra = () => {}) {
    const errors = {};
    const code = str(body.code).toUpperCase();
    const name = str(body.name);
    if (required(errors, 'code', code)) {
      if (!/^[A-Z0-9_-]{1,30}$/.test(code)) addError(errors, 'code', 'org_unit_save.err_code_format');
      else if (rows.some((r) => !r.is_deleted && r.code === code && r.id !== current?.id)) addError(errors, 'code', 'org_unit_save.err_duplicate_code');
    }
    if (required(errors, 'name', name)) maxLength(errors, 'name', name, 150);
    maxLength(errors, 'description', str(body.description), 500);
    extra(errors);
    throwIfErrors(errors);
    return { code, name, description: str(body.description) || null, is_active: body.isActive !== false };
  }
  const validEmployeeId = (errors, field, id) => {
    if (id && !db.employees.some((e) => e.id === id && !e.is_deleted)) addError(errors, field, 'validation.invalid_value');
  };

  // ─── Nhân sự ──────────────────────────────────────────────────────────────

  const hrRules = () => ({ overload: store.ruleValue('HR_OVERLOAD_ALLOCATION_THRESHOLD', 100), min: store.ruleValue('HR_MIN_ALLOCATION_THRESHOLD', 20) });
  const masterByCode = (type, code) => db.master_data.find((m) => m.type === type && m.code === code);
  const isWorking = (e) => e.is_active && !e.is_deleted && masterByCode('employee_status', e.status)?.metadata?.working !== false;

  /** Phân công còn hiệu lực hôm nay của nhân sự (dự án chưa xoá). */
  function activeAssignments(employeeId) {
    const now = today();
    return db.assignments.filter((a) => a.employee_id === employeeId && !a.is_deleted && a.start_date <= now && a.end_date >= now)
      .filter((a) => db.projects.some((p) => p.id === a.project_id && !p.is_deleted));
  }

  function employeeDto(e, culture) {
    const assignments = activeAssignments(e.id);
    // FTE đang gán = tổng fte_allocated các phân công còn hiệu lực; % phân bổ = FTE × 100 (1 FTE = 100%)
    const totalFte = Math.round(assignments.reduce((s, a) => s + (a.fte_allocated || 0), 0) * 100) / 100;
    const allocationPercent = Math.round(totalFte * 100);
    const projects = [...new Map(assignments.map((a) => [a.project_id, a])).values()].map((a) => {
      const p = db.projects.find((x) => x.id === a.project_id);
      return { id: p.id, code: p.code, name: p.name, role: masterByCode('job_position', a.role_in_project) ? store.localized(masterByCode('job_position', a.role_in_project), culture) : a.role_in_project, fte: a.fte_allocated };
    });
    const status = masterByCode('employee_status', e.status);
    const department = db.departments.find((d) => d.id === e.department_id);
    const team = db.teams.find((t) => t.id === e.team_id);
    const user = e.user_id ? db.users.find((u) => u.id === e.user_id) : null;
    const invitation = (db.invitations ?? []).find((i) => i.employee_id === e.id && !i.accepted_at);
    return {
      id: e.id, code: e.code, fullName: e.full_name, email: e.email, phone: e.phone ?? null,
      position: masterByCode('job_position', e.position_code) ? store.masterRef(masterByCode('job_position', e.position_code).id, culture) : null,
      level: store.masterRef(e.level_id, culture),
      department: department ? { id: department.id, name: department.name } : null,
      team: team ? { id: team.id, name: team.name } : null,
      employmentStatus: status ? store.masterRef(status.id, culture) : null,
      joinedDate: e.joined_date, leftDate: e.left_date ?? null, avatarUrl: e.avatar_url ?? null, skills: e.skills ?? [],
      projects, totalFte, allocationPercent, allocationStatus: allocationStatus(allocationPercent, isWorking(e), hrRules()),
      account: user ? { status: user.is_active ? 'active' : 'disabled', email: user.email, lastLoginAt: user.last_login_at ?? null } : invitation ? { status: 'invited', email: invitation.email, lastLoginAt: null } : null,
      isDeleted: Boolean(e.is_deleted), createdAt: e.created_at,
    };
  }

  function searchEmployees(url, culture) {
    const search = url.searchParams.get('search') || '';
    const positionCodes = listParam(url, 'positions');
    const levelIds = listParam(url, 'levelIds');
    const departmentIds = listParam(url, 'departmentIds');
    const teamIds = listParam(url, 'teamIds');
    const allocation = listParam(url, 'allocation');
    const statusCodes = listParam(url, 'statuses');
    const deleted = url.searchParams.get('deleted') === 'true';
    return db.employees
      .filter((e) => Boolean(e.is_deleted) === deleted)
      .filter((e) => matches(search, e.full_name, e.email, e.code))
      .filter((e) => positionCodes.length === 0 || positionCodes.includes(e.position_code))
      .filter((e) => levelIds.length === 0 || levelIds.includes(e.level_id))
      .filter((e) => departmentIds.length === 0 || departmentIds.includes(e.department_id))
      .filter((e) => teamIds.length === 0 || teamIds.includes(e.team_id))
      .filter((e) => statusCodes.length === 0 || statusCodes.includes(e.status))
      .sort((a, b) => a.code.localeCompare(b.code))
      .map((e) => employeeDto(e, culture))
      .filter((dto) => allocation.length === 0 || allocation.includes(dto.allocationStatus));
  }

  function validateEmployee(body, current) {
    const errors = {};
    const fullName = str(body.fullName);
    const email = str(body.email).toLowerCase();
    const code = str(body.code).toUpperCase();
    if (required(errors, 'fullName', fullName)) maxLength(errors, 'fullName', fullName, 150);
    validEmail(errors, 'email', email);
    if (email && db.employees.some((e) => e.email.toLowerCase() === email && e.id !== current?.id)) addError(errors, 'email', 'employee_save.err_duplicate_email');
    if (required(errors, 'code', code)) {
      if (!/^[A-Z0-9_-]{1,20}$/.test(code)) addError(errors, 'code', 'employee_save.err_code_format');
      else if (db.employees.some((e) => e.code === code && e.id !== current?.id)) addError(errors, 'code', 'employee_save.err_duplicate_code');
    }
    const phone = str(body.phone);
    if (phone && !/^[0-9+\-\s]{6,20}$/.test(phone)) addError(errors, 'phone', 'validation.invalid_value');
    const positionCode = str(body.positionCode);
    if (required(errors, 'positionCode', positionCode) && !masterByCode('job_position', positionCode)) addError(errors, 'positionCode', 'validation.invalid_value');
    const levelId = str(body.levelId);
    if (levelId && store.masterById.get(levelId)?.type !== 'hr_level') addError(errors, 'levelId', 'validation.invalid_value');
    const statusCode = str(body.statusCode) || 'ACTIVE';
    if (!masterByCode('employee_status', statusCode)) addError(errors, 'statusCode', 'validation.invalid_value');
    const departmentId = str(body.departmentId) || null;
    const teamId = str(body.teamId) || null;
    if (departmentId && !db.departments.some((d) => d.id === departmentId)) addError(errors, 'departmentId', 'validation.invalid_value');
    if (teamId) {
      const team = db.teams.find((t) => t.id === teamId);
      if (!team) addError(errors, 'teamId', 'validation.invalid_value');
      else if (departmentId && team.department_id !== departmentId) addError(errors, 'teamId', 'employee_save.err_team_department');
    }
    const joinedDate = validDate(errors, 'joinedDate', str(body.joinedDate));
    const leftDate = validDate(errors, 'leftDate', str(body.leftDate), { optional: true });
    if (joinedDate && leftDate && leftDate < joinedDate) addError(errors, 'leftDate', 'employee_save.err_left_before_joined');
    const skills = Array.isArray(body.skills) ? body.skills.map(str).filter(Boolean).slice(0, 30) : [];
    throwIfErrors(errors);
    return {
      full_name: fullName, email, code, phone: phone || null, position_code: positionCode, position: masterByCode('job_position', positionCode).name,
      level_id: levelId || null, level_code: levelId ? store.masterById.get(levelId).code : null, department_id: departmentId, team_id: teamId,
      status: statusCode, is_active: statusCode !== 'RESIGNED', joined_date: joinedDate, left_date: leftDate, skills,
    };
  }

  const findEmployee = (url, index = -1) => {
    const row = db.employees.find((e) => e.id === idFromPath(url, index));
    if (!row) throw notFound();
    return row;
  };

  return [
    // Khách hàng — GET dùng chung cho bộ lọc ở các màn khác (đăng nhập là đọc được)
    ['GET', /^\/api\/v1\/clients$/, async ({ req, url, culture }) => {
      currentUser(req);
      const page = paginate(searchClients(url), url, 20);
      return { data: { ...page, items: page.items.map((c) => clientDto(c, culture)) } };
    }],
    ['GET', /^\/api\/v1\/clients\/summary$/, async ({ req, url }) => {
      currentUser(req);
      const rows = searchClients(url);
      return { data: { total: rows.length, active: rows.filter((c) => c.is_active).length, inactive: rows.filter((c) => !c.is_active).length, totalProjects: rows.reduce((s, c) => s + projectsOfClient(c.id), 0) } };
    }],
    ['POST', /^\/api\/v1\/clients$/, async ({ req, culture }) => {
      settingsAdmin(req);
      const row = { id: randomUUID(), tenant_id: tenantId(), ...validateClient(await readJson(req), null), is_deleted: false, created_at: nowIso() };
      db.clients.push(row);
      return { status: 201, key: 'client_create.msg_success', data: clientDto(row, culture) };
    }],
    ['PUT', /^\/api\/v1\/clients\/[^/]+$/, async ({ req, url, culture }) => {
      settingsAdmin(req);
      const row = findClient(url);
      Object.assign(row, validateClient(await readJson(req), row));
      return { key: 'client_update.msg_success', data: clientDto(row, culture) };
    }],
    ['DELETE', /^\/api\/v1\/clients\/[^/]+$/, async ({ req, url }) => {
      settingsAdmin(req);
      const row = findClient(url);
      // Lần 1: chuyển vào thùng rác. Đã ở thùng rác: xoá hẳn (chặn nếu còn dự án tham chiếu)
      if (!row.is_deleted) {
        row.is_deleted = true;
        return { key: 'client_trash.msg_success', args: { name: row.name } };
      }
      if (db.projects.some((p) => p.client_id === row.id)) throw new ApiError(409, 'client_delete.err_in_use', null, { name: row.name });
      db.clients.splice(db.clients.indexOf(row), 1);
      return { key: 'client_delete.msg_success', args: { name: row.name } };
    }],
    ['POST', /^\/api\/v1\/clients\/[^/]+\/restore$/, async ({ req, url, culture }) => {
      settingsAdmin(req);
      const row = findClient(url, -2);
      row.is_deleted = false;
      return { key: 'client_restore.msg_success', args: { name: row.name }, data: clientDto(row, culture) };
    }],

    // Phòng ban & Team
    ['GET', /^\/api\/v1\/departments$/, async ({ req }) => {
      currentUser(req);
      return { data: db.departments.filter((d) => !d.is_deleted).sort((a, b) => a.sort_seq - b.sort_seq).map(departmentDto) };
    }],
    ['POST', /^\/api\/v1\/departments$/, async ({ req }) => {
      employeeManager(req);
      const body = await readJson(req);
      const values = validateUnit(body, db.departments, null, (e) => validEmployeeId(e, 'managerId', str(body.managerId)));
      const row = { id: randomUUID(), tenant_id: tenantId(), ...values, manager_employee_id: str(body.managerId) || null, sort_seq: db.departments.length + 1, is_deleted: false, created_at: nowIso() };
      db.departments.push(row);
      return { status: 201, key: 'department_create.msg_success', data: departmentDto(row) };
    }],
    ['PUT', /^\/api\/v1\/departments\/[^/]+$/, async ({ req, url }) => {
      employeeManager(req);
      const row = db.departments.find((d) => d.id === idFromPath(url) && !d.is_deleted);
      if (!row) throw notFound();
      const body = await readJson(req);
      Object.assign(row, validateUnit(body, db.departments, row, (e) => validEmployeeId(e, 'managerId', str(body.managerId))), { manager_employee_id: str(body.managerId) || null });
      return { key: 'department_update.msg_success', data: departmentDto(row) };
    }],
    ['DELETE', /^\/api\/v1\/departments\/[^/]+$/, async ({ req, url }) => {
      employeeManager(req);
      const row = db.departments.find((d) => d.id === idFromPath(url) && !d.is_deleted);
      if (!row) throw notFound();
      if (activeEmployees().some((e) => e.department_id === row.id) || db.teams.some((t) => t.department_id === row.id && !t.is_deleted)) {
        throw new ApiError(409, 'department_delete.err_not_empty', null, { name: row.name });
      }
      row.is_deleted = true;
      return { key: 'department_delete.msg_success', args: { name: row.name } };
    }],
    ['POST', /^\/api\/v1\/teams$/, async ({ req }) => {
      employeeManager(req);
      const body = await readJson(req);
      const departmentId = str(body.departmentId);
      const values = validateUnit(body, db.teams, null, (e) => {
        if (!db.departments.some((d) => d.id === departmentId && !d.is_deleted)) addError(e, 'departmentId', 'validation.required');
        validEmployeeId(e, 'leaderId', str(body.leaderId));
      });
      const row = { id: randomUUID(), tenant_id: tenantId(), department_id: departmentId, ...values, leader_employee_id: str(body.leaderId) || null, sort_seq: db.teams.filter((t) => t.department_id === departmentId).length + 1, is_deleted: false, created_at: nowIso() };
      db.teams.push(row);
      return { status: 201, key: 'team_create.msg_success', data: teamDto(row) };
    }],
    ['PUT', /^\/api\/v1\/teams\/[^/]+$/, async ({ req, url }) => {
      employeeManager(req);
      const row = db.teams.find((t) => t.id === idFromPath(url) && !t.is_deleted);
      if (!row) throw notFound();
      const body = await readJson(req);
      const departmentId = str(body.departmentId) || row.department_id;
      Object.assign(row, validateUnit(body, db.teams, row, (e) => {
        if (!db.departments.some((d) => d.id === departmentId && !d.is_deleted)) addError(e, 'departmentId', 'validation.invalid_value');
        validEmployeeId(e, 'leaderId', str(body.leaderId));
      }), { department_id: departmentId, leader_employee_id: str(body.leaderId) || null });
      // Đổi phòng ban của team → nhân sự trong team theo sang phòng ban mới
      for (const e of db.employees) if (e.team_id === row.id) e.department_id = departmentId;
      return { key: 'team_update.msg_success', data: teamDto(row) };
    }],
    ['DELETE', /^\/api\/v1\/teams\/[^/]+$/, async ({ req, url }) => {
      employeeManager(req);
      const row = db.teams.find((t) => t.id === idFromPath(url) && !t.is_deleted);
      if (!row) throw notFound();
      if (activeEmployees().some((e) => e.team_id === row.id)) throw new ApiError(409, 'team_delete.err_not_empty', null, { name: row.name });
      row.is_deleted = true;
      return { key: 'team_delete.msg_success', args: { name: row.name } };
    }],

    // Nhân sự
    ['GET', /^\/api\/v1\/employees$/, async ({ req, url, culture }) => {
      employeeViewer(req);
      return { data: paginate(searchEmployees(url, culture), url, 20) };
    }],
    ['GET', /^\/api\/v1\/employees\/summary$/, async ({ req, url, culture }) => {
      employeeViewer(req);
      const rows = searchEmployees(url, culture);
      const working = rows.filter((r) => r.allocationStatus !== 'resigned');
      // Tỷ lệ sử dụng = tổng FTE đang gán / số nhân sự đang làm việc (trần 100% mỗi người), so với mục tiêu HR_UTILIZATION_TARGET_PERCENT
      const used = working.reduce((s, r) => s + Math.min(1, r.totalFte), 0);
      return {
        data: {
          total: rows.length,
          inProjects: rows.filter((r) => r.projects.length > 0).length,
          bench: rows.filter((r) => r.allocationStatus === 'bench').length,
          overloaded: rows.filter((r) => r.allocationStatus === 'overloaded').length,
          utilizationPercent: working.length ? Math.round((used / working.length) * 1000) / 10 : 0,
          utilizationTarget: store.ruleValue('HR_UTILIZATION_TARGET_PERCENT', 85),
        },
      };
    }],
    ['GET', /^\/api\/v1\/employees\/[^/]+$/, async ({ req, url, culture }) => {
      employeeViewer(req);
      const row = findEmployee(url);
      const balance = db.leave_balances?.find((b) => b.employee_id === row.id && b.year === Number(today().slice(0, 4)));
      const dto = employeeDto(row, culture);
      return { data: { ...dto, leaveBalance: balance ? { year: balance.year, entitledDays: balance.entitled_days, carriedOverDays: balance.carried_over_days, adjustedDays: balance.adjusted_days } : null } };
    }],
    ['POST', /^\/api\/v1\/employees$/, async ({ req, culture }) => {
      employeeManager(req);
      const row = { id: randomUUID(), tenant_id: tenantId(), user_id: null, ...validateEmployee(await readJson(req), null), avatar_url: null, is_deleted: false, created_at: nowIso() };
      db.employees.push(row);
      return { status: 201, key: 'employee_create.msg_success', data: employeeDto(row, culture) };
    }],
    ['PUT', /^\/api\/v1\/employees\/[^/]+$/, async ({ req, url, culture }) => {
      employeeManager(req);
      const row = findEmployee(url);
      Object.assign(row, validateEmployee(await readJson(req), row));
      return { key: 'employee_update.msg_success', data: employeeDto(row, culture) };
    }],
    ['DELETE', /^\/api\/v1\/employees\/[^/]+$/, async ({ req, url }) => {
      employeeManager(req);
      const row = findEmployee(url);
      if (activeAssignments(row.id).length > 0) throw new ApiError(409, 'employee_delete.err_has_assignments', null, { name: row.full_name });
      row.is_deleted = true;
      return { key: 'employee_delete.msg_success', args: { name: row.full_name } };
    }],
    ['POST', /^\/api\/v1\/employees\/[^/]+\/restore$/, async ({ req, url, culture }) => {
      employeeManager(req);
      const row = findEmployee(url, -2);
      row.is_deleted = false;
      return { key: 'employee_restore.msg_success', args: { name: row.full_name }, data: employeeDto(row, culture) };
    }],
    // Mời tạo tài khoản đăng nhập (D4): gửi email chứa link đặt mật khẩu /auth/reset-password/<token>
    ['POST', /^\/api\/v1\/employees\/[^/]+\/invite$/, async ({ req, url, culture }) => {
      const ctx = employeeManager(req);
      const row = findEmployee(url, -2);
      if (row.user_id) throw new ApiError(409, 'employee_invite.err_has_account');
      db.invitations ??= [];
      const hours = Number(db.system_settings.find((s) => s.key === 'invitation_expiry_hours')?.value || 72);
      const token = randomUUID().replace(/-/g, '');
      db.invitations = db.invitations.filter((i) => i.employee_id !== row.id);
      db.invitations.push({ id: randomUUID(), employee_id: row.id, email: row.email, token, invited_by: ctx.user.id, expires_at: new Date(Date.now() + hours * 3600_000).toISOString(), accepted_at: null });
      store.sendMail?.({ to: row.email, subject: 'invitation', link: `/auth/reset-password/${token}` });
      return { key: 'employee_invite.msg_success', args: { email: row.email }, data: employeeDto(row, culture) };
    }],
  ];
}
