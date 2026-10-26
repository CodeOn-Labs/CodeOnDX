// Port utils/timesheet-day-rules.ts + timesheet-week-rules.ts + timesheet-group-rules.ts + timesheet-ron-colors.ts của bản gốc.
// Khác bản gốc: dòng "RON" (đồng bộ từ hệ thống ngoài) thay bằng dòng sinh từ đơn nghỉ / OT đã duyệt (source ≠ manual, D4).
// Khoá ngày và giới hạn giờ do BE tính sẵn (TimesheetDay.locked, TimesheetRules) — ở đây chỉ còn logic hiển thị.
import type { EmployeeLite, TimesheetEntry, TimesheetPeriod, TimesheetStatus } from '@/modules/work/api/work.api';

/** Bảng màu dùng chung (COLORS của shared/utils/constants.ts bản gốc). */
export const COLORS = ['#3b82f6', '#10b981', '#7c3aed', '#f59e0b', '#ef4444', '#06b6d4', '#f97316', '#8b5cf6', '#84cc16', '#ec4899', '#14b8a6', '#eab308', '#6366f1', '#22c55e', '#f43f5e', '#0ea5e9'];
/** Màu cố định của dòng nghỉ phép (đỏ) và OT (cam) — không dùng màu băm theo tên. */
export const LEAVE_COLOR = '#ef4444';
export const OT_COLOR = '#f97316';
const HASH_COLORS = COLORS.filter((c) => c !== LEAVE_COLOR && c !== OT_COLOR);

/** Một dòng trên lưới công: dự án, hạng mục tự do (theo loại công việc) hoặc dòng sinh từ đơn (chỉ đọc). */
export interface TimesheetRowKey {
  /** p:<projectId> | t:<taskTypeId> | leave | ot */
  key: string;
  label: string;
  projectId: string | null;
  taskTypeId: string | null;
  /** leave / ot = sinh từ đơn đã duyệt, không sửa được */
  generated: 'leave' | 'ot' | null;
}

export const generatedKind = (e: Pick<TimesheetEntry, 'source'>): 'leave' | 'ot' | null =>
  e.source === 'leave_request' ? 'leave' : e.source === 'ot_request' ? 'ot' : null;

export function rowKeyOf(e: Pick<TimesheetEntry, 'source' | 'projectId' | 'taskType'>): string {
  const gen = generatedKind(e);
  if (gen) return gen;
  return e.projectId ? `p:${e.projectId}` : `t:${e.taskType?.id ?? 'other'}`;
}

/** Màu của dòng: nghỉ/OT cố định; dòng khác băm theo khoá (ổn định giữa các lần tải). */
export function rowColor(row: Pick<TimesheetRowKey, 'key' | 'generated'>): string {
  if (row.generated === 'leave') return LEAVE_COLOR;
  if (row.generated === 'ot') return OT_COLOR;
  const sum = row.key.split('').reduce((acc, ch) => acc + ch.charCodeAt(0), 0);
  return HASH_COLORS[Math.abs(sum) % HASH_COLORS.length] ?? '#3b82f6';
}

/** Có phân công hiệu lực tại ngày đó không (ranges từ BE). */
export function assignedOn(period: Pick<TimesheetPeriod, 'projects'>, projectId: string, date: string): boolean {
  return period.projects.some((p) => p.id === projectId && p.ranges.some((r) => r.start <= date && r.end >= date));
}

/**
 * Danh sách dòng của một khoảng ngày (port buildDayRows + rowMapping của TimesheetWeekPage):
 *  1. gộp bản ghi theo dự án / loại công việc / loại đơn;
 *  2. thêm dòng 0h cho dự án đang có phân công trong khoảng mà chưa có bản ghi;
 *  3. sắp xếp: dự án trước, rồi hạng mục tự do, cuối cùng khối nghỉ/OT (chỉ đọc).
 */
export function buildRows(period: TimesheetPeriod, extra: TimesheetRowKey[] = []): TimesheetRowKey[] {
  const map = new Map<string, TimesheetRowKey>();
  for (const e of period.entries) {
    const key = rowKeyOf(e);
    if (map.has(key)) continue;
    const gen = generatedKind(e);
    map.set(key, {
      key,
      label: e.projectName ?? e.taskType?.name ?? '—',
      projectId: e.projectId && !gen ? e.projectId : null,
      taskTypeId: !e.projectId && !gen ? (e.taskType?.id ?? null) : null,
      generated: gen,
    });
  }
  for (const p of period.projects) {
    const key = `p:${p.id}`;
    if (!map.has(key) && p.ranges.some((r) => r.start <= period.to && r.end >= period.from)) {
      map.set(key, { key, label: p.name, projectId: p.id, taskTypeId: null, generated: null });
    }
  }
  for (const r of extra) if (!map.has(r.key)) map.set(r.key, r);
  const rank = (r: TimesheetRowKey) => (r.generated ? 2 : r.projectId ? 0 : 1);
  return [...map.values()].sort((a, b) => rank(a) - rank(b) || a.label.localeCompare(b.label, 'vi'));
}

/** grid[rowKey][date] = tổng giờ */
export type HoursGrid = Record<string, Record<string, number>>;

export function buildGrid(entries: TimesheetEntry[]): HoursGrid {
  const grid: HoursGrid = {};
  for (const e of entries) {
    const key = rowKeyOf(e);
    grid[key] ??= {};
    grid[key][e.date] = round2((grid[key][e.date] ?? 0) + e.hours);
  }
  return grid;
}

export const round2 = (n: number) => Math.round(n * 100) / 100;

/** 8 → "8"; 7.5 → "7.5"; 7.25 → "7.25" (port formatHours). */
export function formatHours(hours: number): string {
  const v = Number(hours);
  if (!Number.isFinite(v)) return '0';
  return v % 1 === 0 ? String(v) : String(parseFloat(v.toFixed(2)));
}

/** Chỉ cho gõ số dương tối đa 2 chữ số thập phân, dấu chấm (port isHoursInputAllowed). */
export function isHoursInputAllowed(val: string): boolean {
  if (val.includes(',')) return false;
  return /^\d*\.?\d{0,2}$/.test(val);
}

/** Làm tròn bội số 15 phút (kéo thanh giờ ở màn ngày). */
export const snapToQuarterHour = (hours: number) => Math.round(hours * 4) / 4;

/** Ngày gần nhất (≤ 7 ngày làm việc lùi lại) có công tự nhập, bỏ qua ngày có dòng nghỉ/OT — port findPreviousDayWithData. */
export function findPreviousDayWithData(entries: TimesheetEntry[], fromDate: string, maxBusinessDays = 7): { date: string; rows: TimesheetEntry[] } | null {
  const cursor = new Date(`${fromDate}T00:00:00`);
  let checked = 0;
  while (checked < maxBusinessDays) {
    cursor.setDate(cursor.getDate() - 1);
    const dow = cursor.getDay();
    if (dow === 0 || dow === 6) continue;
    checked += 1;
    const day = toIso(cursor);
    const ofDay = entries.filter((e) => e.date === day);
    if (ofDay.some((e) => generatedKind(e))) continue;
    const rows = ofDay.filter((e) => e.hours > 0);
    if (rows.length > 0) return { date: day, rows };
  }
  return null;
}

export function toIso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
export function addDaysIso(iso: string, n: number): string {
  const d = new Date(`${iso}T00:00:00`);
  d.setDate(d.getDate() + n);
  return toIso(d);
}
/** Thứ 2 của tuần chứa ngày. */
export function mondayOf(iso: string): string {
  const d = new Date(`${iso}T00:00:00`);
  const dow = d.getDay();
  return addDaysIso(iso, dow === 0 ? -6 : 1 - dow);
}
export function daysBetween(from: string, to: string): string[] {
  const out: string[] = [];
  for (let d = from; d <= to && out.length < 62; d = addDaysIso(d, 1)) out.push(d);
  return out;
}

// ─── Báo cáo nhiều nhân viên (port timesheet-group-rules.ts) ───────────────

export type GroupMode = 'all' | 'multi' | 'project';
export interface GroupSelection {
  mode: GroupMode;
  employeeIds: string[];
  label: string;
  projectId?: string;
}

/** multi cần ≥ 2 người; project phải chọn dự án; all luôn được. */
export function canApplyGroupSelection(mode: GroupMode, employeeIds: string[], projectId: string | null): boolean {
  if (mode === 'multi') return employeeIds.length >= 2;
  if (mode === 'project') return projectId !== null && projectId !== '';
  return true;
}

export interface GroupRow {
  key: string;
  label: string;
  generated: 'leave' | 'ot' | null;
  hoursByDate: Record<string, number>;
  total: number;
}
export interface EmployeeGroup {
  employee: EmployeeLite;
  isEmpty: boolean;
  rows: GroupRow[];
  totalRow: { hoursByDate: Record<string, number>; total: number } | null;
  status: TimesheetStatus | null;
}

/** Trạng thái nhóm: còn dòng nháp → draft; có dòng bị từ chối → rejected; còn lại approved (bỏ qua dòng sinh từ đơn). */
export function deriveGroupStatus(entries: Pick<TimesheetEntry, 'status' | 'source'>[]): TimesheetStatus {
  const manual = entries.filter((e) => e.source === 'manual');
  if (manual.some((e) => e.status === 'draft')) return 'draft';
  if (manual.some((e) => e.status === 'rejected')) return 'rejected';
  return 'approved';
}

export function buildGroupReport(employees: EmployeeLite[], entries: TimesheetEntry[], dates: string[], locale: string): EmployeeGroup[] {
  const groups = employees.map<EmployeeGroup>((employee) => {
    const own = entries.filter((e) => e.employeeId === employee.id);
    if (own.length === 0) return { employee, isEmpty: true, rows: [], totalRow: null, status: null };
    const byKey = new Map<string, TimesheetEntry[]>();
    for (const e of own) byKey.set(rowKeyOf(e), [...(byKey.get(rowKeyOf(e)) ?? []), e]);
    const rows = [...byKey.entries()].map<GroupRow>(([key, list]) => {
      const hoursByDate = Object.fromEntries(dates.map((d) => [d, 0]));
      let total = 0;
      for (const e of list) {
        hoursByDate[e.date] = round2((hoursByDate[e.date] ?? 0) + e.hours);
        total += e.hours;
      }
      const first = list[0]!;
      return { key, label: first.projectName ?? first.taskType?.name ?? '—', generated: generatedKind(first), hoursByDate, total: round2(total) };
    });
    rows.sort((a, b) => Number(!!a.generated) - Number(!!b.generated) || a.label.localeCompare(b.label, locale));
    const totalRow = { hoursByDate: Object.fromEntries(dates.map((d) => [d, round2(rows.reduce((s, r) => s + (r.hoursByDate[d] ?? 0), 0))])), total: round2(rows.reduce((s, r) => s + r.total, 0)) };
    return { employee, isEmpty: false, rows, totalRow, status: deriveGroupStatus(own) };
  });
  return groups.sort((a, b) => a.employee.fullName.localeCompare(b.employee.fullName, locale));
}
