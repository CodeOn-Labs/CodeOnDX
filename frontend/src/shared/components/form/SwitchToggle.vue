<script setup lang="ts">
// Công tắc bật/tắt dùng ở Quy tắc, Danh mục, Cài đặt (port nút toggle trong RuleTable.tsx / RuleModal.tsx).
import { Check, X } from '@lucide/vue';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(defineProps<{ size?: 'sm' | 'md'; disabled?: boolean; label?: string }>(), {
  size: 'md',
  disabled: false,
  label: undefined,
});
const model = defineModel<boolean>({ default: false });
</script>

<template>
  <button
    type="button"
    role="switch"
    :aria-checked="model"
    :aria-label="props.label"
    :title="props.label"
    :disabled="props.disabled"
    :class="cn(
      'relative inline-flex shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-300 ease-in-out focus:outline-none focus:ring-2 focus:ring-indigo-500/30 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50',
      props.size === 'sm' ? 'h-5 w-9' : 'h-6 w-11',
      model ? 'bg-indigo-600' : 'bg-slate-200 hover:bg-slate-300',
    )"
    @click.stop="model = !model"
  >
    <span
      :class="cn(
        'pointer-events-none flex transform items-center justify-center rounded-full bg-white ring-0 transition-transform duration-300 ease-in-out',
        props.size === 'sm' ? 'h-4 w-4 shadow-sm' : 'h-5 w-5 shadow-md',
        model ? (props.size === 'sm' ? 'translate-x-4' : 'translate-x-5') : 'translate-x-0',
      )"
    >
      <Check v-if="model" :size="props.size === 'sm' ? 10 : 12" class="text-indigo-600" :stroke-width="3" />
      <X v-else :size="props.size === 'sm' ? 10 : 12" class="text-slate-400" :stroke-width="3" />
    </span>
  </button>
</template>
