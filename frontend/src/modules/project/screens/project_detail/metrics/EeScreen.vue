<script setup lang="ts">
// Port ProjectEEScreen + ProjectEEMemberBoard + ProjectEETable + ProjectEEChart: EE = Actual / HĐ × 100 theo tháng và luỹ kế năm,
// biểu đồ luỹ kế HĐ / Actual / Logwork theo ngày của từng người trong 1 tháng. Mọi số do BE tính (công thức ở tools/mock-api/lib/project-routes.mjs → eeData).
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { Zap } from '@lucide/vue';
import DetailMetricCard from '@/modules/project/components/DetailMetricCard.vue';
import LineChart from '@/modules/project/components/LineChart.vue';
import { projectDetailApi, type ProjectDetail, type Tone } from '@/modules/project/api/project-detail.api';

const props = defineProps<{ project: ProjectDetail }>();
const { t } = useI18n();
const COLORS = { hd: '#22c55e', actual: '#ef4444', logwork: '#3b82f6' } as const;

const monthChoice = ref<string | null>(null);
const query = useQuery({
  queryKey: computed(() => ['projects', 'ee', props.project.id, monthChoice.value] as const),
  queryFn: ({ signal }) => projectDetailApi.ee(props.project.id, monthChoice.value, { signal }),
  placeholderData: (prev) => prev,
});
const data = computed(() => query.data.value);
const years = computed(() => [...new Set((data.value?.months ?? []).map((m) => m.month.slice(0, 4)))].sort());
const yearChoice = ref('');
const year = computed(() => (years.value.includes(yearChoice.value) ? yearChoice.value : (years.value.at(-1) ?? '')));
const yearData = computed(() => (data.value?.months ?? []).filter((m) => m.month.startsWith(year.value)));
const latest = computed(() => yearData.value.at(-1));
const latestWithEE = computed(() => [...yearData.value].reverse().find((m) => m.eeMonth !== null));
const chartMonth = computed(() => data.value?.chartMonth ?? null);
function onYear(event: Event) {
  yearChoice.value = (event.target as HTMLSelectElement).value;
  monthChoice.value = yearData.value.at(-1)?.month ?? null;
}

const formatEE = (v: number | null) => (v === null ? '—' : `${v.toFixed(1)}%`);
const formatHours = (v: number) => `${v.toFixed(1)}h`;
const monthLabel = (m: string) => t('project_detail.lbl_month_year', { month: Number(m.slice(5, 7)), year: m.slice(0, 4) });
/** KPI EE: chỉ tô xanh / đỏ, mức cam để màu mặc định (theo spec bản gốc). */
const cardTone = (tone: Tone) => (tone === 'green' || tone === 'red' ? tone : 'default');
const TEXT: Record<string, string> = { none: 'text-slate-400', green: 'text-green-600', orange: 'text-orange-500', red: 'text-red-500' };
const memberSeries = (points: Array<{ hd: number; actual: number; logwork: number }>) => [
  { key: 'hd', label: t('project_detail.ee_section.lbl_hd'), color: COLORS.hd, values: points.map((p) => p.hd) },
  { key: 'actual', label: t('project_detail.ee_section.lbl_actual'), color: COLORS.actual, values: points.map((p) => p.actual) },
  { key: 'logwork', label: t('project_detail.ee_section.lbl_logwork'), color: COLORS.logwork, values: points.map((p) => p.logwork) },
];
const SELECT = 'h-9 rounded-lg border border-slate-200 bg-white px-3 text-[13px] font-bold text-slate-700 outline-none focus:border-violet-400';
const TH = 'p-3 text-right text-[11px] font-bold uppercase tracking-wide';
</script>

<template>
  <div class="flex flex-col gap-5">
    <div class="rounded-2xl border border-violet-100 bg-violet-50/60 p-4">
      <div class="mb-1.5 flex items-center gap-2 text-[13px] font-black text-violet-700"><Zap :size="15" />{{ $t('project_detail.ee_section.hint_formula_title') }}</div>
      <p class="mb-1.5 text-[12.5px] font-bold text-slate-700">{{ $t('project_detail.ee_section.hint_formula') }}</p>
      <ul class="list-disc space-y-0.5 pl-4 text-[11.5px] text-slate-500">
        <li><span class="font-bold text-emerald-700">{{ $t('project_detail.ee_section.lbl_hd') }}</span> — {{ $t('project_detail.ee_section.hint_formula_hd') }}</li>
        <li><span class="font-bold text-rose-600">{{ $t('project_detail.ee_section.lbl_actual') }}</span> — {{ $t('project_detail.ee_section.hint_formula_actual') }}</li>
        <li><span class="font-bold text-blue-700">{{ $t('project_detail.ee_section.lbl_logwork') }}</span> — {{ $t('project_detail.ee_section.hint_formula_logwork') }}</li>
        <li>{{ $t('project_detail.ee_section.hint_formula_mm') }}</li>
        <li>{{ $t('project_detail.ee_section.hint_formula_ytd') }}</li>
        <li>{{ $t('project_detail.ee_section.hint_formula_tone', { green: data?.thresholds.greenMax ?? 100, orange: data?.thresholds.orangeMax ?? 110 }) }}</li>
      </ul>
    </div>

    <div v-if="query.isPending.value" class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
    <p v-else-if="query.isError.value" role="alert" class="rounded-xl border border-rose-100 bg-rose-50 px-4 py-2.5 text-[12.5px] font-semibold text-rose-600">{{ $t('project_detail.ee_section.lbl_load_failed') }}</p>
    <div v-else-if="!latest" class="flex flex-col items-center gap-2 rounded-2xl border border-dashed border-slate-200 bg-white px-6 py-10 text-center">
      <Zap :size="28" class="text-slate-300" />
      <p class="text-[13px] font-bold text-slate-600">{{ $t('project_detail.ee_section.empty_data') }}</p>
      <p class="text-[12px] text-slate-400">{{ $t('project_detail.ee_section.empty_data_desc') }}</p>
    </div>
    <template v-else>
      <div class="flex items-center justify-end gap-2">
        <span class="text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.lbl_select_year') }}</span>
        <select :aria-label="$t('project_detail.lbl_select_year')" :value="year" :class="SELECT" @change="onYear"><option v-for="y in years" :key="y" :value="y">{{ y }}</option></select>
      </div>
      <div class="grid grid-cols-2 gap-3 sm:grid-cols-5">
        <DetailMetricCard :label="$t('project_detail.ee_section.stat_latest')" :value="formatEE(latestWithEE?.eeMonth ?? null)" :tone="cardTone(latestWithEE?.eeMonthTone ?? 'none')" />
        <DetailMetricCard :label="$t('project_detail.ee_section.stat_ytd')" :value="formatEE(latest.eeYtd)" :tone="cardTone(latest.eeYtdTone)" />
        <DetailMetricCard :label="$t('project_detail.ee_section.stat_hd_ytd')" :value="latest.hdYtdHours" :formatter="formatHours" />
        <DetailMetricCard :label="$t('project_detail.ee_section.stat_actual_ytd')" :value="latest.actualYtdHours" :formatter="formatHours" />
        <DetailMetricCard :label="$t('project_detail.ee_section.stat_logwork_ytd')" :value="latest.logworkYtdHours" :formatter="formatHours" />
      </div>

      <div class="flex flex-wrap items-center justify-between gap-3">
        <div class="flex flex-wrap items-center gap-5 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
          <span class="flex items-center gap-2"><span class="h-[3px] w-[18px] rounded-full" :style="{ backgroundColor: COLORS.hd }" />{{ $t('project_detail.ee_section.lbl_legend_hd') }}</span>
          <span class="flex items-center gap-2"><span class="h-[3px] w-[18px] rounded-full" :style="{ backgroundColor: COLORS.actual }" />{{ $t('project_detail.ee_section.lbl_legend_actual') }}</span>
          <span class="flex items-center gap-2"><span class="h-[3px] w-[18px] rounded-full" :style="{ backgroundColor: COLORS.logwork }" />{{ $t('project_detail.ee_section.lbl_legend_logwork') }}</span>
          <span class="normal-case tracking-normal">{{ $t('project_detail.ee_section.lbl_legend_axes') }}</span>
        </div>
        <div class="flex items-center gap-2">
          <span class="text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.lbl_select_month') }}</span>
          <select :aria-label="$t('project_detail.lbl_select_month')" :value="chartMonth ?? ''" :class="SELECT" @change="monthChoice = ($event.target as HTMLSelectElement).value">
            <option v-for="m in yearData" :key="m.month" :value="m.month">{{ monthLabel(m.month) }}</option>
          </select>
        </div>
      </div>

      <div class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div class="grid grid-cols-[260px_1fr] border-b border-slate-200 bg-slate-50/80">
          <div class="border-r border-slate-200 p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_detail.ee_section.lbl_board_member') }}</div>
          <div class="p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_detail.ee_section.lbl_board_chart', { month: chartMonth ? monthLabel(chartMonth) : '' }) }}</div>
        </div>
        <p v-if="(data?.members ?? []).length === 0" class="p-6 text-center text-[12.5px] text-slate-400">{{ $t('project_detail.ee_section.empty_member') }}</p>
        <div v-for="m in data?.members ?? []" :key="m.employeeId" class="grid grid-cols-[260px_1fr] border-b border-slate-100 last:border-b-0">
          <div class="flex flex-col gap-2 border-r border-slate-100 p-4">
            <div class="flex items-center gap-2.5">
              <div class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-violet-50 text-[13px] font-black text-violet-600">{{ m.fullName.charAt(0) }}</div>
              <div class="min-w-0">
                <div class="truncate text-[13px] font-bold text-slate-800">{{ m.fullName }} <span class="font-semibold text-slate-400">({{ m.code }})</span></div>
                <div class="truncate text-[11.5px] text-slate-500">{{ m.position }}</div>
              </div>
            </div>
            <div class="flex flex-wrap items-center gap-1.5 text-[11px] font-bold">
              <span class="rounded-md bg-emerald-50 px-1.5 py-0.5 text-emerald-700"><span class="opacity-70">{{ $t('project_detail.ee_section.lbl_hd') }}</span> {{ formatHours(m.totals.hdHours) }}</span>
              <span class="rounded-md bg-rose-50 px-1.5 py-0.5 text-rose-600"><span class="opacity-70">{{ $t('project_detail.ee_section.lbl_actual') }}</span> {{ formatHours(m.totals.actualHours) }}</span>
              <span class="rounded-md bg-blue-50 px-1.5 py-0.5 text-blue-700"><span class="opacity-70">{{ $t('project_detail.ee_section.lbl_log') }}</span> {{ formatHours(m.totals.logworkHours) }}</span>
            </div>
            <div class="text-[11.5px] font-semibold text-slate-500">{{ $t('project_detail.ee_section.lbl_member_ee') }} <span :class="['font-black', TEXT[m.totals.eeTone]]">{{ formatEE(m.totals.ee) }}</span></div>
          </div>
          <div class="min-w-0 p-2">
            <LineChart :labels="m.points.map((p) => p.date)" :series="memberSeries(m.points)" :height="150" :point-width="18" axis="day" :format="(v) => `${Math.round(v)}h`" />
          </div>
        </div>
      </div>

      <div class="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table class="w-full border-collapse text-left">
          <thead>
            <tr class="border-b border-slate-200 bg-slate-50/80">
              <th class="p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_detail.ee_section.col_month') }}</th>
              <th :class="[TH, 'text-slate-500']">{{ $t('project_detail.ee_section.col_mm') }}</th>
              <th :class="[TH, 'text-emerald-700']">{{ $t('project_detail.ee_section.lbl_hd') }}</th>
              <th :class="[TH, 'text-rose-600']">{{ $t('project_detail.ee_section.lbl_actual') }}</th>
              <th :class="[TH, 'text-blue-700']">{{ $t('project_detail.ee_section.lbl_logwork') }}</th>
              <th :class="[TH, 'text-slate-500']">{{ $t('project_detail.ee_section.col_ee_month') }}</th>
              <th :class="[TH, 'text-slate-500']">{{ $t('project_detail.ee_section.col_ee_ytd') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(m, i) in yearData" :key="m.month" :class="['border-b border-slate-100 text-[12.5px]', i % 2 === 0 ? 'bg-white' : 'bg-slate-50/60']">
              <td class="p-3 font-bold text-slate-700">{{ monthLabel(m.month) }}</td>
              <td class="p-3 text-right tabular-nums text-slate-500">{{ m.hoursPerMM }}h</td>
              <td class="p-3 text-right tabular-nums">{{ formatHours(m.hdHours) }} <span class="text-[11px] text-slate-400">({{ m.hdMM }} MM)</span></td>
              <td class="p-3 text-right tabular-nums">{{ formatHours(m.actualHours) }} <span class="text-[11px] text-slate-400">({{ m.actualMM }} MM)</span></td>
              <td class="p-3 text-right tabular-nums">{{ formatHours(m.logworkHours) }}</td>
              <td :class="['p-3 text-right font-black tabular-nums', TEXT[m.eeMonthTone]]">{{ formatEE(m.eeMonth) }}</td>
              <td :class="['p-3 text-right font-black tabular-nums', TEXT[m.eeYtdTone]]">{{ formatEE(m.eeYtd) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>
