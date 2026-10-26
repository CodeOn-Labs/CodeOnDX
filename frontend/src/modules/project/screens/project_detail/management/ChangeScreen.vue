<script setup lang="ts">
// Port ProjectChangeScreen + ChangeRequestToolbar/List/Row/Dialog/ImpactPicker: danh sách CR (mã CR-xx không tái dùng, BE cấp),
// thẻ thống kê (đếm trên toàn bộ CR, không theo bộ lọc), lọc theo nguồn (khách hàng / nội bộ) và mức ảnh hưởng, thêm / sửa / gỡ.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { CalendarClock, Coins, ExternalLink, GitPullRequest, Pencil, Plus, ShieldCheck, Target, Trash2 } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import DetailMetricCard from '@/modules/project/components/DetailMetricCard.vue';
import { projectDetailApi, type ChangeImpactArea, type ChangeImpactLevel, type ChangeInput, type ChangeRequest, type ChangeSource, type ProjectDetail } from '@/modules/project/api/project-detail.api';
import { enumLabel } from '@/shared/i18n/enum-label';
import { firstFieldErrors } from '@/api/http';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';
import { matchesQuery } from './management-search';

const props = defineProps<{ project: ProjectDetail; query: string }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();
const q = useQuery({
  queryKey: computed(() => ['projects', 'changes', props.project.id] as const),
  queryFn: ({ signal }) => projectDetailApi.changes(props.project.id, { signal }),
});
const data = computed(() => q.data.value);
const canEdit = computed(() => !!data.value?.canEdit);
const refresh = () => void queryClient.invalidateQueries({ queryKey: ['projects', 'changes', props.project.id] });

const LEVELS: ChangeImpactLevel[] = ['high', 'medium', 'low'];
const AREAS: ChangeImpactArea[] = ['scope', 'schedule', 'cost', 'quality'];
const SOURCES: ChangeSource[] = ['customer', 'internal'];
const AREA_ICON = { scope: Target, schedule: CalendarClock, cost: Coins, quality: ShieldCheck } as const;
const LEVEL_CHIP: Record<ChangeImpactLevel, string> = { high: 'bg-rose-50 text-rose-700 ring-rose-200', medium: 'bg-amber-50 text-amber-700 ring-amber-200', low: 'bg-emerald-50 text-emerald-700 ring-emerald-200' };
const LEVEL_BUTTON: Record<ChangeImpactLevel, string> = { high: 'border-rose-400 bg-rose-50 text-rose-700 ring-2 ring-rose-200', medium: 'border-amber-400 bg-amber-50 text-amber-700 ring-2 ring-amber-200', low: 'border-emerald-400 bg-emerald-50 text-emerald-700 ring-2 ring-emerald-200' };
const SOURCE_CHIP: Record<ChangeSource, string> = { customer: 'bg-sky-50 text-sky-700', internal: 'bg-violet-50 text-violet-700' };

const sourceFilter = ref<'all' | ChangeSource>('all');
const impactFilter = ref<'all' | ChangeImpactLevel>('all');
const rows = computed(() => (data.value?.items ?? [])
  .filter((c) => sourceFilter.value === 'all' || c.source === sourceFilter.value)
  .filter((c) => impactFilter.value === 'all' || c.impactLevel === impactFilter.value)
  .filter((c) => matchesQuery(props.query, c.code, c.fileName, c.impactNote)));
const levelCount = (l: ChangeImpactLevel) => (data.value?.items ?? []).filter((c) => c.impactLevel === l).length;

const dialogOpen = ref(false);
const editing = ref<ChangeRequest | null>(null);
const form = ref<ChangeInput>({ fileName: '', url: '', source: 'customer', impactLevel: 'medium', impactAreas: [], impactNote: '' });
const errors = ref<Record<string, string>>({});
const saving = ref(false);
function open(c: ChangeRequest | null) {
  editing.value = c;
  errors.value = {};
  form.value = c
    ? { fileName: c.fileName, url: c.url, source: c.source, impactLevel: c.impactLevel, impactAreas: [...c.impactAreas], impactNote: c.impactNote ?? '' }
    : { fileName: '', url: '', source: 'customer', impactLevel: 'medium', impactAreas: [], impactNote: '' };
  dialogOpen.value = true;
}
function toggleArea(a: ChangeImpactArea) {
  const list = form.value.impactAreas;
  form.value.impactAreas = list.includes(a) ? list.filter((x) => x !== a) : [...list, a];
}
async function submit() {
  saving.value = true;
  try {
    const res = editing.value ? await projectDetailApi.change.update(props.project.id, editing.value.id, form.value) : await projectDetailApi.change.create(props.project.id, form.value);
    toast.success(res.messageText);
    dialogOpen.value = false;
    refresh();
  } catch (err) {
    errors.value = firstFieldErrors(err);
    if (Object.keys(errors.value).length === 0) toast.error((err as Error).message);
  } finally {
    saving.value = false;
  }
}
async function remove(c: ChangeRequest) {
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.change_section.confirm_delete_title'), description: t('project_detail.change_section.confirm_delete_desc', { code: c.code, name: c.fileName }), confirmText: t('project_detail.btn_confirm_remove') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.change.remove(props.project.id, c.id)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const SELECT = 'h-9 rounded-lg border border-slate-200 bg-white px-2.5 text-[12.5px] font-semibold text-slate-600 outline-none focus:border-pr';
</script>

<template>
  <div v-if="!data" class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
  <div v-else class="flex flex-col gap-4">
    <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
      <DetailMetricCard :label="$t('project_detail.change_section.lbl_total')" :value="data.summary.total" />
      <DetailMetricCard :label="$t('project_detail.change_section.lbl_customer')" :value="data.summary.customer" />
      <DetailMetricCard :label="$t('project_detail.change_section.lbl_internal')" :value="data.summary.internal" />
      <DetailMetricCard :label="$t('project_detail.change_section.lbl_high_impact')" :value="data.summary.high" :tone="data.summary.high > 0 ? 'red' : 'default'" />
    </div>

    <div class="flex flex-wrap items-center justify-between gap-3">
      <div class="flex flex-wrap items-center gap-2">
        <div class="flex rounded-xl border border-slate-200 bg-white p-0.5">
          <button v-for="s in (['all', ...SOURCES] as const)" :key="s" type="button" :class="cn('rounded-lg px-3 py-1 text-[12px] font-bold', sourceFilter === s ? 'bg-pr text-white' : 'text-slate-500 hover:bg-slate-50')" @click="sourceFilter = s">
            {{ s === 'all' ? $t('project_detail.lbl_filter_all_count', { count: data.summary.total }) : `${enumLabel('change_source', s)} (${s === 'customer' ? data.summary.customer : data.summary.internal})` }}
          </button>
        </div>
        <select v-model="impactFilter" :aria-label="$t('project_detail.change_section.lbl_impact_filter')" :class="SELECT">
          <option value="all">{{ $t('project_detail.change_section.lbl_impact_filter_all') }}</option>
          <option v-for="l in LEVELS" :key="l" :value="l">{{ $t('project_detail.change_section.lbl_impact_filter_option', { label: enumLabel('change_impact_level', l), count: levelCount(l) }) }}</option>
        </select>
      </div>
      <Button v-if="canEdit" variant="primary" size="sm" @click="open(null)"><template #leftIcon><Plus :size="14" /></template>{{ $t('project_detail.change_section.btn_add') }}</Button>
    </div>

    <div v-if="data.items.length === 0" class="flex h-48 flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 px-6 text-center">
      <GitPullRequest class="text-slate-300" :size="28" />
      <p class="text-[13px] font-bold text-slate-500">{{ $t('project_detail.change_section.empty_title') }}</p>
      <p class="max-w-sm text-[12px] text-slate-400">{{ $t('project_detail.change_section.empty_hint') }}</p>
    </div>
    <div v-else class="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
      <table class="w-full min-w-[860px] border-collapse text-left">
        <thead>
          <tr class="border-b border-slate-200 bg-slate-50/80 text-[11px] font-bold uppercase tracking-wide text-slate-500">
            <th class="w-[80px] p-3">{{ $t('project_detail.change_section.col_code') }}</th>
            <th class="p-3">{{ $t('project_detail.change_section.col_file') }}</th>
            <th class="w-[110px] p-3">{{ $t('project_detail.change_section.col_source') }}</th>
            <th class="w-[240px] p-3">{{ $t('project_detail.change_section.col_impact') }}</th>
            <th class="w-[220px] p-3">{{ $t('project_detail.change_section.col_note') }}</th>
            <th class="w-[130px] p-3">{{ $t('project_detail.change_section.col_updated') }}</th>
            <th v-if="canEdit" class="w-[76px] p-3" />
          </tr>
        </thead>
        <tbody>
          <tr v-if="rows.length === 0"><td colspan="7" class="p-6 text-center text-[12.5px] text-slate-400">{{ $t('project_detail.change_section.empty_match') }}</td></tr>
          <tr v-for="c in rows" :key="c.id" class="group/row border-b border-slate-100 align-top text-[12.5px] last:border-b-0 hover:bg-slate-50/50">
            <td class="p-3 font-black text-slate-700">{{ c.code }}</td>
            <td class="p-3"><a :href="c.url" target="_blank" rel="noopener noreferrer" class="flex items-center gap-1.5 font-bold text-slate-700 hover:text-pr hover:underline">{{ c.fileName }}<ExternalLink :size="12" class="shrink-0 text-slate-400" /></a></td>
            <td class="p-3"><span :class="cn('rounded-full px-2 py-0.5 text-[11px] font-bold', SOURCE_CHIP[c.source])">{{ enumLabel('change_source', c.source) }}</span></td>
            <td class="p-3">
              <div class="flex flex-wrap items-center gap-1.5">
                <span :class="cn('rounded-full px-2 py-0.5 text-[11px] font-bold ring-1', LEVEL_CHIP[c.impactLevel])">{{ enumLabel('change_impact_level', c.impactLevel) }}</span>
                <span v-for="a in c.impactAreas" :key="a" class="inline-flex items-center gap-1 rounded-md bg-slate-100 px-1.5 py-0.5 text-[11px] font-semibold text-slate-600"><component :is="AREA_ICON[a]" :size="11" />{{ enumLabel('change_impact_area', a) }}</span>
              </div>
            </td>
            <td class="whitespace-pre-line p-3 text-[12px] text-slate-500">{{ c.impactNote || '—' }}</td>
            <td class="p-3 text-[11.5px] text-slate-500">{{ formatDate(c.updatedAt) }}<div class="text-slate-400">{{ c.updatedByName }}</div></td>
            <td v-if="canEdit" class="p-3">
              <div class="flex items-center gap-1 opacity-0 transition-opacity group-hover/row:opacity-100">
                <button type="button" class="rounded-md p-1.5 text-slate-400 hover:bg-violet-100 hover:text-pr" :aria-label="$t('project_detail.change_section.btn_edit')" @click="open(c)"><Pencil :size="13" /></button>
                <button type="button" class="rounded-md p-1.5 text-slate-400 hover:bg-rose-100 hover:text-rose-600" :aria-label="$t('project_detail.change_section.btn_remove')" @click="remove(c)"><Trash2 :size="13" /></button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <BaseDialog v-model:open="dialogOpen" :title="editing ? $t('project_detail.change_section.btn_edit_title', { code: editing.code }) : $t('project_detail.change_section.btn_add')" :description="$t('project_detail.change_section.lbl_dialog_desc')" max-width="xl">
      <div class="flex flex-col gap-4">
        <FormItem :label="$t('project_detail.change_section.lbl_file_name')" required :error="errors.fileName"><BaseInput v-model="form.fileName" :placeholder="$t('project_detail.change_section.ph_file_name')" :error="!!errors.fileName" /></FormItem>
        <FormItem :label="$t('project_detail.change_section.lbl_url')" required :error="errors.url"><BaseInput v-model="form.url" placeholder="https://" :error="!!errors.url" /></FormItem>
        <FormItem :label="$t('project_detail.change_section.lbl_source')" required :error="errors.source">
          <div class="flex gap-2">
            <button v-for="s in SOURCES" :key="s" type="button" :class="cn('flex-1 rounded-xl border px-3 py-2 text-[12.5px] font-bold', form.source === s ? 'border-pr bg-pr/5 text-pr ring-2 ring-pr/15' : 'border-slate-200 text-slate-500')" @click="form.source = s">{{ enumLabel('change_source', s) }}</button>
          </div>
        </FormItem>
        <FormItem :label="$t('project_detail.change_section.lbl_impact_level')" required :error="errors.impactLevel">
          <div class="flex gap-2">
            <button v-for="l in LEVELS" :key="l" type="button" :class="cn('flex-1 rounded-xl border px-3 py-2 text-[12.5px] font-bold', form.impactLevel === l ? LEVEL_BUTTON[l] : 'border-slate-200 text-slate-500')" @click="form.impactLevel = l">{{ enumLabel('change_impact_level', l) }}</button>
          </div>
        </FormItem>
        <FormItem :label="$t('project_detail.change_section.lbl_impact_areas')" required :error="errors.impactAreas" :description="$t('project_detail.change_section.lbl_impact_areas_hint')">
          <div class="grid grid-cols-2 gap-2">
            <button v-for="a in AREAS" :key="a" type="button" :class="cn('flex items-center gap-2 rounded-xl border px-3 py-2 text-[12.5px] font-bold', form.impactAreas.includes(a) ? 'border-pr bg-pr/5 text-pr' : 'border-slate-200 text-slate-500')" @click="toggleArea(a)"><component :is="AREA_ICON[a]" :size="14" />{{ enumLabel('change_impact_area', a) }}</button>
          </div>
        </FormItem>
        <FormItem :label="$t('project_detail.change_section.lbl_impact_note')" :error="errors.impactNote"><BaseTextarea v-model="form.impactNote" :rows="3" :placeholder="$t('project_detail.change_section.ph_impact_note')" :error="!!errors.impactNote" /></FormItem>
      </div>
      <template #footer>
        <Button variant="outline" @click="dialogOpen = false">{{ $t('common.btn_cancel') }}</Button>
        <Button variant="primary" :loading="saving" @click="submit">{{ editing ? $t('common.btn_save') : $t('project_detail.change_section.btn_add') }}</Button>
      </template>
    </BaseDialog>
  </div>
</template>
