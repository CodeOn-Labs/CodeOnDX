<script setup lang="ts">
// Port CategoriesListPage.tsx — Quản trị → Danh mục hệ thống (master data theo tenant, D12).
// Mức hiệu suất (performance_level) và hệ số của nó được sửa ở đây; công thức dùng hệ số nằm ở modules/effort/utils.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/vue-query';
import { Activity, Search } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import type { ActiveFilter } from '@/shared/components/filter/types';
import CategoryFilterBar from './CategoryFilterBar.vue';
import CategoryTable from './CategoryTable.vue';
import CategoryDialog from './CategoryDialog.vue';
import { configApi, firstFieldErrors, type Category, type CategoryFilters, type CategoryInput } from '@/modules/admin/api/config.api';
import { usePagination } from '@/shared/composables/usePagination';
import { enumLabel } from '@/shared/i18n/enum-label';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';

const EMPTY: CategoryFilters = { search: '', types: [] };

const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const committed = ref<CategoryFilters>({ ...EMPTY });
const draft = ref<CategoryFilters>({ ...EMPTY });
const filterOpen = ref(false);
const pagination = usePagination();

const listQuery = useQuery({
  queryKey: computed(() => ['categories', committed.value, pagination.pageIndex.value, pagination.pageSize.value] as const),
  queryFn: ({ signal }) => configApi.categories(committed.value, pagination.pageIndex.value + 1, pagination.pageSize.value, signal),
  placeholderData: keepPreviousData,
});
watch(
  () => listQuery.data.value?.totalCount,
  (total) => {
    if (total !== undefined) pagination.totalItems.value = total;
  },
  { immediate: true },
);

const activeFilters = computed<ActiveFilter[]>(() => {
  const list: ActiveFilter[] = [];
  if (committed.value.search) list.push({ id: 'search', label: t('admin_category_list.lbl_chip_keyword'), displayValue: committed.value.search, icon: Search });
  if (committed.value.types.length) {
    list.push({
      id: 'types',
      label: t('admin_category_list.lbl_chip_type'),
      displayValue: committed.value.types.map((type) => enumLabel('category_type', type)).join(', '),
      icon: Activity,
    });
  }
  return list;
});

function commit(next: CategoryFilters) {
  const same = JSON.stringify(next) === JSON.stringify(committed.value);
  committed.value = { ...next };
  pagination.goToPage(0);
  if (same) void listQuery.refetch();
}
function reset() {
  draft.value = { ...EMPTY };
  commit(EMPTY);
  filterOpen.value = false;
}
function clearFilter(id: string) {
  const next: CategoryFilters = id === 'types' ? { ...committed.value, types: [] } : { ...committed.value, search: '' };
  draft.value = { ...next };
  commit(next);
}

// ─── Thêm / sửa / xoá ───────────────────────────────────────────────────────
const dialogOpen = ref(false);
const editing = ref<Category | null>(null);
const fieldErrors = ref<Record<string, string>>({});

/** Danh mục đổi → làm mới cả danh sách quản trị lẫn danh mục dùng chung ở các màn khác. */
function refreshCaches() {
  void queryClient.invalidateQueries({ queryKey: ['categories'] });
  void queryClient.invalidateQueries({ queryKey: ['master-data'] });
}

const saveMutation = useMutation({
  mutationFn: (input: CategoryInput) => (editing.value ? configApi.updateCategory(editing.value.id, input) : configApi.createCategory(input)),
  onSuccess: (res) => {
    toast.success(res.messageText);
    dialogOpen.value = false;
    refreshCaches();
  },
  onError: (err: Error) => {
    fieldErrors.value = firstFieldErrors(err);
    if (Object.keys(fieldErrors.value).length === 0) toast.error(err.message);
  },
});

function openDialog(item: Category | null) {
  editing.value = item;
  fieldErrors.value = {};
  dialogOpen.value = true;
}

async function remove(item: Category) {
  const ok = await confirm.ask({
    title: t('admin_category_list.confirm_delete_title'),
    description: t('admin_category_list.confirm_delete_desc', { name: item.name }),
    confirmText: t('admin_category_list.btn_confirm_delete'),
    type: 'danger',
  });
  if (!ok) return;
  try {
    const res = await configApi.deleteCategory(item.id);
    toast.success(res.messageText);
    refreshCaches();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
</script>

<template>
  <PageLayout layout="C" content-padding="24px" disable-scroll>
    <div class="h-full flex flex-col min-h-0">
      <div class="flex min-h-0 flex-col gap-3 h-full">
        <CategoryFilterBar
          v-model:draft="draft"
          v-model:filter-open="filterOpen"
          :active-filters="activeFilters"
          :advanced-filter-count="committed.types.length"
          :is-fetching="listQuery.isFetching.value"
          @search="commit(draft)"
          @apply-filters="commit(draft); filterOpen = false"
          @reset="reset"
          @clear-filter="clearFilter"
          @new-category="openDialog(null)"
        />
        <div class="flex flex-1 flex-col gap-2 min-h-0">
          <CategoryTable
            :items="listQuery.data.value?.items ?? []"
            :loading="listQuery.isLoading.value || listQuery.isFetching.value"
            :pagination="pagination"
            @edit="openDialog"
            @delete="remove"
          />
        </div>
      </div>
    </div>

    <CategoryDialog
      v-model:open="dialogOpen"
      :item="editing"
      :default-type="committed.types.length === 1 ? committed.types[0] : undefined"
      :loading="saveMutation.isPending.value"
      :server-errors="fieldErrors"
      @submit="saveMutation.mutate"
    />
  </PageLayout>
</template>
