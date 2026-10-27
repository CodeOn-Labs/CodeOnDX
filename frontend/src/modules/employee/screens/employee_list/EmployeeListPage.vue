<script setup lang="ts">
// Port EmployeeListPage.tsx — Quản trị → Nhân sự.
// Bản gốc chỉ đọc nhân sự đồng bộ từ HRM (nút "Đồng bộ", cột SSO/LDAP); bản mới tự quản lý (D4):
// thêm/sửa hồ sơ, mời tạo tài khoản, xoá mềm/khôi phục. Trạng thái phân bổ, FTE và tỷ lệ sử dụng do BE tính.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query';
import { AlertTriangle, Briefcase, Building2, Clock, Download, Network, Plus, Search, Trash2, TrendingUp, UserCheck, Users } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import DynamicInsights from '@/modules/ai/components/DynamicInsights.vue';
import AnimatedCounter from '@/shared/components/AnimatedCounter.vue';
import ListToolbar from '@/shared/components/filter/ListToolbar.vue';
import StatSegment from '@/shared/components/StatSegment.vue';
import Button from '@/shared/components/Button.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import ExportButton from '@/shared/components/export/ExportButton.vue';
import type { ExportColumn, ExportMode } from '@/shared/components/export/types';
import type { ActiveFilter } from '@/shared/components/filter/types';
import EmployeeTable from './EmployeeTable.vue';
import EmployeeFormDialog from '@/modules/employee/components/EmployeeFormDialog.vue';
import { ALLOCATION_STATUSES, EMPTY_EMPLOYEE_FILTERS, orgApi, type Employee, type EmployeeFilters } from '@/modules/org/api/org.api';
import { usePagination } from '@/shared/composables/usePagination';
import { useMasterData } from '@/shared/composables/useMasterData';
import { useAuthStore } from '@/stores/auth';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { useUiStore } from '@/stores/ui';
import { formatDate } from '@/shared/utils/date';

const { t, locale } = useI18n();
const auth = useAuthStore();
const ui = useUiStore();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();
const canManage = computed(() => auth.can('employee.manage'));

const { items: positions } = useMasterData('job_position');
const { items: levels } = useMasterData('hr_level');
const { items: statuses } = useMasterData('employee_status');
const departments = useQuery({ queryKey: ['departments'], queryFn: orgApi.departments });
const allTeams = computed(() => (departments.data.value ?? []).flatMap((d) => d.teams));

const allocationLabel = (s: string) =>
  ({
    overloaded: t('employee_list.opt_overloaded'),
    normal: t('employee_list.opt_normal'),
    low: t('employee_list.opt_low'),
    bench: t('employee_list.opt_bench'),
    resigned: t('employee_list.opt_resigned'),
  })[s] ?? s;

const selects = computed(() => [
  { key: 'positions' as const, label: t('employee_list.lbl_position'), options: positions.value.map((p) => ({ label: p.name, value: p.code })) },
  { key: 'levelIds' as const, label: t('employee_list.lbl_level'), options: levels.value.map((l) => ({ label: l.name, value: l.id })) },
  { key: 'departmentIds' as const, label: t('employee_list.lbl_department'), options: (departments.data.value ?? []).map((d) => ({ label: d.name, value: d.id })) },
  { key: 'teamIds' as const, label: t('employee_list.lbl_team'), options: allTeams.value.map((tm) => ({ label: tm.name, value: tm.id })) },
  { key: 'allocation' as const, label: t('employee_list.lbl_allocation'), options: ALLOCATION_STATUSES.map((s) => ({ label: allocationLabel(s), value: s })) },
  { key: 'statuses' as const, label: t('employee_list.lbl_employment_status'), options: statuses.value.map((s) => ({ label: s.name, value: s.code })) },
]);

// ─── Bộ lọc + phân trang ────────────────────────────────────────────────────
const saved = ui.getFilters<{ filters: EmployeeFilters; pageSize: number }>('employee_list');
const committed = ref<EmployeeFilters>({ ...EMPTY_EMPLOYEE_FILTERS, ...saved?.filters });
const draft = ref<EmployeeFilters>({ ...committed.value });
const filterOpen = ref(false);
const pagination = usePagination({ initialPageSize: saved?.pageSize });
watch([committed, pagination.pageSize], () => ui.setFilters('employee_list', { filters: committed.value, pageSize: pagination.pageSize.value }), { deep: true });

const listQuery = useQuery({
  queryKey: computed(() => ['employees', 'list', committed.value, pagination.pageIndex.value, pagination.pageSize.value, locale.value] as const),
  queryFn: ({ signal }) => orgApi.employees(committed.value, pagination.pageIndex.value + 1, pagination.pageSize.value, signal),
  placeholderData: keepPreviousData,
});
const summaryQuery = useQuery({
  queryKey: computed(() => ['employees', 'summary', committed.value] as const),
  queryFn: ({ signal }) => orgApi.employeeSummary(committed.value, signal),
  placeholderData: keepPreviousData,
});
watch(
  () => listQuery.data.value?.totalCount,
  (total) => {
    if (total !== undefined) pagination.totalItems.value = total;
  },
  { immediate: true },
);

function commit(next: EmployeeFilters) {
  const same = JSON.stringify(next) === JSON.stringify(committed.value);
  committed.value = { ...next };
  pagination.goToPage(0);
  if (same) void listQuery.refetch();
}
function reset() {
  draft.value = { ...EMPTY_EMPLOYEE_FILTERS };
  commit(EMPTY_EMPLOYEE_FILTERS);
  filterOpen.value = false;
}
function clearFilter(id: string) {
  const key = id as keyof EmployeeFilters;
  const next = { ...committed.value, [key]: EMPTY_EMPLOYEE_FILTERS[key] } as EmployeeFilters;
  draft.value = { ...next };
  commit(next);
}
const asArray = (v: string | string[] | null) => (Array.isArray(v) ? v : v ? [v] : []);
function setDraft(key: (typeof selects.value)[number]['key'], value: string | string[] | null) {
  draft.value = { ...draft.value, [key]: asArray(value) };
}

const ICONS = { positions: Briefcase, levelIds: TrendingUp, departmentIds: Building2, teamIds: Network, allocation: AlertTriangle, statuses: UserCheck } as const;
const activeFilters = computed<ActiveFilter[]>(() => {
  const f = committed.value;
  const list: ActiveFilter[] = [];
  if (f.search) list.push({ id: 'search', label: t('employee_list.lbl_chip_keyword'), displayValue: f.search, icon: Search });
  for (const s of selects.value) {
    const values = f[s.key] as string[];
    if (!values.length) continue;
    const labels = values.map((v) => s.options.find((o) => o.value === v)?.label ?? v);
    list.push({ id: s.key, label: s.label, displayValue: labels.slice(0, 2).join(', ') + (labels.length > 2 ? ` +${labels.length - 2}` : ''), icon: ICONS[s.key] });
  }
  if (f.deleted) list.push({ id: 'deleted', label: t('employee_list.lbl_chip_trash'), displayValue: t('employee_list.lbl_chip_viewing_trash'), icon: Trash2 });
  return list;
});
const advancedCount = computed(() => selects.value.reduce((s, sel) => s + (committed.value[sel.key] as string[]).length, 0) + (committed.value.deleted ? 1 : 0));

// ─── Thao tác ───────────────────────────────────────────────────────────────
const dialogOpen = ref(false);
const editing = ref<Employee | null>(null);
function openDialog(e: Employee | null) {
  editing.value = e;
  dialogOpen.value = true;
}
async function act(action: () => Promise<{ messageText: string }>) {
  try {
    toast.success((await action()).messageText);
    void queryClient.invalidateQueries({ queryKey: ['employees'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}
async function remove(e: Employee) {
  const ok = await confirm.ask({
    title: t('employee_list.confirm_delete_title'),
    description: t('employee_list.confirm_delete_desc', { name: e.fullName }),
    confirmText: t('employee_list.btn_confirm_delete'),
    type: 'danger',
  });
  if (ok) await act(() => orgApi.deleteEmployee(e.id));
}
function restore(e: Employee) {
  void act(() => orgApi.restoreEmployee(e.id));
}
async function invite(e: Employee) {
  const ok = await confirm.ask({
    title: t('employee_list.confirm_invite_title'),
    description: t('employee_list.confirm_invite_desc', { email: e.email }),
    confirmText: t('employee_list.btn_invite'),
    type: 'info',
  });
  if (ok) await act(() => orgApi.inviteEmployee(e.id));
}

// ─── Xuất file ──────────────────────────────────────────────────────────────
const exportColumns = computed<ExportColumn<Employee>[]>(() => [
  { id: 'code', header: t('employee_list.col_code'), accessorKey: 'code', width: 12 },
  { id: 'name', header: t('employee_list.col_employee'), accessorKey: 'fullName', width: 25 },
  { id: 'email', header: t('employee_list.col_export_email'), accessorKey: 'email', width: 30 },
  { id: 'position', header: t('employee_list.col_position'), accessorFn: (r) => r.position?.name ?? '--', width: 20 },
  { id: 'level', header: t('employee_list.col_level'), accessorFn: (r) => r.level?.name ?? '--', width: 15 },
  { id: 'department', header: t('employee_list.col_department'), accessorFn: (r) => r.department?.name ?? '--', width: 20 },
  { id: 'team', header: t('employee_list.col_team'), accessorFn: (r) => r.team?.name ?? '--', width: 20 },
  { id: 'fte', header: t('employee_list.col_fte'), accessorFn: (r) => r.totalFte, width: 12, formatter: (v) => Number(v).toFixed(1) },
  {
    id: 'status',
    header: t('employee_list.col_status'),
    accessorFn: (r) => allocationLabel(r.allocationStatus),
    width: 18,
    conditionalFormat: (_v, r) =>
      ({ overloaded: { color: '#dc2626', bold: true }, normal: { color: '#059669', bold: true }, low: { color: '#2563eb', bold: true }, bench: { color: '#d97706', bold: true }, resigned: { color: '#64748b' } })[r.allocationStatus],
  },
  { id: 'joined', header: t('employee_list.col_joined_date'), accessorFn: (r) => formatDate(r.joinedDate) || '--', width: 15 },
]);
async function exportData(mode: ExportMode) {
  if (mode === 'current_page') return listQuery.data.value?.items ?? [];
  return (await orgApi.employees(mode === 'all' ? EMPTY_EMPLOYEE_FILTERS : committed.value, 1, 0)).items;
}
const summary = computed(() => summaryQuery.data.value);
</script>

<template>
  <PageLayout
    layout="A"
    :insight-title="$t('employee_list.title_insight_panel')"
    content-padding="24px"
    disable-scroll
    insights-width="320px"
  >
    <template #insights>
      <div class="flex flex-col gap-3">
        <div class="bg-bg1 border border-b2 rounded-xl p-4 flex flex-col items-center text-center shadow-sm">
          <div class="text-3xl font-bold text-bl mb-1">
            <AnimatedCounter v-if="summary" :value="summary.utilizationPercent" :formatter="(v: number) => `${v.toFixed(1)}%`" />
            <template v-else>--%</template>
          </div>
          <div class="text-[11px] text-t3 font-medium uppercase tracking-tight">
            {{ $t('employee_list.lbl_utilization', { target: summary?.utilizationTarget ?? 85 }) }}
          </div>
        </div>
        <div class="flex flex-col gap-2 mt-3">
          <DynamicInsights screen="employee_list" />
        </div>
      </div>
    </template>

    <div class="h-full flex flex-col min-h-0 gap-2">
      <ListToolbar
        v-model:search="draft.search"
        v-model:filter-open="filterOpen"
        :title="$t('employee_list.title_page')"
        :kicker="$t('employee_list.lbl_kicker')"
        :search-placeholder="$t('employee_list.ph_search')"
        :active-filters="activeFilters"
        :advanced-filter-count="advancedCount"
        :is-fetching="listQuery.isFetching.value"
        @search="commit(draft)"
        @apply-filters="commit(draft); filterOpen = false"
        @reset="reset"
        @clear-filter="clearFilter"
      >
        <template #tools>
          <ExportButton
            :columns="exportColumns"
            :data-fn="exportData"
            :default-filename="$t('employee_list.lbl_export_filename')"
            variant="ghost"
            size="sm"
            class="h-8 px-3 text-slate-600 hover:bg-white hover:text-pr hover:shadow-sm text-[12px] font-semibold"
            :label="$t('employee_list.btn_export')"
            :total-rows="summary?.total ?? 0"
            :filtered-rows="pagination.totalItems.value"
          >
            <template #icon><Download :size="13" /></template>
          </ExportButton>
        </template>
        <template v-if="canManage" #actions>
          <Button variant="primary" class="h-9 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" @click="openDialog(null)">
            <template #leftIcon><Plus :size="16" :stroke-width="3" /></template>
            {{ $t('employee_list.btn_create') }}
          </Button>
        </template>
        <template #filters>
          <FormSelect
            v-for="s in selects"
            :key="s.key"
            :model-value="draft[s.key]"
            :label="s.label"
            :placeholder="$t('employee_list.ph_all')"
            :options="s.options"
            searchable
            multiple
            hide-message
            trigger-class="h-10 rounded-xl text-[13px]"
            @update:model-value="setDraft(s.key, $event)"
          />
          <div class="flex items-center justify-between p-2 bg-slate-50 rounded-xl border border-slate-100 mt-2">
            <label for="employee-show-deleted" class="flex flex-col gap-0.5 cursor-pointer">
              <span class="text-[12px] font-bold text-slate-700">{{ $t('employee_list.lbl_trash') }}</span>
              <span class="text-[10px] text-slate-400">{{ $t('employee_list.hint_trash') }}</span>
            </label>
            <input
              id="employee-show-deleted"
              v-model="draft.deleted"
              type="checkbox"
              class="h-5 w-9 shrink-0 cursor-pointer appearance-none rounded-full bg-slate-200 transition-all checked:bg-emerald-500 relative before:content-[''] before:absolute before:h-4 before:w-4 before:bg-white before:rounded-full before:left-0.5 before:top-0.5 before:transition-all checked:before:translate-x-4 shadow-inner"
            />
          </div>
        </template>
      </ListToolbar>

      <div class="flex flex-1 flex-col gap-5 min-h-0">
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-px rounded-2xl border border-white/60 bg-white/40 backdrop-blur-xl shadow-xl shadow-slate-200/40 overflow-hidden">
          <StatSegment :label="$t('employee_list.stat_total')" :value="summary?.total ?? 0" tone="violet" :icon="Users" :loading="!summary" />
          <StatSegment :label="$t('employee_list.stat_in_projects')" :value="summary?.inProjects ?? 0" tone="green" :icon="UserCheck" :delay="0.05" :loading="!summary" />
          <StatSegment :label="$t('employee_list.stat_bench')" :value="summary?.bench ?? 0" tone="amber" :icon="Clock" :delay="0.1" :loading="!summary" />
          <StatSegment :label="$t('employee_list.stat_overloaded')" :value="summary?.overloaded ?? 0" tone="red" :icon="AlertTriangle" :delay="0.15" :loading="!summary" />
        </div>

        <div class="flex flex-1 flex-col gap-2 min-h-0">
          <EmployeeTable
            :employees="listQuery.data.value?.items ?? []"
            :loading="listQuery.isFetching.value"
            :pagination="pagination"
            :can-manage="canManage"
            @edit="openDialog"
            @delete="remove"
            @restore="restore"
            @invite="invite"
          />
        </div>
      </div>
    </div>

    <EmployeeFormDialog v-model:open="dialogOpen" :employee="editing" />
  </PageLayout>
</template>
