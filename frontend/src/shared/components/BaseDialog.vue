<script setup lang="ts">
// Port từ BaseDialog.tsx (Radix Dialog → Reka UI Dialog, cùng data-state nên giữ nguyên hiệu ứng trong style.css).
import { DialogClose, DialogContent, DialogDescription, DialogOverlay, DialogPortal, DialogRoot, DialogTitle } from 'reka-ui';
import { X } from '@lucide/vue';
import { cn } from '@/shared/utils/cn';

const MAX_WIDTH = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-lg',
  xl: 'max-w-xl',
  '2xl': 'max-w-2xl',
  '3xl': 'max-w-3xl',
  '4xl': 'max-w-4xl',
} as const;

const props = withDefaults(
  defineProps<{
    open: boolean;
    title: string;
    description?: string;
    showDescriptionDot?: boolean;
    maxWidth?: keyof typeof MAX_WIDTH;
    contentClass?: string;
    bodyClass?: string;
    titleClass?: string;
  }>(),
  { description: undefined, showDescriptionDot: true, maxWidth: 'xl', contentClass: '', bodyClass: undefined, titleClass: '' },
);

const emit = defineEmits<{ 'update:open': [value: boolean] }>();

/** Giữ dialog mở khi người dùng bấm vào một alert-dialog xác nhận chồng lên trên. */
function onInteractOutside(event: Event) {
  const target = event.target as Element | null;
  if (target?.closest('[role="alertdialog"]')) event.preventDefault();
}
</script>

<template>
  <DialogRoot :open="props.open" @update:open="emit('update:open', $event)">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-50 bg-slate-900/20 backdrop-blur-[2px] dialog-overlay" />
      <DialogContent
        :class="cn(
          'fixed left-[50%] top-[50%] z-50 translate-x-[-50%] translate-y-[-50%]',
          'w-[calc(100vw-32px)] flex flex-col max-h-[88vh]',
          MAX_WIDTH[props.maxWidth],
          'bg-white border border-slate-200 rounded-2xl outline-none',
          'shadow-[0_24px_64px_-12px_rgba(0,0,0,0.14)]',
          'overflow-hidden dialog-content',
          props.contentClass,
        )"
        @interact-outside="onInteractOutside"
      >
        <div class="relative flex items-start justify-between px-5 pt-5 pb-4 border-b border-slate-100 shrink-0 overflow-hidden">
          <div class="absolute -top-10 -right-10 w-32 h-32 bg-indigo-500/8 blur-3xl rounded-full pointer-events-none" />
          <div class="relative z-10 min-w-0">
            <DialogTitle :class="cn('!m-0 text-[17px] font-black text-slate-900 tracking-tight leading-tight', props.titleClass)">
              {{ props.title }}
            </DialogTitle>
            <DialogDescription v-if="props.description || $slots.description" as-child>
              <div class="!mt-1 !mb-0 text-slate-500 text-[12px] font-medium flex items-center gap-1.5 flex-wrap">
                <span v-if="props.showDescriptionDot" class="w-1 h-1 rounded-full bg-indigo-500 shrink-0" />
                <slot name="description">{{ props.description }}</slot>
              </div>
            </DialogDescription>
            <DialogDescription v-else class="sr-only">{{ props.title }}</DialogDescription>
          </div>
          <DialogClose
            :class="cn(
              'relative z-10 shrink-0 ml-3',
              'flex h-9 w-9 items-center justify-center rounded-xl bg-slate-50 text-slate-400 transition-all duration-300 hover:rotate-90 hover:bg-red-50 hover:text-red-500 border border-transparent hover:border-red-100/50 active:scale-90 cursor-pointer',
              'outline-none focus:ring-2 focus:ring-indigo-500/20',
            )"
            :aria-label="$t('common.btn_close')"
          >
            <X :size="18" />
          </DialogClose>
        </div>

        <div :class="cn('flex-1 overflow-y-auto', props.bodyClass ?? 'px-5 py-4')">
          <slot />
        </div>

        <div v-if="$slots.footer" class="px-5 py-4 border-t border-slate-100 flex items-center justify-end gap-3 bg-white shrink-0">
          <slot name="footer" />
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
