<script setup lang="ts">
// Port ProjectRiskScreen + RiskSummaryCards + RiskMatrixGuide + RiskMatrix + RiskPriorityList + RiskToolbar + RiskTable + RiskDialog:
// sổ đăng ký rủi ro. Điểm = Xác suất × Ảnh hưởng (1–25), mức 1–4 Thấp · 5–9 Trung bình · 10–16 Cao · 20–25 Nghiêm trọng —
// BE tính (score, level, quá hạn, ma trận đếm rủi ro đang theo dõi); điểm xem trước trong dialog tính lại cùng công thức.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { ChevronRight, Info, Pencil, Plus, ShieldAlert, Trash2, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import DetailMetricCard from '@/modules/project/components/DetailMetricCard.vue';
import { projectDetailApi, type ProjectDetail, type Risk, type RiskCategory, type RiskInput, type RiskLevel, type RiskStatus, type RiskStrategy } from '@/modules/project/api/project-detail.api';
import { enumLabel } from '@/shared/i18n/enum-label';
import { firstFieldErrors } from '@/api/http';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';
import { matchesQuery } from './management-search';

const props = defineProps<{ project: ProjectDetail; query: string }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();
const q = useQuery({
  queryKey: computed(() => ['projects', 'risks', props.project.id] as const),
  queryFn: ({ signal }) => projectDetailApi.risks(props.project.id, { signal }),
});
const data = computed(() => q.data.value);
const canEdit = computed(() => !!data.value?.canEdit);
const refresh = () => void queryClient.invalidateQueries({ queryKey: ['projects', 'risks', props.project.id] });

const SCALE = [1, 2, 3, 4, 5] as const;
const CATEGORIES: RiskCategory[] = ['schedule', 'technical', 'resource', 'customer', 'cost', 'quality'];
const STRATEGIES: RiskStrategy[] = ['avoid', 'mitigate', 'transfer', 'accept'];
const STATUSES: RiskStatus[] = ['open', 'in_progress', 'resolved', 'closed'];
const LEVELS: RiskLevel[] = ['low', 'medium', 'high', 'critical'];
const LEVEL_RANGE: Record<RiskLevel, string> = { low: '1–4', medium: '5–9', high: '10–16', critical: '20–25' };
/** Cùng thang với BE (project-extra-routes.mjs → riskLevel): dùng cho ô ma trận và điểm xem trước trong dialog. */
const levelOf = (score: number): RiskLevel => (score >= 20 ? 'critical' : score >= 10 ? 'high' : score >= 5 ? 'medium' : 'low');
const LEVEL_CHIP: Record<RiskLevel, string> = { low: 'bg-emerald-50 text-emerald-700', medium: 'bg-yellow-50 text-yellow-700', high: 'bg-orange-50 text-orange-700', critical: 'bg-rose-50 text-rose-700' };
const LEVEL_CELL: Record<RiskLevel, string> = { low: 'bg-emerald-100 text-emerald-800', medium: 'bg-yellow-100 text-yellow-800', high: 'bg-orange-200 text-orange-900', critical: 'bg-rose-300 text-rose-950' };
const LEVEL_TEXT: Record<RiskLevel, string> = { low: 'text-emerald-700', medium: 'text-yellow-700', high: 'text-orange-700', critical: 'text-rose-700' };
const STATUS_CHIP: Record<RiskStatus, string> = { open: 'bg-blue-50 text-blue-600', in_progress: 'bg-violet-50 text-violet-600', resolved: 'bg-emerald-50 text-emerald-600', closed: 'bg-slate-100 text-slate-500' };

const statusFilter = ref<'active' | 'done' | 'all'>('active');
const cell = ref<string | null>(null);
const expanded = ref(new Set<string>());
const rows = computed(() => (data.value?.items ?? [])
  .filter((r) => statusFilter.value === 'all' || (statusFilter.value === 'active' ? r.isActive : !r.isActive))
  .filter((r) => !cell.value || `${r.probability}-${r.impact}` === cell.value)
  .filter((r) => matchesQuery(props.query, r.name, r.code, r.ownerName))
  // Đang theo dõi trước, điểm cao trước, hạn gần trước
  .sort((a, b) => Number(b.isActive) - Number(a.isActive) || b.score - a.score || a.dueDate.localeCompare(b.dueDate)));
const priority = computed(() => (data.value?.items ?? []).filter((r) => r.isActive).sort((a, b) => b.score - a.score || a.dueDate.localeCompare(b.dueDate)).slice(0, 5));
function selectCell(key: string) {
  cell.value = cell.value === key ? null : key;
  if (cell.value) statusFilter.value = 'active';
}
function setFilter(f: 'active' | 'done' | 'all') {
  statusFilter.value = f;
  if (f !== 'active') cell.value = null;
}
function toggle(id: string) {
  const next = new Set(expanded.value);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  expanded.value = next;
}
function focusRisk(r: Risk) {
  statusFilter.value = 'active';
  cell.value = null;
  expanded.value = new Set(expanded.value).add(r.id);
}

const dialogOpen = ref(false);
const editing = ref<Risk | null>(null);
const today = new Date().toISOString().slice(0, 10);
const emptyForm = (): RiskInput => ({ name: '', description: '', category: 'schedule', probability: 3, impact: 3, strategy: 'mitigate', responsePlan: '', ownerId: null, dueDate: null, status: 'open', identifiedAt: today });
const form = ref<RiskInput>(emptyForm());
const errors = ref<Record<string, string>>({});
const saving = ref(false);
function open(r: Risk | null) {
  editing.value = r;
  errors.value = {};
  form.value = r
    ? { name: r.name, description: r.description ?? '', category: r.category, probability: r.probability, impact: r.impact, strategy: r.strategy, responsePlan: r.responsePlan ?? '', ownerId: r.ownerId, dueDate: r.dueDate, status: r.status, identifiedAt: r.identifiedAt }
    : emptyForm();
  dialogOpen.value = true;
}
const draftScore = computed(() => form.value.probability * form.value.impact);
const ownerOptions = computed(() => [{ value: '', label: t('project_detail.risk_section.lbl_owner_none') }, ...(data.value?.owners ?? []).map((o) => ({ value: o.id, label: `${o.fullName} (${o.code})` }))]);
const ownerModel = computed({ get: () => form.value.ownerId ?? '', set: (v: string | string[] | null) => (form.value.ownerId = typeof v === 'string' && v ? v : null) });
const options = <T extends string>(list: T[], name: 'risk_category' | 'risk_strategy' | 'risk_status') => list.map((v) => ({ value: v, label: enumLabel(name, v) }));
async function submit() {
  saving.value = true;
  try {
    const res = editing.value ? await projectDetailApi.risk.update(props.project.id, editing.value.id, form.value) : await projectDetailApi.risk.create(props.project.id, form.value);
    toast.success(res.messageText);
    dialogOpen.value = false;
    refresh();
  } catch (err) {
    errors.value = firstFieldErrors(err);
    if (Object.keys(errors.value).length === 0) toast.error((err as Error).message);
  } finally {
    saving.value = false;
  }
}
async function remove(r: Risk) {
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.risk_section.confirm_delete_title'), description: t('project_detail.risk_section.confirm_delete_desc', { code: r.code, name: r.name }), confirmText: t('project_detail.btn_confirm_delete') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.risk.remove(props.project.id, r.id)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const scaleDetail = (r: Risk) => t('project_detail.risk_section.lbl_scale_detail', { p: r.probability, pLabel: enumLabel('risk_probability', `level_${r.probability}`), i: r.impact, iLabel: enumLabel('risk_impact', `level_${r.impact}`) });
</script>

<template>
  <div v-if="!data" class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
  <div v-else class="flex flex-col gap-4">
    <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
      <DetailMetricCard :label="$t('project_detail.risk_section.stat_card_active')" :value="data.summary.active" />
      <DetailMetricCard :label="$t('project_detail.risk_section.stat_card_severe')" :value="data.summary.highOrCritical" :tone="data.summary.highOrCritical > 0 ? 'red' : 'default'" />
      <DetailMetricCard :label="$t('project_detail.risk_section.stat_card_overdue')" :value="data.summary.overdue" :tone="data.summary.overdue > 0 ? 'red' : 'default'" />
      <DetailMetricCard :label="$t('project_detail.risk_section.stat_card_done')" :value="data.summary.resolved" :tone="data.summary.resolved > 0 ? 'green' : 'default'" />
    </div>

    <div class="rounded-2xl border border-violet-100 bg-violet-50/60 p-4">
      <div class="mb-1.5 flex items-center gap-2 text-[13px] font-black text-violet-700"><Info :size="15" />{{ $t('project_detail.risk_section.lbl_guide_title') }}</div>
      <p class="mb-1.5 text-[12.5px] font-bold text-slate-700">{{ $t('project_detail.risk_section.lbl_guide_formula') }}</p>
      <ul class="list-disc space-y-0.5 pl-4 text-[11.5px] text-slate-500">
        <li><span class="font-bold text-slate-600">{{ $t('project_detail.risk_section.lbl_guide_read_label') }}</span>: {{ $t('project_detail.risk_section.lbl_guide_read') }}</li>
        <li>
          <span class="font-bold text-slate-600">{{ $t('project_detail.risk_section.lbl_guide_color_label') }}</span>:
          <template v-for="(l, i) in LEVELS" :key="l"><span v-if="i > 0"> · </span>{{ LEVEL_RANGE[l] }} <span :class="['font-bold', LEVEL_TEXT[l]]">{{ enumLabel('risk_matrix_level', l) }}</span></template>
          {{ $t('project_detail.risk_section.lbl_guide_color_hint') }}
        </li>
        <li><span class="font-bold text-slate-600">{{ $t('project_detail.risk_section.lbl_guide_priority_label') }}</span>: {{ $t('project_detail.risk_section.lbl_guide_priority') }}</li>
        <li><span class="font-bold text-slate-600">{{ $t('project_detail.risk_section.lbl_guide_filter_label') }}</span>: {{ $t('project_detail.risk_section.lbl_guide_filter') }}</li>
      </ul>
    </div>

    <div class="grid grid-cols-1 gap-4 lg:grid-cols-[320px_minmax(0,1fr)]">
      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <div class="mb-2 flex items-center justify-between">
          <h4 class="text-[13px] font-black text-slate-800">{{ $t('project_detail.risk_section.lbl_matrix_title') }}</h4>
          <button v-if="cell" type="button" class="flex items-center gap-1 text-[11px] font-bold text-slate-400 hover:text-pr" @click="cell = null"><X :size="12" />{{ $t('project_detail.risk_section.lbl_clear_cell') }}</button>
        </div>
        <div class="flex gap-1.5">
          <div class="flex w-4 items-center justify-center"><span class="-rotate-90 whitespace-nowrap text-[10px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.risk_section.lbl_probability_axis') }}</span></div>
          <div class="flex-1">
            <div v-for="p in [...SCALE].reverse()" :key="p" class="mb-1 grid grid-cols-[16px_repeat(5,1fr)] gap-1">
              <span class="flex items-center justify-center text-[10px] font-bold text-slate-400">{{ p }}</span>
              <button
                v-for="i in SCALE"
                :key="i"
                type="button"
                :title="$t('project_detail.risk_section.lbl_cell_tooltip', { p, pLabel: enumLabel('risk_probability', `level_${p}`), i, iLabel: enumLabel('risk_impact', `level_${i}`), score: p * i, level: enumLabel('risk_matrix_level', levelOf(p * i)) })"
                :class="cn('flex aspect-square items-center justify-center rounded-md text-[12px] font-black transition-transform hover:scale-105', LEVEL_CELL[levelOf(p * i)], cell === `${p}-${i}` && 'ring-2 ring-slate-800 ring-offset-1')"
                @click="selectCell(`${p}-${i}`)"
              >{{ data.matrix[`${p}-${i}`] || '' }}</button>
            </div>
            <div class="grid grid-cols-[16px_repeat(5,1fr)] gap-1">
              <span />
              <span v-for="i in SCALE" :key="i" class="text-center text-[10px] font-bold text-slate-400">{{ i }}</span>
            </div>
            <div class="mt-1 text-center text-[10px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.risk_section.lbl_impact_axis') }}</div>
          </div>
        </div>
      </div>
      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <h4 class="mb-2 text-[13px] font-black text-slate-800">{{ $t('project_detail.risk_section.lbl_priority_title') }}</h4>
        <p v-if="priority.length === 0" class="py-6 text-center text-[12.5px] text-slate-400">{{ $t('project_detail.risk_section.lbl_priority_empty') }}</p>
        <ul v-else class="flex flex-col gap-1.5">
          <li v-for="r in priority" :key="r.id">
            <button type="button" class="flex w-full items-center gap-3 rounded-xl border border-slate-100 px-3 py-2 text-left hover:border-pr/40 hover:bg-pr/5" @click="focusRisk(r)">
              <span :class="cn('flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-[12px] font-black', LEVEL_CELL[r.level])">{{ r.score }}</span>
              <span class="min-w-0 flex-1">
                <span class="block truncate text-[12.5px] font-bold text-slate-700">{{ r.code }} · {{ r.name }}</span>
                <span class="text-[11px] text-slate-400">{{ r.ownerName ?? $t('project_detail.risk_section.lbl_owner_none') }} · {{ $t('project_detail.risk_section.lbl_due_prefix') }} {{ formatDate(r.dueDate) }}<span v-if="r.isOverdue" class="font-bold text-rose-500"> {{ $t('project_detail.risk_section.lbl_overdue_inline') }}</span></span>
              </span>
            </button>
          </li>
        </ul>
      </div>
    </div>

    <div class="flex flex-wrap items-center justify-between gap-3">
      <div class="flex flex-wrap items-center gap-2">
        <div class="flex rounded-xl border border-slate-200 bg-white p-0.5">
          <button v-for="f in (['active', 'done', 'all'] as const)" :key="f" type="button" :class="cn('rounded-lg px-3 py-1 text-[12px] font-bold', statusFilter === f ? 'bg-pr text-white' : 'text-slate-500 hover:bg-slate-50')" @click="setFilter(f)">
            {{ f === 'active' ? $t('project_detail.risk_section.lbl_filter_active') : f === 'done' ? $t('project_detail.risk_section.lbl_filter_done') : $t('project_detail.risk_section.lbl_filter_all') }}
          </button>
        </div>
        <span v-if="cell" class="flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1 text-[11.5px] font-bold text-slate-600">
          {{ $t('project_detail.risk_section.lbl_cell_filter', { p: cell.split('-')[0], i: cell.split('-')[1] }) }}
          <button type="button" :aria-label="$t('project_detail.risk_section.lbl_clear_cell')" @click="cell = null"><X :size="12" /></button>
        </span>
      </div>
      <Button v-if="canEdit" variant="primary" size="sm" @click="open(null)"><template #leftIcon><Plus :size="14" /></template>{{ $t('project_detail.risk_section.btn_add') }}</Button>
    </div>

    <div v-if="data.items.length === 0" class="flex h-48 flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 px-6 text-center">
      <ShieldAlert class="text-slate-300" :size="28" />
      <p class="text-[13px] font-bold text-slate-500">{{ $t('project_detail.risk_section.empty_title') }}</p>
      <p class="max-w-sm text-[12px] text-slate-400">{{ $t('project_detail.risk_section.empty_hint') }}</p>
    </div>
    <div v-else class="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
      <table class="w-full min-w-[900px] border-collapse text-left">
        <thead>
          <tr class="border-b border-slate-200 bg-slate-50/80 text-[11px] font-bold uppercase tracking-wide text-slate-500">
            <th class="w-8 p-3" />
            <th class="w-[70px] p-3">{{ $t('project_detail.risk_section.col_code') }}</th>
            <th class="p-3">{{ $t('project_detail.risk_section.col_risk') }}</th>
            <th class="w-[140px] p-3">{{ $t('project_detail.risk_section.col_level') }}</th>
            <th class="w-[110px] p-3">{{ $t('project_detail.risk_section.col_strategy') }}</th>
            <th class="w-[150px] p-3">{{ $t('project_detail.risk_section.col_owner') }}</th>
            <th class="w-[110px] p-3">{{ $t('project_detail.risk_section.col_due') }}</th>
            <th class="w-[120px] p-3">{{ $t('project_detail.risk_section.col_status') }}</th>
            <th v-if="canEdit" class="w-[76px] p-3" />
          </tr>
        </thead>
        <tbody>
          <tr v-if="rows.length === 0"><td colspan="9" class="p-6 text-center text-[12.5px] text-slate-400">{{ $t('project_detail.risk_section.empty_match') }}</td></tr>
          <template v-for="r in rows" :key="r.id">
            <tr class="group/row cursor-pointer border-b border-slate-100 text-[12.5px] hover:bg-slate-50/50" @click="toggle(r.id)">
              <td class="p-3"><ChevronRight :size="14" :class="['text-slate-400 transition-transform', expanded.has(r.id) && 'rotate-90']" /></td>
              <td class="p-3 font-black text-slate-700">{{ r.code }}</td>
              <td class="p-3">
                <div class="font-bold text-slate-800">{{ r.name }}</div>
                <div class="text-[11px] text-slate-400">{{ enumLabel('risk_category', r.category) }} · {{ $t('project_detail.risk_section.lbl_identified_prefix') }} {{ formatDate(r.identifiedAt) }}</div>
              </td>
              <td class="p-3"><span :class="cn('rounded-full px-2 py-0.5 text-[11px] font-bold', LEVEL_CHIP[r.level])">{{ r.score }} · {{ enumLabel('risk_matrix_level', r.level) }}</span></td>
              <td class="p-3 text-slate-600">{{ enumLabel('risk_strategy', r.strategy) }}</td>
              <td class="p-3 text-slate-600">{{ r.ownerName ?? '—' }}</td>
              <td :class="cn('p-3', r.isOverdue ? 'font-bold text-rose-600' : 'text-slate-600')">{{ formatDate(r.dueDate) }}<div v-if="r.isOverdue" class="text-[10.5px] font-black uppercase">{{ $t('project_detail.risk_section.lbl_overdue') }}</div></td>
              <td class="p-3"><span :class="cn('rounded-full px-2 py-0.5 text-[11px] font-bold', STATUS_CHIP[r.status])">{{ enumLabel('risk_status', r.status) }}</span></td>
              <td v-if="canEdit" class="p-3" @click.stop>
                <div class="flex items-center gap-1 opacity-0 transition-opacity group-hover/row:opacity-100">
                  <button type="button" class="rounded-md p-1.5 text-slate-400 hover:bg-violet-100 hover:text-pr" :aria-label="$t('common.btn_edit')" @click="open(r)"><Pencil :size="13" /></button>
                  <button type="button" class="rounded-md p-1.5 text-slate-400 hover:bg-rose-100 hover:text-rose-600" :aria-label="$t('common.btn_delete')" @click="remove(r)"><Trash2 :size="13" /></button>
                </div>
              </td>
            </tr>
            <tr v-if="expanded.has(r.id)" class="border-b border-slate-100 bg-slate-50/40">
              <td />
              <td colspan="8" class="p-3 pt-0 text-[12px] text-slate-600">
                <div class="grid grid-cols-1 gap-3 md:grid-cols-3">
                  <div><div class="text-[10.5px] font-bold uppercase text-slate-400">{{ $t('project_detail.risk_section.lbl_description') }}</div><p class="whitespace-pre-line">{{ r.description || '—' }}</p></div>
                  <div><div class="text-[10.5px] font-bold uppercase text-slate-400">{{ $t('project_detail.risk_section.lbl_response_plan') }}</div><p class="whitespace-pre-line">{{ r.responsePlan || '—' }}</p></div>
                  <div>
                    <div class="text-[10.5px] font-bold uppercase text-slate-400">{{ $t('project_detail.risk_section.lbl_score') }}</div>
                    <p>{{ scaleDetail(r) }}</p>
                    <p class="mt-1 text-[11px] text-slate-400">{{ $t('project_detail.lbl_updated_by', { name: r.updatedByName ?? '—', date: formatDate(r.updatedAt) }) }}</p>
                  </div>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <BaseDialog v-model:open="dialogOpen" :title="editing ? $t('project_detail.risk_section.btn_edit_title', { code: editing.code }) : $t('project_detail.risk_section.btn_add')" :description="$t('project_detail.risk_section.lbl_dialog_desc')" max-width="2xl">
      <div class="grid grid-cols-1 gap-4 md:grid-cols-2">
        <FormItem class="md:col-span-2" :label="$t('project_detail.risk_section.lbl_name')" required :error="errors.name"><BaseInput v-model="form.name" :placeholder="$t('project_detail.risk_section.ph_name')" :error="!!errors.name" /></FormItem>
        <FormSelect v-model="form.category" :label="$t('project_detail.risk_section.lbl_category')" :options="options(CATEGORIES, 'risk_category')" :allow-clear="false" :error="errors.category" required />
        <FormSelect v-model="form.strategy" :label="$t('project_detail.risk_section.lbl_strategy')" :options="options(STRATEGIES, 'risk_strategy')" :allow-clear="false" :error="errors.strategy" required />
        <FormItem :label="$t('project_detail.risk_section.lbl_probability')" required :error="errors.probability">
          <div class="flex gap-1">
            <button v-for="n in SCALE" :key="n" type="button" :title="enumLabel('risk_probability', `level_${n}`)" :class="cn('h-9 flex-1 rounded-lg border text-[13px] font-black', form.probability === n ? 'border-pr bg-pr text-white' : 'border-slate-200 text-slate-500 hover:border-pr')" @click="form.probability = n">{{ n }}</button>
          </div>
          <span class="text-[11px] text-slate-400">{{ enumLabel('risk_probability', `level_${form.probability}`) }}</span>
        </FormItem>
        <FormItem :label="$t('project_detail.risk_section.lbl_impact')" required :error="errors.impact">
          <div class="flex gap-1">
            <button v-for="n in SCALE" :key="n" type="button" :title="enumLabel('risk_impact', `level_${n}`)" :class="cn('h-9 flex-1 rounded-lg border text-[13px] font-black', form.impact === n ? 'border-pr bg-pr text-white' : 'border-slate-200 text-slate-500 hover:border-pr')" @click="form.impact = n">{{ n }}</button>
          </div>
          <span class="text-[11px] text-slate-400">{{ enumLabel('risk_impact', `level_${form.impact}`) }}</span>
        </FormItem>
        <div class="md:col-span-2 flex items-center gap-2 rounded-xl bg-slate-50 px-3 py-2 text-[12.5px]">
          <span class="font-bold text-slate-500">{{ $t('project_detail.risk_section.lbl_score') }}</span>
          <span :class="cn('rounded-full px-2.5 py-0.5 text-[12px] font-black', LEVEL_CHIP[levelOf(draftScore)])">{{ draftScore }} · {{ enumLabel('risk_matrix_level', levelOf(draftScore)) }}</span>
        </div>
        <FormItem class="md:col-span-2" :label="$t('project_detail.risk_section.lbl_description')" :error="errors.description"><BaseTextarea v-model="form.description" :rows="2" :placeholder="$t('project_detail.risk_section.ph_description')" /></FormItem>
        <FormItem class="md:col-span-2" :label="$t('project_detail.risk_section.lbl_response_plan')" :error="errors.responsePlan"><BaseTextarea v-model="form.responsePlan" :rows="2" :placeholder="$t('project_detail.risk_section.ph_response_plan')" /></FormItem>
        <FormSelect v-model="ownerModel" :label="$t('project_detail.risk_section.lbl_owner')" :options="ownerOptions" :error="errors.ownerId" searchable />
        <FormSelect v-model="form.status" :label="$t('project_detail.risk_section.lbl_status')" :options="options(STATUSES, 'risk_status')" :allow-clear="false" :error="errors.status" required />
        <FormDatePicker v-model="form.identifiedAt" :label="$t('project_detail.risk_section.lbl_identified_at')" :error="errors.identifiedAt" required />
        <FormDatePicker v-model="form.dueDate" :label="$t('project_detail.risk_section.lbl_due_date')" :error="errors.dueDate" required />
      </div>
      <template #footer>
        <Button variant="outline" @click="dialogOpen = false">{{ $t('common.btn_cancel') }}</Button>
        <Button variant="primary" :loading="saving" @click="submit">{{ editing ? $t('common.btn_save') : $t('project_detail.risk_section.btn_add_action') }}</Button>
      </template>
    </BaseDialog>
  </div>
</template>
