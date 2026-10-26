// API tổ chức (D4 — tự quản lý trong phần mềm): Khách hàng, Phòng ban & Team, Nhân sự.
// Hợp đồng: tools/mock-api/README.md. Hàm ghi trả nguyên ErrorFullModel để màn hình hiện messageText của BE.
import { request, requestFull } from '@/api/http';
import type { MasterDataRef, PagedResult } from '@/api/types';

// ─── Khách hàng ─────────────────────────────────────────────────────────────

export interface Client {
  id: string;
  code: string;
  name: string;
  shortName: string | null;
  country: MasterDataRef | null;
  industry: MasterDataRef | null;
  contactName: string | null;
  contactEmail: string | null;
  defaultCurrency: string | null;
  isActive: boolean;
  isDeleted: boolean;
  projectsCount: number;
  createdAt: string;
}

export interface ClientFilters {
  search: string;
  countryIds: string[];
  industryIds: string[];
  /** '' = tất cả */
  status: '' | 'active' | 'inactive';
  deleted: boolean;
}

export const EMPTY_CLIENT_FILTERS: ClientFilters = { search: '', countryIds: [], industryIds: [], status: '', deleted: false };

export interface ClientSummary {
  total: number;
  active: number;
  inactive: number;
  totalProjects: number;
}

export interface ClientInput {
  name: string;
  code: string;
  countryId: string;
  industryId: string;
  contactName: string;
  contactEmail: string;
  defaultCurrency: string;
  isActive: boolean;
}

const clientQuery = (f: ClientFilters) => ({
  search: f.search.trim() || undefined,
  countryIds: f.countryIds,
  industryIds: f.industryIds,
  status: f.status || undefined,
  deleted: f.deleted || undefined,
});

// ─── Phòng ban & Team ───────────────────────────────────────────────────────

export interface EmployeeRef {
  id: string;
  fullName: string;
  email: string;
  avatarUrl: string | null;
}

export interface Team {
  id: string;
  departmentId: string;
  code: string;
  name: string;
  description: string | null;
  leader: EmployeeRef | null;
  sortSeq: number;
  isActive: boolean;
  memberCount: number;
}

export interface Department {
  id: string;
  code: string;
  name: string;
  description: string | null;
  manager: EmployeeRef | null;
  sortSeq: number;
  isActive: boolean;
  memberCount: number;
  teams: Team[];
}

export interface DepartmentInput {
  code: string;
  name: string;
  description: string;
  managerId: string;
  isActive: boolean;
}

export interface TeamInput {
  departmentId: string;
  code: string;
  name: string;
  description: string;
  leaderId: string;
  isActive: boolean;
}

// ─── Nhân sự ────────────────────────────────────────────────────────────────

/** Trạng thái phân bổ do BE tính theo quy tắc HR_OVERLOAD_ALLOCATION_THRESHOLD / HR_MIN_ALLOCATION_THRESHOLD. */
export type AllocationStatus = 'overloaded' | 'normal' | 'low' | 'bench' | 'resigned';
export const ALLOCATION_STATUSES: AllocationStatus[] = ['overloaded', 'normal', 'low', 'bench', 'resigned'];

export interface Employee {
  id: string;
  code: string;
  fullName: string;
  email: string;
  phone: string | null;
  position: MasterDataRef | null;
  level: MasterDataRef | null;
  department: { id: string; name: string } | null;
  team: { id: string; name: string } | null;
  employmentStatus: MasterDataRef | null;
  joinedDate: string | null;
  leftDate: string | null;
  avatarUrl: string | null;
  skills: string[];
  projects: Array<{ id: string; code: string; name: string; role: string; fte: number }>;
  totalFte: number;
  allocationPercent: number;
  allocationStatus: AllocationStatus;
  /** null = chưa có tài khoản đăng nhập */
  account: { status: 'active' | 'disabled' | 'invited'; email: string; lastLoginAt: string | null } | null;
  isDeleted: boolean;
  createdAt: string;
}

export interface EmployeeDetail extends Employee {
  leaveBalance: { year: number; entitledDays: number; carriedOverDays: number; adjustedDays: number } | null;
}

export interface EmployeeFilters {
  search: string;
  positions: string[];
  levelIds: string[];
  departmentIds: string[];
  teamIds: string[];
  allocation: AllocationStatus[];
  statuses: string[];
  deleted: boolean;
}

export const EMPTY_EMPLOYEE_FILTERS: EmployeeFilters = {
  search: '',
  positions: [],
  levelIds: [],
  departmentIds: [],
  teamIds: [],
  allocation: [],
  statuses: [],
  deleted: false,
};

export interface EmployeeSummary {
  total: number;
  inProjects: number;
  bench: number;
  overloaded: number;
  utilizationPercent: number;
  utilizationTarget: number;
}

export interface EmployeeInput {
  code: string;
  fullName: string;
  email: string;
  phone: string;
  positionCode: string;
  levelId: string;
  departmentId: string;
  teamId: string;
  statusCode: string;
  joinedDate: string | null;
  leftDate: string | null;
  skills: string[];
}

const employeeQuery = (f: EmployeeFilters) => ({
  search: f.search.trim() || undefined,
  positions: f.positions,
  levelIds: f.levelIds,
  departmentIds: f.departmentIds,
  teamIds: f.teamIds,
  allocation: f.allocation,
  statuses: f.statuses,
  deleted: f.deleted || undefined,
});

export const orgApi = {
  clients: (filters: ClientFilters, page: number, pageSize: number, signal?: AbortSignal) =>
    request<PagedResult<Client>>('/clients', { query: { ...clientQuery(filters), page, pageSize }, signal }),
  clientSummary: (filters: ClientFilters, signal?: AbortSignal) => request<ClientSummary>('/clients/summary', { query: clientQuery(filters), signal }),
  createClient: (input: ClientInput) => requestFull<Client>('/clients', { method: 'POST', body: input }),
  updateClient: (id: string, input: ClientInput) => requestFull<Client>(`/clients/${id}`, { method: 'PUT', body: input }),
  deleteClient: (id: string) => requestFull<null>(`/clients/${id}`, { method: 'DELETE' }),
  restoreClient: (id: string) => requestFull<Client>(`/clients/${id}/restore`, { method: 'POST' }),

  departments: () => request<Department[]>('/departments'),
  createDepartment: (input: DepartmentInput) => requestFull<Department>('/departments', { method: 'POST', body: input }),
  updateDepartment: (id: string, input: DepartmentInput) => requestFull<Department>(`/departments/${id}`, { method: 'PUT', body: input }),
  deleteDepartment: (id: string) => requestFull<null>(`/departments/${id}`, { method: 'DELETE' }),
  createTeam: (input: TeamInput) => requestFull<Team>('/teams', { method: 'POST', body: input }),
  updateTeam: (id: string, input: TeamInput) => requestFull<Team>(`/teams/${id}`, { method: 'PUT', body: input }),
  deleteTeam: (id: string) => requestFull<null>(`/teams/${id}`, { method: 'DELETE' }),

  employees: (filters: EmployeeFilters, page: number, pageSize: number, signal?: AbortSignal) =>
    request<PagedResult<Employee>>('/employees', { query: { ...employeeQuery(filters), page, pageSize }, signal }),
  employeeSummary: (filters: EmployeeFilters, signal?: AbortSignal) => request<EmployeeSummary>('/employees/summary', { query: employeeQuery(filters), signal }),
  employee: (id: string, signal?: AbortSignal) => request<EmployeeDetail>(`/employees/${id}`, { signal }),
  createEmployee: (input: EmployeeInput) => requestFull<Employee>('/employees', { method: 'POST', body: input }),
  updateEmployee: (id: string, input: EmployeeInput) => requestFull<Employee>(`/employees/${id}`, { method: 'PUT', body: input }),
  deleteEmployee: (id: string) => requestFull<null>(`/employees/${id}`, { method: 'DELETE' }),
  restoreEmployee: (id: string) => requestFull<Employee>(`/employees/${id}/restore`, { method: 'POST' }),
  inviteEmployee: (id: string) => requestFull<Employee>(`/employees/${id}/invite`, { method: 'POST' }),
};

/** Danh sách nhân sự rút gọn cho ô chọn (người quản lý, trưởng nhóm, thành viên dự án…). */
export async function employeeOptions(signal?: AbortSignal) {
  const page = await orgApi.employees(EMPTY_EMPLOYEE_FILTERS, 1, 0, signal);
  return page.items.filter((e) => e.allocationStatus !== 'resigned');
}
