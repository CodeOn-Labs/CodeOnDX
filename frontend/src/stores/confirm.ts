import { defineStore } from 'pinia';
import { ref } from 'vue';

export type ConfirmType = 'danger' | 'warning' | 'info';

export interface ConfirmOptions {
  title: string;
  description: string;
  confirmText?: string;
  cancelText?: string;
  type?: ConfirmType;
}

/**
 * Thay ConfirmProvider/useConfirm của bản React: `await confirm.ask({...})` trả true khi người dùng xác nhận.
 * Giao diện ở shared/components/ConfirmDialog.vue (gắn một lần trong App.vue).
 */
export const useConfirmStore = defineStore('confirm', () => {
  const open = ref(false);
  const options = ref<ConfirmOptions | null>(null);
  let resolver: ((value: boolean) => void) | null = null;

  function ask(opts: ConfirmOptions): Promise<boolean> {
    resolver?.(false); // hộp thoại cũ chưa trả lời → coi như huỷ
    options.value = { type: 'danger', ...opts };
    open.value = true;
    return new Promise<boolean>((resolve) => {
      resolver = resolve;
    });
  }

  function settle(value: boolean) {
    open.value = false;
    resolver?.(value);
    resolver = null;
  }

  return { open, options, ask, settle };
});
