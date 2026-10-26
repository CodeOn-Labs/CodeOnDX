// Nơi DUY NHẤT (cùng screen-title.ts) được ghép key i18n động — eslint tắt no-dynamic-keys riêng thư mục này.
import { i18n } from '@/i18n';

export type EnumName =
  | 'project_status'
  | 'risk_level'
  | 'timesheet_status'
  | 'rule_module'
  | 'category_type'
  | 'color_preset'
  | 'work_week'
  | 'permission_group'
  | 'insight_condition'
  | 'insight_data_source'
  | 'prompt_scope'
  | 'prompt_status'
  | 'integration_category'
  | 'connector'
  | 'leave_status'
  | 'attendance_source'
  | 'ot_day_kind'
  | 'duration_type'
  | 'risk_category'
  | 'risk_strategy'
  | 'risk_status'
  | 'risk_matrix_level'
  | 'risk_probability'
  | 'risk_impact'
  | 'change_source'
  | 'change_impact_level'
  | 'change_impact_area'
  | 'issue_type'
  | 'issue_kind';

/**
 * Nhãn của giá trị enum / master data theo mã: enumLabel('project_status', 'RUNNING') → "Đang chạy".
 * Không có key thì trả `fallback` (thường là tên master data do BE trả về).
 */
export function enumLabel(enumName: EnumName, code: string | null | undefined, fallback = ''): string {
  if (!code) return fallback;
  const key = `enum.${enumName}.${code.toLowerCase().replace(/-/g, '_')}`;
  return i18n.global.te(key) ? i18n.global.t(key) : fallback || code;
}
