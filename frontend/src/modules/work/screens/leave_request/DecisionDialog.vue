<script setup lang="ts">
// Duyệt / từ chối đơn nghỉ hoặc OT — từ chối bắt buộc nhập lý do, duyệt cho ghi chú tuỳ chọn.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Check, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import { workApi } from '@/modules/work/api/work.api';
import { useToastStore } from '@/stores/toast';

const props = defineProps<{ decision: { kind: 'leave' | 'ot'; id: string; code: string; action: 'approve' | 'reject' } | null }>();
const emit = defineEmits<{ close: []; done: [] }>();
const { t } = useI18n();
const toast = useToastStore();
const open = computed({ get: () => props.decision !== null, set: (v) => !v && emit('close') });
const note = ref('');
const error = ref('');
const busy = ref(false);
watch(open, () => {
  note.value = '';
  error.value = '';
});

async function submit() {
  const d = props.decision;
  if (!d) return;
  if (d.action === 'reject' && !note.value.trim()) {
    error.value = t('leave_request.err_reason_required');
    return;
  }
  busy.value = true;
  try {
    const res = d.kind === 'leave' ? await workApi.decideLeave(d.id, d.action, note.value.trim()) : await workApi.decideOt(d.id, d.action, note.value.trim());
    toast.success(res.messageText);
    emit('done');
    emit('close');
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <BaseDialog
    v-model:open="open"
    :title="props.decision?.action === 'reject' ? $t('leave_request.title_reject_dialog', { code: props.decision?.code ?? '' }) : $t('leave_request.title_approve_dialog', { code: props.decision?.code ?? '' })"
    :description="props.decision?.action === 'approve' ? $t('leave_request.hint_approve') : undefined"
    max-width="md"
  >
    <BaseTextarea v-model="note" :rows="3" :placeholder="props.decision?.action === 'reject' ? $t('leave_request.ph_reject_reason') : $t('leave_request.ph_approve_note')" :error="!!error" />
    <p v-if="error" class="mt-1 text-[11px] font-bold text-red-500">{{ error }}</p>
    <template #footer>
      <Button variant="outline" :disabled="busy" @click="emit('close')">{{ $t('common.btn_cancel') }}</Button>
      <Button :variant="props.decision?.action === 'reject' ? 'danger' : 'success'" :loading="busy" @click="submit">
        <template #leftIcon><X v-if="props.decision?.action === 'reject'" :size="16" /><Check v-else :size="16" /></template>
        {{ props.decision?.action === 'reject' ? $t('leave_request.btn_reject') : $t('leave_request.btn_approve') }}
      </Button>
    </template>
  </BaseDialog>
</template>
