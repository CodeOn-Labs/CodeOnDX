import { MutationCache, QueryCache, QueryClient } from '@tanstack/vue-query';
import { ApiError } from './http';
import { useToastStore } from '@/stores/toast';

/** Lỗi 401 do router chuyển về trang đăng nhập; 400 hiển thị dưới từng field — không bật toast cho 2 loại này. */
function notify(error: unknown) {
  if (error instanceof ApiError && (error.status === 401 || error.status === 400)) return;
  if (error instanceof DOMException && error.name === 'AbortError') return;
  const message = error instanceof Error ? error.message : String(error);
  useToastStore().error(message);
}

/** Query/mutation khai báo `meta: { silent: true }` tự hiển thị lỗi tại chỗ (vd. khung Insight) — không bật toast. */
function isSilent(source: { meta?: Record<string, unknown> }) {
  return source.meta?.silent === true;
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: (error, query) => !isSilent(query) && notify(error) }),
  mutationCache: new MutationCache({ onError: (error, _vars, _ctx, mutation) => !isSilent(mutation) && notify(error) }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => {
        if (error instanceof ApiError && error.status > 0 && error.status < 500) return false;
        return failureCount < 2;
      },
    },
  },
});
