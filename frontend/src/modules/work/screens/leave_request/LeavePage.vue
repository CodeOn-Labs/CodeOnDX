<script setup lang="ts">
// Màn mới (D4): Nghỉ phép & OT — thay phần nghỉ phép/OT đồng bộ từ RON của bản gốc (không có giao diện để chuyển nguyên).
// Tab Đơn nghỉ / Đơn OT của tôi; tab Duyệt đơn (màn con leave_approval, quyền timesheet.approve).
// Đơn đã duyệt tự sinh dòng công (nghỉ phép / OT) trong Nhật ký công việc — BE xử lý.
// Quỹ phép, hệ số OT, giới hạn OT: tham số ở Quản trị → Quy tắc (LEAVE_*, OT_*); ngày lễ ở Quản trị → Cài đặt.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query';
import { CalendarCheck, CalendarClock, CalendarOff, CheckCircle2, Clock, Hourglass, Plus, Timer, XCircle } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import DynamicInsights from '@/modules/ai/components/DynamicInsights.vue';
import Button from '@/shared/components/Button.vue';
import StatSegment from '@/shared/components/StatSegment.vue';
import TabBar from '@/shared/components/TabBar.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import CommonTable from '@/shared/components/table/CommonTable.vue';
import ColorBadge from '@/shared/components/ColorBadge.vue';
import UserAvatar from '@/shared/components/UserAvatar.vue';
import type { TableColumn } from '@/shared/components/table/types';
import LeaveDialog from './LeaveDialog.vue';
import OtDialog from './OtDialog.vue';
import DecisionDialog from './DecisionDialog.vue';
import RequestStatusBadge from './RequestStatusBadge.vue';
import { workApi, type LeaveRequest, type OtRequest, type RequestFilters, type RequestStatus } from '@/modules/work/api/work.api';
import type { PagedResult } from '@/api/types';
import { usePagination } from '@/shared/composables/usePagination';
import { useAuthStore } from '@/stores/auth';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';

type Tab = 'leave' | 'ot' | 'approval';
type Kind = 'leave' | 'ot';

const { t, locale } = useI18n();
const auth = useAuthStore();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();
const canApprove = computed(() => auth.can('timesheet.approve'));
const canCreate = computed(() => auth.can('timesheet.edit'));

const summary = useQuery({ queryKey: ['leave', 'summary'], queryFn: () => workApi.leaveSummary(), retry: false, meta: { silent: true } });
const tab = ref<Tab>(canCreate.value ? 'leave' : 'approval');
const approvalKind = ref<Kind>('leave');
const tabs = computed(() => [
  ...(canCreate.value ? [{ key: 'leave' as Tab, label: t('leave_request.tab_leave'), icon: CalendarOff }, { key: 'ot' as Tab, label: t('leave_request.tab_ot'), icon: Timer }] : []),
  ...(canApprove.value ? [{ key: 'approval' as Tab, label: t('leave_request.tab_approval'), icon: CalendarCheck, count: summary.data.value?.pendingApproval }] : []),
]);

const status = ref<RequestStatus[]>([]);
const statusOptions = computed(() => (['pending', 'approved', 'rejected', 'cancelled'] as const).map((s) => ({ value: s, label: enumStatus(s) })));
const enumStatus = (s: RequestStatus) => ({ pending: t('leave_request.opt_status_pending'), approved: t('leave_request.opt_status_approved'), rejected: t('leave_request.opt_status_rejected'), cancelled: t('leave_request.opt_status_cancelled') })[s];
watch(tab, () => {
  status.value = tab.value === 'approval' ? ['pending'] : [];
  pagination.goToPage(0);
});

const kind = computed<Kind>(() => (tab.value === 'approval' ? approvalKind.value : tab.value));
const filters = computed<RequestFilters>(() => ({ scope: tab.value === 'approval' ? 'approval' : 'mine', statuses: status.value, search: '' }));
const pagination = usePagination();
const listQuery = useQuery({
  queryKey: computed(() => ['leave', kind.value, filters.value, pagination.pageIndex.value, pagination.pageSize.value, locale.value] as const),
  queryFn: ({ signal }): Promise<PagedResult<LeaveRequest | OtRequest>> =>
    kind.value === 'leave'
      ? workApi.leaveRequests(filters.value, pagination.pageIndex.value + 1, pagination.pageSize.value, signal)
      : workApi.otRequests(filters.value, pagination.pageIndex.value + 1, pagination.pageSize.value, signal),
  placeholderData: keepPreviousData,
});
watch(
  () => listQuery.data.value?.totalCount,
  (total) => {
    if (total !== undefined) pagination.totalItems.value = total;
  },
  { immediate: true },
);
const leaveRows = computed(() => (kind.value === 'leave' ? ((listQuery.data.value?.items ?? []) as LeaveRequest[]) : []));
const otRows = computed(() => (kind.value === 'ot' ? ((listQuery.data.value?.items ?? []) as OtRequest[]) : []));

const leaveColumns = computed<TableColumn[]>(() => [
  ...(tab.value === 'approval' ? [{ id: 'employee', header: t('leave_request.col_employee') }] : []),
  { id: 'code', header: t('leave_request.col_code'), headerClass: 'w-[100px]' },
  { id: 'type', header: t('leave_request.col_leave_type') },
  { id: 'period', header: t('leave_request.col_period') },
  { id: 'days', header: t('leave_request.col_days'), headerClass: 'text-center', cellClass: 'text-center' },
  { id: 'reason', header: t('leave_request.col_reason') },
  { id: 'status', header: t('leave_request.col_status') },
  { id: 'actions', header: '', cellClass: 'text-right' },
]);
const otColumns = computed<TableColumn[]>(() => [
  ...(tab.value === 'approval' ? [{ id: 'employee', header: t('leave_request.col_employee') }] : []),
  { id: 'code', header: t('leave_request.col_code'), headerClass: 'w-[100px]' },
  { id: 'date', header: t('leave_request.col_ot_date') },
  { id: 'time', header: t('leave_request.col_ot_time') },
  { id: 'hours', header: t('leave_request.col_ot_hours'), headerClass: 'text-center', cellClass: 'text-center' },
  { id: 'project', header: t('leave_request.col_project') },
  { id: 'reason', header: t('leave_request.col_reason') },
  { id: 'status', header: t('leave_request.col_status') },
  { id: 'actions', header: '', cellClass: 'text-right' },
]);
const durationLabel = (d: LeaveRequest['durationType']) => ({ full_day: t('leave_request.opt_full_day'), half_day_am: t('leave_request.opt_half_day_am'), half_day_pm: t('leave_request.opt_half_day_pm') })[d];
const dayKindLabel = (k: OtRequest['dayKind']) => ({ weekday: t('leave_request.opt_day_weekday'), weekend: t('leave_request.opt_day_weekend'), holiday: t('leave_request.opt_day_holiday') })[k];

// ─── Thao tác ───────────────────────────────────────────────────────────────
const leaveOpen = ref(false);
const otOpen = ref(false);
const decision = ref<{ kind: Kind; id: string; code: string; action: 'approve' | 'reject' } | null>(null);
function refresh() {
  void queryClient.invalidateQueries({ queryKey: ['leave'] });
  void queryClient.invalidateQueries({ queryKey: ['timesheets'] });
}
async function cancel(k: Kind, row: LeaveRequest | OtRequest) {
  const ok = await confirm.ask({ title: t('leave_request.confirm_cancel_title'), description: t('leave_request.confirm_cancel_desc', { code: row.code }), confirmText: t('leave_request.btn_cancel_request'), type: 'warning' });
  if (!ok) return;
  try {
    const res = k === 'leave' ? await workApi.decideLeave(row.id, 'cancel') : await workApi.decideOt(row.id, 'cancel');
    toast.success(res.messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const s = computed(() => summary.data.value);
</script>

<template>
  <PageLayout layout="A" :insight-title="$t('leave_request.title_insight_panel')" content-padding="24px" disable-scroll insights-width="300px">
    <template #insights>
      <DynamicInsights :screen="tab === 'approval' ? 'leave_approval' : 'leave_request'" />
    </template>

    <div class="h-full flex flex-col min-h-0 gap-3">
      <div class="flex flex-col gap-3 rounded-2xl bg-white/60 backdrop-blur-xl p-3 lg:p-4 shadow-xl shadow-slate-200/40 border border-white/60">
        <div class="flex items-center justify-between gap-3 flex-wrap">
          <div class="flex flex-col gap-1">
            <div class="flex items-center gap-2">
              <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
              <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ $t('leave_request.lbl_kicker') }}</span>
            </div>
            <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">{{ $t('leave_request.title_page') }}</h1>
          </div>
          <div v-if="canCreate" class="flex items-center gap-2">
            <Button variant="outline" class="h-9 rounded-xl px-4 text-[13px] font-bold" @click="otOpen = true">
              <template #leftIcon><Timer :size="15" /></template>
              {{ $t('leave_request.btn_create_ot') }}
            </Button>
            <Button variant="primary" class="h-9 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" @click="leaveOpen = true">
              <template #leftIcon><Plus :size="16" :stroke-width="3" /></template>
              {{ $t('leave_request.btn_create_leave') }}
            </Button>
          </div>
        </div>
      </div>

      <div v-if="s" class="grid grid-cols-2 lg:grid-cols-5 gap-px rounded-2xl border border-white/60 bg-white/40 backdrop-blur-xl shadow-xl shadow-slate-200/40 overflow-hidden">
        <StatSegment :label="$t('leave_request.stat_remaining')" :value="s.balance.remainingDays" tone="green" :icon="CalendarCheck" />
        <StatSegment :label="$t('leave_request.stat_entitled')" :value="`${s.balance.entitledDays} + ${s.balance.carriedOverDays}`" tone="violet" :icon="CalendarClock" :delay="0.05" />
        <StatSegment :label="$t('leave_request.stat_used')" :value="s.balance.usedDays" tone="slate" :icon="CheckCircle2" :delay="0.1" />
        <StatSegment :label="$t('leave_request.stat_pending')" :value="s.balance.pendingDays" tone="amber" :icon="Hourglass" :delay="0.15" />
        <StatSegment :label="$t('leave_request.stat_ot_month', { max: s.ot.maxPerMonth })" :value="`${s.ot.monthHours}h`" tone="blue" :icon="Clock" :delay="0.2" />
      </div>

      <div class="flex items-center justify-between gap-3 flex-wrap">
        <TabBar v-model="tab" :tabs="tabs" class="flex-1" />
        <div class="flex items-center gap-2">
          <TabBar
            v-if="tab === 'approval'"
            v-model="approvalKind"
            size="sm"
            :tabs="[{ key: 'leave', label: $t('leave_request.tab_leave') }, { key: 'ot', label: $t('leave_request.tab_ot') }]"
            class="border-none"
          />
          <div class="w-[220px]">
            <FormSelect v-model="status" :options="statusOptions" :placeholder="$t('leave_request.ph_all_statuses')" multiple hide-message trigger-class="h-9 rounded-xl text-[12.5px]" />
          </div>
        </div>
      </div>

      <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
        <CommonTable
          v-if="kind === 'leave'"
          :columns="leaveColumns"
          :rows="leaveRows"
          :loading="listQuery.isFetching.value"
          :empty-message="$t('leave_request.empty_no_request')"
          :pagination="pagination"
          table-class="min-w-[960px]"
          container-class="flex-1 min-h-0"
          table-wrapper-class="border-none shadow-none bg-transparent"
        >
          <template #cell-employee="{ row }">
            <div v-if="row.employee" class="flex items-center gap-2 py-1.5">
              <UserAvatar :name="row.employee.fullName" :id="row.employee.id" :src="row.employee.avatarUrl" :size="30" />
              <div class="flex flex-col"><span class="text-[12.5px] font-bold text-slate-800">{{ row.employee.fullName }}</span><span class="text-[10px] text-slate-400">{{ row.employee.code }}</span></div>
            </div>
          </template>
          <template #cell-code="{ row }"><span class="font-mono text-[11px] font-bold text-slate-500">{{ row.code }}</span></template>
          <template #cell-type="{ row }"><ColorBadge v-if="row.leaveType" :label="row.leaveType.name" :color="row.leaveType.color" /></template>
          <template #cell-period="{ row }">
            <div class="flex flex-col">
              <span class="text-[12.5px] font-bold text-slate-700">{{ row.fromDate === row.toDate ? formatDate(row.fromDate) : `${formatDate(row.fromDate)} → ${formatDate(row.toDate)}` }}</span>
              <span class="text-[10.5px] text-slate-400">{{ durationLabel(row.durationType) }}</span>
            </div>
          </template>
          <template #cell-days="{ row }"><span class="text-[13px] font-black text-slate-800">{{ row.days }}</span></template>
          <template #cell-reason="{ row }">
            <div class="max-w-[260px]">
              <p class="truncate text-[12px] text-slate-600" :title="row.reason">{{ row.reason }}</p>
              <p v-if="row.decisionNote" class="truncate text-[10.5px] italic text-rose-500" :title="row.decisionNote">{{ row.decisionNote }}</p>
            </div>
          </template>
          <template #cell-status="{ row }"><RequestStatusBadge :status="row.status" :approver="row.approver?.fullName" /></template>
          <template #cell-actions="{ row }">
            <div class="flex justify-end gap-1">
              <template v-if="tab === 'approval' && row.status === 'pending'">
                <button type="button" class="inline-flex items-center gap-1 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[11px] font-bold text-emerald-700 hover:bg-emerald-100" @click="decision = { kind: 'leave', id: row.id, code: row.code, action: 'approve' }">
                  <CheckCircle2 :size="12" />{{ $t('leave_request.btn_approve') }}
                </button>
                <button type="button" class="inline-flex items-center gap-1 rounded-md border border-rose-200 bg-rose-50 px-2 py-0.5 text-[11px] font-bold text-rose-700 hover:bg-rose-100" @click="decision = { kind: 'leave', id: row.id, code: row.code, action: 'reject' }">
                  <XCircle :size="12" />{{ $t('leave_request.btn_reject') }}
                </button>
              </template>
              <button v-else-if="tab !== 'approval' && ['pending', 'approved'].includes(row.status)" type="button" class="rounded-md border border-slate-200 px-2 py-0.5 text-[11px] font-bold text-slate-500 hover:border-rose-200 hover:text-rose-600" @click="cancel('leave', row)">
                {{ $t('leave_request.btn_cancel_request') }}
              </button>
            </div>
          </template>
        </CommonTable>

        <CommonTable
          v-else
          :columns="otColumns"
          :rows="otRows"
          :loading="listQuery.isFetching.value"
          :empty-message="$t('leave_request.empty_no_request')"
          :pagination="pagination"
          table-class="min-w-[960px]"
          container-class="flex-1 min-h-0"
          table-wrapper-class="border-none shadow-none bg-transparent"
        >
          <template #cell-employee="{ row }">
            <div v-if="row.employee" class="flex items-center gap-2 py-1.5">
              <UserAvatar :name="row.employee.fullName" :id="row.employee.id" :src="row.employee.avatarUrl" :size="30" />
              <div class="flex flex-col"><span class="text-[12.5px] font-bold text-slate-800">{{ row.employee.fullName }}</span><span class="text-[10px] text-slate-400">{{ row.employee.code }}</span></div>
            </div>
          </template>
          <template #cell-code="{ row }"><span class="font-mono text-[11px] font-bold text-slate-500">{{ row.code }}</span></template>
          <template #cell-date="{ row }">
            <div class="flex flex-col">
              <span class="text-[12.5px] font-bold text-slate-700">{{ formatDate(row.workDate) }}</span>
              <span class="text-[10.5px] text-slate-400">{{ dayKindLabel(row.dayKind) }} · {{ row.ratePercent }}%</span>
            </div>
          </template>
          <template #cell-time="{ row }"><span class="text-[12.5px] font-semibold text-slate-600">{{ row.startTime }} – {{ row.endTime }}</span></template>
          <template #cell-hours="{ row }"><span class="text-[13px] font-black text-slate-800">{{ row.hours }}h</span></template>
          <template #cell-project="{ row }"><span class="text-[12px] font-semibold text-slate-600">{{ row.project?.name ?? '—' }}</span></template>
          <template #cell-reason="{ row }">
            <div class="max-w-[240px]">
              <p class="truncate text-[12px] text-slate-600" :title="row.reason">{{ row.reason }}</p>
              <p v-if="row.decisionNote" class="truncate text-[10.5px] italic text-rose-500" :title="row.decisionNote">{{ row.decisionNote }}</p>
            </div>
          </template>
          <template #cell-status="{ row }"><RequestStatusBadge :status="row.status" :approver="row.approver?.fullName" /></template>
          <template #cell-actions="{ row }">
            <div class="flex justify-end gap-1">
              <template v-if="tab === 'approval' && row.status === 'pending'">
                <button type="button" class="inline-flex items-center gap-1 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[11px] font-bold text-emerald-700 hover:bg-emerald-100" @click="decision = { kind: 'ot', id: row.id, code: row.code, action: 'approve' }">
                  <CheckCircle2 :size="12" />{{ $t('leave_request.btn_approve') }}
                </button>
                <button type="button" class="inline-flex items-center gap-1 rounded-md border border-rose-200 bg-rose-50 px-2 py-0.5 text-[11px] font-bold text-rose-700 hover:bg-rose-100" @click="decision = { kind: 'ot', id: row.id, code: row.code, action: 'reject' }">
                  <XCircle :size="12" />{{ $t('leave_request.btn_reject') }}
                </button>
              </template>
              <button v-else-if="tab !== 'approval' && ['pending', 'approved'].includes(row.status)" type="button" class="rounded-md border border-slate-200 px-2 py-0.5 text-[11px] font-bold text-slate-500 hover:border-rose-200 hover:text-rose-600" @click="cancel('ot', row)">
                {{ $t('leave_request.btn_cancel_request') }}
              </button>
            </div>
          </template>
        </CommonTable>
      </div>
    </div>

    <LeaveDialog v-model:open="leaveOpen" :summary="s ?? null" @saved="refresh" />
    <OtDialog v-model:open="otOpen" :summary="s ?? null" @saved="refresh" />
    <DecisionDialog :decision="decision" @close="decision = null" @done="refresh" />
  </PageLayout>
</template>
