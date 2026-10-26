<script setup lang="ts">
// Port CssQuestionDialog (CssDialogs.tsx) — thêm / sửa 1 câu hỏi chấm điểm 1–5.
import { ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import type { CssQuestion, CssQuestionInput } from '@/modules/admin/api/config.api';

const props = defineProps<{ item: CssQuestion | null; loading: boolean; serverErrors: Record<string, string>; /** Mô tả dialog (mặc định: gợi ý của biểu mẫu mặc định) */ description?: string }>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ submit: [input: CssQuestionInput] }>();

const { t } = useI18n();
const label = ref('');
const required = ref(true);
const errors = ref<Record<string, string>>({});

watch(
  () => [open.value, props.item] as const,
  ([isOpen, item]) => {
    if (!isOpen) return;
    label.value = item?.label ?? '';
    required.value = item?.required ?? true;
    errors.value = {};
  },
  { immediate: true },
);
watch(() => props.serverErrors, (value) => (errors.value = { ...value }));

function submit() {
  errors.value = label.value.trim() ? {} : { label: t('validation.required') };
  if (!errors.value.label) emit('submit', { label: label.value.trim(), required: required.value });
}
</script>

<template>
  <BaseDialog
    v-model:open="open"
    :title="props.item ? $t('admin_css_template.btn_edit_question') : $t('admin_css_template.btn_add_question')"
    :description="props.description ?? $t('admin_css_template.hint_dialog')"
    max-width="md"
  >
    <form class="flex flex-col gap-3" @submit.prevent="submit">
      <FormItem :label="$t('admin_css_template.lbl_question')" for-id="css-question" required :error="errors.label">
        <BaseTextarea id="css-question" v-model="label" :rows="4" :placeholder="$t('admin_css_template.ph_question')" :error="Boolean(errors.label)" />
      </FormItem>
      <label class="flex items-start gap-2.5 cursor-pointer select-none">
        <input v-model="required" type="checkbox" class="mt-0.5 h-4 w-4 rounded border-slate-300 accent-violet-600 cursor-pointer" />
        <span class="text-[12.5px] font-medium text-slate-600">{{ $t('admin_css_template.hint_required_question') }}</span>
      </label>
    </form>
    <template #footer>
      <Button variant="outline" :disabled="props.loading" @click="open = false">{{ $t('admin_css_template.btn_cancel') }}</Button>
      <Button variant="primary" :loading="props.loading" @click="submit">
        {{ props.item ? $t('admin_css_template.btn_save') : $t('admin_css_template.btn_add') }}
      </Button>
    </template>
  </BaseDialog>
</template>
