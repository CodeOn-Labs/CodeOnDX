import { createRouter, createWebHistory, type RouteComponent, type RouteRecordRaw } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { screenTitle } from '@/shared/i18n/screen-title';
import { SCREENS, screenRoutes, type RoutedScreenKey, type ScreenKey } from './screens';
import { ADMIN_PERMISSIONS, ADMIN_PREFIX, resolveAdminEntry, resolveDefaultRoute } from './navigation';

declare module 'vue-router' {
  interface RouteMeta {
    /** screen_key trong shared/screens.json — route dựng từ đó nên luôn có (trừ route khung như /, /admin) */
    screen?: ScreenKey;
    /** Cần ít nhất một trong các quyền này */
    permission?: string[];
    public?: boolean;
    /** Đã đăng nhập thì không vào được (trang đăng nhập) */
    guestOnly?: boolean;
  }
}

const LAST_ROUTE_KEY = 'dx_last_route';
const NOT_TRACKED = ['/', '/auth/login', '/auth/logout'];

const MainLayout = () => import('@/components/layout/MainLayout.vue');
const AdminLayout = () => import('@/components/layout/AdminLayout.vue');

/**
 * Component của từng màn có `route` trong shared/screens.json. Đường dẫn, quyền, public… KHÔNG khai báo ở đây —
 * routeRecords() lấy từ screens.json. Thêm màn mới: thêm vào screens.json trước, TypeScript sẽ báo thiếu dòng ở đây.
 */
const PAGES: Record<RoutedScreenKey, () => Promise<{ default: RouteComponent }>> = {
  auth_login: () => import('@/modules/auth/screens/auth_login/LoginPage.vue'),
  auth_forgot_password: () => import('@/modules/auth/screens/auth_forgot_password/ForgotPasswordPage.vue'),
  auth_reset_password: () => import('@/modules/auth/screens/auth_reset_password/ResetPasswordPage.vue'),
  css_survey_public: () => import('@/modules/auth/screens/css_survey_public/CssSurveyPage.vue'),
  profile: () => import('@/modules/profile/screens/profile/ProfilePage.vue'),
  timesheet: () => import('@/modules/work/screens/timesheet/TimesheetPage.vue'),
  leave_request: () => import('@/modules/work/screens/leave_request/LeavePage.vue'),
  attendance: () => import('@/modules/work/screens/attendance/AttendancePage.vue'),
  effort: () => import('@/modules/effort/screens/effort/EffortPage.vue'),
  project_list: () => import('@/modules/project/screens/project_list/ProjectListPage.vue'),
  project_form: () => import('@/modules/project/screens/project_form/ProjectFormPage.vue'),
  project_detail: () => import('@/modules/project/screens/project_detail/ProjectDetailPage.vue'),
  employee_list: () => import('@/modules/employee/screens/employee_list/EmployeeListPage.vue'),
  employee_detail: () => import('@/modules/employee/screens/employee_detail/EmployeeDetailPage.vue'),
  admin_department_list: () => import('@/modules/admin/screens/admin_department_list/DepartmentListPage.vue'),
  admin_role_list: () => import('@/modules/admin/screens/admin_role_list/RoleListPage.vue'),
  admin_role_form: () => import('@/modules/admin/screens/admin_role_form/RoleFormPage.vue'),
  admin_rule_list: () => import('@/modules/admin/screens/admin_rule_list/RuleListPage.vue'),
  admin_insight_list: () => import('@/modules/admin/screens/admin_insight_list/InsightListPage.vue'),
  admin_insight_form: () => import('@/modules/admin/screens/admin_insight_form/InsightFormPage.vue'),
  admin_prompt_template_list: () => import('@/modules/admin/screens/admin_prompt_template_list/PromptTemplateListPage.vue'),
  admin_prompt_template_form: () => import('@/modules/admin/screens/admin_prompt_template_form/PromptTemplateFormPage.vue'),
  admin_integration_list: () => import('@/modules/admin/screens/admin_integration_list/IntegrationListPage.vue'),
  admin_category_list: () => import('@/modules/admin/screens/admin_category_list/CategoryListPage.vue'),
  admin_client_list: () => import('@/modules/admin/screens/admin_client_list/ClientListPage.vue'),
  admin_setting: () => import('@/modules/admin/screens/admin_setting/SettingPage.vue'),
  admin_pcv_criteria: () => import('@/modules/admin/screens/admin_pcv_criteria/PcvCriteriaPage.vue'),
  admin_css_template: () => import('@/modules/admin/screens/admin_css_template/CssTemplatePage.vue'),
};

/** Mọi route của các màn khớp điều kiện, dựng từ shared/screens.json (đường dẫn tuyệt đối, kể cả khi là route con). */
function routeRecords(match: (screen: ScreenKey) => boolean): RouteRecordRaw[] {
  return (Object.keys(PAGES) as RoutedScreenKey[]).filter(match).flatMap((screen) =>
    screenRoutes(screen).map(({ route, permission }) => ({
      path: route,
      component: PAGES[screen],
      meta: { screen, permission, public: SCREENS[screen].public, guestOnly: SCREENS[screen].guest_only },
    })),
  );
}

const isAdminScreen = (screen: ScreenKey) => SCREENS[screen].route?.startsWith(`${ADMIN_PREFIX}/`) ?? false;

const routes: RouteRecordRaw[] = [
  ...routeRecords((screen) => SCREENS[screen].public === true),
  {
    path: '/',
    component: MainLayout,
    children: [
      {
        path: '',
        redirect: () => {
          const auth = useAuthStore();
          let last: string | null = null;
          try {
            last = localStorage.getItem(LAST_ROUTE_KEY);
          } catch {
            last = null;
          }
          if (last && !NOT_TRACKED.includes(last)) {
            const target = router.resolve(last);
            if (target.matched.length > 0 && auth.can(target.meta.permission)) return last;
          }
          return resolveDefaultRoute(auth.can);
        },
      },
      ...routeRecords((screen) => !SCREENS[screen].public && !isAdminScreen(screen)),
      {
        path: ADMIN_PREFIX,
        component: AdminLayout,
        meta: { permission: ADMIN_PERMISSIONS },
        children: [
          { path: '', redirect: () => resolveAdminEntry(useAuthStore().can) },
          ...routeRecords(isAdminScreen),
        ],
      },
    ],
  },
  { path: '/:pathMatch(.*)*', redirect: '/' },
];

export const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach(async (to) => {
  const auth = useAuthStore();
  await auth.bootstrap();

  if (to.meta.public) {
    if (to.meta.guestOnly && auth.isAuthenticated) {
      const redirect = typeof to.query.redirect === 'string' ? to.query.redirect : '/';
      return redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : '/';
    }
    return true;
  }

  if (!auth.isAuthenticated) {
    return { path: '/auth/login', query: to.fullPath !== '/' ? { redirect: to.fullPath } : {} };
  }

  // Kiểm tra quyền cho mọi route khớp (cả route cha như /admin)
  const denied = to.matched.some((record) => record.meta.permission && !auth.can(record.meta.permission));
  if (denied) {
    const fallback = resolveDefaultRoute(auth.can);
    return fallback === to.path ? '/profile' : fallback;
  }
  return true;
});

router.afterEach((to) => {
  const screen = to.meta.screen;
  document.title = screen ? `${screenTitle(screen)} · DX Platform` : 'DX Platform';
  if (!to.meta.public && !NOT_TRACKED.includes(to.path)) {
    try {
      localStorage.setItem(LAST_ROUTE_KEY, to.path);
    } catch {
      // bỏ qua
    }
  }
});

/** Phiên hết hạn giữa chừng (refresh token không còn hiệu lực) → về trang đăng nhập. */
export function installSessionExpiredRedirect() {
  useAuthStore().onSessionExpired(() => {
    const current = router.currentRoute.value;
    if (!current.meta.public) {
      void router.replace({ path: '/auth/login', query: { redirect: current.fullPath } });
    }
  });
}
