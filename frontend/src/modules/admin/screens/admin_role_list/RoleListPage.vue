<script setup lang="ts">
// Port RoleListPage.tsx — Quản trị → Quản lý vai trò: lọc (tên, quyền, loại), thống kê, bảng,
// gán người dùng vào vai trò, sửa quyền hàng loạt (ma trận vai trò × quyền), xoá vai trò tuỳ chỉnh.
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query';
import { Grid3x3, Layers, Plus, Search, ShieldCheck, Sparkles, Users } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import ListToolbar from '@/shared/components/filter/ListToolbar.vue';
import StatSegment from '@/shared/components/StatSegment.vue';
import Button from '@/shared/components/Button.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import type { ActiveFilter } from '@/shared/components/filter/types';
import RoleTable from './RoleTable.vue';
import AssignRoleUsersDialog from './AssignRoleUsersDialog.vue';
import BulkEditPermissionsDialog from './BulkEditPermissionsDialog.vue';
import { EMPTY_ROLE_FILTERS, systemApi, type Role, type RoleFilters } from '@/modules/admin/api/system.api';
import { usePagination } from '@/shared/composables/usePagination';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { useUiStore } from '@/stores/ui';

const { t, locale } = useI18n();
const router = useRouter();
const ui = useUiStore();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const permissions = useQuery({ queryKey: computed(() => ['permissions', locale.value] as const), queryFn: systemApi.permissions, staleTime: 10 * 60_000 });
const permissionOptions = computed(() => (permissions.data.value ?? []).map((p) => ({ label: p.key, value: p.key, description: p.name, icon: ShieldCheck })));
const typeOptions = computed(() => [
  { label: t('admin_role_list.opt_type_system'), value: 'system' },
  { label: t('admin_role_list.opt_type_custom'), value: 'custom' },
]);

const saved = ui.getFilters<{ filters: RoleFilters; pageSize: number }>('admin_role_list');
const committed = ref<RoleFilters>({ ...EMPTY_ROLE_FILTERS, ...saved?.filters });
const draft = ref<RoleFilters>({ ...committed.value });
const filterOpen = ref(false);
const pagination = usePagination({ initialPageSize: saved?.pageSize });
watch([committed, pagination.pageSize], () => ui.setFilters('admin_role_list', { filters: committed.value, pageSize: pagination.pageSize.value }), { deep: true });

const listQuery = useQuery({
  queryKey: computed(() => ['roles', 'list', committed.value, pagination.pageIndex.value, pagination.pageSize.value, locale.value] as const),
  queryFn: ({ signal }) => systemApi.roles(committed.value, pagination.pageIndex.value + 1, pagination.pageSize.value, signal),
  placeholderData: keepPreviousData,
});
const summaryQuery = useQuery({
  queryKey: computed(() => ['roles', 'summary', committed.value] as const),
  queryFn: ({ signal }) => systemApi.roleSummary(committed.value, signal),
  placeholderData: keepPreviousData,
});
watch(
  () => listQuery.data.value?.totalCount,
  (total) => {
    if (total !== undefined) pagination.totalItems.value = total;
  },
  { immediate: true },
);

function commit(next: RoleFilters) {
  const same = JSON.stringify(next) === JSON.stringify(committed.value);
  committed.value = { ...next, search: next.search.trim() };
  pagination.goToPage(0);
  if (same) void listQuery.refetch();
}
function reset() {
  draft.value = { ...EMPTY_ROLE_FILTERS };
  commit(EMPTY_ROLE_FILTERS);
  filterOpen.value = false;
}
function clearFilter(id: string) {
  const key = id as keyof RoleFilters;
  const next = { ...committed.value, [key]: EMPTY_ROLE_FILTERS[key] } as RoleFilters;
  draft.value = { ...next };
  commit(next);
}
const activeFilters = computed<ActiveFilter[]>(() => {
  const f = committed.value;
  const list: ActiveFilter[] = [];
  if (f.search) list.push({ id: 'search', label: t('admin_role_list.lbl_chip_name'), displayValue: f.search, icon: Search });
  if (f.permissions.length) {
    list.push({ id: 'permissions', label: t('admin_role_list.lbl_permission'), displayValue: f.permissions.slice(0, 2).join(', ') + (f.permissions.length > 2 ? ` +${f.permissions.length - 2}` : ''), icon: ShieldCheck });
  }
  if (f.types.length) list.push({ id: 'types', label: t('admin_role_list.lbl_type'), displayValue: f.types.map((v) => typeOptions.value.find((o) => o.value === v)?.label).join(', '), icon: Layers });
  return list;
});

// ─── Thao tác ───────────────────────────────────────────────────────────────
const assignRole = ref<Role | null>(null);
const bulkOpen = ref(false);

async function remove(role: Role) {
  if (role.isSystem) return;
  const ok = await confirm.ask({
    title: t('admin_role_list.confirm_delete_title'),
    description: role.memberCount > 0 ? t('admin_role_list.confirm_delete_members_desc', { name: role.name, count: role.memberCount }) : t('admin_role_list.confirm_delete_desc', { name: role.name }),
    confirmText: t('admin_role_list.btn_confirm_delete'),
    type: 'danger',
  });
  if (!ok) return;
  try {
    toast.success((await systemApi.deleteRole(role.id)).messageText);
    void queryClient.invalidateQueries({ queryKey: ['roles'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const summary = computed(() => summaryQuery.data.value);
</script>

<template>
  <PageLayout layout="C" content-padding="24px" disable-scroll>
    <div class="h-full flex flex-col min-h-0 gap-3">
      <ListToolbar
        v-model:search="draft.search"
        v-model:filter-open="filterOpen"
        :title="$t('admin_role_list.title_page')"
        :kicker="$t('admin_role_list.lbl_kicker')"
        :search-placeholder="$t('admin_role_list.ph_search')"
        :active-filters="activeFilters"
        :advanced-filter-count="committed.permissions.length + committed.types.length"
        :is-fetching="listQuery.isFetching.value"
        @search="commit(draft)"
        @apply-filters="commit(draft); filterOpen = false"
        @reset="reset"
        @clear-filter="clearFilter"
      >
        <template #tools>
          <Button variant="ghost" size="sm" class="h-8 px-3 text-[12px] font-semibold text-slate-600 hover:bg-white hover:text-pr hover:shadow-sm" @click="bulkOpen = true">
            <template #leftIcon><Grid3x3 :size="14" /></template>
            {{ $t('admin_role_list.btn_bulk_edit') }}
          </Button>
        </template>
        <template #actions>
          <Button variant="primary" class="h-9 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" @click="router.push('/admin/roles/new')">
            <template #leftIcon><Plus :size="16" :stroke-width="3" /></template>
            {{ $t('admin_role_list.btn_create') }}
          </Button>
        </template>
        <template #filters>
          <FormSelect v-model="draft.permissions" :label="$t('admin_role_list.lbl_permission')" :placeholder="$t('admin_role_list.ph_all_permissions')" :options="permissionOptions" searchable multiple hide-message trigger-class="h-10 rounded-xl text-[13px]" />
          <FormSelect
            :model-value="draft.types"
            :label="$t('admin_role_list.lbl_type')"
            :placeholder="$t('admin_role_list.ph_all_types')"
            :options="typeOptions"
            multiple
            hide-message
            trigger-class="h-10 rounded-xl text-[13px]"
            @update:model-value="draft.types = ($event as RoleFilters['types']) ?? []"
          />
        </template>
      </ListToolbar>

      <div class="grid grid-cols-2 lg:grid-cols-4 gap-px rounded-2xl border border-white/60 bg-white/40 backdrop-blur-xl shadow-xl shadow-slate-200/40 overflow-hidden">
        <StatSegment :label="$t('admin_role_list.stat_total')" :value="summary?.total ?? 0" tone="violet" :icon="ShieldCheck" :loading="!summary" />
        <StatSegment :label="$t('admin_role_list.stat_system')" :value="summary?.system ?? 0" tone="slate" :icon="Layers" :delay="0.05" :loading="!summary" />
        <StatSegment :label="$t('admin_role_list.stat_custom')" :value="summary?.custom ?? 0" tone="amber" :icon="Sparkles" :delay="0.1" :loading="!summary" />
        <StatSegment :label="$t('admin_role_list.stat_assigned')" :value="summary?.assignedUsers ?? 0" tone="green" :icon="Users" :delay="0.15" :loading="!summary" />
      </div>

      <div class="flex flex-1 flex-col gap-2 min-h-0">
        <RoleTable
          :roles="listQuery.data.value?.items ?? []"
          :loading="listQuery.isFetching.value"
          :pagination="pagination"
          @view="(r) => router.push(`/admin/roles/${r.id}`)"
          @assign="(r) => (assignRole = r)"
          @delete="remove"
        />
      </div>
    </div>

    <AssignRoleUsersDialog :role="assignRole" @close="assignRole = null" />
    <BulkEditPermissionsDialog v-model:open="bulkOpen" />
  </PageLayout>
</template>
