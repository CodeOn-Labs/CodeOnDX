<script setup lang="ts">
// Port ProjectReadOnlyView + ProjectKpiArea + LegendRow (ProjectDetailPage.tsx): cảnh báo dự án đã xoá,
// cảnh báo biến động nhân sự (BE tính), 4 KPI (budget đã dùng, tiến độ, FTE hợp đồng, FTE thực tế) + link Drive/Ticket/Git,
// chú thích biểu đồ và bảng phân bổ nhân sự. Người không quản lý chỉ thấy link và phần của mình.
import { computed } from 'vue';
import { useQuery } from '@tanstack/vue-query';
import { AlertTriangle, Trash2 } from '@lucide/vue';
import DetailMetricCard from '@/modules/project/components/DetailMetricCard.vue';
import AssignmentTable from './AssignmentTable.vue';
import { projectDetailApi, type ProjectDetail } from '@/modules/project/api/project-detail.api';
import { formatDate } from '@/shared/utils/date';

const props = defineProps<{ project: ProjectDetail }>();
const staffing = useQuery({
  queryKey: computed(() => ['projects', 'staffing', props.project.id] as const),
  queryFn: ({ signal }) => projectDetailApi.staffing(props.project.id, { signal }),
});
const links = computed(() => [
  { key: 'drive', label: 'Drive', url: props.project.driveUrl },
  { key: 'ticket', label: 'Ticket', url: props.project.ticketSystemUrl },
  { key: 'git', label: 'Git', url: props.project.sourceCodeUrl },
]);
const LINK = 'text-[13px] font-bold transition-colors';
</script>

<template>
  <div class="flex h-full flex-col overflow-hidden">
    <div v-if="props.project.isDeleted" class="mb-3 flex shrink-0 items-start gap-3 rounded-[12px] border border-rose-200 bg-rose-50/80 px-4 py-3 text-[13px] text-rose-800 shadow-sm backdrop-blur-md">
      <Trash2 class="mt-0.5 h-5 w-5 shrink-0 text-rose-500" />
      <div class="flex-1">
        <span class="text-[14px] font-bold">{{ $t('project_detail.lbl_deleted_title') }}</span>
        <p class="mt-0.5 text-[12px] text-rose-700">{{ $t('project_detail.lbl_deleted_desc') }}</p>
      </div>
    </div>

    <div v-if="props.project.hrAlerts.length > 0" class="mb-3 flex shrink-0 items-start gap-3 rounded-[12px] border border-rose-200 bg-rose-50/60 px-4 py-3 text-[13px] text-rose-800 shadow-sm backdrop-blur-md">
      <AlertTriangle class="mt-0.5 h-5 w-5 shrink-0 animate-pulse text-rose-500" />
      <div class="flex-1">
        <span class="text-[14px] font-bold">{{ $t('project_detail.lbl_hr_alert_title') }}</span>
        <ul class="mt-1 list-inside list-disc space-y-1">
          <li v-for="a in props.project.hrAlerts" :key="a.employeeId">
            <strong class="text-rose-900">{{ a.fullName }}</strong> ({{ a.code }}) -
            <span v-if="a.kind === 'resigned'" class="font-semibold text-rose-600">{{ $t('project_detail.lbl_resigned_full') }}</span>
            <span v-else class="font-semibold text-amber-600">{{ $t('project_detail.lbl_leaving_on', { date: formatDate(a.leftDate) }) }}</span>
          </li>
        </ul>
      </div>
    </div>

    <div v-if="props.project.canManage" class="mb-2 grid w-full grid-cols-1 gap-2 md:grid-cols-4 lg:grid-cols-5">
      <DetailMetricCard :label="$t('project_detail.stat_budget_used')" :value="props.project.budgetUsedPercent" :formatter="(v) => `${Math.round(v)}%`" :tone="props.project.budgetUsedPercent >= props.project.budgetWarningThreshold ? 'red' : 'default'" />
      <DetailMetricCard :label="$t('project_detail.stat_progress')" :value="props.project.progressPercent ?? 0" :formatter="(v) => `${Math.round(v)}%`" :delay="0.1" />
      <DetailMetricCard :label="$t('project_detail.stat_contract_fte')" :value="props.project.contractStaffFte" :formatter="(v) => v.toFixed(2)" :delay="0.2" />
      <DetailMetricCard :label="$t('project_detail.stat_actual_fte')" :value="props.project.actualStaffFte" :formatter="(v) => v.toFixed(2)" tone="red" :delay="0.3" />
      <div class="flex items-center justify-around rounded-[12px] border border-[#e8ecf5] bg-white px-4 py-3 shadow-sm">
        <a
          v-for="l in links"
          :key="l.key"
          :href="l.url ?? undefined"
          target="_blank"
          rel="noopener noreferrer"
          :class="[LINK, l.url ? 'text-[#5c66d8] hover:underline' : 'pointer-events-none text-slate-300']"
          :title="l.url ? $t('project_detail.tip_open_link', { name: l.label }) : $t('project_detail.tip_no_link', { name: l.label })"
        >{{ l.label }}</a>
      </div>
    </div>
    <div v-else class="mb-2 flex justify-start">
      <div class="flex items-center gap-6 rounded-[12px] border border-[#e8ecf5] bg-white px-6 py-2.5 shadow-sm">
        <a
          v-for="l in links"
          :key="l.key"
          :href="l.url ?? undefined"
          target="_blank"
          rel="noopener noreferrer"
          :class="[LINK, l.url ? 'text-[#5c66d8] hover:underline' : 'pointer-events-none text-slate-300']"
        >{{ l.label }}</a>
      </div>
    </div>

    <div class="flex min-h-0 flex-1 flex-col gap-2">
      <div class="mb-2 flex flex-wrap items-center gap-5 py-1 text-[11px] font-medium uppercase tracking-wide text-[#7c8497]">
        <div class="text-[12px] font-black text-[#1f2430]">{{ $t('project_detail.lbl_personnel_chart') }}</div>
        <div class="flex items-center gap-2"><span class="h-[2px] w-[18px] bg-[#22c55e]" /> {{ $t('project_detail.lbl_contract') }}</div>
        <div class="flex items-center gap-2"><span class="h-[2px] w-[18px] bg-[#3565f6]" /> {{ $t('project_detail.lbl_plan') }}</div>
        <div class="flex items-center gap-2"><span class="h-[2px] w-[18px] bg-[#ef4444]" /> {{ $t('project_detail.lbl_actual') }}</div>
        <div class="ml-auto text-[11px] normal-case text-[#98a1b2]">{{ $t('project_detail.lbl_chart_axes') }}</div>
      </div>
      <div class="min-h-0 flex-1">
        <div v-if="staffing.isPending.value" class="space-y-2">
          <div v-for="i in 4" :key="i" class="h-16 animate-pulse rounded-xl bg-[#e8ecf5]" />
        </div>
        <AssignmentTable v-else-if="staffing.data.value" :project="props.project" :staffing="staffing.data.value" :can-manage="props.project.canManage" />
      </div>
    </div>
  </div>
</template>
