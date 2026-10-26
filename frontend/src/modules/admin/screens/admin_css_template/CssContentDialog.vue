<script setup lang="ts">
// Port CssFormContentDialog (CssDialogs.tsx) — sửa phần chữ của form khảo sát: tiêu đề, lời mở đầu, hướng dẫn thang điểm, câu ý kiến khác.
import { ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import type { CssFormContent } from '@/modules/admin/api/config.api';

const props = defineProps<{ content: CssFormContent; loading: boolean; serverErrors: Record<string, string>; /** Mô tả dialog (mặc định: gợi ý của biểu mẫu mặc định) */ description?: string }>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ submit: [value: CssFormContent] }>();

const { t } = useI18n();
const form = ref<CssFormContent>({ ...props.content });
const errors = ref<Record<string, string>>({});

watch(open, (isOpen) => {
  if (!isOpen) return;
  form.value = { ...props.content };
  errors.value = {};
});
watch(() => props.serverErrors, (value) => (errors.value = { ...value }));

function submit() {
  errors.value = form.value.title.trim() ? {} : { title: t('validation.required') };
  if (!errors.value.title) emit('submit', { ...form.value, title: form.value.title.trim() });
}
</script>

<template>
  <BaseDialog v-model:open="open" :title="$t('admin_css_template.btn_edit_content')" :description="props.description ?? $t('admin_css_template.hint_dialog')" max-width="2xl">
    <form class="flex flex-col gap-3" @submit.prevent="submit">
      <FormItem :label="$t('admin_css_template.lbl_content_title')" for-id="css-title" required :error="errors.title">
        <BaseInput id="css-title" v-model="form.title" :error="Boolean(errors.title)" />
      </FormItem>
      <FormItem :label="$t('admin_css_template.lbl_content_intro')" for-id="css-intro" :error="errors.intro">
        <BaseTextarea id="css-intro" v-model="form.intro" :rows="6" />
      </FormItem>
      <FormItem :label="$t('admin_css_template.lbl_content_scale_note')" for-id="css-scale" :error="errors.scaleNote">
        <BaseTextarea id="css-scale" v-model="form.scaleNote" :rows="3" />
      </FormItem>
      <FormItem :label="$t('admin_css_template.lbl_content_comment')" for-id="css-comment-label" :error="errors.commentLabel">
        <BaseTextarea id="css-comment-label" v-model="form.commentLabel" :rows="3" />
      </FormItem>
    </form>
    <template #footer>
      <Button variant="outline" :disabled="props.loading" @click="open = false">{{ $t('admin_css_template.btn_cancel') }}</Button>
      <Button variant="primary" :loading="props.loading" @click="submit">{{ $t('admin_css_template.btn_save') }}</Button>
    </template>
  </BaseDialog>
</template>
