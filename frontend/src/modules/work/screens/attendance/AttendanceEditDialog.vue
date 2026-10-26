<script setup lang="ts">
// Sửa / bổ sung giờ vào–ra của một ngày. Quản lý (timesheet.all_edit) bắt buộc ghi chú; nhân viên chỉ bổ sung ô còn trống.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Check, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import { workApi, type AttendanceCell, type AttendanceRow } from '@/modules/work/api/work.api';
import { useFormMutation } from '@/shared/composables/useFormMutation';
import { formatDate } from '@/shared/utils/date';

const props = defineProps<{ target: { row: AttendanceRow; date: string; cell: AttendanceCell | null } | null; canEditAll: boolean }>();
const emit = defineEmits<{ close: [] }>();
const { t } = useI18n();
const open = computed({ get: () => props.target !== null, set: (v) => !v && emit('close') });
const form = ref({ checkIn: '', checkOut: '', note: '' });
const errors = ref<Record<string, string>>({});
watch(
  () => props.target,
  (tg) => {
    if (!tg) return;
    form.value = { checkIn: tg.cell?.checkIn ?? '', checkOut: tg.cell?.checkOut ?? '', note: tg.cell?.note ?? '' };
    errors.value = {};
  },
);
const save = useFormMutation({
  mutationFn: () =>
    workApi.saveAttendance({ employeeId: props.target!.row.employee.id, date: props.target!.date, checkIn: form.value.checkIn || null, checkOut: form.value.checkOut || null, note: form.value.note }),
  invalidate: [['attendance']],
  onSuccess: () => emit('close'),
});
watch(save.fieldErrors, (v) => (errors.value = { ...v }));
function submit() {
  const e: Record<string, string> = {};
  if (props.canEditAll && !form.value.note.trim()) e.note = t('attendance.err_note_required');
  if (form.value.checkIn && form.value.checkOut && form.value.checkOut <= form.value.checkIn) e.checkOut = t('attendance.err_out_before_in');
  errors.value = e;
  if (Object.keys(e).length === 0) save.mutate(undefined);
}
</script>

<template>
  <BaseDialog v-model:open="open" :title="$t('attendance.title_edit_dialog', { name: props.target?.row.employee.fullName ?? '', date: formatDate(props.target?.date) })" max-width="md">
    <form class="flex flex-col gap-4" @submit.prevent="submit">
      <div class="grid grid-cols-2 gap-4">
        <FormItem :label="$t('attendance.lbl_check_in')" :error="errors.checkIn">
          <BaseInput v-model="form.checkIn" type="time" :error="!!errors.checkIn" />
        </FormItem>
        <FormItem :label="$t('attendance.lbl_check_out')" :error="errors.checkOut">
          <BaseInput v-model="form.checkOut" type="time" :error="!!errors.checkOut" />
        </FormItem>
      </div>
      <FormItem :label="$t('attendance.lbl_note')" :required="props.canEditAll" :error="errors.note" :description="props.canEditAll ? $t('attendance.hint_manager_note') : $t('attendance.hint_self_fill')">
        <BaseInput v-model="form.note" :error="!!errors.note" />
      </FormItem>
      <p v-if="props.target?.cell?.updatedBy" class="text-[11px] text-slate-400">{{ $t('attendance.lbl_updated_by', { name: props.target.cell.updatedBy }) }}</p>
      <button type="submit" class="hidden" />
    </form>
    <template #footer>
      <Button variant="danger" :disabled="save.isPending.value" @click="emit('close')"><template #leftIcon><X :size="16" /></template>{{ $t('common.btn_cancel') }}</Button>
      <Button variant="primary" :loading="save.isPending.value" @click="submit"><template #leftIcon><Check :size="16" /></template>{{ $t('common.btn_save') }}</Button>
    </template>
  </BaseDialog>
</template>
