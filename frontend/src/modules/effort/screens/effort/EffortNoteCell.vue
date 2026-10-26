<script setup lang="ts">
// Port EffortNoteCell.tsx: ô ghi chú tự giãn chiều cao, lưu khi rời ô.
import { nextTick, onMounted, ref, watch } from 'vue';

const props = withDefaults(defineProps<{ value: string | null; disabled?: boolean; onCommit: (value: string) => Promise<boolean> }>(), { disabled: false });
const draft = ref(props.value ?? '');
const el = ref<HTMLTextAreaElement | null>(null);
watch(() => props.value, (v) => (draft.value = v ?? ''));
function fit() {
  if (!el.value) return;
  el.value.style.height = 'auto';
  el.value.style.height = `${el.value.scrollHeight}px`;
}
watch(draft, () => void nextTick(fit));
onMounted(fit);
async function commit() {
  if (draft.value === (props.value ?? '')) return;
  if (!(await props.onCommit(draft.value))) draft.value = props.value ?? '';
}
</script>

<template>
  <textarea
    ref="el"
    v-model="draft"
    :disabled="props.disabled"
    placeholder="—"
    maxlength="100"
    rows="1"
    class="w-full min-w-32 min-h-8 resize-none rounded-lg border border-slate-200 bg-white px-2 py-1.5 text-[12px] outline-none transition-all hover:border-pr focus:border-pr focus:ring-2 focus:ring-pr/15 disabled:cursor-not-allowed disabled:opacity-70 disabled:bg-slate-50"
    @blur="commit"
  />
</template>
