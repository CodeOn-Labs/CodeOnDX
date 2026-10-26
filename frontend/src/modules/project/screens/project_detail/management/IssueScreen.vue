<script setup lang="ts">
// Port ProjectIssueScreen + IssueLinkTypeSection + IssueLinkRow + IssueLinkDialog: link tới công cụ quản lý issue (Jira, Backlog, GitLab…)
// chia 3 mục — theo issue cha, theo sprint, theo milestone; mỗi link là Task hoặc Bug. Hệ thống chỉ lưu link, không đồng bộ dữ liệu.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { ExternalLink, FileWarning, Pencil, Plus, Trash2 } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import { projectDetailApi, type IssueInput, type IssueKind, type IssueLink, type IssueType, type ProjectDetail } from '@/modules/project/api/project-detail.api';
import { enumLabel } from '@/shared/i18n/enum-label';
import { firstFieldErrors } from '@/api/http';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';
import { matchesQuery } from './management-search';

const props = defineProps<{ project: ProjectDetail; query: string }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();
const q = useQuery({
  queryKey: computed(() => ['projects', 'issues', props.project.id] as const),
  queryFn: ({ signal }) => projectDetailApi.issues(props.project.id, { signal }),
});
const data = computed(() => q.data.value);
const canEdit = computed(() => !!data.value?.canEdit);
const refresh = () => void queryClient.invalidateQueries({ queryKey: ['projects', 'issues', props.project.id] });
const TYPES: IssueType[] = ['parent', 'sprint', 'milestone'];
const KINDS: IssueKind[] = ['task', 'bug'];
const sections = computed(() => TYPES.map((type) => ({
  type,
  title: { parent: t('project_detail.issue_section.lbl_type_parent'), sprint: t('project_detail.issue_section.lbl_type_sprint'), milestone: t('project_detail.issue_section.lbl_type_milestone') }[type],
  hint: { parent: t('project_detail.issue_section.hint_parent'), sprint: t('project_detail.issue_section.hint_sprint'), milestone: t('project_detail.issue_section.hint_milestone') }[type],
  items: (data.value?.items ?? []).filter((i) => i.type === type && matchesQuery(props.query, i.name, i.url)),
})));
const counts = computed(() => {
  const items = data.value?.items ?? [];
  return { ...data.value?.summary, task: items.filter((i) => i.kind === 'task').length, bug: items.filter((i) => i.kind === 'bug').length };
});
const placeholder = (type: IssueType) => ({ parent: t('project_detail.issue_section.lbl_name_ph_parent'), sprint: t('project_detail.issue_section.lbl_name_ph_sprint'), milestone: t('project_detail.issue_section.lbl_name_ph_milestone') }[type]);

const dialogOpen = ref(false);
const editing = ref<IssueLink | null>(null);
const form = ref<IssueInput>({ type: 'parent', kind: 'task', name: '', url: '' });
const errors = ref<Record<string, string>>({});
const saving = ref(false);
function open(type: IssueType, item: IssueLink | null) {
  editing.value = item;
  errors.value = {};
  form.value = item ? { type: item.type, kind: item.kind, name: item.name, url: item.url } : { type, kind: 'task', name: '', url: '' };
  dialogOpen.value = true;
}
async function submit() {
  saving.value = true;
  try {
    const res = editing.value ? await projectDetailApi.issue.update(props.project.id, editing.value.id, form.value) : await projectDetailApi.issue.create(props.project.id, form.value);
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
async function remove(item: IssueLink) {
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.issue_section.confirm_delete_title'), description: t('project_detail.issue_section.confirm_delete_desc', { name: item.name }), confirmText: t('project_detail.btn_confirm_remove') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.issue.remove(props.project.id, item.id)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const KIND_CHIP: Record<IssueKind, string> = { task: 'bg-blue-50 text-blue-700', bug: 'bg-rose-50 text-rose-700' };
</script>

<template>
  <div v-if="!data" class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
  <div v-else class="flex flex-col gap-4">
    <p class="text-[12px] font-semibold text-slate-400">{{ $t('project_detail.issue_section.lbl_summary', { parent: counts.parent ?? 0, sprint: counts.sprint ?? 0, milestone: counts.milestone ?? 0, task: counts.task, bug: counts.bug }) }}</p>
    <div v-if="data.items.length === 0" class="flex h-32 flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 px-6 text-center">
      <FileWarning class="text-slate-300" :size="26" />
      <p class="max-w-md text-[12px] text-slate-400">{{ $t('project_detail.issue_section.empty_hint') }}</p>
    </div>
    <section v-for="s in sections" :key="s.type" class="overflow-hidden rounded-[14px] border border-[#e8ecf5] bg-white shadow-sm">
      <header class="flex items-center gap-2 bg-slate-50/70 px-3 py-2.5">
        <div class="min-w-0 flex-1">
          <span class="text-[13.5px] font-black text-slate-800">{{ s.title }}</span>
          <span class="ml-2 rounded-full bg-white px-2 py-0.5 text-[11px] font-bold text-slate-500 ring-1 ring-slate-200">{{ s.items.length }}</span>
          <div class="text-[11.5px] text-slate-400">{{ s.hint }}</div>
        </div>
        <Button v-if="canEdit" variant="outline" size="sm" class="h-7 px-2.5 text-[12px]" @click="open(s.type, null)"><template #leftIcon><Plus :size="13" /></template>{{ $t('project_detail.issue_section.btn_add') }}</Button>
      </header>
      <p v-if="s.items.length === 0" class="px-4 py-3 text-[12px] font-semibold text-slate-400">{{ $t('project_detail.issue_section.empty_type', { type: s.title }) }}</p>
      <ul v-else class="divide-y divide-slate-100">
        <li v-for="i in s.items" :key="i.id" class="group/row flex items-center gap-3 px-4 py-2.5 hover:bg-slate-50/60">
          <span :class="cn('shrink-0 rounded-md px-1.5 py-0.5 text-[10.5px] font-black uppercase', KIND_CHIP[i.kind])">{{ enumLabel('issue_kind', i.kind) }}</span>
          <a :href="i.url" target="_blank" rel="noopener noreferrer" class="flex min-w-0 flex-1 items-center gap-1.5 truncate text-[13px] font-bold text-slate-700 hover:text-pr hover:underline">{{ i.name }}<ExternalLink :size="12" class="shrink-0 text-slate-400" /></a>
          <div v-if="canEdit" class="flex shrink-0 items-center gap-1 opacity-0 transition-opacity group-hover/row:opacity-100">
            <button type="button" class="rounded-md p-1.5 text-slate-400 hover:bg-violet-100 hover:text-pr" :aria-label="$t('project_detail.issue_section.btn_edit')" @click="open(i.type, i)"><Pencil :size="13" /></button>
            <button type="button" class="rounded-md p-1.5 text-slate-400 hover:bg-rose-100 hover:text-rose-600" :aria-label="$t('project_detail.issue_section.btn_remove')" @click="remove(i)"><Trash2 :size="13" /></button>
          </div>
        </li>
      </ul>
    </section>

    <BaseDialog v-model:open="dialogOpen" :title="editing ? $t('project_detail.issue_section.btn_edit') : $t('project_detail.issue_section.btn_add')" :description="$t('project_detail.issue_section.lbl_dialog_desc')" max-width="lg">
      <div class="flex flex-col gap-4">
        <FormItem :label="$t('project_detail.issue_section.lbl_type_label')" required :error="errors.type">
          <div class="flex gap-2">
            <button v-for="ty in TYPES" :key="ty" type="button" :class="cn('flex-1 rounded-xl border px-3 py-2 text-[12.5px] font-bold', form.type === ty ? 'border-pr bg-pr/5 text-pr ring-2 ring-pr/15' : 'border-slate-200 text-slate-500')" @click="form.type = ty">{{ enumLabel('issue_type', ty) }}</button>
          </div>
        </FormItem>
        <FormItem :label="$t('project_detail.issue_section.lbl_kind_label')" required :error="errors.kind">
          <div class="flex gap-2">
            <button v-for="k in KINDS" :key="k" type="button" :class="cn('flex-1 rounded-xl border px-3 py-2 text-[12.5px] font-bold', form.kind === k ? 'border-pr bg-pr/5 text-pr ring-2 ring-pr/15' : 'border-slate-200 text-slate-500')" @click="form.kind = k">{{ enumLabel('issue_kind', k) }}</button>
          </div>
        </FormItem>
        <FormItem :label="$t('project_detail.issue_section.lbl_name')" required :error="errors.name"><BaseInput v-model="form.name" :placeholder="placeholder(form.type)" :error="!!errors.name" /></FormItem>
        <FormItem :label="$t('project_detail.issue_section.lbl_url')" required :error="errors.url"><BaseInput v-model="form.url" placeholder="https://" :error="!!errors.url" /></FormItem>
      </div>
      <template #footer>
        <Button variant="outline" @click="dialogOpen = false">{{ $t('common.btn_cancel') }}</Button>
        <Button variant="primary" :loading="saving" @click="submit">{{ editing ? $t('common.btn_save') : $t('project_detail.issue_section.btn_add') }}</Button>
      </template>
    </BaseDialog>
  </div>
</template>
