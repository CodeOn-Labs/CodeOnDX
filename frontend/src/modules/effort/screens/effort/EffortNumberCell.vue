<script setup lang="ts">
// Port EffortNumberCell.tsx: ô số, kẹp trong [min, max], làm tròn theo decimals, lưu khi rời ô / Enter; lỗi → trả về giá trị cũ.
import { ref, watch } from 'vue';
import { formatEffortNumber, roundToDecimals } from '@/modules/effort/utils/effort-calculations';

const props = withDefaults(
  defineProps<{ value: number; min: number; max: number; step?: number; decimals: number; suffix?: string; disabled?: boolean; onCommit: (value: number) => Promise<boolean> }>(),
  { step: 0.01, suffix: '', disabled: false },
);
const draft = ref(formatEffortNumber(props.value, props.decimals));
watch(() => props.value, (v) => (draft.value = formatEffortNumber(v, props.decimals)));

async function commit() {
  const parsed = Number(draft.value);
  const clamped = Number.isFinite(parsed) ? Math.min(props.max, Math.max(props.min, roundToDecimals(parsed, props.decimals))) : props.value;
  draft.value = formatEffortNumber(clamped, props.decimals);
  if (clamped === props.value) return;
  if (!(await props.onCommit(clamped))) draft.value = formatEffortNumber(props.value, props.decimals);
}
</script>

<template>
  <span class="inline-flex items-center gap-0.5">
    <input
      v-model="draft"
      type="number"
      :min="props.min"
      :max="props.max"
      :step="props.step"
      :disabled="props.disabled"
      class="h-8 w-14 rounded-lg border border-slate-200 bg-white px-2 text-right text-[12px] outline-none transition-all hover:border-pr focus:border-pr focus:ring-2 focus:ring-pr/15 disabled:cursor-not-allowed disabled:opacity-70 disabled:bg-slate-50 disabled:hover:border-slate-200 [appearance:textfield] [&::-webkit-inner-spin-button]:m-0 [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:m-0 [&::-webkit-outer-spin-button]:appearance-none"
      @blur="commit"
      @keydown.enter="($event.target as HTMLInputElement).blur()"
    />
    {{ props.suffix }}
  </span>
</template>
