import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { registerAuthHandlers, setAccessToken } from '@/api/http';
import { authApi, type AuthSession, type CurrentUser } from '@/modules/auth/api/auth.api';
import { queryClient } from '@/api/query-client';

type AuthStatus = 'unknown' | 'authenticated' | 'anonymous';

/**
 * Phiên đăng nhập (D2): access token chỉ nằm trong bộ nhớ; khi tải lại trang, bootstrap() dùng
 * refresh token trong cookie httpOnly để khôi phục phiên.
 */
export const useAuthStore = defineStore('auth', () => {
  const user = ref<CurrentUser | null>(null);
  const permissions = ref<string[]>([]);
  const status = ref<AuthStatus>('unknown');
  let bootstrapping: Promise<void> | null = null;
  let expiredHandler: (() => void) | null = null;

  const isAuthenticated = computed(() => status.value === 'authenticated');

  function applySession(session: AuthSession) {
    setAccessToken(session.accessToken);
    user.value = session.user;
    permissions.value = session.permissions;
    status.value = 'authenticated';
  }

  function clearSession() {
    setAccessToken(null);
    user.value = null;
    permissions.value = [];
    status.value = 'anonymous';
    queryClient.clear();
  }

  async function refresh(): Promise<boolean> {
    try {
      applySession(await authApi.refresh());
      return true;
    } catch {
      return false;
    }
  }

  registerAuthHandlers({
    refresh,
    onUnauthorized: () => {
      clearSession();
      expiredHandler?.();
    },
  });

  /** Router đăng ký: phiên hết hạn giữa chừng → về trang đăng nhập. */
  function onSessionExpired(handler: () => void) {
    expiredHandler = handler;
  }

  function bootstrap(): Promise<void> {
    if (status.value !== 'unknown') return Promise.resolve();
    bootstrapping ??= refresh().then((ok) => {
      if (!ok) status.value = 'anonymous';
    });
    return bootstrapping;
  }

  async function login(email: string, password: string) {
    const result = await authApi.login(email, password);
    applySession(result.data);
    return result.messageText;
  }

  async function logout() {
    try {
      await authApi.logout();
    } finally {
      clearSession();
    }
  }

  function can(permission?: string | string[]): boolean {
    if (!permission || (Array.isArray(permission) && permission.length === 0)) return true;
    const keys = Array.isArray(permission) ? permission : [permission];
    return keys.some((k) => permissions.value.includes(k));
  }

  /** Cập nhật thông tin người dùng đang đăng nhập sau khi sửa hồ sơ (tên, ảnh, ngôn ngữ). */
  function patchUser(fields: Partial<CurrentUser>) {
    if (user.value) user.value = { ...user.value, ...fields };
  }

  return { user, permissions, status, isAuthenticated, bootstrap, login, logout, refresh, can, onSessionExpired, patchUser };
});
