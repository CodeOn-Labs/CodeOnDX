<script setup lang="ts">
// Port InsightListPage.tsx + InsightFilterBar.tsx + InsightTable.tsx — Quản trị → AI Insights.
// "Màn hình áp dụng" là screen_key trong shared/screens.json (bản gốc dùng mã module riêng như ProjectList, HRList…).
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query';
import { Activity, ArrowUpRight, Pencil, Plus, Search, ShieldCheck, Trash2 } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import ListToolbar from '@/shared/components/filter/ListToolbar.vue';
import Button from '@/shared/components/Button.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import CommonTable from '@/shared/components/table/CommonTable.vue';
import type { TableColumn } from '@/shared/components/table/types';
import type { ActiveFilter } from '@/shared/components/filter/types';
import { systemApi, type InsightConfig, type InsightConfigFilters } from '@/modules/admin/api/system.api';
import { SCREENS, type ScreenKey } from '@/app/screens';
import { enumLabel } from '@/shared/i18n/enum-label';
import { screenTitle } from '@/shared/i18n/screen-title';
import { usePagination } from '@/shared/composables/usePagination';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const EMPTY: InsightConfigFilters = { search: '', screens: [], statuses: [] };
const { t } = useI18n();
const router = useRouter();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const screenOptions = computed(() => (Object.keys(SCREENS) as ScreenKey[]).filter((k) => !SCREENS[k].public).map((k) => ({ label: screenTitle(k), value: k, description: k })));
const statusOptions = computed(() => [
  { label: t('admin_insight_list.opt_status_active'), value: 'active' },
  { label: t('admin_insight_list.opt_status_paused'), value: 'paused' },
]);

const committed = ref<InsightConfigFilters>({ ...EMPTY });
const draft = ref<InsightConfigFilters>({ ...EMPTY });
const filterOpen = ref(false);
const pagination = usePagination();
const listQuery = useQuery({
  queryKey: computed(() => ['insight-configs', committed.value, pagination.pageIndex.value, pagination.pageSize.value] as const),
  queryFn: ({ signal }) => systemApi.insightConfigs(committed.value, pagination.pageIndex.value + 1, pagination.pageSize.value, signal),
  placeholderData: keepPreviousData,
});
watch(
  () => listQuery.data.value?.totalCount,
  (total) => {
    if (total !== undefined) pagination.totalItems.value = total;
  },
  { immediate: true },
);
function commit(next: InsightConfigFilters) {
  committed.value = { ...next };
  pagination.goToPage(0);
}
function reset() {
  draft.value = { ...EMPTY };
  commit(EMPTY);
  filterOpen.value = false;
}
function clearFilter(id: string) {
  const key = id as keyof InsightConfigFilters;
  const next = { ...committed.value, [key]: EMPTY[key] } as InsightConfigFilters;
  draft.value = { ...next };
  commit(next);
}
const activeFilters = computed<ActiveFilter[]>(() => {
  const f = committed.value;
  const list: ActiveFilter[] = [];
  if (f.search) list.push({ id: 'search', label: t('admin_insight_list.lbl_chip_keyword'), displayValue: f.search, icon: Search });
  if (f.screens.length) list.push({ id: 'screens', label: t('admin_insight_list.lbl_screen'), displayValue: f.screens.map((s) => screenTitle(s as ScreenKey)).join(', '), icon: Activity });
  if (f.statuses.length) list.push({ id: 'statuses', label: t('admin_insight_list.lbl_status'), displayValue: f.statuses.map((s) => statusOptions.value.find((o) => o.value === s)?.label).join(', '), icon: ShieldCheck });
  return list;
});

const columns = computed<TableColumn[]>(() => [
  { id: 'name', header: t('admin_insight_list.col_name') },
  { id: 'screen', header: t('admin_insight_list.col_screen') },
  { id: 'condition', header: t('admin_insight_list.col_condition') },
  { id: 'sources', header: t('admin_insight_list.col_sources') },
  { id: 'status', header: t('admin_insight_list.col_status'), headerClass: 'w-[120px]' },
  { id: 'actions', header: '', cellClass: 'text-right w-[110px]' },
]);

async function toggle(item: InsightConfig) {
  const { id: _id, updatedAt: _u, ...input } = item;
  try {
    await systemApi.updateInsightConfig(item.id, { ...input, status: item.status === 'active' ? 'paused' : 'active' });
    toast.success(item.status === 'active' ? t('admin_insight_list.msg_disabled') : t('admin_insight_list.msg_enabled'));
    void queryClient.invalidateQueries({ queryKey: ['insight-configs'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}
async function remove(item: InsightConfig) {
  const ok = await confirm.ask({
    title: t('admin_insight_list.confirm_delete_title'),
    description: t('admin_insight_list.confirm_delete_desc', { name: item.name }),
    confirmText: t('admin_insight_list.btn_confirm_delete'),
    type: 'danger',
  });
  if (!ok) return;
  try {
    toast.success((await systemApi.deleteInsightConfig(item.id)).messageText);
    void queryClient.invalidateQueries({ queryKey: ['insight-configs'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}
</script>

<template>
  <PageLayout layout="D" ai-module="admin" content-padding="24px" disable-scroll chat-width="320px">
    <div class="h-full flex flex-col min-h-0 gap-3">
      <ListToolbar
        v-model:search="draft.search"
        v-model:filter-open="filterOpen"
        :title="$t('admin_insight_list.title_page')"
        :kicker="$t('admin_insight_list.lbl_kicker')"
        :search-placeholder="$t('admin_insight_list.ph_search')"
        :active-filters="activeFilters"
        :advanced-filter-count="committed.screens.length + committed.statuses.length"
        :is-fetching="listQuery.isFetching.value"
        @search="commit(draft)"
        @apply-filters="commit(draft); filterOpen = false"
        @reset="reset"
        @clear-filter="clearFilter"
      >
        <template #actions>
          <Button variant="primary" class="h-9 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" @click="router.push('/admin/insights/new')">
            <template #leftIcon><Plus :size="16" :stroke-width="3" /></template>
            {{ $t('admin_insight_list.btn_create') }}
          </Button>
        </template>
        <template #filters>
          <FormSelect v-model="draft.screens" :label="$t('admin_insight_list.lbl_screen')" :placeholder="$t('admin_insight_list.ph_all')" :options="screenOptions" searchable multiple hide-message trigger-class="h-10 rounded-xl text-[13px]" />
          <FormSelect v-model="draft.statuses" :label="$t('admin_insight_list.lbl_status')" :placeholder="$t('admin_insight_list.ph_all')" :options="statusOptions" multiple hide-message trigger-class="h-10 rounded-xl text-[13px]" />
        </template>
      </ListToolbar>

      <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
        <CommonTable
          :columns="columns"
          :rows="listQuery.data.value?.items ?? []"
          :loading="listQuery.isFetching.value"
          :empty-message="$t('admin_insight_list.empty_no_insight')"
          :pagination="pagination"
          table-class="min-w-[1000px]"
          container-class="flex-1 min-h-0"
          table-wrapper-class="border-none shadow-none bg-transparent"
          :row-class="(r) => cn('transition-all duration-300', r.status === 'active' ? 'hover:!border-pr' : 'opacity-70 hover:!border-slate-300')"
        >
          <template #cell-name="{ row }">
            <button
              type="button"
              class="group/link flex flex-col gap-1 py-2 text-left outline-none"
              @click="router.push(`/admin/insights/${row.id}`)"
            >
              <span class="inline-flex items-center gap-1.5 text-[13px] font-extrabold text-slate-900 transition-colors group-hover/link:text-pr">
                {{ row.name }}
                <ArrowUpRight :size="14" class="opacity-0 transition-all duration-300 group-hover/link:opacity-100 text-pr" />
              </span>
              <span class="line-clamp-1 max-w-[360px] text-[11px] font-medium text-slate-400">{{ row.dataPrompt }}</span>
            </button>
          </template>
          <template #cell-screen="{ row }">
            <span class="inline-flex rounded-lg bg-indigo-50 px-2 py-0.5 text-[11px] font-bold text-indigo-600 ring-1 ring-indigo-100">{{ screenTitle(row.screenKey) }}</span>
          </template>
          <template #cell-condition="{ row }">
            <span class="text-[12px] font-semibold text-slate-600">{{ enumLabel('insight_condition', row.triggerCondition) }}</span>
          </template>
          <template #cell-sources="{ row }">
            <div class="flex max-w-[260px] flex-wrap gap-1">
              <span v-for="s in row.dataSources.slice(0, 3)" :key="s" class="rounded-md bg-slate-100 px-1.5 py-0.5 text-[10px] font-bold text-slate-600">{{ enumLabel('insight_data_source', s) }}</span>
              <span v-if="row.dataSources.length > 3" class="text-[10px] font-black text-slate-400">+{{ row.dataSources.length - 3 }}</span>
            </div>
          </template>
          <template #cell-status="{ row }">
            <div class="flex items-center gap-2">
              <SwitchToggle :model-value="row.status === 'active'" size="sm" :label="$t('admin_insight_list.tip_toggle')" @update:model-value="toggle(row)" />
              <span :class="cn('text-[10px] font-black uppercase tracking-wider', row.status === 'active' ? 'text-emerald-600' : 'text-slate-400')">
                {{ row.status === 'active' ? $t('admin_insight_list.opt_status_active') : $t('admin_insight_list.opt_status_paused') }}
              </span>
            </div>
          </template>
          <template #cell-actions="{ row }">
            <div class="flex justify-end gap-1">
              <button type="button" class="p-2 rounded-xl text-slate-400 transition-all hover:scale-110 hover:bg-indigo-50 hover:text-indigo-500" :title="$t('admin_insight_list.btn_edit')" @click="router.push(`/admin/insights/${row.id}`)">
                <Pencil :size="15" />
              </button>
              <button type="button" class="p-2 rounded-xl text-slate-400 transition-all hover:scale-110 hover:bg-rose-50 hover:text-rose-500" :title="$t('admin_insight_list.btn_delete')" @click="remove(row)">
                <Trash2 :size="15" />
              </button>
            </div>
          </template>
        </CommonTable>
      </div>
    </div>
  </PageLayout>
</template>
