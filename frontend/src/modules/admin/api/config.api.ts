// API cấu hình theo tenant (D12): Danh mục, Quy tắc, Cài đặt chung, Ngày lễ, Biểu mẫu PCV, Biểu mẫu CSS.
// Hợp đồng: tools/mock-api/README.md. Mọi hàm ghi trả nguyên ErrorFullModel để màn hình hiện messageText của BE.
import { request, requestFull } from '@/api/http';
import type { MasterDataType } from '@/api/master-data.api';
import type { PagedResult, SystemRule } from '@/api/types';

// ─── Danh mục ───────────────────────────────────────────────────────────────

/** Loại danh mục quản trị sửa được — cùng thứ tự với BE (CATEGORY_TYPES trong mock API). */
export const CATEGORY_TYPES = [
  'project_status',
  'project_level',
  'risk_level',
  'hr_level',
  'job_position',
  'performance_level',
  'task_type',
  'tech_request_type',
  'employee_status',
  'leave_type',
  'link_type',
  'currency',
  'country',
  'industry',
] as const satisfies readonly MasterDataType[];

export type CategoryType = (typeof CATEGORY_TYPES)[number];

export interface Category {
  id: string;
  type: CategoryType;
  code: string;
  name: string;
  nameEn: string | null;
  description: string | null;
  /** Tên màu preset (slate, blue…) hoặc mã HEX #RRGGBB */
  color: string | null;
  sortSeq: number;
  /** Thuộc tính mở rộng; `performance_level` có `coefficient` (hệ số hiệu suất) */
  metadata: Record<string, unknown>;
  isActive: boolean;
  /** Danh mục do hệ thống tự sinh (vd loại công việc OT/LEAVE) — không xoá được */
  isSystem: boolean;
  updatedAt: string | null;
}

export interface CategoryInput {
  type: CategoryType;
  code: string;
  name: string;
  nameEn: string;
  description: string;
  color: string;
  sortSeq: number;
  metadata: Record<string, unknown>;
  isActive: boolean;
}

export interface CategoryFilters {
  search: string;
  types: CategoryType[];
}

// ─── Cài đặt chung / ngày lễ / biểu mẫu ─────────────────────────────────────

/** Cài đặt chung: mọi giá trị là chuỗi (bảng system_settings). */
export type Settings = Record<string, string>;

export interface Holiday {
  id: string;
  date: string;
  name: string;
  isPaid: boolean;
}

export interface PcvTemplateItem {
  id: string;
  category: string;
  label: string;
  sortSeq: number;
}
export type PcvItemInput = Pick<PcvTemplateItem, 'category' | 'label'>;

export interface CssFormContent {
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
export type CssQuestionInput = Pick<CssQuestion, 'label' | 'required'>;

export const configApi = {
  categories: (filters: CategoryFilters, page: number, pageSize: number, signal?: AbortSignal) =>
    request<PagedResult<Category>>('/categories', { query: { search: filters.search, types: filters.types, page, pageSize }, signal }),
  createCategory: (input: CategoryInput) => requestFull<Category>('/categories', { method: 'POST', body: input }),
  updateCategory: (id: string, input: CategoryInput) => requestFull<Category>(`/categories/${id}`, { method: 'PUT', body: input }),
  deleteCategory: (id: string) => requestFull<null>(`/categories/${id}`, { method: 'DELETE' }),

  rules: () => request<SystemRule[]>('/system-rules'),
  updateRule: (key: string, value: number | boolean, status: number) =>
    requestFull<SystemRule>(`/system-rules/${key}`, { method: 'PUT', body: { value, status } }),

  settings: () => request<Settings>('/settings'),
  updateSettings: (changes: Settings) => requestFull<Settings>('/settings', { method: 'PUT', body: changes }),

  holidays: (year: number) => request<Holiday[]>('/holidays', { query: { year } }),
  createHoliday: (input: Omit<Holiday, 'id'>) => requestFull<Holiday>('/holidays', { method: 'POST', body: input }),
  deleteHoliday: (id: string) => requestFull<null>(`/holidays/${id}`, { method: 'DELETE' }),

  pcvItems: () => request<PcvTemplateItem[]>('/pcv-template-items'),
  createPcvItem: (input: PcvItemInput) => requestFull<PcvTemplateItem>('/pcv-template-items', { method: 'POST', body: input }),
  updatePcvItem: (id: string, input: PcvItemInput) => requestFull<PcvTemplateItem>(`/pcv-template-items/${id}`, { method: 'PUT', body: input }),
  deletePcvItem: (id: string) => requestFull<null>(`/pcv-template-items/${id}`, { method: 'DELETE' }),

  cssContent: () => request<CssFormContent>('/css-template'),
  updateCssContent: (input: CssFormContent) => requestFull<CssFormContent>('/css-template', { method: 'PUT', body: input }),
  cssQuestions: () => request<CssQuestion[]>('/css-template/questions'),
  createCssQuestion: (input: CssQuestionInput) => requestFull<CssQuestion>('/css-template/questions', { method: 'POST', body: input }),
  updateCssQuestion: (id: string, input: CssQuestionInput) =>
    requestFull<CssQuestion>(`/css-template/questions/${id}`, { method: 'PUT', body: input }),
  deleteCssQuestion: (id: string) => requestFull<null>(`/css-template/questions/${id}`, { method: 'DELETE' }),
};

/** Giữ tên cũ cho các màn đã có — helper nằm ở api/http.ts. */
export { firstFieldErrors } from '@/api/http';
