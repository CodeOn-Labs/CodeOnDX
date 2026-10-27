<script setup lang="ts">
// Port PromptTemplateListPage.tsx — Quản trị → Prompt Templates: lọc theo phạm vi/trạng thái, bảng, xoá.
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query';
import { ArrowUpRight, Layers, Pencil, Plus, Search, ShieldCheck, Trash2 } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import ListToolbar from '@/shared/components/filter/ListToolbar.vue';
import Button from '@/shared/components/Button.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import CommonTable from '@/shared/components/table/CommonTable.vue';
import type { TableColumn } from '@/shared/components/table/types';
import type { ActiveFilter } from '@/shared/components/filter/types';
import { systemApi, type PromptStatus, type PromptTemplate, type PromptTemplateFilters } from '@/modules/admin/api/system.api';
import { enumLabel } from '@/shared/i18n/enum-label';
import { screenTitle } from '@/shared/i18n/screen-title';
import { usePagination } from '@/shared/composables/usePagination';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const EMPTY: PromptTemplateFilters = { search: '', scopes: [], statuses: [] };
const SCOPES = ['insight', 'chat', 'translate'] as const;
const STATUSES = ['draft', 'active', 'archived'] as const;
const { t } = useI18n();
const router = useRouter();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const scopeOptions = computed(() => SCOPES.map((s) => ({ label: enumLabel('prompt_scope', s), value: s })));
const statusOptions = computed(() => STATUSES.map((s) => ({ label: enumLabel('prompt_status', s), value: s })));

const committed = ref<PromptTemplateFilters>({ ...EMPTY });
const draft = ref<PromptTemplateFilters>({ ...EMPTY });
const filterOpen = ref(false);
const pagination = usePagination();
const listQuery = useQuery({
  queryKey: computed(() => ['prompt-templates', committed.value, pagination.pageIndex.value, pagination.pageSize.value] as const),
  queryFn: ({ signal }) => systemApi.promptTemplates(committed.value, pagination.pageIndex.value + 1, pagination.pageSize.value, signal),
  placeholderData: keepPreviousData,
});
watch(
  () => listQuery.data.value?.totalCount,
  (total) => {
    if (total !== undefined) pagination.totalItems.value = total;
  },
  { immediate: true },
);
function commit(next: PromptTemplateFilters) {
  committed.value = { ...next };
  pagination.goToPage(0);
}
function reset() {
  draft.value = { ...EMPTY };
  commit(EMPTY);
  filterOpen.value = false;
}
function clearFilter(id: string) {
  const key = id as keyof PromptTemplateFilters;
  const next = { ...committed.value, [key]: EMPTY[key] } as PromptTemplateFilters;
  draft.value = { ...next };
  commit(next);
}
const activeFilters = computed<ActiveFilter[]>(() => {
  const f = committed.value;
  const list: ActiveFilter[] = [];
  if (f.search) list.push({ id: 'search', label: t('admin_prompt_template_list.lbl_chip_keyword'), displayValue: f.search, icon: Search });
  if (f.scopes.length) list.push({ id: 'scopes', label: t('admin_prompt_template_list.lbl_scope'), displayValue: f.scopes.map((s) => enumLabel('prompt_scope', s)).join(', '), icon: Layers });
  if (f.statuses.length) list.push({ id: 'statuses', label: t('admin_prompt_template_list.lbl_status'), displayValue: f.statuses.map((s) => enumLabel('prompt_status', s)).join(', '), icon: ShieldCheck });
  return list;
});

const columns = computed<TableColumn[]>(() => [
  { id: 'name', header: t('admin_prompt_template_list.col_name') },
  { id: 'scope', header: t('admin_prompt_template_list.col_scope'), headerClass: 'w-[120px]' },
  { id: 'screens', header: t('admin_prompt_template_list.col_screens') },
  { id: 'status', header: t('admin_prompt_template_list.col_status'), headerClass: 'w-[120px]' },
  { id: 'updated', header: t('admin_prompt_template_list.col_updated'), headerClass: 'w-[140px]' },
  { id: 'actions', header: '', cellClass: 'text-right w-[110px]' },
]);
const STATUS_CLASS: Record<PromptStatus, string> = {
  active: 'bg-emerald-50 text-emerald-600 ring-emerald-500/20',
  draft: 'bg-amber-50 text-amber-600 ring-amber-500/20',
  archived: 'bg-slate-100 text-slate-500 ring-slate-500/10',
};

async function remove(item: PromptTemplate) {
  const ok = await confirm.ask({
    title: t('admin_prompt_template_list.confirm_delete_title'),
    description: t('admin_prompt_template_list.confirm_delete_desc', { name: item.name }),
    confirmText: t('admin_prompt_template_list.btn_confirm_delete'),
    type: 'danger',
  });
  if (!ok) return;
  try {
    toast.success((await systemApi.deletePromptTemplate(item.id)).messageText);
    void queryClient.invalidateQueries({ queryKey: ['prompt-templates'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}
</script>

<template>
  <PageLayout layout="C" content-padding="24px" disable-scroll>
    <div class="h-full flex flex-col min-h-0 gap-3">
      <ListToolbar
        v-model:search="draft.search"
        v-model:filter-open="filterOpen"
        :title="$t('admin_prompt_template_list.title_page')"
        :kicker="$t('admin_prompt_template_list.lbl_kicker')"
        :search-placeholder="$t('admin_prompt_template_list.ph_search')"
        :active-filters="activeFilters"
        :advanced-filter-count="committed.scopes.length + committed.statuses.length"
        :is-fetching="listQuery.isFetching.value"
        @search="commit(draft)"
        @apply-filters="commit(draft); filterOpen = false"
        @reset="reset"
        @clear-filter="clearFilter"
      >
        <template #actions>
          <Button variant="primary" class="h-9 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" @click="router.push('/admin/prompt-templates/new')">
            <template #leftIcon><Plus :size="16" :stroke-width="3" /></template>
            {{ $t('admin_prompt_template_list.btn_create') }}
          </Button>
        </template>
        <template #filters>
          <FormSelect v-model="draft.scopes" :label="$t('admin_prompt_template_list.lbl_scope')" :placeholder="$t('admin_prompt_template_list.ph_all')" :options="scopeOptions" multiple hide-message trigger-class="h-10 rounded-xl text-[13px]" />
          <FormSelect v-model="draft.statuses" :label="$t('admin_prompt_template_list.lbl_status')" :placeholder="$t('admin_prompt_template_list.ph_all')" :options="statusOptions" multiple hide-message trigger-class="h-10 rounded-xl text-[13px]" />
        </template>
      </ListToolbar>

      <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
        <CommonTable
          :columns="columns"
          :rows="listQuery.data.value?.items ?? []"
          :loading="listQuery.isFetching.value"
          :empty-message="$t('admin_prompt_template_list.empty_no_template')"
          :pagination="pagination"
          table-class="min-w-[900px]"
          container-class="flex-1 min-h-0"
          table-wrapper-class="border-none shadow-none bg-transparent"
          :row-class="() => 'transition-all duration-300 hover:!border-pr'"
        >
          <template #cell-name="{ row }">
            <button type="button" class="group/link flex flex-col gap-1 py-2 text-left outline-none" @click="router.push(`/admin/prompt-templates/${row.id}`)">
              <span class="inline-flex items-center gap-1.5 text-[13px] font-extrabold text-slate-900 transition-colors group-hover/link:text-pr">
                {{ row.name }}
                <span class="rounded bg-slate-100 px-1 text-[9px] font-black text-slate-400">v{{ row.version }}</span>
                <ArrowUpRight :size="14" class="opacity-0 transition-all duration-300 group-hover/link:opacity-100 text-pr" />
              </span>
              <span class="line-clamp-1 max-w-[360px] text-[11px] font-medium text-slate-400">{{ row.description || $t('admin_prompt_template_list.lbl_no_description') }}</span>
            </button>
          </template>
          <template #cell-scope="{ row }">
            <span class="inline-flex rounded-lg bg-indigo-50 px-2 py-0.5 text-[11px] font-bold text-indigo-600 ring-1 ring-indigo-100">{{ enumLabel('prompt_scope', row.scope) }}</span>
          </template>
          <template #cell-screens="{ row }">
            <span v-if="row.screenKeys.length === 0" class="text-[11px] font-semibold italic text-slate-400">{{ $t('admin_prompt_template_list.lbl_all_screens') }}</span>
            <div v-else class="flex max-w-[280px] flex-wrap gap-1">
              <span v-for="s in row.screenKeys.slice(0, 2)" :key="s" class="rounded-md bg-slate-100 px-1.5 py-0.5 text-[10px] font-bold text-slate-600">{{ screenTitle(s) }}</span>
              <span v-if="row.screenKeys.length > 2" class="text-[10px] font-black text-slate-400">+{{ row.screenKeys.length - 2 }}</span>
            </div>
          </template>
          <template #cell-status="{ row }">
            <span :class="cn('inline-flex rounded-full px-2.5 py-0.5 text-[10px] font-black uppercase tracking-widest ring-1 ring-inset', STATUS_CLASS[row.status])">{{ enumLabel('prompt_status', row.status) }}</span>
          </template>
          <template #cell-updated="{ row }"><span class="text-[11px] font-semibold text-slate-500">{{ formatDate(row.updatedAt, 'dd/MM/yyyy HH:mm') || '—' }}</span></template>
          <template #cell-actions="{ row }">
            <div class="flex justify-end gap-1">
              <button type="button" class="p-2 rounded-xl text-slate-400 transition-all hover:scale-110 hover:bg-indigo-50 hover:text-indigo-500" :title="$t('admin_prompt_template_list.btn_edit')" @click="router.push(`/admin/prompt-templates/${row.id}`)">
                <Pencil :size="15" />
              </button>
              <button type="button" class="p-2 rounded-xl text-slate-400 transition-all hover:scale-110 hover:bg-rose-50 hover:text-rose-500" :title="$t('admin_prompt_template_list.btn_delete')" @click="remove(row)">
                <Trash2 :size="15" />
              </button>
            </div>
          </template>
        </CommonTable>
      </div>
    </div>
  </PageLayout>
</template>
