// API Form + Chi tiết dự án. Hợp đồng: tools/mock-api/README.md (mục Dự án — chi tiết).
// Số liệu tổng hợp (EE, PCV, CSS, tỷ lệ lỗi, mức rủi ro, đúng hạn…) do BE tính sẵn kèm `tone` theo ngưỡng ở Quản trị → Quy tắc.
import { request, requestFull } from '@/api/http';
import type { MasterDataRef } from '@/api/types';
import type { ProjectListItem } from './project.api';

export type Tone = 'none' | 'green' | 'orange' | 'red' | 'yellow' | 'lightOrange';

// ─── Dự án ───────────────────────────────────────────────────────────────

export interface ContractStaff {
  id: string;
  roleCode: string;
  roleName: string;
  level: MasterDataRef | null;
  quantity: number;
  ftePerPerson: number;
  startDate: string | null;
  endDate: string | null;
  pricePerMonth: number;
}

export interface HrAlert {
  employeeId: string;
  fullName: string;
  code: string;
  kind: 'resigned' | 'leaving_soon';
  leftDate: string | null;
}

export interface ProjectDetail extends ProjectListItem {
  description: string;
  ticketSystemUrl: string | null;
  sourceCodeUrl: string | null;
  driveUrl: string | null;
  contractStaff: ContractStaff[];
  budgetWarningThreshold: number;
  hrAlerts: HrAlert[];
  canManage: boolean;
  canEditMonthlyReport: boolean;
  canManagePcv: boolean;
  canManageCss: boolean;
}

export interface ContractStaffInput {
  id?: string | null;
  roleCode: string;
  levelId: string;
  quantity: number | string;
  ftePerPerson: number | string;
  startDate: string | null;
  endDate: string | null;
  pricePerMonth: number | string;
}

export interface ProjectInput {
  name: string;
  clientId: string;
  gradeId: string;
  statusId?: string;
  description: string;
  startDate: string | null;
  endDate: string | null;
  budget: number | string | null;
  ticketSystemUrl: string;
  sourceCodeUrl: string;
  contractStaff: ContractStaffInput[];
}

// ─── Phân bổ nhân sự ─────────────────────────────────────────────────────

export interface StaffEmployee {
  id: string;
  code: string;
  fullName: string;
  avatarUrl: string | null;
  position: string | null;
  leftDate: string | null;
  allocationPercent: number;
  overloaded: boolean;
  overloadThreshold: number;
  resigned: boolean;
  leavingSoon: boolean;
}

export interface FteSums {
  contract: number;
  planned: number;
  actual: number;
}

export interface StaffInterval {
  id: string;
  startDate: string;
  endDate: string;
  fte: number;
  sums: FteSums;
}

export interface StaffAssignment {
  id: string;
  roleInProject: string;
  employee: StaffEmployee | null;
  startDate: string;
  endDate: string;
  intervals: StaffInterval[];
}

export interface FtePoint {
  date: string;
  contract: number;
  planned: number;
  actual: number;
}

export interface StaffSlot {
  key: string;
  contractStaffId: string | null;
  slotIndex: number;
  contractFte: number;
  contractStartDate: string | null;
  contractEndDate: string | null;
  assignments: StaffAssignment[];
  series: FtePoint[];
}

export interface StaffGroup {
  key: string;
  roleCode: string;
  roleName: string;
  level: MasterDataRef | null;
  contractCount: number;
  assignedCount: number;
  slots: StaffSlot[];
}

export interface Staffing {
  startDate: string;
  endDate: string;
  fteHours: number;
  groups: StaffGroup[];
}

export interface IntervalInput {
  startDate: string | null;
  endDate: string | null;
  fte: number | string;
}

export interface AssignmentInput {
  employeeId: string;
  contractStaffId?: string | null;
  slotIndex?: number;
  roleInProject?: string;
  intervals: IntervalInput[];
  mode?: 'update' | 'handover' | 'replace';
  handoverDate?: string | null;
}

// ─── Metrics ─────────────────────────────────────────────────────────────

export type MilestoneStatus = 'done-ontime' | 'done-late' | 'overdue' | 'next' | 'upcoming';

export interface Milestone {
  id: string;
  name: string;
  plannedDate: string;
  actualDate: string | null;
  sortSeq: number;
  status: MilestoneStatus;
  delayDays: number;
  justification: string | null;
  justificationBy: string | null;
  justificationAt: string | null;
}

export interface Milestones {
  startDate: string;
  endDate: string;
  today: string;
  items: Milestone[];
  timeliness: { from: string; to: string; dueCount: number; onTimeCount: number; percent: number | null };
  canEdit: boolean;
}

export interface MilestoneInput {
  name: string;
  plannedDate: string | null;
  actualDate: string | null;
  justification: string;
}

export interface EeMonth {
  month: string;
  hoursPerMM: number;
  hdMM: number;
  actualMM: number;
  hdHours: number;
  actualHours: number;
  logworkHours: number;
  eeMonth: number | null;
  eeMonthTone: Tone;
  hdYtdHours: number;
  actualYtdHours: number;
  logworkYtdHours: number;
  eeYtd: number | null;
  eeYtdTone: Tone;
}

export interface EeMember {
  employeeId: string;
  fullName: string;
  code: string;
  position: string;
  points: Array<{ date: string; hd: number; actual: number; logwork: number }>;
  totals: { hdHours: number; actualHours: number; logworkHours: number; ee: number | null; eeTone: Tone };
}

export interface EeData {
  months: EeMonth[];
  chartMonth: string | null;
  members: EeMember[];
  thresholds: { greenMax: number; orangeMax: number };
}

export type QualityKind = 'defect' | 'leakage';

export interface QualityRow {
  month: string;
  hasData: boolean;
  defects: number;
  productSize: number;
  productSizeUnit: string;
  billableMM: number;
  ratePerSize: number | null;
  ratePerMM: number | null;
  ratePerMMTone: Tone;
  defectsYtd: number;
  productSizeYtd: number;
  billableYtd: number;
  ratePerSizeYtd: number | null;
  ratePerMMYtd: number | null;
  ratePerMMYtdTone: Tone;
}

export interface QualityData {
  kind: QualityKind;
  year: number;
  years: number[];
  from: string;
  to: string;
  /** Đơn vị Product size của dự án */
  unit: string;
  rows: QualityRow[];
  thresholds: { goodMin: number; goodMax: number; warnMax: number };
  canEdit: boolean;
}

export interface QualityEntry {
  month: string;
  defects: number | string | null;
  productSize: number | string | null;
}

export interface PcvItem {
  id: string;
  category: string;
  label: string;
  isDeleted: boolean;
  sortSeq: number;
}

export interface PcvResult {
  itemId: string;
  passed: boolean;
  note: string | null;
}

export interface PcvSummary {
  checked: number;
  passed: number;
  failed: number;
  score: number;
}

export interface PcvData {
  month: string;
  isFuture: boolean;
  canEdit: boolean;
  projectGrade: string | null;
  items: PcvItem[];
  assessment: { assessedAt: string; assessorName: string | null; results: PcvResult[] } | null;
  summary: PcvSummary & { total: number; tone: Tone };
  history: Array<PcvSummary & { month: string; tone: Tone; assessorName: string | null; assessedAt: string }>;
  thresholds: { greenMin: number; orangeMin: number };
}

export interface CssForm {
  title: string;
  intro: string;
  scaleNote: string;
  commentLabel: string;
}

export interface CssQuestion {
  id: string;
  label: string;
  required: boolean;
  sortSeq: number;
}

export interface CssData {
  month: string;
  isFuture: boolean;
  canEdit: boolean;
  form: CssForm;
  questions: CssQuestion[];
  survey: {
    status: 'sent' | 'submitted';
    token: string;
    sentAt: string | null;
    senderName: string | null;
    submittedAt: string | null;
    respondent: string | null;
    comment: string | null;
    answers: Array<{ question: string; required: boolean; score: number | null }>;
  } | null;
  summary: { answered: number; score: number; tone: Tone } | null;
  history: Array<{ month: string; status: 'sent' | 'submitted'; score: number | null; answered: number; tone: Tone; submittedAt: string | null; respondent: string | null }>;
  thresholds: { greenMin: number; orangeMin: number };
}

// ─── Quản trị dự án ──────────────────────────────────────────────────────

export interface AuditInfo {
  createdAt: string;
  updatedAt: string;
  createdByName: string | null;
  updatedByName: string | null;
}

export interface ProjectDocument extends AuditInfo {
  id: string;
  groupId: string;
  name: string;
  url: string;
}

export interface DocumentGroup extends AuditInfo {
  id: string;
  name: string;
  description: string | null;
  sortSeq: number;
  documents: ProjectDocument[];
}

export interface DocumentsData {
  groups: DocumentGroup[];
  summary: { groupCount: number; documentCount: number };
  canEdit: boolean;
}

export type RiskCategory = 'schedule' | 'technical' | 'resource' | 'customer' | 'cost' | 'quality';
export type RiskStrategy = 'avoid' | 'mitigate' | 'transfer' | 'accept';
export type RiskStatus = 'open' | 'in_progress' | 'resolved' | 'closed';
export type RiskLevel = 'low' | 'medium' | 'high' | 'critical';

export interface Risk extends AuditInfo {
  id: string;
  code: string;
  name: string;
  description: string | null;
  category: RiskCategory;
  probability: number;
  impact: number;
  score: number;
  level: RiskLevel;
  strategy: RiskStrategy;
  responsePlan: string | null;
  ownerId: string | null;
  ownerName: string | null;
  dueDate: string;
  status: RiskStatus;
  identifiedAt: string;
  isActive: boolean;
  isOverdue: boolean;
}

export interface RisksData {
  items: Risk[];
  matrix: Record<string, number>;
  summary: { total: number; active: number; highOrCritical: number; overdue: number; resolved: number };
  owners: Array<{ id: string; fullName: string; code: string }>;
  canEdit: boolean;
}

export interface RiskInput {
  name: string;
  description: string;
  category: RiskCategory;
  probability: number;
  impact: number;
  strategy: RiskStrategy;
  responsePlan: string;
  ownerId: string | null;
  dueDate: string | null;
  status: RiskStatus;
  identifiedAt: string | null;
}

export type ChangeSource = 'customer' | 'internal';
export type ChangeImpactLevel = 'low' | 'medium' | 'high';
export type ChangeImpactArea = 'scope' | 'schedule' | 'cost' | 'quality';

export interface ChangeRequest extends AuditInfo {
  id: string;
  code: string;
  fileName: string;
  url: string;
  source: ChangeSource;
  impactLevel: ChangeImpactLevel;
  impactAreas: ChangeImpactArea[];
  impactNote: string | null;
}

export interface ChangesData {
  items: ChangeRequest[];
  summary: { total: number; customer: number; internal: number; high: number };
  canEdit: boolean;
}

export interface ChangeInput {
  fileName: string;
  url: string;
  source: ChangeSource;
  impactLevel: ChangeImpactLevel;
  impactAreas: ChangeImpactArea[];
  impactNote: string;
}

export type IssueType = 'parent' | 'sprint' | 'milestone';
export type IssueKind = 'task' | 'bug';

export interface IssueLink extends AuditInfo {
  id: string;
  type: IssueType;
  kind: IssueKind;
  name: string;
  url: string;
}

export interface IssuesData {
  items: IssueLink[];
  summary: Record<IssueType, number>;
  canEdit: boolean;
}

export interface IssueInput {
  type: IssueType;
  kind: IssueKind;
  name: string;
  url: string;
}

// ─── Báo cáo tháng ───────────────────────────────────────────────────────

export type MonthlyReportColumnSource = 'date' | 'hours' | 'content' | 'checkIn' | 'checkOut';

export interface MonthlyReportColumn {
  id: string;
  label: string;
  labelJa: string;
  type: 'text' | 'number';
  source: MonthlyReportColumnSource | null;
}

export interface MonthlyReportDay {
  date: string;
  weekend: boolean;
  hours: number | null;
  hoursSource: 'manual' | 'timesheet' | null;
  hasLog: boolean;
  start: string | null;
  end: string | null;
  timeSource: 'manual' | 'attendance' | null;
  content: string;
  custom: Record<string, string>;
}

export interface MonthlyReportMember {
  employeeId: string;
  fullName: string;
  code: string;
  totalHours: number;
  days: MonthlyReportDay[];
}

export type MonthlyReportHeaderField = 'clientName' | 'companyName' | 'orderNo' | 'workName' | 'responsible' | 'language';

export interface MonthlyReport {
  id: string | null;
  month: string;
  isDraft: boolean;
  canEdit: boolean;
  header: Record<Exclude<MonthlyReportHeaderField, 'language'>, string> & { language: 'vi' | 'ja' };
  columns: MonthlyReportColumn[];
  members: MonthlyReportMember[];
  approvedLogDays: number;
  manualHourDays: number;
  manualTimeDays: number;
  updatedAt: string | null;
  updatedByName: string | null;
}

export type ReportStatus = 'GOOD' | 'RISK' | 'SOS';
export interface StatusReport {
  status: ReportStatus;
  items: Array<{ metric: string; content: string }>;
  updatedAt: string | null;
  updatedByName: string | null;
}

// ─── Gọi API ─────────────────────────────────────────────────────────────

const base = (id: string) => `/projects/${encodeURIComponent(id)}`;
type Opt = { signal?: AbortSignal };

/** CRUD dùng chung cho các bảng của tab Quản trị dự án. */
function crud<TInput>(segment: string) {
  return {
    create: (projectId: string, input: TInput) => requestFull<null>(`${base(projectId)}/${segment}`, { method: 'POST', body: input }),
    update: (projectId: string, id: string, input: TInput) => requestFull<null>(`${base(projectId)}/${segment}/${id}`, { method: 'PUT', body: input }),
    remove: (projectId: string, id: string) => requestFull<null>(`${base(projectId)}/${segment}/${id}`, { method: 'DELETE' }),
  };
}

export const projectDetailApi = {
  detail: (id: string, o: Opt = {}) => request<ProjectDetail>(base(id), o),
  create: (input: ProjectInput) => requestFull<ProjectDetail>('/projects', { method: 'POST', body: input }),
  update: (id: string, input: ProjectInput) => requestFull<ProjectDetail>(base(id), { method: 'PUT', body: input }),
  remove: (id: string) => requestFull<null>(base(id), { method: 'DELETE' }),
  restore: (id: string) => requestFull<null>(`${base(id)}/restore`, { method: 'POST' }),
  updateDates: (id: string, startDate: string | null, endDate: string | null) => requestFull<null>(`${base(id)}/dates`, { method: 'PATCH', body: { startDate, endDate } }),

  staffing: (id: string, o: Opt = {}) => request<Staffing>(`${base(id)}/staffing`, o),
  assignableEmployees: (id: string, o: Opt = {}) => request<Array<StaffEmployee & { level: string | null }>>(`${base(id)}/assignable-employees`, o),
  addAssignment: (id: string, input: AssignmentInput) => requestFull<null>(`${base(id)}/assignments`, { method: 'POST', body: input }),
  updateAssignment: (id: string, assignmentId: string, input: AssignmentInput) => requestFull<null>(`${base(id)}/assignments/${assignmentId}`, { method: 'PUT', body: input }),
  removeAssignment: (id: string, assignmentId: string, intervalId?: string) =>
    requestFull<null>(`${base(id)}/assignments/${assignmentId}`, { method: 'DELETE', query: { intervalId } }),

  milestones: (id: string, range: { from?: string; to?: string }, o: Opt = {}) => request<Milestones>(`${base(id)}/milestones`, { query: range, ...o }),
  milestone: crud<MilestoneInput>('milestones'),
  ee: (id: string, month: string | null, o: Opt = {}) => request<EeData>(`${base(id)}/ee`, { query: { month }, ...o }),
  quality: (id: string, kind: QualityKind, year: number | null, o: Opt = {}) => request<QualityData>(`${base(id)}/quality`, { query: { kind, year }, ...o }),
  saveQuality: (id: string, kind: QualityKind, year: number, entries: QualityEntry[], unit?: string) =>
    requestFull<QualityData>(`${base(id)}/quality`, { method: 'PUT', body: { kind, year, entries, unit } }),

  pcv: (id: string, month: string, o: Opt = {}) => request<PcvData>(`${base(id)}/pcv`, { query: { month }, ...o }),
  savePcv: (id: string, month: string, results: PcvResult[]) => requestFull<null>(`${base(id)}/pcv`, { method: 'PUT', body: { month, results } }),
  pcvItem: crud<{ category: string; label: string }>('pcv/items'),

  css: (id: string, month: string, o: Opt = {}) => request<CssData>(`${base(id)}/css`, { query: { month }, ...o }),
  updateCssForm: (id: string, form: CssForm) => requestFull<null>(`${base(id)}/css/form`, { method: 'PUT', body: form }),
  cssQuestion: crud<{ label: string; required: boolean }>('css/questions'),
  createCssLink: (id: string, month: string) => requestFull<{ token: string }>(`${base(id)}/css/link`, { method: 'POST', body: { month } }),

  documents: (id: string, o: Opt = {}) => request<DocumentsData>(`${base(id)}/documents`, o),
  documentGroup: crud<{ name: string; description: string }>('document-groups'),
  document: crud<{ groupId: string; name: string; url: string }>('documents'),
  risks: (id: string, o: Opt = {}) => request<RisksData>(`${base(id)}/risks`, o),
  risk: crud<RiskInput>('risks'),
  changes: (id: string, o: Opt = {}) => request<ChangesData>(`${base(id)}/changes`, o),
  change: crud<ChangeInput>('changes'),
  issues: (id: string, o: Opt = {}) => request<IssuesData>(`${base(id)}/issues`, o),
  issue: crud<IssueInput>('issues'),

  monthlyReport: (id: string, month: string, o: Opt = {}) => request<MonthlyReport>(`${base(id)}/monthly-reports/${month}`, o),
  saveReportHeader: (id: string, month: string, field: MonthlyReportHeaderField, value: string) =>
    requestFull<{ updatedAt: string }>(`${base(id)}/monthly-reports/${month}/header`, { method: 'PATCH', body: { field, value } }),
  saveReportCell: (id: string, month: string, cell: { employeeId: string; date: string; field: string; value: string | number | null }) =>
    requestFull<{ updatedAt: string }>(`${base(id)}/monthly-reports/${month}/cells`, { method: 'PATCH', body: cell }),
  syncReport: (id: string, month: string, kind: 'hours' | 'time', target?: { employeeId: string; date: string }) =>
    requestFull<null>(`${base(id)}/monthly-reports/${month}/sync`, { method: 'POST', body: { kind, ...target } }),
  addReportColumn: (id: string, month: string, input: { position: number; label: string; labelJa: string; type: 'text' | 'number' }) =>
    requestFull<{ id: string }>(`${base(id)}/monthly-reports/${month}/columns`, { method: 'POST', body: input }),
  updateReportColumn: (id: string, month: string, columnId: string, input: { label: string; labelJa: string; type: 'text' | 'number' }) =>
    requestFull<null>(`${base(id)}/monthly-reports/${month}/columns/${columnId}`, { method: 'PUT', body: input }),
  removeReportColumn: (id: string, month: string, columnId: string) => requestFull<null>(`${base(id)}/monthly-reports/${month}/columns/${columnId}`, { method: 'DELETE' }),

  statusReport: (id: string, o: Opt = {}) => request<StatusReport>(`${base(id)}/status-report`, o),
  saveStatusReport: (id: string, input: { status: ReportStatus; items: StatusReport['items'] }) => requestFull<null>(`${base(id)}/status-report`, { method: 'PUT', body: input }),
  aiFillStatusReport: (id: string) => requestFull<{ status: ReportStatus; items: StatusReport['items'] }>(`${base(id)}/status-report/ai-fill`, { method: 'POST' }),
};
