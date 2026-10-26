<script setup lang="ts">
// Port PcvChecklistItemDialog.tsx — thêm / sửa 1 tiêu chí của biểu mẫu PCV.
import { ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import type { PcvItemInput, PcvTemplateItem } from '@/modules/admin/api/config.api';

const props = defineProps<{ item: PcvTemplateItem | null; loading: boolean; serverErrors: Record<string, string> }>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ submit: [input: PcvItemInput] }>();

const { t } = useI18n();
const category = ref('');
const label = ref('');
const errors = ref<Record<string, string>>({});

watch(
  () => [open.value, props.item] as const,
  ([isOpen, item]) => {
    if (!isOpen) return;
    category.value = item?.category ?? '';
    label.value = item?.label ?? '';
    errors.value = {};
  },
  { immediate: true },
);
watch(() => props.serverErrors, (value) => (errors.value = { ...value }));

function submit() {
  const e: Record<string, string> = {};
  if (!category.value.trim()) e.category = t('validation.required');
  if (!label.value.trim()) e.label = t('validation.required');
  errors.value = e;
  if (Object.keys(e).length === 0) emit('submit', { category: category.value.trim(), label: label.value.trim() });
}
</script>

<template>
  <BaseDialog
    v-model:open="open"
    :title="props.item ? $t('admin_pcv_criteria.title_edit_dialog') : $t('admin_pcv_criteria.title_add_dialog')"
    :description="$t('admin_pcv_criteria.hint_dialog')"
    max-width="sm"
  >
    <form class="flex flex-col gap-3" @submit.prevent="submit">
      <FormItem :label="$t('admin_pcv_criteria.col_category')" for-id="pcv-category" required :error="errors.category">
        <BaseInput id="pcv-category" v-model="category" :placeholder="$t('admin_pcv_criteria.ph_category')" :error="Boolean(errors.category)" />
      </FormItem>
      <FormItem :label="$t('admin_pcv_criteria.col_label')" for-id="pcv-label" required :error="errors.label">
        <BaseTextarea id="pcv-label" v-model="label" :rows="3" :placeholder="$t('admin_pcv_criteria.ph_label')" :error="Boolean(errors.label)" />
      </FormItem>
    </form>
    <template #footer>
      <Button variant="outline" :disabled="props.loading" @click="open = false">{{ $t('admin_pcv_criteria.btn_cancel') }}</Button>
      <Button variant="primary" :loading="props.loading" @click="submit">
        {{ props.item ? $t('admin_pcv_criteria.btn_save') : $t('admin_pcv_criteria.btn_add') }}
      </Button>
    </template>
  </BaseDialog>
</template>
