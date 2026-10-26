<script setup lang="ts">
// Port ProgressCell (ProjectTable.tsx): % tiến độ theo thời gian + thanh màu theo ngưỡng.
import { computed } from 'vue';
import { percentTone } from '@/modules/project/utils/project-view-model';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(defineProps<{ percent: number; label?: string }>(), { label: '' });
const tone = computed(() => percentTone(props.percent));
</script>

<template>
  <div class="flex flex-col gap-1.5 group/progress">
    <div class="flex items-center justify-between text-[10.5px] font-black uppercase tracking-tight">
      <span class="text-slate-900 transition-transform duration-300 group-hover/progress:scale-105 origin-left">{{ props.percent.toFixed(0) }}%</span>
      <span class="text-slate-400 text-[9px] font-bold">{{ props.label }}</span>
    </div>
    <div class="h-1.5 w-full rounded-full bg-slate-100 overflow-hidden shadow-inner p-[1px]">
      <div :class="cn('h-full transition-all duration-1000 rounded-full bg-gradient-to-r', tone.gradient, tone.shadow)" :style="{ width: `${props.percent}%` }" />
    </div>
  </div>
</template>
