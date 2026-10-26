<script setup lang="ts">
// Port ImportWizard.tsx + Step1Upload / Step3Mapping / Step4Preview / Step5Progress (shared/components/import của bản gốc):
// 1. Tải file (.xlsx / .csv) + tải file mẫu  2. Ghép cột file ↔ trường dữ liệu (tự ghép theo tên cột)
// 3. Kiểm tra (lỗi từng dòng do màn gọi kiểm tra trước; BE kiểm tra lại khi lưu)  4. Thực thi theo lô + tổng kết.
// Khác bản gốc: chỉ đọc sheet đầu tiên (bỏ bước chọn sheet), không tạm dừng / tiếp tục giữa chừng.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { AlertCircle, CheckCircle2, Download, FileSpreadsheet, Upload, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import { readSheet, type SheetData, type SheetRow } from '@/shared/utils/sheet-reader';
import { cn } from '@/shared/utils/cn';

export interface ImportField {
  key: string;
  label: string;
  required?: boolean;
}
export interface ImportBatchResult {
  created: number;
  errors: Array<{ row: number; message: string }>;
}

const props = withDefaults(
  defineProps<{
    title: string;
    fields: ImportField[];
    /** Kiểm tra 1 dòng đã ghép cột → danh sách lỗi (rỗng = hợp lệ) */
    validateRow?: (row: Record<string, string>) => string[];
    /** Gửi 1 lô dòng hợp lệ (kèm số dòng trong file) */
    importBatch: (rows: Array<{ row: number; values: Record<string, string> }>) => Promise<ImportBatchResult>;
    downloadTemplate?: () => Promise<void> | void;
    chunkSize?: number;
  }>(),
  { validateRow: () => [], downloadTemplate: undefined, chunkSize: 100 },
);
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ done: [created: number] }>();
const { t } = useI18n();

type Step = 'upload' | 'mapping' | 'preview' | 'importing' | 'summary';
const step = ref<Step>('upload');
const sheet = ref<SheetData | null>(null);
const fileName = ref('');
const error = ref('');
const reading = ref(false);
const mapping = ref<Record<string, string>>({});
const progress = ref({ done: 0, total: 0, created: 0 });
const resultErrors = ref<Array<{ row: number; message: string }>>([]);

watch(open, (v) => {
  if (v) return;
  step.value = 'upload';
  sheet.value = null;
  fileName.value = '';
  error.value = '';
  resultErrors.value = [];
});

const fold = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase().replace(/[^a-z0-9]/g, '');
async function onFile(file: File | undefined) {
  if (!file) return;
  error.value = '';
  if (file.size > 10 * 1024 * 1024) {
    error.value = t('component.import_wizard.err_file_too_large');
    return;
  }
  reading.value = true;
  try {
    const data = await readSheet(file);
    if (data.rows.length === 0) throw new Error(t('component.import_wizard.err_empty_file'));
    sheet.value = data;
    fileName.value = file.name;
    // Tự ghép cột theo tên (không dấu, không phân biệt hoa thường)
    mapping.value = Object.fromEntries(props.fields.map((f) => [f.key, data.headers.find((h) => fold(h) === fold(f.label)) ?? '']));
    step.value = 'mapping';
  } catch (err) {
    error.value = (err as Error).message || t('component.import_wizard.err_read_failed');
  } finally {
    reading.value = false;
  }
}
const dragOver = ref(false);
function onDrop(e: DragEvent) {
  dragOver.value = false;
  void onFile(e.dataTransfer?.files?.[0]);
}

const missingRequired = computed(() => props.fields.filter((f) => f.required && !mapping.value[f.key]));
const mappedRows = computed(() => (sheet.value?.rows ?? []).map((r: SheetRow, i) => {
  const values = Object.fromEntries(props.fields.map((f) => [f.key, mapping.value[f.key] ? (r[mapping.value[f.key] as string] ?? '').trim() : '']));
  const errors = [
    ...props.fields.filter((f) => f.required && !values[f.key]).map((f) => t('component.import_wizard.err_required', { field: f.label })),
    ...props.validateRow(values),
  ];
  // Dòng 1 là tiêu đề → dòng dữ liệu đầu tiên là dòng 2 trong file
  return { row: i + 2, values, errors };
}));
const validRows = computed(() => mappedRows.value.filter((r) => r.errors.length === 0));
const invalidRows = computed(() => mappedRows.value.filter((r) => r.errors.length > 0));
const previewRows = computed(() => [...invalidRows.value, ...validRows.value].slice(0, 50));

async function execute() {
  const rows = validRows.value.map(({ row, values }) => ({ row, values }));
  step.value = 'importing';
  progress.value = { done: 0, total: rows.length, created: 0 };
  resultErrors.value = invalidRows.value.map((r) => ({ row: r.row, message: r.errors.join('; ') }));
  for (let i = 0; i < rows.length; i += props.chunkSize) {
    const batch = rows.slice(i, i + props.chunkSize);
    try {
      const res = await props.importBatch(batch);
      progress.value.created += res.created;
      resultErrors.value.push(...res.errors);
    } catch (err) {
      resultErrors.value.push(...batch.map((b) => ({ row: b.row, message: (err as Error).message })));
    }
    progress.value.done = Math.min(rows.length, i + batch.length);
  }
  resultErrors.value.sort((a, b) => a.row - b.row);
  step.value = 'summary';
  emit('done', progress.value.created);
}
const STEPS = computed(() => [
  { key: 'upload', label: t('component.import_wizard.lbl_step_upload') },
  { key: 'mapping', label: t('component.import_wizard.lbl_step_mapping') },
  { key: 'preview', label: t('component.import_wizard.lbl_step_preview') },
  { key: 'importing', label: t('component.import_wizard.lbl_step_execute') },
]);
const stepIndex = computed(() => ['upload', 'mapping', 'preview', 'importing', 'summary'].indexOf(step.value));
</script>

<template>
  <BaseDialog :open="open" :title="props.title" max-width="3xl" body-class="p-0 overflow-hidden" @update:open="(v: boolean) => { if (step !== 'importing') open = v; }">
    <div class="flex h-[70vh] min-h-[400px] flex-col">
      <div class="flex shrink-0 items-center justify-center gap-2 border-b border-slate-100 bg-white/80 px-6 py-4">
        <template v-for="(s, i) in STEPS" :key="s.key">
          <div class="flex items-center gap-2">
            <div :class="cn('flex h-6 w-6 items-center justify-center rounded-full text-[11px] font-bold', i === Math.min(stepIndex, 3) && stepIndex < 4 ? 'bg-pr text-white ring-4 ring-pr/20' : i < stepIndex ? 'bg-emerald-500 text-white' : 'bg-slate-200 text-slate-500')">
              <CheckCircle2 v-if="i < stepIndex" :size="13" /><template v-else>{{ i + 1 }}</template>
            </div>
            <span :class="cn('text-[12px] font-bold', i === stepIndex ? 'text-slate-800' : i < stepIndex ? 'text-emerald-600' : 'text-slate-400')">{{ s.label }}</span>
          </div>
          <div v-if="i < STEPS.length - 1" :class="cn('mx-1 h-[2px] w-8 rounded-full', i < stepIndex ? 'bg-emerald-500' : 'bg-slate-200')" />
        </template>
      </div>

      <div class="relative flex min-h-0 flex-1 flex-col overflow-y-auto bg-slate-50/30 p-6">
        <!-- 1. Tải file -->
        <div v-if="step === 'upload'" class="mx-auto flex w-full max-w-xl flex-col gap-5 pt-2">
          <div class="text-center">
            <h2 class="mb-2 bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-[22px] font-black tracking-tight text-transparent">{{ $t('component.import_wizard.title_upload') }}</h2>
            <p class="text-[13px] font-medium text-slate-500">{{ $t('component.import_wizard.hint_formats') }}</p>
          </div>
          <label
            :class="cn('relative flex cursor-pointer flex-col items-center justify-center gap-3 rounded-[24px] border-2 border-dashed bg-white px-6 py-12 text-center transition-colors', dragOver ? 'border-pr bg-pr/5' : 'border-slate-200 hover:border-pr/50')"
            @dragover.prevent="dragOver = true"
            @dragleave="dragOver = false"
            @drop.prevent="onDrop"
          >
            <input type="file" accept=".xlsx,.csv" class="hidden" @change="onFile(($event.target as HTMLInputElement).files?.[0])" />
            <div class="flex h-14 w-14 items-center justify-center rounded-2xl bg-pr/10 text-pr"><Upload :size="26" /></div>
            <div class="text-[15px] font-bold text-slate-700">{{ $t('component.import_wizard.lbl_drop') }}</div>
            <p class="text-[12px] font-medium text-slate-400">{{ $t('component.import_wizard.hint_template_only') }}</p>
            <div v-if="reading" class="absolute inset-0 z-10 flex flex-col items-center justify-center rounded-[24px] bg-white/80 backdrop-blur-sm">
              <div class="mb-4 h-8 w-8 animate-spin rounded-full border-[3px] border-pr border-t-transparent" />
              <div class="animate-pulse text-[13px] font-bold text-pr">{{ $t('component.import_wizard.msg_reading') }}</div>
            </div>
          </label>
          <div v-if="error" class="flex items-center gap-3 rounded-[20px] border border-red-100 bg-red-50 p-4 text-red-600 shadow-sm">
            <AlertCircle :size="20" class="shrink-0 text-red-500" /><div class="text-[13px] font-semibold">{{ error }}</div>
          </div>
          <div v-if="props.downloadTemplate" class="flex justify-center border-t border-slate-100/60 pt-6">
            <Button variant="outline" @click="props.downloadTemplate()"><template #leftIcon><Download :size="16" /></template>{{ $t('component.import_wizard.btn_download_template') }}</Button>
          </div>
        </div>

        <!-- 2. Ghép cột -->
        <div v-else-if="step === 'mapping' && sheet" class="flex flex-col gap-4">
          <div class="flex items-center gap-2 text-[13px] font-semibold text-slate-600"><FileSpreadsheet :size="16" class="text-emerald-600" />{{ fileName }} · {{ $t('component.import_wizard.lbl_row_count', { count: sheet.rows.length }) }}</div>
          <div class="overflow-hidden rounded-2xl border border-slate-200 bg-white">
            <div v-for="f in props.fields" :key="f.key" class="grid grid-cols-2 items-center gap-3 border-b border-slate-100 px-4 py-2 last:border-b-0">
              <span class="text-[13px] font-bold text-slate-700">{{ f.label }}<span v-if="f.required" class="text-rose-500"> *</span></span>
              <select v-model="mapping[f.key]" :aria-label="f.label" :class="cn('h-9 rounded-lg border bg-white px-2 text-[12.5px] outline-none focus:border-pr', f.required && !mapping[f.key] ? 'border-rose-300' : 'border-slate-200')">
                <option value="">{{ $t('component.import_wizard.opt_skip') }}</option>
                <option v-for="h in sheet.headers" :key="h" :value="h">{{ h }}</option>
              </select>
            </div>
          </div>
          <p v-if="missingRequired.length > 0" class="text-[12px] font-semibold text-rose-600">{{ $t('component.import_wizard.err_missing_required', { fields: missingRequired.map((f) => f.label).join(', ') }) }}</p>
        </div>

        <!-- 3. Kiểm tra -->
        <div v-else-if="step === 'preview'" class="flex flex-col gap-3">
          <div class="flex flex-wrap gap-2 text-[12px] font-bold">
            <span class="rounded-full bg-emerald-50 px-3 py-1 text-emerald-700">{{ $t('component.import_wizard.lbl_valid', { count: validRows.length }) }}</span>
            <span class="rounded-full bg-rose-50 px-3 py-1 text-rose-700">{{ $t('component.import_wizard.lbl_invalid', { count: invalidRows.length }) }}</span>
          </div>
          <p v-if="invalidRows.length > 0" class="text-[12px] text-slate-500">{{ $t('component.import_wizard.hint_invalid_skipped') }}</p>
          <div class="overflow-auto rounded-2xl border border-slate-200 bg-white">
            <table class="w-full border-collapse text-left text-[12px]">
              <thead class="sticky top-0 bg-slate-50">
                <tr>
                  <th class="border-b border-slate-200 p-2 text-slate-500">#</th>
                  <th v-for="f in props.fields.filter((x) => mapping[x.key])" :key="f.key" class="whitespace-nowrap border-b border-slate-200 p-2 text-slate-500">{{ f.label }}</th>
                </tr>
              </thead>
              <tbody>
                <template v-for="r in previewRows" :key="r.row">
                  <tr :class="r.errors.length > 0 ? 'bg-rose-50/40' : ''">
                    <td class="border-b border-slate-100 p-2 font-bold text-slate-400">{{ r.row }}</td>
                    <td v-for="f in props.fields.filter((x) => mapping[x.key])" :key="f.key" class="max-w-[200px] truncate border-b border-slate-100 p-2 text-slate-700">{{ r.values[f.key] }}</td>
                  </tr>
                  <tr v-if="r.errors.length > 0"><td /><td :colspan="props.fields.length" class="border-b border-slate-100 px-2 pb-2 text-[11.5px] font-semibold text-rose-600">{{ r.errors.join('; ') }}</td></tr>
                </template>
              </tbody>
            </table>
          </div>
        </div>

        <!-- 4. Thực thi + tổng kết -->
        <div v-else class="mx-auto flex w-full max-w-xl flex-col items-center gap-4 pt-6 text-center">
          <template v-if="step === 'importing'">
            <div class="h-10 w-10 animate-spin rounded-full border-[3px] border-pr border-t-transparent" />
            <div class="text-[14px] font-bold text-slate-700">{{ $t('component.import_wizard.msg_importing', { done: progress.done, total: progress.total }) }}</div>
            <div class="h-2 w-full overflow-hidden rounded-full bg-slate-100"><div class="h-full bg-pr transition-all" :style="{ width: `${progress.total ? (progress.done / progress.total) * 100 : 100}%` }" /></div>
          </template>
          <template v-else>
            <CheckCircle2 :size="40" class="text-emerald-500" />
            <div class="text-[16px] font-black text-slate-800">{{ $t('component.import_wizard.msg_summary', { created: progress.created, failed: resultErrors.length }) }}</div>
            <div v-if="resultErrors.length > 0" class="max-h-60 w-full overflow-auto rounded-2xl border border-rose-100 bg-white text-left">
              <div v-for="(e, i) in resultErrors" :key="i" class="border-b border-rose-50 px-3 py-1.5 text-[12px] text-rose-700 last:border-b-0"><b>{{ $t('component.import_wizard.lbl_row', { row: e.row }) }}</b>: {{ e.message }}</div>
            </div>
          </template>
        </div>
      </div>
    </div>
    <template #footer>
      <Button v-if="step !== 'importing'" variant="outline" @click="open = false"><template #leftIcon><X :size="16" /></template>{{ step === 'summary' ? $t('common.btn_close') : $t('common.btn_cancel') }}</Button>
      <Button v-if="step === 'mapping'" variant="outline" @click="step = 'upload'">{{ $t('common.btn_back') }}</Button>
      <Button v-if="step === 'mapping'" variant="primary" :disabled="missingRequired.length > 0" @click="step = 'preview'">{{ $t('component.import_wizard.btn_next') }}</Button>
      <Button v-if="step === 'preview'" variant="outline" @click="step = 'mapping'">{{ $t('common.btn_back') }}</Button>
      <Button v-if="step === 'preview'" variant="primary" :disabled="validRows.length === 0" @click="execute"><template #leftIcon><Upload :size="16" /></template>{{ $t('component.import_wizard.btn_import', { count: validRows.length }) }}</Button>
    </template>
  </BaseDialog>
</template>
