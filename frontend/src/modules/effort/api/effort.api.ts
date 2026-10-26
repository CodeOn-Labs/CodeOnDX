// API lưới Công số. Hợp đồng: tools/mock-api/README.md (GET/POST /effort, PATCH/DELETE /effort/{id}).
import { request, requestFull } from '@/api/http';
import type { MasterDataRef } from '@/api/types';

export interface EffortRow {
  id: string;
  month: string;
  employee: { id: string; code: string; fullName: string; email: string; avatarUrl: string | null } | null;
  teamId: string | null;
  teamName: string | null;
  /** Team gốc trong hồ sơ nhân sự (cột "Team gốc") */
  homeTeamName: string | null;
  departmentId: string | null;
  departmentName: string | null;
  projectId: string;
  projectName: string;
  role: string;
  note: string | null;
  hd: number;
  ttEffort: number;
  otEffort: number;
  performanceLevel: MasterDataRef | null;
  /** Hệ số hiệu suất = performance_level.metadata.coefficient */
  performance: number;
  allocateRate: number;
}

export interface EffortMonth {
  month: string;
  canEdit: boolean;
  rows: EffortRow[];
  departments: Array<{ id: string; name: string }>;
  teams: Array<{ id: string; name: string; departmentId: string }>;
  projects: Array<{ id: string; name: string; departmentId: string | null }>;
  roles: string[];
  performanceLevels: Array<MasterDataRef & { coefficient: number }>;
}

export type EffortPatch = Partial<{
  hd: number;
  ttEffort: number;
  otEffort: number;
  allocateRate: number;
  performanceLevelId: string;
  teamId: string;
  role: string;
  note: string;
  projectId: string;
  departmentId: string;
}>;

export interface EffortAddInput {
  month: string;
  departmentId: string;
  teamId: string;
  projectId: string;
  role: string;
  employeeId: string;
}

export const effortApi = {
  month: (month: string, signal?: AbortSignal) => request<EffortMonth>('/effort', { query: { month }, signal }),
  update: (id: string, patch: EffortPatch) => requestFull<EffortRow[]>(`/effort/${id}`, { method: 'PATCH', body: patch }),
  add: (input: EffortAddInput) => requestFull<EffortRow>('/effort', { method: 'POST', body: input }),
  remove: (id: string) => requestFull<null>(`/effort/${id}`, { method: 'DELETE' }),
};
