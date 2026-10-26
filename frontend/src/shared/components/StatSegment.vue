<script setup lang="ts">
// Port ProjectStatSegment (ProjectStatsCards.tsx): một ô chỉ số trong dải thống kê — dùng chung cho mọi màn danh sách.
import type { Component } from 'vue';
import AnimatedCounter from '@/shared/components/AnimatedCounter.vue';
import { cn } from '@/shared/utils/cn';

export type StatTone = 'slate' | 'green' | 'amber' | 'red' | 'violet' | 'blue';

const props = withDefaults(
  defineProps<{ label: string; value: number | string; tone: StatTone; icon: Component; delay?: number; class?: string; loading?: boolean }>(),
  { delay: 0, class: '', loading: false },
);

const TONES: Record<StatTone, { iconBg: string; text: string; barBg: string }> = {
  slate: { iconBg: 'bg-slate-50 text-slate-600 group-hover:bg-slate-100', text: 'text-slate-900', barBg: 'bg-slate-400' },
  green: { iconBg: 'bg-emerald-50 text-emerald-600 group-hover:bg-emerald-100', text: 'text-emerald-700', barBg: 'bg-emerald-500' },
  amber: { iconBg: 'bg-amber-50 text-amber-600 group-hover:bg-amber-100', text: 'text-amber-700', barBg: 'bg-amber-500' },
  red: { iconBg: 'bg-red-50 text-red-600 group-hover:bg-red-100', text: 'text-red-700', barBg: 'bg-red-500' },
  violet: { iconBg: 'bg-pr/10 text-pr group-hover:bg-pr/15', text: 'text-pr', barBg: 'bg-pr' },
  blue: { iconBg: 'bg-blue-50 text-blue-600 group-hover:bg-blue-100', text: 'text-blue-700', barBg: 'bg-blue-500' },
};
</script>

<template>
  <div
    :class="cn('group relative flex items-center justify-between p-3 lg:p-3.5 bg-transparent transition-all hover:bg-white/50 backdrop-blur-sm animate-in fade-in zoom-in-[0.98] fill-mode-both', props.class)"
    :style="{ animationDelay: `${props.delay}s` }"
  >
    <div class="flex items-center gap-2.5">
      <div :class="cn('flex h-8 w-8 shrink-0 items-center justify-center rounded-lg transition-transform duration-300 group-hover:scale-105', TONES[props.tone].iconBg)">
        <component :is="props.icon" :size="16" />
      </div>
      <div class="flex flex-col">
        <span class="text-[10px] font-bold uppercase tracking-wider text-slate-400 group-hover:text-slate-500 transition-colors">{{ props.label }}</span>
        <span :class="cn('text-base font-black tracking-tight leading-none mt-0.5', TONES[props.tone].text)">
          <span v-if="props.loading" class="inline-block h-4 w-6 rounded bg-slate-100 animate-pulse align-middle" />
          <AnimatedCounter v-else-if="typeof props.value === 'number'" :value="props.value" :delay="props.delay" />
          <template v-else>{{ props.value }}</template>
        </span>
      </div>
    </div>
    <!-- Vạch nhấn bên trái khi rê chuột -->
    <div :class="cn('absolute left-0 top-1/2 -translate-y-1/2 w-1 h-0 rounded-r transition-all duration-300 group-hover:h-5 opacity-0 group-hover:opacity-100', TONES[props.tone].barBg)" />
  </div>
</template>
