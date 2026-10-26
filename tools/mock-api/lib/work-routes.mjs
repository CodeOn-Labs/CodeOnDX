// Endpoint làm việc hằng ngày (D4): Nhật ký công việc (timesheet ngày/tuần/duyệt), Nghỉ phép & OT, Chấm công vào/ra.
// Luật khoá ngày / giới hạn giờ port từ shared/lib/rulesEngine.ts của bản gốc, chạy ở BE (FE chỉ hiển thị kết quả).
// Đơn nghỉ / OT đã duyệt tự sinh dòng công (task LEAVE / OT, status approved, source leave_request / ot_request).
import { randomUUID } from 'node:crypto';
import { ApiError, forbidden, listParam, notFound, paginate, readJson } from './http.mjs';
import { today } from './store.mjs';
import { addError, DATE_PATTERN, idFromPath, matches, maxLength, nowIso, str, throwIfErrors, TIME_PATTERN, validDate } from './validate.mjs';

// ─── Tiện ích ngày giờ ─────────────────────────────────────────────────────

const addDays = (iso, n) => {
  const d = new Date(`${iso}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
};
const dayOfWeek = (iso) => new Date(`${iso}T00:00:00Z`).getUTCDay();
const isWeekend = (iso) => [0, 6].includes(dayOfWeek(iso));
const toMinutes = (hhmm) => {
  const [h, m] = hhmm.split(':').map(Number);
  return h * 60 + m;
};
const fromMinutes = (min) => `${String(Math.floor(min / 60)).padStart(2, '0')}:${String(min % 60).padStart(2, '0')}`;
const dateRange = (from, to) => {
  const out = [];
  for (let d = from; d <= to && out.length < 400; d = addDays(d, 1)) out.push(d);
  return out;
};
const round2 = (n) => Math.round(n * 100) / 100;
/** Giờ hiện tại theo giờ Việt Nam (HH:mm). */
const nowTime = () => new Date(Date.now() + 7 * 3600_000).toISOString().slice(11, 16);

/**
 * @param {{ store: ReturnType<import('./store.mjs').createStore>, currentUser: Function, requirePermission: Function }} deps
 */
export function workRoutes({ store, currentUser, requirePermission }) {
  const { db } = store;
  const tenantId = () => db.tenants[0].id;
  const setting = (key, fallback) => db.system_settings.find((s) => s.key === key)?.value ?? fallback;
  const rule = (key, fallback) => store.ruleValue(key, fallback);
  const ruleOn = (key) => db.system_rules.find((r) => r.key === key)?.status !== 0;
  const masterByCode = (type, code) => db.master_data.find((m) => m.type === type && m.code === code);
  const holidayOn = (iso) => db.holidays.find((h) => h.holiday_date === iso) ?? null;
  const employeeById = (id) => db.employees.find((e) => e.id === id);
  const employeeRef = (e) => (e ? { id: e.id, code: e.code, fullName: e.full_name, email: e.email, avatarUrl: e.avatar_url ?? null } : null);
  const has = (ctx, p) => ctx.permissions.includes(p);

  /** Nhân sự người dùng hiện tại được xem: chính mình; all_view → tất cả; team/approve → team mình lead, phòng mình quản lý, dự án mình làm PM. */
  function visibleEmployeeIds(ctx) {
    const me = ctx.user.employee_id;
    const active = db.employees.filter((e) => !e.is_deleted);
    if (has(ctx, 'timesheet.all_view') || has(ctx, 'timesheet.all_edit')) return new Set(active.map((e) => e.id));
    const ids = new Set(me ? [me] : []);
    if (has(ctx, 'timesheet.team') || has(ctx, 'timesheet.approve')) {
      const myTeams = new Set(db.teams.filter((t) => t.leader_employee_id === me).map((t) => t.id));
      const myDepts = new Set(db.departments.filter((d) => d.manager_employee_id === me).map((d) => d.id));
      const pmProjects = new Set(db.project_members.filter((m) => m.employee_id === me && m.role === 'PM').map((m) => m.project_id));
      for (const e of active) {
        if (myTeams.has(e.team_id) || myDepts.has(e.department_id)) ids.add(e.id);
      }
      for (const m of db.project_members) if (pmProjects.has(m.project_id)) ids.add(m.employee_id);
    }
    return ids;
  }
  function assertCanView(ctx, employeeId) {
    if (!visibleEmployeeIds(ctx).has(employeeId)) throw forbidden();
  }
  function canEditTimesheetOf(ctx, employeeId) {
    if (employeeId === ctx.user.employee_id) return has(ctx, 'timesheet.edit') || has(ctx, 'timesheet.all_edit');
    return has(ctx, 'timesheet.all_edit');
  }
  function myEmployee(ctx) {
    const e = ctx.user.employee_id ? employeeById(ctx.user.employee_id) : null;
    if (!e) throw new ApiError(409, 'work_common.err_no_employee');
    return e;
  }

  // ─── Khoá ngày công (port getUIState) ─────────────────────────────────────

  /** Ngày bị khoá sửa công? Trả { locked, reason, args } — reason là key i18n FE: timesheet.lbl_lock_<reason>. */
  function dayLock(iso) {
    const now = today();
    const diffDays = Math.round((Date.parse(`${now}T00:00:00Z`) - Date.parse(`${iso}T00:00:00Z`)) / 86_400_000);
    if (ruleOn('TIMESHEET_EDIT_LIMIT_DAYS')) {
      const max = rule('TIMESHEET_EDIT_LIMIT_DAYS', 14);
      if (diffDays > max) return { locked: true, reason: 'edit_limit', args: { days: max } };
    }
    if (ruleOn('TIMESHEET_LOCK_DATE_OF_MONTH')) {
      const lockDay = rule('TIMESHEET_LOCK_DATE_OF_MONTH', 5);
      const [ty, tm, td] = now.split('-').map(Number);
      const [y, m] = iso.split('-').map(Number);
      const diffMonths = ty * 12 + tm - (y * 12 + m);
      if (diffMonths > 1 || (diffMonths === 1 && td > lockDay)) return { locked: true, reason: 'month_locked', args: { day: lockDay } };
    }
    const allowFuture = db.system_rules.find((r) => r.key === 'TIMESHEET_ALLOW_FUTURE');
    const futureOk = allowFuture && allowFuture.status !== 0 && (allowFuture.value ?? allowFuture.default_value) === true;
    if (iso > now && !futureOk) return { locked: true, reason: 'future', args: {} };
    return { locked: false, reason: null, args: {} };
  }

  // ─── Nhật ký công việc ────────────────────────────────────────────────────

  function entryDto(t, culture) {
    const project = t.project_id ? db.projects.find((p) => p.id === t.project_id) : null;
    const task = store.masterById.get(t.task_type_id);
    return {
      id: t.id, date: t.work_date, projectId: t.project_id, projectName: project?.name ?? null,
      taskType: task ? { id: task.id, code: task.code, name: store.localized(task, culture) } : null,
      hours: t.hours, description: t.description ?? null, status: t.status, source: t.source, rejectionReason: t.rejection_reason ?? null,
      employeeId: t.employee_id,
    };
  }
  const entriesOf = (employeeIds, from, to) =>
    db.timesheets.filter((t) => employeeIds.includes(t.employee_id) && t.work_date >= from && t.work_date <= to)
      .filter((t) => !t.project_id || !db.projects.find((p) => p.id === t.project_id)?.is_deleted);

  /** Dự án nhân sự được phân công (kèm khoảng ngày còn hiệu lực) — dòng 0h mặc định + kiểm tra ghi công đúng ngày. */
  function assignedProjects(employeeId, from, to, culture) {
    const map = new Map();
    for (const a of db.assignments.filter((x) => x.employee_id === employeeId && !x.is_deleted && x.start_date <= to && x.end_date >= from)) {
      const p = db.projects.find((x) => x.id === a.project_id && !x.is_deleted);
      if (!p) continue;
      const item = map.get(p.id) ?? { id: p.id, code: p.code, name: p.name, status: store.masterRef(p.status_id, culture), ranges: [] };
      item.ranges.push({ start: a.start_date, end: a.end_date });
      map.set(p.id, item);
    }
    return [...map.values()].sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  }
  const timesheetRules = () => ({
    minHours: ruleOn('TIMESHEET_MIN_HOURS') ? rule('TIMESHEET_MIN_HOURS', 0) : 0,
    maxHours: ruleOn('TIMESHEET_MAX_HOURS') ? rule('TIMESHEET_MAX_HOURS', 24) : 24,
    weeklyMaxHours: ruleOn('TIMESHEET_WEEKLY_MAX_HOURS') ? rule('TIMESHEET_WEEKLY_MAX_HOURS', 60) : null,
    fteHours: rule('TIMESHEET_FTE_HOURS', 8),
  });
  function readRange(url) {
    const errors = {};
    const from = validDate(errors, 'from', url.searchParams.get('from') || '');
    const to = validDate(errors, 'to', url.searchParams.get('to') || '');
    if (from && to && (to < from || dateRange(from, to).length > 62)) addError(errors, 'to', 'validation.invalid_value');
    throwIfErrors(errors);
    return { from, to };
  }
  const dayInfo = (iso, entries) => {
    const lock = dayLock(iso);
    const approved = entries.some((t) => t.work_date === iso && t.status === 'approved' && t.source === 'manual');
    const holiday = holidayOn(iso);
    return { date: iso, locked: lock.locked, lockReason: lock.reason, lockArgs: lock.args, approved, weekend: isWeekend(iso), holiday: holiday ? holiday.name : null };
  };

  // ─── Nghỉ phép & OT ───────────────────────────────────────────────────────

  /** Số ngày làm việc trong khoảng (bỏ cuối tuần + ngày lễ); nửa ngày = 0,5. */
  function leaveDays(from, to, durationType) {
    const days = dateRange(from, to).filter((d) => !isWeekend(d) && !holidayOn(d)).length;
    return durationType === 'full_day' ? days : days * 0.5;
  }
  /**
   * Quỹ phép năm (D12 — tham số ở Quy tắc):
   *   được hưởng = LEAVE_ANNUAL_DAYS + floor(thâm niên năm / LEAVE_SENIORITY_STEP_YEARS)
   *   còn lại    = được hưởng + phép cũ chuyển sang (≤ LEAVE_CARRY_OVER_MAX_DAYS) + điều chỉnh − đã dùng (phép năm đã duyệt)
   * Ví dụ: vào làm 2019-11-26, năm 2026 → thâm niên 6 năm → 12 + floor(6/5) = 13 ngày.
   */
  function leaveBalance(employee, year) {
    const row = db.leave_balances.find((b) => b.employee_id === employee.id && b.year === year);
    const annual = rule('LEAVE_ANNUAL_DAYS', 12);
    const step = rule('LEAVE_SENIORITY_STEP_YEARS', 5);
    const seniority = employee.joined_date ? Math.max(0, year - Number(employee.joined_date.slice(0, 4)) - (employee.joined_date.slice(5) > '12-31' ? 1 : 0)) : 0;
    const entitled = annual + (step > 0 ? Math.floor(seniority / step) : 0);
    const carried = Math.min(row?.carried_over_days ?? 0, rule('LEAVE_CARRY_OVER_MAX_DAYS', 5));
    const adjusted = row?.adjusted_days ?? 0;
    const annualType = masterByCode('leave_type', 'ANNUAL')?.id;
    const ofYear = db.leave_requests.filter((r) => r.employee_id === employee.id && r.leave_type_id === annualType && r.from_date.startsWith(`${year}-`));
    const used = ofYear.filter((r) => r.status === 'approved').reduce((s, r) => s + leaveDays(r.from_date, r.to_date, r.duration_type), 0);
    const pending = ofYear.filter((r) => r.status === 'pending').reduce((s, r) => s + leaveDays(r.from_date, r.to_date, r.duration_type), 0);
    return { year, entitledDays: entitled, carriedOverDays: carried, adjustedDays: adjusted, usedDays: used, pendingDays: pending, remainingDays: round2(entitled + carried + adjusted - used) };
  }
  /** Hệ số OT (D12 — mặc định theo luật lao động VN): ngày thường 150%, cuối tuần 200%, ngày lễ 300%. */
  function otKind(iso) {
    if (holidayOn(iso)) return { dayKind: 'holiday', ratePercent: 300 };
    if (isWeekend(iso)) return { dayKind: 'weekend', ratePercent: 200 };
    return { dayKind: 'weekday', ratePercent: 150 };
  }
  function otTotals(employeeId, iso, excludeId) {
    const approvedOrPending = db.ot_requests.filter((r) => r.employee_id === employeeId && r.id !== excludeId && ['approved', 'pending'].includes(r.status));
    const sum = (pred) => approvedOrPending.filter(pred).reduce((s, r) => s + r.hours, 0);
    return { day: sum((r) => r.work_date === iso), month: sum((r) => r.work_date.slice(0, 7) === iso.slice(0, 7)), year: sum((r) => r.work_date.slice(0, 4) === iso.slice(0, 4)) };
  }

  function requestBase(r, culture) {
    const approver = r.approver_user_id ? db.users.find((u) => u.id === r.approver_user_id) : null;
    return {
      id: r.id, code: r.code, employee: employeeRef(employeeById(r.employee_id)), reason: r.reason, status: r.status,
      approver: approver ? { id: approver.id, fullName: approver.full_name } : null, decidedAt: r.decided_at, decisionNote: r.decision_note, createdAt: r.created_at,
      _culture: culture,
    };
  }
  function leaveDto(r, culture) {
    const { _culture, ...base } = requestBase(r, culture);
    return {
      ...base, leaveType: store.masterRef(r.leave_type_id, _culture), fromDate: r.from_date, toDate: r.to_date, durationType: r.duration_type,
      days: leaveDays(r.from_date, r.to_date, r.duration_type), totalHours: r.total_hours, attachmentUrl: r.attachment_url ?? null,
    };
  }
  function otDto(r, culture) {
    const { _culture, ...base } = requestBase(r, culture);
    const project = r.project_id ? db.projects.find((p) => p.id === r.project_id) : null;
    void _culture;
    return { ...base, project: project ? { id: project.id, name: project.name } : null, workDate: r.work_date, startTime: r.start_time, endTime: r.end_time, hours: r.hours, dayKind: r.day_kind, ratePercent: r.rate_percent };
  }

  /** Đơn đã duyệt → sinh dòng công (đã duyệt, chỉ đọc). Huỷ / từ chối → xoá dòng công đã sinh. */
  function syncTimesheetLines(kind, r) {
    db.timesheets = db.timesheets.filter((t) => t.source_request_id !== r.id);
    if (r.status !== 'approved') return;
    const fte = rule('TIMESHEET_FTE_HOURS', 8);
    if (kind === 'leave') {
      const task = masterByCode('task_type', 'LEAVE');
      for (const d of dateRange(r.from_date, r.to_date).filter((x) => !isWeekend(x) && !holidayOn(x))) {
        db.timesheets.push({ id: randomUUID(), tenant_id: tenantId(), employee_id: r.employee_id, project_id: null, task_type_id: task.id, work_date: d, hours: r.duration_type === 'full_day' ? fte : fte / 2, description: r.reason, status: 'approved', source: 'leave_request', source_request_id: r.id });
      }
    } else {
      const task = masterByCode('task_type', 'OT');
      db.timesheets.push({ id: randomUUID(), tenant_id: tenantId(), employee_id: r.employee_id, project_id: r.project_id, task_type_id: task.id, work_date: r.work_date, hours: r.hours, description: r.reason, status: 'approved', source: 'ot_request', source_request_id: r.id });
    }
  }

  function listRequests(table, ctx, url, dto, culture) {
    const scope = url.searchParams.get('scope') === 'approval' ? 'approval' : 'mine';
    if (scope === 'approval') requirePermission(ctx, 'timesheet.approve');
    const statuses = listParam(url, 'statuses');
    const typeIds = listParam(url, 'leaveTypeIds');
    const search = url.searchParams.get('search') || '';
    const visible = scope === 'approval' ? visibleEmployeeIds(ctx) : new Set([ctx.user.employee_id]);
    const year = url.searchParams.get('year');
    const rows = db[table]
      .filter((r) => visible.has(r.employee_id))
      .filter((r) => scope !== 'approval' || r.employee_id !== ctx.user.employee_id || has(ctx, 'timesheet.all_edit'))
      .filter((r) => statuses.length === 0 || statuses.includes(r.status))
      .filter((r) => typeIds.length === 0 || typeIds.includes(r.leave_type_id))
      .filter((r) => !year || (r.from_date ?? r.work_date).startsWith(`${year}-`))
      .filter((r) => matches(search, r.code, employeeById(r.employee_id)?.full_name, r.reason))
      .sort((a, b) => (a.status === 'pending' ? 0 : 1) - (b.status === 'pending' ? 0 : 1) || b.created_at.localeCompare(a.created_at));
    const page = paginate(rows, url, 20);
    return { ...page, items: page.items.map((r) => dto(r, culture)) };
  }

  function decide(table, kind, action) {
    return async ({ req, url, culture }) => {
      const ctx = currentUser(req);
      const row = db[table].find((r) => r.id === idFromPath(url, -2));
      if (!row) throw notFound();
      const body = await readJson(req);
      const note = str(body.note);
      if (action === 'cancel') {
        if (row.employee_id !== ctx.user.employee_id) throw forbidden();
        if (!['pending', 'approved'].includes(row.status)) throw new ApiError(409, 'request_decide.err_not_cancellable');
        if (row.status === 'approved' && (row.from_date ?? row.work_date) < today()) throw new ApiError(409, 'request_decide.err_past_approved');
        row.status = 'cancelled';
      } else {
        requirePermission(ctx, 'timesheet.approve');
        assertCanView(ctx, row.employee_id);
        if (row.employee_id === ctx.user.employee_id && !has(ctx, 'timesheet.all_edit')) throw new ApiError(409, 'request_decide.err_self_approve');
        if (row.status !== 'pending' && !(action === 'reject' && row.status === 'approved')) throw new ApiError(409, 'request_decide.err_not_pending');
        if (action === 'reject' && !note) throw new ApiError(400, 'error.validation_failed', { note: ['request_decide.err_reason_required'] });
        Object.assign(row, { status: action === 'approve' ? 'approved' : 'rejected', approver_user_id: ctx.user.id, decided_at: nowIso(), decision_note: note || null });
      }
      syncTimesheetLines(kind, row);
      const label = { approve: 'request_approve', reject: 'request_reject', cancel: 'request_cancel' }[action];
      return { key: `${label}.msg_success`, args: { code: row.code }, data: kind === 'leave' ? leaveDto(row, culture) : otDto(row, culture) };
    };
  }
  const nextCode = (table, prefix) => `${prefix}-${String(db[table].length + 1).padStart(4, '0')}`;

  // ─── Chấm công vào/ra ─────────────────────────────────────────────────────

  const workStart = () => setting('work_start_time', '08:30');
  const graceMinutes = () => (ruleOn('ATTENDANCE_LATE_GRACE_MINUTES') ? rule('ATTENDANCE_LATE_GRACE_MINUTES', 15) : 0);
  /** Đi muộn: giờ vào > work_start_time + ATTENDANCE_LATE_GRACE_MINUTES (vd 08:30 + 15 phút → sau 08:45 là muộn). */
  const isLate = (checkIn) => !!checkIn && toMinutes(checkIn) > toMinutes(workStart()) + graceMinutes();
  const onLeave = (employeeId, iso) => db.leave_requests.find((r) => r.employee_id === employeeId && r.status === 'approved' && r.from_date <= iso && r.to_date >= iso);
  function attendanceDto(a) {
    const updater = a.updated_by ? db.users.find((u) => u.id === a.updated_by) : null;
    return {
      id: a.id, date: a.work_date, checkIn: a.check_in, checkOut: a.check_out, source: a.source, note: a.note ?? null,
      late: isLate(a.check_in), missingOut: !!a.check_in && !a.check_out && a.work_date < today(), updatedBy: updater ? updater.full_name : null,
    };
  }
  function validateTimes(errors, checkIn, checkOut) {
    if (checkIn && !TIME_PATTERN.test(checkIn)) addError(errors, 'checkIn', 'validation.invalid_time');
    if (checkOut && !TIME_PATTERN.test(checkOut)) addError(errors, 'checkOut', 'validation.invalid_time');
    if (checkIn && checkOut && TIME_PATTERN.test(checkIn) && TIME_PATTERN.test(checkOut) && checkOut <= checkIn) addError(errors, 'checkOut', 'attendance_save.err_out_before_in');
  }
  function upsertAttendance(employeeId, iso, values, userId) {
    let row = db.attendance_records.find((a) => a.employee_id === employeeId && a.work_date === iso);
    if (!row) {
      row = { id: randomUUID(), tenant_id: tenantId(), employee_id: employeeId, work_date: iso, check_in: null, check_out: null, source: values.source, note: null, updated_by: null };
      db.attendance_records.push(row);
    }
    Object.assign(row, values, { updated_by: userId ?? row.updated_by });
    return row;
  }

  return [
    // ── Nhật ký công việc ──
    ['GET', /^\/api\/v1\/timesheets\/employees$/, async ({ req }) => {
      const ctx = currentUser(req);
      const ids = visibleEmployeeIds(ctx);
      const employees = db.employees.filter((e) => ids.has(e.id) && !e.is_deleted).sort((a, b) => a.full_name.localeCompare(b.full_name, 'vi')).map(employeeRef);
      // Dự án để chọn nhóm "theo dự án" ở chế độ xem nhiều người
      const projects = db.projects.filter((p) => !p.is_deleted)
        .map((p) => ({ id: p.id, name: p.name, employeeIds: [...new Set(db.assignments.filter((a) => a.project_id === p.id && !a.is_deleted).map((a) => a.employee_id))].filter((id) => ids.has(id)) }))
        .filter((p) => p.employeeIds.length > 0);
      return {
        data: {
          myEmployeeId: ctx.user.employee_id ?? null, employees, projects,
          canViewOthers: employees.length > 1, canEditAll: has(ctx, 'timesheet.all_edit'), canApprove: has(ctx, 'timesheet.approve'),
        },
      };
    }],
    ['GET', /^\/api\/v1\/timesheets$/, async ({ req, url, culture }) => {
      const ctx = currentUser(req);
      const employeeId = url.searchParams.get('employeeId') || ctx.user.employee_id;
      if (!employeeId) throw new ApiError(409, 'work_common.err_no_employee');
      assertCanView(ctx, employeeId);
      const { from, to } = readRange(url);
      const entries = entriesOf([employeeId], from, to);
      // Tổng tuần chứa ngày `from` (để cảnh báo TIMESHEET_WEEKLY_MAX_HOURS ở màn ngày)
      const weekStart = addDays(from, -((dayOfWeek(from) + 6) % 7));
      const weekEntries = entriesOf([employeeId], weekStart, addDays(weekStart, 6));
      return {
        data: {
          employeeId, from, to, canEdit: canEditTimesheetOf(ctx, employeeId), rules: timesheetRules(),
          entries: entries.map((t) => entryDto(t, culture)),
          days: dateRange(from, to).map((d) => dayInfo(d, entries)),
          projects: assignedProjects(employeeId, from, to, culture),
          week: { start: weekStart, hoursByDate: Object.fromEntries(dateRange(weekStart, addDays(weekStart, 6)).map((d) => [d, round2(weekEntries.filter((t) => t.work_date === d).reduce((s, t) => s + t.hours, 0))])) },
          taskTypes: db.master_data.filter((m) => m.type === 'task_type' && m.is_active && !m.metadata?.system && m.code !== 'PROJECT').sort((a, b) => a.sort_seq - b.sort_seq).map((m) => ({ id: m.id, code: m.code, name: store.localized(m, culture) })),
        },
      };
    }],
    // Lưu công nhiều ngày trong 1 lần (ghi đè dòng tự nhập của các ngày gửi lên; ngày khoá / đã duyệt bị bỏ qua)
    ['PUT', /^\/api\/v1\/timesheets$/, async ({ req, culture }) => {
      const ctx = currentUser(req);
      const body = await readJson(req);
      const employeeId = str(body.employeeId) || ctx.user.employee_id;
      if (!employeeId) throw new ApiError(409, 'work_common.err_no_employee');
      if (!canEditTimesheetOf(ctx, employeeId)) throw forbidden();
      const dates = Array.isArray(body.dates) ? [...new Set(body.dates.map(str))].filter((d) => DATE_PATTERN.test(d)) : [];
      const rows = Array.isArray(body.entries) ? body.entries : [];
      if (dates.length === 0 || dates.length > 31) throw new ApiError(400, 'error.validation_failed', { dates: ['validation.invalid_value'] });
      const rules = timesheetRules();
      const projectTask = masterByCode('task_type', 'PROJECT');
      const all = entriesOf([employeeId], '0000-01-01', '9999-12-31');
      const editable = dates.filter((d) => !dayLock(d).locked && !all.some((t) => t.work_date === d && t.status === 'approved' && t.source === 'manual'));
      const errors = {};
      const clean = [];
      for (const [i, r] of rows.entries()) {
        const date = str(r.date);
        if (!dates.includes(date)) continue;
        const hours = Number(r.hours);
        if (!Number.isFinite(hours) || hours < 0 || hours > 24) { addError(errors, `entries.${i}`, 'validation.invalid_number'); continue; }
        // Bội số 15 phút (0,25h) như kéo thanh ở màn ngày
        if (Math.abs(hours * 4 - Math.round(hours * 4)) > 1e-9) { addError(errors, `entries.${i}`, 'timesheet_save.err_quarter_hour'); continue; }
        if (hours === 0) continue;
        const projectId = str(r.projectId) || null;
        let taskTypeId = str(r.taskTypeId) || null;
        if (projectId) {
          const project = assignedProjects(employeeId, date, date, culture).find((p) => p.id === projectId);
          if (!project || !project.ranges.some((rg) => rg.start <= date && rg.end >= date)) { addError(errors, date, 'timesheet_save.err_no_assignment'); continue; }
          taskTypeId = projectTask?.id ?? taskTypeId;
        } else {
          const task = taskTypeId ? store.masterById.get(taskTypeId) : null;
          if (!task || task.type !== 'task_type' || task.metadata?.system) { addError(errors, `entries.${i}`, 'validation.invalid_value'); continue; }
        }
        clean.push({ date, projectId, taskTypeId, hours, description: str(r.description).slice(0, 500) || null });
      }
      // Tổng giờ mỗi ngày (gồm cả dòng nghỉ phép / OT tự sinh) phải trong [TIMESHEET_MIN_HOURS, TIMESHEET_MAX_HOURS]
      for (const d of editable) {
        const total = clean.filter((c) => c.date === d).reduce((s, c) => s + c.hours, 0) + all.filter((t) => t.work_date === d && t.source !== 'manual').reduce((s, t) => s + t.hours, 0);
        if (total > rules.maxHours) addError(errors, d, { key: 'timesheet_save.err_max_hours', args: { hours: rules.maxHours } });
      }
      if (rules.weeklyMaxHours !== null) {
        const weekStarts = new Set(editable.map((d) => addDays(d, -((dayOfWeek(d) + 6) % 7))));
        for (const ws of weekStarts) {
          const week = dateRange(ws, addDays(ws, 6));
          const total = week.reduce((s, d) => s + (editable.includes(d) ? clean.filter((c) => c.date === d).reduce((x, c) => x + c.hours, 0) + all.filter((t) => t.work_date === d && t.source !== 'manual').reduce((x, t) => x + t.hours, 0) : all.filter((t) => t.work_date === d).reduce((x, t) => x + t.hours, 0)), 0);
          if (total > rules.weeklyMaxHours) addError(errors, ws, { key: 'timesheet_save.err_weekly_max_hours', args: { hours: rules.weeklyMaxHours, total: round2(total) } });
        }
      }
      throwIfErrors(errors);
      if (editable.length === 0) throw new ApiError(409, 'timesheet_save.err_all_locked');
      db.timesheets = db.timesheets.filter((t) => !(t.employee_id === employeeId && editable.includes(t.work_date) && t.source === 'manual'));
      for (const c of clean.filter((x) => editable.includes(x.date))) {
        db.timesheets.push({ id: randomUUID(), tenant_id: tenantId(), employee_id: employeeId, project_id: c.projectId, task_type_id: c.taskTypeId, work_date: c.date, hours: c.hours, description: c.description, status: 'draft', source: 'manual', source_request_id: null });
      }
      return { key: 'timesheet_save.msg_success', args: { days: editable.length }, data: { savedDates: editable, skippedDates: dates.filter((d) => !editable.includes(d)) } };
    }],
    // Báo cáo nhiều nhân viên (xem nhóm / duyệt)
    ['GET', /^\/api\/v1\/timesheets\/group$/, async ({ req, url, culture }) => {
      const ctx = currentUser(req);
      const visible = visibleEmployeeIds(ctx);
      const ids = listParam(url, 'employeeIds').filter((id) => visible.has(id));
      const { from, to } = readRange(url);
      return { data: { from, to, employees: ids.map((id) => employeeRef(employeeById(id))).filter(Boolean), entries: entriesOf(ids, from, to).map((t) => entryDto(t, culture)) } };
    }],
    ['POST', /^\/api\/v1\/timesheets\/approval$/, async ({ req }) => {
      const ctx = currentUser(req);
      requirePermission(ctx, 'timesheet.approve');
      const body = await readJson(req);
      const errors = {};
      const from = validDate(errors, 'from', str(body.from));
      const to = validDate(errors, 'to', str(body.to));
      const action = str(body.action);
      if (!['approve', 'reject'].includes(action)) addError(errors, 'action', 'validation.invalid_value');
      const reason = str(body.reason);
      if (action === 'reject' && !reason) addError(errors, 'reason', 'request_decide.err_reason_required');
      maxLength(errors, 'reason', reason, 500);
      throwIfErrors(errors);
      const visible = visibleEmployeeIds(ctx);
      const ids = (Array.isArray(body.employeeIds) ? body.employeeIds : []).map(str).filter((id) => visible.has(id));
      let count = 0;
      for (const t of db.timesheets) {
        if (!ids.includes(t.employee_id) || t.source !== 'manual' || t.status !== 'draft' || t.work_date < from || t.work_date > to) continue;
        t.status = action === 'approve' ? 'approved' : 'rejected';
        t.rejection_reason = action === 'reject' ? reason : null;
        t.approved_by = ctx.user.id;
        t.approved_at = nowIso();
        count += 1;
      }
      if (count === 0) return { key: 'timesheet_approval_empty.msg_success', data: { updated: 0 } };
      return { key: action === 'approve' ? 'timesheet_approve.msg_success' : 'timesheet_reject.msg_success', args: { count: ids.length }, data: { updated: count } };
    }],

    // ── Nghỉ phép ──
    ['GET', /^\/api\/v1\/leave-requests$/, async ({ req, url, culture }) => {
      const ctx = currentUser(req);
      return { data: listRequests('leave_requests', ctx, url, leaveDto, culture) };
    }],
    ['GET', /^\/api\/v1\/leave-requests\/summary$/, async ({ req, url }) => {
      const ctx = currentUser(req);
      const e = myEmployee(ctx);
      const year = Number(url.searchParams.get('year') || today().slice(0, 4));
      const ot = otTotals(e.id, today(), null);
      return {
        data: {
          balance: leaveBalance(e, year),
          ot: { monthHours: ot.month, yearHours: ot.year, maxPerDay: rule('OT_MAX_HOURS_PER_DAY', 4), maxPerMonth: rule('OT_MAX_HOURS_PER_MONTH', 40), maxPerYear: rule('OT_MAX_HOURS_PER_YEAR', 200) },
          pendingApproval: has(ctx, 'timesheet.approve')
            ? db.leave_requests.filter((r) => r.status === 'pending' && visibleEmployeeIds(ctx).has(r.employee_id) && r.employee_id !== ctx.user.employee_id).length
              + db.ot_requests.filter((r) => r.status === 'pending' && visibleEmployeeIds(ctx).has(r.employee_id) && r.employee_id !== ctx.user.employee_id).length
            : 0,
          minNoticeDays: rule('LEAVE_MIN_NOTICE_DAYS', 1),
        },
      };
    }],
    ['POST', /^\/api\/v1\/leave-requests$/, async ({ req, culture }) => {
      const ctx = currentUser(req);
      requirePermission(ctx, 'timesheet.edit');
      const e = myEmployee(ctx);
      const body = await readJson(req);
      const errors = {};
      const type = store.masterById.get(str(body.leaveTypeId));
      if (!type || type.type !== 'leave_type' || !type.is_active) addError(errors, 'leaveTypeId', 'validation.required');
      const from = validDate(errors, 'fromDate', str(body.fromDate));
      const to = validDate(errors, 'toDate', str(body.toDate));
      const durationType = str(body.durationType) || 'full_day';
      if (!['full_day', 'half_day_am', 'half_day_pm'].includes(durationType)) addError(errors, 'durationType', 'validation.invalid_value');
      if (from && to && to < from) addError(errors, 'toDate', 'leave_save.err_to_before_from');
      if (from && to && durationType !== 'full_day' && from !== to) addError(errors, 'durationType', 'leave_save.err_half_day_range');
      const reason = str(body.reason);
      if (!reason) addError(errors, 'reason', 'validation.required');
      maxLength(errors, 'reason', reason, 500);
      // Báo trước tối thiểu LEAVE_MIN_NOTICE_DAYS ngày (trừ nghỉ ốm)
      const notice = rule('LEAVE_MIN_NOTICE_DAYS', 1);
      if (from && type?.code !== 'SICK' && ruleOn('LEAVE_MIN_NOTICE_DAYS') && from < addDays(today(), notice)) addError(errors, 'fromDate', { key: 'leave_save.err_min_notice', args: { days: notice } });
      if (type?.metadata?.need_attachment && !str(body.attachmentUrl)) addError(errors, 'attachmentUrl', 'leave_save.err_attachment_required');
      if (from && to) {
        const days = leaveDays(from, to, durationType);
        if (days === 0) addError(errors, 'toDate', 'leave_save.err_no_working_day');
        if (type?.metadata?.deduct_balance && days > leaveBalance(e, Number(from.slice(0, 4))).remainingDays) addError(errors, 'leaveTypeId', 'leave_save.err_balance');
        if (db.leave_requests.some((r) => r.employee_id === e.id && ['pending', 'approved'].includes(r.status) && r.from_date <= to && r.to_date >= from)) addError(errors, 'fromDate', 'leave_save.err_overlap');
      }
      throwIfErrors(errors);
      const fte = rule('TIMESHEET_FTE_HOURS', 8);
      const row = {
        id: randomUUID(), tenant_id: tenantId(), code: nextCode('leave_requests', 'NP'), employee_id: e.id, leave_type_id: type.id, from_date: from, to_date: to,
        duration_type: durationType, total_hours: leaveDays(from, to, durationType) * fte, reason, attachment_url: str(body.attachmentUrl) || null,
        status: 'pending', approver_user_id: null, decided_at: null, decision_note: null, created_at: nowIso(),
      };
      db.leave_requests.push(row);
      return { status: 201, key: 'leave_create.msg_success', args: { code: row.code }, data: leaveDto(row, culture) };
    }],
    ['POST', /^\/api\/v1\/leave-requests\/[^/]+\/approve$/, decide('leave_requests', 'leave', 'approve')],
    ['POST', /^\/api\/v1\/leave-requests\/[^/]+\/reject$/, decide('leave_requests', 'leave', 'reject')],
    ['POST', /^\/api\/v1\/leave-requests\/[^/]+\/cancel$/, decide('leave_requests', 'leave', 'cancel')],

    // ── OT ──
    ['GET', /^\/api\/v1\/ot-requests$/, async ({ req, url, culture }) => {
      const ctx = currentUser(req);
      return { data: listRequests('ot_requests', ctx, url, otDto, culture) };
    }],
    ['POST', /^\/api\/v1\/ot-requests$/, async ({ req, culture }) => {
      const ctx = currentUser(req);
      requirePermission(ctx, 'timesheet.edit');
      const e = myEmployee(ctx);
      const body = await readJson(req);
      const errors = {};
      const date = validDate(errors, 'workDate', str(body.workDate));
      const start = str(body.startTime);
      const end = str(body.endTime);
      if (!TIME_PATTERN.test(start)) addError(errors, 'startTime', 'validation.invalid_time');
      if (!TIME_PATTERN.test(end)) addError(errors, 'endTime', 'validation.invalid_time');
      else if (TIME_PATTERN.test(start) && end <= start) addError(errors, 'endTime', 'ot_save.err_end_before_start');
      const projectId = str(body.projectId) || null;
      if (projectId && date && !assignedProjects(e.id, date, date, culture).some((p) => p.id === projectId)) addError(errors, 'projectId', 'timesheet_save.err_no_assignment');
      const reason = str(body.reason);
      if (!reason) addError(errors, 'reason', 'validation.required');
      maxLength(errors, 'reason', reason, 500);
      const hours = TIME_PATTERN.test(start) && TIME_PATTERN.test(end) && end > start ? round2((toMinutes(end) - toMinutes(start)) / 60) : 0;
      if (date && hours > 0) {
        // Giới hạn OT (Quy tắc): OT_MAX_HOURS_PER_DAY / MONTH / YEAR, tính cả đơn đang chờ duyệt
        const used = otTotals(e.id, date, null);
        const limits = [['day', 'OT_MAX_HOURS_PER_DAY', 4], ['month', 'OT_MAX_HOURS_PER_MONTH', 40], ['year', 'OT_MAX_HOURS_PER_YEAR', 200]];
        for (const [k, key, fb] of limits) {
          if (ruleOn(key) && used[k] + hours > rule(key, fb)) addError(errors, 'endTime', { key: `ot_save.err_limit_${k}`, args: { hours: rule(key, fb) } });
        }
      }
      throwIfErrors(errors);
      const kind = otKind(date);
      const row = {
        id: randomUUID(), tenant_id: tenantId(), code: nextCode('ot_requests', 'OT'), employee_id: e.id, project_id: projectId, work_date: date, start_time: start, end_time: end,
        hours, day_kind: kind.dayKind, rate_percent: kind.ratePercent, reason, status: 'pending', approver_user_id: null, decided_at: null, decision_note: null, created_at: nowIso(),
      };
      db.ot_requests.push(row);
      return { status: 201, key: 'ot_create.msg_success', args: { code: row.code }, data: otDto(row, culture) };
    }],
    ['POST', /^\/api\/v1\/ot-requests\/[^/]+\/approve$/, decide('ot_requests', 'ot', 'approve')],
    ['POST', /^\/api\/v1\/ot-requests\/[^/]+\/reject$/, decide('ot_requests', 'ot', 'reject')],
    ['POST', /^\/api\/v1\/ot-requests\/[^/]+\/cancel$/, decide('ot_requests', 'ot', 'cancel')],

    // ── Chấm công vào/ra ──
    ['GET', /^\/api\/v1\/attendance$/, async ({ req, url }) => {
      const ctx = currentUser(req);
      if (!['timesheet.edit', 'timesheet.all_view', 'timesheet.all_edit'].some((p) => has(ctx, p))) throw forbidden();
      const month = url.searchParams.get('month') || today().slice(0, 7);
      if (!/^\d{4}-\d{2}$/.test(month)) throw new ApiError(400, 'error.validation_failed', { month: ['validation.invalid_value'] });
      const all = has(ctx, 'timesheet.all_view') || has(ctx, 'timesheet.all_edit');
      const search = url.searchParams.get('search') || '';
      const departmentIds = listParam(url, 'departmentIds');
      const onlyIssues = url.searchParams.get('onlyIssues') === 'true';
      const first = `${month}-01`;
      const last = new Date(Date.UTC(Number(month.slice(0, 4)), Number(month.slice(5)), 0)).toISOString().slice(0, 10);
      const days = dateRange(first, last);
      const people = db.employees
        .filter((e) => !e.is_deleted && e.is_active && (all || e.id === ctx.user.employee_id))
        .filter((e) => matches(search, e.full_name, e.code))
        .filter((e) => departmentIds.length === 0 || departmentIds.includes(e.department_id))
        .sort((a, b) => a.code.localeCompare(b.code));
      const rows = people.map((e) => {
        const records = {};
        let late = 0;
        let missingOut = 0;
        let worked = 0;
        let leave = 0;
        for (const d of days) {
          const rec = db.attendance_records.find((a) => a.employee_id === e.id && a.work_date === d);
          const dto = rec ? attendanceDto(rec) : null;
          const lv = onLeave(e.id, d);
          if (dto?.late) late += 1;
          if (dto?.missingOut) missingOut += 1;
          if (dto?.checkIn) worked += 1;
          if (lv && !isWeekend(d) && !holidayOn(d)) leave += lv.duration_type === 'full_day' ? 1 : 0.5;
          if (dto || lv) records[d] = { ...(dto ?? { id: null, date: d, checkIn: null, checkOut: null, source: null, note: null, late: false, missingOut: false, updatedBy: null }), onLeave: lv ? lv.duration_type : null };
        }
        return { employee: employeeRef(e), records, summary: { workedDays: worked, lateCount: late, missingOutCount: missingOut, leaveDays: leave } };
      }).filter((r) => !onlyIssues || r.summary.lateCount + r.summary.missingOutCount > 0);
      const page = paginate(rows, url, 50);
      return {
        data: {
          month, days: days.map((d) => ({ date: d, weekend: isWeekend(d), holiday: holidayOn(d)?.name ?? null })), workStartTime: workStart(), workEndTime: setting('work_end_time', '17:30'),
          graceMinutes: graceMinutes(), editLimitDays: rule('ATTENDANCE_EDIT_LIMIT_DAYS', 7), canEditAll: has(ctx, 'timesheet.all_edit'), myEmployeeId: ctx.user.employee_id ?? null,
          ...page,
        },
      };
    }],
    ['GET', /^\/api\/v1\/attendance\/today$/, async ({ req }) => {
      const ctx = currentUser(req);
      const e = ctx.user.employee_id ? employeeById(ctx.user.employee_id) : null;
      const rec = e ? db.attendance_records.find((a) => a.employee_id === e.id && a.work_date === today()) : null;
      return { data: { date: today(), now: nowTime(), allowAppCheckIn: setting('allow_app_check_in', 'true') === 'true' && !!e, record: rec ? attendanceDto(rec) : null, workStartTime: workStart() } };
    }],
    ['POST', /^\/api\/v1\/attendance\/(check-in|check-out)$/, async ({ req, url }) => {
      const ctx = currentUser(req);
      requirePermission(ctx, 'timesheet.edit');
      const e = myEmployee(ctx);
      if (setting('allow_app_check_in', 'true') !== 'true') throw new ApiError(409, 'attendance_check.err_disabled');
      const isIn = url.pathname.endsWith('check-in');
      const rec = db.attendance_records.find((a) => a.employee_id === e.id && a.work_date === today());
      if (isIn && rec?.check_in) throw new ApiError(409, 'attendance_check.err_already_in');
      if (!isIn && !rec?.check_in) throw new ApiError(409, 'attendance_check.err_not_in');
      const time = nowTime();
      // Giờ lấy theo máy chủ (không tin giờ của trình duyệt)
      const row = upsertAttendance(e.id, today(), isIn ? { check_in: time, source: 'app' } : { check_out: time > (rec?.check_in ?? '') ? time : fromMinutes(toMinutes(rec.check_in) + 1) }, null);
      return { key: isIn ? 'attendance_check_in.msg_success' : 'attendance_check_out.msg_success', args: { time }, data: attendanceDto(row) };
    }],
    ['PUT', /^\/api\/v1\/attendance$/, async ({ req }) => {
      const ctx = currentUser(req);
      const body = await readJson(req);
      const errors = {};
      const employeeId = str(body.employeeId) || ctx.user.employee_id;
      const date = validDate(errors, 'date', str(body.date));
      const checkIn = str(body.checkIn) || null;
      const checkOut = str(body.checkOut) || null;
      const note = str(body.note);
      validateTimes(errors, checkIn, checkOut);
      maxLength(errors, 'note', note, 300);
      const self = employeeId === ctx.user.employee_id;
      if (!self && !has(ctx, 'timesheet.all_edit')) throw forbidden();
      // Quản lý sửa tay bắt buộc ghi chú; nhân viên tự bổ sung trong ATTENDANCE_EDIT_LIMIT_DAYS ngày, không sửa giờ đã có
      if (!self && !note) addError(errors, 'note', 'attendance_save.err_note_required');
      if (date && date > today()) addError(errors, 'date', 'attendance_save.err_future');
      if (self && !has(ctx, 'timesheet.all_edit') && date) {
        const limit = rule('ATTENDANCE_EDIT_LIMIT_DAYS', 7);
        if (date < addDays(today(), -limit)) addError(errors, 'date', { key: 'attendance_save.err_edit_limit', args: { days: limit } });
        const existing = db.attendance_records.find((a) => a.employee_id === employeeId && a.work_date === date);
        if (existing?.check_in && checkIn !== existing.check_in) addError(errors, 'checkIn', 'attendance_save.err_self_overwrite');
        if (existing?.check_out && checkOut !== existing.check_out) addError(errors, 'checkOut', 'attendance_save.err_self_overwrite');
      }
      throwIfErrors(errors);
      if (!employeeById(employeeId)) throw notFound();
      const row = upsertAttendance(employeeId, date, { check_in: checkIn, check_out: checkOut, note: note || null, source: 'manual' }, ctx.user.id);
      return { key: 'attendance_save.msg_success', data: attendanceDto(row) };
    }],
    // Import file máy chấm công đã đọc ở FE: [{ employeeCode, date, checkIn, checkOut }] — ghép theo mã nhân viên
    ['POST', /^\/api\/v1\/attendance\/import$/, async ({ req }) => {
      const ctx = currentUser(req);
      requirePermission(ctx, 'timesheet.all_edit');
      const body = await readJson(req);
      const rows = Array.isArray(body.rows) ? body.rows.slice(0, 5000) : [];
      const overwrite = body.overwrite === true;
      let imported = 0;
      let skipped = 0;
      const errors = [];
      rows.forEach((r, i) => {
        const e = db.employees.find((x) => x.code === str(r.employeeCode).toUpperCase() && !x.is_deleted);
        const date = str(r.date);
        const checkIn = str(r.checkIn) || null;
        const checkOut = str(r.checkOut) || null;
        // Lỗi từng dòng trả mã (FE dịch): unknown_employee | invalid_date | invalid_time | out_before_in
        const codes = [];
        if (!e) codes.push('unknown_employee');
        if (!DATE_PATTERN.test(date)) codes.push('invalid_date');
        if ((checkIn && !TIME_PATTERN.test(checkIn)) || (checkOut && !TIME_PATTERN.test(checkOut))) codes.push('invalid_time');
        else if (checkIn && checkOut && checkOut <= checkIn) codes.push('out_before_in');
        if (codes.length) {
          errors.push({ row: i + 1, employeeCode: str(r.employeeCode), codes });
          return;
        }
        const existing = db.attendance_records.find((a) => a.employee_id === e.id && a.work_date === date);
        // Giữ luật cũ: không ghi đè ô đã sửa tay trừ khi chọn ghi đè
        if (existing && existing.source === 'manual' && !overwrite) {
          skipped += 1;
          return;
        }
        upsertAttendance(e.id, date, { check_in: checkIn, check_out: checkOut, source: 'import' }, ctx.user.id);
        imported += 1;
      });
      return { key: 'attendance_import.msg_success', args: { imported, skipped }, data: { imported, skipped, errors } };
    }],
  ];
}
