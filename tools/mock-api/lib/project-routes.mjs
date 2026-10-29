// Endpoint dự án (ngoài danh sách): chi tiết, tạo/sửa/xoá/khôi phục, nhân sự hợp đồng, phân bổ nhân sự,
// mốc tiến độ (milestone), EE và Defect/Leakage. Mọi số liệu tổng hợp do BE tính sẵn trong DTO (Q2);
// công thức cố định trong code, có comment chi tiết; ngưỡng màu lấy từ Quản trị → Quy tắc (module quality, D12).
// Quyền: project.view để xem (người không có project.manage chỉ xem dự án mình được phân bổ và chỉ thấy phần của mình),
// project.create để tạo, project.manage để sửa / phân bổ / quản trị.
import { randomUUID } from 'node:crypto';
import { ApiError, forbidden, notFound, readJson } from './http.mjs';
import { today } from './store.mjs';
import { addError, idFromPath, maxLength, nowIso, numberInRange, str, throwIfErrors, validDate } from './validate.mjs';

// ─── Tiện ích ngày ─────────────────────────────────────────────────────────

const DAY_MS = 86400000;
export const addDays = (iso, n) => new Date(Date.parse(`${iso}T00:00:00Z`) + n * DAY_MS).toISOString().slice(0, 10);
export const isWeekend = (iso) => [0, 6].includes(new Date(`${iso}T00:00:00Z`).getUTCDay());
export const diffDays = (from, to) => Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / DAY_MS);
export function daysOfMonth(month) {
  const days = [];
  for (let d = 1; d <= 31; d++) {
    const iso = `${month}-${String(d).padStart(2, '0')}`;
    if (new Date(`${iso}T00:00:00Z`).getUTCMonth() + 1 !== Number(month.slice(5, 7))) break;
    days.push(iso);
  }
  return days;
}
export const workingDaysOfMonth = (month) => daysOfMonth(month).filter((d) => !isWeekend(d)).length;
const round2 = (n) => Math.round(n * 100) / 100;
const MONTH_PATTERN = /^\d{4}-\d{2}$/;
const PRODUCT_SIZE_UNITS = ['Point', 'Request', 'Module', 'KLOC'];

/** Ngưỡng màu chất lượng — tham số sửa ở Quản trị → Quy tắc (module quality). */
export function qualityRules(store) {
  const v = (key, fallback) => store.ruleValue(key, fallback);
  return {
    pcvGreenMin: v('QUALITY_PCV_GREEN_MIN', 90), pcvOrangeMin: v('QUALITY_PCV_ORANGE_MIN', 70),
    cssGreenMin: v('QUALITY_CSS_GREEN_MIN', 4.5), cssOrangeMin: v('QUALITY_CSS_ORANGE_MIN', 3.5),
    eeGreenMax: v('QUALITY_EE_GREEN_MAX', 100), eeOrangeMax: v('QUALITY_EE_ORANGE_MAX', 110),
    defectGoodMin: v('QUALITY_DEFECT_RATE_GOOD_MIN', 4), defectGoodMax: v('QUALITY_DEFECT_RATE_GOOD_MAX', 5), defectWarnMax: v('QUALITY_DEFECT_RATE_WARN_MAX', 6),
    leakageGoodMin: v('QUALITY_LEAKAGE_RATE_GOOD_MIN', 0.45), leakageGoodMax: v('QUALITY_LEAKAGE_RATE_GOOD_MAX', 0.5), leakageWarnMax: v('QUALITY_LEAKAGE_RATE_WARN_MAX', 0.55),
  };
}

/**
 * Mức EE: EE ≤ ngưỡng xanh (mặc định 100%) → green (dùng đúng/ít hơn công hợp đồng);
 * ≤ ngưỡng cam (mặc định 110%) → orange; lớn hơn → red; không có EE (HĐ = 0) → none.
 */
export function eeTone(value, r) {
  if (value === null) return 'none';
  if (value <= r.eeGreenMax) return 'green';
  return value <= r.eeOrangeMax ? 'orange' : 'red';
}

/**
 * Mức tỷ lệ lỗi (defect/MM) — 4 mức: thấp hơn khoảng tốt (nghi thiếu test) → lightOrange; trong khoảng tốt → green;
 * vượt khoảng tốt tới ngưỡng cảnh báo → yellow; vượt ngưỡng cảnh báo → red.
 * Defect (Egg rate) mặc định: < 4 · 4–5 · > 5–6 · > 6. Leakage (Bug rate): < 0.45 · 0.45–< 0.5 · 0.5–0.55 · > 0.55
 * (Leakage: cận trên khoảng tốt là mở, giống bản gốc).
 */
export function rateTone(value, kind, r) {
  if (value === null) return 'none';
  if (kind === 'defect') {
    if (value < r.defectGoodMin) return 'lightOrange';
    if (value <= r.defectGoodMax) return 'green';
    return value <= r.defectWarnMax ? 'yellow' : 'red';
  }
  if (value < r.leakageGoodMin) return 'lightOrange';
  if (value < r.leakageGoodMax) return 'green';
  return value <= r.leakageWarnMax ? 'yellow' : 'red';
}

/**
 * @param {{ store: ReturnType<import('./store.mjs').createStore>, currentUser: Function, requirePermission: Function }} deps
 */
export function projectRoutes({ store, currentUser, requirePermission, localize }) {
  const { db } = store;
  const fteHours = () => store.ruleValue('TIMESHEET_FTE_HOURS', 8);
  const employeeById = (id) => db.employees.find((e) => e.id === id);
  const userName = (userId) => db.users.find((u) => u.id === userId)?.full_name ?? null;
  const masterByCode = (type, code) => db.master_data.find((m) => m.type === type && m.code === code);

  // ─── Quyền theo dự án ────────────────────────────────────────────────────

  function findProject(id, { allowDeleted = true } = {}) {
    const p = db.projects.find((x) => x.id === id);
    if (!p || (!allowDeleted && p.is_deleted)) throw notFound();
    return p;
  }
  const isMember = (p, user) => !!user.employee_id && db.assignments.some((a) => a.project_id === p.id && a.employee_id === user.employee_id && !a.is_deleted);

  /** Mở dự án: cần project.view; không có project.manage thì phải đang được phân bổ vào dự án (D3: không bypass theo tên role). */
  function openProject(req, url, index = 4) {
    const ctx = currentUser(req);
    requirePermission(ctx, 'project.view');
    const p = findProject(idFromPath(url, index));
    const canManage = ctx.permissions.includes('project.manage');
    if (!canManage && !isMember(p, ctx.user)) throw new ApiError(403, 'project_detail.err_no_access');
    return { ctx, p, canManage };
  }
  function manageProject(req, url, index = 4) {
    const o = openProject(req, url, index);
    if (!o.canManage) throw forbidden();
    if (o.p.is_deleted) throw new ApiError(409, 'project_detail.err_deleted');
    return o;
  }

  // ─── Nhân sự & phân bổ ───────────────────────────────────────────────────

  const schedulesOf = (assignmentId) => db.project_assignment_schedules.filter((s) => s.assignment_id === assignmentId).sort((a, b) => a.start_date.localeCompare(b.start_date));
  const liveAssignments = (projectId) => db.assignments.filter((a) => a.project_id === projectId && !a.is_deleted);

  /** % phân bổ hiện tại của nhân sự trên mọi dự án đang chạy = Σ FTE các giai đoạn còn hiệu lực hôm nay × 100. */
  function currentAllocationPercent(employeeId) {
    const now = today();
    let fte = 0;
    for (const a of db.assignments.filter((x) => x.employee_id === employeeId && !x.is_deleted)) {
      if (!db.projects.some((p) => p.id === a.project_id && !p.is_deleted)) continue;
      for (const s of schedulesOf(a.id)) if (s.start_date <= now && s.end_date >= now) fte += Number(s.fte_allocated || 0);
    }
    return Math.round(fte * 100);
  }

  /**
   * Trạng thái nghỉ việc so với giai đoạn phân bổ (port ProjectReadOnlyView/AssignmentTable):
   * chỉ cảnh báo khi ngày nghỉ rơi vào hoặc trước ngày kết thúc phân bổ (không có ngày nghỉ mà is_active = false → coi là trùng).
   * resigned = (không còn hoạt động hoặc ngày nghỉ ≤ hôm nay); leavingSoon = ngày nghỉ > hôm nay.
   */
  function leaveFlags(e, assignmentEnd) {
    const now = today();
    const left = e.left_date ?? null;
    const conflict = left ? !assignmentEnd || left <= assignmentEnd : true;
    const resigned = (!e.is_active || (!!left && left <= now)) && conflict;
    const leavingSoon = !resigned && !!left && left > now && conflict;
    return { resigned, leavingSoon };
  }

  function employeeRef(e, assignmentEnd) {
    if (!e) return null;
    const overload = store.ruleValue('HR_OVERLOAD_ALLOCATION_THRESHOLD', 100);
    const allocationPercent = currentAllocationPercent(e.id);
    return {
      id: e.id, code: e.code, fullName: e.full_name, avatarUrl: e.avatar_url ?? null, position: e.position ?? null,
      leftDate: e.left_date ?? null, allocationPercent, overloaded: allocationPercent > overload, overloadThreshold: overload,
      ...leaveFlags(e, assignmentEnd),
    };
  }

  /** Cảnh báo biến động nhân sự của dự án: người đã/sắp nghỉ mà chưa có người khác nhận tiếp vị trí. */
  function hrAlerts(p, onlyEmployeeId) {
    const rows = liveAssignments(p.id).filter((a) => !onlyEmployeeId || a.employee_id === onlyEmployeeId);
    const alerts = [];
    for (const a of rows) {
      const e = employeeById(a.employee_id);
      if (!e) continue;
      const end = schedulesOf(a.id).at(-1)?.end_date ?? a.end_date;
      const replaced = a.contract_staff_id && liveAssignments(p.id).some((b) => b.id !== a.id && b.contract_staff_id === a.contract_staff_id && b.slot_index === a.slot_index && b.start_date > a.start_date && b.employee_id !== a.employee_id);
      const f = leaveFlags(e, end);
      if (!replaced && (f.resigned || f.leavingSoon)) alerts.push({ employeeId: e.id, fullName: e.full_name, code: e.code, kind: f.resigned ? 'resigned' : 'leaving_soon', leftDate: e.left_date ?? null });
    }
    return alerts;
  }

  /** Số ngày làm việc (T2–T6) trong khoảng [from, to]. */
  function workingDaysBetween(from, to) {
    let n = 0;
    for (let d = from; d <= to; d = addDays(d, 1)) if (!isWeekend(d)) n += 1;
    return n;
  }

  /**
   * Tiến độ dự án (%) = (hôm nay − ngày bắt đầu) / (ngày kết thúc − ngày bắt đầu) × 100, kẹp trong 0–100 và làm tròn xuống;
   * thiếu ngày hoặc ngày kết thúc ≤ ngày bắt đầu → dùng % tiến độ nhập tay (progress_pct). Port getProjectProgressPercent.
   */
  function progressPercent(p) {
    if (p.start_date && p.end_date && p.end_date > p.start_date) {
      const pct = (diffDays(p.start_date, today()) / diffDays(p.start_date, p.end_date)) * 100;
      return Math.floor(Math.min(100, Math.max(0, pct)));
    }
    return Number(p.progress_pct || 0);
  }

  function linkUrl(p, providers) {
    return db.project_links.find((l) => l.project_id === p.id && providers.includes(l.provider))?.url ?? null;
  }

  function contractStaffDto(cs, culture) {
    const role = masterByCode('job_position', cs.role_code);
    return {
      id: cs.id, roleCode: cs.role_code, roleName: role ? store.localized(role, culture) : cs.role_code, level: store.masterRef(cs.level_id, culture),
      quantity: cs.quantity, ftePerPerson: cs.fte_per_person, startDate: cs.start_date, endDate: cs.end_date, pricePerMonth: cs.price_per_month,
    };
  }

  function detailDto(p, ctx, canManage, culture) {
    const base = store.projectDto(p, culture);
    const staff = db.contract_staff.filter((cs) => cs.project_id === p.id && !cs.is_deleted);
    let contractStaffFte = base.contractStaffFte;
    let actualStaffFte = base.actualStaffFte;
    if (!canManage) {
      // Người không quản lý chỉ thấy phần của mình (port filteredProject): FTE hợp đồng = Σ FTE/người của các vị trí mình giữ;
      // FTE thực tế = tổng giờ mình log vào dự án / (số ngày làm việc của dự án × số giờ 1 FTE).
      const mine = liveAssignments(p.id).filter((a) => a.employee_id === ctx.user.employee_id);
      const slotIds = new Set(mine.map((a) => a.contract_staff_id).filter(Boolean));
      contractStaffFte = round2(staff.filter((cs) => slotIds.has(cs.id)).reduce((s, cs) => s + (cs.fte_per_person || 0), 0));
      const hours = db.timesheets.filter((t) => t.project_id === p.id && t.employee_id === ctx.user.employee_id).reduce((s, t) => s + Number(t.hours || 0), 0);
      const days = p.start_date && p.end_date ? workingDaysBetween(p.start_date, p.end_date) : 0;
      actualStaffFte = days > 0 ? round2(hours / (days * fteHours())) : 0;
    }
    return {
      ...base, contractStaffFte, actualStaffFte,
      description: p.description ?? '', progressPercent: progressPercent(p),
      ticketSystemUrl: linkUrl(p, ['jira', 'backlog']), sourceCodeUrl: linkUrl(p, ['github', 'gitlab']), driveUrl: linkUrl(p, ['google_drive']),
      contractStaff: canManage ? staff.map((cs) => contractStaffDto(cs, culture)) : [],
      budgetWarningThreshold: store.ruleValue('PROJECT_BUDGET_WARNING_THRESHOLD', 75),
      hrAlerts: hrAlerts(p, canManage ? null : ctx.user.employee_id),
      canManage, canEditMonthlyReport: true,
      canManagePcv: ctx.permissions.includes('pcv.manage'), canManageCss: ctx.permissions.includes('css.manage'),
    };
  }

  /**
   * Bảng phân bổ nhân sự + đường HĐ / KH / TT theo ngày cho từng vị trí (port groupProjectMembers + buildMemberTimelinePoints):
   * - HĐ (contract) của ngày = FTE/người của vị trí hợp đồng nếu ngày nằm trong thời gian hợp đồng, ngược lại 0;
   * - KH (planned) = FTE phân bổ lớn nhất trong các giai đoạn còn hiệu lực của người giữ vị trí (0 nếu vị trí trống);
   * - TT (actual) = tổng giờ Nhật ký công việc vào dự án của người đang giữ vị trí ngày đó / số giờ 1 FTE (quy tắc TIMESHEET_FTE_HOURS);
   *   ngày cuối tuần không log thì giữ giá trị của ngày làm việc gần nhất trước đó (để đường không rơi về 0).
   * Tổng HĐ / KH của một giai đoạn chỉ cộng ngày T2–T6 (FTE-ngày), TT cộng mọi ngày có log.
   */
  function staffing(p, ctx, canManage, culture) {
    const start = p.start_date ?? today();
    const end = p.end_date && p.end_date >= start ? p.end_date : addDays(start, 179);
    const dayCount = Math.min(1095, diffDays(start, end) + 1);
    const days = Array.from({ length: dayCount }, (_, i) => addDays(start, i));
    const perFte = fteHours();
    const hoursBy = new Map();
    for (const t of db.timesheets) if (t.project_id === p.id) hoursBy.set(`${t.employee_id}|${t.work_date}`, (hoursBy.get(`${t.employee_id}|${t.work_date}`) ?? 0) + Number(t.hours || 0));

    const assignments = liveAssignments(p.id);
    function slot({ key, contractStaff, slotIndex, rows }) {
      const contractFte = contractStaff ? Number(contractStaff.fte_per_person || 0) : 0;
      const intervals = rows.flatMap((a) => schedulesOf(a.id).map((s) => ({ a, s })));
      const series = [];
      let lastWorkdayActual = 0;
      for (const d of days) {
        const active = intervals.filter(({ s }) => s.start_date <= d && s.end_date >= d);
        const contract = contractStaff && contractStaff.start_date <= d && contractStaff.end_date >= d ? contractFte : 0;
        const planned = active.reduce((m, { s }) => Math.max(m, Number(s.fte_allocated || 0)), 0);
        const hours = active.reduce((sum, { a }) => sum + (hoursBy.get(`${a.employee_id}|${d}`) ?? 0), 0);
        let actual = active.length > 0 && hours > 0 ? round2(hours / perFte) : 0;
        if (active.length > 0 && hours === 0 && isWeekend(d)) actual = lastWorkdayActual;
        if (!isWeekend(d)) lastWorkdayActual = actual;
        series.push({ date: d, contract: round2(contract), planned: round2(planned), actual });
      }
      const sums = (from, to) => {
        const inRange = series.filter((pt) => pt.date >= from && pt.date <= to);
        const work = inRange.filter((pt) => !isWeekend(pt.date));
        return {
          contract: round2(work.reduce((s, pt) => s + pt.contract, 0)),
          planned: round2(work.reduce((s, pt) => s + pt.planned, 0)),
          actual: round2(inRange.reduce((s, pt) => s + pt.actual, 0)),
        };
      };
      return {
        key, contractStaffId: contractStaff?.id ?? null, slotIndex, contractFte,
        contractStartDate: contractStaff?.start_date ?? null, contractEndDate: contractStaff?.end_date ?? null,
        assignments: rows.map((a) => {
          const sch = schedulesOf(a.id);
          const last = sch.at(-1)?.end_date ?? a.end_date;
          return {
            id: a.id, roleInProject: a.role_in_project, employee: employeeRef(employeeById(a.employee_id), last),
            startDate: sch[0]?.start_date ?? a.start_date, endDate: last,
            intervals: sch.map((s) => ({ id: s.id, startDate: s.start_date, endDate: s.end_date, fte: s.fte_allocated, sums: sums(s.start_date, s.end_date) })),
          };
        }),
        series,
      };
    }

    const groups = new Map();
    const staff = db.contract_staff.filter((cs) => cs.project_id === p.id && !cs.is_deleted);
    for (const cs of staff) {
      const gKey = `${cs.role_code}::${cs.level_id}`;
      const role = masterByCode('job_position', cs.role_code);
      if (!groups.has(gKey)) groups.set(gKey, { key: gKey, roleCode: cs.role_code, roleName: role ? store.localized(role, culture) : cs.role_code, level: store.masterRef(cs.level_id, culture), contractCount: 0, slots: [] });
      const g = groups.get(gKey);
      g.contractCount += cs.quantity || 0;
      for (let i = 0; i < (cs.quantity || 0); i++) {
        g.slots.push(slot({ key: `${cs.id}:${i}`, contractStaff: cs, slotIndex: i, rows: assignments.filter((a) => a.contract_staff_id === cs.id && (a.slot_index ?? 0) === i) }));
      }
    }
    // Phân bổ ngoài hợp đồng (không gắn vị trí hợp đồng) → nhóm "Ngoại HĐ" theo vai trò
    for (const a of assignments.filter((x) => !x.contract_staff_id || !staff.some((cs) => cs.id === x.contract_staff_id))) {
      const gKey = `outside::${a.role_in_project}`;
      const role = masterByCode('job_position', a.role_in_project);
      if (!groups.has(gKey)) groups.set(gKey, { key: gKey, roleCode: a.role_in_project, roleName: role ? store.localized(role, culture) : a.role_in_project, level: store.masterRef(a.level_id, culture), contractCount: 0, slots: [] });
      groups.get(gKey).slots.push(slot({ key: `outside:${a.id}`, contractStaff: null, slotIndex: 0, rows: [a] }));
    }
    let result = [...groups.values()].map((g) => ({ ...g, assignedCount: g.slots.filter((s) => s.assignments.length > 0).length }));
    if (!canManage) {
      // Người không quản lý: chỉ vị trí mình giữ
      result = result
        .map((g) => ({ ...g, slots: g.slots.filter((s) => s.assignments.some((a) => a.employee?.id === ctx.user.employee_id)) }))
        .filter((g) => g.slots.length > 0);
    }
    return { startDate: start, endDate: end, fteHours: perFte, groups: result };
  }

  // ─── Kiểm tra dữ liệu ghi ────────────────────────────────────────────────

  function parseProject(body, { isCreate }) {
    const errors = {};
    const name = str(body.name);
    if (!name) addError(errors, 'name', 'validation.required');
    maxLength(errors, 'name', name, 200);
    const clientId = str(body.clientId);
    if (!clientId) addError(errors, 'clientId', 'validation.required');
    else if (!db.clients.some((c) => c.id === clientId && !c.is_deleted)) addError(errors, 'clientId', 'validation.invalid_value');
    const gradeId = str(body.gradeId);
    if (!gradeId) addError(errors, 'gradeId', 'validation.required');
    else if (store.masterById.get(gradeId)?.type !== 'project_level') addError(errors, 'gradeId', 'validation.invalid_value');
    let statusId = null;
    if (!isCreate) {
      statusId = str(body.statusId);
      if (!statusId) addError(errors, 'statusId', 'validation.required');
      else if (store.masterById.get(statusId)?.type !== 'project_status') addError(errors, 'statusId', 'validation.invalid_value');
    }
    const description = str(body.description);
    maxLength(errors, 'description', description, 4000);
    const startDate = validDate(errors, 'startDate', str(body.startDate), { optional: true });
    const endDate = validDate(errors, 'endDate', str(body.endDate), { optional: true });
    if (startDate && endDate && endDate < startDate) addError(errors, 'endDate', 'project_save.err_end_before_start');
    let budget = null;
    if (body.budget !== null && body.budget !== undefined && body.budget !== '') budget = numberInRange(errors, 'budget', body.budget, 0, null);
    const ticketSystemUrl = str(body.ticketSystemUrl);
    const sourceCodeUrl = str(body.sourceCodeUrl);
    for (const [field, value] of [['ticketSystemUrl', ticketSystemUrl], ['sourceCodeUrl', sourceCodeUrl]]) {
      if (value && !/^https?:\/\/\S+$/i.test(value)) addError(errors, field, 'validation.invalid_url');
      maxLength(errors, field, value, 500);
    }
    const rows = Array.isArray(body.contractStaff) ? body.contractStaff : [];
    if (rows.length === 0) addError(errors, 'contractStaff', 'project_save.err_contract_staff_required');
    const contractStaff = rows.map((r, i) => {
      const f = (k) => `contractStaff.${i}.${k}`;
      const roleCode = str(r.roleCode);
      if (!roleCode) addError(errors, f('roleCode'), 'validation.required');
      else if (!masterByCode('job_position', roleCode)) addError(errors, f('roleCode'), 'validation.invalid_value');
      const levelId = str(r.levelId);
      if (!levelId) addError(errors, f('levelId'), 'validation.required');
      else if (store.masterById.get(levelId)?.type !== 'hr_level') addError(errors, f('levelId'), 'validation.invalid_value');
      const quantity = numberInRange(errors, f('quantity'), r.quantity, 1, 100, { integer: true });
      const ftePerPerson = numberInRange(errors, f('ftePerPerson'), r.ftePerPerson, 0.01, 2);
      const pricePerMonth = r.pricePerMonth === '' || r.pricePerMonth === null || r.pricePerMonth === undefined ? 0 : numberInRange(errors, f('pricePerMonth'), r.pricePerMonth, 0, null);
      const s = validDate(errors, f('startDate'), str(r.startDate), { optional: true }) ?? startDate;
      const e = validDate(errors, f('endDate'), str(r.endDate), { optional: true }) ?? endDate;
      if (s && e && e < s) addError(errors, f('endDate'), 'project_save.err_end_before_start');
      if (s && startDate && s < startDate) addError(errors, f('startDate'), 'project_save.err_staff_before_project');
      if (e && endDate && e > endDate) addError(errors, f('endDate'), 'project_save.err_staff_after_project');
      return { id: str(r.id) || null, roleCode, levelId, quantity, ftePerPerson, pricePerMonth, startDate: s, endDate: e };
    });
    throwIfErrors(errors);
    return { name, clientId, gradeId, statusId, description, startDate, endDate, budget, ticketSystemUrl, sourceCodeUrl, contractStaff };
  }

  function upsertLink(p, provider, linkCode, label, url) {
    const existing = db.project_links.find((l) => l.project_id === p.id && (provider === 'ticket' ? ['jira', 'backlog'].includes(l.provider) : ['github', 'gitlab'].includes(l.provider)));
    if (!url) {
      if (existing) db.project_links.splice(db.project_links.indexOf(existing), 1);
      return;
    }
    const realProvider = provider === 'ticket' ? (/backlog/i.test(url) ? 'backlog' : 'jira') : (/gitlab/i.test(url) ? 'gitlab' : 'github');
    const linkType = masterByCode('link_type', realProvider === 'google_drive' ? 'GOOGLE_DRIVE' : realProvider.toUpperCase());
    if (existing) Object.assign(existing, { url, provider: realProvider, link_type_id: linkType?.id ?? existing.link_type_id });
    else db.project_links.push({ id: randomUUID(), tenant_id: p.tenant_id, project_id: p.id, link_type_id: linkType?.id ?? null, provider: realProvider, name: label, url });
    void linkCode;
  }

  function applyContractStaff(p, rows) {
    const current = db.contract_staff.filter((cs) => cs.project_id === p.id && !cs.is_deleted);
    const errors = {};
    // Vị trí bị bỏ hoặc giảm số lượng thì không được còn người đang được phân bổ ở slot bị cắt
    current.forEach((cs) => {
      const row = rows.find((r) => r.id === cs.id);
      const keep = row ? row.quantity : 0;
      const busy = liveAssignments(p.id).filter((a) => a.contract_staff_id === cs.id && (a.slot_index ?? 0) >= keep);
      if (busy.length > 0) {
        const index = row ? rows.indexOf(row) : -1;
        addError(errors, index >= 0 ? `contractStaff.${index}.quantity` : 'contractStaff', 'project_save.err_staff_in_use');
      }
    });
    throwIfErrors(errors);
    for (const cs of current) if (!rows.some((r) => r.id === cs.id)) cs.is_deleted = true;
    for (const r of rows) {
      const fields = { role_code: r.roleCode, level_id: r.levelId, quantity: r.quantity, fte_per_person: r.ftePerPerson, start_date: r.startDate ?? p.start_date, end_date: r.endDate ?? p.end_date, price_per_month: r.pricePerMonth };
      const cs = r.id ? current.find((x) => x.id === r.id) : null;
      if (cs) Object.assign(cs, fields);
      else db.contract_staff.push({ id: randomUUID(), tenant_id: p.tenant_id, project_id: p.id, ...fields, is_deleted: false });
    }
    // FTE hợp đồng của dự án = Σ (số lượng × FTE/người) các vị trí hợp đồng
    p.contract_staff_fte = round2(rows.reduce((s, r) => s + r.quantity * r.ftePerPerson, 0));
  }

  function nextProjectCode() {
    const max = db.projects.reduce((m, x) => Math.max(m, Number(/^PRJ-(\d+)$/.exec(x.code)?.[1] ?? 0)), 0);
    return `PRJ-${String(max + 1).padStart(2, '0')}`;
  }

  // ─── Phân bổ nhân sự ─────────────────────────────────────────────────────

  function parseIntervals(body, p) {
    const errors = {};
    const list = Array.isArray(body.intervals) ? body.intervals : [];
    if (list.length === 0) addError(errors, 'intervals', 'project_assignment.err_interval_required');
    const intervals = list.map((it, i) => {
      const f = (k) => `intervals.${i}.${k}`;
      const s = validDate(errors, f('startDate'), str(it.startDate));
      const e = validDate(errors, f('endDate'), str(it.endDate));
      const fte = numberInRange(errors, f('fte'), it.fte, 0.01, 2);
      if (s && e && e < s) addError(errors, f('endDate'), 'project_save.err_end_before_start');
      if (s && p.start_date && s < p.start_date) addError(errors, f('startDate'), { key: 'project_assignment.err_before_project', args: { date: p.start_date } });
      if (e && p.end_date && e > p.end_date) addError(errors, f('endDate'), { key: 'project_assignment.err_after_project', args: { date: p.end_date } });
      return { startDate: s, endDate: e, fte };
    });
    // Các giai đoạn của cùng một người không được chồng nhau: (startA ≤ endB) và (endA ≥ startB)
    for (let i = 0; i < intervals.length; i++) {
      for (let j = i + 1; j < intervals.length; j++) {
        const a = intervals[i];
        const b = intervals[j];
        if (a.startDate && a.endDate && b.startDate && b.endDate && a.startDate <= b.endDate && a.endDate >= b.startDate) {
          addError(errors, `intervals.${j}.startDate`, { key: 'project_assignment.err_overlap', args: { a: i + 1, b: j + 1 } });
        }
      }
    }
    return { errors, intervals };
  }

  function writeAssignment(a, intervals) {
    db.project_assignment_schedules = db.project_assignment_schedules.filter((s) => s.assignment_id !== a.id);
    const sorted = [...intervals].sort((x, y) => x.startDate.localeCompare(y.startDate));
    for (const it of sorted) db.project_assignment_schedules.push({ id: randomUUID(), tenant_id: a.tenant_id, assignment_id: a.id, start_date: it.startDate, end_date: it.endDate, fte_allocated: it.fte });
    // Dòng phân bổ giữ khoảng bao ngoài + FTE lớn nhất để các màn tổng hợp (Nhân sự, Công số) đọc nhanh
    a.start_date = sorted[0].startDate;
    a.end_date = sorted.at(-1).endDate;
    a.fte_allocated = Math.max(...sorted.map((x) => x.fte));
    if (!db.project_members.some((m) => m.project_id === a.project_id && m.employee_id === a.employee_id)) db.project_members.push({ project_id: a.project_id, employee_id: a.employee_id, role: a.role_in_project });
  }

  function newAssignment(p, { employeeId, contractStaffId, slotIndex, roleInProject, levelId }) {
    const a = { id: randomUUID(), tenant_id: p.tenant_id, project_id: p.id, employee_id: employeeId, contract_staff_id: contractStaffId, slot_index: slotIndex, role_in_project: roleInProject, level_id: levelId, fte_allocated: 0, start_date: null, end_date: null, is_deleted: false };
    db.assignments.push(a);
    return a;
  }

  // ─── Milestone ───────────────────────────────────────────────────────────

  /**
   * Trạng thái mốc (port computeMilestoneStatuses): có ngày thực tế → done-ontime nếu thực tế ≤ dự kiến, ngược lại done-late;
   * chưa xong mà dự kiến < hôm nay → overdue; các mốc tương lai: mốc gần nhất là next, còn lại upcoming.
   */
  function milestoneStatuses(rows) {
    const now = today();
    const map = new Map();
    const future = [];
    for (const m of rows) {
      if (m.actual_date) map.set(m.id, m.actual_date <= m.planned_date ? 'done-ontime' : 'done-late');
      else if (m.planned_date < now) map.set(m.id, 'overdue');
      else future.push(m);
    }
    future.sort((a, b) => a.planned_date.localeCompare(b.planned_date)).forEach((m, i) => map.set(m.id, i === 0 ? 'next' : 'upcoming'));
    return map;
  }

  /**
   * Tỷ lệ đúng hạn (port computeTimeliness) = số mốc đúng hạn / số mốc đã đến hạn trong khoảng × 100 (làm tròn);
   * "đã đến hạn" = mốc có ngày dự kiến trong khoảng và không phải next/upcoming. Không có mốc đến hạn → null.
   */
  function timeliness(rows, statuses, from, to) {
    const due = rows.filter((m) => m.planned_date >= from && m.planned_date <= to && !['next', 'upcoming'].includes(statuses.get(m.id)));
    if (due.length === 0) return { dueCount: 0, onTimeCount: 0, percent: null };
    const onTime = due.filter((m) => statuses.get(m.id) === 'done-ontime').length;
    return { dueCount: due.length, onTimeCount: onTime, percent: Math.round((onTime / due.length) * 100) };
  }

  function milestonesDto(p, range = {}) {
    const rows = db.project_milestones.filter((m) => m.project_id === p.id && !m.is_deleted).sort((a, b) => a.planned_date.localeCompare(b.planned_date) || a.sort_seq - b.sort_seq);
    const statuses = milestoneStatuses(rows);
    const start = p.start_date ?? rows[0]?.planned_date ?? today();
    const end = p.end_date ?? rows.at(-1)?.planned_date ?? today();
    return {
      startDate: start, endDate: end, today: today(),
      items: rows.map((m) => ({
        id: m.id, name: m.name, plannedDate: m.planned_date, actualDate: m.actual_date ?? null, sortSeq: m.sort_seq,
        status: statuses.get(m.id), delayDays: m.actual_date ? Math.max(0, diffDays(m.planned_date, m.actual_date)) : m.planned_date < today() ? diffDays(m.planned_date, today()) : 0,
        justification: m.justification ?? null, justificationBy: m.justification_by ? userName(m.justification_by) : null, justificationAt: m.justification_at ?? null,
      })),
      timeliness: { from: range.from || start, to: range.to || end, ...timeliness(rows, statuses, range.from || start, range.to || end) },
    };
  }

  function parseMilestone(body, current) {
    const errors = {};
    const name = str(body.name);
    if (!name) addError(errors, 'name', 'validation.required');
    maxLength(errors, 'name', name, 200);
    const plannedDate = validDate(errors, 'plannedDate', str(body.plannedDate));
    const actualDate = validDate(errors, 'actualDate', str(body.actualDate), { optional: true });
    if (actualDate && actualDate > today()) addError(errors, 'actualDate', 'project_milestone.err_actual_future');
    const justification = str(body.justification);
    maxLength(errors, 'justification', justification, 1000);
    throwIfErrors(errors);
    void current;
    return { name, plannedDate, actualDate, justification: justification || null };
  }

  // ─── EE ──────────────────────────────────────────────────────────────────

  /**
   * EE (Effort Efficiency) theo tháng — port computeProjectEE / buildMemberSeries:
   * - Kế hoạch (MM) của một người trong tháng = cột Kế hoạch (`hd`) ở màn Công số; Actual (MM) = Thực tế + OT (cột Actual ở Công số, chưa nhân Performance);
   * - 1 MM quy ra giờ = số ngày T2–T6 của tháng × 8 giờ (chưa trừ ngày lễ) → mỗi tháng 1 MM ra số giờ khác nhau;
   * - EE tháng (%) = Actual giờ / Kế hoạch giờ × 100; Kế hoạch = 0 → không tính được (null);
   * - EE luỹ kế năm = Σ Actual / Σ Kế hoạch của các tháng có EE trong cùng năm dương lịch, tính đến tháng đó;
   * - Logwork = tổng giờ Nhật ký công việc vào dự án (chỉ để đối chiếu, không dùng tính EE).
   * Ví dụ: tháng 10/2026 có 22 ngày làm việc → 1 MM = 176h; Kế hoạch 2 MM = 352h, Actual 1,6 + OT 0,2 = 1,8 MM = 316,8h → EE = 90%.
   */
  function eeData(p, ctx, canManage, monthParam) {
    const STANDARD = 8;
    const onlyMine = canManage ? null : ctx.user.employee_id;
    const rows = new Map();
    const rowFor = (employeeId, month) => {
      const k = `${employeeId}|${month}`;
      if (!rows.has(k)) {
        const e = employeeById(employeeId);
        rows.set(k, { month, employeeId, fullName: e?.full_name ?? '', code: e?.code ?? '', position: e?.position ?? '', hdMM: 0, actualMM: 0, logworkHours: 0, logDaily: new Map() });
      }
      return rows.get(k);
    };
    for (const a of db.effort_allocations) {
      if (a.project_id !== p.id || a.is_deleted || (onlyMine && a.employee_id !== onlyMine)) continue;
      const r = rowFor(a.employee_id, a.year_month.slice(0, 7));
      r.hdMM += Number(a.hd || 0);
      r.actualMM += Number(a.tt_effort || 0) + Number(a.ot_effort || 0);
    }
    for (const t of db.timesheets) {
      if (t.project_id !== p.id || (onlyMine && t.employee_id !== onlyMine)) continue;
      const r = rowFor(t.employee_id, t.work_date.slice(0, 7));
      r.logworkHours += Number(t.hours || 0);
      r.logDaily.set(t.work_date, (r.logDaily.get(t.work_date) ?? 0) + Number(t.hours || 0));
    }
    const entries = [...rows.values()];
    const months = [...new Set(entries.map((e) => e.month))].sort();
    const r = qualityRules(store);
    const ee = (actual, hd) => (hd > 0 ? round2((actual / hd) * 100) : null);
    const ytdByYear = new Map();
    const monthly = months.map((month) => {
      const list = entries.filter((e) => e.month === month);
      const perMM = workingDaysOfMonth(month) * STANDARD;
      const hdMM = list.reduce((s, e) => s + e.hdMM, 0);
      const actualMM = list.reduce((s, e) => s + e.actualMM, 0);
      const logworkHours = list.reduce((s, e) => s + e.logworkHours, 0);
      const hdHours = hdMM * perMM;
      const actualHours = actualMM * perMM;
      const eeMonth = ee(actualHours, hdHours);
      const year = month.slice(0, 4);
      const ytd = ytdByYear.get(year) ?? { hd: 0, actual: 0, logwork: 0 };
      if (eeMonth !== null) {
        ytd.hd += hdHours;
        ytd.actual += actualHours;
        ytd.logwork += logworkHours;
      }
      ytdByYear.set(year, ytd);
      const eeYtd = ee(ytd.actual, ytd.hd);
      return {
        month, hoursPerMM: perMM, hdMM: round2(hdMM), actualMM: round2(actualMM), hdHours: round2(hdHours), actualHours: round2(actualHours), logworkHours: round2(logworkHours),
        eeMonth, eeMonthTone: eeTone(eeMonth, r), hdYtdHours: round2(ytd.hd), actualYtdHours: round2(ytd.actual), logworkYtdHours: round2(ytd.logwork), eeYtd, eeYtdTone: eeTone(eeYtd, r),
      };
    });
    const chartMonth = monthParam && months.includes(monthParam) ? monthParam : months.at(-1) ?? null;
    // Biểu đồ từng người trong tháng: mỗi ngày 1 điểm luỹ kế từ ngày 1; HĐ/Actual chia đều cho ngày T2–T6 (MM × 8h/ngày), Logwork theo giờ log
    const members = chartMonth
      ? entries.filter((e) => e.month === chartMonth).sort((a, b) => a.fullName.localeCompare(b.fullName, 'vi')).map((e) => {
        const cum = { hd: 0, actual: 0, logwork: 0 };
        const points = daysOfMonth(chartMonth).map((d) => {
          const working = !isWeekend(d);
          cum.hd += working ? e.hdMM * STANDARD : 0;
          cum.actual += working ? e.actualMM * STANDARD : 0;
          cum.logwork += e.logDaily.get(d) ?? 0;
          return { date: d, hd: round2(cum.hd), actual: round2(cum.actual), logwork: round2(cum.logwork) };
        });
        const value = ee(cum.actual, cum.hd);
        return { employeeId: e.employeeId, fullName: e.fullName, code: e.code, position: e.position, points, totals: { hdHours: round2(cum.hd), actualHours: round2(cum.actual), logworkHours: round2(cum.logwork), ee: value, eeTone: eeTone(value, r) } };
      })
      : [];
    return { months: monthly, chartMonth, members, thresholds: { greenMax: r.eeGreenMax, orangeMax: r.eeOrangeMax } };
  }

  // ─── Defect / Leakage ────────────────────────────────────────────────────

  /**
   * Tỷ lệ lỗi theo tháng + luỹ kế năm (port computeQualityRows):
   * - ratePerSize = số lỗi / kích thước sản phẩm (vd KLOC); ratePerMM = số lỗi / MM tính tiền (Σ Actual ở Công số của tháng) — không × 100;
   * - luỹ kế chỉ cộng các tháng đã nhập (tháng chưa nhập không cộng cả MM tính tiền);
   * - mẫu số = 0 → null. Ví dụ: 12 lỗi, 6 MM → 2 defect/MM.
   * Khoảng tháng nhập được: từ tháng bắt đầu dự án tới min(tháng kết thúc, tháng hiện tại).
   */
  function qualityData(p, kind, yearParam) {
    const nowKey = today().slice(0, 7);
    const from = (p.start_date || `${nowKey.slice(0, 4)}-01`).slice(0, 7);
    const endKey = (p.end_date || nowKey).slice(0, 7);
    const to = endKey < nowKey ? endKey : nowKey;
    const years = [];
    for (let y = Number(from.slice(0, 4)); y <= Number(to.slice(0, 4)); y++) years.push(y);
    const year = years.includes(Number(yearParam)) ? Number(yearParam) : years.at(-1) ?? Number(nowKey.slice(0, 4));
    const months = [];
    for (let m = 1; m <= 12; m++) {
      const key = `${year}-${String(m).padStart(2, '0')}`;
      if (key >= from && key <= to) months.push(key);
    }
    // MM tính tiền của tháng = Σ Actual (Thực tế + OT) của dự án ở màn Công số
    const billable = (month) => round2(db.effort_allocations.filter((a) => a.project_id === p.id && !a.is_deleted && a.year_month.startsWith(month)).reduce((s, a) => s + Number(a.tt_effort || 0) + Number(a.ot_effort || 0), 0));
    const r = qualityRules(store);
    const ratio = (a, b) => (b > 0 ? Math.round((a / b) * 10000) / 10000 : null);
    const ytd = { defects: 0, size: 0, billable: 0 };
    const rows = months.map((month) => {
      const entry = db.project_quality_metrics.find((q) => q.project_id === p.id && q.kind === kind && q.metric_month.startsWith(month));
      const defects = entry?.defect_count ?? 0;
      const size = entry?.product_size ?? 0;
      const mm = billable(month);
      if (entry) {
        ytd.defects += defects;
        ytd.size += size;
        ytd.billable += mm;
      }
      const perMM = entry ? ratio(defects, mm) : null;
      const perMMYtd = ratio(ytd.defects, ytd.billable);
      return {
        month, hasData: !!entry, defects, productSize: size, productSizeUnit: entry?.product_size_unit ?? 'KLOC', billableMM: mm,
        ratePerSize: entry ? ratio(defects, size) : null, ratePerMM: perMM, ratePerMMTone: rateTone(perMM, kind, r),
        defectsYtd: ytd.defects, productSizeYtd: round2(ytd.size), billableYtd: round2(ytd.billable),
        ratePerSizeYtd: ratio(ytd.defects, ytd.size), ratePerMMYtd: perMMYtd, ratePerMMYtdTone: rateTone(perMMYtd, kind, r),
      };
    });
    const thresholds = kind === 'defect'
      ? { goodMin: r.defectGoodMin, goodMax: r.defectGoodMax, warnMax: r.defectWarnMax }
      : { goodMin: r.leakageGoodMin, goodMax: r.leakageGoodMax, warnMax: r.leakageWarnMax };
    // Đơn vị Product size chọn cho dự án (Point / Request / Module / KLOC) — lấy theo bản ghi gần nhất
    const unit = db.project_quality_metrics.filter((q) => q.project_id === p.id && q.kind === kind).sort((a, b) => b.metric_month.localeCompare(a.metric_month))[0]?.product_size_unit ?? 'Point';
    return { kind, year, years, from, to, unit, rows, thresholds };
  }

  // ─── Routes ──────────────────────────────────────────────────────────────

  const P = String.raw`^\/api\/v1\/projects\/[^/]+`;
  return [
    ['POST', /^\/api\/v1\/projects$/, async ({ req, culture }) => {
      const ctx = currentUser(req);
      requirePermission(ctx, 'project.create');
      const input = parseProject(await readJson(req), { isCreate: true });
      const planning = masterByCode('project_status', 'PLANNING');
      const p = {
        id: randomUUID(), tenant_id: ctx.user.tenant_id, code: nextProjectCode(), name: input.name, client_id: input.clientId, grade_id: input.gradeId,
        status_id: planning?.id ?? null, risk_level_id: masterByCode('risk_level', 'LOW')?.id ?? null, description: input.description,
        start_date: input.startDate, end_date: input.endDate, budget: input.budget ?? 0, currency_code: db.clients.find((c) => c.id === input.clientId)?.default_currency || 'USD',
        progress_pct: 0, contract_staff_fte: 0, actual_staff_fte: 0, actual_cost_fte: 0, is_deleted: false, created_at: nowIso(), updated_at: nowIso(),
      };
      db.projects.push(p);
      applyContractStaff(p, input.contractStaff);
      upsertLink(p, 'ticket', 'JIRA', 'Ticket', input.ticketSystemUrl);
      upsertLink(p, 'source', 'GITHUB', 'Source code', input.sourceCodeUrl);
      return { status: 201, key: 'project_create.msg_success', args: { name: p.name }, data: detailDto(p, ctx, true, culture) };
    }],
    // Import dự án từ Excel (port project-import.service.ts): mỗi dòng = 1 dự án + 1 vai trò hợp đồng; các dòng cùng tên dự án
    // gộp thành 1 dự án nhiều vai trò. Khách hàng / cấp độ / rủi ro / vai trò / cấp độ vai trò nhập theo tên hoặc mã danh mục.
    ['POST', /^\/api\/v1\/projects\/import$/, async ({ req, culture }) => {
      const ctx = currentUser(req);
      requirePermission(ctx, 'project.create');
      const body = await readJson(req);
      const rows = Array.isArray(body.rows) ? body.rows.slice(0, 5000) : [];
      const norm = (s) => str(s).toLowerCase();
      const byNameOrCode = (type, value) => {
        const v = norm(value);
        return v ? db.master_data.find((m) => m.type === type && m.is_active && (norm(m.name) === v || norm(m.name_en) === v || norm(m.code) === v)) : null;
      };
      const groups = new Map();
      for (const r of rows) {
        const key = norm(r.values?.name);
        if (!groups.has(key)) groups.set(key, []);
        groups.get(key).push(r);
      }
      let created = 0;
      const errors = [];
      const planning = masterByCode('project_status', 'PLANNING');
      for (const list of groups.values()) {
        const first = list[0].values ?? {};
        const rowErrors = [];
        const client = db.clients.find((c) => !c.is_deleted && (norm(c.name) === norm(first.client) || norm(c.code) === norm(first.client)));
        if (!client) rowErrors.push({ row: list[0].row, key: 'project_import.err_client_not_found', args: { value: first.client ?? '' } });
        const grade = first.grade ? byNameOrCode('project_level', first.grade) : db.master_data.find((m) => m.type === 'project_level' && m.code === 'B');
        if (first.grade && !grade) rowErrors.push({ row: list[0].row, key: 'project_import.err_grade_not_found', args: { value: first.grade } });
        const risk = first.risk ? byNameOrCode('risk_level', first.risk) : masterByCode('risk_level', 'LOW');
        if (first.risk && !risk) rowErrors.push({ row: list[0].row, key: 'project_import.err_risk_not_found', args: { value: first.risk } });
        const staff = list.filter((r) => str(r.values?.role)).map((r) => {
          const role = byNameOrCode('job_position', r.values.role);
          const level = byNameOrCode('hr_level', r.values.roleLevel) ?? masterByCode('hr_level', 'JUNIOR');
          if (!role) rowErrors.push({ row: r.row, key: 'project_import.err_role_not_found', args: { value: r.values.role } });
          return { roleCode: role?.code ?? '', levelId: level?.id ?? '', quantity: r.values.quantity || 1, ftePerPerson: r.values.fte || 1, startDate: r.values.roleStart || '', endDate: r.values.roleEnd || '', pricePerMonth: r.values.price || 0 };
        });
        if (rowErrors.length === 0) {
          try {
            const input = parseProject({
              name: first.name, clientId: client.id, gradeId: grade?.id, description: first.description, startDate: first.startDate, endDate: first.endDate,
              budget: first.budget, ticketSystemUrl: first.ticketSystem, sourceCodeUrl: first.sourceCode,
              contractStaff: staff.length > 0 ? staff : [{ roleCode: 'PM', levelId: masterByCode('hr_level', 'SENIOR')?.id, quantity: 1, ftePerPerson: 0.5, startDate: '', endDate: '', pricePerMonth: 0 }],
            }, { isCreate: true });
            const p = {
              id: randomUUID(), tenant_id: ctx.user.tenant_id, code: nextProjectCode(), name: input.name, client_id: input.clientId, grade_id: input.gradeId,
              status_id: planning?.id ?? null, risk_level_id: risk?.id ?? null, description: input.description, start_date: input.startDate, end_date: input.endDate,
              budget: input.budget ?? 0, currency_code: client.default_currency || 'USD', progress_pct: 0, contract_staff_fte: 0, actual_staff_fte: 0, actual_cost_fte: 0,
              is_deleted: false, created_at: nowIso(), updated_at: nowIso(),
            };
            db.projects.push(p);
            applyContractStaff(p, input.contractStaff);
            upsertLink(p, 'ticket', 'JIRA', 'Ticket', input.ticketSystemUrl);
            upsertLink(p, 'source', 'GITHUB', 'Source code', input.sourceCodeUrl);
            created += 1;
          } catch (err) {
            if (!(err instanceof ApiError) || err.status !== 400) throw err;
            for (const [field, list2] of Object.entries(err.data ?? {})) {
              const staffIndex = /^contractStaff\.(\d+)\./.exec(field)?.[1];
              const row = staffIndex !== undefined ? list[Number(staffIndex)]?.row ?? list[0].row : list[0].row;
              for (const e of list2) rowErrors.push({ row, field, ...(typeof e === 'string' ? { key: e } : e) });
            }
          }
        }
        for (const e of rowErrors) errors.push(e);
      }
      const localized = errors.map(({ row, field, key, args }) => ({ row, field: field ?? null, message: localize(key, culture, args) }));
      return { key: 'project_import.msg_success', args: { count: created }, data: { created, errors: localized } };
    }],
    ['GET', new RegExp(`${P}$`), async ({ req, url, culture }) => {
      const { ctx, p, canManage } = openProject(req, url);
      return { data: detailDto(p, ctx, canManage, culture) };
    }],
    ['PUT', new RegExp(`${P}$`), async ({ req, url, culture }) => {
      const { ctx, p } = manageProject(req, url);
      const input = parseProject(await readJson(req), { isCreate: false });
      applyContractStaff(p, input.contractStaff);
      Object.assign(p, {
        name: input.name, client_id: input.clientId, grade_id: input.gradeId, status_id: input.statusId, description: input.description,
        start_date: input.startDate, end_date: input.endDate, budget: input.budget ?? 0, updated_at: nowIso(),
      });
      upsertLink(p, 'ticket', 'JIRA', 'Ticket', input.ticketSystemUrl);
      upsertLink(p, 'source', 'GITHUB', 'Source code', input.sourceCodeUrl);
      return { key: 'project_update.msg_success', args: { name: p.name }, data: detailDto(p, ctx, true, culture) };
    }],
    ['DELETE', new RegExp(`${P}$`), async ({ req, url }) => {
      const { p } = manageProject(req, url);
      p.is_deleted = true;
      p.updated_at = nowIso();
      return { key: 'project_delete.msg_success', args: { name: p.name } };
    }],
    ['POST', new RegExp(`${P}/restore$`), async ({ req, url }) => {
      const ctx = currentUser(req);
      requirePermission(ctx, 'project.manage');
      const p = findProject(idFromPath(url, -2));
      p.is_deleted = false;
      p.updated_at = nowIso();
      return { key: 'project_restore.msg_success', args: { name: p.name } };
    }],

    // Phân bổ nhân sự + đường HĐ/KH/TT theo ngày
    ['GET', new RegExp(`${P}/staffing$`), async ({ req, url, culture }) => {
      const { ctx, p, canManage } = openProject(req, url);
      return { data: staffing(p, ctx, canManage, culture) };
    }],
    // Nhân sự chọn được khi phân bổ: còn làm việc (chưa xoá, chưa nghỉ hẳn), kèm % phân bổ hiện tại và ngày nghỉ (nếu có)
    ['GET', new RegExp(`${P}/assignable-employees$`), async ({ req, url, culture }) => {
      manageProject(req, url);
      const now = today();
      const rows = db.employees
        .filter((e) => !e.is_deleted && e.is_active && (!e.left_date || e.left_date > now))
        .sort((a, b) => a.full_name.localeCompare(b.full_name, 'vi'))
        .map((e) => ({ ...employeeRef(e, null), level: store.masterRef(e.level_id, culture)?.name ?? null }));
      return { data: rows };
    }],
    ['POST', new RegExp(`${P}/assignments$`), async ({ req, url }) => {
      const { p } = manageProject(req, url);
      const body = await readJson(req);
      const { errors, intervals } = parseIntervals(body, p);
      const employeeId = str(body.employeeId);
      const employee = employeeById(employeeId);
      if (!employeeId) addError(errors, 'employeeId', 'validation.required');
      else if (!employee || employee.is_deleted) addError(errors, 'employeeId', 'validation.invalid_value');
      const contractStaffId = str(body.contractStaffId) || null;
      const cs = contractStaffId ? db.contract_staff.find((x) => x.id === contractStaffId && x.project_id === p.id && !x.is_deleted) : null;
      if (contractStaffId && !cs) addError(errors, 'contractStaffId', 'validation.invalid_value');
      const slotIndex = Number(body.slotIndex ?? 0);
      if (cs && (!Number.isInteger(slotIndex) || slotIndex < 0 || slotIndex >= cs.quantity)) addError(errors, 'slotIndex', 'validation.invalid_value');
      const roleInProject = cs ? cs.role_code : str(body.roleInProject);
      if (!cs && !masterByCode('job_position', roleInProject)) addError(errors, 'roleInProject', 'validation.required');
      throwIfErrors(errors);
      // Một người chỉ giữ một vị trí trong dự án (bàn giao thì sửa vị trí cũ)
      if (liveAssignments(p.id).some((a) => a.employee_id === employeeId)) throw new ApiError(409, 'project_assignment.err_already_assigned');
      const a = newAssignment(p, { employeeId, contractStaffId: cs?.id ?? null, slotIndex: cs ? slotIndex : 0, roleInProject, levelId: cs?.level_id ?? employee.level_id });
      writeAssignment(a, intervals);
      return { status: 201, key: 'project_assignment_create.msg_success', args: { name: employee.full_name } };
    }],
    ['PUT', new RegExp(`${P}/assignments/[^/]+$`), async ({ req, url }) => {
      const { p } = manageProject(req, url, -3);
      const a = liveAssignments(p.id).find((x) => x.id === idFromPath(url));
      if (!a) throw notFound();
      const body = await readJson(req);
      const { errors, intervals } = parseIntervals(body, p);
      const employeeId = str(body.employeeId) || a.employee_id;
      const employee = employeeById(employeeId);
      if (!employee || employee.is_deleted) addError(errors, 'employeeId', 'validation.invalid_value');
      const mode = ['update', 'handover', 'replace'].includes(body.mode) ? body.mode : 'update';
      const handoverDate = mode === 'handover' && employeeId !== a.employee_id ? validDate(errors, 'handoverDate', str(body.handoverDate)) : null;
      if (handoverDate && handoverDate < (schedulesOf(a.id)[0]?.start_date ?? a.start_date)) addError(errors, 'handoverDate', 'project_assignment.err_handover_before_start');
      throwIfErrors(errors);
      if (employeeId !== a.employee_id && liveAssignments(p.id).some((x) => x.employee_id === employeeId)) throw new ApiError(409, 'project_assignment.err_already_assigned');
      if (employeeId === a.employee_id || mode === 'update') {
        a.employee_id = employeeId;
        writeAssignment(a, intervals);
        return { key: 'project_assignment_update.msg_success', args: { name: employee.full_name } };
      }
      if (mode === 'replace') {
        // Thay thế hoàn toàn (sửa nhập sai): xoá lịch sử phân bổ của người cũ ở vị trí này
        a.is_deleted = true;
      } else {
        // Bàn giao: người cũ làm đến hết ngày bàn giao, người mới nhận từ ngày kế tiếp; giai đoạn sau ngày bàn giao của người cũ bị bỏ
        const kept = schedulesOf(a.id).filter((s) => s.start_date <= handoverDate).map((s) => ({ startDate: s.start_date, endDate: s.end_date > handoverDate ? handoverDate : s.end_date, fte: s.fte_allocated }));
        if (kept.length > 0) writeAssignment(a, kept);
        else a.is_deleted = true;
        const from = addDays(handoverDate, 1);
        for (const it of intervals) if (it.startDate < from) it.startDate = from;
        const valid = intervals.filter((it) => it.endDate >= it.startDate);
        if (valid.length === 0) throw new ApiError(400, 'error.validation_failed', { handoverDate: ['project_assignment.err_handover_after_end'] });
        intervals.splice(0, intervals.length, ...valid);
      }
      const b = newAssignment(p, { employeeId, contractStaffId: a.contract_staff_id, slotIndex: a.slot_index, roleInProject: a.role_in_project, levelId: a.level_id });
      writeAssignment(b, intervals);
      return { key: mode === 'handover' ? 'project_assignment_handover.msg_success' : 'project_assignment_update.msg_success', args: { name: employee.full_name } };
    }],
    ['DELETE', new RegExp(`${P}/assignments/[^/]+$`), async ({ req, url }) => {
      const { p } = manageProject(req, url, -3);
      const a = liveAssignments(p.id).find((x) => x.id === idFromPath(url));
      if (!a) throw notFound();
      // Bỏ gán 1 giai đoạn (?intervalId=) hoặc cả phân bổ; nhật ký công việc đã ghi vẫn giữ nguyên
      const intervalId = url.searchParams.get('intervalId');
      const rest = intervalId ? schedulesOf(a.id).filter((s) => s.id !== intervalId) : [];
      if (rest.length > 0) writeAssignment(a, rest.map((s) => ({ startDate: s.start_date, endDate: s.end_date, fte: s.fte_allocated })));
      else a.is_deleted = true;
      return { key: 'project_assignment_delete.msg_success', args: { name: employeeById(a.employee_id)?.full_name ?? '' } };
    }],

    // Milestone
    ['GET', new RegExp(`${P}/milestones$`), async ({ req, url }) => {
      const { p, canManage } = openProject(req, url);
      const errors = {};
      const from = validDate(errors, 'from', url.searchParams.get('from') || '', { optional: true });
      const to = validDate(errors, 'to', url.searchParams.get('to') || '', { optional: true });
      throwIfErrors(errors);
      return { data: { ...milestonesDto(p, { from, to }), canEdit: canManage && !p.is_deleted } };
    }],
    // Sửa nhanh ngày bắt đầu / kết thúc dự án từ Timeline toàn dự án
    ['PATCH', new RegExp(`${P}/dates$`), async ({ req, url }) => {
      const { p } = manageProject(req, url);
      const body = await readJson(req);
      const errors = {};
      const startDate = validDate(errors, 'startDate', str(body.startDate));
      const endDate = validDate(errors, 'endDate', str(body.endDate));
      if (startDate && endDate && endDate < startDate) addError(errors, 'endDate', 'project_save.err_end_before_start');
      throwIfErrors(errors);
      Object.assign(p, { start_date: startDate, end_date: endDate, updated_at: nowIso() });
      return { key: 'project_dates_update.msg_success' };
    }],
    ['POST', new RegExp(`${P}/milestones$`), async ({ req, url }) => {
      const { ctx, p } = manageProject(req, url);
      const input = parseMilestone(await readJson(req));
      const seq = db.project_milestones.filter((m) => m.project_id === p.id).reduce((s, m) => Math.max(s, m.sort_seq), 0) + 1;
      db.project_milestones.push({
        id: randomUUID(), tenant_id: p.tenant_id, project_id: p.id, name: input.name, sort_seq: seq, planned_date: input.plannedDate, actual_date: input.actualDate,
        justification: input.justification, justification_by: input.justification ? ctx.user.id : null, justification_at: input.justification ? nowIso() : null, is_deleted: false,
      });
      return { status: 201, key: 'project_milestone_create.msg_success', args: { name: input.name } };
    }],
    ['PUT', new RegExp(`${P}/milestones/[^/]+$`), async ({ req, url }) => {
      const { ctx, p } = manageProject(req, url, -3);
      const m = db.project_milestones.find((x) => x.id === idFromPath(url) && x.project_id === p.id && !x.is_deleted);
      if (!m) throw notFound();
      const input = parseMilestone(await readJson(req), m);
      const justificationChanged = (input.justification ?? null) !== (m.justification ?? null);
      Object.assign(m, { name: input.name, planned_date: input.plannedDate, actual_date: input.actualDate, justification: input.justification });
      if (justificationChanged) Object.assign(m, { justification_by: input.justification ? ctx.user.id : null, justification_at: input.justification ? nowIso() : null });
      return { key: 'project_milestone_update.msg_success', args: { name: m.name } };
    }],
    ['DELETE', new RegExp(`${P}/milestones/[^/]+$`), async ({ req, url }) => {
      const { p } = manageProject(req, url, -3);
      const m = db.project_milestones.find((x) => x.id === idFromPath(url) && x.project_id === p.id && !x.is_deleted);
      if (!m) throw notFound();
      m.is_deleted = true;
      return { key: 'project_milestone_delete.msg_success', args: { name: m.name } };
    }],

    // EE
    ['GET', new RegExp(`${P}/ee$`), async ({ req, url }) => {
      const { ctx, p, canManage } = openProject(req, url);
      const month = url.searchParams.get('month');
      if (month && !MONTH_PATTERN.test(month)) throw new ApiError(400, 'error.validation_failed', { month: ['validation.invalid_value'] });
      return { data: eeData(p, ctx, canManage, month) };
    }],

    // Defect / Leakage
    ['GET', new RegExp(`${P}/quality$`), async ({ req, url }) => {
      const { p, canManage, ctx } = openProject(req, url);
      const kind = url.searchParams.get('kind') === 'leakage' ? 'leakage' : 'defect';
      return { data: { ...qualityData(p, kind, url.searchParams.get('year')), canEdit: (canManage || ctx.permissions.includes('report.quality')) && !p.is_deleted } };
    }],
    ['PUT', new RegExp(`${P}/quality$`), async ({ req, url }) => {
      const { ctx, p, canManage } = openProject(req, url);
      if (!canManage && !ctx.permissions.includes('report.quality')) throw forbidden();
      const body = await readJson(req);
      const kind = body.kind === 'leakage' ? 'leakage' : 'defect';
      const entries = Array.isArray(body.entries) ? body.entries : [];
      const range = qualityData(p, kind, null);
      const errors = {};
      entries.forEach((e, i) => {
        if (!MONTH_PATTERN.test(String(e.month)) || e.month < range.from || e.month > range.to) addError(errors, `entries.${i}.month`, 'validation.invalid_value');
        if (e.defects !== null && e.defects !== '') numberInRange(errors, `entries.${i}.defects`, e.defects, 0, 100000, { integer: true });
        if (e.productSize !== null && e.productSize !== '' && e.productSize !== undefined) numberInRange(errors, `entries.${i}.productSize`, e.productSize, 0, 1000000);
      });
      const unit = str(body.unit);
      if (unit && !PRODUCT_SIZE_UNITS.includes(unit)) addError(errors, 'unit', 'validation.invalid_value');
      throwIfErrors(errors);
      if (unit) for (const q of db.project_quality_metrics.filter((x) => x.project_id === p.id && x.kind === kind)) q.product_size_unit = unit;
      for (const e of entries) {
        const existing = db.project_quality_metrics.find((q) => q.project_id === p.id && q.kind === kind && q.metric_month.startsWith(e.month));
        if (e.defects === null || e.defects === '') {
          // Ô số lỗi để trống: xoá tháng đã có, bỏ qua tháng chưa có
          if (existing) db.project_quality_metrics.splice(db.project_quality_metrics.indexOf(existing), 1);
          continue;
        }
        const fields = { defect_count: Number(e.defects), product_size: Number(e.productSize || 0), product_size_unit: unit || existing?.product_size_unit || range.unit };
        if (existing) Object.assign(existing, fields);
        else db.project_quality_metrics.push({ id: randomUUID(), tenant_id: p.tenant_id, project_id: p.id, metric_month: `${e.month}-01`, kind, ...fields });
      }
      return { key: 'project_quality_save.msg_success', data: qualityData(p, kind, body.year) };
    }],
  ];
}
