<script setup lang="ts">
// Màn mới (D4): Chấm công vào/ra — thay dữ liệu chấm công lấy từ RON của bản gốc.
// Bảng tháng × nhân sự: giờ vào/ra, đi muộn (sau work_start_time + ATTENDANCE_LATE_GRACE_MINUTES), thiếu giờ ra, đang nghỉ phép.
// 3 nguồn ghi: nút Vào/Ra (giờ máy chủ, bật/tắt bằng allow_app_check_in), import file máy chấm công, sửa tay (quản lý ghi chú).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query';
import { AlarmClock, CalendarOff, LogIn, LogOut, Search, Upload, UserX } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import DynamicInsights from '@/modules/ai/components/DynamicInsights.vue';
import Button from '@/shared/components/Button.vue';
import StatSegment from '@/shared/components/StatSegment.vue';
import MonthNavigator from '@/shared/components/form/MonthNavigator.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import PaginationFooter from '@/shared/components/table/PaginationFooter.vue';
import UserAvatar from '@/shared/components/UserAvatar.vue';
import AttendanceEditDialog from './AttendanceEditDialog.vue';
import AttendanceImportDialog from './AttendanceImportDialog.vue';
import { workApi, type AttendanceCell, type AttendanceRow } from '@/modules/work/api/work.api';
import { usePagination } from '@/shared/composables/usePagination';
import { useAuthStore } from '@/stores/auth';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const { t } = useI18n();
const auth = useAuthStore();
const toast = useToastStore();
const queryClient = useQueryClient();

const month = ref(new Date().toISOString().slice(0, 7));
const search = ref('');
const committedSearch = ref('');
const onlyIssues = ref(false);
const pagination = usePagination({ initialPageSize: 50, pageSizeOptions: [20, 50, 100] });
const query = useQuery({
  queryKey: computed(() => ['attendance', month.value, committedSearch.value, onlyIssues.value, pagination.pageIndex.value, pagination.pageSize.value] as const),
  queryFn: ({ signal }) => workApi.attendance(month.value, { search: committedSearch.value, departmentIds: [], onlyIssues: onlyIssues.value }, pagination.pageIndex.value + 1, pagination.pageSize.value, signal),
  placeholderData: keepPreviousData,
});
watch(
  () => query.data.value?.totalCount,
  (total) => {
    if (total !== undefined) pagination.totalItems.value = total;
  },
  { immediate: true },
);
watch([month, onlyIssues], () => pagination.goToPage(0));
const data = computed(() => query.data.value);
const todayQuery = useQuery({ queryKey: ['attendance', 'today'], queryFn: workApi.attendanceToday });
const today = computed(() => todayQuery.data.value);

const totals = computed(() => {
  const rows = data.value?.items ?? [];
  return {
    late: rows.reduce((s, r) => s + r.summary.lateCount, 0),
    missing: rows.reduce((s, r) => s + r.summary.missingOutCount, 0),
    leave: rows.reduce((s, r) => s + r.summary.leaveDays, 0),
  };
});

// ─── Vào / Ra ───────────────────────────────────────────────────────────────
const checking = ref(false);
async function check(kind: 'in' | 'out') {
  checking.value = true;
  try {
    const res = kind === 'in' ? await workApi.checkIn() : await workApi.checkOut();
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['attendance'] });
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    checking.value = false;
  }
}

// ─── Sửa ô ──────────────────────────────────────────────────────────────────
const editing = ref<{ row: AttendanceRow; date: string; cell: AttendanceCell | null } | null>(null);
function canEditCell(row: AttendanceRow, date: string) {
  const d = data.value;
  if (!d || date > (today.value?.date ?? '')) return false;
  if (d.canEditAll) return true;
  if (row.employee.id !== d.myEmployeeId || !auth.can('timesheet.edit')) return false;
  const limit = new Date(`${today.value?.date ?? date}T00:00:00`);
  limit.setDate(limit.getDate() - d.editLimitDays);
  return new Date(`${date}T00:00:00`) >= limit;
}
function openCell(row: AttendanceRow, date: string) {
  if (canEditCell(row, date)) editing.value = { row, date, cell: row.records[date] ?? null };
}
const importOpen = ref(false);
const dayNum = (iso: string) => Number(iso.slice(8, 10));
const DOW = computed(() => [t('attendance.lbl_dow_sun'), t('attendance.lbl_dow_mon'), t('attendance.lbl_dow_tue'), t('attendance.lbl_dow_wed'), t('attendance.lbl_dow_thu'), t('attendance.lbl_dow_fri'), t('attendance.lbl_dow_sat')]);
const dowOf = (iso: string) => DOW.value[new Date(`${iso}T00:00:00`).getDay()];
</script>

<template>
  <PageLayout layout="A" :insight-title="$t('attendance.title_insight_panel')" content-padding="24px" disable-scroll insights-width="300px">
    <template #insights>
      <div class="flex flex-col gap-4">
        <div v-if="today" class="rounded-2xl border border-slate-100 bg-white p-4 shadow-sm">
          <div class="text-[11px] font-black uppercase tracking-widest text-slate-400">{{ $t('attendance.title_today') }}</div>
          <div class="mt-2 grid grid-cols-2 gap-2 text-center">
            <div class="rounded-xl bg-slate-50 p-2">
              <div class="text-[10px] font-bold text-slate-400">{{ $t('attendance.lbl_check_in') }}</div>
              <div :class="cn('text-[18px] font-black', today.record?.late ? 'text-rose-500' : 'text-slate-800')">{{ today.record?.checkIn ?? '--:--' }}</div>
            </div>
            <div class="rounded-xl bg-slate-50 p-2">
              <div class="text-[10px] font-bold text-slate-400">{{ $t('attendance.lbl_check_out') }}</div>
              <div class="text-[18px] font-black text-slate-800">{{ today.record?.checkOut ?? '--:--' }}</div>
            </div>
          </div>
          <div v-if="today.allowAppCheckIn" class="mt-3 grid grid-cols-2 gap-2">
            <Button variant="success" size="sm" :disabled="!!today.record?.checkIn" :loading="checking" @click="check('in')">
              <template #leftIcon><LogIn :size="14" /></template>{{ $t('attendance.btn_check_in') }}
            </Button>
            <Button variant="secondary" size="sm" :disabled="!today.record?.checkIn" :loading="checking" @click="check('out')">
              <template #leftIcon><LogOut :size="14" /></template>{{ $t('attendance.btn_check_out') }}
            </Button>
          </div>
          <p v-else class="mt-3 text-[11px] italic text-slate-400">{{ $t('attendance.hint_app_check_in_off') }}</p>
          <p class="mt-2 text-[10px] text-slate-400">{{ $t('attendance.hint_work_start', { time: today.workStartTime, grace: data?.graceMinutes ?? 0 }) }}</p>
        </div>
        <DynamicInsights screen="attendance" />
      </div>
    </template>

    <div class="h-full flex flex-col min-h-0 gap-3">
      <div class="flex flex-col gap-3 rounded-2xl bg-white/60 backdrop-blur-xl p-3 lg:p-4 shadow-xl shadow-slate-200/40 border border-white/60">
        <div class="flex items-center justify-between gap-3 flex-wrap">
          <div class="flex flex-col gap-1">
            <div class="flex items-center gap-2">
              <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
              <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ $t('attendance.lbl_kicker') }}</span>
            </div>
            <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">{{ $t('attendance.title_page') }}</h1>
          </div>
          <div class="flex items-center gap-3 flex-wrap">
            <MonthNavigator v-model="month" />
            <Button v-if="data?.canEditAll" variant="outline" class="h-9 lg:h-10 rounded-xl px-4 text-[13px] font-bold" @click="importOpen = true">
              <template #leftIcon><Upload :size="15" /></template>
              {{ $t('attendance.btn_import') }}
            </Button>
          </div>
        </div>
        <form v-if="data?.canEditAll" class="flex items-center gap-3 flex-wrap" @submit.prevent="committedSearch = search; pagination.goToPage(0)">
          <div class="group relative flex items-center">
            <Search :size="16" class="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 group-focus-within:text-pr" />
            <input
              v-model="search"
              type="text"
              :placeholder="$t('attendance.ph_search')"
              :aria-label="$t('attendance.ph_search')"
              class="h-9 lg:h-10 w-72 rounded-xl border border-slate-200 bg-white pl-10 pr-4 text-[13.5px] font-medium text-slate-900 outline-none transition-all placeholder:text-slate-400 focus:border-pr focus:ring-4 focus:ring-pr/10"
            />
          </div>
          <Button type="submit" variant="primary" class="h-9 lg:h-10 rounded-xl px-5 text-[13px] font-bold">{{ $t('attendance.btn_search') }}</Button>
          <label class="ml-auto flex items-center gap-2 text-[12px] font-bold text-slate-600">
            <SwitchToggle v-model="onlyIssues" size="sm" :label="$t('attendance.lbl_only_issues')" />
            {{ $t('attendance.lbl_only_issues') }}
          </label>
        </form>
      </div>

      <div class="grid grid-cols-2 lg:grid-cols-4 gap-px rounded-2xl border border-white/60 bg-white/40 backdrop-blur-xl shadow-xl shadow-slate-200/40 overflow-hidden">
        <StatSegment :label="$t('attendance.stat_people')" :value="data?.totalCount ?? 0" tone="violet" :icon="AlarmClock" :loading="!data" />
        <StatSegment :label="$t('attendance.stat_late')" :value="totals.late" tone="red" :icon="AlarmClock" :delay="0.05" :loading="!data" />
        <StatSegment :label="$t('attendance.stat_missing_out')" :value="totals.missing" tone="amber" :icon="UserX" :delay="0.1" :loading="!data" />
        <StatSegment :label="$t('attendance.stat_leave_days')" :value="totals.leave" tone="blue" :icon="CalendarOff" :delay="0.15" :loading="!data" />
      </div>

      <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
        <div class="flex-1 min-h-0 overflow-auto" style="scrollbar-gutter: stable">
          <table class="border-separate border-spacing-0 text-[11.5px] min-w-max">
            <thead class="sticky top-0 z-20">
              <tr class="bg-slate-50/90 backdrop-blur-md text-[10.5px] font-bold uppercase tracking-wider text-slate-500">
                <th class="sticky left-0 z-30 bg-slate-50/95 border-b border-r border-slate-200 px-4 py-2 text-left min-w-[220px]">{{ $t('attendance.col_employee') }}</th>
                <th class="border-b border-slate-200 px-2 py-2 text-center min-w-[90px]">{{ $t('attendance.col_summary') }}</th>
                <th
                  v-for="d in data?.days ?? []"
                  :key="d.date"
                  :class="cn('border-b border-slate-200 px-1 py-2 text-center min-w-[64px]', d.weekend && 'bg-slate-100/80 text-slate-400', d.holiday && 'bg-rose-50 text-rose-400')"
                  :title="d.holiday ?? undefined"
                >
                  <div class="flex flex-col items-center leading-tight">
                    <span>{{ dowOf(d.date) }}</span>
                    <span class="text-[11px] font-black">{{ dayNum(d.date) }}</span>
                  </div>
                </th>
              </tr>
            </thead>
            <tbody>
              <template v-if="query.isPending.value">
                <tr v-for="i in 6" :key="i"><td :colspan="33" class="border-b border-slate-100 px-4 py-4"><div class="h-4 animate-pulse rounded bg-slate-100" /></td></tr>
              </template>
              <tr v-for="row in data?.items ?? []" :key="row.employee.id" class="group">
                <td class="sticky left-0 z-10 bg-white group-hover:bg-slate-50 border-b border-r border-slate-100 px-4 py-1.5">
                  <div class="flex items-center gap-2">
                    <UserAvatar :name="row.employee.fullName" :id="row.employee.id" :src="row.employee.avatarUrl" :size="28" />
                    <div class="flex flex-col min-w-0">
                      <span class="truncate text-[12.5px] font-bold text-slate-800">{{ row.employee.fullName }}</span>
                      <span class="text-[10px] text-slate-400">{{ row.employee.code }}</span>
                    </div>
                  </div>
                </td>
                <td class="border-b border-slate-100 px-2 py-1.5 text-center">
                  <div class="flex flex-col items-center gap-0.5 text-[10px] font-bold">
                    <span class="text-slate-600">{{ $t('attendance.lbl_worked_days', { n: row.summary.workedDays }) }}</span>
                    <span v-if="row.summary.lateCount" class="text-rose-500">{{ $t('attendance.lbl_late_count', { n: row.summary.lateCount }) }}</span>
                    <span v-if="row.summary.missingOutCount" class="text-amber-500">{{ $t('attendance.lbl_missing_count', { n: row.summary.missingOutCount }) }}</span>
                  </div>
                </td>
                <td
                  v-for="d in data?.days ?? []"
                  :key="d.date"
                  :class="cn(
                    'border-b border-l border-slate-100 px-1 py-1 text-center align-middle',
                    d.weekend && 'bg-slate-50/80',
                    d.holiday && 'bg-rose-50/50',
                    canEditCell(row, d.date) && 'cursor-pointer hover:bg-pr/5',
                  )"
                  :title="row.records[d.date]?.note ?? undefined"
                  @click="openCell(row, d.date)"
                >
                  <template v-if="row.records[d.date]">
                    <div v-if="row.records[d.date]!.onLeave && !row.records[d.date]!.checkIn" class="rounded-md bg-blue-50 px-1 py-0.5 text-[9.5px] font-black text-blue-600">{{ $t('attendance.lbl_on_leave') }}</div>
                    <div v-else class="flex flex-col leading-tight">
                      <span :class="cn('font-bold', row.records[d.date]!.late ? 'text-rose-500' : 'text-slate-700')">{{ row.records[d.date]!.checkIn ?? '--' }}</span>
                      <span :class="cn('font-semibold', row.records[d.date]!.missingOut ? 'text-amber-500' : 'text-slate-400')">{{ row.records[d.date]!.checkOut ?? (row.records[d.date]!.missingOut ? '??' : '--') }}</span>
                      <span v-if="row.records[d.date]!.source === 'manual'" class="mx-auto mt-0.5 h-1 w-1 rounded-full bg-pr" />
                    </div>
                  </template>
                </td>
              </tr>
              <tr v-if="!query.isPending.value && (data?.items.length ?? 0) === 0">
                <td :colspan="(data?.days.length ?? 0) + 2" class="px-6 py-16 text-center text-[13px] font-medium text-slate-400">{{ $t('attendance.empty_no_data') }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="flex items-center gap-4 border-t border-slate-100 px-4 py-2 text-[10.5px] font-semibold text-slate-400">
          <span class="flex items-center gap-1"><span class="h-2 w-2 rounded-full bg-rose-500" />{{ $t('attendance.lbl_legend_late') }}</span>
          <span class="flex items-center gap-1"><span class="h-2 w-2 rounded-full bg-amber-500" />{{ $t('attendance.lbl_legend_missing') }}</span>
          <span class="flex items-center gap-1"><span class="h-2 w-2 rounded-full bg-blue-500" />{{ $t('attendance.lbl_on_leave') }}</span>
          <span class="flex items-center gap-1"><span class="h-1.5 w-1.5 rounded-full bg-pr" />{{ $t('attendance.lbl_legend_manual') }}</span>
        </div>
        <PaginationFooter :pagination="pagination" />
      </div>
    </div>

    <AttendanceEditDialog :target="editing" :can-edit-all="!!data?.canEditAll" @close="editing = null" />
    <AttendanceImportDialog v-model:open="importOpen" />
  </PageLayout>
</template>
