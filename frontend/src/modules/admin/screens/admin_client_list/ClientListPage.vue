<script setup lang="ts">
// Port ClientListPage.tsx — Quản trị → Khách hàng: lọc (quốc gia, ngành nghề, trạng thái, thùng rác), thống kê,
// bảng, thêm/sửa, xoá mềm → xoá vĩnh viễn, khôi phục, xuất Excel/CSV, xem dự án của khách hàng.
// Bỏ so với bản gốc: Import khách hàng (chưa có wizard Import dùng chung — xem báo cáo chuyển đổi).
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query';
import { Building2, Download, Filter, Globe, LayoutGrid, Plus, Search, ShieldCheck, Trash2 } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import ListToolbar from '@/shared/components/filter/ListToolbar.vue';
import StatSegment from '@/shared/components/StatSegment.vue';
import Button from '@/shared/components/Button.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import ExportButton from '@/shared/components/export/ExportButton.vue';
import type { ExportColumn, ExportMode } from '@/shared/components/export/types';
import type { ActiveFilter } from '@/shared/components/filter/types';
import ClientTable from './ClientTable.vue';
import ClientDialog from './ClientDialog.vue';
import { EMPTY_CLIENT_FILTERS, orgApi, type Client, type ClientFilters, type ClientInput } from '@/modules/org/api/org.api';
import { EMPTY_PROJECT_FILTERS } from '@/modules/project/api/project.api';
import { usePagination } from '@/shared/composables/usePagination';
import { useMasterData } from '@/shared/composables/useMasterData';
import { useFormMutation } from '@/shared/composables/useFormMutation';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { useUiStore } from '@/stores/ui';

const { t, locale } = useI18n();
const router = useRouter();
const ui = useUiStore();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const { items: countries } = useMasterData('country');
const { items: industries } = useMasterData('industry');
const toOptions = (rows: { id: string; name: string }[]) => rows.map((r) => ({ label: r.name, value: r.id }));
const statusOptions = computed(() => [
  { label: t('admin_client_list.opt_status_active'), value: 'active' },
  { label: t('admin_client_list.opt_status_inactive'), value: 'inactive' },
]);

// ─── Bộ lọc + phân trang (giữ khi rời màn) ──────────────────────────────────
const saved = ui.getFilters<{ filters: ClientFilters; pageSize: number }>('admin_client_list');
const committed = ref<ClientFilters>({ ...EMPTY_CLIENT_FILTERS, ...saved?.filters });
const draft = ref<ClientFilters>({ ...committed.value });
const filterOpen = ref(false);
const pagination = usePagination({ initialPageSize: saved?.pageSize });
watch([committed, pagination.pageSize], () => ui.setFilters('admin_client_list', { filters: committed.value, pageSize: pagination.pageSize.value }), { deep: true });

const listQuery = useQuery({
  queryKey: computed(() => ['clients', 'list', committed.value, pagination.pageIndex.value, pagination.pageSize.value, locale.value] as const),
  queryFn: ({ signal }) => orgApi.clients(committed.value, pagination.pageIndex.value + 1, pagination.pageSize.value, signal),
  placeholderData: keepPreviousData,
});
const summaryQuery = useQuery({
  queryKey: computed(() => ['clients', 'summary', committed.value] as const),
  queryFn: ({ signal }) => orgApi.clientSummary(committed.value, signal),
  placeholderData: keepPreviousData,
});
watch(
  () => listQuery.data.value?.totalCount,
  (total) => {
    if (total !== undefined) pagination.totalItems.value = total;
  },
  { immediate: true },
);

function commit(next: ClientFilters) {
  const same = JSON.stringify(next) === JSON.stringify(committed.value);
  committed.value = { ...next };
  pagination.goToPage(0);
  if (same) void listQuery.refetch();
}
function reset() {
  draft.value = { ...EMPTY_CLIENT_FILTERS };
  commit(EMPTY_CLIENT_FILTERS);
  filterOpen.value = false;
}
function clearFilter(id: string) {
  const key = id as keyof ClientFilters;
  const next = { ...committed.value, [key]: EMPTY_CLIENT_FILTERS[key] } as ClientFilters;
  draft.value = { ...next };
  commit(next);
}

const names = (ids: string[], rows: { id: string; name: string }[]) => ids.map((id) => rows.find((r) => r.id === id)?.name ?? id).join(', ');
const activeFilters = computed<ActiveFilter[]>(() => {
  const f = committed.value;
  const list: ActiveFilter[] = [];
  if (f.search) list.push({ id: 'search', label: t('admin_client_list.lbl_chip_keyword'), displayValue: f.search, icon: Search });
  if (f.countryIds.length) list.push({ id: 'countryIds', label: t('admin_client_list.lbl_country'), displayValue: names(f.countryIds, countries.value), icon: Globe });
  if (f.industryIds.length) list.push({ id: 'industryIds', label: t('admin_client_list.lbl_industry'), displayValue: names(f.industryIds, industries.value), icon: Building2 });
  if (f.status) list.push({ id: 'status', label: t('admin_client_list.lbl_status'), displayValue: statusOptions.value.find((o) => o.value === f.status)?.label ?? '', icon: ShieldCheck });
  if (f.deleted) list.push({ id: 'deleted', label: t('admin_client_list.lbl_chip_trash'), displayValue: t('admin_client_list.lbl_chip_viewing_trash'), icon: Trash2 });
  return list;
});
const advancedCount = computed(() => committed.value.countryIds.length + committed.value.industryIds.length + (committed.value.status ? 1 : 0) + (committed.value.deleted ? 1 : 0));

// ─── Thêm / sửa / xoá / khôi phục ───────────────────────────────────────────
const dialogOpen = ref(false);
const editing = ref<Client | null>(null);
const save = useFormMutation({
  mutationFn: (input: ClientInput) => (editing.value ? orgApi.updateClient(editing.value.id, input) : orgApi.createClient(input)),
  invalidate: [['clients']],
  onSuccess: () => (dialogOpen.value = false),
});
function openDialog(client: Client | null) {
  editing.value = client;
  save.fieldErrors.value = {};
  dialogOpen.value = true;
}

async function remove(client: Client) {
  const ok = await confirm.ask({
    title: client.isDeleted ? t('admin_client_list.confirm_purge_title') : t('admin_client_list.confirm_delete_title'),
    description: client.isDeleted ? t('admin_client_list.confirm_purge_desc', { name: client.name }) : t('admin_client_list.confirm_delete_desc', { name: client.name }),
    confirmText: client.isDeleted ? t('admin_client_list.btn_confirm_purge') : t('admin_client_list.btn_confirm_delete'),
    type: 'danger',
  });
  if (!ok) return;
  await run(() => orgApi.deleteClient(client.id));
}
async function restore(client: Client) {
  const ok = await confirm.ask({
    title: t('admin_client_list.confirm_restore_title'),
    description: t('admin_client_list.confirm_restore_desc', { name: client.name }),
    confirmText: t('admin_client_list.btn_restore'),
    type: 'info',
  });
  if (ok) await run(() => orgApi.restoreClient(client.id));
}
async function run(action: () => Promise<{ messageText: string }>) {
  try {
    toast.success((await action()).messageText);
    void queryClient.invalidateQueries({ queryKey: ['clients'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}

/** Bấm số dự án → mở danh sách dự án đã lọc theo khách hàng (giống handleViewProjects bản gốc). */
function viewProjects(client: Client) {
  ui.setFilters('project_list', { filters: { ...EMPTY_PROJECT_FILTERS, clientIds: [client.id] }, pageIndex: 0, pageSize: 20 });
  void router.push('/projects');
}

// ─── Xuất file ──────────────────────────────────────────────────────────────
const exportColumns = computed<ExportColumn<Client>[]>(() => [
  { id: 'name', header: t('admin_client_list.col_export_name'), accessorKey: 'name', width: 25 },
  { id: 'country', header: t('admin_client_list.col_export_country'), accessorFn: (r) => r.country?.name ?? '--', width: 20 },
  { id: 'industry', header: t('admin_client_list.col_export_industry'), accessorFn: (r) => r.industry?.name ?? '--', width: 20 },
  { id: 'contact_name', header: t('admin_client_list.col_export_contact'), accessorFn: (r) => r.contactName ?? '', width: 22 },
  { id: 'contact_email', header: t('admin_client_list.col_export_email'), accessorFn: (r) => r.contactEmail ?? '', width: 25 },
  { id: 'currency', header: t('admin_client_list.col_export_currency'), accessorFn: (r) => r.defaultCurrency ?? '--', width: 15 },
  {
    id: 'status',
    header: t('admin_client_list.col_export_status'),
    accessorFn: (r) => (r.isActive ? t('admin_client_list.opt_status_active') : t('admin_client_list.opt_status_inactive')),
    width: 18,
    conditionalFormat: (_v, r) => (r.isActive ? { color: '#059669', bold: true } : { color: '#dc2626', bold: true }),
  },
]);
async function exportData(mode: ExportMode): Promise<Client[]> {
  if (mode === 'current_page') return listQuery.data.value?.items ?? [];
  return (await orgApi.clients(mode === 'all' ? EMPTY_CLIENT_FILTERS : committed.value, 1, 0)).items;
}
const allCount = useQuery({ queryKey: ['clients', 'count-all'], queryFn: () => orgApi.clientSummary(EMPTY_CLIENT_FILTERS).then((s) => s.total) });
const summary = computed(() => summaryQuery.data.value);
</script>

<template>
  <PageLayout layout="D" ai-module="admin" content-padding="24px" disable-scroll chat-width="320px">
    <div class="h-full flex flex-col min-h-0 gap-3">
      <ListToolbar
        v-model:search="draft.search"
        v-model:filter-open="filterOpen"
        :title="$t('admin_client_list.title_page')"
        :kicker="$t('admin_client_list.lbl_kicker')"
        :search-placeholder="$t('admin_client_list.ph_search')"
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
            :default-filename="$t('admin_client_list.lbl_export_filename')"
            variant="ghost"
            size="sm"
            class="h-8 px-3 text-slate-600 hover:bg-white hover:text-indigo-600 hover:shadow-sm text-[12px] font-semibold"
            :label="$t('admin_client_list.btn_export')"
            :total-rows="allCount.data.value ?? 0"
            :filtered-rows="pagination.totalItems.value"
          >
            <template #icon><Download :size="13" /></template>
          </ExportButton>
        </template>
        <template #actions>
          <Button variant="primary" class="h-9 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" @click="openDialog(null)">
            <template #leftIcon><Plus :size="16" :stroke-width="3" /></template>
            {{ $t('admin_client_list.btn_create') }}
          </Button>
        </template>
        <template #filters>
          <FormSelect v-model="draft.countryIds" :label="$t('admin_client_list.lbl_country')" :placeholder="$t('admin_client_list.ph_all_countries')" :options="toOptions(countries)" searchable multiple hide-message trigger-class="h-10 rounded-xl text-[13px]" />
          <FormSelect v-model="draft.industryIds" :label="$t('admin_client_list.lbl_industry')" :placeholder="$t('admin_client_list.ph_all_industries')" :options="toOptions(industries)" searchable multiple hide-message trigger-class="h-10 rounded-xl text-[13px]" />
          <FormSelect
            :model-value="draft.status || null"
            :label="$t('admin_client_list.lbl_status')"
            :placeholder="$t('admin_client_list.ph_all_statuses')"
            :options="statusOptions"
            hide-message
            trigger-class="h-10 rounded-xl text-[13px]"
            @update:model-value="draft.status = (($event as string) || '') as ClientFilters['status']"
          />
          <div class="flex items-center justify-between p-2 bg-slate-50 rounded-xl border border-slate-100 mt-2">
            <label for="client-show-deleted" class="flex flex-col gap-0.5 cursor-pointer">
              <span class="text-[12px] font-bold text-slate-700">{{ $t('admin_client_list.lbl_trash') }}</span>
              <span class="text-[10px] text-slate-400">{{ $t('admin_client_list.hint_trash') }}</span>
            </label>
            <input
              id="client-show-deleted"
              v-model="draft.deleted"
              type="checkbox"
              class="h-5 w-9 shrink-0 cursor-pointer appearance-none rounded-full bg-slate-200 transition-all checked:bg-emerald-500 relative before:content-[''] before:absolute before:h-4 before:w-4 before:bg-white before:rounded-full before:left-0.5 before:top-0.5 before:transition-all checked:before:translate-x-4 shadow-inner"
            />
          </div>
        </template>
      </ListToolbar>

      <div class="grid grid-cols-2 lg:grid-cols-4 gap-px rounded-2xl border border-white/60 bg-white/40 backdrop-blur-xl shadow-xl shadow-slate-200/40 overflow-hidden">
        <StatSegment :label="$t('admin_client_list.stat_total')" :value="summary?.total ?? 0" tone="violet" :icon="Building2" :loading="!summary" />
        <StatSegment :label="$t('admin_client_list.stat_active')" :value="summary?.active ?? 0" tone="green" :icon="Globe" :delay="0.05" :loading="!summary" />
        <StatSegment :label="$t('admin_client_list.stat_inactive')" :value="summary?.inactive ?? 0" tone="red" :icon="Filter" :delay="0.1" :loading="!summary" />
        <StatSegment :label="$t('admin_client_list.stat_projects')" :value="summary?.totalProjects ?? 0" tone="amber" :icon="LayoutGrid" :delay="0.15" :loading="!summary" />
      </div>

      <div class="flex flex-1 flex-col gap-2 min-h-0">
        <ClientTable
          :clients="listQuery.data.value?.items ?? []"
          :loading="listQuery.isFetching.value"
          :pagination="pagination"
          @edit="openDialog"
          @delete="remove"
          @restore="restore"
          @view-projects="viewProjects"
        />
      </div>
    </div>

    <ClientDialog v-model:open="dialogOpen" :client="editing" :loading="save.isPending.value" :server-errors="save.fieldErrors.value" @submit="save.mutate" />
  </PageLayout>
</template>
