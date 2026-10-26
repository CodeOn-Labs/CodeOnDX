#!/usr/bin/env node
/**
 * Mock API — đứng thay BE .NET 10 khi phát triển frontend (chưa có BE).
 *
 *   node tools/mock-api/server.mjs            # http://127.0.0.1:5080
 *   MOCK_API_PORT=5081 MOCK_LATENCY_MS=0 node tools/mock-api/server.mjs
 *
 * - Không cần cài thư viện (Node ≥ 20).
 * - Đọc dữ liệu từ mock-data/seed + mock-data/ai, câu thông báo từ backend/src/Dx.Api/Locales (Q1).
 * - Mọi response là ErrorFullModel { status, messageText, data }, HTTP status = status (Q2).
 * - Hợp đồng REST giống BE thật (/api/v1/...), nên frontend không phải sửa khi chuyển sang BE.
 *   Danh sách endpoint: tools/mock-api/README.md.
 */
import { createServer } from 'node:http';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ApiError, badRequest, cultureOf, dateParam, forbidden, listParam, notFound, paginate, readJson, send } from './lib/http.mjs';
import { createLocalizer } from './lib/localizer.mjs';
import { createStore, today } from './lib/store.mjs';
import { configRoutes } from './lib/config-routes.mjs';
import { orgRoutes } from './lib/org-routes.mjs';
import { systemRoutes } from './lib/system-routes.mjs';
import { accountRoutes } from './lib/account-routes.mjs';
import { workRoutes } from './lib/work-routes.mjs';
import { effortRoutes } from './lib/effort-routes.mjs';
import { projectRoutes } from './lib/project-routes.mjs';
import { projectExtraRoutes } from './lib/project-extra-routes.mjs';
import {
  checkLoginRate, clearLoginFailures, configureBackendJwt, issueTokens, recordLoginFailure, refreshCookie, requireUserId, revoke, rotateRefresh, verifyIdentityV3,
} from './lib/auth.mjs';

const REPO = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const PORT = Number(process.env.MOCK_API_PORT || 5080);
const HOST = process.env.MOCK_API_HOST || '127.0.0.1';
const LATENCY = Number(process.env.MOCK_LATENCY_MS ?? 250);
const FAILURE_RATE = Number(process.env.MOCK_FAILURE_RATE ?? 0);

const localizer = createLocalizer(join(REPO, 'backend/src/Dx.Api/Locales'));
const store = createStore(process.env.MOCK_DATA_ROOT || join(REPO, 'mock-data'));
const screens = JSON.parse(readFileSync(join(REPO, 'shared/screens.json'), 'utf8'));

/**
 * Cấu hình JWT của BE .NET (Jwt:Issuer/Audience/SigningKey) để mock nhận access token do BE cấp
 * khi frontend chạy hybrid. Khoá: MOCK_BACKEND_JWT_KEY, hoặc đọc backend/src/Dx.Api/appsettings(.Secrets).json.
 * MOCK_BACKEND_JWT=0 để tắt.
 */
function backendJwtSettings() {
  if (process.env.MOCK_BACKEND_JWT === '0') return {};
  const apiDir = join(REPO, 'backend/src/Dx.Api');
  const jwt = {};
  for (const file of ['appsettings.json', 'appsettings.Development.json', 'appsettings.Secrets.json']) {
    const path = join(apiDir, file);
    if (!existsSync(path)) continue;
    try {
      const section = JSON.parse(readFileSync(path, 'utf8').replace(/^﻿/, '')).Jwt ?? {};
      Object.assign(jwt, Object.fromEntries(Object.entries(section).filter(([, v]) => v !== '')));
    } catch {
      console.warn(`Bỏ qua ${file}: không đọc được JSON`);
    }
  }
  return { signingKey: process.env.MOCK_BACKEND_JWT_KEY || jwt.SigningKey, issuer: jwt.Issuer, audience: jwt.Audience };
}
const acceptsBackendJwt = configureBackendJwt(backendJwtSettings());

// ─── Ngữ cảnh request ─────────────────────────────────────────────────────

function currentUser(req) {
  const userId = requireUserId(req);
  const user = store.db.users.find((u) => u.id === userId && u.is_active);
  if (!user) throw new ApiError(401, 'error.unauthorized');
  return { user, permissions: store.permissionsOfUser(user.id) };
}

function requirePermission(ctx, key) {
  if (!ctx.permissions.includes(key)) throw forbidden();
}

function projectFilters(url) {
  const errors = {};
  const filters = {
    search: url.searchParams.get('search') || '',
    statusIds: listParam(url, 'statusIds'),
    gradeIds: listParam(url, 'gradeIds'),
    clientIds: listParam(url, 'clientIds'),
    riskLevelIds: listParam(url, 'riskLevelIds'),
    startFrom: dateParam(url, 'startFrom', errors),
    endTo: dateParam(url, 'endTo', errors),
    deleted: url.searchParams.get('deleted') === 'true',
  };
  if (filters.search.length > 200) errors.search = [{ key: 'validation.max_length', args: { max: 200 } }];
  if (Object.keys(errors).length > 0) throw badRequest(errors);
  return filters;
}

// ─── Định tuyến ───────────────────────────────────────────────────────────

/** @type {Array<[string, RegExp, (ctx: any) => Promise<{status?: number, key?: string, data?: unknown, args?: object, headers?: object}>]>} */
const routes = [
  ['GET', /^\/health$/, async () => ({ data: { status: 'ok', mode: 'mock', today: today() } })],

  // Xác thực
  ['POST', /^\/api\/v1\/auth\/login$/, async ({ req, culture }) => {
    const ip = req.socket.remoteAddress || 'local';
    checkLoginRate(ip);
    const body = await readJson(req);
    const errors = {};
    const email = String(body.email || '').trim();
    const password = String(body.password || '');
    if (!email) errors.email = ['validation.required'];
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = ['validation.invalid_email'];
    if (!password) errors.password = ['validation.required'];
    if (Object.keys(errors).length > 0) throw badRequest(errors);

    const user = store.db.users.find((u) => u.normalized_email === email.toUpperCase());
    if (!user || !verifyIdentityV3(password, user.password_hash)) {
      recordLoginFailure(ip);
      throw new ApiError(401, 'auth_login.err_invalid_credentials');
    }
    if (!user.is_active) throw new ApiError(403, 'auth_login.err_account_disabled');
    clearLoginFailures(ip);
    user.last_login_at = new Date().toISOString();
    const tokens = issueTokens(user.id);
    return {
      key: 'auth_login.msg_success',
      data: { accessToken: tokens.accessToken, expiresIn: tokens.expiresIn, user: store.userDto(user, culture), permissions: store.permissionsOfUser(user.id) },
      headers: { 'Set-Cookie': refreshCookie(tokens.refreshToken, req.headers['x-forwarded-proto'] === 'https') },
    };
  }],
  ['POST', /^\/api\/v1\/auth\/refresh$/, async ({ req, culture }) => {
    const tokens = rotateRefresh(req);
    const owner = store.db.users.find((u) => u.id === tokens.userId && u.is_active);
    return {
      data: { accessToken: tokens.accessToken, expiresIn: tokens.expiresIn, user: owner ? store.userDto(owner, culture) : null, permissions: owner ? store.permissionsOfUser(owner.id) : [] },
      headers: { 'Set-Cookie': refreshCookie(tokens.refreshToken, req.headers['x-forwarded-proto'] === 'https') },
    };
  }],
  ['POST', /^\/api\/v1\/auth\/logout$/, async ({ req }) => {
    revoke(req);
    return { key: 'auth_logout.msg_success', headers: { 'Set-Cookie': refreshCookie('', false) } };
  }],
  ['GET', /^\/api\/v1\/auth\/me$/, async ({ req, culture }) => {
    const ctx = currentUser(req);
    return { data: { user: store.userDto(ctx.user, culture), permissions: ctx.permissions } };
  }],

  // Tài khoản: quên/đặt lại mật khẩu, lời mời, hồ sơ; khảo sát CSS công khai
  ...accountRoutes({ store, currentUser }),

  // Danh mục
  ['GET', /^\/api\/v1\/master-data$/, async ({ req, url, culture }) => {
    currentUser(req);
    return { data: store.masterList(listParam(url, 'type'), culture) };
  }],
  ['GET', /^\/api\/v1\/system-rules$/, async ({ req, culture }) => {
    currentUser(req);
    return { data: store.rules(culture) };
  }],

  // Cấu hình theo tenant: danh mục, quy tắc, cài đặt, ngày lễ, biểu mẫu PCV/CSS (D12)
  ...configRoutes({ store, currentUser, requirePermission }),

  // Tổ chức: khách hàng, phòng ban & team, nhân sự (D4)
  ...orgRoutes({ store, currentUser, requirePermission }),

  // Quản trị hệ thống: vai trò & quyền, AI Insight, prompt template, tích hợp
  ...systemRoutes({ store, currentUser, requirePermission, screens }),

  // Nhật ký công việc, nghỉ phép & OT, chấm công vào/ra (D4)
  ...workRoutes({ store, currentUser, requirePermission }),

  // Lưới công số
  ...effortRoutes({ store, currentUser, requirePermission }),

  // Dự án
  ['GET', /^\/api\/v1\/projects$/, async ({ req, url, culture }) => {
    const ctx = currentUser(req);
    requirePermission(ctx, 'project.view');
    const rows = store.searchProjects(projectFilters(url), ctx.user, ctx.permissions);
    const page = paginate(rows, url, 20);
    return { data: { ...page, items: page.items.map((p) => store.projectDto(p, culture)) } };
  }],
  ['GET', /^\/api\/v1\/projects\/summary$/, async ({ req, url }) => {
    const ctx = currentUser(req);
    requirePermission(ctx, 'project.view');
    return { data: store.projectSummary(store.searchProjects(projectFilters(url), ctx.user, ctx.permissions)) };
  }],
  // Chi tiết dự án: thông tin, phân bổ, milestone, EE, Defect/Leakage, PCV, CSS, quản trị dự án, báo cáo tháng
  ...projectRoutes({ store, currentUser, requirePermission, localize: (key, culture, args) => localizer.text(key, culture, args) }),
  ...projectExtraRoutes({ store, currentUser, requirePermission }),

  // AI (connector AI ở chế độ mock: đọc mock-data/ai)
  ['GET', /^\/api\/v1\/ai\/insights$/, async ({ req, url, culture }) => {
    currentUser(req);
    const screen = url.searchParams.get('screen') || '';
    if (!/^[a-z0-9_]+$/.test(screen) || !screens[screen]) throw badRequest({ screen: ['validation.invalid_value'] });
    const refresh = url.searchParams.get('refresh') === 'true';
    await new Promise((r) => setTimeout(r, refresh ? 900 : 400)); // mô phỏng thời gian LLM xử lý
    return { key: 'ai_insight_run.msg_success', data: { screenKey: screen, generatedAt: new Date().toISOString(), cached: !refresh, items: store.insights(screen, culture, screens) } };
  }],
  ['GET', /^\/api\/v1\/ai\/chat\/greeting$/, async ({ req, culture }) => {
    currentUser(req);
    return { data: { message: store.chatGreeting(culture) } };
  }],
  ['POST', /^\/api\/v1\/ai\/chat$/, async ({ req, culture }) => {
    currentUser(req);
    const body = await readJson(req);
    const message = String(body.message || '').trim();
    if (!message) throw badRequest({ message: ['validation.required'] });
    if (message.length > 2000) throw badRequest({ message: [{ key: 'validation.max_length', args: { max: 2000 } }] });
    await new Promise((r) => setTimeout(r, 700));
    return { key: 'ai_chat_send.msg_success', data: { reply: store.chatReply(message, culture), module: body.module || 'general' } };
  }],
];

// ─── Server ───────────────────────────────────────────────────────────────

function localizeErrors(errors, culture) {
  if (!errors || typeof errors !== 'object') return errors;
  return Object.fromEntries(Object.entries(errors).map(([field, list]) => [
    field,
    (Array.isArray(list) ? list : [list]).map((e) => (typeof e === 'string' ? localizer.text(e, culture) : localizer.text(e.key, culture, e.args))),
  ]));
}

const server = createServer(async (req, res) => {
  const culture = cultureOf(req);
  const url = new URL(req.url || '/', `http://${req.headers.host || 'localhost'}`);
  const started = Date.now();
  try {
    if (LATENCY > 0 && url.pathname.startsWith('/api/')) await new Promise((r) => setTimeout(r, LATENCY));
    if (FAILURE_RATE > 0 && url.pathname.startsWith('/api/') && Math.random() < FAILURE_RATE) throw new Error('Lỗi giả lập (MOCK_FAILURE_RATE)');

    const matches = routes.filter(([, re]) => re.test(url.pathname));
    if (matches.length === 0) throw notFound();
    const route = matches.find(([method]) => method === req.method);
    if (!route) throw new ApiError(405, 'error.method_not_allowed');

    const result = await route[2]({ req, res, url, culture });
    send(res, localizer, culture, result.status ?? 200, result.key ?? 'common.msg_success', result.data ?? null, result.args ?? {}, result.headers ?? {});
  } catch (err) {
    if (err instanceof ApiError) {
      const data = err.status === 400 ? localizeErrors(err.data, culture) : err.data;
      send(res, localizer, culture, err.status, err.messageKey, data, err.args);
    } else {
      const traceId = Math.random().toString(16).slice(2, 10);
      console.error(`[${traceId}]`, err);
      send(res, localizer, culture, 500, 'error.internal', { traceId });
    }
  } finally {
    console.log(`${req.method} ${url.pathname}${url.search} → ${res.statusCode} (${Date.now() - started}ms)`);
  }
});

server.listen(PORT, HOST, () => {
  console.log(`DX mock API: http://${HOST}:${PORT}  (hôm nay = ${today()}, độ trễ ${LATENCY}ms)`);
  console.log('Tài khoản demo: admin@example.com · pm@example.com · finance@example.com · member@example.com');
  console.log(acceptsBackendJwt ? 'Nhận cả JWT do BE .NET cấp (frontend chạy hybrid).' : 'Không có Jwt:SigningKey của BE — chỉ nhận token của mock.');
});
