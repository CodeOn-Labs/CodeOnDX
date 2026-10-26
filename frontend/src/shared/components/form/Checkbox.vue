<script setup lang="ts">
// Port Checkbox.tsx (Radix Checkbox → Reka UI Checkbox), hỗ trợ trạng thái "một phần" (indeterminate).
import { CheckboxIndicator, CheckboxRoot } from 'reka-ui';
import { Check, Minus } from '@lucide/vue';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(defineProps<{ indeterminate?: boolean; disabled?: boolean; label?: string; class?: string }>(), {
  indeterminate: false,
  disabled: false,
  label: undefined,
  class: '',
});
const model = defineModel<boolean>({ default: false });
</script>

<template>
  <CheckboxRoot
    :model-value="props.indeterminate ? 'indeterminate' : model"
    :disabled="props.disabled"
    :aria-label="props.label"
    :class="cn(
      'peer h-4 w-4 shrink-0 rounded-[5px] border border-slate-300 bg-white shadow-sm transition-all outline-none',
      'focus-visible:ring-2 focus-visible:ring-pr/30 disabled:cursor-not-allowed disabled:opacity-50 cursor-pointer',
      'data-[state=checked]:bg-pr data-[state=checked]:border-pr data-[state=checked]:text-white',
      'data-[state=indeterminate]:bg-pr data-[state=indeterminate]:border-pr data-[state=indeterminate]:text-white',
      props.class,
    )"
    @update:model-value="model = $event === true"
    @click.stop
  >
    <CheckboxIndicator class="flex items-center justify-center text-current">
      <Minus v-if="props.indeterminate" :size="11" :stroke-width="3.5" />
      <Check v-else :size="11" :stroke-width="3.5" />
    </CheckboxIndicator>
  </CheckboxRoot>
</template>
