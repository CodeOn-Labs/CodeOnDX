// Cấu hình hiển thị của từng loại danh mục: màu + icon (port TYPE_CONFIG_MAP của CategoryTable.tsx).
import type { Component } from 'vue';
import {
  Activity, AlertTriangle, Briefcase, Building2, CalendarOff, ClipboardList, Coins, Gauge, Globe, Layers, Link, ShieldCheck, UserCheck,
} from '@lucide/vue';
import type { CategoryType } from '@/modules/admin/api/config.api';

export const CATEGORY_TYPE_STYLE: Record<CategoryType, { color: string; icon: Component }> = {
  project_status: { color: '#059669', icon: Activity },
  project_level: { color: '#2563eb', icon: Layers },
  risk_level: { color: '#d97706', icon: AlertTriangle },
  hr_level: { color: '#7c3aed', icon: ShieldCheck },
  job_position: { color: '#4b5563', icon: Briefcase },
  performance_level: { color: '#0d9488', icon: Gauge },
  task_type: { color: '#0369a1', icon: ClipboardList },
  employee_status: { color: '#16a34a', icon: UserCheck },
  leave_type: { color: '#ea580c', icon: CalendarOff },
  link_type: { color: '#db2777', icon: Link },
  currency: { color: '#ca8a04', icon: Coins },
  country: { color: '#0891b2', icon: Globe },
  industry: { color: '#9333ea', icon: Building2 },
};

/** Kiểu dữ liệu của một thuộc tính mở rộng (metadata). */
export type MetaValueType = 'string' | 'number' | 'boolean';

/** Thuộc tính gợi ý theo loại danh mục — nhãn hiển thị lấy qua i18n ở CategoryDialog. */
export const SUGGESTED_META: Partial<Record<CategoryType, Array<{ key: string; type: MetaValueType }>>> = {
  project_status: [{ key: 'is_closed', type: 'boolean' }, { key: 'allow_timesheet', type: 'boolean' }],
  project_level: [{ key: 'min_members', type: 'number' }, { key: 'max_members', type: 'number' }],
  risk_level: [{ key: 'severity_score', type: 'number' }],
  hr_level: [{ key: 'is_billable', type: 'boolean' }, { key: 'standard_hourly_rate', type: 'number' }, { key: 'abbreviation', type: 'string' }],
  job_position: [{ key: 'is_billable', type: 'boolean' }, { key: 'department', type: 'string' }],
  task_type: [{ key: 'is_billable', type: 'boolean' }, { key: 'is_overtime', type: 'boolean' }],
  employee_status: [{ key: 'working', type: 'boolean' }],
  leave_type: [{ key: 'paid', type: 'boolean' }, { key: 'deduct_balance', type: 'boolean' }, { key: 'need_attachment', type: 'boolean' }],
  link_type: [{ key: 'kind', type: 'string' }],
  currency: [{ key: 'symbol', type: 'string' }],
};

/** Khoá metadata có ô nhập riêng trong form — không lặp lại ở danh sách "thuộc tính bổ sung". */
export const RESERVED_META_KEYS = ['color', 'rank', 'system', 'coefficient'];
