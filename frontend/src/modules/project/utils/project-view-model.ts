// Port các hàm hiển thị của projectViewModel.ts — so khớp theo `code` của master data thay vì đoán theo tên.
import type { MasterDataRef } from '@/api/types';

export const DEFAULT_ENDING_SOON_DAYS = 30;
export const DEFAULT_BUDGET_WARNING_THRESHOLD = 75;

export type BadgeVariant = 'slate' | 'green' | 'amber' | 'red' | 'violet';

/** Lớp màu theo `metadata.color` của master data (giống COLOR_CLASSES bản gốc). */
export const COLOR_CLASSES: Record<string, string> = {
  red: 'bg-red-50 text-red-600 ring-red-500/20',
  amber: 'bg-amber-50 text-amber-600 ring-amber-500/20',
  pr: 'bg-pr/5 text-pr ring-pr/20',
  bl: 'bg-bl/5 text-bl ring-bl/20',
  slate: 'bg-slate-50 text-slate-500 ring-slate-500/10',
  green: 'bg-emerald-50 text-emerald-600 ring-emerald-500/20',
  emerald: 'bg-emerald-50 text-emerald-600 ring-emerald-500/20',
  blue: 'bg-blue-50 text-blue-600 ring-blue-500/20',
};

export const DOT_COLOR_CLASSES: Record<string, string> = {
  green: 'bg-emerald-500',
  emerald: 'bg-emerald-500',
  amber: 'bg-amber-500',
  slate: 'bg-slate-400',
  pr: 'bg-pr',
  bl: 'bg-bl',
  red: 'bg-red-500',
};

const VARIANT_TO_COLOR: Record<BadgeVariant, string> = { red: 'red', amber: 'amber', violet: 'pr', slate: 'slate', green: 'green' };

export function variantColor(variant: BadgeVariant) {
  return VARIANT_TO_COLOR[variant];
}

export function isHexColor(color: string | null | undefined): color is string {
  return !!color && color.startsWith('#');
}

/** Kiểu inline cho màu hex do quản trị tự đặt trong Danh mục. */
export function hexStyle(color: string) {
  return { backgroundColor: `${color}15`, color, boxShadow: `inset 0 0 0 1px ${color}33` };
}

export function gradeVariant(grade: MasterDataRef | null): BadgeVariant {
  switch (grade?.code) {
    case 'S':
      return 'red';
    case 'A':
      return 'amber';
    case 'C':
      return 'slate';
    default:
      return 'violet';
  }
}

export type StatusKey = 'running' | 'ending' | 'planning' | 'completed' | 'other';

export function statusKey(status: MasterDataRef | null): StatusKey {
  switch (status?.code) {
    case 'RUNNING':
      return 'running';
    case 'ENDING':
      return 'ending';
    case 'PLANNING':
    case undefined:
      return 'planning';
    case 'COMPLETED':
      return 'completed';
    default:
      return 'other';
  }
}

export function statusVariant(status: MasterDataRef | null): BadgeVariant {
  const key = statusKey(status);
  if (key === 'running') return 'green';
  if (key === 'ending') return 'amber';
  if (key === 'planning' && status) return 'violet';
  return 'slate';
}

export type RiskKey = 'high' | 'medium' | 'low' | 'none';

export function riskKey(risk: MasterDataRef | null): RiskKey {
  switch (risk?.code) {
    case 'HIGH':
      return 'high';
    case 'MEDIUM':
      return 'medium';
    case 'LOW':
      return 'low';
    default:
      return 'none';
  }
}

export function riskVariant(risk: MasterDataRef | null): BadgeVariant {
  const key = riskKey(risk);
  if (key === 'high') return 'red';
  if (key === 'medium') return 'amber';
  return 'green';
}

function formatDay(value: string | null | undefined) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()}`;
}

/** "dd/MM/yyyy – dd/MM/yyyy" như formatProjectTimeline bản gốc. */
export function formatProjectTimeline(startDate?: string | null, endDate?: string | null) {
  const start = formatDay(startDate);
  const end = formatDay(endDate);
  if (!start && !end) return '';
  if (!start) return end;
  if (!end) return start;
  return `${start} – ${end}`;
}

export type TimelineState = 'running' | 'not_started' | 'completed';

/** Trạng thái theo thời gian + % thời gian đã trôi qua (cột "Thời gian & Tiến độ"). */
export function timelineInfo(startDate: string | null, endDate: string | null, now = new Date()): { state: TimelineState; percent: number } {
  const start = startDate ? new Date(startDate) : null;
  const end = endDate ? new Date(endDate) : null;
  if (!start || !end) return { state: 'running', percent: 0 };
  const total = end.getTime() - start.getTime();
  const percent = total > 0 ? Math.min(100, Math.max(0, ((now.getTime() - start.getTime()) / total) * 100)) : 0;
  if (now < start) return { state: 'not_started', percent };
  if (now > end) return { state: 'completed', percent };
  return { state: 'running', percent };
}

/** Cùng ngưỡng màu của BudgetCell / ProgressCell bản gốc. */
export function percentTone(percent: number) {
  if (percent >= 75) return { text: 'text-emerald-700', gradient: 'from-emerald-400 to-emerald-600', shadow: 'shadow-[0_0_8px_rgba(16,185,129,0.3)]' };
  if (percent >= 50) return { text: 'text-blue-700', gradient: 'from-blue-400 to-blue-600', shadow: 'shadow-[0_0_8px_rgba(59,130,246,0.3)]' };
  if (percent >= 25) return { text: 'text-amber-700', gradient: 'from-amber-400 to-amber-600', shadow: 'shadow-[0_0_8px_rgba(245,158,11,0.3)]' };
  return { text: 'text-red-700', gradient: 'from-red-400 to-red-600', shadow: 'shadow-[0_0_8px_rgba(239,68,68,0.3)]' };
}
