<script setup lang="ts">
// Port ProjectQualityScreen.tsx (dùng chung cho Defect và Leakage): PO nhập số defect + Product size theo tháng,
// Total billable (MM) lấy tự động từ màn Công số; tỷ lệ tháng / luỹ kế năm và màu do BE tính (ngưỡng ở Quản trị → Quy tắc).
// Ô số defect để trống khi lưu = xoá số liệu tháng đó.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Bug, Egg, Lock, Pencil, Save, X } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import DetailMetricCard from '@/modules/project/components/DetailMetricCard.vue';
import { projectDetailApi, type ProjectDetail, type QualityKind, type Tone } from '@/modules/project/api/project-detail.api';
import { firstFieldErrors } from '@/api/http';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ project: ProjectDetail; kind: QualityKind }>();
const { t } = useI18n();
const toast = useToastStore();
const queryClient = useQueryClient();
const UNITS = ['Point', 'Request', 'Module', 'KLOC'];

const yearChoice = ref<number | null>(null);
const query = useQuery({
  queryKey: computed(() => ['projects', 'quality', props.project.id, props.kind, yearChoice.value] as const),
  queryFn: ({ signal }) => projectDetailApi.quality(props.project.id, props.kind, yearChoice.value, { signal }),
  placeholderData: (prev) => prev,
});
const data = computed(() => query.data.value);
const latest = computed(() => data.value?.rows.at(-1));
const isDefect = computed(() => props.kind === 'defect');
const th = computed(() => data.value?.thresholds);
const L = computed(() => (isDefect.value
  ? {
    formulaTitle: t('project_detail.quality_section.lbl_defect_formula_title'), formulaSize: t('project_detail.quality_section.lbl_defect_formula_size'), formulaMM: t('project_detail.quality_section.lbl_defect_formula_mm'),
    purpose: t('project_detail.quality_section.lbl_defect_formula_purpose'), kpiSize: t('project_detail.quality_section.lbl_defect_kpi_rate_size'), kpiMM: t('project_detail.quality_section.lbl_defect_kpi_rate_mm'),
    table: t('project_detail.quality_section.lbl_defect_table_title', { year: data.value?.year ?? '' }), colMonthly: t('project_detail.quality_section.lbl_defect_col_monthly'), colYtd: t('project_detail.quality_section.lbl_defect_col_ytd'), colDefects: t('project_detail.quality_section.lbl_defect_col_defects'),
    tone: t('project_detail.quality_section.lbl_defect_tone', { min: th.value?.goodMin ?? 4, max: th.value?.goodMax ?? 5, warn: th.value?.warnMax ?? 6 }),
  }
  : {
    formulaTitle: t('project_detail.quality_section.lbl_leakage_formula_title'), formulaSize: t('project_detail.quality_section.lbl_leakage_formula_size'), formulaMM: t('project_detail.quality_section.lbl_leakage_formula_mm'),
    purpose: t('project_detail.quality_section.lbl_leakage_formula_purpose'), kpiSize: t('project_detail.quality_section.lbl_leakage_kpi_rate_size'), kpiMM: t('project_detail.quality_section.lbl_leakage_kpi_rate_mm'),
    table: t('project_detail.quality_section.lbl_leakage_table_title', { year: data.value?.year ?? '' }), colMonthly: t('project_detail.quality_section.lbl_leakage_col_monthly'), colYtd: t('project_detail.quality_section.lbl_leakage_col_ytd'), colDefects: t('project_detail.quality_section.lbl_leakage_col_defects'),
    tone: t('project_detail.quality_section.lbl_leakage_tone', { min: th.value?.goodMin ?? 0.45, max: th.value?.goodMax ?? 0.5, warn: th.value?.warnMax ?? 0.55 }),
  }));
const monthLabel = (m: string) => t('project_detail.lbl_month_year', { month: Number(m.slice(5, 7)), year: m.slice(0, 4) });
const fmtRate = (v: number | null) => (v === null ? '—' : v.toFixed(3));
const fmtPerMM = (v: number | null) => (v === null ? '—' : v.toFixed(2));
const fmtNum = (v: number) => (Number.isInteger(v) ? String(v) : v.toFixed(2));
const TONE_CELL: Record<Tone, string> = { none: 'text-slate-300', lightOrange: 'bg-orange-50 text-orange-500', green: 'bg-emerald-50 text-emerald-700', yellow: 'bg-yellow-50 text-yellow-700', red: 'bg-rose-50 text-rose-600', orange: 'bg-orange-50 text-orange-600' };
const cardTone = (tone: Tone | undefined) => (!tone || tone === 'none' ? 'default' : tone);

const drafts = ref<Record<string, { defects: string; productSize: string }> | null>(null);
const saving = ref(false);
function startEdit() {
  drafts.value = Object.fromEntries((data.value?.rows ?? []).map((r) => [r.month, r.hasData ? { defects: String(r.defects), productSize: String(r.productSize) } : { defects: '', productSize: '' }]));
}
async function save() {
  const d = data.value;
  if (!d || !drafts.value) return;
  saving.value = true;
  try {
    const entries = Object.entries(drafts.value).map(([month, v]) => ({ month, defects: v.defects.trim() === '' ? null : v.defects, productSize: v.productSize.trim() === '' ? 0 : v.productSize }));
    const res = await projectDetailApi.saveQuality(props.project.id, props.kind, d.year, entries);
    toast.success(res.messageText);
    drafts.value = null;
    void queryClient.invalidateQueries({ queryKey: ['projects', 'quality', props.project.id, props.kind] });
  } catch (err) {
    const fe = firstFieldErrors(err);
    const first = Object.entries(fe)[0];
    if (first) {
      const index = Number(first[0].split('.')[1]);
      const month = Object.keys(drafts.value ?? {})[index];
      toast.error(`${month ? monthLabel(month) : ''}: ${first[1]}`);
    } else toast.error((err as Error).message);
  } finally {
    saving.value = false;
  }
}
async function changeUnit(event: Event) {
  const d = data.value;
  if (!d) return;
  try {
    await projectDetailApi.saveQuality(props.project.id, props.kind, d.year, [], (event.target as HTMLSelectElement).value);
    void queryClient.invalidateQueries({ queryKey: ['projects', 'quality', props.project.id, props.kind] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const INPUT = 'h-8 w-full rounded-lg border border-slate-200 px-2 text-center text-[12.5px] font-semibold outline-none focus:border-violet-400 focus:ring-2 focus:ring-violet-100';
const TH = 'px-2 py-2 text-[10px] font-bold uppercase tracking-wide text-slate-500';
const TL = `${TH} text-center border-l border-slate-200`;
</script>

<template>
  <div class="flex flex-col gap-5">
    <div class="rounded-2xl border border-violet-100 bg-violet-50/60 p-4">
      <div class="mb-1.5 flex items-center gap-2 text-[13px] font-black text-violet-700"><Egg v-if="isDefect" :size="15" /><Bug v-else :size="15" />{{ L.formulaTitle }}</div>
      <p class="text-[12.5px] font-bold text-slate-700">{{ L.formulaSize }}</p>
      <p class="mb-1.5 text-[12.5px] font-bold text-slate-700">{{ L.formulaMM }}</p>
      <ul class="list-disc space-y-0.5 pl-4 text-[11.5px] text-slate-500">
        <li>{{ L.purpose }}</li>
        <li>{{ $t('project_detail.quality_section.hint_formula_billable') }}</li>
        <li>{{ L.tone }}</li>
        <li>{{ $t('project_detail.quality_section.hint_formula_ytd') }}</li>
      </ul>
    </div>

    <div v-if="!data" class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
    <template v-else>
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div class="flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 py-1.5 shadow-sm">
          <span class="text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.quality_section.unit') }}</span>
          <select v-if="data.canEdit" :aria-label="$t('project_detail.quality_section.unit')" :value="data.unit" class="h-7 rounded-md border border-slate-200 bg-white px-2 text-[13px] font-bold text-slate-700 outline-none focus:border-violet-400" @change="changeUnit">
            <option v-for="u in UNITS" :key="u" :value="u">{{ u }}</option>
          </select>
          <span v-else class="text-[13px] font-black text-slate-700">{{ data.unit }}</span>
        </div>
        <div class="flex items-center gap-2">
          <span class="text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.lbl_select_year') }}</span>
          <select :aria-label="$t('project_detail.lbl_select_year')" :value="data.year" :disabled="!!drafts" class="h-9 rounded-lg border border-slate-200 bg-white px-3 text-[13px] font-bold text-slate-700 outline-none focus:border-violet-400 disabled:opacity-50" @change="yearChoice = Number(($event.target as HTMLSelectElement).value)">
            <option v-for="y in data.years" :key="y" :value="y">{{ y }}</option>
          </select>
        </div>
      </div>

      <div v-if="latest" class="grid grid-cols-2 gap-3 sm:grid-cols-5">
        <DetailMetricCard :label="L.kpiSize" :value="fmtRate(latest.ratePerSizeYtd)" />
        <DetailMetricCard :label="L.kpiMM" :value="fmtPerMM(latest.ratePerMMYtd)" :tone="cardTone(latest.ratePerMMYtdTone)" />
        <DetailMetricCard :label="$t('project_detail.quality_section.stat_defects')" :value="latest.defectsYtd" />
        <DetailMetricCard :label="$t('project_detail.quality_section.stat_size', { unit: data.unit })" :value="fmtNum(latest.productSizeYtd)" />
        <DetailMetricCard :label="$t('project_detail.quality_section.stat_billable')" :value="fmtNum(latest.billableYtd)" />
      </div>

      <div class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div class="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 bg-slate-50/50 px-4 py-2.5">
          <span class="flex items-center gap-1.5 text-[12px] font-black uppercase tracking-wide text-slate-500"><Egg v-if="isDefect" :size="14" /><Bug v-else :size="14" />{{ L.table }}</span>
          <template v-if="data.canEdit">
            <div v-if="drafts" class="flex items-center gap-2">
              <Button variant="outline" size="sm" :disabled="saving" @click="drafts = null"><template #leftIcon><X :size="14" /></template>{{ $t('common.btn_cancel') }}</Button>
              <Button variant="primary" size="sm" :loading="saving" @click="save"><template #leftIcon><Save :size="14" /></template>{{ $t('project_detail.quality_section.btn_save') }}</Button>
            </div>
            <Button v-else variant="outline" size="sm" :disabled="data.rows.length === 0 || query.isFetching.value" @click="startEdit"><template #leftIcon><Pencil :size="14" /></template>{{ $t('project_detail.quality_section.btn_edit') }}</Button>
          </template>
          <span v-else class="flex items-center gap-1.5 text-[11px] font-semibold text-slate-400"><Lock :size="12" />{{ $t('project_detail.quality_section.lbl_read_only') }}</span>
        </div>
        <p v-if="data.rows.length === 0" class="px-4 py-8 text-center text-[12.5px] font-semibold text-slate-400">{{ $t('project_detail.quality_section.empty_months') }}</p>
        <div v-else class="overflow-x-auto">
          <table class="w-full min-w-[860px] border-collapse text-left">
            <thead>
              <tr class="border-b border-slate-200 bg-slate-50/80">
                <th :class="TH" rowspan="2">{{ $t('project_detail.col_month') }}</th>
                <th :class="TL" colspan="2">{{ $t('project_detail.quality_section.col_input') }}</th>
                <th :class="TL" rowspan="2">{{ $t('project_detail.quality_section.col_billable') }}<div class="font-semibold normal-case tracking-normal text-slate-400">{{ $t('project_detail.quality_section.col_billable_hint') }}</div></th>
                <th :class="TL" colspan="2">{{ L.colMonthly }}</th>
                <th :class="TL" colspan="3">{{ L.colYtd }}</th>
              </tr>
              <tr class="border-b border-slate-200 bg-slate-50/80">
                <th :class="cn(TL, 'text-rose-600')">{{ L.colDefects }}</th>
                <th :class="cn(TH, 'text-center')">{{ $t('project_detail.quality_section.col_size', { unit: data.unit }) }}</th>
                <th :class="TL">{{ $t('project_detail.quality_section.col_per_size') }}</th>
                <th :class="cn(TH, 'text-center')">{{ $t('project_detail.quality_section.col_per_mm') }}</th>
                <th :class="TL">{{ $t('project_detail.quality_section.col_defect_count') }}</th>
                <th :class="cn(TH, 'text-center')">{{ $t('project_detail.quality_section.col_per_size') }}</th>
                <th :class="cn(TH, 'text-center')">{{ $t('project_detail.quality_section.col_per_mm') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(r, idx) in data.rows" :key="r.month" :class="cn('border-b border-slate-100 text-[12.5px] last:border-b-0', idx % 2 === 0 ? 'bg-white' : 'bg-slate-50/60')">
                <td class="whitespace-nowrap p-2.5 font-bold text-slate-700">{{ monthLabel(r.month) }}</td>
                <template v-if="drafts && drafts[r.month]">
                  <td class="border-l border-slate-100 p-2"><input v-model="drafts[r.month]!.defects" type="number" min="0" step="1" placeholder="—" :aria-label="`${monthLabel(r.month)} — ${L.colDefects}`" :class="INPUT" /></td>
                  <td class="p-2"><input v-model="drafts[r.month]!.productSize" type="number" min="0" step="any" placeholder="0" :aria-label="`${monthLabel(r.month)} — ${data.unit}`" :class="INPUT" /></td>
                </template>
                <template v-else>
                  <td class="border-l border-slate-100 p-2.5 text-center font-bold text-rose-600">{{ r.hasData ? r.defects : '—' }}</td>
                  <td class="p-2.5 text-center text-slate-600">{{ r.hasData ? fmtNum(r.productSize) : '—' }}</td>
                </template>
                <td class="border-l border-slate-100 p-2.5 text-center tabular-nums text-slate-600">{{ fmtNum(r.billableMM) }}</td>
                <td class="border-l border-slate-100 p-2.5 text-center tabular-nums">{{ fmtRate(r.ratePerSize) }}</td>
                <td class="p-2"><span :class="cn('block rounded-md px-2 py-1 text-center font-black tabular-nums', TONE_CELL[r.ratePerMMTone])">{{ fmtPerMM(r.ratePerMM) }}</span></td>
                <td class="border-l border-slate-100 p-2.5 text-center tabular-nums">{{ r.defectsYtd }}</td>
                <td class="p-2.5 text-center tabular-nums">{{ fmtRate(r.ratePerSizeYtd) }}</td>
                <td class="p-2"><span :class="cn('block rounded-md px-2 py-1 text-center font-black tabular-nums', TONE_CELL[r.ratePerMMYtdTone])">{{ fmtPerMM(r.ratePerMMYtd) }}</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>
  </div>
</template>
