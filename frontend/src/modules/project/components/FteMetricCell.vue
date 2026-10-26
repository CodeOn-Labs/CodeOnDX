<script setup lang="ts">
// Port FteMetricCell (ProjectTable.tsx): số FTE trong nhãn bo tròn.
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ value: number; tone: 'pr' | 'bl' }>();
const TONES = {
  pr: { bg: 'bg-pr/5', text: 'text-pr', ring: 'ring-pr/10' },
  bl: { bg: 'bg-bl/5', text: 'text-bl', ring: 'ring-bl/10' },
} as const;
</script>

<template>
  <div :class="cn('inline-flex h-7 items-center gap-1.5 rounded-lg px-2.5 ring-1 ring-inset transition-all duration-200 hover:shadow-sm', TONES[props.tone].bg, TONES[props.tone].ring)">
    <span :class="cn('text-[13px] font-bold tabular-nums', TONES[props.tone].text)">{{ (props.value || 0).toFixed(1) }}</span>
    <span class="text-[10px] font-bold uppercase tracking-wider opacity-60">{{ $t('component.fte_metric_cell.unit_fte') }}</span>
  </div>
</template>
