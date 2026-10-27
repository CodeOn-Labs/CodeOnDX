// API quản trị hệ thống: Vai trò & quyền (D3), AI Insight, Prompt template (D5), Tích hợp (Q3).
// Hợp đồng: tools/mock-api/README.md.
import { request, requestFull } from '@/api/http';
import type { PagedResult } from '@/api/types';
import type { InsightItem } from '@/modules/ai/api/ai.api';
import type { ScreenKey } from '@/app/screens';

// ─── Vai trò & quyền ────────────────────────────────────────────────────────

export interface Permission {
  key: string;
  group: string;
  name: string;
  sortSeq: number;
}

export interface Role {
  id: string;
  code: string;
  name: string;
  description: string | null;
  /** Vai trò mặc định của hệ thống: không đổi tên, không xoá (vẫn sửa được quyền) */
  isSystem: boolean;
  permissions: string[];
  memberCount: number;
  updatedAt: string | null;
}

export interface RoleFilters {
  search: string;
  permissions: string[];
  types: Array<'system' | 'custom'>;
}
export const EMPTY_ROLE_FILTERS: RoleFilters = { search: '', permissions: [], types: [] };

export interface RoleSummary {
  total: number;
  system: number;
  custom: number;
  assignedUsers: number;
}

export interface RoleInput {
  name: string;
  description: string;
  permissions: string[];
}

export interface UserRef {
  id: string;
  fullName: string;
  email: string;
  avatarUrl: string | null;
  isActive: boolean;
}

// ─── AI Insight / Prompt template ───────────────────────────────────────────

export const INSIGHT_CONDITIONS = ['always', 'budget_anomaly', 'fte_shortage', 'fte_overload', 'unassigned_positions', 'missing_timesheet', 'schedule_weekly_mon', 'ai_smart_detect'] as const;
export const INSIGHT_DATA_SOURCES = [
  'timesheets', 'employees', 'budget', 'milestones', 'pcv', 'css', 'defect', 'leakage', 'ee', 'issues_tracker', 'git', 'skills',
  'documents', 'risks', 'changes', 'issues', 'cross_links', 'monthly_report', 'projects', 'leave', 'attendance',
] as const;
export type InsightCondition = (typeof INSIGHT_CONDITIONS)[number];
export type InsightDataSource = (typeof INSIGHT_DATA_SOURCES)[number];

/** Nguồn dữ liệu gợi ý theo màn (port DEFAULT_SOURCES_BY_MODULE). Màn không có trong bảng → lấy theo màn cha. */
export const DEFAULT_SOURCES: Partial<Record<ScreenKey, InsightDataSource[]>> = {
  timesheet: ['timesheets'],
  timesheet_day: ['timesheets'],
  timesheet_week: ['timesheets'],
  project_list: ['timesheets', 'budget'],
  project_detail: ['timesheets', 'budget'],
  project_detail_metrics: ['timesheets', 'budget'],
  project_detail_metrics_timeline: ['milestones', 'budget'],
  project_detail_metrics_pcv: ['pcv'],
  project_detail_metrics_css: ['css'],
  project_detail_metrics_defect: ['defect'],
  project_detail_metrics_leakage: ['leakage'],
  project_detail_metrics_ee: ['ee'],
  project_detail_monthly_report: ['monthly_report'],
  project_detail_management_documents: ['documents', 'cross_links'],
  project_detail_management_risk: ['risks', 'cross_links'],
  project_detail_management_change: ['changes', 'cross_links'],
  project_detail_management_issue: ['issues', 'cross_links'],
  employee_list: ['employees', 'skills'],
  employee_detail: ['employees', 'skills'],
  leave_request: ['leave'],
  attendance: ['attendance'],
  effort: ['timesheets', 'employees'],
};

export interface InsightConfig {
  id: string;
  name: string;
  screenKey: ScreenKey;
  triggerCondition: InsightCondition;
  dataSources: InsightDataSource[];
  allowedRoleIds: string[];
  dataPrompt: string;
  status: 'active' | 'paused';
  updatedAt: string | null;
}
export type InsightConfigInput = Omit<InsightConfig, 'id' | 'updatedAt'>;

export interface InsightConfigFilters {
  search: string;
  screens: string[];
  statuses: string[];
}

export type PromptScope = 'insight' | 'chat' | 'translate';
export type PromptStatus = 'draft' | 'active' | 'archived';

export interface PromptTemplate {
  id: string;
  name: string;
  scope: PromptScope;
  /** Rỗng = áp dụng mọi màn */
  screenKeys: ScreenKey[];
  template: string;
  description: string | null;
  status: PromptStatus;
  version: number;
  updatedAt: string | null;
}
export type PromptTemplateInput = Pick<PromptTemplate, 'name' | 'scope' | 'screenKeys' | 'template' | 'status'> & { description: string };

export interface PromptTemplateFilters {
  search: string;
  scopes: string[];
  statuses: string[];
}

// ─── Tích hợp ───────────────────────────────────────────────────────────────

export type ConnectorKey = 'Jira' | 'Base' | 'Bitrix';

export interface Integration {
  key: ConnectorKey;
  category: 'project_management' | 'work_management' | 'crm';
  enabled: boolean;
  /** Integrations:<Key>:UseMock trên server — chỉ đọc ở giao diện */
  useMock: boolean;
  settings: Record<string, string>;
  hasSecret: boolean;
  lastSyncAt: string | null;
  lastTestAt: string | null;
  lastTestOk: boolean | null;
}

export interface IntegrationPreviewItem {
  title: string;
  subtitle: string;
  url: string | null;
  updatedAt: string | null;
}

export const systemApi = {
  permissions: () => request<Permission[]>('/permissions'),
  roles: (f: RoleFilters, page: number, pageSize: number, signal?: AbortSignal) =>
    request<PagedResult<Role>>('/roles', { query: { search: f.search.trim() || undefined, permissions: f.permissions, types: f.types, page, pageSize }, signal }),
  roleSummary: (f: RoleFilters, signal?: AbortSignal) =>
    request<RoleSummary>('/roles/summary', { query: { search: f.search.trim() || undefined, permissions: f.permissions, types: f.types }, signal }),
  role: (id: string, signal?: AbortSignal) => request<Role>(`/roles/${id}`, { signal }),
  createRole: (input: RoleInput) => requestFull<Role>('/roles', { method: 'POST', body: input }),
  updateRole: (id: string, input: RoleInput) => requestFull<Role>(`/roles/${id}`, { method: 'PUT', body: input }),
  deleteRole: (id: string) => requestFull<null>(`/roles/${id}`, { method: 'DELETE' }),
  bulkUpdateRoles: (updates: Array<{ roleId: string; permissions: string[] }>) => requestFull<Role[]>('/roles/permissions', { method: 'PUT', body: { updates } }),
  roleUsers: (id: string, signal?: AbortSignal) => request<UserRef[]>(`/roles/${id}/users`, { signal }),
  assignUser: (roleId: string, userId: string) => requestFull<UserRef[]>(`/roles/${roleId}/users`, { method: 'POST', body: { userId } }),
  removeUser: (roleId: string, userId: string) => requestFull<UserRef[]>(`/roles/${roleId}/users/${userId}`, { method: 'DELETE' }),
  users: (signal?: AbortSignal) => request<UserRef[]>('/users', { signal }),

  insightConfigs: (f: InsightConfigFilters, page: number, pageSize: number, signal?: AbortSignal) =>
    request<PagedResult<InsightConfig>>('/ai/insight-configs', { query: { search: f.search.trim() || undefined, screens: f.screens, statuses: f.statuses, page, pageSize }, signal }),
  insightConfig: (id: string, signal?: AbortSignal) => request<InsightConfig>(`/ai/insight-configs/${id}`, { signal }),
  createInsightConfig: (input: InsightConfigInput) => requestFull<InsightConfig>('/ai/insight-configs', { method: 'POST', body: input }),
  updateInsightConfig: (id: string, input: InsightConfigInput) => requestFull<InsightConfig>(`/ai/insight-configs/${id}`, { method: 'PUT', body: input }),
  deleteInsightConfig: (id: string) => requestFull<null>(`/ai/insight-configs/${id}`, { method: 'DELETE' }),
  previewInsightConfig: (input: InsightConfigInput) => request<InsightItem[]>('/ai/insight-configs/preview', { method: 'POST', body: input }),

  promptTemplates: (f: PromptTemplateFilters, page: number, pageSize: number, signal?: AbortSignal) =>
    request<PagedResult<PromptTemplate>>('/ai/prompt-templates', { query: { search: f.search.trim() || undefined, scopes: f.scopes, statuses: f.statuses, page, pageSize }, signal }),
  promptTemplate: (id: string, signal?: AbortSignal) => request<PromptTemplate>(`/ai/prompt-templates/${id}`, { signal }),
  createPromptTemplate: (input: PromptTemplateInput) => requestFull<PromptTemplate>('/ai/prompt-templates', { method: 'POST', body: input }),
  updatePromptTemplate: (id: string, input: PromptTemplateInput) => requestFull<PromptTemplate>(`/ai/prompt-templates/${id}`, { method: 'PUT', body: input }),
  deletePromptTemplate: (id: string) => requestFull<null>(`/ai/prompt-templates/${id}`, { method: 'DELETE' }),
  previewPromptTemplate: (template: string) => request<{ output: string; variables: string[] }>('/ai/prompt-templates/preview', { method: 'POST', body: { template } }),

  integrations: () => request<Integration[]>('/integrations'),
  updateIntegration: (key: ConnectorKey, input: { enabled: boolean; settings: Record<string, string> }) =>
    requestFull<Integration>(`/integrations/${key}`, { method: 'PUT', body: input }),
  testIntegration: (key: ConnectorKey) => requestFull<Integration>(`/integrations/${key}/test`, { method: 'POST' }),
  previewIntegration: (key: ConnectorKey) => request<IntegrationPreviewItem[]>(`/integrations/${key}/preview`),
};
