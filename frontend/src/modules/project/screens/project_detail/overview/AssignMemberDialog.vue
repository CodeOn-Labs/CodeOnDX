<script setup lang="ts">
// Port AddMemberModal.tsx + member-assignment.schema.ts: gán nhân sự vào 1 vị trí hợp đồng với nhiều giai đoạn FTE.
// Sửa phân bổ mà chọn người khác → chọn phương án: Bàn giao (giữ lịch sử, người mới nhận từ ngày sau ngày bàn giao)
// hoặc Thay thế hoàn toàn (xoá lịch sử phân bổ người cũ — sửa nhập sai). Nhân sự đã/sắp nghỉ trong giai đoạn → hỏi xác nhận.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { Check, Plus, Trash2, UserCheck, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import { projectDetailApi, type IntervalInput, type StaffAssignment, type StaffGroup, type StaffSlot } from '@/modules/project/api/project-detail.api';
import { firstFieldErrors } from '@/api/http';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { addDays } from 'date-fns';
import { formatDate, parseDate, toIsoDate } from '@/shared/utils/date';

const props = defineProps<{
  projectId: string;
  projectStart: string | null;
  projectEnd: string | null;
  group: StaffGroup | null;
  position: StaffSlot | null;
  assignment: StaffAssignment | null;
}>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ saved: [] }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();

const people = useQuery({
  queryKey: computed(() => ['projects', props.projectId, 'assignable'] as const),
  queryFn: ({ signal }) => projectDetailApi.assignableEmployees(props.projectId, { signal }),
  enabled: open,
  staleTime: 60_000,
});
const options = computed(() => (people.data.value ?? []).map((p) => ({ value: p.id, label: `${p.fullName} (${p.code})`, description: `${p.position ?? t('project_detail.lbl_employee_default_position')} • ${p.level ?? '—'} • ${p.allocationPercent}%` })));

const employeeId = ref('');
const intervals = ref<IntervalInput[]>([]);
const mode = ref<'handover' | 'replace'>('handover');
const handoverDate = ref<string | null>(null);
const errors = ref<Record<string, string>>({});
const saving = ref(false);
const isEdit = computed(() => !!props.assignment);
const replacing = computed(() => isEdit.value && !!employeeId.value && employeeId.value !== props.assignment?.employee?.id);

watch(open, (v) => {
  if (!v) return;
  errors.value = {};
  mode.value = 'handover';
  handoverDate.value = null;
  if (props.assignment) {
    employeeId.value = props.assignment.employee?.id ?? '';
    intervals.value = props.assignment.intervals.map((i) => ({ startDate: i.startDate, endDate: i.endDate, fte: i.fte }));
  } else {
    employeeId.value = '';
    intervals.value = [{ startDate: props.position?.contractStartDate ?? props.projectStart, endDate: props.position?.contractEndDate ?? props.projectEnd, fte: props.position?.contractFte || 1 }];
  }
});
/** Bàn giao: người mới bắt đầu từ ngày kế tiếp ngày bàn giao */
const handoverNext = computed(() => (handoverDate.value ? toIsoDate(addDays(parseDate(handoverDate.value) as Date, 1)) : null));

function addInterval() {
  const last = intervals.value.at(-1);
  const start = last?.endDate ? toIsoDate(addDays(parseDate(last.endDate) as Date, 1)) : props.projectStart;
  intervals.value.push({ startDate: start, endDate: props.projectEnd, fte: last?.fte ?? 1 });
}

/**
 * Tổng hợp kế hoạch: số tháng (theo ngày, 1 tháng ≈ 30,44 ngày) và FTE trung bình có trọng số theo số ngày của từng giai đoạn.
 * Ví dụ: 60 ngày × 1.0 + 30 ngày × 0.5 → FTE TB = (60 + 15) / 90 = 0.83.
 */
const stats = computed(() => {
  let days = 0;
  let weighted = 0;
  for (const it of intervals.value) {
    const s = parseDate(it.startDate);
    const e = parseDate(it.endDate);
    if (!s || !e || e < s) continue;
    const d = Math.round((e.getTime() - s.getTime()) / 86_400_000) + 1;
    days += d;
    weighted += d * Number(it.fte || 0);
  }
  return { months: (days / 30.44).toFixed(1), avgFte: days > 0 ? (weighted / days).toFixed(2) : '0.00' };
});

const errorOf = (i: number, f: string) => errors.value[`intervals.${i}.${f}`];

async function submit() {
  errors.value = {};
  if (!employeeId.value) {
    errors.value = { employeeId: t('validation.required') };
    return;
  }
  const person = people.data.value?.find((p) => p.id === employeeId.value);
  const lastEnd = intervals.value.map((i) => i.endDate).filter(Boolean).sort().at(-1);
  if (person?.leftDate && (!lastEnd || person.leftDate <= lastEnd)) {
    const ok = await confirm.ask({
      type: 'warning',
      title: t('project_detail.confirm_leaving_title'),
      description: t('project_detail.confirm_leaving_desc', { name: person.fullName, date: formatDate(person.leftDate) }),
      confirmText: t('project_detail.btn_continue_assign'),
    });
    if (!ok) return;
  }
  saving.value = true;
  try {
    const body = {
      employeeId: employeeId.value, intervals: intervals.value,
      contractStaffId: props.position?.contractStaffId ?? null, slotIndex: props.position?.slotIndex ?? 0, roleInProject: props.group?.roleCode,
      mode: replacing.value ? mode.value : ('update' as const), handoverDate: replacing.value && mode.value === 'handover' ? handoverDate.value : null,
    };
    const res = props.assignment ? await projectDetailApi.updateAssignment(props.projectId, props.assignment.id, body) : await projectDetailApi.addAssignment(props.projectId, body);
    toast.success(res.messageText);
    open.value = false;
    emit('saved');
  } catch (err) {
    errors.value = firstFieldErrors(err);
    if (Object.keys(errors.value).length === 0) toast.error((err as Error).message);
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <BaseDialog v-model:open="open" :title="isEdit ? $t('project_detail.title_assign_update') : $t('project_detail.title_assign_new')" max-width="2xl">
    <div class="flex flex-col gap-5">
      <div v-if="props.group" class="flex items-center gap-2 rounded-xl bg-slate-50 border border-slate-100 px-3 py-2 text-[12px]">
        <span class="font-bold uppercase tracking-wider text-slate-400">{{ $t('project_detail.lbl_position') }}</span>
        <span class="font-black text-slate-800">{{ props.group.roleName }}</span>
        <span v-if="props.group.level" class="rounded bg-white px-1.5 py-0.5 text-[11px] font-bold text-slate-500 ring-1 ring-slate-200">{{ props.group.level.name }}</span>
      </div>

      <section class="flex flex-col gap-2">
        <h4 class="flex items-center gap-2 text-[13px] font-black text-slate-800"><UserCheck :size="15" class="text-pr" />{{ $t('project_detail.lbl_personnel_info') }}</h4>
        <FormSelect
          v-model="employeeId"
          :options="options"
          :placeholder="people.isPending.value ? $t('project_detail.lbl_loading_people') : $t('project_detail.ph_search_employee')"
          :search-placeholder="$t('project_detail.ph_search_employee_input')"
          :error="errors.employeeId"
          searchable
        />
        <p v-if="isEdit" class="text-[11.5px] text-slate-500">{{ $t('project_detail.hint_edit_replace') }}</p>
      </section>

      <section v-if="replacing" class="flex flex-col gap-3 rounded-2xl border border-indigo-100 bg-indigo-50/40 p-4">
        <h4 class="text-[13px] font-black text-indigo-900">{{ $t('project_detail.lbl_replace_plan') }}</h4>
        <label class="flex cursor-pointer items-start gap-2.5 rounded-xl border bg-white p-3" :class="mode === 'handover' ? 'border-indigo-300 ring-1 ring-indigo-500/20' : 'border-slate-200'">
          <input v-model="mode" type="radio" value="handover" class="mt-1 accent-indigo-600" />
          <span><span class="block text-[12.5px] font-bold text-slate-800">{{ $t('project_detail.lbl_mode_handover') }}</span><span class="text-[11.5px] text-slate-500">{{ $t('project_detail.hint_mode_handover') }}</span></span>
        </label>
        <label class="flex cursor-pointer items-start gap-2.5 rounded-xl border bg-white p-3" :class="mode === 'replace' ? 'border-rose-300 ring-1 ring-rose-500/20' : 'border-slate-200'">
          <input v-model="mode" type="radio" value="replace" class="mt-1 accent-rose-600" />
          <span><span class="block text-[12.5px] font-bold text-slate-800">{{ $t('project_detail.lbl_mode_replace') }}</span><span class="text-[11.5px] text-slate-500">{{ $t('project_detail.hint_mode_replace') }}</span></span>
        </label>
        <div v-if="mode === 'handover'" class="flex flex-col gap-1.5">
          <span class="text-[11.5px] font-bold text-slate-600">{{ $t('project_detail.lbl_handover_date') }}</span>
          <FormDatePicker v-model="handoverDate" :error="errors.handoverDate" />
          <p v-if="handoverNext" class="text-[11.5px] text-slate-500">{{ $t('project_detail.hint_handover_next') }} <strong class="text-indigo-600">{{ formatDate(handoverNext) }}</strong></p>
        </div>
      </section>

      <section class="flex flex-col gap-2">
        <h4 class="text-[13px] font-black text-slate-800">{{ $t('project_detail.lbl_fte_plan') }}</h4>
        <div class="grid grid-cols-[1fr_1fr_90px_32px] gap-2 text-[10px] font-bold uppercase tracking-wider text-slate-400">
          <span>{{ $t('project_detail.lbl_from_date') }}</span><span>{{ $t('project_detail.lbl_to_date') }}</span><span>FTE</span><span />
        </div>
        <div v-for="(it, i) in intervals" :key="i" class="flex flex-col gap-1">
          <div class="grid grid-cols-[1fr_1fr_90px_32px] items-start gap-2">
            <FormDatePicker v-model="it.startDate" :error="errorOf(i, 'startDate')" hide-message />
            <FormDatePicker v-model="it.endDate" :error="errorOf(i, 'endDate')" hide-message />
            <BaseInput v-model="it.fte" type="number" step="0.05" min="0.05" max="2" class="text-center font-bold text-pr" :error="!!errorOf(i, 'fte')" />
            <button type="button" class="mt-1.5 flex h-8 w-8 items-center justify-center rounded-lg text-slate-300 hover:bg-red-50 hover:text-red-500 disabled:opacity-30" :disabled="intervals.length === 1" :aria-label="$t('project_detail.btn_remove_phase')" @click="intervals.splice(i, 1)"><Trash2 :size="15" /></button>
          </div>
          <p v-for="f in ['startDate', 'endDate', 'fte']" v-show="errorOf(i, f)" :key="f" class="text-[11.5px] font-semibold text-rose-600">{{ errorOf(i, f) }}</p>
        </div>
        <p v-if="errors.intervals" class="text-[11.5px] font-semibold text-rose-600">{{ errors.intervals }}</p>
        <button type="button" class="flex items-center gap-1.5 self-start text-[12px] font-bold text-pr hover:underline" @click="addInterval"><Plus :size="14" />{{ $t('project_detail.btn_add_phase') }}</button>
        <div class="mt-1 grid grid-cols-2 gap-3 rounded-xl bg-slate-50 p-3">
          <div><div class="text-[10px] font-bold uppercase tracking-wider text-slate-500">{{ $t('project_detail.lbl_duration') }}</div><div class="text-[16px] font-black text-slate-800">{{ stats.months }} <span class="text-[11px] font-medium text-slate-500">{{ $t('project_detail.lbl_months') }}</span></div></div>
          <div><div class="text-[10px] font-bold uppercase tracking-wider text-slate-500">{{ $t('project_detail.lbl_avg_fte') }}</div><div class="text-[16px] font-black text-pr">{{ stats.avgFte }}</div></div>
        </div>
      </section>
    </div>
    <template #footer>
      <Button variant="danger" :disabled="saving" @click="open = false"><template #leftIcon><X :size="16" /></template>{{ $t('common.btn_cancel') }}</Button>
      <Button variant="primary" :loading="saving" @click="submit"><template #leftIcon><Check :size="16" /></template>{{ isEdit ? $t('project_detail.btn_save_changes') : $t('project_detail.btn_complete_assign') }}</Button>
    </template>
  </BaseDialog>
</template>
