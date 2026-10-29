// Endpoint lưới Công số (màn effort): mỗi dòng = 1 nhân sự × 1 dự án × 1 vai trò × 1 tháng.
// Phòng ban của dòng = phòng ban "trung tâm" của dự án (effort_project_centers) — một dự án chỉ thuộc một phòng ban.
// Trường cấp nhân sự (mức hiệu suất / hệ số Performance) đồng bộ cho mọi dòng của người đó trong tháng (port syncMemberLevelFields).
// Kế hoạch (hd) và Thực tế (tt_effort) là số của từng dòng; sửa Kế hoạch mà không gửi kèm Thực tế → Thực tế = Kế hoạch.
// OT effort và mức hiệu suất khoá trên lưới (đồng bộ từ hệ thống OT / hồ sơ nhân sự) nhưng API vẫn nhận để job đồng bộ ghi vào.
// Actual / Actual1 / Free / Free1 tính ở FE theo frontend/src/modules/effort/utils/effort-calculations.ts (công thức cố định, D12).
import { randomUUID } from 'node:crypto';
import { ApiError, forbidden, notFound, readJson } from './http.mjs';
import { today } from './store.mjs';
import { addError, idFromPath, maxLength, numberInRange, str, throwIfErrors } from './validate.mjs';

/**
 * @param {{ store: ReturnType<import('./store.mjs').createStore>, currentUser: Function, requirePermission: Function }} deps
 */
export function effortRoutes({ store, currentUser, requirePermission }) {
  const { db } = store;
  const monthOf = (url) => {
    const m = url.searchParams.get('month') || today().slice(0, 7);
    if (!/^\d{4}-\d{2}$/.test(m)) throw new ApiError(400, 'error.validation_failed', { month: ['validation.invalid_value'] });
    return `${m}-01`;
  };
  const centerOf = (projectId) => db.effort_project_centers.find((c) => c.project_id === projectId);
  const coefficientOf = (levelId) => Number(store.masterById.get(levelId)?.metadata?.coefficient ?? 1);

  function rowDto(r, culture) {
    const e = db.employees.find((x) => x.id === r.employee_id);
    const p = db.projects.find((x) => x.id === r.project_id);
    const center = centerOf(r.project_id);
    const dept = center ? db.departments.find((d) => d.id === center.department_id) : null;
    const team = db.teams.find((x) => x.id === r.team_id);
    const homeTeam = e ? db.teams.find((x) => x.id === e.team_id) : null;
    return {
      id: r.id, month: r.year_month.slice(0, 7),
      employee: e ? { id: e.id, code: e.code, fullName: e.full_name, email: e.email, avatarUrl: e.avatar_url ?? null } : null,
      teamId: r.team_id, teamName: team?.name ?? null, homeTeamName: homeTeam?.name ?? null,
      departmentId: dept?.id ?? null, departmentName: dept?.name ?? null,
      projectId: r.project_id, projectName: p?.name ?? '—', role: r.role, note: r.note ?? null,
      hd: r.hd, ttEffort: r.tt_effort, otEffort: r.ot_effort,
      performanceLevel: store.masterRef(r.performance_level_id, culture), performance: r.performance, allocateRate: r.allocate_rate,
    };
  }
  const viewer = (req) => {
    const ctx = currentUser(req);
    requirePermission(ctx, 'effort.view');
    return ctx;
  };
  const editor = (req) => {
    const ctx = currentUser(req);
    requirePermission(ctx, 'effort.edit');
    return ctx;
  };

  return [
    ['GET', /^\/api\/v1\/effort$/, async ({ req, url, culture }) => {
      const ctx = viewer(req);
      const month = monthOf(url);
      const rows = db.effort_allocations.filter((r) => r.year_month === month && !r.is_deleted);
      return {
        data: {
          month: month.slice(0, 7), canEdit: ctx.permissions.includes('effort.edit'),
          rows: rows.map((r) => rowDto(r, culture)).sort((a, b) => (a.employee?.fullName ?? '').localeCompare(b.employee?.fullName ?? '', 'vi') || a.projectName.localeCompare(b.projectName, 'vi')),
          departments: db.departments.filter((d) => !d.is_deleted).sort((a, b) => a.sort_seq - b.sort_seq).map((d) => ({ id: d.id, name: d.name })),
          teams: db.teams.filter((t) => !t.is_deleted).map((t) => ({ id: t.id, name: t.name, departmentId: t.department_id })),
          projects: db.projects.filter((p) => !p.is_deleted).map((p) => ({ id: p.id, name: p.name, departmentId: centerOf(p.id)?.department_id ?? null })),
          roles: db.master_data.filter((m) => m.type === 'job_position' && m.is_active).sort((a, b) => a.sort_seq - b.sort_seq).map((m) => m.name),
          performanceLevels: db.master_data.filter((m) => m.type === 'performance_level' && m.is_active).sort((a, b) => a.sort_seq - b.sort_seq).map((m) => ({ ...store.masterRef(m.id, culture), coefficient: Number(m.metadata?.coefficient ?? 0) })),
        },
      };
    }],
    // Sửa một trường của dòng (lưu ngay khi rời ô — port useEffortRowAutosave)
    ['PATCH', /^\/api\/v1\/effort\/[^/]+$/, async ({ req, url, culture }) => {
      editor(req);
      const row = db.effort_allocations.find((r) => r.id === idFromPath(url) && !r.is_deleted);
      if (!row) throw notFound();
      const body = await readJson(req);
      const errors = {};
      const patch = {};
      for (const [field, value] of Object.entries(body)) {
        if (['hd', 'ttEffort', 'otEffort'].includes(field)) patch[{ hd: 'hd', ttEffort: 'tt_effort', otEffort: 'ot_effort' }[field]] = numberInRange(errors, field, value, 0, 2);
        else if (field === 'allocateRate') patch.allocate_rate = numberInRange(errors, field, value, 0, 100, { integer: true });
        else if (field === 'performanceLevelId') {
          const m = store.masterById.get(str(value));
          if (!m || m.type !== 'performance_level') addError(errors, field, 'validation.invalid_value');
          else Object.assign(patch, { performance_level_id: m.id, performance: coefficientOf(m.id) });
        } else if (field === 'teamId') {
          if (!db.teams.some((t) => t.id === str(value))) addError(errors, field, 'validation.invalid_value');
          else patch.team_id = str(value);
        } else if (field === 'role') {
          if (!str(value)) addError(errors, field, 'validation.required');
          else patch.role = str(value).slice(0, 50);
        } else if (field === 'note') {
          maxLength(errors, field, str(value), 500);
          patch.note = str(value) || null;
        } else if (field === 'projectId') {
          const projectId = str(value);
          const departmentId = str(body.departmentId) || centerOf(row.project_id)?.department_id;
          const center = centerOf(projectId);
          if (!db.projects.some((p) => p.id === projectId && !p.is_deleted)) addError(errors, field, 'validation.invalid_value');
          // Một dự án chỉ thuộc một phòng ban (port PROJECT_ALREADY_MAPPED)
          else if (center && center.department_id !== departmentId) addError(errors, field, 'effort_update.err_project_mapped');
          else {
            if (!center && departmentId) db.effort_project_centers.push({ id: randomUUID(), tenant_id: db.tenants[0].id, project_id: projectId, department_id: departmentId });
            patch.project_id = projectId;
          }
        } else if (field !== 'departmentId') addError(errors, field, 'validation.invalid_value');
      }
      if (patch.project_id || patch.role) {
        const projectId = patch.project_id ?? row.project_id;
        const role = patch.role ?? row.role;
        if (db.effort_allocations.some((r) => r.id !== row.id && !r.is_deleted && r.employee_id === row.employee_id && r.year_month === row.year_month && r.project_id === projectId && r.role === role)) addError(errors, 'role', 'effort_add.err_duplicate');
      }
      // Nhập Kế hoạch → Thực tế tự bằng Kế hoạch (PMO sửa Thực tế riêng khi trong tháng có thay đổi)
      if ('hd' in patch && !('ttEffort' in body)) patch.tt_effort = patch.hd;
      throwIfErrors(errors);
      const oldProject = row.project_id;
      Object.assign(row, patch);
      // Trường cấp nhân sự đồng bộ sang mọi dòng của người đó trong tháng
      const memberPatch = Object.fromEntries(Object.entries(patch).filter(([k]) => ['performance', 'performance_level_id'].includes(k)));
      const siblings = db.effort_allocations.filter((r) => r.employee_id === row.employee_id && r.year_month === row.year_month && !r.is_deleted);
      if (Object.keys(memberPatch).length) for (const s of siblings) Object.assign(s, memberPatch);
      // Dự án cũ không còn dòng nào trong mọi tháng → bỏ ánh xạ phòng ban
      if (patch.project_id && oldProject !== patch.project_id && !db.effort_allocations.some((r) => r.project_id === oldProject && !r.is_deleted)) {
        db.effort_project_centers = db.effort_project_centers.filter((c) => c.project_id !== oldProject);
      }
      const e = db.employees.find((x) => x.id === row.employee_id);
      return { key: 'effort_update.msg_success', args: { name: e?.full_name ?? '' }, data: siblings.map((r) => rowDto(r, culture)) };
    }],
    ['POST', /^\/api\/v1\/effort$/, async ({ req, culture }) => {
      editor(req);
      const body = await readJson(req);
      const errors = {};
      const month = /^\d{4}-\d{2}$/.test(str(body.month)) ? `${str(body.month)}-01` : null;
      if (!month) addError(errors, 'month', 'validation.invalid_value');
      const employee = db.employees.find((e) => e.id === str(body.employeeId) && !e.is_deleted && e.is_active);
      if (!employee) addError(errors, 'employeeId', 'validation.required');
      const departmentId = str(body.departmentId);
      if (!db.departments.some((d) => d.id === departmentId)) addError(errors, 'departmentId', 'validation.required');
      const projectId = str(body.projectId);
      if (!db.projects.some((p) => p.id === projectId && !p.is_deleted)) addError(errors, 'projectId', 'validation.required');
      const center = centerOf(projectId);
      if (center && center.department_id !== departmentId) addError(errors, 'projectId', 'effort_update.err_project_mapped');
      const teamId = str(body.teamId);
      if (!db.teams.some((t) => t.id === teamId)) addError(errors, 'teamId', 'validation.required');
      const role = str(body.role);
      if (!role) addError(errors, 'role', 'validation.required');
      if (month && employee && db.effort_allocations.some((r) => !r.is_deleted && r.employee_id === employee.id && r.year_month === month && r.project_id === projectId && r.role === role)) addError(errors, 'role', 'effort_add.err_duplicate');
      throwIfErrors(errors);
      if (!center) db.effort_project_centers.push({ id: randomUUID(), tenant_id: db.tenants[0].id, project_id: projectId, department_id: departmentId });
      // Người đã có dòng trong tháng → kế thừa mức hiệu suất (trường cấp nhân sự); Kế hoạch / Thực tế của dòng mới bắt đầu từ 0
      const sibling = db.effort_allocations.find((r) => r.employee_id === employee.id && r.year_month === month && !r.is_deleted);
      // Mặc định mức "CT" (Chính thức); tenant không có mã này → mức đầu tiên của danh mục
      const full = db.master_data.find((m) => m.type === 'performance_level' && m.code === 'CT') ?? db.master_data.find((m) => m.type === 'performance_level');
      const row = {
        id: randomUUID(), tenant_id: db.tenants[0].id, employee_id: employee.id, project_id: projectId, year_month: month, team_id: teamId, role, note: null,
        hd: 0, tt_effort: 0, ot_effort: 0, performance: sibling?.performance ?? coefficientOf(full?.id), performance_level_id: sibling?.performance_level_id ?? full?.id ?? null, allocate_rate: 100,
      };
      db.effort_allocations.push(row);
      return { status: 201, key: 'effort_add.msg_success', args: { name: employee.full_name }, data: rowDto(row, culture) };
    }],
    ['DELETE', /^\/api\/v1\/effort\/[^/]+$/, async ({ req, url }) => {
      const ctx = editor(req);
      const row = db.effort_allocations.find((r) => r.id === idFromPath(url) && !r.is_deleted);
      if (!row) throw notFound();
      if (!ctx.permissions.includes('effort.edit')) throw forbidden();
      row.is_deleted = true;
      const e = db.employees.find((x) => x.id === row.employee_id);
      return { key: 'effort_delete.msg_success', args: { name: e?.full_name ?? '' } };
    }],
  ];
}
