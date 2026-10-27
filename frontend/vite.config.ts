import { fileURLToPath, URL } from 'node:url';
import { spawn } from 'node:child_process';
import { connect } from 'node:net';
import { defineConfig, loadEnv, type Connect, type Logger, type Plugin } from 'vite';
import vue from '@vitejs/plugin-vue';
import tailwindcss from '@tailwindcss/vite';

// ─── Nơi proxy /api (chỉ lúc `npm run dev` / `npm run preview`, không nhúng vào bundle) ───
// DX_API_MODE:
//   mock    — mọi /api → mock API (tools/mock-api, DX_MOCK_API_TARGET, mặc định 5080).
//   hybrid  — endpoint BE .NET đã làm (BACKEND_ROUTES) → DX_BACKEND_TARGET (mặc định 5090), còn lại → mock.
//             Mock tự chấp nhận JWT do BE cấp (cùng khoá ký trong appsettings.Secrets.json), nên đăng nhập ở BE
//             vẫn gọi được các màn còn chạy trên mock.
//   backend — mọi /api → BE .NET (endpoint chưa làm sẽ trả 404).
type ApiMode = 'mock' | 'hybrid' | 'backend';
const API_MODES: readonly ApiMode[] = ['mock', 'hybrid', 'backend'];

/**
 * Endpoint đã có ở BE thật (backend/src/Dx.Api/Controllers) — làm xong endpoint mới thì thêm vào đây.
 * Khớp theo method + path (không gồm query). Chỉ ghi đúng method BE đã có: vd POST /projects chưa làm nên vẫn về mock.
 */
const ID = '[0-9a-fA-F-]{36}';
const BACKEND_ROUTES: ReadonlyArray<readonly [method: string, path: RegExp]> = [
  ['POST', /^\/api\/v1\/auth\/(login|refresh|logout)$/],
  ['GET', /^\/api\/v1\/auth\/me$/],
  ['GET', /^\/api\/v1\/master-data$/],
  ['GET', /^\/api\/v1\/system-rules$/],
  ['PUT', /^\/api\/v1\/system-rules\/[^/]+$/],
  ['GET', /^\/api\/v1\/settings$/],
  ['PUT', /^\/api\/v1\/settings$/],
  // Cấu hình: danh mục, ngày lễ, biểu mẫu PCV / CSS
  ['GET', /^\/api\/v1\/categories$/],
  ['POST', /^\/api\/v1\/categories$/],
  ['PUT', new RegExp(`^/api/v1/categories/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/categories/${ID}$`)],
  ['GET', /^\/api\/v1\/holidays$/],
  ['POST', /^\/api\/v1\/holidays$/],
  ['DELETE', new RegExp(`^/api/v1/holidays/${ID}$`)],
  ['GET', /^\/api\/v1\/pcv-template-items$/],
  ['POST', /^\/api\/v1\/pcv-template-items$/],
  ['PUT', new RegExp(`^/api/v1/pcv-template-items/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/pcv-template-items/${ID}$`)],
  ['GET', /^\/api\/v1\/css-template$/],
  ['PUT', /^\/api\/v1\/css-template$/],
  ['GET', /^\/api\/v1\/css-template\/questions$/],
  ['POST', /^\/api\/v1\/css-template\/questions$/],
  ['PUT', new RegExp(`^/api/v1/css-template/questions/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/css-template/questions/${ID}$`)],
  // Tổ chức: khách hàng, phòng ban & team, nhân sự
  ['GET', /^\/api\/v1\/clients(\/summary)?$/],
  ['POST', /^\/api\/v1\/clients$/],
  ['PUT', new RegExp(`^/api/v1/clients/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/clients/${ID}$`)],
  ['POST', new RegExp(`^/api/v1/clients/${ID}/restore$`)],
  ['GET', /^\/api\/v1\/departments$/],
  ['POST', /^\/api\/v1\/(departments|teams)$/],
  ['PUT', new RegExp(`^/api/v1/(departments|teams)/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/(departments|teams)/${ID}$`)],
  ['GET', /^\/api\/v1\/employees(\/summary)?$/],
  ['GET', new RegExp(`^/api/v1/employees/${ID}$`)],
  ['POST', /^\/api\/v1\/employees$/],
  ['PUT', new RegExp(`^/api/v1/employees/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/employees/${ID}$`)],
  ['POST', new RegExp(`^/api/v1/employees/${ID}/(restore|invite)$`)],
  // Phân quyền: quyền, vai trò, người dùng
  ['GET', /^\/api\/v1\/(permissions|users)$/],
  ['GET', /^\/api\/v1\/roles(\/summary)?$/],
  ['PUT', /^\/api\/v1\/roles\/permissions$/],
  ['GET', new RegExp(`^/api/v1/roles/${ID}(/users)?$`)],
  ['POST', /^\/api\/v1\/roles$/],
  ['PUT', new RegExp(`^/api/v1/roles/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/roles/${ID}$`)],
  ['POST', new RegExp(`^/api/v1/roles/${ID}/users$`)],
  ['DELETE', new RegExp(`^/api/v1/roles/${ID}/users/${ID}$`)],
  // Tài khoản: quên / đặt lại mật khẩu, nhận lời mời, hồ sơ
  ['POST', /^\/api\/v1\/auth\/(forgot-password|reset-password)$/],
  ['GET', /^\/api\/v1\/auth\/reset-password\/[^/]+$/],
  ['GET', /^\/api\/v1\/profile$/],
  ['PUT', /^\/api\/v1\/profile$/],
  ['POST', /^\/api\/v1\/profile\/change-password$/],
  ['GET', /^\/api\/v1\/projects$/],
  ['GET', /^\/api\/v1\/projects\/summary$/],
  // Dự án lõi: chi tiết, tạo / sửa / xoá / khôi phục, import, nhân sự, mốc, EE, chất lượng
  ['POST', /^\/api\/v1\/projects(\/import)?$/],
  ['GET', new RegExp(`^/api/v1/projects/${ID}(/staffing|/assignable-employees|/milestones|/ee|/quality)?$`)],
  ['PUT', new RegExp(`^/api/v1/projects/${ID}(/quality)?$`)],
  ['DELETE', new RegExp(`^/api/v1/projects/${ID}$`)],
  ['POST', new RegExp(`^/api/v1/projects/${ID}/(restore|assignments|milestones)$`)],
  ['PUT', new RegExp(`^/api/v1/projects/${ID}/(assignments|milestones)/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/projects/${ID}/(assignments|milestones)/${ID}$`)],
  ['PATCH', new RegExp(`^/api/v1/projects/${ID}/dates$`)],
  // Nhật ký công việc, nghỉ phép & OT, chấm công, công số
  ['GET', /^\/api\/v1\/timesheets(\/employees|\/group)?$/],
  ['PUT', /^\/api\/v1\/timesheets$/],
  ['POST', /^\/api\/v1\/timesheets\/approval$/],
  ['GET', /^\/api\/v1\/(leave-requests(\/summary)?|ot-requests)$/],
  ['POST', /^\/api\/v1\/(leave-requests|ot-requests)$/],
  ['POST', new RegExp(`^/api/v1/(leave-requests|ot-requests)/${ID}/(approve|reject|cancel)$`)],
  ['GET', /^\/api\/v1\/attendance(\/today)?$/],
  ['POST', /^\/api\/v1\/attendance\/(check-in|check-out|import)$/],
  ['PUT', /^\/api\/v1\/attendance$/],
  ['GET', /^\/api\/v1\/effort$/],
  ['POST', /^\/api\/v1\/effort$/],
  ['PATCH', new RegExp(`^/api/v1/effort/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/effort/${ID}$`)],
  // Dự án mở rộng: PCV, CSS, quản trị dự án, báo cáo tháng, báo cáo tình trạng; khảo sát CSS công khai
  ['GET', new RegExp(`^/api/v1/projects/${ID}/(pcv|css|documents|document-groups|risks|changes|issues|status-report)$`)],
  ['PUT', new RegExp(`^/api/v1/projects/${ID}/(pcv|css/form|status-report)$`)],
  ['POST', new RegExp(`^/api/v1/projects/${ID}/(pcv/items|css/questions|css/link|document-groups|documents|risks|changes|issues|status-report/ai-fill)$`)],
  ['PUT', new RegExp(`^/api/v1/projects/${ID}/(pcv/items|css/questions|document-groups|documents|risks|changes|issues)/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/projects/${ID}/(pcv/items|css/questions|document-groups|documents|risks|changes|issues)/${ID}$`)],
  ['GET', new RegExp(`^/api/v1/projects/${ID}/monthly-reports/\\d{4}-\\d{2}$`)],
  ['PATCH', new RegExp(`^/api/v1/projects/${ID}/monthly-reports/\\d{4}-\\d{2}/(header|cells)$`)],
  ['POST', new RegExp(`^/api/v1/projects/${ID}/monthly-reports/\\d{4}-\\d{2}/(sync|columns)$`)],
  ['PUT', new RegExp(`^/api/v1/projects/${ID}/monthly-reports/\\d{4}-\\d{2}/columns/[^/]+$`)],
  ['DELETE', new RegExp(`^/api/v1/projects/${ID}/monthly-reports/\\d{4}-\\d{2}/columns/[^/]+$`)],
  ['GET', /^\/api\/v1\/public\/css-surveys\/[^/]+$/],
  ['POST', /^\/api\/v1\/public\/css-surveys\/[^/]+$/],
  // AI: insight, chat, cấu hình insight, prompt; tích hợp
  ['GET', /^\/api\/v1\/ai\/(insights|chat\/greeting|insight-configs|prompt-templates)$/],
  ['POST', /^\/api\/v1\/ai\/(chat|insight-configs|insight-configs\/preview|prompt-templates|prompt-templates\/preview)$/],
  ['GET', new RegExp(`^/api/v1/ai/(insight-configs|prompt-templates)/${ID}$`)],
  ['PUT', new RegExp(`^/api/v1/ai/(insight-configs|prompt-templates)/${ID}$`)],
  ['DELETE', new RegExp(`^/api/v1/ai/(insight-configs|prompt-templates)/${ID}$`)],
  ['GET', /^\/api\/v1\/integrations(\/[A-Za-z]+\/preview)?$/],
  ['PUT', /^\/api\/v1\/integrations\/[A-Za-z]+$/],
  ['POST', /^\/api\/v1\/integrations\/[A-Za-z]+\/test$/],
];

/** Tiền tố nội bộ: request đổi url sang tiền tố này sẽ đi proxy BE (tiền tố được bỏ trước khi gửi). */
const BACKEND_PREFIX = '/__dx_backend';

function isBackendRoute(method: string | undefined, url: string): boolean {
  const path = url.split('?')[0] ?? '';
  return BACKEND_ROUTES.some(([m, re]) => m === method && re.test(path));
}

/**
 * `npm run dev` tự bật mock API (tools/mock-api) nếu cổng mock chưa có ai nghe — khỏi phải mở thêm cửa sổ.
 * Chỉ khi đích mock là máy này; tắt bằng DX_MOCK_AUTOSTART=0. Mock con tắt theo Vite.
 */
async function startMockApi(target: string, logger: Logger, httpServer: { once(event: 'close', cb: () => void): unknown } | null) {
  const url = new URL(target);
  if (!['127.0.0.1', 'localhost'].includes(url.hostname)) return;
  const port = Number(url.port || 80);
  const listening = await new Promise<boolean>((resolve) => {
    const socket = connect({ host: '127.0.0.1', port }, () => {
      socket.end();
      resolve(true);
    });
    socket.on('error', () => resolve(false));
  });
  if (listening) return;
  const script = fileURLToPath(new URL('../tools/mock-api/server.mjs', import.meta.url));
  const child = spawn(process.execPath, [script], {
    env: { ...process.env, MOCK_API_PORT: String(port), MOCK_API_HOST: '127.0.0.1' },
    stdio: ['ignore', 'inherit', 'inherit'],
  });
  logger.info(`  ➜  Mock API chưa chạy — đã tự bật ${target} (DX_MOCK_AUTOSTART=0 để tắt)`);
  const stop = () => {
    if (!child.killed) child.kill();
  };
  httpServer?.once('close', stop);
  process.once('exit', stop);
}

/** Chế độ hybrid: chuyển request của endpoint BE đã làm sang BACKEND_PREFIX trước khi tới proxy của Vite. */
function apiRouterPlugin(mode: ApiMode, mockTarget: string, backendTarget: string, autoStartMock: boolean): Plugin {
  const route: Connect.NextHandleFunction = (req, _res, next) => {
    const url = req.url ?? '';
    if (mode === 'hybrid' && url.startsWith('/api/') && isBackendRoute(req.method, url)) {
      req.url = BACKEND_PREFIX + url;
    }
    next();
  };
  const summary =
    mode === 'mock' ? `mock ${mockTarget}` : mode === 'backend' ? `BE ${backendTarget}` : `hybrid — BE ${backendTarget} + mock ${mockTarget}`;
  return {
    name: 'dx-api-router',
    configureServer(server) {
      server.middlewares.use(route);
      server.httpServer?.once('listening', () => server.config.logger.info(`  ➜  API:     ${summary}`));
      if (mode !== 'backend' && autoStartMock) void startMockApi(mockTarget, server.config.logger, server.httpServer);
    },
    configurePreviewServer(server) {
      server.middlewares.use(route);
    },
  };
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  // `vite --mode hybrid` / `--mode backend` (npm run dev:hybrid / dev:backend) chọn luôn chế độ API; DX_API_MODE ghi đè.
  const apiMode = (env.DX_API_MODE || (API_MODES.includes(mode as ApiMode) ? mode : 'mock')) as ApiMode;
  if (!API_MODES.includes(apiMode)) throw new Error(`DX_API_MODE không hợp lệ: "${apiMode}" (mock | hybrid | backend)`);
  const mockTarget = env.DX_MOCK_API_TARGET || 'http://127.0.0.1:5080';
  const backendTarget = env.DX_BACKEND_TARGET || 'http://127.0.0.1:5090';
  const defaultTarget = apiMode === 'backend' ? backendTarget : mockTarget;
  const proxy = {
    [BACKEND_PREFIX]: { target: backendTarget, changeOrigin: false, rewrite: (path: string) => path.slice(BACKEND_PREFIX.length) },
    '/api': { target: defaultTarget, changeOrigin: false },
    '/health': { target: defaultTarget, changeOrigin: false },
  };

  return {
    plugins: [apiRouterPlugin(apiMode, mockTarget, backendTarget, env.DX_MOCK_AUTOSTART !== '0'), vue(), tailwindcss()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
        '@shared': fileURLToPath(new URL('../shared', import.meta.url)),
      },
    },
    server: {
      port: 5173,
      strictPort: true,
      // Cho phép đọc ../shared/screens.json (danh mục màn hình dùng chung FE + BE)
      fs: { allow: [fileURLToPath(new URL('..', import.meta.url))] },
      proxy,
    },
    preview: {
      port: 4173,
      proxy,
    },
    worker: { format: 'es' },
    build: {
      target: 'es2022',
      cssCodeSplit: true,
      chunkSizeWarningLimit: 1400,
      rollupOptions: {
        output: {
          manualChunks(id) {
            if (!id.includes('/node_modules/')) return undefined;
            if (/[\\/]node_modules[\\/](vue|@vue|vue-router|pinia|vue-i18n|@intlify)[\\/]/.test(id)) return 'vendor-vue';
            if (/[\\/]node_modules[\\/](reka-ui|@floating-ui|@lucide)[\\/]/.test(id)) return 'vendor-ui';
            if (id.includes('/node_modules/@tanstack/')) return 'vendor-query';
            if (id.includes('/node_modules/exceljs/')) return 'vendor-excel';
            return undefined;
          },
        },
      },
    },
  };
});
