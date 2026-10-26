<script setup lang="ts">
// Port ProjectPcvScreen + ProjectPcvHistoryTable + PcvChecklistItemDialog: chấm checklist PCV theo tháng (Đạt / Không đạt + ghi chú lỗi),
// KPI tháng, sửa tiêu chí riêng của dự án, lịch sử các tháng. Điểm, mức màu và lịch sử do BE tính
// (PCV = Đạt / (Đạt + Không đạt) × 100; ngưỡng ở Quản trị → Quy tắc). Tháng xem nằm trên URL (?pcvMonth=).
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { AlertTriangle, BarChart3, CheckCircle2, Lock, Pencil, Plus, Trash2, XCircle } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import MonthNavigator from '@/shared/components/form/MonthNavigator.vue';
import DetailMetricCard from '@/modules/project/components/DetailMetricCard.vue';
import { projectDetailApi, type PcvItem, type PcvResult, type ProjectDetail } from '@/modules/project/api/project-detail.api';
import { firstFieldErrors } from '@/api/http';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ project: ProjectDetail }>();
const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const thisMonth = new Date().toISOString().slice(0, 7);
const month = computed({
  get: () => (typeof route.query.pcvMonth === 'string' && /^\d{4}-\d{2}$/.test(route.query.pcvMonth) ? route.query.pcvMonth : thisMonth),
  set: (v: string) => void router.replace({ query: { ...route.query, pcvMonth: v } }),
});
const query = useQuery({
  queryKey: computed(() => ['projects', 'pcv', props.project.id, month.value] as const),
  queryFn: ({ signal }) => projectDetailApi.pcv(props.project.id, month.value, { signal }),
});
const data = computed(() => query.data.value);
const monthLabel = computed(() => t('project_detail.lbl_month_year', { month: Number(month.value.slice(5, 7)), year: month.value.slice(0, 4) }));

// Bản nháp kết quả: khởi tạo lại từ đánh giá đã lưu mỗi khi đổi tháng / sau khi lưu
const draft = ref<Record<string, PcvResult>>({});
watch(() => data.value, (d) => {
  draft.value = Object.fromEntries((d?.assessment?.results ?? []).map((r) => [r.itemId, { ...r }]));
}, { immediate: true });

/** Điểm bản nháp (cùng công thức với BE, để KPI đổi ngay khi bấm): Đạt / (Đạt + Không đạt) × 100; chưa chọn bị loại. */
const summary = computed(() => {
  const list = Object.values(draft.value);
  const passed = list.filter((r) => r.passed).length;
  const checked = list.length;
  return { checked, passed, failed: checked - passed, score: checked > 0 ? (passed / checked) * 100 : 0 };
});
const tone = computed(() => {
  const th = data.value?.thresholds;
  if (!th || summary.value.checked === 0) return 'default' as const;
  const s = summary.value.score;
  return s >= th.greenMin ? ('green' as const) : s >= th.orangeMin ? ('default' as const) : ('red' as const);
});
const canEdit = computed(() => !!data.value?.canEdit);
const showGrid = computed(() => canEdit.value || !!data.value?.assessment);

function markPassed(item: PcvItem) {
  if (draft.value[item.id]?.passed) delete draft.value[item.id];
  else draft.value[item.id] = { itemId: item.id, passed: true, note: null };
}
const noteItem = ref<PcvItem | null>(null);
const noteText = ref('');
const noteError = ref('');
function openNote(item: PcvItem) {
  noteText.value = draft.value[item.id]?.note ?? '';
  noteError.value = '';
  noteItem.value = item;
}
function markFailed(item: PcvItem) {
  const cur = draft.value[item.id];
  if (cur && !cur.passed) delete draft.value[item.id];
  else openNote(item);
}
function submitNote() {
  if (!noteItem.value) return;
  if (!noteText.value.trim()) {
    noteError.value = t('validation.required');
    return;
  }
  draft.value[noteItem.value.id] = { itemId: noteItem.value.id, passed: false, note: noteText.value.trim() };
  noteItem.value = null;
}
const saving = ref(false);
const refresh = () => void queryClient.invalidateQueries({ queryKey: ['projects', 'pcv', props.project.id] });
async function save() {
  const results = Object.values(draft.value);
  if (results.length === 0) {
    toast.error(t('project_detail.pcv_section.btn_save_empty'));
    return;
  }
  saving.value = true;
  try {
    toast.success((await projectDetailApi.savePcv(props.project.id, month.value, results)).messageText);
    refresh();
  } catch (err) {
    toast.error(Object.values(firstFieldErrors(err))[0] ?? (err as Error).message);
  } finally {
    saving.value = false;
  }
}

// Tiêu chí riêng của dự án
const itemOpen = ref(false);
const editingItem = ref<PcvItem | null>(null);
const itemForm = ref({ category: '', label: '' });
const itemErrors = ref<Record<string, string>>({});
const itemSaving = ref(false);
function openItem(item: PcvItem | null) {
  editingItem.value = item;
  itemForm.value = { category: item?.category ?? '', label: item?.label ?? '' };
  itemErrors.value = {};
  itemOpen.value = true;
}
async function submitItem() {
  itemSaving.value = true;
  try {
    const res = editingItem.value ? await projectDetailApi.pcvItem.update(props.project.id, editingItem.value.id, itemForm.value) : await projectDetailApi.pcvItem.create(props.project.id, itemForm.value);
    toast.success(res.messageText);
    itemOpen.value = false;
    refresh();
  } catch (err) {
    itemErrors.value = firstFieldErrors(err);
    if (Object.keys(itemErrors.value).length === 0) toast.error((err as Error).message);
  } finally {
    itemSaving.value = false;
  }
}
async function removeItem(item: PcvItem) {
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.pcv_section.confirm_delete_title'), description: t('project_detail.pcv_section.confirm_delete_desc', { label: item.label }), confirmText: t('project_detail.btn_confirm_delete') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.pcvItem.remove(props.project.id, item.id)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const TONE_TEXT: Record<string, string> = { green: 'text-emerald-600', orange: 'text-amber-600', red: 'text-rose-600', none: 'text-slate-400' };
const RESULT_BUTTON = 'flex h-8 items-center gap-1 rounded-lg border px-2.5 text-[12px] font-bold transition-colors';
const ICON_BUTTON = 'flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 transition-colors';
</script>

<template>
  <div class="flex flex-col gap-5">
    <div class="rounded-2xl border border-violet-100 bg-violet-50/60 p-4">
      <div class="mb-1.5 flex items-center gap-2 text-[13px] font-black text-violet-700"><BarChart3 :size="15" />{{ $t('project_detail.pcv_section.hint_formula_title') }}</div>
      <p class="mb-1.5 text-[12.5px] font-bold text-slate-700">{{ $t('project_detail.pcv_section.hint_formula') }}</p>
      <ul class="list-disc space-y-0.5 pl-4 text-[11.5px] text-slate-500">
        <li>{{ $t('project_detail.pcv_section.hint_formula_pass_fail') }}</li>
        <li>{{ $t('project_detail.pcv_section.hint_formula_note_required') }}</li>
        <li>{{ $t('project_detail.pcv_section.hint_formula_unchecked') }}</li>
        <li>{{ $t('project_detail.pcv_section.hint_formula_monthly') }}</li>
        <li>{{ $t('project_detail.pcv_section.hint_formula_tone', { green: data?.thresholds.greenMin ?? 90, orange: data?.thresholds.orangeMin ?? 70 }) }}</li>
      </ul>
      <p class="mt-2 flex items-center gap-1.5 text-[11px] font-semibold text-slate-400"><Lock :size="12" />{{ $t('project_detail.pcv_section.hint_formula_lock') }}</p>
    </div>

    <div class="flex flex-wrap items-center justify-between gap-3">
      <div class="flex w-fit items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 py-1.5 shadow-sm">
        <span class="text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.pcv_section.lbl_project_type') }}</span>
        <span class="text-[13px] font-black text-slate-700">{{ data?.projectGrade ?? '—' }}</span>
      </div>
      <div class="flex items-center gap-2">
        <span class="text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.pcv_section.lbl_month') }}</span>
        <MonthNavigator v-model="month" />
      </div>
    </div>
    <p v-if="data?.isFuture" class="text-[11.5px] font-semibold text-amber-600">{{ $t('project_detail.pcv_section.lbl_future_month') }}</p>

    <div v-if="!data" class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
    <template v-else>
      <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <DetailMetricCard :label="$t('project_detail.pcv_section.stat_score')" :value="summary.score" :formatter="(v) => `${v.toFixed(1)}%`" :tone="tone" />
        <DetailMetricCard :label="$t('project_detail.pcv_section.stat_checked')" :value="`${summary.checked}/${data.items.length}`" />
        <DetailMetricCard :label="$t('project_detail.pcv_section.stat_failed')" :value="summary.failed" :tone="summary.failed > 0 ? 'red' : 'default'" />
        <DetailMetricCard :label="$t('project_detail.pcv_section.stat_month')" :value="monthLabel" />
      </div>
      <p v-if="data.assessment" class="text-[11.5px] font-semibold text-slate-400">
        {{ $t('project_detail.pcv_section.lbl_assessed_by', { name: data.assessment.assessorName ?? '—', time: `${formatDate(data.assessment.assessedAt)} ${new Date(data.assessment.assessedAt).toTimeString().slice(0, 5)}` }) }}
      </p>

      <div v-if="!showGrid" class="flex h-48 flex-col items-center justify-center gap-2 rounded-[16px] border border-dashed border-slate-200 bg-slate-50/50 px-6 text-center">
        <BarChart3 class="text-slate-300" :size="26" />
        <p class="max-w-sm text-[12.5px] font-bold text-slate-400">{{ $t('project_detail.pcv_section.empty_assessment', { month: monthLabel }) }}</p>
      </div>
      <div v-else class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div v-if="canEdit" class="flex items-center justify-between gap-3 border-b border-slate-100 bg-slate-50/50 px-4 py-2.5">
          <span class="text-[11.5px] font-semibold text-slate-400">{{ $t('project_detail.pcv_section.lbl_checklist_hint') }}</span>
          <Button variant="outline" size="sm" @click="openItem(null)"><template #leftIcon><Plus :size="14" /></template>{{ $t('project_detail.pcv_section.btn_add_criterion') }}</Button>
        </div>
        <table class="w-full border-collapse text-left">
          <thead>
            <tr class="border-b border-slate-200 bg-slate-50/80">
              <th class="p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_detail.pcv_section.col_criterion') }}</th>
              <th class="w-[220px] p-3 text-center text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_detail.pcv_section.col_result') }}</th>
              <th class="w-[260px] p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_detail.pcv_section.col_note') }}</th>
              <th v-if="canEdit" class="w-[80px] p-3 text-center text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_detail.col_actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.items.length === 0"><td colspan="4" class="p-6 text-center text-[12.5px] text-slate-400">{{ $t('project_detail.pcv_section.empty_items') }}</td></tr>
            <tr v-for="(item, idx) in data.items" :key="item.id" :class="cn('border-b border-slate-100', idx % 2 === 0 ? 'bg-white' : 'bg-slate-50/60')">
              <td class="p-3 align-top">
                <div class="mb-0.5 text-[10px] font-black uppercase tracking-wide text-violet-500">{{ item.category }}</div>
                <div :class="cn('text-[13px] font-semibold text-slate-700', item.isDeleted && 'text-slate-400 line-through')">{{ item.label }}</div>
                <span v-if="item.isDeleted" class="text-[10.5px] font-bold text-slate-400">{{ $t('project_detail.pcv_section.lbl_deleted_criterion') }}</span>
              </td>
              <td class="p-3 align-top">
                <div v-if="canEdit && !item.isDeleted" class="flex items-center justify-center gap-1.5">
                  <button type="button" :aria-pressed="draft[item.id]?.passed === true" :class="cn(RESULT_BUTTON, draft[item.id]?.passed === true ? 'border-emerald-500 bg-emerald-500 text-white shadow-sm hover:border-emerald-600 hover:bg-emerald-600' : 'border-emerald-200 bg-emerald-50 text-emerald-600 hover:border-emerald-300 hover:bg-emerald-100 hover:text-emerald-700')" @click="markPassed(item)">
                    <CheckCircle2 :size="14" />{{ $t('project_detail.pcv_section.lbl_passed') }}
                  </button>
                  <button type="button" :aria-pressed="draft[item.id]?.passed === false" :class="cn(RESULT_BUTTON, draft[item.id]?.passed === false ? 'border-rose-500 bg-rose-500 text-white shadow-sm hover:border-rose-600 hover:bg-rose-600' : 'border-rose-200 bg-rose-50 text-rose-600 hover:border-rose-300 hover:bg-rose-100 hover:text-rose-700')" @click="markFailed(item)">
                    <XCircle :size="14" />{{ $t('project_detail.pcv_section.lbl_failed') }}
                  </button>
                </div>
                <span v-else-if="!draft[item.id]" class="block text-center text-[12px] font-semibold text-slate-300">{{ $t('project_detail.pcv_section.lbl_unchecked') }}</span>
                <span v-else :class="cn('flex items-center justify-center gap-1 text-[12.5px] font-bold', draft[item.id]?.passed ? 'text-emerald-600' : 'text-rose-600')">
                  <CheckCircle2 v-if="draft[item.id]?.passed" :size="14" /><XCircle v-else :size="14" />
                  {{ draft[item.id]?.passed ? $t('project_detail.pcv_section.lbl_passed') : $t('project_detail.pcv_section.lbl_failed') }}
                </span>
              </td>
              <td class="p-3 align-top">
                <div v-if="draft[item.id] && !draft[item.id]?.passed" class="flex items-start gap-1.5">
                  <p class="flex-1 whitespace-pre-line text-[12px] text-slate-600">{{ draft[item.id]?.note || '—' }}</p>
                  <button v-if="canEdit && !item.isDeleted" type="button" class="flex h-6 w-6 shrink-0 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-600" :aria-label="$t('project_detail.pcv_section.btn_edit_note')" @click="openNote(item)"><Pencil :size="12" /></button>
                </div>
                <span v-else class="text-[12px] text-slate-300">—</span>
              </td>
              <td v-if="canEdit" class="p-3 align-top">
                <div v-if="!item.isDeleted" class="flex items-center justify-center gap-1">
                  <button type="button" :class="cn(ICON_BUTTON, 'hover:bg-slate-100 hover:text-slate-600')" :aria-label="$t('project_detail.pcv_section.btn_edit_criterion')" @click="openItem(item)"><Pencil :size="14" /></button>
                  <button type="button" :class="cn(ICON_BUTTON, 'hover:bg-red-50 hover:text-red-500')" :aria-label="$t('project_detail.pcv_section.btn_delete_criterion')" @click="removeItem(item)"><Trash2 :size="14" /></button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
        <div v-if="canEdit" class="flex items-center justify-between gap-3 border-t border-slate-100 bg-slate-50/50 px-4 py-3">
          <span class="flex items-center gap-1.5 text-[11.5px] font-semibold text-slate-400"><AlertTriangle :size="13" />{{ $t('project_detail.pcv_section.lbl_overwrite_hint') }}</span>
          <Button variant="primary" :loading="saving" @click="save">{{ $t('project_detail.pcv_section.btn_save') }}</Button>
        </div>
      </div>

      <div v-if="data.history.length === 0" class="flex h-48 flex-col items-center justify-center gap-2 rounded-[16px] border border-dashed border-slate-200 bg-slate-50/50 px-6 text-center">
        <BarChart3 class="text-slate-300" :size="26" />
        <p class="max-w-sm text-[12.5px] font-bold text-slate-400">{{ $t('project_detail.pcv_section.empty_history') }}</p>
      </div>
      <div v-else class="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table class="w-full min-w-[680px] border-collapse text-left">
          <thead>
            <tr class="border-b border-slate-200 bg-slate-50/80 text-[11px] font-bold uppercase tracking-wide">
              <th class="p-3 text-slate-500">{{ $t('project_detail.col_month') }}</th>
              <th class="p-3 text-center text-slate-500">{{ $t('project_detail.pcv_section.stat_checked') }}</th>
              <th class="p-3 text-center text-emerald-700">{{ $t('project_detail.pcv_section.lbl_passed') }}</th>
              <th class="p-3 text-center text-rose-700">{{ $t('project_detail.pcv_section.lbl_failed') }}</th>
              <th class="p-3 text-center text-slate-500">PCV</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(h, i) in data.history" :key="h.month" :class="cn('border-b border-slate-100 text-[12.5px]', i % 2 === 0 ? 'bg-white' : 'bg-slate-50/60')">
              <td class="p-3 font-bold text-slate-700">{{ $t('project_detail.lbl_month_year', { month: Number(h.month.slice(5, 7)), year: h.month.slice(0, 4) }) }}</td>
              <td class="p-3 text-center font-semibold text-slate-500">{{ h.checked }}</td>
              <td class="p-3 text-center font-bold text-emerald-700">{{ h.passed }}</td>
              <td class="p-3 text-center font-bold text-rose-700">{{ h.failed }}</td>
              <td :class="cn('p-3 text-center font-black', TONE_TEXT[h.tone])">{{ h.score.toFixed(1) }}%</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <BaseDialog :open="!!noteItem" :title="$t('project_detail.pcv_section.lbl_note_title')" :description="noteItem ? $t('project_detail.pcv_section.lbl_note_desc', { label: noteItem.label }) : undefined" max-width="sm" @update:open="(v: boolean) => { if (!v) noteItem = null; }">
      <FormItem :label="$t('project_detail.pcv_section.lbl_note_label')" required :error="noteError">
        <BaseTextarea v-model="noteText" :rows="4" :placeholder="$t('project_detail.pcv_section.ph_note')" :error="!!noteError" />
      </FormItem>
      <template #footer>
        <Button variant="outline" @click="noteItem = null">{{ $t('common.btn_cancel') }}</Button>
        <Button variant="danger" @click="submitNote">{{ $t('project_detail.pcv_section.btn_save_failed_result') }}</Button>
      </template>
    </BaseDialog>

    <BaseDialog v-model:open="itemOpen" :title="editingItem ? $t('project_detail.pcv_section.btn_edit_criterion') : $t('project_detail.pcv_section.btn_add_criterion')" :description="$t('project_detail.pcv_section.lbl_item_dialog_desc')" max-width="md">
      <div class="flex flex-col gap-4">
        <FormItem :label="$t('project_detail.pcv_section.lbl_category')" required :error="itemErrors.category">
          <BaseInput v-model="itemForm.category" :placeholder="$t('project_detail.pcv_section.ph_category')" :error="!!itemErrors.category" />
        </FormItem>
        <FormItem :label="$t('project_detail.pcv_section.lbl_label')" required :error="itemErrors.label">
          <BaseTextarea v-model="itemForm.label" :rows="3" :placeholder="$t('project_detail.pcv_section.ph_label')" :error="!!itemErrors.label" />
        </FormItem>
      </div>
      <template #footer>
        <Button variant="outline" @click="itemOpen = false">{{ $t('common.btn_cancel') }}</Button>
        <Button variant="primary" :loading="itemSaving" @click="submitItem">{{ editingItem ? $t('common.btn_save') : $t('common.btn_create') }}</Button>
      </template>
    </BaseDialog>
  </div>
</template>
