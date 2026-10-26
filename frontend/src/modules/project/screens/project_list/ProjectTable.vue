<script setup lang="ts">
// Port ProjectTable.tsx: bảng dự án (người không có project.manage chỉ thấy các cột cơ bản).
import { computed } from 'vue';
import { RouterLink } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { ArrowUpRight, Calendar, Target, Trash2 } from '@lucide/vue';
import CommonTable from '@/shared/components/table/CommonTable.vue';
import type { TableColumn } from '@/shared/components/table/types';
import type { PaginationController } from '@/shared/composables/usePagination';
import GradeBadge from '@/modules/project/components/GradeBadge.vue';
import StatusBadge from '@/modules/project/components/StatusBadge.vue';
import RiskBadge from '@/modules/project/components/RiskBadge.vue';
import FteMetricCell from '@/modules/project/components/FteMetricCell.vue';
import BudgetCell from '@/modules/project/components/BudgetCell.vue';
import ProgressCell from '@/modules/project/components/ProgressCell.vue';
import type { ProjectListItem } from '@/modules/project/api/project.api';
import { formatProjectTimeline, riskKey, timelineInfo } from '@/modules/project/utils/project-view-model';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{
  projects: ProjectListItem[];
  loading: boolean;
  pagination: PaginationController;
  canManage: boolean;
  budgetThreshold: number;
}>();
const { t } = useI18n();

const MANAGE_ONLY = ['contract_fte', 'actual_fte', 'budget_percent', 'risk'];

const columns = computed<TableColumn[]>(() => {
  const all: TableColumn[] = [
    { id: 'name', header: t('project_list.col_project_info') },
    { id: 'grade', header: t('project_list.col_grade'), headerClass: 'text-center', cellClass: 'text-center w-[80px]' },
    { id: 'contract_fte', headerClass: 'w-[140px]' },
    { id: 'actual_fte', header: t('project_list.col_actual_fte'), headerClass: 'w-[140px]' },
    { id: 'budget_percent', header: t('project_list.col_budget_used'), headerClass: 'w-[120px]' },
    {
      id: 'timeline',
      header: props.canManage ? t('project_list.col_timeline_progress') : t('project_list.col_timeline'),
      headerClass: 'w-[200px]',
    },
    { id: 'status', header: t('project_list.col_status') },
    { id: 'risk', header: t('project_list.col_risk'), headerClass: 'text-right', cellClass: 'text-right pr-6' },
  ];
  return props.canManage ? all : all.filter((c) => !MANAGE_ONLY.includes(c.id));
});

function timelineLabel(state: ReturnType<typeof timelineInfo>['state']) {
  if (state === 'not_started') return t('project_list.lbl_timeline_not_started');
  if (state === 'completed') return t('project_list.lbl_timeline_completed');
  return t('project_list.lbl_timeline_running');
}

const TIMELINE_ICON_COLOR = { running: 'text-pr/50', not_started: 'text-amber-500/50', completed: 'text-slate-400/50' } as const;

/** Tô nền dòng: rủi ro cao → đỏ, vượt ngưỡng budget → vàng (giống getProjectRowClassName). */
function rowClass(project: ProjectListItem) {
  const isHighRisk = riskKey(project.riskLevel) === 'high';
  const isOverBudget = project.budgetUsedPercent >= props.budgetThreshold;
  return cn(
    'cursor-pointer',
    'transition-all duration-300 ease-in-out',
    isHighRisk
      ? 'bg-red-50/20 hover:bg-red-50/40 hover:!border-red-500'
      : isOverBudget
        ? 'bg-amber-50/20 hover:bg-amber-50/40 hover:!border-amber-500'
        : 'hover:!border-pr hover:bg-slate-50/40',
  );
}
</script>

<template>
  <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
    <CommonTable
      :columns="columns"
      :rows="props.projects"
      :row-key="(p) => p.id"
      :loading="props.loading"
      :empty-message="$t('project_list.empty_no_project')"
      :pagination="props.pagination"
      table-class="min-w-[1250px]"
      :row-class="rowClass"
      container-class="flex-1 min-h-0"
      table-wrapper-class="border-none shadow-none bg-transparent"
    >
      <template #header-contract_fte>
        <div class="flex flex-col leading-tight">
          <span>{{ $t('project_list.col_contract_fte_line1') }}</span>
          <span>{{ $t('project_list.col_contract_fte_line2') }}</span>
        </div>
      </template>

      <template #cell-name="{ row }">
        <div class="flex flex-col gap-1.5 py-2">
          <div class="inline-flex items-center gap-2">
            <RouterLink
              :to="`/projects/${row.id}`"
              class="group/link inline-flex items-center gap-2 text-[13px] font-extrabold text-slate-900 transition-all duration-300 hover:text-pr outline-none focus-visible:ring-2 focus-visible:ring-pr/30 focus-visible:ring-offset-1 rounded-md px-1 -mx-1"
            >
              <span class="relative">
                {{ row.name }}
                <span class="absolute -bottom-0.5 left-0 h-[2px] w-0 bg-pr transition-all duration-500 group-hover/link:w-full" />
              </span>
              <ArrowUpRight
                :size="14"
                class="opacity-0 -translate-x-2 translate-y-1 transition-all duration-300 group-hover/link:opacity-100 group-hover/link:translate-x-0 group-hover/link:translate-y-0 text-pr"
              />
            </RouterLink>
            <span
              v-if="row.isDeleted"
              class="inline-flex items-center gap-1 rounded-full bg-rose-50 px-2 py-0.5 text-[9px] font-black uppercase tracking-widest text-rose-500 ring-1 ring-rose-200"
            >
              <Trash2 :size="9" />
              {{ $t('project_list.lbl_deleted') }}
            </span>
          </div>
          <div class="flex items-center gap-3">
            <div class="flex items-center gap-1.5 text-[11px] font-bold text-slate-400 bg-slate-50 px-2 py-0.5 rounded-md border border-slate-100 transition-colors hover:bg-slate-100">
              <Target :size="11" class="text-slate-400" />
              {{ row.client?.name }}
            </div>
            <div class="text-[9px] font-black uppercase tracking-widest text-slate-300">
              {{ $t('project_list.lbl_id', { id: row.id ? row.id.slice(0, 8).toUpperCase() : 'N/A' }) }}
            </div>
          </div>
        </div>
      </template>

      <template #cell-grade="{ row }"><GradeBadge :grade="row.grade" /></template>
      <template #cell-contract_fte="{ row }"><FteMetricCell :value="row.contractStaffFte" tone="pr" /></template>
      <template #cell-actual_fte="{ row }"><FteMetricCell :value="row.actualStaffFte" tone="bl" /></template>
      <template #cell-budget_percent="{ row }"><BudgetCell :percent="row.budgetUsedPercent" /></template>

      <template #cell-timeline="{ row }">
        <div class="flex flex-col gap-2 py-1 min-w-[160px]">
          <div class="flex flex-col gap-0.5">
            <div class="text-[11px] font-black text-slate-700 tracking-tight whitespace-nowrap">{{ formatProjectTimeline(row.startDate, row.endDate) }}</div>
            <div class="flex items-center gap-1.5 text-[10px] font-black uppercase tracking-wider text-slate-400">
              <Calendar :size="11" :class="TIMELINE_ICON_COLOR[timelineInfo(row.startDate, row.endDate).state]" />
              {{ timelineLabel(timelineInfo(row.startDate, row.endDate).state) }}
            </div>
          </div>
          <ProgressCell v-if="props.canManage" :percent="timelineInfo(row.startDate, row.endDate).percent" />
        </div>
      </template>

      <template #cell-status="{ row }"><StatusBadge :status="row.status" /></template>
      <template #cell-risk="{ row }"><RiskBadge :risk="row.riskLevel" /></template>
    </CommonTable>
  </div>
</template>
