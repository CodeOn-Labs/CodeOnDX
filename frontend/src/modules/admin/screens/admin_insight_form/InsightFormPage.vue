<script setup lang="ts">
// Port InsightDetailPage.tsx — tạo / sửa cấu hình AI Insight: tên, màn hình áp dụng, điều kiện hiển thị, nguồn dữ liệu,
// vai trò được xem, prompt; "Xem trước" gọi BE chạy thử (D5 — mọi lời gọi LLM qua BE).
// Bỏ so với bản gốc: bảng gợi ý từ vựng dữ liệu theo nguồn (InsightDataVocabularyHint) — từ vựng do BE định nghĩa khi dựng payload.
import { computed, ref, watch } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { ArrowLeft, Check, Sparkles, X } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import InsightAlert from '@/shared/components/insight/InsightAlert.vue';
import {
  DEFAULT_SOURCES, EMPTY_ROLE_FILTERS, INSIGHT_CONDITIONS, INSIGHT_DATA_SOURCES, systemApi, type InsightConfigInput, type InsightDataSource,
} from '@/modules/admin/api/system.api';
import type { InsightItem } from '@/modules/ai/api/ai.api';
import { SCREENS, type ScreenKey } from '@/app/screens';
import { enumLabel } from '@/shared/i18n/enum-label';
import { screenTitle } from '@/shared/i18n/screen-title';
import { useFormMutation } from '@/shared/composables/useFormMutation';
import { cn } from '@/shared/utils/cn';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const id = computed(() => String(route.params.id ?? 'new'));
const isNew = computed(() => id.value === 'new' || route.path.endsWith('/new'));

const screenOptions = computed(() => (Object.keys(SCREENS) as ScreenKey[]).filter((k) => !SCREENS[k].public).map((k) => ({ label: screenTitle(k), value: k, description: k })));
const conditionOptions = computed(() => INSIGHT_CONDITIONS.map((c) => ({ label: enumLabel('insight_condition', c), value: c })));
const statusOptions = computed(() => [
  { label: t('admin_insight_form.opt_status_active'), value: 'active' },
  { label: t('admin_insight_form.opt_status_paused'), value: 'paused' },
]);
const roles = useQuery({ queryKey: ['roles', 'options'], queryFn: () => systemApi.roles(EMPTY_ROLE_FILTERS, 1, 0).then((r) => r.items) });
const roleOptions = computed(() => (roles.data.value ?? []).map((r) => ({ label: r.name, value: r.id })));

/** Nguồn gợi ý của màn: màn không khai báo thì lấy theo màn cha (cùng chuỗi kế thừa Insight). */
function defaultSources(screen: ScreenKey): InsightDataSource[] {
  let current: ScreenKey | undefined = screen;
  while (current) {
    const found: InsightDataSource[] | undefined = DEFAULT_SOURCES[current];
    if (found) return [...found];
    current = SCREENS[current].parent;
  }
  return ['projects'];
}

const empty = (): InsightConfigInput => ({ name: '', screenKey: 'project_list', triggerCondition: 'always', dataSources: defaultSources('project_list'), allowedRoleIds: [], dataPrompt: '', status: 'active' });
const form = ref<InsightConfigInput>(empty());
const errors = ref<Record<string, string>>({});
const query = useQuery({
  queryKey: computed(() => ['insight-configs', 'detail', id.value] as const),
  queryFn: ({ signal }) => systemApi.insightConfig(id.value, signal),
  enabled: computed(() => !isNew.value),
});
watch(
  () => query.data.value,
  (d) => {
    if (d) form.value = { name: d.name, screenKey: d.screenKey, triggerCondition: d.triggerCondition, dataSources: d.dataSources.length ? [...d.dataSources] : defaultSources(d.screenKey), allowedRoleIds: [...d.allowedRoleIds], dataPrompt: d.dataPrompt, status: d.status };
  },
  { immediate: true },
);
function changeScreen(value: string | string[] | null) {
  if (typeof value !== 'string' || !value) return;
  form.value.screenKey = value as ScreenKey;
  form.value.dataSources = defaultSources(value as ScreenKey);
}
function toggleSource(source: InsightDataSource) {
  const list = form.value.dataSources;
  form.value.dataSources = list.includes(source) ? list.filter((s) => s !== source) : [...list, source];
}

const save = useFormMutation({
  mutationFn: (input: InsightConfigInput) => (isNew.value ? systemApi.createInsightConfig(input) : systemApi.updateInsightConfig(id.value, input)),
  invalidate: [['insight-configs'], ['ai', 'insights']],
  onSuccess: () => void router.push('/admin/insights'),
});
watch(save.fieldErrors, (v) => (errors.value = { ...v }));
function submit() {
  const e: Record<string, string> = {};
  if (!form.value.name.trim()) e.name = t('validation.required');
  if (form.value.dataSources.length === 0) e.dataSources = t('validation.required');
  if (!form.value.dataPrompt.trim()) e.dataPrompt = t('validation.required');
  errors.value = e;
  if (Object.keys(e).length === 0) save.mutate({ ...form.value });
}

// ─── Xem trước ──────────────────────────────────────────────────────────────
const previewing = ref(false);
const previewResult = ref<InsightItem[] | null>(null);
const previewError = ref('');
async function preview() {
  if (!form.value.dataPrompt.trim()) {
    errors.value = { ...errors.value, dataPrompt: t('admin_insight_form.err_prompt_required') };
    return;
  }
  previewing.value = true;
  previewError.value = '';
  previewResult.value = null;
  try {
    const items = await systemApi.previewInsightConfig({ ...form.value });
    if (items.length === 0) previewError.value = t('admin_insight_form.err_no_result');
    else previewResult.value = items;
  } catch (err) {
    previewError.value = (err as Error).message;
  } finally {
    previewing.value = false;
  }
}
</script>

<template>
  <PageLayout layout="C" content-padding="24px">
    <div v-if="!isNew && query.isPending.value" class="flex items-center justify-center h-full py-20">
      <div class="text-slate-400 animate-pulse text-[13px] font-medium">{{ $t('admin_insight_form.lbl_loading') }}</div>
    </div>
    <form v-else novalidate class="flex-1 flex flex-col gap-8 min-h-0" @submit.prevent="submit">
      <div class="flex items-center justify-between">
        <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">
          {{ isNew ? $t('admin_insight_form.title_create') : $t('admin_insight_form.title_edit') }}
        </h1>
        <RouterLink to="/admin/insights" class="group flex items-center gap-2 text-sm font-semibold text-slate-600 hover:text-pr transition-colors">
          <ArrowLeft :size="18" class="transition-transform group-hover:-translate-x-1" />
          {{ $t('admin_insight_form.lbl_back_to_list') }}
        </RouterLink>
      </div>

      <div class="flex-1 flex flex-col gap-6">
        <div class="grid grid-cols-1 gap-6 md:grid-cols-4">
          <FormItem :label="$t('admin_insight_form.lbl_name')" required :error="errors.name">
            <BaseInput v-model="form.name" :placeholder="$t('admin_insight_form.ph_name')" :error="!!errors.name" />
          </FormItem>
          <FormSelect :model-value="form.screenKey" :label="$t('admin_insight_form.lbl_screen')" :options="screenOptions" :error="errors.screenKey" :allow-clear="false" searchable required @update:model-value="changeScreen" />
          <FormSelect v-model="form.triggerCondition" :label="$t('admin_insight_form.lbl_condition')" :options="conditionOptions" :error="errors.triggerCondition" :allow-clear="false" required />
          <FormSelect v-model="form.status" :label="$t('admin_insight_form.lbl_status')" :options="statusOptions" :allow-clear="false" />
        </div>

        <div class="grid grid-cols-1 gap-6 md:grid-cols-2">
          <FormItem :label="$t('admin_insight_form.lbl_sources')" required :error="errors.dataSources">
            <div :class="cn('flex gap-2 flex-wrap p-2.5 min-h-[42px] rounded-xl border bg-white transition-colors items-center', errors.dataSources ? 'border-red-500 bg-red-50/5' : 'border-slate-200')">
              <button
                v-for="source in INSIGHT_DATA_SOURCES"
                :key="source"
                type="button"
                :class="cn(
                  'px-2.5 py-1 rounded-lg text-[10px] font-bold transition-all duration-200 hover:scale-105 active:scale-95 border',
                  form.dataSources.includes(source) ? 'bg-pr/10 text-pr border-pr/30 shadow-sm' : 'bg-white text-slate-500 border-slate-200 hover:border-slate-300 hover:bg-slate-50',
                )"
                @click="toggleSource(source)"
              >
                {{ enumLabel('insight_data_source', source) }}<template v-if="form.dataSources.includes(source)"> ✓</template>
              </button>
            </div>
          </FormItem>
          <FormSelect v-model="form.allowedRoleIds" :label="$t('admin_insight_form.lbl_roles')" :placeholder="$t('admin_insight_form.ph_all_roles')" :options="roleOptions" multiple searchable />
        </div>

        <div class="flex flex-col gap-1">
          <FormItem :label="$t('admin_insight_form.lbl_prompt')" required :description="$t('admin_insight_form.hint_prompt')" :error="errors.dataPrompt">
            <BaseTextarea v-model="form.dataPrompt" :rows="5" :placeholder="$t('admin_insight_form.ph_prompt')" :error="!!errors.dataPrompt" />
          </FormItem>
          <p class="text-[10px] text-slate-400 italic mt-1">{{ $t('admin_insight_form.hint_tip') }}</p>
        </div>
      </div>

      <div v-if="previewResult !== null || previewError" class="border border-slate-200 rounded-[20px] p-5 bg-slate-50/30 flex flex-col gap-4 animate-in fade-in slide-in-from-top-4 duration-300 mt-4">
        <div class="flex items-center justify-between pb-3 border-b border-slate-200">
          <span class="text-[11px] font-extrabold text-slate-800 uppercase tracking-widest flex items-center gap-1.5">
            <Sparkles :size="14" class="text-blue-500 animate-pulse" />
            {{ $t('admin_insight_form.title_preview') }}
          </span>
          <span class="text-[10px] text-blue-600 bg-blue-50 border border-blue-100 px-2.5 py-0.5 rounded-full font-bold">{{ $t('admin_insight_form.lbl_preview_mode') }}</span>
        </div>
        <div v-if="previewError" class="text-[12px] text-red-500 bg-red-50/30 border border-red-100 rounded-xl p-4 font-medium">{{ previewError }}</div>
        <div v-else class="flex flex-col gap-3">
          <InsightAlert v-for="item in previewResult ?? []" :key="item.id" :variant="item.variant" :title="item.title" :content="item.content" />
        </div>
        <p class="text-[10px] text-slate-400 italic">{{ $t('admin_insight_form.hint_preview') }}</p>
      </div>

      <div class="sticky bottom-[-24px] mx-[-24px] mb-[-24px] mt-6 z-10 flex items-center justify-end gap-3 bg-white/80 px-6 py-4 shadow-[0_-8px_16px_-6px_rgba(0,0,0,0.05)] backdrop-blur-xl">
        <Button variant="secondary" class="min-w-[130px] shadow-md shadow-blue-500/10" :disabled="previewing || !form.dataPrompt.trim()" @click="preview">
          <template #leftIcon><Sparkles :size="16" /></template>
          {{ previewing ? $t('admin_insight_form.lbl_previewing') : $t('admin_insight_form.btn_preview') }}
        </Button>
        <Button variant="danger" class="min-w-[120px] shadow-sm" @click="router.push('/admin/insights')">
          <template #leftIcon><X :size="16" /></template>
          {{ $t('common.btn_cancel') }}
        </Button>
        <Button type="submit" variant="primary" class="min-w-[160px] shadow-lg shadow-pr/25 px-6" :loading="save.isPending.value">
          {{ isNew ? $t('admin_insight_form.btn_create') : $t('admin_insight_form.btn_save') }}
          <template #rightIcon><Check :size="16" /></template>
        </Button>
      </div>
    </form>
  </PageLayout>
</template>
