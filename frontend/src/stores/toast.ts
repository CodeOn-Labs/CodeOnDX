import { defineStore } from 'pinia';
import { ref } from 'vue';

export type ToastType = 'success' | 'error' | 'warning' | 'info';

export interface Toast {
  id: string;
  type: ToastType;
  title?: string;
  message: string;
  duration: number;
}

/** Thay ToastProvider/useToast của bản React; giao diện ở ToastContainer.vue. */
export const useToastStore = defineStore('toast', () => {
  const toasts = ref<Toast[]>([]);

  function remove(id: string) {
    toasts.value = toasts.value.filter((t) => t.id !== id);
  }

  function push(message: string, type: ToastType = 'info', title?: string, duration = 5000) {
    // Bỏ trùng: cùng nội dung đang hiển thị thì không thêm (vd nhiều query lỗi cùng lúc)
    if (toasts.value.some((t) => t.message === message && t.type === type)) return;
    const id = Math.random().toString(36).slice(2, 9);
    toasts.value.push({ id, type, title, message, duration });
  }

  return {
    toasts,
    remove,
    toast: push,
    success: (message: string, title?: string, duration?: number) => push(message, 'success', title, duration),
    error: (message: string, title?: string, duration?: number) => push(message, 'error', title, duration),
    warning: (message: string, title?: string, duration?: number) => push(message, 'warning', title, duration),
    info: (message: string, title?: string, duration?: number) => push(message, 'info', title, duration),
  };
});
