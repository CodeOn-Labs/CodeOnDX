// Điều hướng: mục Sidebar, tab khu Quản trị, thứ tự chọn trang mặc định.
// Chỉ khai báo screen_key — đường dẫn và quyền luôn lấy từ shared/screens.json (qua SCREENS), không viết lại ở đây.
import type { Component } from 'vue';
import {
  BarChart3, Building, CalendarOff, CalendarRange, ClipboardCheck, Clock, FileText, Folder, GitBranch, Lightbulb, Network, Plug, Settings,
  Settings2, ShieldCheck, Smile, UserCheck, UserCog, Users,
} from '@lucide/vue';
import { i18n } from '@/i18n';
import { SCREEN_KEYS, SCREENS, type RoutedScreenKey } from './screens';

export interface NavItem {
  screen: RoutedScreenKey;
  icon: Component;
  /** Hàm để nhãn đổi theo ngôn ngữ đang chọn */
  label: () => string;
}

/** Nhóm tab khu Quản trị: một tab trên thanh chính, các màn con hiện ở thanh tab phụ ngay bên dưới. */
export interface NavGroup {
  key: string;
  icon: Component;
  label: () => string;
  children: NavItem[];
}

export type AdminTab = NavItem | NavGroup;

type Can = (permission?: string[]) => boolean;

const t = i18n.global.t;

/** Thanh điều hướng dọc (Sidebar.vue), theo thứ tự hiển thị. */
export const SIDEBAR_ITEMS: NavItem[] = [
  { screen: 'effort', icon: CalendarRange, label: () => t('menu.effort') },
  { screen: 'timesheet', icon: Clock, label: () => t('menu.timesheets') },
  { screen: 'attendance', icon: UserCheck, label: () => t('menu.attendance') },
  { screen: 'leave_request', icon: CalendarOff, label: () => t('menu.leave') },
  { screen: 'project_list', icon: Folder, label: () => t('menu.projects') },
];

/**
 * Thanh tab khu Quản trị (AdminLayout.vue), theo thứ tự hiển thị. Nhóm chỉ hiện khi người dùng có quyền với ít nhất một màn con;
 * bấm nhóm mở màn con đầu tiên có quyền. Tab / màn con đầu tiên người dùng có quyền là trang mở khi vào /admin.
 */
export const ADMIN_TABS: AdminTab[] = [
  {
    key: 'hrm',
    icon: UserCog,
    label: () => t('menu.admin_hrm'),
    children: [
      { screen: 'employee_list', icon: Users, label: () => t('menu.admin_employees') },
      { screen: 'admin_department_list', icon: Network, label: () => t('menu.admin_departments') },
      { screen: 'admin_role_list', icon: ShieldCheck, label: () => t('menu.admin_roles') },
    ],
  },
  {
    key: 'insight',
    icon: Lightbulb,
    label: () => t('menu.admin_insight_group'),
    children: [
      { screen: 'admin_rule_list', icon: GitBranch, label: () => t('menu.admin_rules') },
      { screen: 'admin_insight_list', icon: BarChart3, label: () => t('menu.admin_insights') },
      { screen: 'admin_prompt_template_list', icon: FileText, label: () => t('menu.admin_prompt_templates') },
    ],
  },
  { screen: 'admin_integration_list', icon: Plug, label: () => t('menu.admin_integrations') },
  { screen: 'admin_client_list', icon: Building, label: () => t('menu.admin_clients') },
  { screen: 'admin_category_list', icon: Settings2, label: () => t('menu.admin_categories') },
  { screen: 'admin_setting', icon: Settings, label: () => t('menu.admin_settings') },
  { screen: 'admin_pcv_criteria', icon: ClipboardCheck, label: () => t('menu.admin_pcv_criteria') },
  { screen: 'admin_css_template', icon: Smile, label: () => t('menu.admin_css_template') },
];

export const isNavGroup = (tab: AdminTab): tab is NavGroup => 'children' in tab;

/** Thứ tự chọn trang mặc định sau đăng nhập — giữ như ROUTE_PRIORITY của bản React; không có màn nào thì về Hồ sơ cá nhân. */
const DEFAULT_SCREENS: RoutedScreenKey[] = [
  'timesheet',
  'project_list',
  'employee_list',
  'admin_setting',
  'admin_role_list',
  'admin_insight_list',
  'admin_integration_list',
];

export const ADMIN_PREFIX = '/admin';

/** Có ít nhất một quyền này thì vào được khu Quản trị = hợp quyền của mọi màn có route dưới /admin. */
export const ADMIN_PERMISSIONS: string[] = [
  ...new Set(SCREEN_KEYS.filter((key) => SCREENS[key].route?.startsWith(`${ADMIN_PREFIX}/`)).flatMap((key) => SCREENS[key].permission ?? [])),
];

export const navPath = (item: NavItem): string => SCREENS[item.screen].route as string;
export const navPermission = (item: NavItem): string[] | undefined => SCREENS[item.screen].permission;

export function resolveDefaultRoute(can: Can): string {
  const screen = DEFAULT_SCREENS.find((key) => can(SCREENS[key].permission)) ?? 'profile';
  return SCREENS[screen].route as string;
}

export function resolveAdminEntry(can: Can): string {
  const item = ADMIN_TABS.flatMap((tab) => (isNavGroup(tab) ? tab.children : [tab])).find((i) => can(navPermission(i)));
  return item ? navPath(item) : '/';
}
