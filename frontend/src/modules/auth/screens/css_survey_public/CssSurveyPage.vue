<script setup lang="ts">
// Port CssSurveyPage.tsx — trang khảo sát CSS công khai (/khao-sat-css/:token): khách hàng mở link, không cần đăng nhập.
// Khác bản gốc: ngôn ngữ khung theo lựa chọn hiện tại (bản gốc cố định tiếng Nhật — D12 tiếng Nhật là tuỳ chọn).
import { computed, ref } from 'vue';
import { useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { CheckCircle2, XCircle } from '@lucide/vue';
import CssCustomerForm from '@/modules/project/components/CssCustomerForm.vue';
import { request, requestFull, ApiError } from '@/api/http';
import { useToastStore } from '@/stores/toast';

interface PublicSurvey {
  projectName: string | null;
  periodMonth: string;
  submitted: boolean;
  form: { title: string; intro: string; scaleNote: string; commentLabel: string };
  questions: Array<{ id: string; label: string; required: boolean }>;
}

const route = useRoute();
const { t } = useI18n();
const toast = useToastStore();
const token = computed(() => String(route.params.token ?? ''));
const query = useQuery({
  queryKey: computed(() => ['public-css', token.value] as const),
  queryFn: () => request<PublicSurvey>(`/public/css-surveys/${encodeURIComponent(token.value)}`, { skipAuthRefresh: true }),
  retry: false,
  meta: { silent: true },
});
const outcome = ref<'submitted' | 'invalid' | null>(null);
const submitting = ref(false);
const invalid = computed(() => outcome.value === 'invalid' || (query.error.value instanceof ApiError && query.error.value.status === 404));

async function submit(value: { respondent: string; comment: string; answers: Array<{ questionId: string; score: number }> }) {
  submitting.value = true;
  try {
    await requestFull<null>(`/public/css-surveys/${encodeURIComponent(token.value)}`, { method: 'POST', body: value, skipAuthRefresh: true });
    outcome.value = 'submitted';
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) outcome.value = 'submitted';
    else if (err instanceof ApiError && err.status === 404) outcome.value = 'invalid';
    else if (err instanceof ApiError && err.status === 400) {
      // Biểu mẫu vừa được sửa trong lúc khách đang điền: tải lại câu hỏi mới
      toast.error(t('css_survey_public.err_form_changed'));
      void query.refetch();
    } else toast.error(t('css_survey_public.err_submit_failed'));
  } finally {
    submitting.value = false;
  }
}
// Trang tự cuộn trong khung h-screen (giữ như bản gốc)
const SHELL = 'h-screen overflow-y-auto bg-violet-50/60';
</script>

<template>
  <div v-if="query.isPending.value" :class="[SHELL, 'flex items-center justify-center p-5']">
    <div class="text-[14px] font-bold text-slate-500 animate-pulse">{{ $t('css_survey_public.lbl_loading') }}</div>
  </div>
  <div v-else-if="invalid || (query.error.value && !query.data.value)" :class="[SHELL, 'flex items-center justify-center p-5']">
    <div class="flex w-full max-w-lg flex-col items-center gap-3 rounded-xl border-t-[10px] border-t-violet-600 bg-white p-8 text-center shadow-sm ring-1 ring-slate-900/5">
      <XCircle class="text-slate-300" :size="40" />
      <h1 class="text-[18px] font-black text-slate-700">{{ invalid ? $t('css_survey_public.title_invalid') : $t('css_survey_public.title_load_failed') }}</h1>
      <p v-if="invalid" class="text-[13px] text-slate-500">{{ $t('css_survey_public.lbl_invalid_desc') }}</p>
    </div>
  </div>
  <div v-else-if="query.data.value && (query.data.value.submitted || outcome === 'submitted')" :class="[SHELL, 'flex items-center justify-center p-5']">
    <div class="flex w-full max-w-lg flex-col items-center gap-3 rounded-xl border-t-[10px] border-t-violet-600 bg-white p-8 text-center shadow-sm ring-1 ring-slate-900/5">
      <CheckCircle2 class="text-emerald-500" :size="40" />
      <h1 class="text-[18px] font-black text-slate-700">{{ $t('css_survey_public.title_thanks') }}</h1>
      <p class="text-[13px] text-slate-500">{{ $t('css_survey_public.lbl_thanks_desc', { title: query.data.value.form.title }) }}</p>
    </div>
  </div>
  <div v-else-if="query.data.value" :class="[SHELL, 'px-4 py-8']">
    <div class="mx-auto w-full max-w-2xl">
      <CssCustomerForm
        :content="query.data.value.form"
        :questions="query.data.value.questions"
        :period-month="query.data.value.periodMonth"
        :project-name="query.data.value.projectName"
        :submitting="submitting"
        @submit="submit"
      />
    </div>
  </div>
</template>
