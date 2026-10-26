<script setup lang="ts">
// Port ReportDialog.tsx + ReportItemsTable.tsx + reportTemplates.ts: báo cáo tình trạng dự án (GOOD / RISK / SOS) gồm các hạng mục
// (tiêu chí + nội dung), áp dụng mẫu, AI phân tích & điền (qua BE — D5), xuất CSV / Excel, lưu.
// Khác bản gốc: bỏ "Nhập từ file" (ReportImportPopover).
import { ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { ArrowDown, ArrowUp, Plus, Save, Sparkles, Trash2, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import { useExport } from '@/shared/components/export/useExport';
import { projectDetailApi, type ReportStatus, type StatusReport } from '@/modules/project/api/project-detail.api';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { firstFieldErrors } from '@/api/http';

const props = defineProps<{ projectId: string; projectName: string; clientName: string | null }>();
const open = defineModel<boolean>('open', { required: true });
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const { startExport } = useExport();

const status = ref<ReportStatus>('GOOD');
const items = ref<StatusReport['items']>([]);
const loading = ref(false);
const saving = ref(false);
const aiBusy = ref(false);
const errors = ref<Record<string, string>>({});

watch(open, async (v) => {
  if (!v) return;
  errors.value = {};
  loading.value = true;
  try {
    const r = await projectDetailApi.statusReport(props.projectId);
    status.value = r.status;
    items.value = r.items.map((i) => ({ ...i }));
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    loading.value = false;
  }
});

/** Mẫu "Đầy đủ": 7 tiêu chí với khung gợi ý đánh giá định tính; "Trống": không có dòng nào. */
function templateItems(id: 'full' | 'blank') {
  if (id === 'blank') return [];
  const scaffold = t('project_detail.report_dialog.lbl_scaffold');
  return [
    { metric: t('project_detail.report_dialog.lbl_metric_progress'), content: scaffold },
    { metric: t('project_detail.report_dialog.lbl_metric_quality'), content: scaffold },
    { metric: t('project_detail.report_dialog.lbl_metric_personnel'), content: scaffold },
    { metric: t('project_detail.report_dialog.lbl_metric_technical'), content: scaffold },
    { metric: t('project_detail.report_dialog.lbl_metric_communication'), content: scaffold },
    { metric: t('project_detail.report_dialog.lbl_metric_closing'), content: t('project_detail.report_dialog.lbl_scaffold_closing') },
    { metric: t('project_detail.report_dialog.lbl_metric_followup'), content: t('project_detail.report_dialog.lbl_scaffold_followup') },
  ];
}
async function applyTemplate(event: Event) {
  const id = (event.target as HTMLSelectElement).value as 'full' | 'blank' | '';
  (event.target as HTMLSelectElement).value = '';
  if (!id) return;
  if (items.value.some((i) => i.content.trim()) && !(await confirm.ask({ type: 'warning', title: t('project_detail.confirm_template_title'), description: t('project_detail.confirm_template_desc'), confirmText: t('project_detail.btn_replace') }))) return;
  items.value = templateItems(id);
  toast.success(t('project_detail.msg_template_applied'));
}
async function aiFill() {
  if (items.value.some((i) => i.content.trim()) && !(await confirm.ask({ type: 'warning', title: t('project_detail.confirm_ai_title'), description: t('project_detail.confirm_ai_desc'), confirmText: t('project_detail.btn_replace') }))) return;
  aiBusy.value = true;
  try {
    const res = await projectDetailApi.aiFillStatusReport(props.projectId);
    status.value = res.data.status;
    items.value = res.data.items;
    toast.success(res.messageText);
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    aiBusy.value = false;
  }
}
function move(i: number, d: -1 | 1) {
  const j = i + d;
  if (j < 0 || j >= items.value.length) return;
  const list = [...items.value];
  [list[i], list[j]] = [list[j] as StatusReport['items'][number], list[i] as StatusReport['items'][number]];
  items.value = list;
}
async function save() {
  saving.value = true;
  errors.value = {};
  try {
    toast.success((await projectDetailApi.saveStatusReport(props.projectId, { status: status.value, items: items.value })).messageText);
    open.value = false;
  } catch (err) {
    errors.value = firstFieldErrors(err);
    toast.error(Object.values(errors.value)[0] ?? (err as Error).message);
  } finally {
    saving.value = false;
  }
}
function exportFile(format: 'xlsx' | 'csv') {
  const rows = items.value.map((i, idx) => ({ no: idx + 1, ...i }));
  startExport(rows, {
    filename: `${t('project_detail.report_dialog.lbl_filename')}_${props.projectName}`.replace(/[\\/:*?"<>|\s]+/g, '_'), format, mode: 'all',
    sheets: [{
      sheetName: t('project_detail.report_dialog.title'), title: `${props.projectName}${props.clientName ? ` — ${props.clientName}` : ''}`, subTitle: `${t('project_detail.report_dialog.lbl_status')}: ${status.value}`,
      data: rows,
      columns: [
        { id: 'no', header: '#', accessorKey: 'no', width: 6 },
        { id: 'metric', header: t('project_detail.report_dialog.col_metric'), accessorKey: 'metric', width: 24 },
        { id: 'content', header: t('project_detail.report_dialog.col_content'), accessorKey: 'content', width: 80 },
      ],
    }],
  });
}
const STATUS_CLASS: Record<ReportStatus, string> = { GOOD: 'bg-emerald-50 text-emerald-700 border-emerald-200', RISK: 'bg-amber-50 text-amber-700 border-amber-200', SOS: 'bg-rose-50 text-rose-700 border-rose-200' };
</script>

<template>
  <BaseDialog v-model:open="open" :title="$t('project_detail.report_dialog.title')" :description="props.projectName" max-width="4xl">
    <div v-if="loading" class="py-10 text-center text-[13px] text-slate-400 animate-pulse">{{ $t('project_detail.lbl_loading') }}</div>
    <div v-else class="flex flex-col gap-4">
      <div class="flex flex-wrap items-center gap-3">
        <span class="text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.report_dialog.lbl_status') }}</span>
        <div class="flex gap-1.5">
          <button v-for="s in (['GOOD', 'RISK', 'SOS'] as const)" :key="s" type="button" :class="['rounded-lg border px-3 py-1 text-[12px] font-black', status === s ? STATUS_CLASS[s] : 'border-slate-200 bg-white text-slate-400']" @click="status = s">{{ s }}</button>
        </div>
        <select :aria-label="$t('project_detail.report_dialog.lbl_template_apply')" class="ml-auto h-9 rounded-lg border border-slate-200 bg-white px-2 text-[12px] font-semibold text-slate-600" @change="applyTemplate">
          <option value="">{{ $t('project_detail.report_dialog.ph_template') }}</option>
          <option value="full">{{ $t('project_detail.report_dialog.lbl_template_full') }}</option>
          <option value="blank">{{ $t('project_detail.report_dialog.lbl_template_blank') }}</option>
        </select>
        <Button variant="outline" size="sm" :loading="aiBusy" @click="aiFill">
          <template #leftIcon><Sparkles :size="14" class="text-violet-500" /></template>
          {{ aiBusy ? $t('project_detail.report_dialog.lbl_ai_filling') : $t('project_detail.report_dialog.lbl_ai_fill') }}
        </Button>
      </div>
      <div class="overflow-hidden rounded-xl border border-slate-200">
        <table class="w-full border-collapse text-left">
          <thead>
            <tr class="border-b border-slate-200 bg-slate-50/80 text-[11px] font-bold uppercase tracking-wide text-slate-500">
              <th class="w-10 p-2 text-center">#</th>
              <th class="w-[200px] p-2">{{ $t('project_detail.report_dialog.col_metric') }}</th>
              <th class="p-2">{{ $t('project_detail.report_dialog.col_content') }}</th>
              <th class="w-[96px] p-2" />
            </tr>
          </thead>
          <tbody>
            <tr v-if="items.length === 0"><td colspan="4" class="p-6 text-center text-[12.5px] text-slate-400">{{ $t('project_detail.report_dialog.empty') }}</td></tr>
            <tr v-for="(it, i) in items" :key="i" class="border-b border-slate-100 align-top">
              <td class="p-2 pt-4 text-center text-[12px] font-bold text-slate-400">{{ i + 1 }}</td>
              <td class="p-2"><BaseInput v-model="it.metric" :error="!!errors[`items.${i}.metric`]" /></td>
              <td class="p-2"><BaseTextarea v-model="it.content" :rows="4" class="text-[12.5px]" /></td>
              <td class="p-2 pt-3">
                <div class="flex items-center gap-0.5">
                  <button type="button" class="flex h-7 w-7 items-center justify-center rounded-md text-slate-400 hover:bg-slate-100 disabled:opacity-30" :disabled="i === 0" :aria-label="$t('project_detail.btn_move_up')" @click="move(i, -1)"><ArrowUp :size="13" /></button>
                  <button type="button" class="flex h-7 w-7 items-center justify-center rounded-md text-slate-400 hover:bg-slate-100 disabled:opacity-30" :disabled="i === items.length - 1" :aria-label="$t('project_detail.btn_move_down')" @click="move(i, 1)"><ArrowDown :size="13" /></button>
                  <button type="button" class="flex h-7 w-7 items-center justify-center rounded-md text-slate-400 hover:bg-rose-50 hover:text-rose-500" :aria-label="$t('project_detail.btn_remove_row')" @click="items.splice(i, 1)"><Trash2 :size="13" /></button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <button type="button" class="flex items-center gap-1.5 self-start text-[12px] font-bold text-pr hover:underline" @click="items.push({ metric: '', content: '' })"><Plus :size="14" />{{ $t('project_detail.btn_add_row') }}</button>
    </div>
    <template #footer>
      <Button variant="outline" @click="open = false"><template #leftIcon><X :size="16" /></template>{{ $t('common.btn_close') }}</Button>
      <Button variant="outline" :disabled="items.length === 0" @click="exportFile('csv')">{{ $t('project_detail.btn_export_csv') }}</Button>
      <Button variant="outline" :disabled="items.length === 0" @click="exportFile('xlsx')">{{ $t('project_detail.btn_export_excel') }}</Button>
      <Button variant="primary" :loading="saving" @click="save"><template #leftIcon><Save :size="16" /></template>{{ $t('common.btn_save') }}</Button>
    </template>
  </BaseDialog>
</template>
