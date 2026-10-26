import { request, requestFull } from '@/api/http';

export interface TenantInfo {
  id: string;
  code: string;
  name: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  fullName: string;
  avatarUrl: string | null;
  preferredLocale: 'vi' | 'en';
  employeeId: string | null;
  tenant: TenantInfo | null;
  roles: { id: string; code: string; name: string }[];
}

export interface AuthSession {
  accessToken: string;
  expiresIn: number;
  user: CurrentUser;
  permissions: string[];
}

export const authApi = {
  login: (email: string, password: string) =>
    requestFull<AuthSession>('/auth/login', { method: 'POST', body: { email, password }, skipAuthRefresh: true }),
  /** Dùng refresh token trong cookie httpOnly để lấy access token mới. */
  refresh: () => request<AuthSession>('/auth/refresh', { method: 'POST', skipAuthRefresh: true }),
  logout: () => request<null>('/auth/logout', { method: 'POST', skipAuthRefresh: true }),
  me: () => request<{ user: CurrentUser; permissions: string[] }>('/auth/me'),
};

export interface ResetTokenInfo {
  /** invite = nhận lời mời tạo tài khoản; reset = quên mật khẩu */
  kind: 'invite' | 'reset';
  email: string;
  expiresAt: string;
  passwordMinLength: number;
}

export interface Profile extends CurrentUser {
  phone: string | null;
  employee: {
    id: string;
    code: string;
    position: { id: string; code: string; name: string } | null;
    level: { id: string; code: string; name: string; color: string | null } | null;
    department: { id: string; name: string } | null;
    team: { id: string; name: string } | null;
    joinedDate: string | null;
    leftDate: string | null;
    isActive: boolean;
    skills: string[];
  } | null;
  projects: Array<{ id: string; code: string; name: string }>;
  lastLoginAt: string | null;
}

export interface ProfileInput {
  fullName?: string;
  phone?: string;
  preferredLocale?: 'vi' | 'en';
  avatarUrl?: string | null;
  skills?: string[];
}

/** Tài khoản (D4): quên / đặt lại mật khẩu, nhận lời mời, hồ sơ cá nhân. */
export const accountApi = {
  forgotPassword: (email: string) => requestFull<null>('/auth/forgot-password', { method: 'POST', body: { email }, skipAuthRefresh: true }),
  resetTokenInfo: (token: string) => request<ResetTokenInfo>(`/auth/reset-password/${encodeURIComponent(token)}`, { skipAuthRefresh: true }),
  resetPassword: (token: string, password: string, confirmPassword: string) =>
    requestFull<null>('/auth/reset-password', { method: 'POST', body: { token, password, confirmPassword }, skipAuthRefresh: true }),
  profile: (signal?: AbortSignal) => request<Profile>('/profile', { signal }),
  updateProfile: (input: ProfileInput) => requestFull<Profile>('/profile', { method: 'PUT', body: input }),
  changePassword: (currentPassword: string, password: string, confirmPassword: string) =>
    requestFull<null>('/profile/change-password', { method: 'POST', body: { currentPassword, password, confirmPassword } }),
};
