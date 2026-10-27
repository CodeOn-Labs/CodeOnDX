<script setup lang="ts">
// Port RulesListPage.tsx — Quản trị → Quy tắc hệ thống.
// Khoá quy tắc cố định trong code (BE), quản trị chỉ chỉnh giá trị và bật/tắt. Danh sách nhỏ nên lọc + phân trang ở FE.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useMutation, useQueryClient } from '@tanstack/vue-query';
import { Layout, Search, ShieldAlert } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import type { ActiveFilter } from '@/shared/components/filter/types';
import RuleFilterBar from './RuleFilterBar.vue';
import type { RuleFilters } from './types';
import RuleTable from './RuleTable.vue';
import RuleDialog from './RuleDialog.vue';
import { configApi } from '@/modules/admin/api/config.api';
import type { SystemRule } from '@/api/types';
import { useSystemRules } from '@/shared/composables/useMasterData';
import { usePagination } from '@/shared/composables/usePagination';
import { enumLabel } from '@/shared/i18n/enum-label';
import { useToastStore } from '@/stores/toast';
import { useAuthStore } from '@/stores/auth';

const EMPTY: RuleFilters = { search: '', modules: [] };

const { t } = useI18n();
const toast = useToastStore();
const auth = useAuthStore();
const queryClient = useQueryClient();
const canManage = computed(() => auth.can('admin.settings'));

const rulesQuery = useSystemRules();
const committed = ref<RuleFilters>({ ...EMPTY });
const draft = ref<RuleFilters>({ ...EMPTY });
const filterOpen = ref(false);
const pagination = usePagination();

const filtered = computed(() => {
  const q = committed.value.search.trim().toLowerCase();
  return (rulesQuery.data.value ?? []).filter(
    (r) =>
      (!q || r.name.toLowerCase().includes(q) || r.key.toLowerCase().includes(q)) &&
      (committed.value.modules.length === 0 || committed.value.modules.includes(r.module)),
  );
});
watch(filtered, (rows) => (pagination.totalItems.value = rows.length), { immediate: true });
const pageRows = computed(() => {
  const start = pagination.pageIndex.value * pagination.pageSize.value;
  return filtered.value.slice(start, start + pagination.pageSize.value);
});

const moduleOptions = computed(() =>
  [...new Set((rulesQuery.data.value ?? []).map((r) => r.module))].map((m) => ({ value: m, label: enumLabel('rule_module', m) })),
);

const activeFilters = computed<ActiveFilter[]>(() => {
  const list: ActiveFilter[] = [];
  if (committed.value.search) list.push({ id: 'search', label: t('admin_rule_list.lbl_chip_keyword'), displayValue: committed.value.search, icon: Search });
  if (committed.value.modules.length) {
    list.push({
      id: 'modules',
      label: t('admin_rule_list.lbl_chip_module'),
      displayValue: committed.value.modules.map((m) => enumLabel('rule_module', m)).join(', '),
      icon: Layout,
    });
  }
  return list;
});

function commit(next: RuleFilters) {
  committed.value = { ...next };
  pagination.goToPage(0);
}
function reset() {
  draft.value = { ...EMPTY };
  commit(EMPTY);
  filterOpen.value = false;
}
function clearFilter(id: string) {
  const next = { ...committed.value, [id]: id === 'modules' ? [] : '' };
  draft.value = { ...draft.value, [id]: next[id as keyof RuleFilters] };
  commit(next);
}

// ─── Lưu ────────────────────────────────────────────────────────────────────
const dialogOpen = ref(false);
const selected = ref<SystemRule | null>(null);

const saveMutation = useMutation({
  mutationFn: (input: { key: string; value: number | boolean; status: number }) => configApi.updateRule(input.key, input.value, input.status),
  onSuccess: (res) => {
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['system-rules'] });
  },
  onError: (err: Error) => toast.error(err.message),
});

function openEdit(rule: SystemRule) {
  selected.value = rule;
  dialogOpen.value = true;
}

async function save(input: { value: number | boolean; status: number }) {
  if (!selected.value) return;
  await saveMutation.mutateAsync({ key: selected.value.key, ...input }).then(
    () => (dialogOpen.value = false),
    () => undefined,
  );
}

function toggleStatus(rule: SystemRule) {
  saveMutation.mutate({ key: rule.key, value: rule.value ?? rule.defaultValue ?? 0, status: rule.status === 0 ? 1 : 0 });
}
</script>

<template>
  <PageLayout layout="C" content-padding="24px" disable-scroll>
    <div class="h-full flex flex-col min-h-0">
      <div class="flex min-h-0 flex-col gap-3 h-full">
        <RuleFilterBar
          v-model:draft="draft"
          v-model:filter-open="filterOpen"
          :active-filters="activeFilters"
          :advanced-filter-count="committed.modules.length"
          :is-fetching="rulesQuery.isFetching.value"
          :module-options="moduleOptions"
          @search="commit(draft)"
          @apply-filters="commit(draft); filterOpen = false"
          @reset="reset"
          @clear-filter="clearFilter"
        />

        <div class="flex flex-1 flex-col gap-2 min-h-0">
          <div
            v-if="rulesQuery.isError.value"
            class="p-6 border border-rose-100 bg-rose-50/50 rounded-2xl flex flex-col items-center gap-4 text-center max-w-md mx-auto my-12"
          >
            <ShieldAlert :size="40" class="text-rose-500" />
            <div class="space-y-1">
              <h3 class="text-sm font-black text-rose-800 uppercase tracking-wider">{{ $t('admin_rule_list.err_load_title') }}</h3>
              <p class="text-[12px] text-rose-600">{{ rulesQuery.error.value?.message || $t('admin_rule_list.err_load_desc') }}</p>
            </div>
            <Button variant="outline" class="h-9 rounded-xl font-bold text-[12px]" @click="rulesQuery.refetch()">
              {{ $t('admin_rule_list.btn_reload') }}
            </Button>
          </div>
          <RuleTable
            v-else
            :rules="pageRows"
            :loading="rulesQuery.isLoading.value"
            :can-manage="canManage"
            :pagination="pagination"
            @edit="openEdit"
            @toggle-status="toggleStatus"
          />
        </div>
      </div>
    </div>

    <RuleDialog v-model:open="dialogOpen" :rule="selected" :loading="saveMutation.isPending.value" @submit="save" />
  </PageLayout>
</template>
