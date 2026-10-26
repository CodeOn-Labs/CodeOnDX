<script setup lang="ts">
// Port PromptTemplateDetailPage.tsx — tạo / sửa prompt template: tên, phạm vi, màn áp dụng, trạng thái, mô tả, nội dung.
// Khác bản gốc: biên dịch Handlebars + xem trước chạy ở BE (D5 — prompt chỉ dựng ở server); FE chỉ kiểm tra nhanh cặp {{ }}.
import { computed, ref, watch } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { ArrowLeft, Check, Play, X } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import { systemApi, type PromptTemplateInput } from '@/modules/admin/api/system.api';
import { SCREENS, type ScreenKey } from '@/app/screens';
import { enumLabel } from '@/shared/i18n/enum-label';
import { screenTitle } from '@/shared/i18n/screen-title';
import { useFormMutation } from '@/shared/composables/useFormMutation';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const id = computed(() => String(route.params.id ?? 'new'));
const isNew = computed(() => id.value === 'new' || route.path.endsWith('/new'));

const scopeOptions = computed(() => (['insight', 'chat', 'translate'] as const).map((s) => ({ label: enumLabel('prompt_scope', s), value: s })));
const statusOptions = computed(() => (['draft', 'active', 'archived'] as const).map((s) => ({ label: enumLabel('prompt_status', s), value: s })));
const screenOptions = computed(() => (Object.keys(SCREENS) as ScreenKey[]).filter((k) => !SCREENS[k].public).map((k) => ({ label: screenTitle(k), value: k, description: k })));

const form = ref<PromptTemplateInput>({ name: '', scope: 'insight', screenKeys: [], template: '', description: '', status: 'draft' });
const errors = ref<Record<string, string>>({});
const query = useQuery({
  queryKey: computed(() => ['prompt-templates', 'detail', id.value] as const),
  queryFn: ({ signal }) => systemApi.promptTemplate(id.value, signal),
  enabled: computed(() => !isNew.value),
});
watch(
  () => query.data.value,
  (d) => {
    if (d) form.value = { name: d.name, scope: d.scope, screenKeys: [...d.screenKeys], template: d.template, description: d.description ?? '', status: d.status };
  },
  { immediate: true },
);

/** Kiểm tra nhanh trên FE: số dấu mở/đóng biến phải bằng nhau (BE kiểm tra đầy đủ khi lưu). */
const validation = computed<'valid' | 'invalid' | null>(() => {
  const text = form.value.template;
  if (!text.trim()) return null;
  return (text.match(/\{\{/g) ?? []).length === (text.match(/\}\}/g) ?? []).length ? 'valid' : 'invalid';
});

const previewOutput = ref('');
const previewVariables = ref<string[]>([]);
const previewing = ref(false);
async function preview() {
  previewing.value = true;
  try {
    const res = await systemApi.previewPromptTemplate(form.value.template);
    previewOutput.value = res.output;
    previewVariables.value = res.variables;
  } catch (err) {
    previewOutput.value = (err as Error).message;
  } finally {
    previewing.value = false;
  }
}

const save = useFormMutation({
  mutationFn: (input: PromptTemplateInput) => (isNew.value ? systemApi.createPromptTemplate(input) : systemApi.updatePromptTemplate(id.value, input)),
  invalidate: [['prompt-templates']],
  onSuccess: () => void router.push('/admin/prompt-templates'),
});
watch(save.fieldErrors, (v) => (errors.value = { ...v }));
function submit() {
  const e: Record<string, string> = {};
  if (!form.value.name.trim()) e.name = t('validation.required');
  if (!form.value.template.trim()) e.template = t('validation.required');
  errors.value = e;
  if (Object.keys(e).length === 0 && validation.value !== 'invalid') save.mutate({ ...form.value });
}
</script>

<template>
  <PageLayout layout="C" content-padding="24px">
    <div v-if="!isNew && query.isPending.value" class="flex items-center justify-center h-full py-20">
      <div class="text-slate-400 animate-pulse text-[13px]">{{ $t('admin_prompt_template_form.lbl_loading') }}</div>
    </div>
    <form v-else novalidate class="flex-1 flex flex-col gap-8" @submit.prevent="submit">
      <div class="flex items-center justify-between">
        <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">
          {{ isNew ? $t('admin_prompt_template_form.title_create') : $t('admin_prompt_template_form.title_edit') }}
        </h1>
        <RouterLink to="/admin/prompt-templates" class="group flex items-center gap-2 text-sm font-semibold text-slate-600 hover:text-pr transition-colors">
          <ArrowLeft :size="18" class="transition-transform group-hover:-translate-x-1" />
          {{ $t('admin_prompt_template_form.lbl_back_to_list') }}
        </RouterLink>
      </div>

      <div class="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-4">
        <FormItem :label="$t('admin_prompt_template_form.lbl_name')" required :error="errors.name">
          <BaseInput v-model="form.name" :placeholder="$t('admin_prompt_template_form.ph_name')" :error="!!errors.name" />
        </FormItem>
        <FormSelect v-model="form.scope" :label="$t('admin_prompt_template_form.lbl_scope')" :options="scopeOptions" :allow-clear="false" required />
        <FormSelect v-model="form.screenKeys" :label="$t('admin_prompt_template_form.lbl_screens')" :placeholder="$t('admin_prompt_template_form.ph_all_screens')" :options="screenOptions" multiple searchable />
        <FormSelect v-model="form.status" :label="$t('admin_prompt_template_form.lbl_status')" :options="statusOptions" :allow-clear="false" required />
      </div>
      <FormItem :label="$t('admin_prompt_template_form.lbl_description')">
        <BaseInput v-model="form.description" :placeholder="$t('admin_prompt_template_form.ph_description')" />
      </FormItem>

      <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div>
          <FormItem :label="$t('admin_prompt_template_form.lbl_template')" required :error="errors.template">
            <BaseTextarea v-model="form.template" :rows="20" class="font-mono text-[12px]" :placeholder="$t('admin_prompt_template_form.ph_template')" :error="!!errors.template" />
          </FormItem>
          <p v-if="validation === 'invalid'" class="text-[11px] text-red-500 mt-1 font-medium">{{ $t('admin_prompt_template_form.err_unbalanced') }}</p>
          <p v-else-if="validation === 'valid'" class="text-[11px] text-green-600 mt-1 font-medium">{{ $t('admin_prompt_template_form.msg_valid') }}</p>
        </div>
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <span class="text-[12px] font-semibold text-slate-700">{{ $t('admin_prompt_template_form.lbl_preview') }}</span>
            <Button variant="secondary" size="sm" class="h-7 text-[10px]" :disabled="previewing || !form.template.trim()" @click="preview">
              <template #leftIcon><Play :size="12" /></template>
              {{ previewing ? $t('admin_prompt_template_form.lbl_rendering') : $t('admin_prompt_template_form.btn_render') }}
            </Button>
          </div>
          <pre class="text-[11px] font-mono bg-slate-900 text-green-400 rounded-xl p-4 h-[500px] overflow-auto whitespace-pre-wrap break-words">{{ previewOutput || $t('admin_prompt_template_form.hint_preview') }}</pre>
          <details class="mt-3 text-[11px] text-slate-500">
            <summary class="cursor-pointer font-medium text-slate-600">{{ $t('admin_prompt_template_form.lbl_variables') }}</summary>
            <div class="mt-2 p-3 bg-slate-50 rounded-xl space-y-1 font-mono text-[10px]">
              <p v-if="previewVariables.length">{{ previewVariables.join(', ') }}</p>
              <p v-else>{{ $t('admin_prompt_template_form.hint_variables') }}</p>
            </div>
          </details>
        </div>
      </div>

      <div class="sticky bottom-[-24px] mx-[-24px] mb-[-24px] mt-6 z-10 flex items-center justify-end gap-3 bg-white/80 px-6 py-4 shadow-[0_-8px_16px_-6px_rgba(0,0,0,0.05)] backdrop-blur-xl">
        <Button variant="danger" @click="router.push('/admin/prompt-templates')">
          <template #leftIcon><X :size="16" /></template>
          {{ $t('common.btn_cancel') }}
        </Button>
        <Button type="submit" variant="primary" :disabled="validation === 'invalid'" :loading="save.isPending.value">
          {{ isNew ? $t('admin_prompt_template_form.btn_create') : $t('admin_prompt_template_form.btn_save') }}
          <template #rightIcon><Check :size="16" /></template>
        </Button>
      </div>
    </form>
  </PageLayout>
</template>
