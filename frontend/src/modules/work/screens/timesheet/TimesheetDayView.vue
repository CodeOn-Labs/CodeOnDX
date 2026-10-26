<script setup lang="ts">
// Port TimesheetDayPage.tsx — Công số theo ngày: thanh phân bổ 24h, mỗi dòng kéo được (bội số 15 phút), thêm hạng mục tự do,
// sao chép ngày trước (≤ 7 ngày làm việc lùi lại), xuất file, lưu; panel phải: phân bổ hôm nay, Insight, vòng giờ trong tuần.
// Khác bản gốc: dòng "RON" thay bằng dòng sinh từ đơn nghỉ/OT đã duyệt (chỉ đọc); hạng mục tự do chọn theo Loại công việc
// (danh mục task_type) + mô tả thay vì gõ tên tự do; khoá ngày do BE tính; bỏ nút "Update RON" (D4).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Check, ChevronLeft, ChevronRight, Clock, Copy, Lock, Plus, RefreshCw } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import DynamicInsights from '@/modules/ai/components/DynamicInsights.vue';
import Button from '@/shared/components/Button.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import ExportButton from '@/shared/components/export/ExportButton.vue';
import type { ExportColumn } from '@/shared/components/export/types';
import TimesheetModeToggle from './TimesheetModeToggle.vue';
import TimesheetPeopleBar from './TimesheetPeopleBar.vue';
import TimesheetGroupReport from './TimesheetGroupReport.vue';
import { workApi, type TimesheetEntry, type TimesheetPeople } from '@/modules/work/api/work.api';
import {
  addDaysIso, buildRows, findPreviousDayWithData, formatHours, mondayOf, rowColor, rowKeyOf, snapToQuarterHour, toIso, type GroupSelection, type TimesheetRowKey,
} from '@/modules/work/utils/timesheet-rules';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ people: TimesheetPeople | null }>();
const employeeId = defineModel<string | null>('employeeId', { required: true });
const group = defineModel<GroupSelection | null>('group', { required: true });
const emit = defineEmits<{ switch: [] }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const date = ref(toIso(new Date()));
const isToday = computed(() => date.value === toIso(new Date()));
const query = useQuery({
  queryKey: computed(() => ['timesheets', 'period', employeeId.value, date.value, date.value] as const),
  queryFn: ({ signal }) => workApi.timesheets(employeeId.value, date.value, date.value, signal),
  enabled: computed(() => !group.value),
});
const period = computed(() => query.data.value);
const rules = computed(() => period.value?.rules ?? { minHours: 0, maxHours: 24, weeklyMaxHours: null, fteHours: 8 });
const day = computed(() => period.value?.days[0]);
const noEmployee = computed(() => props.people !== null && !props.people.myEmployeeId && !employeeId.value);

// ─── Trạng thái chỉnh sửa ───────────────────────────────────────────────────
const extraRows = ref<TimesheetRowKey[]>([]);
const hours = ref<Record<string, number>>({});
const original = ref<Record<string, number>>({});
const copied = ref(false);

function reload() {
  const p = period.value;
  if (!p) return;
  const map: Record<string, number> = {};
  for (const e of p.entries) map[rowKeyOf(e)] = (map[rowKeyOf(e)] ?? 0) + e.hours;
  hours.value = { ...map };
  original.value = { ...map };
  extraRows.value = [];
  copied.value = false;
}
watch(period, reload, { immediate: true });

const rows = computed(() => (period.value ? buildRows(period.value, extraRows.value) : []));
const total = computed(() => rows.value.reduce((s, r) => s + (hours.value[r.key] ?? 0), 0));
const dirty = computed(() => {
  const keys = new Set([...Object.keys(hours.value), ...Object.keys(original.value)]);
  return [...keys].some((k) => Math.abs((hours.value[k] ?? 0) - (original.value[k] ?? 0)) > 0.001) || extraRows.value.some((r) => (hours.value[r.key] ?? 0) === 0);
});
const hasGenerated = computed(() => rows.value.some((r) => r.generated));
const canModify = computed(() => !!period.value?.canEdit && !!day.value && !day.value.locked && !day.value.approved);

const totalTone = computed(() => (total.value > rules.value.maxHours ? 'text-rd' : total.value < rules.value.minHours ? 'text-[#f59e0b]' : 'text-[#6E62E5]'));
const lockMessage = computed(() => {
  const d = day.value;
  if (!d) return null;
  if (d.approved) return t('timesheet.lbl_day_approved');
  if (!d.locked) return null;
  if (d.lockReason === 'edit_limit') return t('timesheet.lbl_lock_edit_limit', { days: d.lockArgs.days ?? 0 });
  if (d.lockReason === 'month_locked') return t('timesheet.lbl_lock_month', { day: d.lockArgs.day ?? 0 });
  return t('timesheet.lbl_lock_future');
});

// ─── Điều hướng ngày (hỏi lại khi còn thay đổi chưa lưu) ───────────────────
async function confirmLeave() {
  if (!dirty.value) return true;
  return confirm.ask({ title: t('timesheet.confirm_unsaved_title'), description: t('timesheet.confirm_unsaved_desc'), confirmText: t('timesheet.btn_continue'), type: 'warning' });
}
async function goTo(next: string | null) {
  if (!next || next === date.value) return;
  if (await confirmLeave()) date.value = next;
}
async function refresh() {
  if (await confirmLeave()) {
    await query.refetch();
    reload();
  }
}
async function changeEmployee(id: string) {
  if (await confirmLeave()) {
    employeeId.value = id === props.people?.myEmployeeId ? null : id;
    group.value = null;
  }
}

// ─── Kéo thanh giờ (0–24h, bội số 15 phút) ─────────────────────────────────
let drag: { key: string; startX: number; startHours: number; width: number } | null = null;
function startDrag(event: MouseEvent, row: TimesheetRowKey) {
  if (!canModify.value || row.generated) return;
  const container = (event.currentTarget as HTMLElement).closest('[data-bar]') as HTMLElement | null;
  if (!container) return;
  event.preventDefault();
  drag = { key: row.key, startX: event.clientX, startHours: hours.value[row.key] ?? 0, width: container.offsetWidth };
  document.addEventListener('mousemove', onDrag);
  document.addEventListener('mouseup', endDrag);
}
function onDrag(event: MouseEvent) {
  if (!drag) return;
  const next = Math.max(0, Math.min(24, snapToQuarterHour(drag.startHours + ((event.clientX - drag.startX) / drag.width) * 24)));
  if (hours.value[drag.key] !== next) hours.value = { ...hours.value, [drag.key]: next };
}
function endDrag() {
  drag = null;
  document.removeEventListener('mousemove', onDrag);
  document.removeEventListener('mouseup', endDrag);
}
/** Bàn phím: ← / → đổi 15 phút (thay cho kéo chuột). */
function nudge(row: TimesheetRowKey, delta: number) {
  if (!canModify.value || row.generated) return;
  hours.value = { ...hours.value, [row.key]: Math.max(0, Math.min(24, (hours.value[row.key] ?? 0) + delta)) };
}

// ─── Thêm / xoá hạng mục tự do ──────────────────────────────────────────────
const showAdd = ref(false);
const newTaskTypeId = ref<string | null>(null);
const taskOptions = computed(() => (period.value?.taskTypes ?? []).map((tt) => ({ value: tt.id, label: tt.name })));
function addRow() {
  const tt = period.value?.taskTypes.find((x) => x.id === newTaskTypeId.value);
  if (!tt) return;
  const key = `t:${tt.id}`;
  if (rows.value.some((r) => r.key === key)) {
    toast.error(t('timesheet.err_row_exists'));
    return;
  }
  extraRows.value = [...extraRows.value, { key, label: tt.name, projectId: null, taskTypeId: tt.id, generated: null }];
  newTaskTypeId.value = null;
  showAdd.value = false;
}
async function removeRow(row: TimesheetRowKey) {
  const ok = await confirm.ask({ title: t('timesheet.confirm_remove_row_title'), description: t('timesheet.confirm_remove_row_desc', { name: row.label }), confirmText: t('timesheet.btn_remove'), type: 'danger' });
  if (!ok) return;
  extraRows.value = extraRows.value.filter((r) => r.key !== row.key);
  const next = { ...hours.value };
  delete next[row.key];
  hours.value = next;
}

// ─── Sao chép ngày trước ────────────────────────────────────────────────────
const copying = ref(false);
/** 7 ngày làm việc lùi lại rơi xa nhất vào 11 ngày lịch (khi ngày đang xem là Thứ 2). */
const COPY_LOOKBACK_DAYS = 11;
async function copyPrevDay() {
  if (!canModify.value || hasGenerated.value || copying.value) return;
  copying.value = true;
  try {
    const win = await workApi.timesheets(employeeId.value, addDaysIso(date.value, -COPY_LOOKBACK_DAYS), addDaysIso(date.value, -1));
    const match = findPreviousDayWithData(win.entries, date.value);
    if (!match) {
      toast.error(t('timesheet.err_no_prev_day'));
      return;
    }
    const label = formatDate(match.date);
    const ok = await confirm.ask({ title: t('timesheet.confirm_copy_day_title'), description: t('timesheet.confirm_copy_day_desc', { date: label }), confirmText: t('timesheet.btn_copy_now'), type: 'warning' });
    if (!ok) return;
    const next: Record<string, number> = {};
    const extra: TimesheetRowKey[] = [];
    for (const e of match.rows as TimesheetEntry[]) {
      // Dự án không còn phân công ngày này thì bỏ qua (BE cũng chặn khi lưu)
      if (e.projectId && !period.value?.projects.some((p) => p.id === e.projectId && p.ranges.some((r) => r.start <= date.value && r.end >= date.value))) continue;
      const key = rowKeyOf(e);
      next[key] = (next[key] ?? 0) + e.hours;
      if (!rows.value.some((r) => r.key === key)) extra.push({ key, label: e.projectName ?? e.taskType?.name ?? '—', projectId: e.projectId, taskTypeId: e.projectId ? null : (e.taskType?.id ?? null), generated: null });
    }
    hours.value = next;
    extraRows.value = extra;
    copied.value = true;
    toast.success(t('timesheet.msg_copied_day', { date: label }));
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    copying.value = false;
  }
}

// ─── Lưu ────────────────────────────────────────────────────────────────────
const saving = ref(false);
async function save() {
  if (!canModify.value || !dirty.value) return;
  const weekRest = Object.entries(period.value?.week.hoursByDate ?? {}).filter(([d]) => d !== date.value).reduce((s, [, h]) => s + h, 0);
  if (rules.value.weeklyMaxHours !== null && weekRest + total.value > rules.value.weeklyMaxHours) {
    toast.error(t('timesheet.err_weekly_max', { hours: rules.value.weeklyMaxHours, total: formatHours(weekRest + total.value) }));
    return;
  }
  if (total.value > rules.value.maxHours) {
    toast.error(t('timesheet.err_max_hours', { hours: rules.value.maxHours }));
    return;
  }
  saving.value = true;
  try {
    const entries = rows.value
      .filter((r) => !r.generated && (hours.value[r.key] ?? 0) > 0)
      .map((r) => ({ date: date.value, projectId: r.projectId, taskTypeId: r.taskTypeId, hours: hours.value[r.key] ?? 0 }));
    const res = await workApi.saveTimesheets(employeeId.value, [date.value], entries);
    toast.success(res.messageText);
    await queryClient.invalidateQueries({ queryKey: ['timesheets'] });
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    saving.value = false;
  }
}

// ─── Xuất file ──────────────────────────────────────────────────────────────
interface ExportRow { date: string; name: string; category: string; hours: number; status: string }
const exportColumns = computed<ExportColumn<ExportRow>[]>(() => [
  { id: 'date', header: t('timesheet.col_export_date'), accessorFn: (r) => formatDate(r.date), width: 15 },
  { id: 'name', header: t('timesheet.col_export_project'), accessorKey: 'name', width: 30 },
  { id: 'category', header: t('timesheet.col_export_category'), accessorKey: 'category', width: 20 },
  { id: 'hours', header: t('timesheet.col_export_hours'), accessorFn: (r) => `${r.hours}h`, width: 12 },
  { id: 'status', header: t('timesheet.col_status'), accessorKey: 'status', width: 18 },
]);
const statusText = (s: string) => ({ draft: t('timesheet.lbl_status_draft'), approved: t('timesheet.lbl_status_approved'), rejected: t('timesheet.lbl_status_rejected') })[s] ?? s;
async function exportData(): Promise<ExportRow[]> {
  return (period.value?.entries ?? []).filter((e) => e.hours > 0).map((e) => ({ date: e.date, name: e.projectName ?? e.taskType?.name ?? '—', category: e.taskType?.name ?? '', hours: e.hours, status: statusText(e.status) }));
}

// ─── Panel tuần ─────────────────────────────────────────────────────────────
const DOW = computed(() => [t('timesheet.lbl_dow_mon'), t('timesheet.lbl_dow_tue'), t('timesheet.lbl_dow_wed'), t('timesheet.lbl_dow_thu'), t('timesheet.lbl_dow_fri'), t('timesheet.lbl_dow_sat'), t('timesheet.lbl_dow_sun')]);
const week = computed(() => {
  const start = period.value?.week.start ?? mondayOf(date.value);
  const todayIso = toIso(new Date());
  return Array.from({ length: 7 }, (_, i) => {
    const d = addDaysIso(start, i);
    const h = d === date.value ? total.value : (period.value?.week.hoursByDate[d] ?? 0);
    return { date: d, label: DOW.value[i], hours: h, isToday: d === todayIso, future: d > todayIso, weekend: i >= 5 };
  });
});
const RADIUS = 11.5;
const CIRC = 2 * Math.PI * RADIUS;
const positiveRows = computed(() => rows.value.filter((r) => (hours.value[r.key] ?? 0) > 0));
const groupDates = computed(() => [date.value]);
</script>

<template>
  <PageLayout layout="A" content-padding="24px" insights-width="300px" disable-scroll>
    <template #insights>
      <div class="flex flex-col gap-6">
        <div class="pb-6 border-b border-[#D8DDF0]">
          <h3 class="text-[14px] font-black text-slate-800 tracking-tight mb-5">{{ $t('timesheet.title_allocation_today') }}</h3>
          <div class="flex w-full h-3 rounded-full bg-slate-100 border border-slate-200/40 p-[1.5px] shadow-inner gap-[1.5px]">
            <div
              v-for="(r, i) in positiveRows"
              :key="r.key"
              :class="cn('h-full transition-all duration-300', i === 0 && 'rounded-l-full', i === positiveRows.length - 1 && 'rounded-r-full')"
              :style="{ width: `${((hours[r.key] ?? 0) / Math.max(total, 1)) * 100}%`, backgroundColor: rowColor(r) }"
              :title="`${r.label}: ${formatHours(hours[r.key] ?? 0)}h`"
            />
          </div>
          <div class="mt-6 flex flex-col gap-3">
            <div v-for="r in rows" :key="r.key" class="flex items-center gap-3 cursor-default">
              <span class="w-2 h-2 rounded-full flex-shrink-0" :style="{ backgroundColor: rowColor(r) }" />
              <span class="truncate text-[13px] text-[#4C5568]">{{ r.label }}</span>
              <span :class="cn('ml-auto font-bold text-[13px]', (hours[r.key] ?? 0) > 0 ? 'text-[#8994AD]' : 'text-[#A0ABC0]')">{{ Math.round(((hours[r.key] ?? 0) / Math.max(total, 1)) * 100) }}%</span>
            </div>
          </div>
        </div>
        <div class="pb-6 border-b border-[#D8DDF0]">
          <h3 class="text-[14px] font-black text-slate-800 tracking-tight mb-4">{{ $t('timesheet.title_insights') }}</h3>
          <DynamicInsights screen="timesheet_day" />
        </div>
        <div>
          <h3 class="text-[14px] font-black text-slate-800 tracking-tight mb-4">{{ $t('timesheet.title_this_week') }}</h3>
          <div class="flex items-center justify-between gap-1 mt-4 px-1">
            <div
              v-for="d in week"
              :key="d.date"
              class="flex flex-col items-center gap-1.5 flex-1 cursor-default"
              :title="d.future ? '' : `${formatHours(d.hours)}h / ${rules.fteHours}h`"
            >
              <div class="relative w-[30px] h-[30px] flex items-center justify-center">
                <svg class="w-full h-full -rotate-90 drop-shadow-sm" aria-hidden="true">
                  <circle cx="15" cy="15" :r="RADIUS" stroke-width="3" class="stroke-[#E9EDF6]" fill="transparent" />
                  <circle
                    v-if="!d.future && d.hours > 0"
                    cx="15"
                    cy="15"
                    :r="RADIUS"
                    stroke-width="3"
                    :class="cn('transition-all duration-1000 ease-out', d.hours > rules.maxHours ? 'stroke-[#ef4444]' : d.hours < rules.minHours ? 'stroke-[#f59e0b]' : d.isToday ? 'stroke-[#7c3aed]' : 'stroke-[#10b981]')"
                    fill="transparent"
                    :stroke-dasharray="CIRC"
                    :stroke-dashoffset="CIRC - (Math.min((d.hours / rules.fteHours) * 100, 100) / 100) * CIRC"
                    stroke-linecap="round"
                  />
                </svg>
                <div :class="cn('absolute inset-0 flex items-center justify-center text-[9.5px] font-bold', d.isToday ? 'text-[#7c3aed]' : d.hours > 0 && !d.future ? 'text-[#2D3448]' : 'text-[#A0ABC0]')">
                  {{ d.future ? '--' : d.hours > 0 ? formatHours(d.hours) : '' }}
                </div>
              </div>
              <span :class="cn('text-[10px] font-bold tracking-wide mt-1', d.isToday ? 'text-[#7c3aed] bg-[#7c3aed]/10 px-1.5 py-0.5 rounded-md' : d.weekend ? 'text-[#A0ABC0]' : 'text-[#68738D]')">{{ d.label }}</span>
            </div>
          </div>
        </div>
      </div>
    </template>

    <div class="flex h-full flex-col gap-6 animate-in fade-in duration-300">
      <div class="flex-none">
        <div class="flex flex-wrap items-start justify-between gap-4 mb-3 border-b border-slate-100/50 pb-3">
          <div class="flex flex-col gap-1">
            <div class="flex items-center gap-2">
              <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
              <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ $t('timesheet.lbl_kicker') }}</span>
            </div>
            <div class="flex items-baseline gap-3 min-h-[32px] flex-wrap">
              <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent m-0 p-0">{{ $t('timesheet.title_day') }}</h1>
              <div v-if="query.isPending.value && !group" class="h-7 w-20 bg-slate-100 rounded-lg animate-pulse" />
              <template v-else-if="!group">
                <div :class="cn('text-[22px] font-black tracking-tight', totalTone)">
                  {{ formatHours(total) }} <span class="text-[16px] text-slate-500">h / {{ rules.fteHours }}h</span>
                </div>
                <span v-if="total < rules.minHours" class="text-[11px] font-bold text-amber-500 bg-amber-50 border border-amber-200/50 rounded-lg px-2 py-0.5">{{ $t('timesheet.lbl_min_hours_warning', { hours: rules.minHours }) }}</span>
                <span v-if="total > rules.maxHours" class="text-[11px] font-bold text-red-500 bg-red-50 border border-red-200/50 rounded-lg px-2 py-0.5">{{ $t('timesheet.lbl_max_hours_warning', { hours: rules.maxHours }) }}</span>
              </template>
            </div>
          </div>

          <div class="flex flex-wrap items-center gap-3">
            <button
              type="button"
              :disabled="query.isFetching.value"
              class="flex h-9 lg:h-10 w-9 lg:w-10 items-center justify-center rounded-full bg-white/80 shadow-sm border border-[#E4E8F3] text-[#8994AD] transition-all hover:bg-white hover:text-pr"
              :title="$t('timesheet.btn_refresh')"
              :aria-label="$t('timesheet.btn_refresh')"
              @click="refresh"
            >
              <RefreshCw :class="cn('h-4 w-4', query.isFetching.value && 'animate-spin text-pr')" />
            </button>
            <TimesheetModeToggle mode="day" @switch="emit('switch')" />
            <div class="flex items-center gap-2 text-[12px] font-semibold text-[#6E7891]">
              <button type="button" class="flex h-8 lg:h-9 w-8 lg:w-9 items-center justify-center rounded-full text-[#8994AD] hover:bg-white hover:text-[#4C5568]" :aria-label="$t('timesheet.btn_prev_day')" @click="goTo(addDaysIso(date, -1))">
                <ChevronLeft class="h-4 w-4" />
              </button>
              <FormDatePicker :model-value="date" :allow-clear="false" hide-message class="h-9 lg:h-10 w-[200px] rounded-xl border-[#E4E8F3] bg-white text-[12px] lg:text-[13px] font-semibold text-[#4C5568] shadow-sm" @update:model-value="goTo($event)" />
              <button type="button" class="flex h-8 lg:h-9 w-8 lg:w-9 items-center justify-center rounded-full text-[#8994AD] hover:bg-white hover:text-[#4C5568]" :aria-label="$t('timesheet.btn_next_day')" @click="goTo(addDaysIso(date, 1))">
                <ChevronRight class="h-4 w-4" />
              </button>
            </div>
            <Button variant="outline" class="h-9 lg:h-10 rounded-xl px-4 text-[13px] font-bold text-[#4C5568] hover:text-pr hover:border-pr/50 shadow-sm" @click="goTo(toIso(new Date()))">
              {{ $t('timesheet.btn_today') }}
            </Button>
          </div>
        </div>

        <div v-if="noEmployee" class="flex items-center gap-3 py-3 px-4 mt-4 bg-amber-50 border border-amber-200 rounded-2xl">
          <div class="w-8 h-8 rounded-full bg-amber-100 text-amber-600 flex items-center justify-center text-[12px] font-bold shrink-0">!</div>
          <div class="flex-1 text-[12px] text-amber-700 leading-relaxed font-medium">{{ $t('timesheet.lbl_no_employee') }}</div>
        </div>
        <div v-else-if="lockMessage && !group" class="flex items-center gap-3 py-3 px-4 mt-4 bg-red-50 border border-red-200 rounded-2xl animate-in fade-in slide-in-from-top-4 duration-300">
          <div class="w-8 h-8 rounded-full bg-red-100 text-red-600 flex items-center justify-center shrink-0"><Lock class="w-3.5 h-3.5" /></div>
          <div class="flex-1 text-[12px] text-red-700 leading-relaxed font-medium">{{ lockMessage }}</div>
        </div>

        <TimesheetPeopleBar
          v-if="props.people?.canViewOthers"
          :people="props.people"
          :employee-id="employeeId"
          :group="group"
          @change-employee="changeEmployee"
          @apply-group="group = $event"
          @clear-group="group = null"
        />
      </div>

      <TimesheetGroupReport v-if="group" :group="group" :dates="groupDates" :can-approve="!!props.people?.canApprove" />
      <template v-else>
        <div :class="cn('flex h-3 w-full rounded-full bg-slate-100 border border-slate-200/40 p-[1.5px] shadow-inner gap-[1.5px]', query.isPending.value && 'animate-pulse bg-slate-200')">
          <div
            v-for="(r, i) in positiveRows"
            :key="r.key"
            :class="cn('h-full transition-all duration-300', i === 0 && 'rounded-l-full', i === positiveRows.length - 1 && 'rounded-r-full')"
            :style="{ width: `${((hours[r.key] ?? 0) / 24) * 100}%`, backgroundColor: rowColor(r) }"
            :title="`${r.label}: ${formatHours(hours[r.key] ?? 0)}h (${Math.round(((hours[r.key] ?? 0) / 24) * 100)}%)`"
          />
        </div>

        <div class="relative min-h-0 flex-1 flex flex-col overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-2xl shadow-slate-200/50">
          <div class="flex-none px-5 pt-4 pb-2 border-b border-slate-100 bg-white" style="scrollbar-gutter: stable">
            <div class="flex items-center gap-3">
              <div class="w-[156px] shrink-0" />
              <div class="flex-1 relative h-5 overflow-hidden">
                <span
                  v-for="h in [0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24]"
                  :key="h"
                  class="absolute top-0 text-[11.5px] font-bold text-[#98A2B8] tracking-wider"
                  :style="{ left: `${(h / 24) * 100}%`, transform: h === 0 ? 'none' : h === 24 ? 'translateX(-100%)' : 'translateX(-50%)' }"
                >{{ h }}</span>
              </div>
              <div class="w-[48px] shrink-0" />
              <div v-if="canModify" class="w-8 shrink-0" />
            </div>
          </div>

          <div class="flex-1 overflow-y-auto px-5 py-4 space-y-2 min-h-0" style="scrollbar-gutter: stable">
            <template v-if="query.isPending.value">
              <div v-for="i in 6" :key="i" class="flex items-center gap-3 animate-pulse">
                <div class="w-[156px] h-4 bg-slate-100 rounded" />
                <div class="flex-1 h-3.5 bg-slate-50 rounded-full" />
                <div class="w-[48px] h-4 bg-slate-100 rounded" />
              </div>
            </template>
            <div v-for="r in rows" v-else :key="r.key" class="group flex items-center gap-3 h-8 py-2 transition-all duration-300 hover:bg-slate-50/50 rounded-xl px-2 -mx-2 select-none">
              <div class="flex w-[156px] shrink-0 items-center gap-3">
                <span class="h-6 w-[3px] rounded-full" :style="{ backgroundColor: rowColor(r), opacity: (hours[r.key] ?? 0) > 0 ? 1 : 0.55 }" />
                <div
                  class="truncate py-1 text-[13.5px] font-semibold text-right"
                  :style="{ color: r.generated ? rowColor(r) : (hours[r.key] ?? 0) > 0 ? '#1F2937' : '#94A3B8' }"
                  :title="r.generated ? $t('timesheet.tip_generated_readonly') : r.label"
                >
                  {{ r.label }}<template v-if="r.generated"> ({{ r.generated === 'leave' ? $t('timesheet.lbl_generated_leave') : $t('timesheet.lbl_generated_ot') }})</template>
                </div>
              </div>
              <div data-bar class="flex-1 h-3.5 bg-bg2/40 rounded-full relative shadow-inner">
                <div :style="{ width: `${((hours[r.key] ?? 0) / 24) * 100}%`, backgroundColor: `${rowColor(r)}55` }" class="h-full rounded-full relative transition-all duration-75">
                  <div
                    role="slider"
                    :tabindex="canModify && !r.generated ? 0 : -1"
                    :aria-label="r.label"
                    :aria-valuenow="hours[r.key] ?? 0"
                    aria-valuemin="0"
                    aria-valuemax="24"
                    :style="{ borderColor: rowColor(r), boxShadow: `0 0 10px ${rowColor(r)}33` }"
                    :class="cn('w-5 h-5 rounded-full bg-white border-[3.5px] absolute -right-2.5 -top-[3.5px] z-10 flex items-center justify-center transition-all outline-none focus-visible:ring-2 focus-visible:ring-pr/40', canModify && !r.generated ? 'cursor-ew-resize hover:scale-125' : 'opacity-0')"
                    @mousedown="startDrag($event, r)"
                    @keydown.right.prevent="nudge(r, 0.25)"
                    @keydown.left.prevent="nudge(r, -0.25)"
                  />
                </div>
              </div>
              <div :class="cn('w-[48px] shrink-0 text-right text-[14.5px] font-black', (hours[r.key] ?? 0) > rules.maxHours && 'text-[#ef4444]')" :style="(hours[r.key] ?? 0) > rules.maxHours ? undefined : { color: rowColor(r), opacity: (hours[r.key] ?? 0) > 0 ? 1 : 0.5 }">
                {{ formatHours(hours[r.key] ?? 0) }}<span class="ml-0.5 text-[11px] font-normal opacity-60">h</span>
              </div>
              <template v-if="canModify">
                <div v-if="r.projectId || r.generated" class="h-8 w-8 shrink-0" />
                <button v-else type="button" class="flex h-8 w-8 items-center justify-center rounded-lg text-[18px] text-t3 transition-all hover:bg-rdbg hover:text-rd" :title="$t('timesheet.btn_remove')" @click="removeRow(r)">×</button>
              </template>
            </div>
            <div v-if="!query.isPending.value && rows.length === 0" class="flex flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-slate-200 bg-slate-50/50 py-8 text-slate-400">
              <Clock class="w-8 h-8 text-slate-300 mb-1" />
              <div class="text-sm font-medium text-slate-500">{{ $t('timesheet.empty_no_data') }}</div>
              <div class="text-[11px] text-slate-400">{{ $t('timesheet.hint_day_empty', { date: isToday ? $t('timesheet.btn_today') : formatDate(date) }) }}</div>
            </div>
          </div>

          <div v-if="canModify" class="flex-none px-5 pb-5 pt-3 border-t border-slate-100 bg-white">
            <div v-if="showAdd" class="relative z-10 flex flex-col gap-4 py-2 animate-in fade-in duration-300">
              <div class="flex flex-col gap-2">
                <span class="text-[12px] font-bold text-slate-500">{{ $t('timesheet.lbl_free_category') }}</span>
                <div class="flex gap-2">
                  <div class="flex-1">
                    <FormSelect v-model="newTaskTypeId" :options="taskOptions" :placeholder="$t('timesheet.ph_free_category')" hide-message trigger-class="h-9 lg:h-10 text-[13px]" />
                  </div>
                  <Button variant="primary" class="h-9 lg:h-10 text-[13px] font-bold shadow-md shadow-pr/20 rounded-xl px-5" :disabled="!newTaskTypeId" @click="addRow">{{ $t('timesheet.btn_add') }}</Button>
                </div>
              </div>
              <div class="flex justify-end pt-3 border-t border-slate-100">
                <Button variant="danger" size="sm" class="h-8 rounded-xl px-4 text-[12px] font-bold shadow-sm" @click="showAdd = false">{{ $t('timesheet.btn_close') }}</Button>
              </div>
            </div>
            <button
              v-else
              type="button"
              class="w-full h-10 rounded-xl border border-dashed border-[#CBD5E1] hover:border-pr hover:bg-slate-50/50 text-[13px] font-bold text-[#6E7891] hover:text-pr flex items-center justify-center gap-1.5 transition-all duration-300 shadow-sm"
              @click="showAdd = true"
            >
              <Plus class="w-3.5 h-3.5" />
              <span>{{ $t('timesheet.btn_add_free_category') }}</span>
            </button>
          </div>
          <div v-if="query.isFetching.value && !query.isPending.value" class="absolute inset-0 z-50 flex items-center justify-center bg-white/20 backdrop-blur-[1px]">
            <div class="flex flex-col items-center gap-2 rounded-2xl bg-white/80 p-4 shadow-xl border border-white/40">
              <div class="w-5 h-5 border-2 border-pr border-t-transparent rounded-full animate-spin" />
              <span class="text-[10px] font-bold text-slate-500 uppercase tracking-tighter">{{ $t('timesheet.lbl_updating') }}</span>
            </div>
          </div>
        </div>

        <div class="flex-none pt-4 flex items-center justify-between gap-3">
          <Button variant="primary" class="h-9 lg:h-10 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" :loading="copying" :disabled="!canModify || noEmployee || query.isFetching.value || hasGenerated" @click="copyPrevDay">
            <template #leftIcon><Copy class="w-4 h-4" /></template>
            {{ $t('timesheet.btn_copy_prev_day') }}
          </Button>
          <div class="flex items-center gap-3">
            <ExportButton
              :columns="exportColumns"
              :data-fn="exportData"
              :default-filename="`timesheet_${date}`"
              :label="$t('timesheet.btn_export_day')"
              hide-scope
              class="h-9 lg:h-10 rounded-xl px-4 text-[13px] font-bold text-[#4C5568] hover:text-pr hover:border-pr/50 hover:bg-slate-50 border border-[#E4E8F3] bg-white shadow-sm shrink-0"
            />
            <Button
              variant="primary"
              class="h-9 lg:h-10 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25"
              :loading="saving"
              :disabled="!canModify || !dirty || noEmployee"
              :title="!canModify ? $t('timesheet.tip_no_edit_permission') : !dirty ? $t('timesheet.tip_no_changes') : undefined"
              @click="save"
            >
              <template #leftIcon><Check class="w-4 h-4" /></template>
              {{ $t('timesheet.btn_save') }}
            </Button>
          </div>
        </div>
      </template>
    </div>
  </PageLayout>
</template>
