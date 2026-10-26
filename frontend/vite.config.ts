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
 * Khớp theo method + path (không gồm query). Chỉ ghi đúng method BE đã có: vd PUT /settings chưa làm nên vẫn về mock.
 */
const BACKEND_ROUTES: ReadonlyArray<readonly [method: string, path: RegExp]> = [
  ['POST', /^\/api\/v1\/auth\/(login|refresh|logout)$/],
  ['GET', /^\/api\/v1\/auth\/me$/],
  ['GET', /^\/api\/v1\/master-data$/],
  ['GET', /^\/api\/v1\/system-rules$/],
  ['PUT', /^\/api\/v1\/system-rules\/[^/]+$/],
  ['GET', /^\/api\/v1\/settings$/],
  ['GET', /^\/api\/v1\/projects$/],
  ['GET', /^\/api\/v1\/projects\/summary$/],
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
