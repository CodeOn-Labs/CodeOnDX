<script setup lang="ts">
// Port ProjectDocumentsScreen + DocumentGroupCard + DocumentRow + DocumentFieldsDialog: nhóm tài liệu do PM tạo, mỗi nhóm gắn
// nhiều link tài liệu (mới nhất lên đầu, có nhãn "Mới nhất"), thu gọn / mở nhóm, sửa / xoá nhóm và tài liệu.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { ChevronRight, ExternalLink, FileText, FolderOpen, FolderPlus, Pencil, Plus, Trash2 } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import { projectDetailApi, type DocumentGroup, type ProjectDetail, type ProjectDocument } from '@/modules/project/api/project-detail.api';
import { firstFieldErrors } from '@/api/http';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { matchesQuery } from './management-search';

const props = defineProps<{ project: ProjectDetail; query: string }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();
const q = useQuery({
  queryKey: computed(() => ['projects', 'documents', props.project.id] as const),
  queryFn: ({ signal }) => projectDetailApi.documents(props.project.id, { signal }),
});
const data = computed(() => q.data.value);
const canEdit = computed(() => !!data.value?.canEdit);
const refresh = () => void queryClient.invalidateQueries({ queryKey: ['projects', 'documents', props.project.id] });
/** Lọc theo từ khoá: nhóm khớp tên → hiện cả nhóm; ngược lại chỉ giữ tài liệu khớp tên / link. */
const groups = computed(() => (data.value?.groups ?? []).map((g) => {
  if (!props.query || matchesQuery(props.query, g.name, g.description)) return g;
  return { ...g, documents: g.documents.filter((d) => matchesQuery(props.query, d.name, d.url)) };
}).filter((g) => !props.query || g.documents.length > 0 || matchesQuery(props.query, g.name, g.description)));

const collapsed = ref(new Set<string>());
function toggle(id: string) {
  const next = new Set(collapsed.value);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  collapsed.value = next;
}

type DialogState = { kind: 'group'; group: DocumentGroup | null } | { kind: 'document'; group: DocumentGroup; doc: ProjectDocument | null };
const dialog = ref<DialogState | null>(null);
const first = ref('');
const second = ref('');
const errors = ref<Record<string, string>>({});
const saving = ref(false);
function open(state: DialogState) {
  dialog.value = state;
  errors.value = {};
  if (state.kind === 'group') {
    first.value = state.group?.name ?? '';
    second.value = state.group?.description ?? '';
  } else {
    first.value = state.doc?.name ?? '';
    second.value = state.doc?.url ?? '';
  }
}
const dialogTitle = computed(() => {
  const d = dialog.value;
  if (!d) return '';
  if (d.kind === 'group') return d.group ? t('project_detail.documents_section.btn_edit_group') : t('project_detail.documents_section.btn_create_group');
  return d.doc ? t('project_detail.documents_section.btn_edit_document') : t('project_detail.documents_section.btn_attach_document');
});
async function submit() {
  const d = dialog.value;
  if (!d) return;
  saving.value = true;
  try {
    let res;
    if (d.kind === 'group') {
      const input = { name: first.value, description: second.value };
      res = d.group ? await projectDetailApi.documentGroup.update(props.project.id, d.group.id, input) : await projectDetailApi.documentGroup.create(props.project.id, input);
    } else {
      const input = { groupId: d.group.id, name: first.value, url: second.value };
      res = d.doc ? await projectDetailApi.document.update(props.project.id, d.doc.id, input) : await projectDetailApi.document.create(props.project.id, input);
      // Gắn tài liệu vào nhóm đang thu gọn thì mở nhóm ra
      const next = new Set(collapsed.value);
      next.delete(d.group.id);
      collapsed.value = next;
    }
    toast.success(res.messageText);
    dialog.value = null;
    refresh();
  } catch (err) {
    errors.value = firstFieldErrors(err);
    if (Object.keys(errors.value).length === 0) toast.error((err as Error).message);
  } finally {
    saving.value = false;
  }
}
async function removeGroup(g: DocumentGroup) {
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.documents_section.confirm_delete_group_title'), description: g.documents.length > 0 ? t('project_detail.documents_section.confirm_delete_group_with_docs', { name: g.name, count: g.documents.length }) : t('project_detail.documents_section.confirm_delete_group_desc', { name: g.name }), confirmText: t('project_detail.btn_confirm_delete') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.documentGroup.remove(props.project.id, g.id)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
async function removeDoc(d: ProjectDocument) {
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.documents_section.confirm_delete_doc_title'), description: t('project_detail.documents_section.confirm_delete_doc_desc', { name: d.name }), confirmText: t('project_detail.btn_confirm_delete') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.document.remove(props.project.id, d.id)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
</script>

<template>
  <div v-if="!data" class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
  <div v-else class="flex flex-col gap-4">
    <div class="flex items-center justify-between gap-3">
      <div class="flex items-center gap-2">
        <FolderOpen :size="16" class="text-pr" />
        <h3 class="text-[14px] font-black text-slate-800">{{ $t('project_detail.management_section.lbl_documents') }}</h3>
        <span class="text-[12px] font-semibold text-slate-400">{{ $t('project_detail.documents_section.lbl_summary', { groups: data.summary.groupCount, docs: data.summary.documentCount }) }}</span>
      </div>
      <Button v-if="canEdit" variant="primary" size="sm" @click="open({ kind: 'group', group: null })"><template #leftIcon><FolderPlus :size="14" /></template>{{ $t('project_detail.documents_section.btn_create_group') }}</Button>
    </div>

    <div v-if="groups.length === 0" class="flex h-48 flex-col items-center justify-center gap-2 rounded-[16px] border border-dashed border-slate-200 bg-slate-50/50 px-6 text-center">
      <FolderOpen class="text-slate-300" :size="28" />
      <p class="text-[13px] font-bold text-slate-500">{{ props.query ? $t('project_detail.management_section.empty_result') : $t('project_detail.documents_section.empty_title') }}</p>
      <p v-if="!props.query" class="max-w-sm text-[12px] text-slate-400">{{ $t('project_detail.documents_section.empty_hint') }}</p>
    </div>
    <section v-for="g in groups" :key="g.id" class="overflow-hidden rounded-[14px] border border-[#e8ecf5] bg-white shadow-sm">
      <header :class="['group/header flex items-center gap-2 bg-slate-50/70 px-3 py-2.5', !collapsed.has(g.id) && g.documents.length > 0 && 'border-b border-slate-100']">
        <button type="button" class="flex min-w-0 flex-1 items-center gap-2 text-left" @click="toggle(g.id)">
          <ChevronRight :size="15" :class="['shrink-0 text-slate-400 transition-transform', !collapsed.has(g.id) && 'rotate-90']" />
          <FolderOpen :size="16" class="shrink-0 text-amber-500" />
          <span class="truncate text-[13.5px] font-black text-slate-800">{{ g.name }}</span>
          <span class="shrink-0 rounded-full bg-white px-2 py-0.5 text-[11px] font-bold text-slate-500 ring-1 ring-slate-200">{{ g.documents.length }}</span>
          <span v-if="g.description" class="hidden truncate text-[12px] font-medium text-slate-400 sm:inline">— {{ g.description }}</span>
        </button>
        <div v-if="canEdit" class="flex shrink-0 items-center gap-1">
          <button type="button" :title="$t('project_detail.documents_section.btn_edit_group')" class="rounded-md p-1.5 text-slate-400 opacity-0 transition-opacity hover:bg-violet-100 hover:text-pr group-hover/header:opacity-100" @click="open({ kind: 'group', group: g })"><Pencil :size="14" /></button>
          <button type="button" :title="$t('project_detail.documents_section.btn_delete_group')" class="rounded-md p-1.5 text-slate-400 opacity-0 transition-opacity hover:bg-rose-100 hover:text-rose-600 group-hover/header:opacity-100" @click="removeGroup(g)"><Trash2 :size="14" /></button>
          <Button variant="outline" size="sm" class="h-7 px-2.5 text-[12px]" @click="open({ kind: 'document', group: g, doc: null })"><template #leftIcon><Plus :size="13" /></template>{{ $t('project_detail.documents_section.btn_attach_document') }}</Button>
        </div>
      </header>
      <template v-if="!collapsed.has(g.id)">
        <p v-if="g.documents.length === 0" class="px-4 py-4 text-center text-[12px] font-semibold text-slate-400">{{ $t('project_detail.documents_section.empty_group') }}</p>
        <ul v-else class="divide-y divide-slate-100">
          <li v-for="(d, i) in g.documents" :key="d.id" class="group/row flex items-center gap-3 px-4 py-2.5 hover:bg-slate-50/60">
            <FileText :size="15" class="shrink-0 text-slate-400" />
            <div class="min-w-0 flex-1">
              <a :href="d.url" target="_blank" rel="noopener noreferrer" class="flex items-center gap-1.5 truncate text-[13px] font-bold text-slate-700 hover:text-pr hover:underline">
                {{ d.name }}<ExternalLink :size="12" class="shrink-0 text-slate-400" />
              </a>
              <div class="text-[11px] text-slate-400">{{ $t('project_detail.lbl_updated_by', { name: d.updatedByName ?? '—', date: formatDate(d.updatedAt) }) }}</div>
            </div>
            <span v-if="i === 0 && !props.query" class="shrink-0 rounded-full bg-emerald-50 px-2 py-0.5 text-[10.5px] font-bold text-emerald-600">{{ $t('project_detail.documents_section.lbl_latest') }}</span>
            <div v-if="canEdit" class="flex shrink-0 items-center gap-1 opacity-0 transition-opacity group-hover/row:opacity-100">
              <button type="button" class="rounded-md p-1.5 text-slate-400 hover:bg-violet-100 hover:text-pr" :aria-label="$t('common.btn_edit')" @click="open({ kind: 'document', group: g, doc: d })"><Pencil :size="13" /></button>
              <button type="button" class="rounded-md p-1.5 text-slate-400 hover:bg-rose-100 hover:text-rose-600" :aria-label="$t('common.btn_delete')" @click="removeDoc(d)"><Trash2 :size="13" /></button>
            </div>
          </li>
        </ul>
      </template>
    </section>

    <BaseDialog :open="!!dialog" :title="dialogTitle" :description="dialog?.kind === 'document' ? dialog.group.name : undefined" max-width="md" @update:open="(v: boolean) => { if (!v) dialog = null; }">
      <div class="flex flex-col gap-4">
        <template v-if="dialog?.kind === 'group'">
          <FormItem :label="$t('project_detail.documents_section.lbl_group_name')" required :error="errors.name"><BaseInput v-model="first" :placeholder="$t('project_detail.documents_section.ph_group_name')" :error="!!errors.name" /></FormItem>
          <FormItem :label="$t('project_detail.documents_section.lbl_group_desc')" :error="errors.description"><BaseInput v-model="second" :error="!!errors.description" /></FormItem>
        </template>
        <template v-else>
          <FormItem :label="$t('project_detail.documents_section.lbl_name')" required :error="errors.name"><BaseInput v-model="first" :placeholder="$t('project_detail.documents_section.ph_name')" :error="!!errors.name" /></FormItem>
          <FormItem :label="$t('project_detail.documents_section.lbl_url')" required :error="errors.url"><BaseInput v-model="second" placeholder="https://" :error="!!errors.url" /></FormItem>
        </template>
      </div>
      <template #footer>
        <Button variant="outline" @click="dialog = null">{{ $t('common.btn_cancel') }}</Button>
        <Button variant="primary" :loading="saving" @click="submit">{{ $t('common.btn_save') }}</Button>
      </template>
    </BaseDialog>
  </div>
</template>
