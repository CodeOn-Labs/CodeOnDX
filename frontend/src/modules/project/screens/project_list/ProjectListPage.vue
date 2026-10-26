<script setup lang="ts">
// Port ProjectListPage.tsx — màn Quản lý dự án.
// Bộ lọc đã áp dụng + phân trang giữ trong ui store (thay Redux uiSlice); dữ liệu qua TanStack Vue Query từ /api/v1/projects.
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { keepPreviousData, useQuery } from '@tanstack/vue-query';
import { Activity, AlertTriangle, BarChart3, Calendar, Download, Search, User } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import DynamicInsights from '@/modules/ai/components/DynamicInsights.vue';
import ExportButton from '@/shared/components/export/ExportButton.vue';
import ProjectImportDialog from './ProjectImportDialog.vue';
import type { ExportColumn, ExportMode } from '@/shared/components/export/types';
import type { ActiveFilter } from '@/shared/components/filter/types';
import ProjectFilterBar from './ProjectFilterBar.vue';
import ProjectStatsCards from './ProjectStatsCards.vue';
import ProjectTable from './ProjectTable.vue';
import { EMPTY_PROJECT_FILTERS, projectApi, type ProjectFilters, type ProjectListItem } from '@/modules/project/api/project.api';
import { DEFAULT_BUDGET_WARNING_THRESHOLD, formatProjectTimeline } from '@/modules/project/utils/project-view-model';
import { DEFAULT_PAGE_SIZE, usePagination } from '@/shared/composables/usePagination';
import { useClients, useMasterData, useSystemRules } from '@/shared/composables/useMasterData';
import { enumLabel } from '@/shared/i18n/enum-label';
import { formatDate } from '@/shared/utils/date';
import { useAuthStore } from '@/stores/auth';
import { useUiStore } from '@/stores/ui';

interface SavedState {
  filters: ProjectFilters;
  pageIndex: number;
  pageSize: number;
}

const { t, locale } = useI18n();
const router = useRouter();
const auth = useAuthStore();
const ui = useUiStore();

const canManage = computed(() => auth.can('project.manage'));
const canCreate = computed(() => auth.can('project.create'));

// ─── Danh mục ───────────────────────────────────────────────────────────────
const { items: statuses } = useMasterData('project_status');
const { items: grades } = useMasterData('project_level');
const { items: riskLevels } = useMasterData('risk_level');
const { items: clients } = useClients();
const rules = useSystemRules();
const budgetThreshold = computed(() => rules.ruleNumber('PROJECT_BUDGET_WARNING_THRESHOLD', DEFAULT_BUDGET_WARNING_THRESHOLD));

const toOptions = (rows: { id: string; name: string }[]) => rows.map((r) => ({ label: r.name, value: r.id }));

// ─── Bộ lọc + phân trang (giữ khi rời màn) ──────────────────────────────────
const saved = ui.getFilters<SavedState>('project_list');
const committed = ref<ProjectFilters>({ ...EMPTY_PROJECT_FILTERS, ...saved?.filters });
const draft = ref<ProjectFilters>({ ...committed.value });
const filterOpen = ref(false);
const pagination = usePagination({ initialPageIndex: saved?.pageIndex ?? 0, initialPageSize: saved?.pageSize ?? DEFAULT_PAGE_SIZE });

watch(
  [committed, pagination.pageIndex, pagination.pageSize],
  () => ui.setFilters<SavedState>('project_list', { filters: committed.value, pageIndex: pagination.pageIndex.value, pageSize: pagination.pageSize.value }),
  { deep: true },
);

// ─── Dữ liệu ────────────────────────────────────────────────────────────────
const listQuery = useQuery({
  queryKey: computed(() => ['projects', 'list', committed.value, pagination.pageIndex.value, pagination.pageSize.value, locale.value] as const),
  queryFn: ({ signal }) => projectApi.list(committed.value, pagination.pageIndex.value + 1, pagination.pageSize.value, signal),
  placeholderData: keepPreviousData,
});
const summaryQuery = useQuery({
  queryKey: computed(() => ['projects', 'summary', committed.value] as const),
  queryFn: ({ signal }) => projectApi.summary(committed.value, signal),
  placeholderData: keepPreviousData,
});
// Tổng số dự án không lọc — hiển thị ở lựa chọn "Tất cả dữ liệu" khi xuất file
const allCountQuery = useQuery({
  queryKey: ['projects', 'count-all'],
  queryFn: ({ signal }) => projectApi.list(EMPTY_PROJECT_FILTERS, 1, 1, signal).then((r) => r.totalCount),
  enabled: canManage,
});

const projects = computed(() => listQuery.data.value?.items ?? []);
watch(
  () => listQuery.data.value?.totalCount,
  (total) => {
    if (total !== undefined) pagination.totalItems.value = total;
  },
  { immediate: true },
);

// ─── Thao tác ───────────────────────────────────────────────────────────────
function commit(filters: ProjectFilters) {
  const same = JSON.stringify(filters) === JSON.stringify(committed.value);
  pagination.goToPage(0);
  committed.value = { ...filters };
  // Bấm Tìm kiếm với bộ lọc không đổi → vẫn tải lại (giống bản gốc)
  if (same) void listQuery.refetch();
}

function search() {
  commit(draft.value);
}

function applyFilters() {
  commit(draft.value);
  filterOpen.value = false;
}

function reset() {
  draft.value = { ...EMPTY_PROJECT_FILTERS };
  commit(EMPTY_PROJECT_FILTERS);
  filterOpen.value = false;
}

function clearFilter(id: string) {
  const key = id as keyof ProjectFilters;
  const empty = { ...EMPTY_PROJECT_FILTERS }[key];
  const next = { ...committed.value, [key]: Array.isArray(empty) ? [] : empty };
  draft.value = { ...draft.value, [key]: next[key] };
  commit(next);
}

const importOpen = ref(false);
function showImport() {
  importOpen.value = true;
}

// ─── Chip bộ lọc đang áp dụng ───────────────────────────────────────────────
function joinNames(ids: string[], rows: { id: string; name: string }[]) {
  const names = ids.map((id) => rows.find((r) => r.id === id)?.name).filter(Boolean).slice(0, 2).join(', ');
  return ids.length > 2 ? `${names} +${ids.length - 2}` : names;
}

const activeFilters = computed<ActiveFilter[]>(() => {
  const f = committed.value;
  const list: ActiveFilter[] = [];
  if (f.search) list.push({ id: 'search', label: t('project_list.lbl_chip_keyword'), displayValue: f.search, icon: Search });
  if (f.clientIds.length) list.push({ id: 'clientIds', label: t('project_list.lbl_chip_client'), displayValue: joinNames(f.clientIds, clients.value), icon: User });
  if (f.statusIds.length) list.push({ id: 'statusIds', label: t('project_list.lbl_chip_status'), displayValue: joinNames(f.statusIds, statuses.value), icon: Activity });
  if (f.gradeIds.length) list.push({ id: 'gradeIds', label: t('project_list.lbl_chip_grade'), displayValue: joinNames(f.gradeIds, grades.value), icon: BarChart3 });
  if (f.riskLevelIds.length) list.push({ id: 'riskLevelIds', label: t('project_list.lbl_chip_risk'), displayValue: joinNames(f.riskLevelIds, riskLevels.value), icon: AlertTriangle });
  if (f.startFrom) list.push({ id: 'startFrom', label: t('project_list.lbl_chip_start'), displayValue: formatDate(f.startFrom), icon: Calendar });
  if (f.endTo) list.push({ id: 'endTo', label: t('project_list.lbl_chip_end'), displayValue: formatDate(f.endTo), icon: Calendar });
  if (f.deleted) list.push({ id: 'deleted', label: t('project_list.lbl_chip_trash'), displayValue: t('project_list.lbl_chip_viewing_trash'), icon: AlertTriangle });
  return list;
});

const advancedFilterCount = computed(() => {
  const f = committed.value;
  return f.statusIds.length + f.gradeIds.length + f.clientIds.length + f.riskLevelIds.length + (f.startFrom ? 1 : 0) + (f.endTo ? 1 : 0) + (f.deleted ? 1 : 0);
});

// ─── Xuất file ──────────────────────────────────────────────────────────────
const STATUS_COLORS: Record<string, { color: string; bold?: boolean }> = {
  RUNNING: { color: '#059669', bold: true },
  ENDING: { color: '#d97706', bold: true },
  PLANNING: { color: '#2563eb', bold: true },
  COMPLETED: { color: '#64748b' },
};
const RISK_COLORS: Record<string, { color: string; bold?: boolean }> = {
  HIGH: { color: '#dc2626', bold: true },
  MEDIUM: { color: '#d97706', bold: true },
  LOW: { color: '#059669', bold: true },
};

const exportColumns = computed<ExportColumn<ProjectListItem>[]>(() => [
  { id: 'name', header: t('project_list.col_export_name'), accessorKey: 'name', width: 25 },
  { id: 'client', header: t('project_list.col_export_client'), accessorFn: (row) => row.client?.name || '--', width: 25 },
  { id: 'level', header: t('project_list.col_export_grade'), accessorFn: (row) => row.grade?.name || '--', width: 15 },
  { id: 'contract_staff', header: t('project_list.col_export_contract_fte'), accessorFn: (row) => row.contractStaffFte, width: 22, formatter: (v) => Number(v || 0).toFixed(1) },
  { id: 'actual_staff', header: t('project_list.col_export_actual_fte'), accessorFn: (row) => row.actualStaffFte, width: 22, formatter: (v) => Number(v || 0).toFixed(1) },
  { id: 'budget_spent', header: t('project_list.col_export_budget_used'), accessorFn: (row) => row.budgetUsedPercent, width: 20, formatter: (v) => `${Number(v || 0).toFixed(0)}%` },
  { id: 'timeline', header: t('project_list.col_export_timeline'), accessorFn: (row) => formatProjectTimeline(row.startDate, row.endDate) || '--', width: 25 },
  {
    id: 'status',
    header: t('project_list.col_export_status'),
    accessorFn: (row) => enumLabel('project_status', row.status?.code ?? 'PLANNING', row.status?.name),
    width: 18,
    conditionalFormat: (_v, row) => STATUS_COLORS[row.status?.code ?? ''] ?? { color: '#64748b' },
  },
  {
    id: 'risk',
    header: t('project_list.col_export_risk'),
    accessorFn: (row) => (row.riskLevel ? enumLabel('risk_level', row.riskLevel.code, row.riskLevel.name) : '—'),
    width: 18,
    conditionalFormat: (_v, row) => RISK_COLORS[row.riskLevel?.code ?? ''] ?? { color: '#64748b' },
  },
]);

async function exportData(mode: ExportMode): Promise<ProjectListItem[]> {
  if (mode === 'current_page') return projects.value;
  const filters = mode === 'all' ? EMPTY_PROJECT_FILTERS : committed.value;
  return (await projectApi.list(filters, 1, 0)).items;
}
</script>

<template>
  <PageLayout
    layout="B"
    :insight-title="$t('project_list.title_insight_panel')"
    ai-module="projects"
    content-padding="24px"
    disable-scroll
    insights-width="320px"
    chat-width="300px"
  >
    <template #insights>
      <DynamicInsights screen="project_list" />
    </template>

    <div class="h-full flex flex-col min-h-0">
      <div class="flex min-h-0 flex-col gap-2 h-full">
        <ProjectFilterBar
          v-model:draft="draft"
          v-model:filter-open="filterOpen"
          :active-filters="activeFilters"
          :advanced-filter-count="advancedFilterCount"
          :is-fetching="listQuery.isFetching.value"
          :can-manage="canManage"
          :can-create="canCreate"
          :client-options="toOptions(clients)"
          :status-options="toOptions(statuses)"
          :grade-options="toOptions(grades)"
          :risk-options="toOptions(riskLevels)"
          @search="search"
          @reset="reset"
          @apply-filters="applyFilters"
          @clear-filter="clearFilter"
          @show-import="showImport"
          @new-project="router.push('/projects/new')"
        >
          <template #export>
            <ExportButton
              :columns="exportColumns"
              :data-fn="exportData"
              :default-filename="$t('project_list.lbl_export_filename')"
              variant="ghost"
              size="sm"
              class="h-8 px-3 text-slate-600 hover:bg-white hover:text-pr hover:shadow-sm text-[12px] font-semibold"
              :label="$t('project_list.btn_export')"
              :total-rows="allCountQuery.data.value ?? 0"
              :filtered-rows="pagination.totalItems.value"
            >
              <template #icon><Download :size="13" /></template>
            </ExportButton>
          </template>
        </ProjectFilterBar>

        <div class="flex flex-1 flex-col gap-5 min-h-0">
          <ProjectStatsCards :summary="summaryQuery.data.value" :loading="summaryQuery.isPending.value" />

          <div class="flex flex-1 flex-col gap-2 min-h-0">
            <ProjectTable
              :projects="projects"
              :loading="listQuery.isFetching.value"
              :pagination="pagination"
              :can-manage="canManage"
              :budget-threshold="budgetThreshold"
            />
            <div class="flex items-center gap-2 px-1 text-[11px] font-bold uppercase tracking-widest text-slate-400 pb-1">
              <div class="h-1 w-1 rounded-full bg-slate-300" />
              {{ $t('project_list.hint_click_project_name') }}
            </div>
          </div>
        </div>
      </div>
    </div>
    <ProjectImportDialog v-model:open="importOpen" />
  </PageLayout>
</template>
