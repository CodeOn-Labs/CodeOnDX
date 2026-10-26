<script setup lang="ts">
// Port EffortTableSelect.tsx: ô chọn gọn trong lưới (lưu ngay khi chọn).
const props = withDefaults(defineProps<{ value: string; options: Array<{ value: string; label: string }>; disabled?: boolean }>(), { disabled: false });
const emit = defineEmits<{ change: [value: string] }>();
function onChange(event: Event) {
  const v = (event.target as HTMLSelectElement).value;
  if (v && v !== props.value) emit('change', v);
}
</script>

<template>
  <select
    :value="props.value"
    :disabled="props.disabled"
    class="h-8 w-full rounded-lg border border-slate-200 bg-white px-2 text-[12px] text-slate-700 outline-none transition-all hover:border-pr focus:border-pr focus:ring-2 focus:ring-pr/15 disabled:cursor-not-allowed disabled:opacity-70 disabled:bg-slate-50"
    @change="onChange"
  >
    <option v-if="!props.options.some((o) => o.value === props.value)" :value="props.value" disabled>{{ props.value || '—' }}</option>
    <option v-for="o in props.options" :key="o.value" :value="o.value">{{ o.label }}</option>
  </select>
</template>
