import { request, requestFull } from '@/api/http';
import type { MasterDataRef, PagedResult } from '@/api/types';

/** Dòng dự án trong danh sách — DTO camelCase của GET /api/v1/projects. */
export interface ProjectListItem {
  id: string;
  code: string;
  name: string;
  client: { id: string; name: string } | null;
  grade: MasterDataRef | null;
  status: MasterDataRef | null;
  riskLevel: MasterDataRef | null;
  startDate: string | null;
  endDate: string | null;
  budget: number | null;
  currencyCode: string | null;
  progressPercent: number | null;
  contractStaffFte: number;
  actualStaffFte: number;
  /** % budget đã dùng — BE tính (port getProjectBudgetPercent) */
  budgetUsedPercent: number;
  isDeleted: boolean;
  createdAt: string;
}

export interface ProjectSummary {
  total: number;
  running: number;
  endingSoon: number;
  planning: number;
  risky: number;
  endingSoonDays: number;
}

/** Bộ lọc danh sách dự án (giữ trong ui store để quay lại màn vẫn còn). */
export interface ProjectFilters {
  search: string;
  statusIds: string[];
  gradeIds: string[];
  clientIds: string[];
  riskLevelIds: string[];
  startFrom: string | null;
  endTo: string | null;
  deleted: boolean;
}

export const EMPTY_PROJECT_FILTERS: ProjectFilters = {
  search: '',
  statusIds: [],
  gradeIds: [],
  clientIds: [],
  riskLevelIds: [],
  startFrom: null,
  endTo: null,
  deleted: false,
};

function toQuery(f: ProjectFilters) {
  return {
    search: f.search.trim() || undefined,
    statusIds: f.statusIds,
    gradeIds: f.gradeIds,
    clientIds: f.clientIds,
    riskLevelIds: f.riskLevelIds,
    startFrom: f.startFrom || undefined,
    endTo: f.endTo || undefined,
    deleted: f.deleted || undefined,
  };
}

export const projectApi = {
  /** page bắt đầu từ 1; pageSize = 0 → lấy tất cả (dùng cho xuất file). */
  list: (filters: ProjectFilters, page: number, pageSize: number, signal?: AbortSignal) =>
    request<PagedResult<ProjectListItem>>('/projects', { query: { ...toQuery(filters), page, pageSize }, signal }),
  summary: (filters: ProjectFilters, signal?: AbortSignal) => request<ProjectSummary>('/projects/summary', { query: toQuery(filters), signal }),
  /** Import dự án từ Excel: mỗi dòng (kèm số dòng trong file) → BE gộp theo tên dự án; lỗi trả theo dòng + trường. */
  importProjects: (rows: Array<{ row: number; values: Record<string, string> }>) =>
    requestFull<{ created: number; errors: Array<{ row: number; field: string | null; message: string }> }>('/projects/import', { method: 'POST', body: { rows } }),
};
