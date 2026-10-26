<script setup lang="ts">
// Port ConfirmProvider.tsx (Radix AlertDialog → Reka UI AlertDialog, giữ nguyên class).
// Nút xác nhận không bọc AlertDialogAction để chỉ resolve một lần (true); đóng bằng nút huỷ / Esc / X → false.
import {
  AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogOverlay, AlertDialogPortal, AlertDialogRoot,
  AlertDialogTitle,
} from 'reka-ui';
import { AlertTriangle, CheckCircle2, Info, Trash2, X } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import { useConfirmStore } from '@/stores/confirm';
import { cn } from '@/shared/utils/cn';

const confirm = useConfirmStore();

function onOpenChange(value: boolean) {
  if (!value) confirm.settle(false);
}
</script>

<template>
  <AlertDialogRoot :open="confirm.open" @update:open="onOpenChange">
    <AlertDialogPortal>
      <AlertDialogOverlay class="fixed inset-0 z-[9998] bg-[#1e1b4b]/20 backdrop-blur-[4px] dialog-overlay" />
      <AlertDialogContent
        class="fixed left-[50%] top-[50%] z-[9999] w-[calc(100vw-32px)] max-w-[400px] translate-x-[-50%] translate-y-[-50%] border border-[#e5e8f2] bg-white shadow-[0_24px_64px_-12px_rgba(30,27,75,0.15)] rounded-[24px] dialog-content overflow-hidden outline-none"
      >
        <div class="p-8">
          <div class="flex items-start justify-between mb-6">
            <div
              :class="cn(
                'w-14 h-14 rounded-[20px] flex items-center justify-center shadow-sm',
                confirm.options?.type === 'danger' && 'bg-[#fff1f2] text-[#f43f5e] border border-[#ffe4e6]',
                confirm.options?.type === 'warning' && 'bg-[#fffbeb] text-[#f59e0b] border border-[#fef3c7]',
                confirm.options?.type === 'info' && 'bg-[#eef2ff] text-[#6366f1] border border-[#e0e7ff]',
              )"
            >
              <Trash2 v-if="confirm.options?.type === 'danger'" :size="28" :stroke-width="2.5" />
              <AlertTriangle v-else-if="confirm.options?.type === 'warning'" :size="28" :stroke-width="2.5" />
              <Info v-else :size="28" :stroke-width="2.5" />
            </div>
            <AlertDialogCancel as-child>
              <button
                type="button"
                class="p-2 text-[#94a3b8] hover:text-[#1e1b4b] hover:bg-[#f1f5f9] rounded-full transition-all"
                :aria-label="$t('common.btn_close')"
              >
                <X :size="20" />
              </button>
            </AlertDialogCancel>
          </div>

          <AlertDialogTitle class="text-[20px] font-black text-[#1e1b4b] tracking-tight leading-tight mb-2">
            {{ confirm.options?.title }}
          </AlertDialogTitle>
          <AlertDialogDescription class="text-[#64748b] text-[14px] leading-relaxed font-medium whitespace-pre-line">
            {{ confirm.options?.description }}
          </AlertDialogDescription>
        </div>

        <div class="px-8 py-6 bg-[#fafbff] border-t border-[#eef2f7] flex items-center gap-3">
          <AlertDialogCancel as-child>
            <Button variant="danger" class="flex-1 h-12 rounded-xl font-black text-[14px] uppercase tracking-wider transition-all shadow-sm">
              <template #leftIcon><X :size="18" /></template>
              {{ confirm.options?.cancelText || $t('component.confirm_dialog.btn_cancel') }}
            </Button>
          </AlertDialogCancel>
          <Button
            variant="primary"
            class="flex-1 h-12 rounded-xl font-black text-[14px] uppercase tracking-wider transition-all shadow-sm shadow-[0_8px_20px_-6px_rgba(99,102,241,0.3)] hover:shadow-[0_12px_24px_-8px_rgba(99,102,241,0.4)]"
            @click="confirm.settle(true)"
          >
            <template #leftIcon><CheckCircle2 :size="18" /></template>
            {{ confirm.options?.confirmText || $t('component.confirm_dialog.btn_confirm') }}
          </Button>
        </div>
      </AlertDialogContent>
    </AlertDialogPortal>
  </AlertDialogRoot>
</template>
