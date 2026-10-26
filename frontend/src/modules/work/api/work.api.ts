// API làm việc hằng ngày (D4): Nhật ký công việc, Nghỉ phép & OT, Chấm công vào/ra. Hợp đồng: tools/mock-api/README.md.
// Luật khoá ngày, giới hạn giờ, quỹ phép, giới hạn OT, đi muộn do BE tính — FE chỉ hiển thị.
import { request, requestFull } from '@/api/http';
import type { MasterDataRef, PagedResult } from '@/api/types';

export interface EmployeeLite {
  id: string;
  code: string;
  fullName: string;
  email: string;
  avatarUrl: string | null;
}

// ─── Nhật ký công việc ──────────────────────────────────────────────────────

export type TimesheetStatus = 'draft' | 'approved' | 'rejected';
/** manual = tự nhập; leave_request / ot_request = sinh từ đơn đã duyệt (chỉ đọc) */
export type TimesheetSource = 'manual' | 'leave_request' | 'ot_request';

export interface TimesheetEntry {
  id: string;
  date: string;
  projectId: string | null;
  projectName: string | null;
  taskType: { id: string; code: string; name: string } | null;
  hours: number;
  description: string | null;
  status: TimesheetStatus;
  source: TimesheetSource;
  rejectionReason: string | null;
  employeeId: string;
}

export interface TimesheetDay {
  date: string;
  locked: boolean;
  /** edit_limit | month_locked | future */
  lockReason: 'edit_limit' | 'month_locked' | 'future' | null;
  lockArgs: Record<string, number>;
  /** Ngày đã có dòng tự nhập được duyệt → chỉ đọc */
  approved: boolean;
  weekend: boolean;
  holiday: string | null;
}

export interface TimesheetRules {
  minHours: number;
  maxHours: number;
  weeklyMaxHours: number | null;
  fteHours: number;
}

export interface TimesheetPeriod {
  employeeId: string;
  from: string;
  to: string;
  canEdit: boolean;
  rules: TimesheetRules;
  entries: TimesheetEntry[];
  days: TimesheetDay[];
  projects: Array<{ id: string; code: string; name: string; status: MasterDataRef | null; ranges: Array<{ start: string; end: string }> }>;
  week: { start: string; hoursByDate: Record<string, number> };
  taskTypes: Array<{ id: string; code: string; name: string }>;
}

export interface TimesheetSaveRow {
  date: string;
  projectId: string | null;
  taskTypeId: string | null;
  hours: number;
  description?: string | null;
}

export interface TimesheetPeople {
  myEmployeeId: string | null;
  employees: EmployeeLite[];
  projects: Array<{ id: string; name: string; employeeIds: string[] }>;
  canViewOthers: boolean;
  canEditAll: boolean;
  canApprove: boolean;
}

// ─── Nghỉ phép & OT ─────────────────────────────────────────────────────────

export type RequestStatus = 'pending' | 'approved' | 'rejected' | 'cancelled';
export type DurationType = 'full_day' | 'half_day_am' | 'half_day_pm';

interface RequestBase {
  id: string;
  code: string;
  employee: EmployeeLite | null;
  reason: string;
  status: RequestStatus;
  approver: { id: string; fullName: string } | null;
  decidedAt: string | null;
  decisionNote: string | null;
  createdAt: string;
}

export interface LeaveRequest extends RequestBase {
  leaveType: MasterDataRef | null;
  fromDate: string;
  toDate: string;
  durationType: DurationType;
  days: number;
  totalHours: number;
  attachmentUrl: string | null;
}

export interface OtRequest extends RequestBase {
  project: { id: string; name: string } | null;
  workDate: string;
  startTime: string;
  endTime: string;
  hours: number;
  dayKind: 'weekday' | 'weekend' | 'holiday';
  ratePercent: number;
}

export interface LeaveSummary {
  balance: { year: number; entitledDays: number; carriedOverDays: number; adjustedDays: number; usedDays: number; pendingDays: number; remainingDays: number };
  ot: { monthHours: number; yearHours: number; maxPerDay: number; maxPerMonth: number; maxPerYear: number };
  pendingApproval: number;
  minNoticeDays: number;
}

export interface RequestFilters {
  scope: 'mine' | 'approval';
  statuses: RequestStatus[];
  search: string;
  year?: number;
}

export interface LeaveInput {
  leaveTypeId: string;
  fromDate: string | null;
  toDate: string | null;
  durationType: DurationType;
  reason: string;
  attachmentUrl: string;
}

export interface OtInput {
  projectId: string;
  workDate: string | null;
  startTime: string;
  endTime: string;
  reason: string;
}

// ─── Chấm công vào/ra ───────────────────────────────────────────────────────

export interface AttendanceCell {
  id: string | null;
  date: string;
  checkIn: string | null;
  checkOut: string | null;
  source: 'app' | 'import' | 'manual' | null;
  note: string | null;
  late: boolean;
  missingOut: boolean;
  updatedBy: string | null;
  onLeave: DurationType | null;
}

export interface AttendanceRow {
  employee: EmployeeLite;
  records: Record<string, AttendanceCell>;
  summary: { workedDays: number; lateCount: number; missingOutCount: number; leaveDays: number };
}

export interface AttendanceMonth extends PagedResult<AttendanceRow> {
  month: string;
  days: Array<{ date: string; weekend: boolean; holiday: string | null }>;
  workStartTime: string;
  workEndTime: string;
  graceMinutes: number;
  editLimitDays: number;
  canEditAll: boolean;
  myEmployeeId: string | null;
}

export interface AttendanceToday {
  date: string;
  now: string;
  allowAppCheckIn: boolean;
  record: Omit<AttendanceCell, 'onLeave'> | null;
  workStartTime: string;
}

export interface AttendanceImportRow {
  employeeCode: string;
  date: string;
  checkIn: string | null;
  checkOut: string | null;
}

export interface AttendanceImportResult {
  imported: number;
  skipped: number;
  errors: Array<{ row: number; employeeCode: string; codes: Array<'unknown_employee' | 'invalid_date' | 'invalid_time' | 'out_before_in'> }>;
}

const requestQuery = (f: RequestFilters, page: number, pageSize: number) => ({
  scope: f.scope,
  statuses: f.statuses,
  search: f.search.trim() || undefined,
  year: f.year,
  page,
  pageSize,
});

export const workApi = {
  timesheetPeople: () => request<TimesheetPeople>('/timesheets/employees'),
  timesheets: (employeeId: string | null, from: string, to: string, signal?: AbortSignal) =>
    request<TimesheetPeriod>('/timesheets', { query: { employeeId: employeeId ?? undefined, from, to }, signal }),
  saveTimesheets: (employeeId: string | null, dates: string[], entries: TimesheetSaveRow[]) =>
    requestFull<{ savedDates: string[]; skippedDates: string[] }>('/timesheets', { method: 'PUT', body: { employeeId, dates, entries } }),
  timesheetGroup: (employeeIds: string[], from: string, to: string, signal?: AbortSignal) =>
    request<{ from: string; to: string; employees: EmployeeLite[]; entries: TimesheetEntry[] }>('/timesheets/group', { query: { employeeIds, from, to }, signal }),
  timesheetApproval: (input: { employeeIds: string[]; from: string; to: string; action: 'approve' | 'reject'; reason?: string }) =>
    requestFull<{ updated: number }>('/timesheets/approval', { method: 'POST', body: input }),

  leaveSummary: (year?: number) => request<LeaveSummary>('/leave-requests/summary', { query: { year } }),
  leaveRequests: (f: RequestFilters, page: number, pageSize: number, signal?: AbortSignal) =>
    request<PagedResult<LeaveRequest>>('/leave-requests', { query: requestQuery(f, page, pageSize), signal }),
  createLeave: (input: LeaveInput) => requestFull<LeaveRequest>('/leave-requests', { method: 'POST', body: input }),
  decideLeave: (id: string, action: 'approve' | 'reject' | 'cancel', note = '') =>
    requestFull<LeaveRequest>(`/leave-requests/${id}/${action}`, { method: 'POST', body: { note } }),
  otRequests: (f: RequestFilters, page: number, pageSize: number, signal?: AbortSignal) =>
    request<PagedResult<OtRequest>>('/ot-requests', { query: requestQuery(f, page, pageSize), signal }),
  createOt: (input: OtInput) => requestFull<OtRequest>('/ot-requests', { method: 'POST', body: input }),
  decideOt: (id: string, action: 'approve' | 'reject' | 'cancel', note = '') =>
    requestFull<OtRequest>(`/ot-requests/${id}/${action}`, { method: 'POST', body: { note } }),

  attendance: (month: string, filters: { search: string; departmentIds: string[]; onlyIssues: boolean }, page: number, pageSize: number, signal?: AbortSignal) =>
    request<AttendanceMonth>('/attendance', {
      query: { month, search: filters.search.trim() || undefined, departmentIds: filters.departmentIds, onlyIssues: filters.onlyIssues || undefined, page, pageSize },
      signal,
    }),
  attendanceToday: () => request<AttendanceToday>('/attendance/today'),
  checkIn: () => requestFull<AttendanceCell>('/attendance/check-in', { method: 'POST' }),
  checkOut: () => requestFull<AttendanceCell>('/attendance/check-out', { method: 'POST' }),
  saveAttendance: (input: { employeeId: string; date: string; checkIn: string | null; checkOut: string | null; note: string }) =>
    requestFull<AttendanceCell>('/attendance', { method: 'PUT', body: input }),
  importAttendance: (rows: AttendanceImportRow[], overwrite: boolean) =>
    requestFull<AttendanceImportResult>('/attendance/import', { method: 'POST', body: { rows, overwrite } }),
};
