<script setup lang="ts">
// Port ProjectDetailPage.tsx: header + 4 tab (Tổng quan · Báo cáo tháng · Metrics · Quản trị dự án), tab/subtab nằm trên URL
// (?tab=&subtab=), Insight theo màn đang xem (BE ghép chuỗi kế thừa ADR-0001; tab Quản trị dự án không kế thừa), xuất Excel,
// báo cáo tình trạng, xoá / khôi phục. Quyền: BE trả 403 khi người không quản lý không được phân bổ vào dự án.
import { computed, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Activity, AlertTriangle, ClipboardList, FileSpreadsheet, Info } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import TabBar, { type TabItem } from '@/shared/components/TabBar.vue';
import ExportDialog from '@/shared/components/export/ExportDialog.vue';
import { useExport } from '@/shared/components/export/useExport';
import type { ExportColumn, ExportConfig, ExportSheet } from '@/shared/components/export/types';
import ProjectDetailHeader from './ProjectDetailHeader.vue';
import StatusReportDialog from './StatusReportDialog.vue';
import OverviewTab from './overview/OverviewTab.vue';
import MonthlyReportTab from './monthly-report/MonthlyReportTab.vue';
import MetricsTab from './metrics/MetricsTab.vue';
import ManagementTab from './management/ManagementTab.vue';
import { projectDetailApi, type Staffing } from '@/modules/project/api/project-detail.api';
import { ApiError } from '@/api/http';
import type { ScreenKey } from '@/app/screens';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

type MainTab = 'overview' | 'monthly-report' | 'metrics' | 'management';
const route = useRoute();
const router = useRouter();
const { t, locale } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const id = computed(() => String(route.params.id));
const detail = useQuery({
  queryKey: computed(() => ['projects', 'detail', id.value, locale.value] as const),
  queryFn: ({ signal }) => projectDetailApi.detail(id.value, { signal }),
  retry: false,
  meta: { silent: true },
});
const project = computed(() => detail.data.value);
const noAccess = computed(() => detail.error.value instanceof ApiError && detail.error.value.status === 403);

// Tab Báo cáo tháng tạm ẩn (30/10): giữ nguyên MonthlyReportTab, đổi thành true để hiện lại.
const SHOW_MONTHLY_REPORT = false;
const TABS: MainTab[] = SHOW_MONTHLY_REPORT ? ['overview', 'monthly-report', 'metrics', 'management'] : ['overview', 'metrics', 'management'];
const tab = computed<MainTab>({
  get: () => (TABS.includes(route.query.tab as MainTab) ? (route.query.tab as MainTab) : 'overview'),
  // Đổi tab chính phải xoá subtab (subtab dùng chung giữa Metrics và Quản trị dự án)
  set: (next) => void router.replace({ query: { ...route.query, tab: next, subtab: undefined } }),
});
const subtab = computed({
  get: () => (typeof route.query.subtab === 'string' ? route.query.subtab : ''),
  set: (next: string) => void router.replace({ query: { ...route.query, subtab: next } }),
});
const tabs = computed(() => ([
  { key: 'overview', label: t('project_detail.tab_overview'), icon: Info },
  { key: 'monthly-report', label: t('project_detail.tab_monthly_report'), icon: FileSpreadsheet },
  { key: 'metrics', label: t('project_detail.tab_metrics'), icon: Activity },
  { key: 'management', label: t('project_detail.tab_management'), icon: ClipboardList },
] satisfies TabItem<MainTab>[]).filter((item) => TABS.includes(item.key)));

const METRIC_SCREENS: Record<string, ScreenKey> = {
  timeline: 'project_detail_metrics_timeline', ee: 'project_detail_metrics_ee', pcv: 'project_detail_metrics_pcv',
  css: 'project_detail_metrics_css', defect: 'project_detail_metrics_defect', leakage: 'project_detail_metrics_leakage',
};
const MANAGEMENT_SCREENS: Record<string, ScreenKey> = {
  risk: 'project_detail_management_risk', issue: 'project_detail_management_issue',
};
const insightScreen = computed<ScreenKey>(() => {
  if (tab.value === 'metrics') return METRIC_SCREENS[subtab.value] ?? 'project_detail_metrics_timeline';
  if (tab.value === 'management') return MANAGEMENT_SCREENS[subtab.value] ?? 'project_detail_management_risk';
  if (tab.value === 'monthly-report') return 'project_detail_monthly_report';
  return 'project_detail_overview';
});

async function remove() {
  if (!project.value) return;
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.confirm_delete_title'), description: t('project_detail.confirm_delete_desc'), confirmText: t('project_detail.btn_confirm_delete') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.remove(project.value.id)).messageText);
    void queryClient.invalidateQueries({ queryKey: ['projects'] });
    void router.push('/projects');
  } catch (err) {
    toast.error((err as Error).message);
  }
}
async function restore() {
  if (!project.value) return;
  const ok = await confirm.ask({ type: 'warning', title: t('project_detail.confirm_restore_title'), description: t('project_detail.confirm_restore_desc'), confirmText: t('project_detail.btn_confirm_restore') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.restore(project.value.id)).messageText);
    void queryClient.invalidateQueries({ queryKey: ['projects'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}

// ─── Xuất dữ liệu: thông tin chung · phân bổ nhân sự · số liệu biểu đồ HĐ/KH/TT theo ngày ───
const reportOpen = ref(false);
const exportOpen = ref(false);
const { progress, startExport, cancelExport, reset } = useExport();
const exportColumns = computed<ExportColumn[]>(() => [
  { id: 'general', header: t('project_detail.export_dialog.lbl_sheet_general') },
  { id: 'assignments', header: t('project_detail.export_dialog.lbl_sheet_assignments') },
  { id: 'charts', header: t('project_detail.export_dialog.lbl_sheet_charts') },
]);
function openExport() {
  reset();
  exportOpen.value = true;
}
async function runExport(config: ExportConfig) {
  const p = project.value;
  if (!p) return;
  const visible = new Set(exportColumns.value.filter((c) => (config.columns ?? exportColumns.value).some((x) => x.id === c.id && !x.hidden)).map((c) => c.id));
  let staffing: Staffing | undefined = queryClient.getQueryData(['projects', 'staffing', p.id]);
  staffing ??= await projectDetailApi.staffing(p.id);
  const sheets: ExportSheet[] = [];
  if (visible.has('general')) {
    const rows = [
      [t('project_detail.export_dialog.lbl_field_name'), p.name], [t('project_detail.export_dialog.lbl_field_grade'), p.grade?.name ?? '--'], [t('project_detail.export_dialog.lbl_field_client'), p.client?.name ?? '--'],
      [t('project_detail.export_dialog.lbl_field_start'), formatDate(p.startDate) || '--'], [t('project_detail.export_dialog.lbl_field_end'), formatDate(p.endDate) || '--'],
      [t('project_detail.export_dialog.lbl_field_status'), p.status?.name ?? '--'], [t('project_detail.export_dialog.lbl_field_risk'), p.riskLevel?.name ?? '--'],
      [t('project_detail.export_dialog.lbl_field_budget'), p.budget ?? '--'], [t('project_detail.stat_contract_fte'), p.contractStaffFte], [t('project_detail.stat_actual_fte'), p.actualStaffFte],
      [t('project_detail.stat_progress'), `${p.progressPercent ?? 0}%`],
    ].map(([field, value]) => ({ field, value }));
    sheets.push({ sheetName: t('project_detail.export_dialog.lbl_sheet_general'), data: rows, columns: [
      { id: 'field', header: t('project_detail.export_dialog.col_field'), accessorKey: 'field', width: 25, excel: { bold: true } },
      { id: 'value', header: t('project_detail.export_dialog.col_value'), accessorKey: 'value', width: 40, type: 'string' },
    ] });
  }
  if (visible.has('assignments')) {
    const rows = staffing.groups.flatMap((g) => g.slots.flatMap((s) => (s.assignments.length === 0 ? [{ a: null, iv: null, i: 0 }] : s.assignments.flatMap((a) => a.intervals.map((iv, i) => ({ a, iv, i })))).map(({ a, iv, i }) => ({
      role: g.roleName, level: g.level?.name ?? '--', name: a?.employee?.fullName ?? t('project_detail.export_dialog.lbl_vacant'), code: a?.employee?.code ?? '--',
      phase: a && a.intervals.length > 1 ? t('project_detail.lbl_phase', { n: i + 1 }) : '--', start: formatDate(iv?.startDate) || '--', end: formatDate(iv?.endDate) || '--', fte: iv?.fte ?? 0,
    }))));
    sheets.push({ sheetName: t('project_detail.export_dialog.lbl_sheet_assignments'), data: rows, rowMergeColumns: [1, 2], columns: [
      { id: 'role', header: t('project_detail.col_contract_role'), accessorKey: 'role', width: 22 },
      { id: 'level', header: t('project_detail.export_dialog.col_level'), accessorKey: 'level', width: 14 },
      { id: 'name', header: t('project_detail.export_dialog.col_name'), accessorKey: 'name', width: 26 },
      { id: 'code', header: t('project_detail.export_dialog.col_code'), accessorKey: 'code', width: 12 },
      { id: 'phase', header: t('project_detail.export_dialog.col_phase'), accessorKey: 'phase', width: 12 },
      { id: 'start', header: t('project_detail.export_dialog.lbl_field_start'), accessorKey: 'start', width: 14 },
      { id: 'end', header: t('project_detail.export_dialog.lbl_field_end'), accessorKey: 'end', width: 14 },
      { id: 'fte', header: 'FTE', accessorKey: 'fte', type: 'number', width: 10, excel: { numFmt: '0.00' } },
    ] });
  }
  if (visible.has('charts')) {
    const rows = staffing.groups.flatMap((g) => g.slots.flatMap((s) => s.series.filter((pt) => pt.contract > 0 || pt.planned > 0 || pt.actual > 0).map((pt) => ({
      role: g.roleName, person: s.assignments.map((a) => a.employee?.fullName).filter(Boolean).join(', ') || t('project_detail.export_dialog.lbl_vacant'), ...pt, date: formatDate(pt.date),
    }))));
    sheets.push({ sheetName: t('project_detail.export_dialog.lbl_sheet_charts'), data: rows, columns: [
      { id: 'role', header: t('project_detail.col_contract_role'), accessorKey: 'role', width: 22 },
      { id: 'person', header: t('project_detail.export_dialog.col_name'), accessorKey: 'person', width: 26 },
      { id: 'date', header: t('project_detail.export_dialog.col_date'), accessorKey: 'date', width: 14 },
      { id: 'contract', header: t('project_detail.lbl_contract'), accessorKey: 'contract', type: 'number', width: 12 },
      { id: 'planned', header: t('project_detail.lbl_plan'), accessorKey: 'planned', type: 'number', width: 12 },
      { id: 'actual', header: t('project_detail.lbl_actual'), accessorKey: 'actual', type: 'number', width: 12 },
    ] });
  }
  startExport([], { ...config, sheets });
}
</script>

<template>
  <PageLayout
    layout="A"
    :insight-title="$t('project_detail.title_insight_panel')"
    :insight-screen="project ? insightScreen : undefined"
    content-padding="24px"
    disable-scroll
    insights-width="340px"
  >
    <div v-if="detail.isPending.value" class="flex flex-col gap-4">
      <div class="h-8 w-64 animate-pulse rounded-lg bg-[#e8ecf5]" />
      <div class="grid grid-cols-1 gap-3 md:grid-cols-5"><div v-for="i in 5" :key="i" class="h-[68px] animate-pulse rounded-[12px] bg-[#e8ecf5]" /></div>
      <div v-for="i in 4" :key="i" class="h-14 animate-pulse rounded-xl bg-[#e8ecf5]" />
    </div>

    <div v-else-if="noAccess || !project" class="flex h-[60vh] flex-col items-center justify-center px-4 text-center">
      <div class="mb-4 rounded-full bg-rose-50 p-4 text-rose-500 shadow-inner"><AlertTriangle :size="48" /></div>
      <h2 class="mb-2 text-xl font-black tracking-tight text-slate-900">{{ noAccess ? $t('project_detail.title_no_access') : $t('project_detail.title_not_found') }}</h2>
      <p class="mb-6 max-w-md text-sm leading-relaxed text-slate-500">{{ noAccess ? $t('project_detail.msg_no_access') : (detail.error.value?.message ?? '') }}</p>
      <Button variant="outline" class="h-10 rounded-xl px-6 text-[14px] font-bold" @click="router.push('/projects')">{{ $t('project_detail.btn_back_list') }}</Button>
    </div>

    <div v-else class="flex h-full min-h-0 flex-col">
      <div class="z-40 -mx-6 mb-2 border-b border-slate-100 bg-white/80 px-6 pt-2 pb-3 backdrop-blur-xl transition-all duration-300">
        <ProjectDetailHeader :project="project" @export="openExport" @report="reportOpen = true" @delete="remove" @restore="restore" />
      </div>
      <div class="-mx-6 mb-3 shrink-0 px-6">
        <TabBar v-model="tab" :tabs="tabs" />
      </div>
      <div :class="cn('min-h-0 flex-1', tab !== 'overview' && 'overflow-y-auto')">
        <OverviewTab v-if="tab === 'overview'" :project="project" />
        <MonthlyReportTab v-else-if="tab === 'monthly-report'" :project="project" />
        <MetricsTab v-else-if="tab === 'metrics'" v-model:subtab="subtab" :project="project" />
        <ManagementTab v-else v-model:subtab="subtab" :project="project" />
      </div>

      <StatusReportDialog v-model:open="reportOpen" :project-id="project.id" :project-name="project.name" :client-name="project.client?.name ?? null" />
      <ExportDialog
        :open="exportOpen"
        :columns="exportColumns"
        :progress="progress"
        :default-filename="`${$t('project_detail.export_dialog.lbl_filename')}_${project.code}`"
        hide-scope
        @close="exportOpen = false; reset()"
        @export="runExport"
        @cancel="cancelExport"
      />
    </div>
  </PageLayout>
</template>
