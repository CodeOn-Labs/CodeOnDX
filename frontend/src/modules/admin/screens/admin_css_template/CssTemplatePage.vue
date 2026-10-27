<script setup lang="ts">
// Port CssTemplatePage.tsx — Quản trị → Biểu mẫu CSS: nội dung form khảo sát + câu hỏi chấm điểm 1–5 (D12).
// Thay đổi lưu ngay và chỉ áp dụng cho dự án tạo sau đó — form CSS của dự án đã có giữ nguyên.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query';
import { Eye, FileText, Info, Pencil, Plus, Smile, Trash2 } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import CssQuestionDialog from './CssQuestionDialog.vue';
import CssContentDialog from './CssContentDialog.vue';
import CssPreviewDialog from './CssPreviewDialog.vue';
import {
  configApi, firstFieldErrors, type CssFormContent, type CssQuestion, type CssQuestionInput,
} from '@/modules/admin/api/config.api';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const contentQuery = useQuery({ queryKey: ['css-template', 'content'], queryFn: configApi.cssContent });
const questionsQuery = useQuery({ queryKey: ['css-template', 'questions'], queryFn: configApi.cssQuestions });
const questions = computed(() => questionsQuery.data.value ?? []);
const loadError = computed(() => contentQuery.isError.value || questionsQuery.isError.value);

const previewOpen = ref(false);
const contentOpen = ref(false);
const questionOpen = ref(false);
const editing = ref<CssQuestion | null>(null);
const fieldErrors = ref<Record<string, string>>({});

const contentMutation = useMutation({
  mutationFn: configApi.updateCssContent,
  onSuccess: (res) => {
    toast.success(res.messageText);
    contentOpen.value = false;
    queryClient.setQueryData(['css-template', 'content'], res.data);
  },
  onError: (err: Error) => {
    fieldErrors.value = firstFieldErrors(err);
    if (Object.keys(fieldErrors.value).length === 0) toast.error(err.message);
  },
});

const questionMutation = useMutation({
  mutationFn: (input: CssQuestionInput) => (editing.value ? configApi.updateCssQuestion(editing.value.id, input) : configApi.createCssQuestion(input)),
  onSuccess: (res) => {
    toast.success(res.messageText);
    questionOpen.value = false;
    void queryClient.invalidateQueries({ queryKey: ['css-template', 'questions'] });
  },
  onError: (err: Error) => {
    fieldErrors.value = firstFieldErrors(err);
    if (Object.keys(fieldErrors.value).length === 0) toast.error(err.message);
  },
});

function openContent() {
  fieldErrors.value = {};
  contentOpen.value = true;
}
function openQuestion(item: CssQuestion | null) {
  editing.value = item;
  fieldErrors.value = {};
  questionOpen.value = true;
}
function saveContent(value: CssFormContent) {
  contentMutation.mutate(value);
}

async function remove(item: CssQuestion) {
  const ok = await confirm.ask({
    title: t('admin_css_template.confirm_delete_title'),
    description: t('admin_css_template.confirm_delete_desc', { label: item.label }),
    confirmText: t('admin_css_template.btn_confirm_delete'),
    type: 'danger',
  });
  if (!ok) return;
  try {
    const res = await configApi.deleteCssQuestion(item.id);
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['css-template', 'questions'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}

const th = 'p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500';
const ICON_BUTTON = 'flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 transition-colors';
</script>

<template>
  <PageLayout layout="C" content-padding="24px">
    <div class="flex flex-col gap-4 rounded-[24px] border border-slate-200/80 bg-white p-6 shadow-sm">
      <div class="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-2">
        <div class="flex items-center gap-2.5">
          <div class="rounded-xl border border-violet-100 bg-violet-50 p-2 text-violet-600">
            <Smile :size="16" class="stroke-[2.5]" />
          </div>
          <div>
            <h1 class="text-[13px] font-black uppercase tracking-tight text-slate-800">{{ $t('admin_css_template.title_page') }}</h1>
            <p class="text-[10px] font-medium text-slate-400">{{ $t('admin_css_template.lbl_subtitle') }}</p>
          </div>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <Button variant="outline" size="sm" :disabled="!contentQuery.data.value" @click="previewOpen = true">
            <template #leftIcon><Eye :size="14" /></template>
            {{ $t('admin_css_template.btn_preview') }}
          </Button>
          <Button variant="outline" size="sm" :disabled="!contentQuery.data.value" @click="openContent">
            <template #leftIcon><Pencil :size="14" /></template>
            {{ $t('admin_css_template.btn_edit_content') }}
          </Button>
          <Button variant="outline" size="sm" @click="openQuestion(null)">
            <template #leftIcon><Plus :size="14" /></template>
            {{ $t('admin_css_template.btn_add_question') }}
          </Button>
        </div>
      </div>

      <p class="flex items-start gap-1.5 rounded-xl bg-slate-50 px-3 py-2 text-[11.5px] font-medium text-slate-500">
        <Info :size="13" class="mt-0.5 shrink-0" />
        {{ $t('admin_css_template.hint_template') }}
      </p>

      <p v-if="loadError" class="py-6 text-center text-[12.5px] font-semibold text-rose-500">{{ $t('admin_css_template.err_load') }}</p>
      <template v-else>
        <div v-if="contentQuery.data.value" class="flex items-start gap-2.5 rounded-xl border border-slate-200 px-4 py-3">
          <FileText :size="15" class="mt-0.5 shrink-0 text-violet-500" />
          <div class="min-w-0">
            <div class="text-[13.5px] font-bold text-slate-700">{{ contentQuery.data.value.title }}</div>
            <p class="line-clamp-3 whitespace-pre-line text-[12px] text-slate-500">{{ contentQuery.data.value.intro }}</p>
          </div>
        </div>

        <div v-if="!questionsQuery.isLoading.value" class="overflow-hidden rounded-xl border border-slate-200">
          <p v-if="questions.length === 0" class="py-6 text-center text-[12.5px] font-semibold text-slate-400">{{ $t('admin_css_template.empty_questions') }}</p>
          <table v-else class="w-full border-collapse text-left">
            <thead>
              <tr class="border-b border-slate-200 bg-slate-50/80">
                <th :class="cn(th, 'w-[48px] text-center')">#</th>
                <th :class="th">{{ $t('admin_css_template.col_question') }}</th>
                <th :class="cn(th, 'w-[110px] text-center')">{{ $t('admin_css_template.col_scale') }}</th>
                <th :class="cn(th, 'w-[110px] text-center')">{{ $t('admin_css_template.col_required') }}</th>
                <th :class="cn(th, 'w-[90px] text-center')">{{ $t('admin_css_template.col_actions') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, idx) in questions" :key="item.id" :class="cn('border-b border-slate-100 last:border-b-0', idx % 2 === 0 ? 'bg-white' : 'bg-slate-50/60')">
                <td class="p-3 text-center align-top text-[12px] font-semibold text-slate-400">{{ idx + 1 }}</td>
                <td class="p-3 text-[13px] font-semibold text-slate-700">{{ item.label }}</td>
                <td class="p-3 text-center align-top text-[12px] font-semibold text-slate-500">{{ $t('admin_css_template.lbl_scale_range') }}</td>
                <td class="p-3 text-center align-top">
                  <span :class="cn('rounded-md px-2 py-0.5 text-[11px] font-bold', item.required ? 'bg-rose-50 text-rose-600' : 'bg-slate-100 text-slate-500')">
                    {{ item.required ? $t('admin_css_template.lbl_required') : $t('admin_css_template.lbl_optional') }}
                  </span>
                </td>
                <td class="p-3 align-top">
                  <div class="flex items-center justify-center gap-1">
                    <button
                      type="button"
                      :class="cn(ICON_BUTTON, 'hover:bg-slate-100 hover:text-slate-600')"
                      :title="$t('admin_css_template.btn_edit_question')"
                      :aria-label="$t('admin_css_template.btn_edit_question')"
                      @click="openQuestion(item)"
                    >
                      <Pencil :size="14" />
                    </button>
                    <button
                      type="button"
                      :class="cn(ICON_BUTTON, 'hover:bg-red-50 hover:text-red-500')"
                      :title="$t('admin_css_template.btn_delete_question')"
                      :aria-label="$t('admin_css_template.btn_delete_question')"
                      @click="remove(item)"
                    >
                      <Trash2 :size="14" />
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </div>

    <template v-if="contentQuery.data.value">
      <CssPreviewDialog v-model:open="previewOpen" :content="contentQuery.data.value" :questions="questions" />
      <CssContentDialog
        v-model:open="contentOpen"
        :content="contentQuery.data.value"
        :loading="contentMutation.isPending.value"
        :server-errors="fieldErrors"
        @submit="saveContent"
      />
    </template>
    <CssQuestionDialog
      v-model:open="questionOpen"
      :item="editing"
      :loading="questionMutation.isPending.value"
      :server-errors="fieldErrors"
      @submit="questionMutation.mutate"
    />
  </PageLayout>
</template>
