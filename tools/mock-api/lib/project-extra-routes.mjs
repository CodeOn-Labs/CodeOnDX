// Endpoint các tab con của Chi tiết dự án: Metrics › PCV, CSS; Quản trị dự án (tài liệu, rủi ro, thay đổi, issue);
// Báo cáo tháng; Báo cáo tình trạng dự án. Số liệu tổng hợp (điểm, mức, đếm) do BE tính sẵn (Q2), công thức có comment,
// ngưỡng màu lấy từ Quản trị → Quy tắc (module quality, D12).
import { randomBytes, randomUUID } from 'node:crypto';
import { ApiError, forbidden, notFound, readJson } from './http.mjs';
import { today } from './store.mjs';
import { addError, idFromPath, maxLength, nowIso, numberInRange, str, throwIfErrors, TIME_PATTERN, validDate } from './validate.mjs';
import { daysOfMonth, isWeekend, qualityRules } from './project-routes.mjs';

const MONTH_PATTERN = /^\d{4}-\d{2}$/;
const URL_PATTERN = /^https?:\/\/\S+$/i;
const round2 = (n) => Math.round(n * 100) / 100;

/** Mức PCV: điểm ≥ ngưỡng xanh (mặc định 90%) → green; ≥ ngưỡng cam (70%) → orange; thấp hơn → red. */
export const pcvTone = (score, r) => (score >= r.pcvGreenMin ? 'green' : score >= r.pcvOrangeMin ? 'orange' : 'red');
/** Mức CSS (thang 1–5): ≥ ngưỡng xanh (mặc định 4.5) → green; ≥ ngưỡng cam (3.5) → orange; thấp hơn → red. */
export const cssTone = (score, r) => (score >= r.cssGreenMin ? 'green' : score >= r.cssOrangeMin ? 'orange' : 'red');

/**
 * PCV (Process Compliance Verification) = số tiêu chí Đạt / (Đạt + Không đạt) × 100.
 * Tiêu chí chưa chọn không có trong kết quả nên tự bị loại khỏi cả tử số lẫn mẫu số. Ví dụ: 18 Đạt, 2 Không đạt → 90%.
 */
export function summarizePcv(results) {
  const checked = results.length;
  const passed = results.filter((r) => r.passed).length;
  return { checked, passed, failed: checked - passed, score: checked > 0 ? round2((passed / checked) * 100) : 0 };
}

/**
 * CSS (Customer Satisfaction Score) = tổng điểm các câu khách đã chấm / số câu đã chấm (thang 1–5).
 * Câu không bắt buộc bị bỏ trống không tính (không tính là 0). Ví dụ: 5, 4, 5, (bỏ trống), 5 → 19 / 4 = 4.75.
 */
export function summarizeCss(scores) {
  const answered = scores.filter((s) => s.score !== null && s.score !== undefined);
  const total = answered.reduce((sum, s) => sum + Number(s.score), 0);
  return { answered: answered.length, score: answered.length > 0 ? round2(total / answered.length) : 0 };
}

/**
 * Thang rủi ro (port risk.ts): điểm = xác suất (1–5) × ảnh hưởng (1–5) = 1–25;
 * mức: 1–4 Thấp · 5–9 Trung bình · 10–16 Cao · 20–25 Nghiêm trọng. Điểm và mức không lưu DB, đổi ngưỡng ở đây là đổi toàn hệ.
 * Rủi ro đang theo dõi = open / in_progress; quá hạn = đang theo dõi và hạn xử lý < hôm nay.
 */
export const riskScore = (r) => r.probability * r.impact;
export const riskLevel = (score) => (score >= 20 ? 'critical' : score >= 10 ? 'high' : score >= 5 ? 'medium' : 'low');
export const isRiskActive = (status) => status === 'open' || status === 'in_progress';
const RISK_CATEGORIES = ['schedule', 'technical', 'resource', 'customer', 'cost', 'quality'];
const RISK_STRATEGIES = ['avoid', 'mitigate', 'transfer', 'accept'];
const RISK_STATUSES = ['open', 'in_progress', 'resolved', 'closed'];
const CR_SOURCES = ['customer', 'internal'];
const CR_LEVELS = ['low', 'medium', 'high'];
const CR_AREAS = ['scope', 'schedule', 'cost', 'quality'];
const ISSUE_TYPES = ['parent', 'sprint', 'milestone'];
const ISSUE_KINDS = ['task', 'bug'];
const REPORT_STATUSES = ['GOOD', 'RISK', 'SOS'];

/** Mã kế tiếp dạng CR-01, R-07 theo max + 1 trên mọi mã đã có (kể cả bản ghi đã xoá) — mã không tái dùng. */
export function nextSequenceCode(prefix, codes) {
  const pattern = new RegExp(`^${prefix}-(\\d+)$`);
  const max = codes.reduce((m, c) => Math.max(m, Number(pattern.exec(String(c).trim())?.[1] ?? 0)), 0);
  return `${prefix}-${String(max + 1).padStart(2, '0')}`;
}

/**
 * @param {{ store: ReturnType<import('./store.mjs').createStore>, currentUser: Function, requirePermission: Function }} deps
 */
export function projectExtraRoutes({ store, currentUser, requirePermission }) {
  const { db } = store;
  for (const t of ['project_reports', 'monthly_reports', 'monthly_report_cells']) db[t] ??= [];
  const userName = (id) => db.users.find((u) => u.id === id)?.full_name ?? null;
  const employeeById = (id) => db.employees.find((e) => e.id === id);

  function openProject(req, url, index = 4) {
    const ctx = currentUser(req);
    requirePermission(ctx, 'project.view');
    const p = db.projects.find((x) => x.id === idFromPath(url, index));
    if (!p) throw notFound();
    const canManage = ctx.permissions.includes('project.manage');
    const member = !!ctx.user.employee_id && db.assignments.some((a) => a.project_id === p.id && a.employee_id === ctx.user.employee_id && !a.is_deleted);
    if (!canManage && !member) throw new ApiError(403, 'project_detail.err_no_access');
    return { ctx, p, canManage };
  }
  /** Ghi: cần quyền `permission` (mặc định project.manage) và dự án chưa bị xoá. */
  function writeProject(req, url, index, permission = 'project.manage') {
    const o = openProject(req, url, index);
    if (!o.ctx.permissions.includes(permission)) throw forbidden();
    if (o.p.is_deleted) throw new ApiError(409, 'project_detail.err_deleted');
    return o;
  }
  const audit = (ctx) => ({ created_at: nowIso(), updated_at: nowIso(), created_by: ctx.user.id, updated_by: ctx.user.id, is_deleted: false });
  const touched = (row, ctx) => Object.assign(row, { updated_at: nowIso(), updated_by: ctx.user.id });
  const auditDto = (r) => ({ createdAt: r.created_at, updatedAt: r.updated_at, createdByName: userName(r.created_by), updatedByName: userName(r.updated_by) });
  const monthParam = (url) => {
    const m = url.searchParams.get('month') || today().slice(0, 7);
    if (!MONTH_PATTERN.test(m)) throw new ApiError(400, 'error.validation_failed', { month: ['validation.invalid_value'] });
    return m;
  };
  function urlField(errors, field, value, { required = true } = {}) {
    if (!value) {
      if (required) addError(errors, field, 'validation.required');
      return;
    }
    if (!URL_PATTERN.test(value)) addError(errors, field, 'validation.invalid_url');
    maxLength(errors, field, value, 1000);
  }
  function textField(errors, field, value, max, { required = true } = {}) {
    if (!value && required) addError(errors, field, 'validation.required');
    maxLength(errors, field, value, max);
  }
  function oneOf(errors, field, value, list) {
    if (!list.includes(value)) addError(errors, field, 'validation.invalid_value');
  }

  // ─── PCV ─────────────────────────────────────────────────────────────────

  function pcvData(p, ctx, month) {
    const r = qualityRules(store);
    const items = db.project_pcv_items.filter((i) => i.project_id === p.id).sort((a, b) => a.sort_seq - b.sort_seq);
    const assessments = db.project_pcv_assessments.filter((a) => a.project_id === p.id).sort((a, b) => b.assess_month.localeCompare(a.assess_month));
    const resultsOf = (a) => db.project_pcv_results.filter((x) => x.assessment_id === a.id && x.result).map((x) => ({ itemId: x.item_id, passed: x.result === 'pass', note: x.error_note ?? null }));
    const current = assessments.find((a) => a.assess_month.startsWith(month));
    const currentResults = current ? resultsOf(current) : [];
    const saved = new Set(currentResults.map((x) => x.itemId));
    const summary = summarizePcv(currentResults);
    return {
      month, isFuture: month > today().slice(0, 7), canEdit: ctx.permissions.includes('pcv.manage') && !p.is_deleted && month <= today().slice(0, 7),
      projectGrade: store.masterRef(p.grade_id)?.name ?? null,
      // Tiêu chí đã xoá vẫn hiện (chỉ đọc) nếu tháng đang xem đã lưu kết quả cho nó — KPI khớp bảng lịch sử
      items: items.filter((i) => !i.is_deleted || saved.has(i.id)).map((i) => ({ id: i.id, category: i.group_name, label: i.content, isDeleted: !!i.is_deleted, sortSeq: i.sort_seq })),
      assessment: current ? { assessedAt: current.assessed_at, assessorName: userName(current.assessed_by), results: currentResults } : null,
      summary: { ...summary, total: items.filter((i) => !i.is_deleted || saved.has(i.id)).length, tone: summary.checked > 0 ? pcvTone(summary.score, r) : 'none' },
      history: assessments.map((a) => {
        const s = summarizePcv(resultsOf(a));
        return { month: a.assess_month.slice(0, 7), ...s, tone: s.checked > 0 ? pcvTone(s.score, r) : 'none', assessorName: userName(a.assessed_by), assessedAt: a.assessed_at };
      }),
      thresholds: { greenMin: r.pcvGreenMin, orangeMin: r.pcvOrangeMin },
    };
  }
  function parsePcvItem(body) {
    const errors = {};
    const category = str(body.category);
    const label = str(body.label);
    textField(errors, 'category', category, 100);
    textField(errors, 'label', label, 500);
    throwIfErrors(errors);
    return { category, label };
  }

  // ─── CSS ─────────────────────────────────────────────────────────────────

  function cssData(p, ctx, month) {
    const r = qualityRules(store);
    const form = store.projectCssForm(p.id);
    const questions = store.projectCssQuestions(p.id);
    const surveys = db.project_css_surveys.filter((s) => s.project_id === p.id && !s.is_revoked).sort((a, b) => b.survey_month.localeCompare(a.survey_month));
    const current = surveys.find((s) => s.survey_month.startsWith(month));
    const summary = current?.status === 'submitted' ? summarizeCss(current.scores ?? []) : null;
    return {
      month, isFuture: month > today().slice(0, 7), canEdit: ctx.permissions.includes('css.manage') && !p.is_deleted,
      form: { title: form.title, intro: form.intro ?? '', scaleNote: form.scale_note ?? '', commentLabel: form.comment_label ?? '' },
      questions: questions.map((q) => ({ id: q.id, label: q.content, required: q.is_required, sortSeq: q.sort_seq })),
      survey: current ? {
        status: current.status, token: current.token, sentAt: current.sent_at ?? null, senderName: userName(current.sent_by),
        submittedAt: current.submitted_at ?? null, respondent: current.respondent ?? null, comment: current.comment ?? null,
        answers: (current.scores ?? []).map((s) => ({ question: s.question, required: s.required ?? true, score: s.score })),
      } : null,
      summary: summary ? { ...summary, tone: cssTone(summary.score, r) } : null,
      history: surveys.map((s) => {
        const sum = s.status === 'submitted' ? summarizeCss(s.scores ?? []) : null;
        return { month: s.survey_month.slice(0, 7), status: s.status, score: sum?.score ?? null, answered: sum?.answered ?? 0, tone: sum ? cssTone(sum.score, r) : 'none', submittedAt: s.submitted_at ?? null, respondent: s.respondent ?? null };
      }),
      thresholds: { greenMin: r.cssGreenMin, orangeMin: r.cssOrangeMin },
    };
  }

  // ─── Quản trị dự án ──────────────────────────────────────────────────────

  const docGroupsOf = (p) => db.project_document_groups.filter((g) => g.project_id === p.id && !g.is_deleted).sort((a, b) => a.sort_seq - b.sort_seq);
  function documentsData(p) {
    const groups = docGroupsOf(p).map((g) => ({
      id: g.id, name: g.name, description: g.description ?? null, sortSeq: g.sort_seq, ...auditDto(g),
      documents: db.project_documents.filter((d) => d.group_id === g.id && !d.is_deleted).sort((a, b) => b.updated_at.localeCompare(a.updated_at)).map((d) => ({ id: d.id, groupId: g.id, name: d.name, url: d.url, ...auditDto(d) })),
    }));
    return { groups, summary: { groupCount: groups.length, documentCount: groups.reduce((s, g) => s + g.documents.length, 0) } };
  }

  function riskDto(r) {
    const score = riskScore(r);
    const owner = r.owner_id ? employeeById(r.owner_id) : null;
    return {
      id: r.id, code: r.code, name: r.name, description: r.description ?? null, category: r.category, probability: r.probability, impact: r.impact,
      score, level: riskLevel(score), strategy: r.strategy, responsePlan: r.response_plan ?? null, ownerId: r.owner_id ?? null, ownerName: owner?.full_name ?? null,
      dueDate: r.due_date, status: r.status, identifiedAt: r.identified_at, isActive: isRiskActive(r.status),
      isOverdue: isRiskActive(r.status) && !!r.due_date && r.due_date < today(), ...auditDto(r),
    };
  }
  function risksData(p) {
    const items = db.project_risks.filter((r) => r.project_id === p.id && !r.is_deleted).map(riskDto).sort((a, b) => b.score - a.score || a.code.localeCompare(b.code));
    const active = items.filter((r) => r.isActive);
    // Ma trận 5×5 chỉ đếm rủi ro đang theo dõi — key `${probability}-${impact}`
    const matrix = {};
    for (const r of active) matrix[`${r.probability}-${r.impact}`] = (matrix[`${r.probability}-${r.impact}`] ?? 0) + 1;
    return {
      items, matrix,
      summary: { total: items.length, active: active.length, highOrCritical: active.filter((r) => r.score >= 10).length, overdue: items.filter((r) => r.isOverdue).length, resolved: items.filter((r) => !r.isActive).length },
      owners: [...new Set(db.assignments.filter((a) => a.project_id === p.id && !a.is_deleted).map((a) => a.employee_id))].map(employeeById).filter(Boolean).map((e) => ({ id: e.id, fullName: e.full_name, code: e.code })),
    };
  }
  function parseRisk(body) {
    const errors = {};
    const name = str(body.name);
    textField(errors, 'name', name, 200);
    const description = str(body.description);
    maxLength(errors, 'description', description, 2000);
    oneOf(errors, 'category', body.category, RISK_CATEGORIES);
    const probability = numberInRange(errors, 'probability', body.probability, 1, 5, { integer: true });
    const impact = numberInRange(errors, 'impact', body.impact, 1, 5, { integer: true });
    oneOf(errors, 'strategy', body.strategy, RISK_STRATEGIES);
    const responsePlan = str(body.responsePlan);
    maxLength(errors, 'responsePlan', responsePlan, 2000);
    const ownerId = str(body.ownerId) || null;
    if (ownerId && !employeeById(ownerId)) addError(errors, 'ownerId', 'validation.invalid_value');
    const dueDate = validDate(errors, 'dueDate', str(body.dueDate));
    oneOf(errors, 'status', body.status, RISK_STATUSES);
    const identifiedAt = validDate(errors, 'identifiedAt', str(body.identifiedAt));
    if (identifiedAt && dueDate && dueDate < identifiedAt) addError(errors, 'dueDate', 'project_risk.err_due_before_identified');
    throwIfErrors(errors);
    return { name, description: description || null, category: body.category, probability, impact, strategy: body.strategy, response_plan: responsePlan || null, owner_id: ownerId, due_date: dueDate, status: body.status, identified_at: identifiedAt };
  }

  const changeDto = (c) => ({ id: c.id, code: c.code, fileName: c.file_name, url: c.url, source: c.source, impactLevel: c.impact_level, impactAreas: c.impact_areas, impactNote: c.impact_note ?? null, ...auditDto(c) });
  function changesData(p) {
    const items = db.project_change_requests.filter((c) => c.project_id === p.id && !c.is_deleted).sort((a, b) => b.code.localeCompare(a.code, undefined, { numeric: true })).map(changeDto);
    return { items, summary: { total: items.length, customer: items.filter((c) => c.source === 'customer').length, internal: items.filter((c) => c.source === 'internal').length, high: items.filter((c) => c.impactLevel === 'high').length } };
  }
  function parseChange(body) {
    const errors = {};
    const fileName = str(body.fileName);
    textField(errors, 'fileName', fileName, 200);
    const link = str(body.url);
    urlField(errors, 'url', link);
    oneOf(errors, 'source', body.source, CR_SOURCES);
    oneOf(errors, 'impactLevel', body.impactLevel, CR_LEVELS);
    const areas = Array.isArray(body.impactAreas) ? [...new Set(body.impactAreas)] : [];
    if (areas.length === 0) addError(errors, 'impactAreas', 'project_change.err_area_required');
    else if (areas.some((a) => !CR_AREAS.includes(a))) addError(errors, 'impactAreas', 'validation.invalid_value');
    const note = str(body.impactNote);
    maxLength(errors, 'impactNote', note, 1000);
    throwIfErrors(errors);
    return { file_name: fileName, url: link, source: body.source, impact_level: body.impactLevel, impact_areas: CR_AREAS.filter((a) => areas.includes(a)), impact_note: note || null };
  }

  const issueDto = (i) => ({ id: i.id, type: i.type, kind: i.kind, name: i.name, url: i.url, ...auditDto(i) });
  function issuesData(p) {
    const items = db.project_issue_links.filter((i) => i.project_id === p.id && !i.is_deleted).sort((a, b) => ISSUE_TYPES.indexOf(a.type) - ISSUE_TYPES.indexOf(b.type) || a.name.localeCompare(b.name, 'vi')).map(issueDto);
    return { items, summary: Object.fromEntries(ISSUE_TYPES.map((t) => [t, items.filter((i) => i.type === t).length])) };
  }
  function parseIssue(body) {
    const errors = {};
    oneOf(errors, 'type', body.type, ISSUE_TYPES);
    oneOf(errors, 'kind', body.kind, ISSUE_KINDS);
    const name = str(body.name);
    textField(errors, 'name', name, 200);
    const link = str(body.url);
    urlField(errors, 'url', link);
    throwIfErrors(errors);
    return { type: body.type, kind: body.kind, name, url: link };
  }

  /** CRUD chung cho 1 bảng quản trị dự án: list(p) → data; parse(body) → cột; extra(p) → cột thêm khi tạo. */
  function crud(segment, table, { list, parse, onCreate, labelOf, key }) {
    const base = String.raw`^\/api\/v1\/projects\/[^/]+\/${segment}`;
    const find = (p, url) => {
      const row = db[table].find((x) => x.id === idFromPath(url) && x.project_id === p.id && !x.is_deleted);
      if (!row) throw notFound();
      return row;
    };
    return [
      ['GET', new RegExp(`${base}$`), async ({ req, url }) => {
        const { p, canManage } = openProject(req, url);
        return { data: { ...list(p), canEdit: canManage && !p.is_deleted } };
      }],
      ['POST', new RegExp(`${base}$`), async ({ req, url }) => {
        const { ctx, p } = writeProject(req, url, 4);
        const row = { id: randomUUID(), tenant_id: p.tenant_id, project_id: p.id, ...parse(await readJson(req), p), ...audit(ctx) };
        onCreate?.(row, p);
        db[table].push(row);
        return { status: 201, key: `${key}_create.msg_success`, args: { name: labelOf(row) } };
      }],
      ['PUT', new RegExp(`${base}/[^/]+$`), async ({ req, url }) => {
        const { ctx, p } = writeProject(req, url, -3);
        const row = find(p, url);
        Object.assign(row, parse(await readJson(req), p));
        touched(row, ctx);
        return { key: `${key}_update.msg_success`, args: { name: labelOf(row) } };
      }],
      ['DELETE', new RegExp(`${base}/[^/]+$`), async ({ req, url }) => {
        const { ctx, p } = writeProject(req, url, -3);
        const row = find(p, url);
        row.is_deleted = true;
        touched(row, ctx);
        return { key: `${key}_delete.msg_success`, args: { name: labelOf(row) } };
      }],
    ];
  }

  // ─── Báo cáo tháng ───────────────────────────────────────────────────────

  const DEFAULT_COLUMNS = [
    { id: 'date', label: 'Ngày', label_ja: '日付', type: 'text', source: 'date' },
    { id: 'start', label: 'Bắt đầu', label_ja: '開始', type: 'text', source: 'checkIn' },
    { id: 'end', label: 'Kết thúc', label_ja: '終了', type: 'text', source: 'checkOut' },
    { id: 'hours', label: 'Số giờ', label_ja: '作業時間', type: 'number', source: 'hours' },
    { id: 'content', label: 'Nội dung công việc', label_ja: '作業内容', type: 'text', source: 'content' },
  ];
  const HEADER_FIELDS = { clientName: ['client_name', 200], companyName: ['company_name', 200], orderNo: ['order_no', 100], workName: ['work_name', 200], responsible: ['responsible', 200] };

  /** Nhân sự của báo cáo = người có giai đoạn phân bổ giao với tháng (không thêm / bớt tay), trừ người đã bị loại. */
  function membersInMonth(p, month) {
    const first = `${month}-01`;
    const last = daysOfMonth(month).at(-1);
    const ids = new Set();
    for (const a of db.assignments.filter((x) => x.project_id === p.id && !x.is_deleted)) {
      const schedules = db.project_assignment_schedules.filter((s) => s.assignment_id === a.id);
      if (schedules.some((s) => s.start_date <= last && s.end_date >= first)) ids.add(a.employee_id);
    }
    return [...ids].map(employeeById).filter(Boolean).sort((a, b) => a.full_name.localeCompare(b.full_name, 'vi'));
  }
  const findReport = (p, month) => db.monthly_reports.find((r) => r.project_id === p.id && r.report_month.startsWith(month));
  function draftReport(p, month) {
    const client = db.clients.find((c) => c.id === p.client_id);
    const pm = db.assignments.find((a) => a.project_id === p.id && !a.is_deleted && a.role_in_project === 'PM');
    const company = db.system_settings.find((s) => s.key === 'company_name')?.value ?? '';
    return {
      id: null, project_id: p.id, report_month: `${month}-01`, client_name: client?.name ?? '', company_name: company, order_no: '', work_name: p.name,
      responsible: pm ? employeeById(pm.employee_id)?.full_name ?? '' : '', language: 'vi', columns: DEFAULT_COLUMNS.map((c) => ({ ...c })), removed_employee_ids: [], updated_at: null, updated_by: null,
    };
  }
  /** Lần sửa đầu tiên mới ghi báo cáo (tháng chưa có báo cáo hiển thị bản nháp). */
  function ensureReport(p, month, ctx) {
    let r = findReport(p, month);
    if (!r) {
      r = { ...draftReport(p, month), id: randomUUID(), tenant_id: p.tenant_id };
      db.monthly_reports.push(r);
    }
    r.updated_at = nowIso();
    r.updated_by = ctx.user.id;
    return r;
  }
  const cellOf = (reportId, employeeId, date) => db.monthly_report_cells.find((c) => c.report_id === reportId && c.employee_id === employeeId && c.work_date === date);
  function ensureCell(r, employeeId, date) {
    let c = cellOf(r.id, employeeId, date);
    if (!c) {
      c = { report_id: r.id, employee_id: employeeId, work_date: date, hours: null, source: 'timesheet', start_time: null, end_time: null, time_source: 'attendance', content: '', custom: {} };
      db.monthly_report_cells.push(c);
    }
    return c;
  }

  /**
   * Dữ liệu báo cáo tháng. Ô giờ làm lấy từ Nhật ký công việc đã duyệt của dự án (tổng giờ / người / ngày), ô Bắt đầu / Kết thúc
   * lấy từ Chấm công vào/ra — trừ khi PO đã sửa tay ô đó (source / time_source = 'manual'). Tổng giờ của người = Σ giờ các ngày.
   */
  function reportData(p, month, canEdit) {
    const saved = findReport(p, month);
    const r = saved ?? draftReport(p, month);
    const removed = new Set(r.removed_employee_ids ?? []);
    const members = membersInMonth(p, month).filter((e) => !removed.has(e.id));
    const dates = daysOfMonth(month);
    const approved = new Map();
    for (const t of db.timesheets) {
      if (t.project_id !== p.id || t.status !== 'approved' || !t.work_date.startsWith(month)) continue;
      const k = `${t.employee_id}|${t.work_date}`;
      approved.set(k, (approved.get(k) ?? 0) + Number(t.hours || 0));
    }
    const attendance = new Map(db.attendance_records.filter((a) => a.work_date.startsWith(month)).map((a) => [`${a.employee_id}|${a.work_date}`, a]));
    let manualHours = 0;
    let manualTimes = 0;
    const memberDtos = members.map((e) => {
      let total = 0;
      const days = dates.map((d) => {
        const c = saved ? cellOf(saved.id, e.id, d) : null;
        const k = `${e.id}|${d}`;
        const att = attendance.get(k);
        const hoursManual = c?.source === 'manual';
        const timeManual = c?.time_source === 'manual';
        if (hoursManual) manualHours += 1;
        if (timeManual) manualTimes += 1;
        const hours = hoursManual ? c.hours : approved.has(k) ? round2(approved.get(k)) : null;
        total += Number(hours || 0);
        return {
          date: d, weekend: isWeekend(d), hours, hoursSource: hoursManual ? 'manual' : approved.has(k) ? 'timesheet' : null, hasLog: approved.has(k),
          start: timeManual ? c.start_time : att?.check_in ?? null, end: timeManual ? c.end_time : att?.check_out ?? null,
          timeSource: timeManual ? 'manual' : att ? 'attendance' : null, content: c?.content ?? '', custom: c?.custom ?? {},
        };
      });
      return { employeeId: e.id, fullName: e.full_name, code: e.code, totalHours: round2(total), days };
    });
    return {
      id: saved?.id ?? null, month, isDraft: !saved, canEdit,
      header: { clientName: r.client_name, companyName: r.company_name, orderNo: r.order_no, workName: r.work_name, responsible: r.responsible, language: r.language ?? 'vi' },
      columns: r.columns.map((c) => ({ id: c.id, label: c.label, labelJa: c.label_ja ?? '', type: c.type, source: c.source ?? null })),
      members: memberDtos, approvedLogDays: approved.size, manualHourDays: manualHours, manualTimeDays: manualTimes,
      updatedAt: r.updated_at ?? null, updatedByName: userName(r.updated_by),
    };
  }
  function parseColumn(body, r, currentId) {
    const errors = {};
    const label = str(body.label);
    textField(errors, 'label', label, 50);
    if (label && r.columns.some((c) => c.id !== currentId && c.label.toLowerCase() === label.toLowerCase())) addError(errors, 'label', 'monthly_report_column.err_duplicate');
    const labelJa = str(body.labelJa);
    maxLength(errors, 'labelJa', labelJa, 50);
    oneOf(errors, 'type', body.type ?? 'text', ['text', 'number']);
    throwIfErrors(errors);
    return { label, label_ja: labelJa, type: body.type ?? 'text' };
  }

  // ─── Báo cáo tình trạng dự án (nút "Report") ─────────────────────────────

  const FULL_TEMPLATE = ['Tiến độ', 'Chất lượng', 'Nhân sự', 'Kỹ thuật', 'Giao tiếp', 'Báo cáo kết thúc', 'Theo dõi issue/risk cũ'];
  /** AI (connector Ai ở chế độ mock) điền nội dung từ số liệu dự án: mốc trễ, rủi ro đang mở, cảnh báo nhân sự. */
  function aiFill(p) {
    const now = today();
    const overdue = db.project_milestones.filter((m) => m.project_id === p.id && !m.is_deleted && !m.actual_date && m.planned_date < now);
    const risks = db.project_risks.filter((r) => r.project_id === p.id && !r.is_deleted && isRiskActive(r.status));
    const high = risks.filter((r) => riskScore(r) >= 10);
    const status = high.length > 1 || overdue.length > 1 ? 'SOS' : high.length > 0 || overdue.length > 0 ? 'RISK' : 'GOOD';
    const items = [
      { metric: 'Tiến độ', content: overdue.length > 0 ? `- Đánh giá chung: NG\n- ${overdue.length} mốc đã quá hạn: ${overdue.map((m) => m.name).join(', ')}\n- Action: rà soát kế hoạch, thống nhất mốc mới với khách hàng` : '- Đánh giá chung: OK\n- Các mốc bàn giao đúng kế hoạch' },
      { metric: 'Chất lượng', content: '- Đánh giá chung: OK\n- Theo dõi Defect/Leakage ở tab Metrics' },
      { metric: 'Nhân sự', content: '- Đánh giá chung: OK\n- Xem cảnh báo biến động nhân sự ở tab Tổng quan (nếu có)' },
      { metric: 'Rủi ro', content: risks.length > 0 ? `- ${risks.length} rủi ro đang theo dõi, ${high.length} ở mức Cao/Nghiêm trọng\n${high.map((r) => `- ${r.code} ${r.name}: ${r.response_plan ?? ''}`).join('\n')}` : '- Không có rủi ro đang mở' },
    ];
    return { status, items };
  }

  const P = String.raw`^\/api\/v1\/projects\/[^/]+`;
  const MR = String.raw`${P}\/monthly-reports\/\d{4}-\d{2}`;
  const mrMonth = (url, index) => idFromPath(url, index);

  return [
    // PCV
    ['GET', new RegExp(`${P}/pcv$`), async ({ req, url }) => {
      const { ctx, p } = openProject(req, url);
      return { data: pcvData(p, ctx, monthParam(url)) };
    }],
    ['PUT', new RegExp(`${P}/pcv$`), async ({ req, url }) => {
      const { ctx, p } = writeProject(req, url, 4, 'pcv.manage');
      const body = await readJson(req);
      const month = str(body.month);
      const errors = {};
      if (!MONTH_PATTERN.test(month)) addError(errors, 'month', 'validation.invalid_value');
      else if (month > today().slice(0, 7)) addError(errors, 'month', 'project_pcv.err_future_month');
      const results = Array.isArray(body.results) ? body.results : [];
      if (results.length === 0) addError(errors, 'results', 'project_pcv.err_empty');
      results.forEach((x, i) => {
        if (!db.project_pcv_items.some((it) => it.id === x.itemId && it.project_id === p.id)) addError(errors, `results.${i}.itemId`, 'validation.invalid_value');
        // Không đạt bắt buộc ghi chú lỗi
        if (x.passed === false && !str(x.note)) addError(errors, `results.${i}.note`, 'project_pcv.err_note_required');
        maxLength(errors, `results.${i}.note`, str(x.note), 1000);
      });
      throwIfErrors(errors);
      // Mỗi tháng 1 lần đánh giá — lưu là ghi đè đánh giá cũ của đúng tháng đó
      let a = db.project_pcv_assessments.find((x) => x.project_id === p.id && x.assess_month.startsWith(month));
      if (!a) {
        a = { id: randomUUID(), tenant_id: p.tenant_id, project_id: p.id, assess_month: `${month}-01` };
        db.project_pcv_assessments.push(a);
      }
      Object.assign(a, { assessed_by: ctx.user.id, assessed_at: nowIso() });
      db.project_pcv_results = db.project_pcv_results.filter((x) => x.assessment_id !== a.id);
      for (const x of results) db.project_pcv_results.push({ assessment_id: a.id, item_id: x.itemId, result: x.passed ? 'pass' : 'fail', error_note: x.passed ? null : str(x.note) });
      const s = summarizePcv(results.map((x) => ({ passed: !!x.passed })));
      return { key: 'project_pcv_save.msg_success', args: { month, score: s.score.toFixed(1) } };
    }],
    ['POST', new RegExp(`${P}/pcv/items$`), async ({ req, url }) => {
      const { p } = writeProject(req, url, 4, 'pcv.manage');
      const input = parsePcvItem(await readJson(req));
      const seq = db.project_pcv_items.filter((i) => i.project_id === p.id).reduce((m, i) => Math.max(m, i.sort_seq), 0) + 1;
      db.project_pcv_items.push({ id: randomUUID(), tenant_id: p.tenant_id, project_id: p.id, group_name: input.category, content: input.label, sort_seq: seq, is_deleted: false });
      return { status: 201, key: 'project_pcv_item_create.msg_success' };
    }],
    ['PUT', new RegExp(`${P}/pcv/items/[^/]+$`), async ({ req, url }) => {
      const { p } = writeProject(req, url, -4, 'pcv.manage');
      const item = db.project_pcv_items.find((i) => i.id === idFromPath(url) && i.project_id === p.id && !i.is_deleted);
      if (!item) throw notFound();
      const input = parsePcvItem(await readJson(req));
      Object.assign(item, { group_name: input.category, content: input.label });
      return { key: 'project_pcv_item_update.msg_success' };
    }],
    ['DELETE', new RegExp(`${P}/pcv/items/[^/]+$`), async ({ req, url }) => {
      const { p } = writeProject(req, url, -4, 'pcv.manage');
      const item = db.project_pcv_items.find((i) => i.id === idFromPath(url) && i.project_id === p.id && !i.is_deleted);
      if (!item) throw notFound();
      // Xoá mềm: đánh giá đã lưu trước đó không đổi
      item.is_deleted = true;
      return { key: 'project_pcv_item_delete.msg_success' };
    }],

    // CSS
    ['GET', new RegExp(`${P}/css$`), async ({ req, url }) => {
      const { ctx, p } = openProject(req, url);
      return { data: cssData(p, ctx, monthParam(url)) };
    }],
    ['PUT', new RegExp(`${P}/css/form$`), async ({ req, url }) => {
      const { p } = writeProject(req, url, 4, 'css.manage');
      const body = await readJson(req);
      const errors = {};
      const title = str(body.title);
      textField(errors, 'title', title, 200);
      for (const f of ['intro', 'scaleNote', 'commentLabel']) maxLength(errors, f, str(body[f]), 2000);
      throwIfErrors(errors);
      Object.assign(store.projectCssForm(p.id), { title, intro: str(body.intro), scale_note: str(body.scaleNote), comment_label: str(body.commentLabel) });
      return { key: 'project_css_form_update.msg_success' };
    }],
    ['POST', new RegExp(`${P}/css/questions$`), async ({ req, url }) => {
      const { p } = writeProject(req, url, 4, 'css.manage');
      const body = await readJson(req);
      const errors = {};
      const label = str(body.label);
      textField(errors, 'label', label, 500);
      throwIfErrors(errors);
      const seq = store.projectCssQuestions(p.id).reduce((m, q) => Math.max(m, q.sort_seq), 0) + 1;
      db.project_css_questions.push({ id: randomUUID(), project_id: p.id, content: label, is_required: body.required !== false, sort_seq: seq, is_deleted: false });
      return { status: 201, key: 'project_css_question_create.msg_success' };
    }],
    ['PUT', new RegExp(`${P}/css/questions/[^/]+$`), async ({ req, url }) => {
      const { p } = writeProject(req, url, -4, 'css.manage');
      const q = store.projectCssQuestions(p.id).find((x) => x.id === idFromPath(url));
      if (!q) throw notFound();
      const body = await readJson(req);
      const errors = {};
      const label = str(body.label);
      textField(errors, 'label', label, 500);
      throwIfErrors(errors);
      Object.assign(q, { content: label, is_required: body.required !== false });
      return { key: 'project_css_question_update.msg_success' };
    }],
    ['DELETE', new RegExp(`${P}/css/questions/[^/]+$`), async ({ req, url }) => {
      const { p } = writeProject(req, url, -4, 'css.manage');
      const q = store.projectCssQuestions(p.id).find((x) => x.id === idFromPath(url));
      if (!q) throw notFound();
      q.is_deleted = true;
      return { key: 'project_css_question_delete.msg_success' };
    }],
    // Tạo (lại) link khảo sát của tháng: link cũ hết hiệu lực, kết quả đã nộp của tháng đó bị xoá
    ['POST', new RegExp(`${P}/css/link$`), async ({ req, url }) => {
      const { ctx, p } = writeProject(req, url, 4, 'css.manage');
      const body = await readJson(req);
      const month = str(body.month);
      if (!MONTH_PATTERN.test(month)) throw new ApiError(400, 'error.validation_failed', { month: ['validation.invalid_value'] });
      if (month > today().slice(0, 7)) throw new ApiError(400, 'error.validation_failed', { month: ['project_css.err_future_month'] });
      if (store.projectCssQuestions(p.id).length === 0) throw new ApiError(409, 'project_css.err_no_questions');
      for (const s of db.project_css_surveys.filter((x) => x.project_id === p.id && x.survey_month.startsWith(month))) s.is_revoked = true;
      const survey = {
        id: randomUUID(), tenant_id: p.tenant_id, project_id: p.id, survey_month: `${month}-01`, token: randomBytes(24).toString('hex'),
        status: 'sent', sent_at: nowIso(), sent_by: ctx.user.id, submitted_at: null, respondent: null, comment: null, scores: [],
      };
      db.project_css_surveys.push(survey);
      return { status: 201, key: 'project_css_link.msg_success', args: { month }, data: { token: survey.token } };
    }],

    // Quản trị dự án
    ['GET', new RegExp(`${P}/documents$`), async ({ req, url }) => {
      const { p, canManage } = openProject(req, url);
      return { data: { ...documentsData(p), canEdit: canManage && !p.is_deleted } };
    }],
    ...crud('document-groups', 'project_document_groups', {
      key: 'project_document_group', list: documentsData, labelOf: (g) => g.name,
      parse: (body) => {
        const errors = {};
        const name = str(body.name);
        textField(errors, 'name', name, 100);
        const description = str(body.description);
        maxLength(errors, 'description', description, 500);
        throwIfErrors(errors);
        return { name, description: description || null };
      },
      // Nhóm mới luôn ở cuối: sort_seq = max + 1
      onCreate: (row, p) => { row.sort_seq = db.project_document_groups.filter((g) => g.project_id === p.id).reduce((m, g) => Math.max(m, g.sort_seq), 0) + 1; },
    }),
    ...crud('documents', 'project_documents', {
      key: 'project_document', list: documentsData, labelOf: (d) => d.name,
      parse: (body, p) => {
        const errors = {};
        const name = str(body.name);
        textField(errors, 'name', name, 200);
        const link = str(body.url);
        urlField(errors, 'url', link);
        const groupId = str(body.groupId);
        if (!groupId) addError(errors, 'groupId', 'validation.required');
        else if (!docGroupsOf(p).some((g) => g.id === groupId)) addError(errors, 'groupId', 'validation.invalid_value');
        throwIfErrors(errors);
        return { name, url: link, group_id: groupId };
      },
    }),
    ...crud('risks', 'project_risks', {
      key: 'project_risk', list: risksData, parse: parseRisk, labelOf: (r) => r.code,
      onCreate: (row, p) => { row.code = nextSequenceCode('R', db.project_risks.filter((r) => r.project_id === p.id).map((r) => r.code)); },
    }),
    ...crud('changes', 'project_change_requests', {
      key: 'project_change', list: changesData, parse: parseChange, labelOf: (c) => c.code,
      onCreate: (row, p) => { row.code = nextSequenceCode('CR', db.project_change_requests.filter((c) => c.project_id === p.id).map((c) => c.code)); },
    }),
    ...crud('issues', 'project_issue_links', { key: 'project_issue', list: issuesData, parse: parseIssue, labelOf: (i) => i.name }),

    // Báo cáo tháng (mọi người xem được dự án đều sửa được — giống bản gốc)
    ['GET', new RegExp(`${MR}$`), async ({ req, url }) => {
      const { p } = openProject(req, url);
      const month = mrMonth(url, -1);
      return { data: reportData(p, month, !p.is_deleted) };
    }],
    ['PATCH', new RegExp(`${MR}/header$`), async ({ req, url }) => {
      const { ctx, p } = writeProject(req, url, 4, 'project.view');
      const month = mrMonth(url, -2);
      const body = await readJson(req);
      const field = str(body.field);
      const errors = {};
      if (field === 'language') oneOf(errors, 'value', body.value, ['vi', 'ja']);
      else if (!HEADER_FIELDS[field]) addError(errors, 'field', 'validation.invalid_value');
      else maxLength(errors, 'value', str(body.value), HEADER_FIELDS[field][1]);
      throwIfErrors(errors);
      const r = ensureReport(p, month, ctx);
      if (field === 'language') r.language = body.value;
      else r[HEADER_FIELDS[field][0]] = str(body.value);
      return { key: 'monthly_report_save.msg_success', data: { updatedAt: r.updated_at } };
    }],
    ['PATCH', new RegExp(`${MR}/cells$`), async ({ req, url }) => {
      const { ctx, p } = writeProject(req, url, 4, 'project.view');
      const month = mrMonth(url, -2);
      const body = await readJson(req);
      const employeeId = str(body.employeeId);
      const date = str(body.date);
      const field = str(body.field);
      const errors = {};
      if (!membersInMonth(p, month).some((e) => e.id === employeeId)) addError(errors, 'employeeId', 'validation.invalid_value');
      if (!date.startsWith(month) || !daysOfMonth(month).includes(date)) addError(errors, 'date', 'validation.invalid_value');
      const value = body.value === null || body.value === undefined ? '' : body.value;
      const r0 = findReport(p, month) ?? draftReport(p, month);
      if (field === 'hours') {
        if (value !== '') {
          // Giờ làm: số 0–24, bước 0,5
          const n = numberInRange(errors, 'value', value, 0, 24);
          if (n !== null && Math.round(n * 2) !== n * 2) addError(errors, 'value', 'monthly_report_cell.err_hours_step');
        }
      } else if (field === 'start' || field === 'end') {
        if (value !== '' && !TIME_PATTERN.test(String(value))) addError(errors, 'value', 'validation.invalid_time');
      } else if (field === 'content') {
        maxLength(errors, 'value', String(value), 1000);
      } else if (field.startsWith('custom:')) {
        const col = r0.columns.find((c) => c.id === field.slice(7) && !c.source);
        if (!col) addError(errors, 'field', 'validation.invalid_value');
        else if (col.type === 'number' && value !== '' && !Number.isFinite(Number(value))) addError(errors, 'value', 'validation.invalid_number');
        else maxLength(errors, 'value', String(value), 500);
      } else addError(errors, 'field', 'validation.invalid_value');
      throwIfErrors(errors);
      const r = ensureReport(p, month, ctx);
      const c = ensureCell(r, employeeId, date);
      // Ô giờ / giờ vào-ra sửa tay → đánh dấu manual để đồng bộ không ghi đè
      if (field === 'hours') Object.assign(c, { hours: value === '' ? null : Number(value), source: 'manual' });
      else if (field === 'start') Object.assign(c, { start_time: value || null, end_time: c.time_source === 'manual' ? c.end_time : db.attendance_records.find((a) => a.employee_id === employeeId && a.work_date === date)?.check_out ?? null, time_source: 'manual' });
      else if (field === 'end') Object.assign(c, { end_time: value || null, start_time: c.time_source === 'manual' ? c.start_time : db.attendance_records.find((a) => a.employee_id === employeeId && a.work_date === date)?.check_in ?? null, time_source: 'manual' });
      else if (field === 'content') c.content = String(value);
      else c.custom = { ...c.custom, [field.slice(7)]: String(value) };
      return { key: 'monthly_report_save.msg_success', data: { updatedAt: r.updated_at } };
    }],
    // Đồng bộ lại: bỏ đánh dấu sửa tay → ô lấy lại giờ từ Nhật ký (kind hours) hoặc giờ vào/ra từ Chấm công (kind time)
    ['POST', new RegExp(`${MR}/sync$`), async ({ req, url }) => {
      const { ctx, p } = writeProject(req, url, 4, 'project.view');
      const month = mrMonth(url, -2);
      const body = await readJson(req);
      const kind = body.kind === 'time' ? 'time' : 'hours';
      const r = findReport(p, month);
      let changed = 0;
      if (r) {
        for (const c of db.monthly_report_cells.filter((x) => x.report_id === r.id)) {
          if (body.employeeId && (c.employee_id !== body.employeeId || c.work_date !== body.date)) continue;
          if (kind === 'hours' && c.source === 'manual') {
            Object.assign(c, { source: 'timesheet', hours: null });
            changed += 1;
          }
          if (kind === 'time' && c.time_source === 'manual') {
            Object.assign(c, { time_source: 'attendance', start_time: null, end_time: null });
            changed += 1;
          }
        }
        ensureReport(p, month, ctx);
      }
      return { key: kind === 'time' ? 'monthly_report_sync_time.msg_success' : 'monthly_report_sync_hours.msg_success', args: { count: changed } };
    }],
    ['POST', new RegExp(`${MR}/columns$`), async ({ req, url }) => {
      const { ctx, p } = writeProject(req, url, 4, 'project.view');
      const month = mrMonth(url, -2);
      const body = await readJson(req);
      const r = ensureReport(p, month, ctx);
      const input = parseColumn(body, r);
      const position = Math.max(0, Math.min(r.columns.length, Number(body.position ?? r.columns.length)));
      const col = { id: `c_${randomBytes(4).toString('hex')}`, ...input };
      r.columns.splice(position, 0, col);
      return { status: 201, key: 'monthly_report_column_create.msg_success', data: { id: col.id } };
    }],
    ['PUT', new RegExp(`${MR}/columns/[^/]+$`), async ({ req, url }) => {
      const { ctx, p } = writeProject(req, url, 4, 'project.view');
      const month = mrMonth(url, -3);
      const r = ensureReport(p, month, ctx);
      const col = r.columns.find((c) => c.id === idFromPath(url));
      if (!col) throw notFound();
      const input = parseColumn(await readJson(req), r, col.id);
      // Cột hệ thống chỉ đổi được nhãn, không đổi kiểu
      Object.assign(col, col.source ? { label: input.label, label_ja: input.label_ja } : input);
      return { key: 'monthly_report_column_update.msg_success' };
    }],
    ['DELETE', new RegExp(`${MR}/columns/[^/]+$`), async ({ req, url }) => {
      const { ctx, p } = writeProject(req, url, 4, 'project.view');
      const month = mrMonth(url, -3);
      const r = ensureReport(p, month, ctx);
      const col = r.columns.find((c) => c.id === idFromPath(url));
      if (!col) throw notFound();
      if (col.source) throw new ApiError(409, 'monthly_report_column.err_system');
      r.columns = r.columns.filter((c) => c.id !== col.id);
      return { key: 'monthly_report_column_delete.msg_success', args: { name: col.label } };
    }],

    // Báo cáo tình trạng dự án
    ['GET', new RegExp(`${P}/status-report$`), async ({ req, url }) => {
      const { p } = writeProject(req, url, 4);
      const r = db.project_reports.find((x) => x.project_id === p.id);
      return { data: r ? { status: r.status, items: r.items, updatedAt: r.updated_at, updatedByName: userName(r.updated_by) } : { status: 'GOOD', items: FULL_TEMPLATE.map((metric) => ({ metric, content: '' })), updatedAt: null, updatedByName: null } };
    }],
    ['PUT', new RegExp(`${P}/status-report$`), async ({ req, url }) => {
      const { ctx, p } = writeProject(req, url, 4);
      const body = await readJson(req);
      const errors = {};
      oneOf(errors, 'status', body.status, REPORT_STATUSES);
      const items = Array.isArray(body.items) ? body.items : [];
      if (items.length === 0) addError(errors, 'items', 'project_status_report.err_items_required');
      items.forEach((it, i) => {
        textField(errors, `items.${i}.metric`, str(it.metric), 100);
        maxLength(errors, `items.${i}.content`, str(it.content), 4000);
      });
      throwIfErrors(errors);
      let r = db.project_reports.find((x) => x.project_id === p.id);
      if (!r) {
        r = { id: randomUUID(), project_id: p.id };
        db.project_reports.push(r);
      }
      Object.assign(r, { status: body.status, items: items.map((it) => ({ metric: str(it.metric), content: str(it.content) })), updated_at: nowIso(), updated_by: ctx.user.id });
      return { key: 'project_status_report_save.msg_success' };
    }],
    ['POST', new RegExp(`${P}/status-report/ai-fill$`), async ({ req, url }) => {
      const { p } = writeProject(req, url, 4);
      await new Promise((resolve) => setTimeout(resolve, 700)); // mô phỏng thời gian LLM xử lý
      return { key: 'project_status_report_ai.msg_success', data: aiFill(p) };
    }],
  ];
}
