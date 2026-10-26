<script setup lang="ts">
// Port giao diện ToastProvider (Toast.tsx). Hiệu ứng spring của framer-motion thay bằng <TransitionGroup>.
import { onBeforeUnmount, watch } from 'vue';
import { AlertCircle, CheckCircle2, Info, TriangleAlert, X } from '@lucide/vue';
import { useToastStore, type ToastType } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const store = useToastStore();
const timers = new Map<string, ReturnType<typeof setTimeout>>();

watch(
  () => store.toasts.map((t) => t.id),
  (ids) => {
    for (const toast of store.toasts) {
      if (!timers.has(toast.id)) {
        timers.set(toast.id, setTimeout(() => close(toast.id), toast.duration));
      }
    }
    for (const id of [...timers.keys()]) {
      if (!ids.includes(id)) {
        clearTimeout(timers.get(id));
        timers.delete(id);
      }
    }
  },
  { immediate: true },
);

function close(id: string) {
  const timer = timers.get(id);
  if (timer) clearTimeout(timer);
  timers.delete(id);
  store.remove(id);
}

onBeforeUnmount(() => timers.forEach((t) => clearTimeout(t)));

const CONFIG: Record<ToastType, { icon: typeof Info; iconClass: string; bg: string; border: string; glow: string; bar: string; iconBg: string }> = {
  success: { icon: CheckCircle2, iconClass: 'text-emerald-600', bg: 'bg-emerald-50/90', border: 'border-emerald-200/50', glow: 'shadow-emerald-500/10', bar: 'bg-emerald-500', iconBg: 'bg-emerald-100' },
  error: { icon: AlertCircle, iconClass: 'text-rose-600', bg: 'bg-rose-50/90', border: 'border-rose-200/50', glow: 'shadow-rose-500/10', bar: 'bg-rose-500', iconBg: 'bg-rose-100' },
  warning: { icon: TriangleAlert, iconClass: 'text-amber-600', bg: 'bg-amber-50/90', border: 'border-amber-200/50', glow: 'shadow-amber-500/10', bar: 'bg-amber-500', iconBg: 'bg-amber-100' },
  info: { icon: Info, iconClass: 'text-indigo-600', bg: 'bg-indigo-50/90', border: 'border-indigo-200/50', glow: 'shadow-indigo-500/10', bar: 'bg-indigo-500', iconBg: 'bg-indigo-100' },
};
</script>

<template>
  <div class="fixed top-6 right-6 z-[9999] flex flex-col gap-4 w-full max-w-[380px] pointer-events-none">
    <TransitionGroup
      enter-active-class="transition-all duration-300 ease-out"
      enter-from-class="opacity-0 translate-x-[50px] scale-90"
      leave-active-class="transition-all duration-200 ease-in"
      leave-to-class="opacity-0 translate-x-5 scale-95"
      move-class="transition-transform duration-300"
    >
      <div
        v-for="toast in store.toasts"
        :key="toast.id"
        :class="cn(
          'pointer-events-auto relative overflow-hidden',
          'flex items-center gap-4 p-4 rounded-2xl border backdrop-blur-xl shadow-2xl',
          CONFIG[toast.type].bg, CONFIG[toast.type].border, CONFIG[toast.type].glow,
        )"
      >
        <div :class="cn('shrink-0 flex items-center justify-center w-10 h-10 rounded-xl shadow-sm', CONFIG[toast.type].iconBg)">
          <component :is="CONFIG[toast.type].icon" :class="cn('w-5 h-5', CONFIG[toast.type].iconClass)" />
        </div>
        <div class="flex-1 min-w-0 py-0.5">
          <h4 v-if="toast.title" class="text-[13px] font-black text-slate-900 uppercase tracking-wider mb-1 leading-tight">
            {{ toast.title }}
          </h4>
          <p class="text-[13px] font-bold text-slate-600 leading-relaxed break-words">{{ toast.message }}</p>
        </div>
        <button
          type="button"
          class="shrink-0 h-8 w-8 flex items-center justify-center rounded-lg hover:bg-black/5 text-slate-400 hover:text-slate-600 transition-colors"
          :aria-label="$t('common.btn_close')"
          @click="close(toast.id)"
        >
          <X :size="16" :stroke-width="2.5" />
        </button>
        <div
          :class="cn('absolute bottom-0 left-0 h-[3px] toast-bar', CONFIG[toast.type].bar)"
          :style="{ animationDuration: `${toast.duration}ms` }"
        />
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-bar {
  width: 100%;
  animation-name: toast-countdown;
  animation-timing-function: linear;
  animation-fill-mode: forwards;
}

@keyframes toast-countdown {
  from {
    width: 100%;
  }
  to {
    width: 0%;
  }
}
</style>
