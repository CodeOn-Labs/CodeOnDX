<script setup lang="ts">
// Port TimesheetGroupReport.tsx + TimesheetGroupReportRow.tsx + use-timesheet-group-approval.ts + RejectTimesheetDialog.tsx:
// bảng nhiều nhân viên × ngày, mỗi người một nhóm dòng + dòng Tổng; người có quyền duyệt được Duyệt / Từ chối / Duyệt tất cả.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Check, Lock, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import UserAvatar from '@/shared/components/UserAvatar.vue';
import { workApi, type TimesheetStatus } from '@/modules/work/api/work.api';
import { buildGroupReport, formatHours, LEAVE_COLOR, OT_COLOR, type EmployeeGroup, type GroupSelection } from '@/modules/work/utils/timesheet-rules';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ group: GroupSelection; dates: string[]; canApprove: boolean }>();
const { t, locale } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const from = computed(() => props.dates[0] ?? '');
const to = computed(() => props.dates[props.dates.length - 1] ?? '');
const query = useQuery({
  queryKey: computed(() => ['timesheets', 'group', props.group.employeeIds, from.value, to.value] as const),
  queryFn: ({ signal }) => workApi.timesheetGroup(props.group.employeeIds, from.value, to.value, signal),
  enabled: computed(() => props.dates.length > 0),
});
const groups = computed(() => (query.data.value ? buildGroupReport(query.data.value.employees, query.data.value.entries, props.dates, locale.value) : []));
const pendingIds = computed(() => groups.value.filter((g) => g.status === 'draft').map((g) => g.employee.id));
const busy = ref(false);

const WEEKDAYS = computed(() => [t('timesheet.lbl_dow_sun'), t('timesheet.lbl_dow_mon'), t('timesheet.lbl_dow_tue'), t('timesheet.lbl_dow_wed'), t('timesheet.lbl_dow_thu'), t('timesheet.lbl_dow_fri'), t('timesheet.lbl_dow_sat')]);
const dowOf = (iso: string) => WEEKDAYS.value[new Date(`${iso}T00:00:00`).getDay()];
const period = computed(() =>
  from.value === to.value ? t('timesheet.lbl_period_day', { date: formatDate(from.value) }) : t('timesheet.lbl_period_range', { start: formatDate(from.value), end: formatDate(to.value) }),
);
const cell = (v: number | undefined) => (v ? formatHours(v) : '--');

async function run(employeeIds: string[], action: 'approve' | 'reject', reason?: string) {
  busy.value = true;
  try {
    const res = await workApi.timesheetApproval({ employeeIds, from: from.value, to: to.value, action, reason });
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['timesheets'] });
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    busy.value = false;
  }
}
async function approveOne(g: EmployeeGroup) {
  const ok = await confirm.ask({ title: t('timesheet.confirm_approve_title'), description: t('timesheet.confirm_approve_one_desc', { name: g.employee.fullName, period: period.value }), confirmText: t('timesheet.btn_approve'), type: 'info' });
  if (ok) await run([g.employee.id], 'approve');
}
async function approveAll() {
  const ok = await confirm.ask({ title: t('timesheet.confirm_approve_title'), description: t('timesheet.confirm_approve_all_desc', { count: pendingIds.value.length, period: period.value }), confirmText: t('timesheet.btn_approve'), type: 'info' });
  if (ok) await run(pendingIds.value, 'approve');
}
const rejectTarget = ref<EmployeeGroup | null>(null);
const rejectReason = ref('');
const rejectError = ref('');
const rejectOpen = computed({ get: () => rejectTarget.value !== null, set: (v) => !v && (rejectTarget.value = null) });
function openReject(g: EmployeeGroup) {
  rejectTarget.value = g;
  rejectReason.value = '';
  rejectError.value = '';
}
async function submitReject() {
  if (!rejectReason.value.trim()) {
    rejectError.value = t('timesheet.err_reject_reason_required');
    return;
  }
  const g = rejectTarget.value!;
  rejectTarget.value = null;
  await run([g.employee.id], 'reject', rejectReason.value.trim());
}

const STATUS: Record<TimesheetStatus, { cls: string }> = {
  draft: { cls: 'bg-amber-50 text-amber-600' },
  approved: { cls: 'bg-emerald-50 text-emerald-600' },
  rejected: { cls: 'bg-rose-50 text-rose-600' },
};
const statusLabel = (s: TimesheetStatus) => ({ draft: t('timesheet.lbl_status_draft'), approved: t('timesheet.lbl_status_approved'), rejected: t('timesheet.lbl_status_rejected') })[s];
const genLabel = (g: 'leave' | 'ot') => (g === 'leave' ? t('timesheet.lbl_generated_leave') : t('timesheet.lbl_generated_ot'));
const COL1 = 'sticky left-0 z-10 w-[190px] min-w-[190px] bg-white px-3 py-2.5 border-b border-r border-slate-100 align-top';
const COL2 = 'sticky left-[190px] z-10 w-[210px] min-w-[210px] px-3 py-2.5 border-b border-r border-slate-100 align-top';
const ROW_ACTION = 'inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-[11px] font-bold transition-colors disabled:cursor-not-allowed disabled:opacity-50';
</script>

<template>
  <div class="relative min-h-0 flex-1 overflow-auto rounded-3xl border border-slate-200 bg-white shadow-2xl shadow-slate-200/50">
    <div v-if="query.isPending.value" class="space-y-4 p-5">
      <div v-for="i in 6" :key="i" class="flex animate-pulse items-center gap-3">
        <div class="h-4 w-[156px] rounded bg-slate-100" />
        <div class="h-3.5 flex-1 rounded-full bg-slate-50" />
        <div class="h-4 w-[48px] rounded bg-slate-100" />
      </div>
    </div>
    <table v-else class="w-full border-collapse text-[12.5px]">
      <thead>
        <tr class="border-b border-slate-200 bg-slate-50 text-left text-[11px] font-bold uppercase tracking-wide text-slate-500">
          <th class="sticky left-0 top-0 z-30 w-[190px] min-w-[190px] bg-slate-50 px-3 py-2">{{ $t('timesheet.col_group_staff') }}</th>
          <th class="sticky left-[190px] top-0 z-30 w-[210px] min-w-[210px] bg-slate-50 px-3 py-2">{{ $t('timesheet.col_project_category') }}</th>
          <th class="sticky top-0 z-20 bg-slate-50 px-3 py-2 text-center">{{ $t('timesheet.col_total') }}</th>
          <th v-for="d in props.dates" :key="d" class="sticky top-0 z-20 bg-slate-50 px-2 py-2 text-center">
            <div class="flex flex-col items-center">
              <span>{{ dowOf(d) }}</span>
              <span class="text-slate-400">{{ formatDate(d, 'd/M') }}</span>
            </div>
          </th>
          <th class="sticky top-0 z-20 bg-slate-50 px-3 py-2 text-center">
            <div class="flex flex-col items-center gap-1">
              <span>{{ $t('timesheet.col_status') }}</span>
              <Button v-if="props.canApprove" variant="success" size="sm" :disabled="pendingIds.length === 0 || busy" class="h-6 px-2 text-[10.5px] normal-case tracking-normal rounded-lg" @click="approveAll">
                <template #leftIcon><Check class="h-3.5 w-3.5" /></template>
                {{ $t('timesheet.btn_approve_all') }}
              </Button>
            </div>
          </th>
        </tr>
      </thead>
      <tbody>
        <template v-for="g in groups" :key="g.employee.id">
          <tr v-if="g.isEmpty" class="border-b border-slate-100">
            <td :class="COL1">
              <div class="flex items-center gap-2">
                <UserAvatar :name="g.employee.fullName" :id="g.employee.id" :src="g.employee.avatarUrl" :size="28" />
                <span class="truncate text-[12.5px] font-bold text-slate-800">{{ g.employee.fullName }}</span>
              </div>
            </td>
            <td :class="[COL2, 'bg-white italic text-slate-400']">{{ $t('timesheet.lbl_no_work_logged') }}</td>
            <td class="border-b border-slate-100 px-3 py-2.5 text-center">--</td>
            <td v-for="d in props.dates" :key="d" class="border-b border-slate-100 px-2 py-2.5 text-center">--</td>
            <td class="border-b border-slate-100 px-3 py-2.5 text-center">--</td>
          </tr>
          <template v-else>
            <tr
              v-for="(row, idx) in g.rows"
              :key="row.key"
              :class="cn('border-b border-slate-50', row.generated === 'leave' && 'bg-red-50', row.generated === 'ot' && 'bg-orange-50', row.generated && 'font-bold')"
              :style="row.generated ? { color: row.generated === 'leave' ? LEAVE_COLOR : OT_COLOR } : undefined"
            >
              <td v-if="idx === 0" :rowspan="g.rows.length + 1" :class="COL1">
                <div class="flex items-center gap-2">
                  <UserAvatar :name="g.employee.fullName" :id="g.employee.id" :src="g.employee.avatarUrl" :size="28" />
                  <span class="truncate text-[12.5px] font-bold text-slate-800">{{ g.employee.fullName }}</span>
                </div>
              </td>
              <td :class="cn(COL2, row.generated === 'leave' ? 'bg-red-50' : row.generated === 'ot' ? 'bg-orange-50' : 'bg-white')" :title="row.generated ? $t('timesheet.tip_generated_readonly') : undefined">
                <span class="inline-flex items-center gap-1 truncate">
                  <Lock v-if="row.generated" class="h-3 w-3 shrink-0" />
                  <span class="truncate">{{ row.generated ? `${row.label} (${genLabel(row.generated)})` : row.label }}</span>
                </span>
              </td>
              <td class="px-3 py-1.5 text-center tabular-nums font-bold">{{ cell(row.total) }}</td>
              <td v-for="d in props.dates" :key="d" class="px-2 py-1.5 text-center tabular-nums">{{ cell(row.hoursByDate[d]) }}</td>
              <td v-if="idx === 0" :rowspan="g.rows.length + 1" class="border-b border-slate-100 px-3 py-2.5 text-center align-top">
                <div class="flex flex-col items-center gap-1.5">
                  <span v-if="g.status" :class="cn('inline-flex items-center gap-1 whitespace-nowrap rounded-full px-2 py-0.5 text-[11px] font-bold', STATUS[g.status].cls)">
                    <Check v-if="g.status === 'approved'" class="h-3 w-3" />
                    <X v-else-if="g.status === 'rejected'" class="h-3 w-3" />
                    {{ statusLabel(g.status) }}
                  </span>
                  <div v-if="props.canApprove && g.status === 'draft'" class="flex items-center gap-1">
                    <button type="button" :disabled="busy" :class="cn(ROW_ACTION, 'border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100')" @click="approveOne(g)">
                      <Check class="h-3 w-3 text-emerald-600" />{{ $t('timesheet.btn_approve') }}
                    </button>
                    <button type="button" :disabled="busy" :class="cn(ROW_ACTION, 'border-rose-200 bg-rose-50 text-rose-700 hover:bg-rose-100')" @click="openReject(g)">
                      <X class="h-3 w-3 text-rose-600" />{{ $t('timesheet.btn_reject') }}
                    </button>
                  </div>
                </div>
              </td>
            </tr>
            <tr class="border-b border-slate-100 bg-slate-50/70 font-bold text-slate-800">
              <td :class="[COL2, 'bg-white uppercase']">{{ $t('timesheet.col_total') }}</td>
              <td class="px-3 py-1.5 text-center tabular-nums text-pr">{{ cell(g.totalRow?.total) }}</td>
              <td v-for="d in props.dates" :key="d" class="px-2 py-1.5 text-center tabular-nums">{{ cell(g.totalRow?.hoursByDate[d]) }}</td>
            </tr>
          </template>
        </template>
      </tbody>
    </table>

    <BaseDialog v-model:open="rejectOpen" :title="$t('timesheet.title_reject_dialog', { name: rejectTarget?.employee.fullName ?? '' })" max-width="md">
      <BaseTextarea v-model="rejectReason" :rows="4" :placeholder="$t('timesheet.ph_reject_reason')" :error="!!rejectError" />
      <p v-if="rejectError" class="mt-1 text-[11px] font-bold text-red-500">{{ rejectError }}</p>
      <template #footer>
        <Button variant="outline" @click="rejectTarget = null">{{ $t('common.btn_cancel') }}</Button>
        <Button variant="danger" @click="submitReject">{{ $t('timesheet.btn_confirm_reject') }}</Button>
      </template>
    </BaseDialog>
  </div>
</template>
