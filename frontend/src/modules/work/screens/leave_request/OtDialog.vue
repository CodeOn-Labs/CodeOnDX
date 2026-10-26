<script setup lang="ts">
// Tạo đơn OT: ngày, giờ bắt đầu–kết thúc, dự án (đang được phân công), lý do.
// Hệ số (150% ngày thường / 200% cuối tuần / 300% ngày lễ) và giới hạn OT_MAX_HOURS_* do BE áp; FE hiện số giờ ước tính.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { Check, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import { workApi, type LeaveSummary, type OtInput } from '@/modules/work/api/work.api';
import { useFormMutation } from '@/shared/composables/useFormMutation';

const props = defineProps<{ summary: LeaveSummary | null }>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ saved: [] }>();
const { t } = useI18n();

const empty = (): OtInput => ({ projectId: '', workDate: null, startTime: '18:00', endTime: '20:00', reason: '' });
const form = ref<OtInput>(empty());
const errors = ref<Record<string, string>>({});
watch(open, (v) => {
  if (v) {
    form.value = empty();
    errors.value = {};
  }
});
const projects = useQuery({
  queryKey: computed(() => ['timesheets', 'period', null, form.value.workDate, form.value.workDate] as const),
  queryFn: () => workApi.timesheets(null, form.value.workDate!, form.value.workDate!),
  enabled: computed(() => open.value && !!form.value.workDate),
});
const projectOptions = computed(() => (projects.data.value?.projects ?? []).map((p) => ({ value: p.id, label: p.name })));
const hours = computed(() => {
  const m = /^(\d{2}):(\d{2})$/;
  const a = m.exec(form.value.startTime);
  const b = m.exec(form.value.endTime);
  if (!a || !b) return 0;
  const diff = Number(b[1]) * 60 + Number(b[2]) - (Number(a[1]) * 60 + Number(a[2]));
  return diff > 0 ? Math.round((diff / 60) * 100) / 100 : 0;
});

const save = useFormMutation({
  mutationFn: (input: OtInput) => workApi.createOt(input),
  onSuccess: () => {
    open.value = false;
    emit('saved');
  },
});
watch(save.fieldErrors, (v) => (errors.value = { ...v }));
function submit() {
  const e: Record<string, string> = {};
  if (!form.value.workDate) e.workDate = t('validation.required');
  if (!form.value.reason.trim()) e.reason = t('validation.required');
  if (hours.value <= 0) e.endTime = t('leave_request.err_end_before_start');
  errors.value = e;
  if (Object.keys(e).length === 0) save.mutate({ ...form.value });
}
</script>

<template>
  <BaseDialog
    v-model:open="open"
    :title="$t('leave_request.title_ot_dialog')"
    :description="props.summary ? $t('leave_request.hint_ot_limits', { day: props.summary.ot.maxPerDay, month: props.summary.ot.maxPerMonth, used: props.summary.ot.monthHours }) : undefined"
    max-width="lg"
  >
    <form class="flex flex-col gap-5" @submit.prevent="submit">
      <div class="grid grid-cols-3 gap-4">
        <FormDatePicker v-model="form.workDate" :label="$t('leave_request.lbl_ot_date')" :error="errors.workDate" required />
        <FormItem :label="$t('leave_request.lbl_start_time')" required :error="errors.startTime">
          <BaseInput v-model="form.startTime" type="time" :error="!!errors.startTime" />
        </FormItem>
        <FormItem :label="$t('leave_request.lbl_end_time')" required :error="errors.endTime">
          <BaseInput v-model="form.endTime" type="time" :error="!!errors.endTime" />
        </FormItem>
      </div>
      <FormSelect v-model="form.projectId" :label="$t('leave_request.lbl_project')" :options="projectOptions" :placeholder="form.workDate ? $t('leave_request.ph_project') : $t('leave_request.ph_pick_date_first')" :disabled="!form.workDate" :error="errors.projectId" />
      <div class="flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50 px-4 py-2.5 text-[12px]">
        <span class="font-bold text-slate-500">{{ $t('leave_request.lbl_ot_hours') }}</span>
        <span class="font-black text-slate-800">{{ hours }}h</span>
      </div>
      <FormItem :label="$t('leave_request.lbl_reason')" required :error="errors.reason">
        <BaseTextarea v-model="form.reason" :rows="3" :error="!!errors.reason" />
      </FormItem>
      <button type="submit" class="hidden" />
    </form>
    <template #footer>
      <Button variant="danger" :disabled="save.isPending.value" @click="open = false"><template #leftIcon><X :size="16" /></template>{{ $t('common.btn_cancel') }}</Button>
      <Button variant="primary" :loading="save.isPending.value" @click="submit"><template #leftIcon><Check :size="16" /></template>{{ $t('leave_request.btn_submit') }}</Button>
    </template>
  </BaseDialog>
</template>
