<script setup lang="ts">
// Port TimesheetWeekPage.tsx — Công số theo tuần: lưới dòng × ngày nhập số giờ, hàng Tổng, sao chép tuần trước, xuất file, lưu;
// panel phải: tổng quan tuần, phân bổ tuần, Insight.
// Khác bản gốc: chọn tuần bằng một ngày bất kỳ (tự lấy Thứ 2 → Chủ nhật) thay cho chọn khoảng ngày tự do;
// dòng nghỉ/OT sinh từ đơn đã duyệt thay dòng RON (chỉ đọc); khoá ngày do BE tính.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Check, ChevronLeft, ChevronRight, Copy, RefreshCw } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import DynamicInsights from '@/modules/ai/components/DynamicInsights.vue';
import Button from '@/shared/components/Button.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import ExportButton from '@/shared/components/export/ExportButton.vue';
import type { ExportColumn } from '@/shared/components/export/types';
import TimesheetModeToggle from './TimesheetModeToggle.vue';
import TimesheetPeopleBar from './TimesheetPeopleBar.vue';
import TimesheetGroupReport from './TimesheetGroupReport.vue';
import { workApi, type TimesheetDay, type TimesheetPeople } from '@/modules/work/api/work.api';
import {
  addDaysIso, assignedOn, buildGrid, buildRows, daysBetween, formatHours, isHoursInputAllowed, mondayOf, round2, rowColor, rowKeyOf, toIso,
  type GroupSelection, type HoursGrid, type TimesheetRowKey,
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

const start = ref(mondayOf(toIso(new Date())));
const end = computed(() => addDaysIso(start.value, 6));
const days = computed(() => daysBetween(start.value, end.value));
const todayIso = toIso(new Date());

const query = useQuery({
  queryKey: computed(() => ['timesheets', 'period', employeeId.value, start.value, end.value] as const),
  queryFn: ({ signal }) => workApi.timesheets(employeeId.value, start.value, end.value, signal),
  enabled: computed(() => !group.value),
});
const period = computed(() => query.data.value);
const rules = computed(() => period.value?.rules ?? { minHours: 0, maxHours: 24, weeklyMaxHours: 60, fteHours: 8 });
const dayInfo = computed<Record<string, TimesheetDay>>(() => Object.fromEntries((period.value?.days ?? []).map((d) => [d.date, d])));
const noEmployee = computed(() => props.people !== null && !props.people.myEmployeeId && !employeeId.value);

// ─── Lưới ───────────────────────────────────────────────────────────────────
const grid = ref<HoursGrid>({});
const original = ref<HoursGrid>({});
/** Chuỗi đang gõ của từng ô (chưa rời ô) — "rowKey|date" → text */
const display = ref<Record<string, string>>({});
const extraRows = ref<TimesheetRowKey[]>([]);
const flash = ref(false);

function reload() {
  const p = period.value;
  if (!p) return;
  grid.value = buildGrid(p.entries);
  original.value = buildGrid(p.entries);
  display.value = {};
  extraRows.value = [];
}
watch(period, reload, { immediate: true });

const rows = computed(() => (period.value ? buildRows(period.value, extraRows.value) : []));
const cellKey = (row: string, d: string) => `${row}|${d}`;
/** Giá trị hiện tại của ô (ưu tiên chuỗi đang gõ). */
function valueOf(row: string, d: string): number {
  const text = display.value[cellKey(row, d)];
  if (text !== undefined) {
    const n = text && text !== '.' ? parseFloat(text) : 0;
    return Number.isNaN(n) ? (grid.value[row]?.[d] ?? 0) : n;
  }
  return grid.value[row]?.[d] ?? 0;
}
const dayTotal = (d: string) => round2(rows.value.reduce((s, r) => s + valueOf(r.key, d), 0));
const rowTotal = (row: string) => round2(days.value.reduce((s, d) => s + valueOf(row, d), 0));
const weekTotal = computed(() => round2(rows.value.reduce((s, r) => s + rowTotal(r.key), 0)));
const targetWeekHours = computed(() => days.value.filter((d) => !dayInfo.value[d]?.weekend).length * rules.value.fteHours);
const dirty = computed(() =>
  rows.value.some((r) => days.value.some((d) => Math.abs(valueOf(r.key, d) - (original.value[r.key]?.[d] ?? 0)) > 0.001)),
);
const editableDay = (d: string) => !!period.value?.canEdit && !!dayInfo.value[d] && !dayInfo.value[d]!.locked && !dayInfo.value[d]!.approved;

function cellDisabled(row: TimesheetRowKey, d: string) {
  if (!editableDay(d) || row.generated) return true;
  return !!row.projectId && !!period.value && !assignedOn(period.value, row.projectId, d);
}
function cellTitle(row: TimesheetRowKey, d: string): string | undefined {
  const info = dayInfo.value[d];
  if (!info) return undefined;
  if (info.approved) return t('timesheet.lbl_day_approved');
  if (info.locked) {
    if (info.lockReason === 'edit_limit') return t('timesheet.lbl_lock_edit_limit', { days: info.lockArgs.days ?? 0 });
    if (info.lockReason === 'month_locked') return t('timesheet.lbl_lock_month', { day: info.lockArgs.day ?? 0 });
    return t('timesheet.lbl_lock_future');
  }
  if (!period.value?.canEdit) return t('timesheet.tip_no_edit_permission');
  if (row.generated) return t('timesheet.tip_generated_readonly');
  if (row.projectId && period.value && !assignedOn(period.value, row.projectId, d)) return t('timesheet.tip_no_assignment');
  return info.holiday ?? undefined;
}
function onInput(row: string, d: string, value: string) {
  if (!isHoursInputAllowed(value)) return;
  display.value = { ...display.value, [cellKey(row, d)]: value };
}
function onBlur(row: string, d: string) {
  const key = cellKey(row, d);
  if (!(key in display.value)) return;
  let text = display.value[key] ?? '';
  const nextDisplay = { ...display.value };
  delete nextDisplay[key];
  if (text.startsWith('.')) text = `0${text}`;
  const n = !text || text === '.' ? 0 : parseFloat(text);
  if (!Number.isNaN(n)) grid.value = { ...grid.value, [row]: { ...(grid.value[row] ?? {}), [d]: n } };
  display.value = nextDisplay;
}
const cellText = (row: string, d: string) => display.value[cellKey(row, d)] ?? ((grid.value[row]?.[d] ?? 0) > 0 ? String(grid.value[row]?.[d]) : '');

// ─── Điều hướng ─────────────────────────────────────────────────────────────
async function confirmLeave() {
  if (!dirty.value) return true;
  return confirm.ask({ title: t('timesheet.confirm_unsaved_title'), description: t('timesheet.confirm_unsaved_desc'), confirmText: t('timesheet.btn_continue'), type: 'warning' });
}
async function goToWeek(anyDay: string | null) {
  if (!anyDay) return;
  const monday = mondayOf(anyDay);
  if (monday !== start.value && (await confirmLeave())) start.value = monday;
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

// ─── Sao chép tuần trước ────────────────────────────────────────────────────
async function copyLastWeek() {
  const ok = await confirm.ask({ title: t('timesheet.confirm_copy_week_title'), description: t('timesheet.confirm_copy_week_desc'), confirmText: t('timesheet.btn_copy_now'), type: 'warning' });
  if (!ok) return;
  try {
    const last = await workApi.timesheets(employeeId.value, addDaysIso(start.value, -7), addDaysIso(start.value, -1));
    if (last.entries.length === 0) {
      toast.error(t('timesheet.err_no_last_week'));
      return;
    }
    // Ngày nguồn có dòng nghỉ/OT bị bỏ qua cả ngày; ngày đích khoá / đã duyệt / tương lai / có dòng nghỉ-OT giữ nguyên
    const sourceGenerated = new Set(last.entries.filter((e) => e.source !== 'manual').map((e) => e.date));
    const targetGenerated = new Set((period.value?.entries ?? []).filter((e) => e.source !== 'manual').map((e) => e.date));
    const targets = days.value.slice(0, 5).filter((d) => d <= todayIso && editableDay(d) && !targetGenerated.has(d) && !sourceGenerated.has(addDaysIso(d, -7)));
    if (targets.length === 0) {
      toast.error(t('timesheet.err_copy_no_target'));
      return;
    }
    const next: HoursGrid = JSON.parse(JSON.stringify(grid.value)) as HoursGrid;
    for (const r of Object.keys(next)) for (const d of targets) if (next[r]) delete next[r][d];
    const extra: TimesheetRowKey[] = [...extraRows.value];
    let copiedCells = 0;
    for (const e of last.entries) {
      if (e.source !== 'manual') continue;
      const target = addDaysIso(e.date, 7);
      if (!targets.includes(target)) continue;
      if (e.projectId && period.value && !assignedOn(period.value, e.projectId, target)) continue;
      const key = rowKeyOf(e);
      next[key] ??= {};
      next[key][target] = round2((next[key][target] ?? 0) + e.hours);
      copiedCells += 1;
      if (!rows.value.some((r) => r.key === key) && !extra.some((r) => r.key === key)) {
        extra.push({ key, label: e.projectName ?? e.taskType?.name ?? '—', projectId: e.projectId, taskTypeId: e.projectId ? null : (e.taskType?.id ?? null), generated: null });
      }
    }
    if (copiedCells === 0) {
      toast.error(t('timesheet.err_copy_no_data'));
      return;
    }
    grid.value = next;
    extraRows.value = extra;
    display.value = {};
    flash.value = true;
    setTimeout(() => (flash.value = false), 1000);
    toast.success(t('timesheet.msg_copied_week'));
  } catch (err) {
    toast.error((err as Error).message);
  }
}

// ─── Lưu ────────────────────────────────────────────────────────────────────
const saving = ref(false);
async function save() {
  for (const key of Object.keys(display.value)) {
    const [row, d] = key.split('|');
    if (row && d) onBlur(row, d);
  }
  if (rules.value.weeklyMaxHours !== null && weekTotal.value > rules.value.weeklyMaxHours) {
    toast.error(t('timesheet.err_weekly_max', { hours: rules.value.weeklyMaxHours, total: formatHours(weekTotal.value) }));
    return;
  }
  const over = days.value.filter((d) => dayTotal(d) > rules.value.maxHours);
  if (over.length) {
    toast.error(t('timesheet.err_days_over_max', { days: over.map((d) => formatDate(d, 'd/M')).join(', '), hours: rules.value.maxHours }));
    return;
  }
  const editable = days.value.filter(editableDay);
  if (editable.length === 0) {
    toast.error(t('timesheet.err_all_locked'));
    return;
  }
  saving.value = true;
  try {
    const entries = rows.value.filter((r) => !r.generated).flatMap((r) =>
      editable.filter((d) => valueOf(r.key, d) > 0).map((d) => ({ date: d, projectId: r.projectId, taskTypeId: r.taskTypeId, hours: valueOf(r.key, d) })),
    );
    const res = await workApi.saveTimesheets(employeeId.value, editable, entries);
    toast.success(res.messageText);
    await queryClient.invalidateQueries({ queryKey: ['timesheets'] });
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    saving.value = false;
  }
}

// ─── Xuất file / panel ──────────────────────────────────────────────────────
interface ExportRow { date: string; name: string; category: string; hours: number }
const exportColumns = computed<ExportColumn<ExportRow>[]>(() => [
  { id: 'date', header: t('timesheet.col_export_date'), accessorFn: (r) => formatDate(r.date), width: 15 },
  { id: 'name', header: t('timesheet.col_export_project'), accessorKey: 'name', width: 30 },
  { id: 'category', header: t('timesheet.col_export_category'), accessorKey: 'category', width: 20 },
  { id: 'hours', header: t('timesheet.col_export_hours'), accessorFn: (r) => `${r.hours}h`, width: 12 },
]);
async function exportData(): Promise<ExportRow[]> {
  return (period.value?.entries ?? []).filter((e) => e.hours > 0).sort((a, b) => a.date.localeCompare(b.date))
    .map((e) => ({ date: e.date, name: e.projectName ?? e.taskType?.name ?? '—', category: e.taskType?.name ?? '', hours: e.hours }));
}
const DOW = computed(() => [t('timesheet.lbl_dow_sun'), t('timesheet.lbl_dow_mon'), t('timesheet.lbl_dow_tue'), t('timesheet.lbl_dow_wed'), t('timesheet.lbl_dow_thu'), t('timesheet.lbl_dow_fri'), t('timesheet.lbl_dow_sat')]);
const dowOf = (d: string) => DOW.value[new Date(`${d}T00:00:00`).getDay()];
const distribution = computed(() => rows.value.map((r) => ({ row: r, hours: rowTotal(r.key), pct: weekTotal.value > 0 ? (rowTotal(r.key) / weekTotal.value) * 100 : 0 })));
const weekTone = computed(() => (rules.value.weeklyMaxHours !== null && weekTotal.value > rules.value.weeklyMaxHours ? 'text-[#ef4444]' : weekTotal.value >= targetWeekHours.value ? 'text-[#10b981]' : 'text-[#7c3aed]'));
</script>

<template>
  <PageLayout layout="A" content-padding="24px" insights-width="300px" disable-scroll>
    <template #insights>
      <div class="flex flex-col gap-6">
        <div class="pb-6 border-b border-[#D8DDF0]">
          <h3 class="text-[14px] font-black text-slate-800 tracking-tight mb-4">{{ $t('timesheet.title_week_overview') }}</h3>
          <div class="flex flex-col gap-1 mt-2 max-h-[300px] overflow-y-auto pr-1">
            <div v-for="d in days" :key="d" :class="cn('flex items-center gap-3 p-1.5 rounded-lg transition-colors hover:bg-[#F8FAFC]', d === todayIso && 'bg-[#7c3aed]/[0.03]')">
              <span :class="cn('w-6 text-[11px] font-bold text-center tracking-wide', d === todayIso ? 'text-[#7c3aed]' : dayInfo[d]?.weekend ? 'text-[#A0ABC0]' : 'text-[#68738D]')">{{ dowOf(d) }}</span>
              <div class="flex-1 h-[6px] bg-[#E9EDF6] rounded-full overflow-hidden relative">
                <div
                  v-if="dayTotal(d) > 0"
                  :style="{ width: `${Math.min((dayTotal(d) / rules.fteHours) * 100, 100)}%` }"
                  :class="cn('h-full rounded-full transition-all duration-500 ease-out', dayTotal(d) > rules.maxHours ? 'bg-[#ef4444]' : dayTotal(d) < rules.minHours ? 'bg-[#f59e0b]' : d === todayIso ? 'bg-[#7c3aed]' : 'bg-[#10b981]')"
                />
              </div>
              <span :class="cn('text-[11px] font-bold w-8 text-right', dayTotal(d) > 0 ? 'text-[#2D3448]' : 'text-[#A0ABC0]')">{{ dayTotal(d) > 0 ? formatHours(dayTotal(d)) : '--' }}</span>
            </div>
          </div>
        </div>
        <div class="pb-6 border-b border-[#D8DDF0]">
          <h3 class="text-[14px] font-black text-slate-800 tracking-tight mb-5">{{ $t('timesheet.title_week_allocation') }}</h3>
          <div class="w-full h-3 rounded-full bg-slate-100 border border-slate-200/40 p-[1.5px] shadow-inner gap-[1.5px] flex">
            <div
              v-for="(item, i) in distribution.filter((x) => x.pct > 0)"
              :key="item.row.key"
              :class="cn('h-full', i === 0 && 'rounded-l-full', i === distribution.filter((x) => x.pct > 0).length - 1 && 'rounded-r-full')"
              :style="{ width: `${item.pct}%`, backgroundColor: rowColor(item.row) }"
              :title="`${item.row.label}: ${formatHours(item.hours)}h`"
            />
          </div>
          <div class="flex flex-col gap-3 mt-6">
            <div v-for="item in distribution" :key="item.row.key" class="flex items-center gap-3 cursor-default">
              <span class="w-2 h-2 rounded-full flex-shrink-0" :style="{ backgroundColor: rowColor(item.row) }" />
              <span class="truncate text-[13px] text-[#4C5568]">{{ item.row.label }}</span>
              <span :class="cn('ml-auto font-bold text-[13px]', item.pct > 0 ? 'text-[#8994AD]' : 'text-[#A0ABC0]')">{{ Math.round(item.pct) }}%</span>
            </div>
          </div>
        </div>
        <div>
          <h3 class="text-[14px] font-black text-slate-800 tracking-tight mb-4">{{ $t('timesheet.title_insights') }}</h3>
          <DynamicInsights screen="timesheet_week" />
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
            <div class="flex items-baseline gap-3 min-h-[32px]">
              <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent m-0 p-0">{{ $t('timesheet.title_week') }}</h1>
              <div v-if="query.isPending.value && !group" class="h-7 w-32 bg-slate-100 rounded-lg animate-pulse" />
              <div v-else-if="!group" class="text-[22px] font-black tracking-tight text-slate-900">
                <span :class="weekTone">{{ weekTotal.toFixed(1) }}</span>
                <span class="text-[16px] text-slate-500"> h / {{ (rules.weeklyMaxHours ?? targetWeekHours).toFixed(0) }}h</span>
              </div>
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
            <TimesheetModeToggle mode="week" @switch="emit('switch')" />
            <div class="flex items-center gap-2 text-[12px] font-semibold text-[#6E7891]">
              <button type="button" class="flex h-8 lg:h-9 w-8 lg:w-9 items-center justify-center rounded-full text-[#8994AD] hover:bg-white hover:text-[#4C5568]" :aria-label="$t('timesheet.btn_prev_week')" @click="goToWeek(addDaysIso(start, -7))">
                <ChevronLeft class="h-4 w-4" />
              </button>
              <FormDatePicker :model-value="start" :allow-clear="false" hide-message class="h-9 lg:h-10 w-[200px] rounded-xl border-[#E4E8F3] bg-white text-[12px] font-semibold text-[#4C5568] shadow-sm" @update:model-value="goToWeek($event)" />
              <span class="text-[12px] font-bold text-slate-400 whitespace-nowrap">→ {{ formatDate(end) }}</span>
              <button type="button" class="flex h-8 lg:h-9 w-8 lg:w-9 items-center justify-center rounded-full text-[#8994AD] hover:bg-white hover:text-[#4C5568]" :aria-label="$t('timesheet.btn_next_week')" @click="goToWeek(addDaysIso(start, 7))">
                <ChevronRight class="h-4 w-4" />
              </button>
            </div>
            <Button variant="outline" class="h-9 lg:h-10 rounded-xl px-4 text-[13px] font-bold text-[#4C5568] hover:text-pr hover:border-pr/50 shadow-sm" @click="goToWeek(todayIso)">
              {{ $t('timesheet.btn_today') }}
            </Button>
          </div>
        </div>

        <div v-if="noEmployee" class="flex items-center gap-3 py-3 px-4 mt-4 bg-amber-50 border border-amber-200 rounded-2xl">
          <div class="w-8 h-8 rounded-full bg-amber-100 text-amber-600 flex items-center justify-center text-sm font-bold shrink-0">!</div>
          <div class="flex-1 text-sm text-amber-700 leading-relaxed font-medium">{{ $t('timesheet.lbl_no_employee') }}</div>
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

      <TimesheetGroupReport v-if="group" :group="group" :dates="days" :can-approve="!!props.people?.canApprove" />
      <div v-else :class="cn('flex-1 min-h-0 overflow-auto flex flex-col rounded-3xl border border-slate-200 bg-white shadow-2xl shadow-slate-200/50 transition-all duration-500', flash && 'ring-4 ring-green-400/40')">
        <table class="w-full border-separate border-spacing-0 text-left min-w-max">
          <thead class="sticky top-0 z-20">
            <tr class="bg-slate-50/80 backdrop-blur-md text-[11px] font-bold uppercase tracking-widest text-slate-500">
              <th class="border-b border-slate-200 px-4 py-2.5 w-[240px] min-w-[240px] pl-6 sticky left-0 z-30 bg-slate-50/90 backdrop-blur-md">{{ $t('timesheet.col_category') }}</th>
              <th
                v-for="d in days"
                :key="d"
                :class="cn('border-b border-slate-200 px-2 py-2.5 w-[80px] min-w-[80px] text-center', d === todayIso ? 'text-pr' : dayInfo[d]?.weekend ? 'text-slate-400' : '')"
                :title="dayInfo[d]?.holiday ?? undefined"
              >
                <div class="flex flex-col items-center">
                  <span class="text-[11px] font-bold tracking-wide uppercase">{{ dowOf(d) }}</span>
                  <span :class="cn('text-[10px] font-bold mt-0.5 tracking-wider', d === todayIso ? 'text-pr' : dayInfo[d]?.holiday ? 'text-rose-400' : 'text-slate-400')">{{ formatDate(d, 'd/M') }}</span>
                  <div v-if="d === todayIso" class="h-1 w-1 rounded-full bg-pr mt-1" />
                </div>
              </th>
              <th class="border-b border-slate-200 px-2 py-2.5 w-[100px] min-w-[100px] text-center">{{ $t('timesheet.col_total') }}</th>
              <th class="border-b border-slate-200 px-2 py-2.5 w-[80px] min-w-[80px] text-center">%</th>
            </tr>
          </thead>
          <tbody>
            <template v-if="query.isPending.value">
              <tr v-for="i in 5" :key="i">
                <td v-for="c in days.length + 3" :key="c" class="border-b border-slate-100 px-6 py-5"><div class="h-4 animate-pulse rounded bg-slate-100" /></td>
              </tr>
            </template>
            <tr v-for="r in rows" v-else :key="r.key" class="group">
              <td class="border-b border-slate-100 px-4 py-1.5 pl-6 sticky left-0 z-10 bg-white group-hover:bg-slate-50/90 transition-colors border-r border-slate-100 shadow-[2px_0_5px_-2px_rgba(0,0,0,0.05)]">
                <div class="flex items-center gap-2.5">
                  <span class="w-2 h-2 rounded-full shrink-0 shadow-sm" :style="{ backgroundColor: rowColor(r) }" />
                  <span class="font-extrabold text-[13px] text-slate-900 truncate max-w-[180px]" :style="r.generated ? { color: rowColor(r) } : undefined" :title="r.label">
                    {{ r.label }}<template v-if="r.generated"> ({{ r.generated === 'leave' ? $t('timesheet.lbl_generated_leave') : $t('timesheet.lbl_generated_ot') }})</template>
                  </span>
                </div>
              </td>
              <td v-for="d in days" :key="d" class="border-b border-slate-100 px-2 py-1.5 text-center" :title="cellTitle(r, d)">
                <div class="flex justify-center">
                  <input
                    type="text"
                    inputmode="decimal"
                    :disabled="cellDisabled(r, d)"
                    :aria-label="`${r.label} ${formatDate(d)}`"
                    :class="cn(
                      'h-9 w-[54px] rounded-[10px] text-center text-[13px] transition-all duration-200 outline-none border',
                      cellDisabled(r, d)
                        ? 'bg-transparent border-transparent opacity-30 text-slate-400'
                        : valueOf(r.key, d) > rules.maxHours
                          ? 'bg-red-50 border-red-300 text-red-600 font-black focus:border-red-500 focus:ring-2 focus:ring-red-500/20'
                          : valueOf(r.key, d) > 0
                            ? 'bg-white border-transparent text-slate-900 font-black shadow-[0_4px_12px_rgba(15,23,42,0.05)] focus:border-pr focus:ring-2 focus:ring-pr/20'
                            : 'bg-white/80 border-transparent text-slate-400 hover:bg-white hover:border-slate-200 focus:bg-white focus:border-pr focus:ring-2 focus:ring-pr/20',
                      d === todayIso && 'bg-[#EEF2FB]',
                    )"
                    :style="valueOf(r.key, d) > 0 && valueOf(r.key, d) <= rules.maxHours && !cellDisabled(r, d) ? { color: rowColor(r) } : undefined"
                    :value="cellText(r.key, d)"
                    :placeholder="cellDisabled(r, d) ? '' : '--'"
                    @input="onInput(r.key, d, ($event.target as HTMLInputElement).value)"
                    @blur="onBlur(r.key, d)"
                  />
                </div>
              </td>
              <td class="border-b border-slate-100 px-2 py-1.5 text-center">
                <span class="text-[13px] font-black text-slate-900" :style="rowTotal(r.key) > 0 ? { color: rowColor(r) } : undefined">{{ rowTotal(r.key).toFixed(1) }}</span>
              </td>
              <td class="border-b border-slate-100 px-2 py-1.5 text-center">
                <span class="text-[12px] font-bold text-slate-500">{{ weekTotal > 0 ? Math.round((rowTotal(r.key) / weekTotal) * 100) : 0 }}%</span>
              </td>
            </tr>
            <tr v-if="!query.isPending.value && rows.length === 0">
              <td :colspan="days.length + 3" class="px-6 py-16 text-center text-[13px] font-medium text-slate-400">{{ $t('timesheet.empty_no_data') }}</td>
            </tr>
          </tbody>
          <tfoot class="sticky bottom-0 z-20">
            <tr class="text-sm font-black bg-slate-50/90 backdrop-blur-sm">
              <td class="p-3 pl-6 text-[#2D3448] uppercase text-xs tracking-widest font-bold sticky left-0 z-10 bg-slate-50/90 border-t border-[#C9D2E7] border-r border-slate-200">{{ $t('timesheet.col_total') }}</td>
              <td v-for="d in days" :key="d" :class="cn('p-3 text-center border-t border-[#C9D2E7]', d === todayIso && 'text-[#7c3aed]')">
                <span
                  :title="dayTotal(d) > rules.maxHours ? $t('timesheet.lbl_max_hours_warning', { hours: rules.maxHours }) : dayTotal(d) < rules.minHours && !dayInfo[d]?.weekend ? $t('timesheet.lbl_min_hours_warning', { hours: rules.minHours }) : undefined"
                  :class="cn(
                    dayTotal(d) > 0 ? (d === todayIso ? 'text-[#7c3aed]' : 'text-slate-900') : 'text-slate-300',
                    dayTotal(d) < rules.minHours && dayTotal(d) > 0 && 'text-amber-500 font-bold bg-amber-50 rounded-lg px-1.5 py-0.5 border border-amber-200/50',
                    dayTotal(d) > rules.maxHours && 'text-red-500 font-bold bg-red-50 rounded-lg px-1.5 py-0.5 border border-red-200/50',
                  )"
                >{{ dayTotal(d) > 0 ? dayTotal(d).toFixed(1) : '--' }}</span>
              </td>
              <td class="p-3 text-center border-t border-[#C9D2E7]"><span class="text-[#7c3aed] text-base">{{ weekTotal.toFixed(1) }}</span></td>
              <td class="p-3 text-center border-t border-[#C9D2E7]"><span class="text-slate-500">100%</span></td>
            </tr>
          </tfoot>
        </table>
      </div>

      <div v-if="!group" class="flex-none pt-4 flex items-center justify-between gap-3">
        <Button variant="primary" class="h-9 lg:h-10 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" :disabled="saving || !period?.canEdit || noEmployee" @click="copyLastWeek">
          <template #leftIcon><Copy class="w-4 h-4" /></template>
          {{ $t('timesheet.btn_copy_last_week') }}
        </Button>
        <div class="flex items-center gap-3">
          <ExportButton
            :columns="exportColumns"
            :data-fn="exportData"
            :default-filename="`timesheet_${start}_${end}`"
            :label="$t('timesheet.btn_export_week')"
            hide-scope
            class="h-9 lg:h-10 rounded-xl px-4 text-[13px] font-bold text-[#4C5568] hover:text-pr hover:border-pr/50 hover:bg-slate-50 border border-[#E4E8F3] bg-white shadow-sm shrink-0"
          />
          <Button variant="primary" class="h-9 lg:h-10 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" :loading="saving" :disabled="!period?.canEdit || !dirty || noEmployee" @click="save">
            <template #leftIcon><Check class="w-4 h-4" /></template>
            {{ $t('timesheet.btn_save') }}
          </Button>
        </div>
      </div>
    </div>
  </PageLayout>
</template>
