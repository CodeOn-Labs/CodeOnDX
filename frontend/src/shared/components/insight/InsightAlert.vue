<script setup lang="ts">
// Port InsightAlert (shared/components/InsightPanel.tsx): thẻ cảnh báo/nhận định AI.
import type { InsightVariant } from '@/modules/ai/api/ai.api';

const props = withDefaults(defineProps<{ title: string; content?: string; variant?: InsightVariant; clickable?: boolean }>(), {
  content: '',
  variant: 'amber',
  clickable: false,
});
const emit = defineEmits<{ click: [] }>();

const VARIANTS: Record<InsightVariant, string> = {
  amber: 'bg-ambg text-amt border-current/10',
  purple: 'bg-prbg text-prt border-current/10',
  red: 'bg-rdbg text-rdt border-current/10',
  teal: 'bg-tlbg text-tlt border-current/10',
  blue: 'bg-blbg text-blt border-current/10',
  green: 'bg-gnbg text-gnt border-current/10',
};
</script>

<template>
  <div
    :role="props.clickable ? 'button' : undefined"
    :tabindex="props.clickable ? 0 : undefined"
    :class="[
      'transform-gpu will-change-transform [backface-visibility:hidden] [perspective:1000px] rounded-xl border shadow-[0_2px_8px_rgba(0,0,0,0.02)] p-3 transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md',
      props.clickable ? 'cursor-pointer' : '',
      VARIANTS[props.variant] ?? VARIANTS.amber,
    ]"
    @click="props.clickable && emit('click')"
    @keydown.enter.prevent="props.clickable && emit('click')"
    @keydown.space.prevent="props.clickable && emit('click')"
  >
    <div class="flex items-center gap-2 mb-1.5">
      <span class="w-1.5 h-1.5 rounded-full shrink-0 bg-current opacity-60 animate-pulse" />
      <span data-testid="insight-title" class="font-bold text-[12px] leading-none">{{ props.title }}</span>
    </div>
    <div class="leading-relaxed text-[11px] opacity-90 pl-3.5">
      <slot>{{ props.content }}</slot>
    </div>
  </div>
</template>
