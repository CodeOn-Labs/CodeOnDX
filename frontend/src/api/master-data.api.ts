import { request } from './http';
import type { ClientOption, MasterDataItem, PagedResult, SystemRule } from './types';

export type MasterDataType =
  | 'project_status'
  | 'project_level'
  | 'risk_level'
  | 'hr_level'
  | 'job_position'
  | 'performance_level'
  | 'task_type'
  | 'tech_request_type'
  | 'employee_status'
  | 'leave_type'
  | 'link_type'
  | 'currency'
  | 'country'
  | 'industry';

/** Danh mục dùng chung (master data, khách hàng, quy tắc hệ thống) — tên đã dịch theo Accept-Language. */
export const masterDataApi = {
  list: (types: MasterDataType[]) => request<MasterDataItem[]>('/master-data', { query: { type: types } }),
  clients: () => request<PagedResult<ClientOption>>('/clients', { query: { pageSize: 0, status: 'active' } }).then((r) => r.items),
  systemRules: () => request<SystemRule[]>('/system-rules'),
};
