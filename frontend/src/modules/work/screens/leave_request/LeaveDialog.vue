<script setup lang="ts">
// Tạo đơn nghỉ: loại nghỉ (danh mục leave_type), từ–đến ngày, cả ngày / nửa ngày sáng / chiều, lý do, đính kèm (nếu loại nghỉ yêu cầu).
// Số ngày tính ở FE chỉ để hiển thị (bỏ cuối tuần); BE tính lại có trừ ngày lễ và kiểm tra quỹ phép, báo trước, trùng đơn.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Check, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import { workApi, type LeaveInput, type LeaveSummary } from '@/modules/work/api/work.api';
import { useMasterData } from '@/shared/composables/useMasterData';
import { useFormMutation } from '@/shared/composables/useFormMutation';
import { daysBetween } from '@/modules/work/utils/timesheet-rules';

const props = defineProps<{ summary: LeaveSummary | null }>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ saved: [] }>();
const { t } = useI18n();
const { items: leaveTypes } = useMasterData('leave_type');
const typeOptions = computed(() => leaveTypes.value.map((x) => ({ value: x.id, label: x.name })));
const durationOptions = computed(() => [
  { value: 'full_day', label: t('leave_request.opt_full_day') },
  { value: 'half_day_am', label: t('leave_request.opt_half_day_am') },
  { value: 'half_day_pm', label: t('leave_request.opt_half_day_pm') },
]);

const empty = (): LeaveInput => ({ leaveTypeId: '', fromDate: null, toDate: null, durationType: 'full_day', reason: '', attachmentUrl: '' });
const form = ref<LeaveInput>(empty());
const errors = ref<Record<string, string>>({});
watch(open, (v) => {
  if (v) {
    form.value = empty();
    errors.value = {};
  }
});
watch(() => form.value.durationType, (d) => {
  if (d !== 'full_day' && form.value.fromDate) form.value.toDate = form.value.fromDate;
});
const selectedType = computed(() => leaveTypes.value.find((x) => x.id === form.value.leaveTypeId));
const needAttachment = computed(() => selectedType.value?.metadata.need_attachment === true);
const deductBalance = computed(() => selectedType.value?.metadata.deduct_balance === true);
const estimatedDays = computed(() => {
  const { fromDate, toDate, durationType } = form.value;
  if (!fromDate || !toDate || toDate < fromDate) return 0;
  const working = daysBetween(fromDate, toDate).filter((d) => ![0, 6].includes(new Date(`${d}T00:00:00`).getDay())).length;
  return durationType === 'full_day' ? working : working * 0.5;
});

const save = useFormMutation({
  mutationFn: (input: LeaveInput) => workApi.createLeave(input),
  onSuccess: () => {
    open.value = false;
    emit('saved');
  },
});
watch(save.fieldErrors, (v) => (errors.value = { ...v }));
function submit() {
  const e: Record<string, string> = {};
  if (!form.value.leaveTypeId) e.leaveTypeId = t('validation.required');
  if (!form.value.fromDate) e.fromDate = t('validation.required');
  if (!form.value.toDate) e.toDate = t('validation.required');
  if (!form.value.reason.trim()) e.reason = t('validation.required');
  if (needAttachment.value && !form.value.attachmentUrl.trim()) e.attachmentUrl = t('leave_request.err_attachment_required');
  errors.value = e;
  if (Object.keys(e).length === 0) save.mutate({ ...form.value, toDate: form.value.durationType === 'full_day' ? form.value.toDate : form.value.fromDate });
}
</script>

<template>
  <BaseDialog v-model:open="open" :title="$t('leave_request.title_leave_dialog')" :description="props.summary ? $t('leave_request.hint_leave_balance', { days: props.summary.balance.remainingDays }) : undefined" max-width="lg">
    <form class="flex flex-col gap-5" @submit.prevent="submit">
      <div class="grid grid-cols-2 gap-4">
        <FormSelect v-model="form.leaveTypeId" :label="$t('leave_request.lbl_leave_type')" :options="typeOptions" :error="errors.leaveTypeId" required />
        <FormSelect v-model="form.durationType" :label="$t('leave_request.lbl_duration')" :options="durationOptions" :allow-clear="false" :error="errors.durationType" />
        <FormDatePicker v-model="form.fromDate" :label="$t('leave_request.lbl_from_date')" :error="errors.fromDate" required />
        <FormDatePicker v-model="form.toDate" :label="$t('leave_request.lbl_to_date')" :error="errors.toDate" required :class="form.durationType !== 'full_day' ? 'pointer-events-none opacity-60' : ''" />
      </div>
      <div class="flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50 px-4 py-2.5 text-[12px]">
        <span class="font-bold text-slate-500">{{ $t('leave_request.lbl_estimated_days') }}</span>
        <span class="font-black text-slate-800">{{ estimatedDays }}<template v-if="deductBalance"> / {{ props.summary?.balance.remainingDays ?? '—' }}</template></span>
      </div>
      <FormItem :label="$t('leave_request.lbl_reason')" required :error="errors.reason">
        <BaseTextarea v-model="form.reason" :rows="3" :error="!!errors.reason" />
      </FormItem>
      <FormItem v-if="needAttachment" :label="$t('leave_request.lbl_attachment')" required :error="errors.attachmentUrl" :description="$t('leave_request.hint_attachment')">
        <BaseInput v-model="form.attachmentUrl" placeholder="https://" :error="!!errors.attachmentUrl" />
      </FormItem>
      <p class="text-[11px] text-slate-400">{{ $t('leave_request.hint_min_notice', { days: props.summary?.minNoticeDays ?? 1 }) }}</p>
      <button type="submit" class="hidden" />
    </form>
    <template #footer>
      <Button variant="danger" :disabled="save.isPending.value" @click="open = false"><template #leftIcon><X :size="16" /></template>{{ $t('common.btn_cancel') }}</Button>
      <Button variant="primary" :loading="save.isPending.value" @click="submit"><template #leftIcon><Check :size="16" /></template>{{ $t('leave_request.btn_submit') }}</Button>
    </template>
  </BaseDialog>
</template>
