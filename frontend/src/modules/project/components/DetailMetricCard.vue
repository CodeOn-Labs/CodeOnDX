<script setup lang="ts">
// Port DetailMetricCard.tsx: thẻ KPI nhỏ của Chi tiết dự án (số có hiệu ứng đếm, màu theo tone).
import AnimatedCounter from '@/shared/components/AnimatedCounter.vue';
import type { Tone } from '@/modules/project/api/project-detail.api';

const props = withDefaults(
  defineProps<{ label: string; value: string | number; tone?: Tone | 'default'; formatter?: (v: number) => string; delay?: number }>(),
  { tone: 'default', formatter: undefined, delay: 0 },
);
const TEXT: Record<string, string> = {
  default: 'text-[#1f2937]', none: 'text-[#1f2937]', red: 'text-[#ef4444]', green: 'text-[#22c55e]', yellow: 'text-amber-500', lightOrange: 'text-orange-400', orange: 'text-orange-500',
};
</script>

<template>
  <div class="min-h-[68px] border border-[#e8ecf5] rounded-[12px] px-3 py-2.5 flex flex-col justify-between bg-white shadow-sm hover:shadow-md transition-shadow animate-in fade-in slide-in-from-bottom-1 duration-300">
    <span class="text-[#667085] text-[10px] font-bold uppercase tracking-widest">{{ props.label }}</span>
    <strong :class="['text-[18px] font-black tracking-tight', TEXT[props.tone]]">
      <AnimatedCounter v-if="typeof props.value === 'number'" :value="props.value" :formatter="props.formatter" :delay="props.delay" />
      <template v-else>{{ props.value }}</template>
    </strong>
  </div>
</template>
