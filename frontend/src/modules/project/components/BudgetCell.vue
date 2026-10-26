<script setup lang="ts">
// Port BudgetCell (ProjectTable.tsx): % budget đã dùng + thanh tiến độ màu theo ngưỡng.
import { computed } from 'vue';
import { percentTone } from '@/modules/project/utils/project-view-model';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ percent: number }>();
const tone = computed(() => percentTone(props.percent));
</script>

<template>
  <div class="flex flex-col gap-1.5 group/budget">
    <div :class="cn('text-[12px] font-black tracking-tight flex items-baseline gap-0.5 transition-transform duration-300 group-hover/budget:scale-105 origin-left', tone.text)">
      {{ props.percent.toFixed(0) }}
      <span class="text-[10px] opacity-60">%</span>
    </div>
    <div class="h-1.5 w-full rounded-full bg-slate-100 overflow-hidden shadow-inner p-[1px]">
      <div :class="cn('h-full transition-all duration-1000 rounded-full bg-gradient-to-r', tone.gradient, tone.shadow)" :style="{ width: `${Math.min(100, props.percent)}%` }" />
    </div>
  </div>
</template>
