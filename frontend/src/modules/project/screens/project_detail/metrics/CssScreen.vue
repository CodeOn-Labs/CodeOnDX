<script setup lang="ts">
// Port ProjectCssScreen + CssScoreSummary + ProjectCssHistoryTable: khảo sát mức độ hài lòng khách hàng theo tháng —
// tạo / tạo lại link khảo sát (khách mở link không cần đăng nhập), điểm CSS tháng (BE tính: tổng điểm / số câu đã chấm, thang 1–5),
// form + câu hỏi riêng của dự án (khởi tạo từ Quản trị → Biểu mẫu CSS), xem trước form, lịch sử.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { CheckCircle2, Clock, Copy, Eye, FileText, Link2, Lock, Pencil, Plus, RefreshCw, Smile, Trash2 } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import MonthNavigator from '@/shared/components/form/MonthNavigator.vue';
import DetailMetricCard from '@/modules/project/components/DetailMetricCard.vue';
import CssContentDialog from '@/modules/admin/screens/admin_css_template/CssContentDialog.vue';
import CssQuestionDialog from '@/modules/admin/screens/admin_css_template/CssQuestionDialog.vue';
import CssPreviewDialog from '@/modules/admin/screens/admin_css_template/CssPreviewDialog.vue';
import { projectDetailApi, type CssForm, type CssQuestion, type ProjectDetail } from '@/modules/project/api/project-detail.api';
import { firstFieldErrors } from '@/api/http';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ project: ProjectDetail }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const month = ref(new Date().toISOString().slice(0, 7));
const query = useQuery({
  queryKey: computed(() => ['projects', 'css', props.project.id, month.value] as const),
  queryFn: ({ signal }) => projectDetailApi.css(props.project.id, month.value, { signal }),
});
const data = computed(() => query.data.value);
const refresh = () => void queryClient.invalidateQueries({ queryKey: ['projects', 'css', props.project.id] });
const monthLabel = (m: string) => t('project_detail.lbl_month_year', { month: Number(m.slice(5, 7)), year: m.slice(0, 4) });
const canEdit = computed(() => !!data.value?.canEdit);
const surveyUrl = computed(() => (data.value?.survey ? `${window.location.origin}/khao-sat-css/${data.value.survey.token}` : ''));
const statusLabel = computed(() => {
  const s = data.value?.survey?.status;
  return s === 'submitted' ? t('project_detail.css_section.lbl_status_submitted') : s === 'sent' ? t('project_detail.css_section.lbl_status_sent') : t('project_detail.css_section.lbl_status_not_sent');
});
const dateTime = (iso: string | null) => (iso ? `${formatDate(iso)} ${new Date(iso).toTimeString().slice(0, 5)}` : '—');
const scoreTone = computed(() => {
  const tone = data.value?.summary?.tone;
  return tone === 'green' || tone === 'red' ? tone : 'default';
});

async function copy(text: string) {
  try {
    await navigator.clipboard.writeText(text);
    toast.success(t('project_detail.css_section.lbl_link_copied'));
  } catch {
    toast.error(t('project_detail.css_section.lbl_link_copy_failed'));
  }
}
const creating = ref(false);
async function createLink() {
  const d = data.value;
  if (!d) return;
  if (d.survey) {
    const ok = await confirm.ask({
      type: 'warning',
      title: t('project_detail.css_section.btn_recreate_title'),
      description: d.survey.status === 'submitted' ? t('project_detail.css_section.btn_recreate_submitted_desc', { month: monthLabel(month.value) }) : t('project_detail.css_section.btn_recreate_sent_desc', { month: monthLabel(month.value) }),
      confirmText: t('project_detail.css_section.confirm_recreate'),
    });
    if (!ok) return;
  }
  creating.value = true;
  try {
    const res = await projectDetailApi.createCssLink(props.project.id, month.value);
    toast.success(res.messageText);
    await copy(`${window.location.origin}/khao-sat-css/${res.data.token}`);
    refresh();
  } catch (err) {
    toast.error(Object.values(firstFieldErrors(err))[0] ?? (err as Error).message);
  } finally {
    creating.value = false;
  }
}

// Form & câu hỏi riêng của dự án
const previewOpen = ref(false);
const contentOpen = ref(false);
const contentErrors = ref<Record<string, string>>({});
const contentSaving = ref(false);
async function saveContent(form: CssForm) {
  contentSaving.value = true;
  try {
    toast.success((await projectDetailApi.updateCssForm(props.project.id, form)).messageText);
    contentOpen.value = false;
    refresh();
  } catch (err) {
    contentErrors.value = firstFieldErrors(err);
    if (Object.keys(contentErrors.value).length === 0) toast.error((err as Error).message);
  } finally {
    contentSaving.value = false;
  }
}
const questionOpen = ref(false);
const editingQuestion = ref<CssQuestion | null>(null);
const questionErrors = ref<Record<string, string>>({});
const questionSaving = ref(false);
function openQuestion(q: CssQuestion | null) {
  editingQuestion.value = q;
  questionErrors.value = {};
  questionOpen.value = true;
}
async function saveQuestion(input: { label: string; required: boolean }) {
  questionSaving.value = true;
  try {
    const res = editingQuestion.value ? await projectDetailApi.cssQuestion.update(props.project.id, editingQuestion.value.id, input) : await projectDetailApi.cssQuestion.create(props.project.id, input);
    toast.success(res.messageText);
    questionOpen.value = false;
    refresh();
  } catch (err) {
    questionErrors.value = firstFieldErrors(err);
    if (Object.keys(questionErrors.value).length === 0) toast.error((err as Error).message);
  } finally {
    questionSaving.value = false;
  }
}
async function removeQuestion(q: CssQuestion) {
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.css_section.btn_delete_question_title'), description: t('project_detail.css_section.btn_delete_question_desc', { label: q.label }), confirmText: t('project_detail.btn_confirm_delete') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.cssQuestion.remove(props.project.id, q.id)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const TONE_TEXT: Record<string, string> = { green: 'text-emerald-600', orange: 'text-amber-600', red: 'text-rose-600', none: 'text-slate-400' };
</script>

<template>
  <div class="flex flex-col gap-5">
    <div class="rounded-2xl border border-violet-100 bg-violet-50/60 p-4">
      <div class="mb-1.5 flex items-center gap-2 text-[13px] font-black text-violet-700"><Smile :size="15" />{{ $t('project_detail.css_section.hint_formula_title') }}</div>
      <p class="mb-1.5 text-[12.5px] font-bold text-slate-700">{{ $t('project_detail.css_section.hint_formula') }}</p>
      <ul class="list-disc space-y-0.5 pl-4 text-[11.5px] text-slate-500">
        <li>{{ $t('project_detail.css_section.hint_formula_form') }}</li>
        <li>{{ $t('project_detail.css_section.hint_formula_link') }}</li>
        <li>{{ $t('project_detail.css_section.hint_formula_skip') }}</li>
        <li>{{ $t('project_detail.css_section.hint_formula_tone', { green: data?.thresholds.greenMin ?? 4.5, orange: data?.thresholds.orangeMin ?? 3.5 }) }}</li>
      </ul>
      <p class="mt-2 flex items-center gap-1.5 text-[11px] font-semibold text-slate-400"><Lock :size="12" />{{ $t('project_detail.css_section.hint_formula_lock') }}</p>
    </div>

    <div class="flex items-center justify-end gap-2">
      <span class="text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.css_section.lbl_month') }}</span>
      <MonthNavigator v-model="month" />
    </div>

    <div v-if="!data" class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
    <template v-else>
      <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <DetailMetricCard :label="$t('project_detail.css_section.stat_score')" :value="data.summary ? data.summary.score.toFixed(2) : '—'" :tone="scoreTone" />
        <DetailMetricCard :label="$t('project_detail.css_section.stat_status')" :value="statusLabel" />
        <DetailMetricCard :label="$t('project_detail.css_section.stat_questions')" :value="$t('project_detail.css_section.lbl_question_count', { total: data.questions.length, required: data.questions.filter((q) => q.required).length })" />
        <DetailMetricCard :label="$t('project_detail.css_section.stat_month')" :value="monthLabel(month)" />
      </div>

      <div class="flex flex-col gap-2 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <div class="flex items-center gap-2 text-[13px] font-black text-slate-700"><Link2 :size="14" />{{ $t('project_detail.css_section.lbl_link_title', { month: monthLabel(month) }) }}</div>
        <div v-if="!data.survey" class="flex flex-wrap items-center justify-between gap-3">
          <span class="text-[12px] font-semibold text-slate-400">
            {{ data.isFuture ? $t('project_detail.css_section.lbl_future_month') : data.questions.length === 0 ? $t('project_detail.css_section.empty_questions') : $t('project_detail.css_section.lbl_not_sent') }}
          </span>
          <Button v-if="canEdit && !data.isFuture && data.questions.length > 0" variant="primary" size="sm" :loading="creating" @click="createLink"><template #leftIcon><Link2 :size="14" /></template>{{ $t('project_detail.css_section.btn_create_link') }}</Button>
        </div>
        <template v-else>
          <div class="flex flex-wrap items-center gap-2">
            <input :value="surveyUrl" readonly :aria-label="$t('project_detail.css_section.lbl_link_title', { month: monthLabel(month) })" class="h-9 min-w-[280px] flex-1 rounded-lg border border-slate-200 bg-slate-50 px-3 text-[12px] font-mono text-slate-600 outline-none" />
            <Button variant="outline" size="sm" @click="copy(surveyUrl)"><template #leftIcon><Copy :size="14" /></template>{{ $t('project_detail.css_section.btn_copy') }}</Button>
            <Button v-if="canEdit && !data.isFuture" variant="outline" size="sm" :loading="creating" @click="createLink"><template #leftIcon><RefreshCw :size="14" /></template>{{ $t('project_detail.css_section.btn_recreate_link') }}</Button>
          </div>
          <span :class="cn('flex items-center gap-1.5 text-[11.5px] font-semibold', data.survey.status === 'submitted' ? 'text-emerald-600' : 'text-amber-600')">
            <CheckCircle2 v-if="data.survey.status === 'submitted'" :size="13" /><Clock v-else :size="13" />
            {{ data.survey.status === 'submitted' ? $t('project_detail.css_section.lbl_submitted_by', { time: dateTime(data.survey.submittedAt), respondent: data.survey.respondent ?? '—' }) : $t('project_detail.css_section.lbl_sent_by', { time: dateTime(data.survey.sentAt), name: data.survey.senderName ?? '—' }) }}
          </span>
        </template>
      </div>

      <div v-if="data.survey?.status === 'submitted'" class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div class="border-b border-slate-100 bg-slate-50/50 px-4 py-2.5 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_detail.css_section.lbl_result_title', { month: monthLabel(month) }) }}</div>
        <div v-for="(a, i) in data.survey.answers" :key="i" class="flex items-center justify-between gap-3 border-b border-slate-100 px-4 py-2 text-[12.5px] last:border-b-0">
          <span class="text-slate-700">{{ a.question }}<span v-if="!a.required" class="ml-1 text-[11px] text-slate-400">{{ $t('project_detail.css_section.lbl_optional') }}</span></span>
          <span :class="cn('font-black tabular-nums', a.score === null ? 'text-slate-300' : 'text-slate-800')">{{ a.score ?? '—' }}</span>
        </div>
        <p v-if="data.survey.comment" class="whitespace-pre-line border-t border-slate-100 bg-slate-50/50 px-4 py-2.5 text-[12px] text-slate-600">{{ data.survey.comment }}</p>
      </div>

      <div class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div class="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 bg-slate-50/50 px-4 py-2.5">
          <span class="text-[11.5px] font-semibold text-slate-400">{{ $t('project_detail.css_section.lbl_form_hint') }}</span>
          <div class="flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" @click="previewOpen = true"><template #leftIcon><Eye :size="14" /></template>{{ $t('project_detail.css_section.btn_preview_form') }}</Button>
            <template v-if="canEdit">
              <Button variant="outline" size="sm" @click="contentErrors = {}; contentOpen = true"><template #leftIcon><Pencil :size="14" /></template>{{ $t('project_detail.css_section.btn_edit_content') }}</Button>
              <Button variant="outline" size="sm" @click="openQuestion(null)"><template #leftIcon><Plus :size="14" /></template>{{ $t('project_detail.css_section.btn_add_question') }}</Button>
            </template>
          </div>
        </div>
        <div class="flex items-start gap-2.5 border-b border-slate-100 px-4 py-3">
          <FileText :size="15" class="mt-0.5 shrink-0 text-violet-500" />
          <div class="min-w-0">
            <div class="text-[13.5px] font-bold text-slate-700">{{ data.form.title || '—' }}</div>
            <p class="line-clamp-2 whitespace-pre-line text-[12px] text-slate-500">{{ data.form.intro }}</p>
          </div>
        </div>
        <table class="w-full border-collapse text-left">
          <tbody>
            <tr v-for="(q, i) in data.questions" :key="q.id" class="border-b border-slate-100 last:border-b-0">
              <td class="w-10 p-3 text-center text-[12px] font-bold text-slate-400">{{ i + 1 }}</td>
              <td class="p-3 text-[13px] text-slate-700">{{ q.label }}</td>
              <td class="w-[120px] p-3"><span :class="cn('rounded-full px-2 py-0.5 text-[11px] font-bold', q.required ? 'bg-rose-50 text-rose-600' : 'bg-slate-100 text-slate-500')">{{ q.required ? $t('project_detail.css_section.lbl_required') : $t('project_detail.css_section.lbl_optional') }}</span></td>
              <td v-if="canEdit" class="w-[80px] p-3">
                <div class="flex items-center justify-center gap-1">
                  <button type="button" class="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-600" :aria-label="$t('common.btn_edit')" @click="openQuestion(q)"><Pencil :size="14" /></button>
                  <button type="button" class="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-500" :aria-label="$t('common.btn_delete')" @click="removeQuestion(q)"><Trash2 :size="14" /></button>
                </div>
              </td>
            </tr>
            <tr v-if="data.questions.length === 0"><td colspan="4" class="p-6 text-center text-[12.5px] text-slate-400">{{ $t('project_detail.css_section.empty_questions') }}</td></tr>
          </tbody>
        </table>
      </div>

      <div v-if="data.history.length === 0" class="flex h-40 flex-col items-center justify-center gap-2 rounded-[16px] border border-dashed border-slate-200 bg-slate-50/50 px-6 text-center">
        <Smile class="text-slate-300" :size="26" />
        <p class="max-w-sm text-[12.5px] font-bold text-slate-400">{{ $t('project_detail.css_section.empty_history') }}</p>
      </div>
      <div v-else class="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table class="w-full min-w-[640px] border-collapse text-left">
          <thead>
            <tr class="border-b border-slate-200 bg-slate-50/80 text-[11px] font-bold uppercase tracking-wide text-slate-500">
              <th class="p-3">{{ $t('project_detail.col_month') }}</th>
              <th class="p-3">{{ $t('project_detail.css_section.stat_status') }}</th>
              <th class="p-3">{{ $t('project_detail.css_section.col_respondent') }}</th>
              <th class="p-3 text-center">{{ $t('project_detail.css_section.col_answered') }}</th>
              <th class="p-3 text-center">CSS</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(h, i) in data.history" :key="h.month" :class="cn('border-b border-slate-100 text-[12.5px]', i % 2 === 0 ? 'bg-white' : 'bg-slate-50/60')">
              <td class="p-3 font-bold text-slate-700">{{ monthLabel(h.month) }}</td>
              <td class="p-3 text-slate-500">{{ h.status === 'submitted' ? $t('project_detail.css_section.lbl_status_submitted') : $t('project_detail.css_section.lbl_status_sent') }}</td>
              <td class="p-3 text-slate-600">{{ h.respondent ?? '—' }}</td>
              <td class="p-3 text-center font-semibold text-slate-500">{{ h.status === 'submitted' ? h.answered : '—' }}</td>
              <td :class="cn('p-3 text-center font-black', TONE_TEXT[h.tone])">{{ h.score === null ? '—' : h.score.toFixed(2) }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <CssPreviewDialog v-model:open="previewOpen" :content="data.form" :questions="data.questions" />
      <CssContentDialog v-model:open="contentOpen" :content="data.form" :loading="contentSaving" :server-errors="contentErrors" :description="$t('project_detail.css_section.lbl_project_dialog_desc')" @submit="saveContent" />
      <CssQuestionDialog v-model:open="questionOpen" :item="editingQuestion" :loading="questionSaving" :server-errors="questionErrors" :description="$t('project_detail.css_section.lbl_project_dialog_desc')" @submit="saveQuestion" />
    </template>
  </div>
</template>
