// Client HTTP duy nhất của frontend: luôn đọc ErrorFullModel (Q2), gửi Accept-Language theo ngôn ngữ đang chọn,
// tự gọi /auth/refresh một lần khi gặp 401 rồi thử lại request.
import { currentLocale, i18n } from '@/i18n';
import type { ErrorFullModel } from './types';

export const API_BASE = '/api/v1';

export class ApiError extends Error {
  readonly status: number;
  readonly data: unknown;

  constructor(status: number, message: string, data: unknown = null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.data = data;
  }

  /** Lỗi 400: data = { field: [message] } */
  get fieldErrors(): Record<string, string[]> {
    return this.status === 400 && this.data && typeof this.data === 'object' ? (this.data as Record<string, string[]>) : {};
  }
}

type Query = Record<string, string | number | boolean | string[] | null | undefined>;

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  query?: Query;
  body?: unknown;
  signal?: AbortSignal;
  /** Không tự refresh token khi 401 (dùng cho chính các API xác thực). */
  skipAuthRefresh?: boolean;
}

// ─── Token (giữ trong bộ nhớ, không lưu localStorage — D2) ──────────────────

let accessToken: string | null = null;
let refreshHandler: (() => Promise<boolean>) | null = null;
let unauthorizedHandler: (() => void) | null = null;
let refreshing: Promise<boolean> | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

/** auth store đăng ký cách refresh và xử lý khi phiên hết hạn (tránh import vòng). */
export function registerAuthHandlers(handlers: { refresh: () => Promise<boolean>; onUnauthorized: () => void }) {
  refreshHandler = handlers.refresh;
  unauthorizedHandler = handlers.onUnauthorized;
}

function tryRefresh(): Promise<boolean> {
  if (!refreshHandler) return Promise.resolve(false);
  refreshing ??= refreshHandler().finally(() => {
    refreshing = null;
  });
  return refreshing;
}

// ─── Request ────────────────────────────────────────────────────────────────

function buildUrl(path: string, query?: Query): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value)) {
      if (value.length > 0) params.set(key, value.join(','));
    } else {
      params.set(key, String(value));
    }
  }
  const qs = params.toString();
  return `${API_BASE}${path}${qs ? `?${qs}` : ''}`;
}

async function send(path: string, options: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
    'Accept-Language': currentLocale(),
  };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;

  try {
    return await fetch(buildUrl(path, options.query), {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      credentials: 'same-origin',
      signal: options.signal,
    });
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') throw err;
    throw new ApiError(0, i18n.global.t('error.network'));
  }
}

async function parse<T>(response: Response): Promise<ErrorFullModel<T>> {
  const text = await response.text();
  try {
    const body = JSON.parse(text) as ErrorFullModel<T>;
    if (typeof body === 'object' && body !== null && 'status' in body && 'messageText' in body) return body;
  } catch {
    // không phải JSON (proxy lỗi, server chưa chạy…) — xử lý bên dưới
  }
  throw new ApiError(response.status, i18n.global.t('error.server_unreachable'));
}

/** Gọi API, trả nguyên ErrorFullModel (dùng khi cần messageText của thao tác thành công). */
export async function requestFull<T>(path: string, options: RequestOptions = {}): Promise<ErrorFullModel<T>> {
  let response = await send(path, options);

  if (response.status === 401 && !options.skipAuthRefresh) {
    const refreshed = await tryRefresh();
    if (refreshed) {
      response = await send(path, options);
    }
    if (response.status === 401) unauthorizedHandler?.();
  }

  const body = await parse<T>(response);
  if (!response.ok || body.status >= 400) {
    throw new ApiError(body.status || response.status, body.messageText, body.data);
  }
  return body;
}

/** Gọi API, trả `data`. */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  return (await requestFull<T>(path, options)).data;
}

/** Lỗi 400 của BE → { field: "thông báo đầu tiên" } để gắn vào form. */
export function firstFieldErrors(err: unknown): Record<string, string> {
  if (!(err instanceof ApiError) || err.status !== 400) return {};
  return Object.fromEntries(Object.entries(err.fieldErrors).map(([field, list]) => [field, Array.isArray(list) ? String(list[0] ?? '') : String(list)]));
}
