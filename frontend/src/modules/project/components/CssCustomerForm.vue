<script setup lang="ts">
// Port CssCustomerForm.tsx — form khảo sát CSS gửi đến khách hàng (dùng cho bản xem trước ở Quản trị và trang khảo sát công khai).
// Khác bản gốc: phần khung (nhãn cố định, nút, lỗi) theo ngôn ngữ đang chọn thay vì luôn tiếng Nhật (D12 — tiếng Nhật là tuỳ chọn).
import { computed, ref } from 'vue';
import { cn } from '@/shared/utils/cn';

interface CssFormSubmitValue {
  respondent: string;
  comment: string;
  answers: Array<{ questionId: string; score: number }>;
}

const SCALE = [1, 2, 3, 4, 5] as const;

const props = withDefaults(
  defineProps<{
    content: { title: string; intro: string; scaleNote: string; commentLabel: string };
    questions: Array<{ id: string; label: string; required: boolean }>;
    /** Tháng đánh giá yyyy-MM-01 — cố định theo link, khách hàng không chọn lại. */
    periodMonth: string;
    projectName?: string | null;
    /** Bản xem trước nội bộ: không gửi được. */
    preview?: boolean;
    submitting?: boolean;
  }>(),
  { projectName: null, preview: false, submitting: false },
);
const emit = defineEmits<{ submit: [value: CssFormSubmitValue] }>();

const respondent = ref('');
const answers = ref<Record<string, number>>({});
const comment = ref('');
const showErrors = ref(false);

const period = computed(() => {
  const [year, month] = props.periodMonth.split('-').map(Number);
  return { year: year ?? 0, month: month ?? 0 };
});
const missingRespondent = computed(() => !respondent.value.trim());
const missingIds = computed(() => new Set(props.questions.filter((q) => q.required && !answers.value[q.id]).map((q) => q.id)));
const isValid = computed(() => !missingRespondent.value && missingIds.value.size === 0);

function submit() {
  if (props.preview) return;
  if (!isValid.value) {
    showErrors.value = true;
    return;
  }
  emit('submit', {
    respondent: respondent.value.trim(),
    comment: comment.value.trim(),
    answers: props.questions.filter((q) => answers.value[q.id]).map((q) => ({ questionId: q.id, score: answers.value[q.id] as number })),
  });
}

const card = 'rounded-xl border border-slate-200 bg-white p-6 shadow-sm';
</script>

<template>
  <div class="flex flex-col gap-3">
    <div :class="cn(card, 'border-t-[10px] border-t-violet-600')">
      <h1 class="text-[24px] font-semibold text-slate-900">{{ props.content.title }}</h1>
      <p v-if="props.projectName" class="mt-1 text-[12.5px] font-semibold text-violet-600">{{ props.projectName }}</p>
      <p v-if="props.content.intro" class="mt-3 whitespace-pre-line text-[13.5px] leading-relaxed text-slate-700">{{ props.content.intro }}</p>
      <p class="mt-4 border-t border-slate-100 pt-3 text-[12px] text-rose-500">{{ $t('component.css_customer_form.hint_required') }}</p>
    </div>

    <div :class="cn(card, showErrors && missingRespondent && 'border-rose-300')">
      <label for="css-respondent" class="block text-[14px] font-medium text-slate-800">
        {{ $t('component.css_customer_form.lbl_respondent') }}<span class="ml-1 text-rose-500" aria-hidden="true">*</span>
      </label>
      <input
        id="css-respondent"
        v-model="respondent"
        maxlength="255"
        :placeholder="$t('component.css_customer_form.ph_answer')"
        class="mt-4 w-full max-w-md border-b border-slate-300 bg-transparent py-1.5 text-[13.5px] outline-none focus:border-violet-600"
      />
      <p v-if="showErrors && missingRespondent" class="mt-2 text-[12px] text-rose-500">{{ $t('component.css_customer_form.err_required') }}</p>
    </div>

    <div :class="card">
      <p class="block text-[14px] font-medium text-slate-800">{{ $t('component.css_customer_form.lbl_month') }}</p>
      <div class="mt-4 inline-flex h-10 min-w-[120px] items-center rounded-md border border-slate-300 px-3 text-[13.5px] text-slate-700">
        {{ $t('component.css_customer_form.lbl_month_value', period) }}
      </div>
    </div>

    <div v-if="props.content.scaleNote" :class="cn(card, 'bg-violet-50/60')">
      <p class="whitespace-pre-line text-[13.5px] font-medium leading-relaxed text-slate-800">{{ props.content.scaleNote }}</p>
    </div>

    <div v-for="(q, idx) in props.questions" :key="q.id" :class="cn(card, showErrors && missingIds.has(q.id) && 'border-rose-300')">
      <fieldset>
        <legend class="text-[14px] font-medium leading-relaxed text-slate-800">
          {{ idx + 1 }}. {{ q.label }}<span v-if="q.required" class="ml-1 text-rose-500" aria-hidden="true">*</span>
        </legend>
        <div class="mt-4 flex items-end justify-center gap-6 sm:gap-10">
          <label v-for="v in SCALE" :key="v" class="flex cursor-pointer flex-col items-center gap-2 text-[13px] text-slate-600">
            {{ v }}
            <input
              type="radio"
              :name="`css-q-${q.id}`"
              :value="v"
              :checked="answers[q.id] === v"
              class="h-5 w-5 cursor-pointer accent-violet-600"
              @change="answers = { ...answers, [q.id]: v }"
            />
          </label>
        </div>
      </fieldset>
      <p v-if="showErrors && missingIds.has(q.id)" class="mt-3 text-[12px] text-rose-500">{{ $t('component.css_customer_form.err_required') }}</p>
    </div>

    <div :class="card">
      <label for="css-comment" class="block whitespace-pre-line text-[14px] font-medium leading-relaxed text-slate-800">{{ props.content.commentLabel }}</label>
      <textarea
        id="css-comment"
        v-model="comment"
        maxlength="5000"
        rows="3"
        :placeholder="$t('component.css_customer_form.ph_answer')"
        class="mt-4 w-full resize-y border-b border-slate-300 bg-transparent py-1.5 text-[13.5px] outline-none focus:border-violet-600"
      />
    </div>

    <div class="flex items-center justify-between pt-1">
      <button
        type="button"
        :disabled="props.preview || props.submitting"
        class="h-10 rounded-md bg-violet-600 px-6 text-[13.5px] font-semibold text-white shadow-sm transition-colors hover:bg-violet-700 disabled:cursor-not-allowed disabled:bg-slate-300"
        @click="submit"
      >
        {{ $t('component.css_customer_form.btn_submit') }}
      </button>
      <span v-if="showErrors && !isValid" class="text-[12px] text-rose-500">{{ $t('component.css_customer_form.err_fix_required') }}</span>
    </div>
  </div>
</template>
