<script setup lang="ts">
// Port MonthlyReportTab + Toolbar + StatusLine + GeneralInfo + MemberChips + DayTable (+ các ô Hours/Time/Content/CustomValue,
// ColumnHeader, ColumnDialog, TotalRow): báo cáo công việc tháng gửi khách hàng (kiêm biên bản nghiệm thu), mỗi nhân sự 1 bảng ngày.
// - Tháng chưa có báo cáo hiển thị bản nháp; lần sửa đầu tiên mới ghi DB. Mọi người xem được dự án đều sửa được (giống bản gốc).
// - Giờ làm lấy từ Nhật ký công việc đã duyệt, giờ bắt đầu / kết thúc từ Chấm công vào/ra (BE tính khi đọc); ô sửa tay được đánh dấu
//   và không bị đồng bộ ghi đè — nút "Đồng bộ lại" trả các ô sửa tay về dữ liệu nguồn.
// - Tự lưu từng ô khi rời ô. Cột bổ sung: chèn / đổi tên (bấm đúp) / sửa / xoá; cột hệ thống không xoá được.
// Khác bản gốc: bỏ tự dịch nội dung sang tiếng Nhật (chế độ 日本語 chỉ đổi nhãn cột / đầu trang), xuất Excel dựng bằng code thay template,
// bỏ xem trước / xuất PDF, bỏ insight riêng của báo cáo tháng (dùng Insight theo màn ở cột phải).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { CalendarClock, FileDown, Pencil, Plus, RefreshCw, RotateCcw, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import MonthNavigator from '@/shared/components/form/MonthNavigator.vue';
import { projectDetailApi, type MonthlyReportColumn, type MonthlyReportDay, type MonthlyReportHeaderField, type ProjectDetail } from '@/modules/project/api/project-detail.api';
import { buildMonthlyReportWorkbook } from './monthly-report-excel';
import { firstFieldErrors } from '@/api/http';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ project: ProjectDetail }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

/** Mặc định tháng trước (báo cáo thường lập đầu tháng cho tháng vừa xong). */
const prev = new Date();
prev.setDate(1);
prev.setMonth(prev.getMonth() - 1);
const month = ref(`${prev.getFullYear()}-${String(prev.getMonth() + 1).padStart(2, '0')}`);
const memberIndex = ref(0);
watch(month, () => (memberIndex.value = 0));

const queryKey = computed(() => ['projects', 'monthly-report', props.project.id, month.value] as const);
const query = useQuery({
  queryKey,
  queryFn: ({ signal }) => projectDetailApi.monthlyReport(props.project.id, month.value, { signal }),
  placeholderData: (p) => p,
});
const report = computed(() => query.data.value);
const canEdit = computed(() => !!report.value?.canEdit);
const member = computed(() => report.value?.members[Math.min(memberIndex.value, (report.value?.members.length ?? 1) - 1)]);
const isJa = computed(() => report.value?.header.language === 'ja');
const refresh = () => queryClient.invalidateQueries({ queryKey: queryKey.value });

const pending = ref(0);
const saveError = ref(false);
async function run<T>(fn: () => Promise<T>, quiet = true): Promise<T | null> {
  pending.value += 1;
  try {
    const res = await fn();
    saveError.value = false;
    if (!quiet) toast.success((res as { messageText?: string })?.messageText ?? '');
    return res;
  } catch (err) {
    const fe = Object.values(firstFieldErrors(err))[0];
    toast.error(fe ?? (err as Error).message);
    if (!fe) saveError.value = true;
    return null;
  } finally {
    pending.value -= 1;
    await refresh();
  }
}

// ─── Thông tin chung ───
const HEADER_FIELDS: Array<{ key: Exclude<MonthlyReportHeaderField, 'language'>; ja: string }> = [
  { key: 'clientName', ja: '御中' }, { key: 'companyName', ja: '会社名' }, { key: 'orderNo', ja: '注文番号' }, { key: 'workName', ja: '作業名' }, { key: 'responsible', ja: '作業責任者' },
];
const headerLabel = (f: (typeof HEADER_FIELDS)[number]) => ({
  clientName: t('project_detail.monthly_report_section.lbl_client'), companyName: t('project_detail.monthly_report_section.lbl_company'), orderNo: t('project_detail.monthly_report_section.lbl_order_no'),
  workName: t('project_detail.monthly_report_section.lbl_work_name'), responsible: t('project_detail.monthly_report_section.lbl_responsible'),
})[f.key];
const headerPlaceholder = (f: (typeof HEADER_FIELDS)[number]) => ({
  clientName: t('project_detail.monthly_report_section.ph_client'), companyName: t('project_detail.monthly_report_section.ph_company'), orderNo: t('project_detail.monthly_report_section.ph_order_no'),
  workName: t('project_detail.monthly_report_section.ph_work_name'), responsible: t('project_detail.monthly_report_section.ph_responsible'),
})[f.key];
async function saveHeader(field: MonthlyReportHeaderField, value: string) {
  const r = report.value;
  if (!r || (field !== 'language' && r.header[field] === value) || (field === 'language' && r.header.language === value)) return;
  await run(() => projectDetailApi.saveReportHeader(props.project.id, month.value, field, value));
}

// ─── Ô ngày ───
async function saveCell(day: MonthlyReportDay, field: string, raw: string) {
  const m = member.value;
  if (!m) return;
  const current = field === 'hours' ? (day.hours === null ? '' : String(day.hours))
    : field === 'start' ? day.start ?? '' : field === 'end' ? day.end ?? '' : field === 'content' ? day.content : day.custom[field.slice(7)] ?? '';
  if (raw === current) return;
  await run(() => projectDetailApi.saveReportCell(props.project.id, month.value, { employeeId: m.employeeId, date: day.date, field, value: raw === '' ? null : raw }));
}
const val = (e: Event) => (e.target as HTMLInputElement).value;
async function restoreCell(day: MonthlyReportDay, kind: 'hours' | 'time') {
  const m = member.value;
  if (!m) return;
  await run(() => projectDetailApi.syncReport(props.project.id, month.value, kind, { employeeId: m.employeeId, date: day.date }));
}
async function syncAll(kind: 'hours' | 'time') {
  const r = report.value;
  if (!r) return;
  const count = kind === 'hours' ? r.manualHourDays : r.manualTimeDays;
  if (count === 0) {
    toast.info(kind === 'hours' ? t('project_detail.monthly_report_section.btn_sync_hours_nothing') : t('project_detail.monthly_report_section.btn_sync_time_nothing'));
    return;
  }
  const ok = await confirm.ask({
    type: 'warning',
    title: kind === 'hours' ? t('project_detail.monthly_report_section.btn_sync_hours_title') : t('project_detail.monthly_report_section.btn_sync_time_title'),
    description: kind === 'hours' ? t('project_detail.monthly_report_section.btn_sync_hours_desc', { count }) : t('project_detail.monthly_report_section.btn_sync_time_desc', { count }),
    confirmText: t('project_detail.monthly_report_section.btn_sync_confirm'),
  });
  if (ok) await run(() => projectDetailApi.syncReport(props.project.id, month.value, kind), false);
}

// ─── Cột ───
const systemLabel = (c: MonthlyReportColumn) => ({
  date: t('project_detail.monthly_report_section.col_date'), hours: t('project_detail.monthly_report_section.col_hours'), content: t('project_detail.monthly_report_section.col_content'),
  checkIn: t('project_detail.monthly_report_section.col_check_in'), checkOut: t('project_detail.monthly_report_section.col_check_out'),
})[c.source as NonNullable<MonthlyReportColumn['source']>];
/** Nhãn cột: chế độ 日本語 dùng tên tiếng Nhật (trống thì dùng tên thường); cột hệ thống chưa đổi tên dùng nhãn theo ngôn ngữ giao diện. */
function columnLabel(c: MonthlyReportColumn) {
  if (isJa.value && c.labelJa) return c.labelJa;
  return c.source ? systemLabel(c) : c.label;
}
const editingColumn = ref<string | null>(null);
const renameText = ref('');
function startRename(c: MonthlyReportColumn) {
  if (!canEdit.value || c.source) return;
  editingColumn.value = c.id;
  renameText.value = c.label;
}
async function finishRename(c: MonthlyReportColumn) {
  const label = renameText.value.trim();
  editingColumn.value = null;
  if (!label || label === c.label) return;
  await run(() => projectDetailApi.updateReportColumn(props.project.id, month.value, c.id, { label, labelJa: c.labelJa, type: c.type }));
}
async function insertAfter(index: number) {
  const r = report.value;
  if (!r) return;
  let n = r.columns.filter((c) => !c.source).length + 1;
  const names = new Set(r.columns.map((c) => c.label.toLowerCase()));
  while (names.has(t('project_detail.monthly_report_section.lbl_new_column', { n }).toLowerCase())) n += 1;
  const res = await run(() => projectDetailApi.addReportColumn(props.project.id, month.value, { position: index + 1, label: t('project_detail.monthly_report_section.lbl_new_column', { n }), labelJa: '', type: 'text' }));
  if (res) {
    editingColumn.value = res.data.id;
    renameText.value = t('project_detail.monthly_report_section.lbl_new_column', { n });
  }
}
async function removeColumn(c: MonthlyReportColumn) {
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.monthly_report_section.btn_remove_column_title'), description: t('project_detail.monthly_report_section.btn_remove_column_desc', { name: c.label }), confirmText: t('project_detail.btn_confirm_delete') });
  if (ok) await run(() => projectDetailApi.removeReportColumn(props.project.id, month.value, c.id), false);
}
const columnDialog = ref<MonthlyReportColumn | null>(null);
const columnForm = ref({ label: '', labelJa: '', type: 'text' as 'text' | 'number' });
const columnErrors = ref<Record<string, string>>({});
function openColumn(c: MonthlyReportColumn) {
  columnDialog.value = c;
  columnErrors.value = {};
  columnForm.value = { label: c.source ? columnLabel(c) : c.label, labelJa: c.labelJa, type: c.type };
}
async function submitColumn() {
  const c = columnDialog.value;
  if (!c) return;
  try {
    toast.success((await projectDetailApi.updateReportColumn(props.project.id, month.value, c.id, columnForm.value)).messageText);
    columnDialog.value = null;
    await refresh();
  } catch (err) {
    columnErrors.value = firstFieldErrors(err);
    if (Object.keys(columnErrors.value).length === 0) toast.error((err as Error).message);
  }
}

/** Tổng cột số của nhân sự đang xem: Σ giờ làm và Σ từng cột bổ sung kiểu Số. */
const totals = computed(() => {
  const m = member.value;
  const out: Record<string, number> = {};
  if (!m || !report.value) return out;
  for (const c of report.value.columns) {
    if (c.source === 'hours') out[c.id] = m.totalHours;
    else if (!c.source && c.type === 'number') out[c.id] = Math.round(m.days.reduce((s, d) => s + (Number(d.custom[c.id]) || 0), 0) * 100) / 100;
  }
  return out;
});
const weekday = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString(isJa.value ? 'ja-JP' : undefined, { weekday: 'short' });

const exporting = ref(false);
async function exportExcel() {
  const r = report.value;
  if (!r) return;
  exporting.value = true;
  try {
    const blob = await buildMonthlyReportWorkbook(r, {
      sheetTitle: isJa.value ? '作業報告書 兼 納品書' : t('project_detail.monthly_report_section.lbl_excel_title'),
      client: isJa.value ? '御中' : t('project_detail.monthly_report_section.lbl_client'), company: isJa.value ? '会社名' : t('project_detail.monthly_report_section.lbl_company'),
      orderNo: isJa.value ? '注文番号' : t('project_detail.monthly_report_section.lbl_order_no'), workName: isJa.value ? '作業名' : t('project_detail.monthly_report_section.lbl_work_name'),
      responsible: isJa.value ? '作業責任者' : t('project_detail.monthly_report_section.lbl_responsible'), member: isJa.value ? '作業担当者' : t('project_detail.monthly_report_section.lbl_member'),
      month: isJa.value ? '対象月' : t('project_detail.monthly_report_section.lbl_month'), total: isJa.value ? '合計' : t('project_detail.monthly_report_section.lbl_total'), columnLabel,
    });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `${t('project_detail.monthly_report_section.lbl_excel_filename')}_${props.project.code}_${month.value}.xlsx`;
    a.click();
    URL.revokeObjectURL(a.href);
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    exporting.value = false;
  }
}
const CELL_INPUT = 'h-8 w-full rounded-md border border-transparent bg-transparent px-1.5 text-[12.5px] outline-none hover:border-slate-200 focus:border-pr focus:bg-white focus:ring-2 focus:ring-pr/15 disabled:hover:border-transparent';
</script>

<template>
  <div class="flex flex-col gap-3 pb-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div class="flex items-center gap-3">
        <h3 class="text-[14px] font-black text-slate-800">{{ $t('project_detail.tab_monthly_report') }}</h3>
        <MonthNavigator v-model="month" />
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <div v-if="report" class="flex rounded-xl border border-slate-200 bg-white p-0.5" role="group" :aria-label="$t('project_detail.monthly_report_section.lbl_language')">
          <button type="button" :disabled="!canEdit" :class="cn('rounded-lg px-2.5 py-1 text-[12px] font-bold', !isJa ? 'bg-pr text-white' : 'text-slate-500')" @click="saveHeader('language', 'vi')">{{ $t('project_detail.monthly_report_section.lbl_language_default') }}</button>
          <button type="button" :disabled="!canEdit" :class="cn('rounded-lg px-2.5 py-1 text-[12px] font-bold', isJa ? 'bg-pr text-white' : 'text-slate-500')" @click="saveHeader('language', 'ja')">日本語</button>
        </div>
        <template v-if="canEdit">
          <Button variant="outline" size="sm" @click="syncAll('hours')"><template #leftIcon><RefreshCw :size="14" /></template>{{ $t('project_detail.monthly_report_section.btn_sync_hours') }}</Button>
          <Button variant="outline" size="sm" @click="syncAll('time')"><template #leftIcon><CalendarClock :size="14" /></template>{{ $t('project_detail.monthly_report_section.btn_sync_time') }}</Button>
        </template>
        <Button variant="primary" size="sm" :loading="exporting" :disabled="!report || pending > 0" @click="exportExcel"><template #leftIcon><FileDown :size="14" /></template>{{ $t('project_detail.monthly_report_section.btn_export') }}</Button>
      </div>
    </div>

    <p v-if="query.isError.value" role="alert" class="rounded-[12px] border border-rose-200 bg-rose-50 px-4 py-3 text-[12.5px] font-semibold text-rose-700">{{ $t('project_detail.monthly_report_section.lbl_load_error') }}</p>
    <p v-else-if="!report" class="py-8 text-center text-[12.5px] font-semibold text-slate-400">{{ $t('project_detail.lbl_loading') }}</p>
    <div v-else class="rounded-[12px] border border-[#e8ecf5] bg-white shadow-sm">
      <div class="flex flex-wrap items-center gap-x-4 gap-y-1 border-b border-slate-100 px-4 py-2 text-[11.5px] font-semibold text-slate-500">
        <span>{{ report.isDraft ? $t('project_detail.monthly_report_section.lbl_status_draft') : $t('project_detail.monthly_report_section.lbl_status_saved', { time: formatDate(report.updatedAt), name: report.updatedByName ?? '' }) }}</span>
        <span>{{ $t('project_detail.monthly_report_section.lbl_source_hours', { days: report.approvedLogDays }) }}</span>
        <span>{{ $t('project_detail.monthly_report_section.lbl_source_time') }}</span>
        <span v-if="pending > 0" class="text-amber-600">{{ $t('project_detail.monthly_report_section.lbl_saving') }}</span>
        <span v-else-if="saveError" class="text-rose-600">{{ $t('project_detail.monthly_report_section.btn_save_error') }}</span>
        <span v-if="!canEdit" class="ml-auto text-slate-400">{{ $t('project_detail.monthly_report_section.lbl_read_only') }}</span>
      </div>

      <div class="grid grid-cols-1 gap-3 p-4 sm:grid-cols-2 lg:grid-cols-5">
        <label v-for="f in HEADER_FIELDS" :key="f.key" class="flex flex-col gap-1 text-[10.5px] font-bold uppercase tracking-wide text-slate-400">
          <span>{{ isJa ? f.ja : headerLabel(f) }} <span class="normal-case text-slate-300">· {{ isJa ? headerLabel(f) : f.ja }}</span></span>
          <input
            :value="report.header[f.key]"
            :disabled="!canEdit"
            :placeholder="headerPlaceholder(f)"
            maxlength="200"
            class="h-9 w-full rounded-lg border border-slate-200 bg-white px-2.5 text-[12.5px] font-normal normal-case tracking-normal text-slate-800 outline-none focus:border-pr focus:ring-2 focus:ring-pr/20 disabled:bg-slate-50 disabled:text-slate-500"
            @change="saveHeader(f.key, val($event).trim())"
          />
        </label>
      </div>

      <div class="flex flex-wrap gap-1.5 border-t border-slate-100 px-4 py-2.5">
        <button v-for="(m, i) in report.members" :key="m.employeeId" type="button" :class="cn('rounded-full border px-3 py-1 text-[12px] font-bold transition-colors', i === memberIndex ? 'border-pr bg-pr text-white' : 'border-slate-200 bg-white text-slate-600 hover:border-pr hover:text-pr')" @click="memberIndex = i">
          {{ m.fullName }} <span :class="i === memberIndex ? 'text-white/70' : 'text-slate-400'">· {{ m.totalHours }}h</span>
        </button>
      </div>

      <div v-if="!member" class="flex h-32 flex-col items-center justify-center gap-1.5 border-t border-slate-100 text-center">
        <p class="text-[12.5px] font-bold text-slate-400">{{ $t('project_detail.monthly_report_section.empty_members') }}</p>
      </div>
      <div v-else class="overflow-x-auto border-t border-slate-100">
        <table class="w-full min-w-[820px] border-collapse text-[12.5px]">
          <thead>
            <tr class="bg-slate-50/80">
              <th v-for="(c, ci) in report.columns" :key="c.id" :class="cn('group/col relative border-b border-r border-slate-100 px-2 py-2 text-left text-[11px] font-bold text-slate-500 last:border-r-0', c.source === 'content' ? 'min-w-[280px]' : c.source === 'date' ? 'w-[86px]' : 'w-[110px]')">
                <div class="flex items-center gap-1">
                  <input v-if="editingColumn === c.id" v-model="renameText" :aria-label="$t('project_detail.monthly_report_section.lbl_column_name')" class="h-7 w-full rounded border border-pr px-1.5 text-[12px] font-bold outline-none" @blur="finishRename(c)" @keydown.enter="($event.target as HTMLInputElement).blur()" @keydown.esc="editingColumn = null" />
                  <span v-else class="flex-1 truncate" :title="!c.source && canEdit ? $t('project_detail.monthly_report_section.lbl_dblclick_hint') : undefined" @dblclick="startRename(c)">{{ columnLabel(c) }}</span>
                  <span v-if="c.source === 'checkIn' || c.source === 'checkOut'" class="rounded bg-sky-50 px-1 text-[9px] font-black text-sky-600">{{ $t('project_detail.monthly_report_section.lbl_badge_attendance') }}</span>
                  <template v-if="canEdit && editingColumn !== c.id">
                    <button type="button" class="rounded p-0.5 text-slate-400 opacity-0 hover:bg-violet-100 hover:text-pr group-hover/col:opacity-100" :title="$t('project_detail.monthly_report_section.btn_edit_column')" @click="openColumn(c)"><Pencil :size="11" /></button>
                    <button v-if="!c.source" type="button" class="rounded p-0.5 text-slate-400 opacity-0 hover:bg-rose-100 hover:text-rose-600 group-hover/col:opacity-100" :title="$t('project_detail.monthly_report_section.btn_remove_column')" @click="removeColumn(c)"><X :size="11" /></button>
                  </template>
                </div>
                <button v-if="canEdit" type="button" class="absolute -right-2 top-1/2 z-10 flex h-4 w-4 -translate-y-1/2 items-center justify-center rounded-full bg-pr text-white opacity-0 shadow group-hover/col:opacity-100" :title="$t('project_detail.monthly_report_section.btn_insert_column')" @click="insertAfter(ci)"><Plus :size="10" /></button>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="d in member.days" :key="`${member.employeeId}-${d.date}`" :class="cn('border-b border-slate-100', d.weekend && 'bg-slate-50/70')">
              <td v-for="c in report.columns" :key="c.id" class="border-r border-slate-100 px-1 py-0.5 align-top last:border-r-0">
                <div v-if="c.source === 'date'" :class="cn('px-1.5 py-1.5 font-bold tabular-nums', d.weekend ? 'text-rose-400' : 'text-slate-700')">{{ d.date.slice(8, 10) }}/{{ d.date.slice(5, 7) }} <span class="text-[10.5px] font-semibold text-slate-400">{{ weekday(d.date) }}</span></div>
                <div v-else-if="c.source === 'hours'" class="flex items-center gap-1">
                  <input :value="d.hours ?? ''" type="number" min="0" max="24" step="0.5" :disabled="!canEdit" :aria-label="$t('project_detail.monthly_report_section.lbl_hours_label', { date: formatDate(d.date) })" :class="cn(CELL_INPUT, 'text-center font-bold', d.hoursSource === 'manual' && 'text-amber-600')" @change="saveCell(d, 'hours', val($event))" />
                  <button v-if="d.hoursSource === 'manual' && canEdit" type="button" class="shrink-0 rounded p-0.5 text-amber-500 hover:bg-amber-50" :title="$t('project_detail.monthly_report_section.btn_restore_hours')" @click="restoreCell(d, 'hours')"><RotateCcw :size="11" /></button>
                </div>
                <div v-else-if="c.source === 'checkIn' || c.source === 'checkOut'" class="flex items-center gap-1">
                  <input :value="(c.source === 'checkIn' ? d.start : d.end) ?? ''" type="time" :disabled="!canEdit" :class="cn(CELL_INPUT, 'text-center tabular-nums', d.timeSource === 'manual' && 'text-amber-600')" :title="d.timeSource === 'manual' ? $t('project_detail.monthly_report_section.lbl_time_manual_hint') : d.timeSource === 'attendance' ? $t('project_detail.monthly_report_section.lbl_time_attendance_hint') : undefined" @change="saveCell(d, c.source === 'checkIn' ? 'start' : 'end', val($event))" />
                  <button v-if="d.timeSource === 'manual' && canEdit && c.source === 'checkOut'" type="button" class="shrink-0 rounded p-0.5 text-amber-500 hover:bg-amber-50" :title="$t('project_detail.monthly_report_section.btn_restore_time')" @click="restoreCell(d, 'time')"><RotateCcw :size="11" /></button>
                </div>
                <textarea v-else-if="c.source === 'content'" :value="d.content" rows="1" maxlength="1000" :disabled="!canEdit" :class="cn(CELL_INPUT, 'h-auto min-h-8 resize-y py-1.5')" @change="saveCell(d, 'content', val($event))" />
                <input v-else :value="d.custom[c.id] ?? ''" :type="c.type === 'number' ? 'number' : 'text'" :disabled="!canEdit" :class="cn(CELL_INPUT, c.type === 'number' && 'text-center')" @change="saveCell(d, `custom:${c.id}`, val($event))" />
              </td>
            </tr>
            <tr class="bg-slate-50 font-black">
              <td v-for="(c, ci) in report.columns" :key="c.id" class="border-r border-slate-100 px-2.5 py-2 last:border-r-0">
                <template v-if="ci === 0">{{ isJa ? '合計' : $t('project_detail.monthly_report_section.lbl_total') }}</template>
                <template v-else-if="totals[c.id] !== undefined"><span class="block text-center tabular-nums text-pr">{{ totals[c.id] }}</span></template>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <BaseDialog :open="!!columnDialog" :title="$t('project_detail.monthly_report_section.btn_edit_column')" :description="$t('project_detail.monthly_report_section.lbl_column_dialog_desc')" max-width="md" @update:open="(v: boolean) => { if (!v) columnDialog = null; }">
      <div class="flex flex-col gap-4">
        <FormItem :label="$t('project_detail.monthly_report_section.lbl_column_name')" required :error="columnErrors.label"><BaseInput v-model="columnForm.label" maxlength="50" :error="!!columnErrors.label" /></FormItem>
        <FormItem :label="$t('project_detail.monthly_report_section.lbl_column_name_ja')" :error="columnErrors.labelJa"><BaseInput v-model="columnForm.labelJa" maxlength="50" /></FormItem>
        <FormItem v-if="!columnDialog?.source" :label="$t('project_detail.monthly_report_section.lbl_column_type')">
          <div class="flex gap-2">
            <button type="button" :class="cn('flex-1 rounded-xl border px-3 py-2 text-[12.5px] font-bold', columnForm.type === 'text' ? 'border-pr bg-pr/5 text-pr' : 'border-slate-200 text-slate-500')" @click="columnForm.type = 'text'">{{ $t('project_detail.monthly_report_section.lbl_type_text') }}</button>
            <button type="button" :class="cn('flex-1 rounded-xl border px-3 py-2 text-[12.5px] font-bold', columnForm.type === 'number' ? 'border-pr bg-pr/5 text-pr' : 'border-slate-200 text-slate-500')" @click="columnForm.type = 'number'">{{ $t('project_detail.monthly_report_section.lbl_type_number') }}</button>
          </div>
        </FormItem>
      </div>
      <template #footer>
        <Button variant="outline" @click="columnDialog = null">{{ $t('common.btn_cancel') }}</Button>
        <Button variant="primary" @click="submitColumn">{{ $t('common.btn_save') }}</Button>
      </template>
    </BaseDialog>
  </div>
</template>
