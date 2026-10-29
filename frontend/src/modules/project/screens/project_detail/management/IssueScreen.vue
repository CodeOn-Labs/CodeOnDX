<script setup lang="ts">
// Quản lý issue: logwork issue Jira của dự án theo Loại yêu cầu kỹ thuật (danh mục `tech_request_type`, Quản trị › Danh mục).
//   - Theo loại: giờ, MM, tỷ trọng (%) của dự án — kế toán dùng tỷ trọng này để phân bổ chi tiết chi phí.
//   - Theo thành viên: giờ của từng người × từng loại — PMO đối chiếu để chi phí phân bổ không bị sai.
//   - Chi tiết issue: lọc theo loại / thành viên / từ khoá.
// BE tính sẵn mọi tổng, tỷ trọng và MM (xem issueWorklogsData trong tools/mock-api/lib/project-extra-routes.mjs).
// Thay cho màn gắn link issue cũ (theo issue cha / sprint / milestone).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { AlertTriangle, ExternalLink, FileWarning, Info } from '@lucide/vue';
import MonthNavigator from '@/shared/components/form/MonthNavigator.vue';
import ColorBadge from '@/shared/components/ColorBadge.vue';
import DetailMetricCard from '@/modules/project/components/DetailMetricCard.vue';
import { projectDetailApi, UNCLASSIFIED_TYPE, type IssueWorklogMember, type ProjectDetail } from '@/modules/project/api/project-detail.api';
import { resolveColor } from '@/shared/utils/color';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';
import { matchesQuery } from './management-search';

const props = defineProps<{ project: ProjectDetail; query: string }>();
const { t, locale } = useI18n();

const now = new Date();
const month = ref(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`);
const q = useQuery({
  queryKey: computed(() => ['projects', 'issue-worklogs', props.project.id, month.value] as const),
  queryFn: ({ signal }) => projectDetailApi.issueWorklogs(props.project.id, month.value, { signal }),
});
const data = computed(() => q.data.value);

const num = (n: number, digits = 2) => n.toLocaleString(locale.value, { maximumFractionDigits: digits });
const hours = (n: number) => t('project_detail.issue_section.unit_hours', { value: num(n, 1) });

/** Loại yêu cầu (kèm cột "Chưa phân loại" cuối cùng) theo thứ tự danh mục. */
const columns = computed(() => [
  ...(data.value?.types ?? []).map((ty) => ({ id: ty.id, name: ty.name, color: ty.color })),
  { id: UNCLASSIFIED_TYPE, name: t('project_detail.issue_section.lbl_unclassified'), color: 'amber' },
]);
const columnOf = (id: string) => columns.value.find((c) => c.id === id) ?? columns.value.at(-1)!;
const byTypeRows = computed(() => (data.value?.byType ?? []).map((b) => ({ ...b, ...columnOf(b.typeId) })));

const typeFilter = ref('');
const memberFilter = ref('');
watch(month, () => {
  typeFilter.value = '';
  memberFilter.value = '';
});
const members = computed(() => (data.value?.members ?? []).filter((m) => matchesQuery(props.query, m.fullName, m.code, m.teamName)));
const items = computed(() => (data.value?.items ?? [])
  .filter((i) => !typeFilter.value || i.typeId === typeFilter.value)
  .filter((i) => !memberFilter.value || i.employeeId === memberFilter.value)
  .filter((i) => matchesQuery(props.query, i.issueKey, i.summary, i.employeeName)));
const cellHours = (m: IssueWorklogMember, typeId: string) => m.hoursByType[typeId] ?? 0;
const toggleMember = (id: string) => (memberFilter.value = memberFilter.value === id ? '' : id);

const SELECT = 'h-9 rounded-lg border border-slate-200 bg-white px-3 text-[12.5px] font-bold text-slate-700 outline-none focus:border-violet-400';
const TH = 'px-3 py-2.5 text-[10.5px] font-bold uppercase tracking-wide text-slate-500 whitespace-nowrap';
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <MonthNavigator v-model="month" />
      <p class="text-[11.5px] font-semibold text-slate-400">
        {{ data?.syncedAt ? $t('project_detail.issue_section.lbl_source_synced', { time: formatDate(data.syncedAt, 'dd/MM/yyyy HH:mm') }) : $t('project_detail.issue_section.lbl_source') }}
      </p>
    </div>

    <div v-if="!data" class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
    <template v-else>
      <div v-if="!data.canViewAll" class="flex items-start gap-2 rounded-xl border border-blue-100 bg-blue-50/60 px-3 py-2.5 text-[12px] font-semibold text-blue-700">
        <Info :size="15" class="mt-0.5 shrink-0" />{{ $t('project_detail.issue_section.hint_self_only') }}
      </div>
      <div v-if="data.totals.unclassifiedHours > 0" class="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2.5 text-[12px] font-semibold text-amber-800">
        <AlertTriangle :size="15" class="mt-0.5 shrink-0" />
        {{ $t('project_detail.issue_section.lbl_unclassified_warning', { count: data.totals.unclassifiedIssueCount, hours: num(data.totals.unclassifiedHours, 1) }) }}
      </div>

      <div v-if="data.items.length === 0" class="flex h-32 flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 px-6 text-center">
        <FileWarning class="text-slate-300" :size="26" />
        <p class="max-w-md text-[12px] text-slate-400">{{ $t('project_detail.issue_section.empty_month') }}</p>
      </div>

      <template v-else>
        <div class="grid grid-cols-2 gap-2.5 lg:grid-cols-4">
          <DetailMetricCard :label="$t('project_detail.issue_section.stat_total_hours')" :value="data.totals.hours" :formatter="(v) => num(v, 1)" />
          <DetailMetricCard :label="$t('project_detail.issue_section.stat_total_mm')" :value="data.totals.mm" :formatter="(v) => num(v)" :delay="0.05" />
          <DetailMetricCard :label="$t('project_detail.issue_section.stat_issue_count')" :value="data.totals.issueCount" :formatter="(v) => num(v, 0)" :delay="0.1" />
          <DetailMetricCard :label="$t('project_detail.issue_section.stat_member_count')" :value="data.totals.memberCount" :formatter="(v) => num(v, 0)" :delay="0.15" />
        </div>

        <!-- Theo loại yêu cầu kỹ thuật -->
        <section class="overflow-hidden rounded-[14px] border border-[#e8ecf5] bg-white shadow-sm">
          <header class="bg-slate-50/70 px-4 py-2.5">
            <div class="text-[13.5px] font-black text-slate-800">{{ $t('project_detail.issue_section.title_by_type') }}</div>
            <div class="text-[11.5px] text-slate-400">{{ $t('project_detail.issue_section.hint_by_type', { hours: num(data.hoursPerMm, 0) }) }}</div>
          </header>
          <div class="flex flex-col gap-3 p-4">
            <div class="flex h-3 w-full overflow-hidden rounded-full bg-slate-100" role="img" :aria-label="$t('project_detail.issue_section.title_by_type')">
              <div v-for="r in byTypeRows.filter((x) => x.hours > 0)" :key="r.typeId" :style="{ width: `${r.percent}%`, backgroundColor: resolveColor(r.color) }" :title="`${r.name}: ${num(r.percent, 1)}%`" />
            </div>
            <table class="w-full text-left text-[12.5px]">
              <thead>
                <tr class="border-b border-slate-100">
                  <th :class="TH">{{ $t('project_detail.issue_section.col_type') }}</th>
                  <th :class="cn(TH, 'text-right')">{{ $t('project_detail.issue_section.col_hours') }}</th>
                  <th :class="cn(TH, 'text-right')">{{ $t('project_detail.issue_section.col_mm') }}</th>
                  <th :class="cn(TH, 'text-right')">{{ $t('project_detail.issue_section.col_percent') }}</th>
                  <th :class="cn(TH, 'text-right')">{{ $t('project_detail.issue_section.col_issue_count') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-50">
                <tr v-for="r in byTypeRows" :key="r.typeId" :class="cn('cursor-pointer hover:bg-slate-50/70', typeFilter === r.typeId && 'bg-violet-50/60', r.hours === 0 && 'text-slate-400')" @click="typeFilter = typeFilter === r.typeId ? '' : r.typeId">
                  <td class="px-3 py-2"><span class="inline-flex items-center gap-2 font-bold"><span class="h-2.5 w-2.5 rounded-full" :style="{ backgroundColor: resolveColor(r.color) }" />{{ r.name }}</span></td>
                  <td class="px-3 py-2 text-right font-bold tabular-nums">{{ num(r.hours, 1) }}</td>
                  <td class="px-3 py-2 text-right tabular-nums">{{ num(r.mm) }}</td>
                  <td class="px-3 py-2 text-right font-black tabular-nums">{{ num(r.percent, 1) }}%</td>
                  <td class="px-3 py-2 text-right tabular-nums">{{ r.issueCount }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Theo thành viên -->
        <section class="overflow-hidden rounded-[14px] border border-[#e8ecf5] bg-white shadow-sm">
          <header class="bg-slate-50/70 px-4 py-2.5">
            <div class="text-[13.5px] font-black text-slate-800">{{ $t('project_detail.issue_section.title_by_member') }}</div>
            <div class="text-[11.5px] text-slate-400">{{ $t('project_detail.issue_section.hint_by_member') }}</div>
          </header>
          <div class="overflow-x-auto">
            <table class="w-full min-w-[720px] text-left text-[12.5px]">
              <thead>
                <tr class="border-b border-slate-100">
                  <th :class="cn(TH, 'min-w-[190px]')">{{ $t('project_detail.issue_section.col_member') }}</th>
                  <th v-for="c in columns" :key="c.id" :class="cn(TH, 'min-w-[84px] max-w-[130px] whitespace-normal text-right align-bottom leading-tight')" :style="{ color: resolveColor(c.color) }">{{ c.name }}</th>
                  <th :class="cn(TH, 'text-right')">{{ $t('project_detail.issue_section.col_total') }}</th>
                  <th :class="cn(TH, 'text-right')">{{ $t('project_detail.issue_section.col_mm') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-50">
                <tr v-if="members.length === 0"><td :colspan="columns.length + 3" class="px-4 py-3 text-[12px] font-semibold text-slate-400">{{ $t('project_detail.management_section.empty_result') }}</td></tr>
                <tr v-for="m in members" :key="m.employeeId" :class="cn('cursor-pointer hover:bg-slate-50/70', memberFilter === m.employeeId && 'bg-violet-50/60')" :title="$t('project_detail.issue_section.tip_filter_member')" @click="toggleMember(m.employeeId)">
                  <td class="px-3 py-2">
                    <div class="whitespace-nowrap font-bold text-slate-800">{{ m.fullName }}</div>
                    <div class="whitespace-nowrap text-[11px] text-slate-400">{{ m.code }}<template v-if="m.teamName"> · {{ m.teamName }}</template></div>
                  </td>
                  <td v-for="c in columns" :key="c.id" :class="cn('px-3 py-2 text-right tabular-nums', !cellHours(m, c.id) && 'text-slate-300', c.id === UNCLASSIFIED_TYPE && cellHours(m, c.id) > 0 && 'font-bold text-amber-700')">{{ cellHours(m, c.id) ? num(cellHours(m, c.id), 1) : '–' }}</td>
                  <td class="px-3 py-2 text-right font-black tabular-nums text-slate-800">{{ num(m.totalHours, 1) }}</td>
                  <td class="px-3 py-2 text-right tabular-nums">{{ num(m.mm) }}</td>
                </tr>
              </tbody>
              <tfoot v-if="data.canViewAll">
                <tr class="border-t border-slate-200 bg-slate-50/70 font-black text-slate-800">
                  <td class="px-3 py-2.5">{{ $t('project_detail.issue_section.lbl_row_total') }}</td>
                  <td v-for="r in byTypeRows" :key="r.typeId" class="px-3 py-2.5 text-right tabular-nums">{{ num(r.hours, 1) }}</td>
                  <td class="px-3 py-2.5 text-right tabular-nums">{{ num(data.totals.hours, 1) }}</td>
                  <td class="px-3 py-2.5 text-right tabular-nums">{{ num(data.totals.mm) }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </section>

        <!-- Chi tiết issue -->
        <section class="overflow-hidden rounded-[14px] border border-[#e8ecf5] bg-white shadow-sm">
          <header class="flex flex-wrap items-center gap-2 bg-slate-50/70 px-4 py-2.5">
            <div class="min-w-0 flex-1">
              <span class="text-[13.5px] font-black text-slate-800">{{ $t('project_detail.issue_section.title_issues') }}</span>
              <span class="ml-2 rounded-full bg-white px-2 py-0.5 text-[11px] font-bold text-slate-500 ring-1 ring-slate-200">{{ items.length }}</span>
            </div>
            <select v-model="typeFilter" :aria-label="$t('project_detail.issue_section.col_type')" :class="SELECT">
              <option value="">{{ $t('project_detail.issue_section.opt_all_types') }}</option>
              <option v-for="c in columns" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
            <select v-if="data.canViewAll" v-model="memberFilter" :aria-label="$t('project_detail.issue_section.col_member')" :class="SELECT">
              <option value="">{{ $t('project_detail.issue_section.opt_all_members') }}</option>
              <option v-for="m in data.members" :key="m.employeeId" :value="m.employeeId">{{ m.fullName }}</option>
            </select>
          </header>
          <div class="overflow-x-auto">
            <table class="w-full min-w-[760px] text-left text-[12.5px]">
              <thead>
                <tr class="border-b border-slate-100">
                  <th :class="TH">{{ $t('project_detail.issue_section.col_issue') }}</th>
                  <th :class="TH">{{ $t('project_detail.issue_section.col_type') }}</th>
                  <th :class="TH">{{ $t('project_detail.issue_section.col_member') }}</th>
                  <th :class="TH">{{ $t('project_detail.issue_section.col_status') }}</th>
                  <th :class="TH">{{ $t('project_detail.issue_section.col_last_logged') }}</th>
                  <th :class="cn(TH, 'text-right')">{{ $t('project_detail.issue_section.col_hours') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-50">
                <tr v-if="items.length === 0"><td colspan="6" class="px-4 py-3 text-[12px] font-semibold text-slate-400">{{ $t('project_detail.issue_section.empty_filtered') }}</td></tr>
                <tr v-for="i in items" :key="i.id" class="hover:bg-slate-50/60">
                  <td class="max-w-[320px] px-3 py-2">
                    <a :href="i.url" target="_blank" rel="noopener noreferrer" class="flex items-center gap-1.5 font-bold text-slate-700 hover:text-pr hover:underline" :title="$t('project_detail.issue_section.tip_open_issue')">
                      <span class="shrink-0 text-pr">{{ i.issueKey }}</span><span class="truncate">{{ i.summary }}</span><ExternalLink :size="12" class="shrink-0 text-slate-400" />
                    </a>
                  </td>
                  <td class="px-3 py-2"><ColorBadge :color="columnOf(i.typeId).color" :label="columnOf(i.typeId).name" dot /></td>
                  <td class="whitespace-nowrap px-3 py-2 text-slate-600">{{ i.employeeName }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-slate-500">{{ i.status }}</td>
                  <td class="whitespace-nowrap px-3 py-2 tabular-nums text-slate-500">{{ formatDate(i.lastLoggedDate) }}</td>
                  <td class="px-3 py-2 text-right font-bold tabular-nums">{{ hours(i.hours) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <p class="text-[11.5px] text-slate-400">{{ $t('project_detail.issue_section.hint_config_types') }}</p>
      </template>
    </template>
  </div>
</template>
