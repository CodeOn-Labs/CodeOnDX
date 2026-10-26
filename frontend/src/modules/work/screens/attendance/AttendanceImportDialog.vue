<script setup lang="ts">
// Import file xuất từ máy chấm công (.xlsx / .csv): chọn file → ghép cột (Mã NV, Ngày, Giờ vào, Giờ ra) → xem trước → nhập.
// Mặc định không ghi đè ô đã sửa tay (giữ luật cũ của báo cáo tháng), có tuỳ chọn ghi đè.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQueryClient } from '@tanstack/vue-query';
import { FileSpreadsheet, Upload, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import { workApi, type AttendanceImportResult, type AttendanceImportRow } from '@/modules/work/api/work.api';
import { normalizeDate, normalizeTime, readSheet, type SheetData } from '@/shared/utils/sheet-reader';
import { useToastStore } from '@/stores/toast';

const open = defineModel<boolean>('open', { required: true });
const { t } = useI18n();
const toast = useToastStore();
const queryClient = useQueryClient();

const FIELDS = ['employeeCode', 'date', 'checkIn', 'checkOut'] as const;
type Field = (typeof FIELDS)[number];
/** Gợi ý tự ghép cột theo tên tiêu đề hay gặp của máy chấm công. */
const GUESS: Record<Field, RegExp> = {
  employeeCode: /(mã|ma|code|id|nv|emp)/i,
  date: /(ngày|ngay|date)/i,
  checkIn: /(vào|vao|in|giờ vào|check.?in)/i,
  checkOut: /(ra|out|check.?out)/i,
};

const sheet = ref<SheetData | null>(null);
const fileName = ref('');
const mapping = ref<Record<Field, string>>({ employeeCode: '', date: '', checkIn: '', checkOut: '' });
const overwrite = ref(false);
const busy = ref(false);
const result = ref<AttendanceImportResult | null>(null);
const fileInput = ref<HTMLInputElement | null>(null);
watch(open, (v) => {
  if (v) {
    sheet.value = null;
    fileName.value = '';
    result.value = null;
    overwrite.value = false;
  }
});

async function pick(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  try {
    sheet.value = await readSheet(file);
    fileName.value = file.name;
    result.value = null;
    const used = new Set<string>();
    for (const f of FIELDS) {
      const hit = sheet.value.headers.find((h) => !used.has(h) && GUESS[f].test(h)) ?? '';
      mapping.value[f] = hit;
      if (hit) used.add(hit);
    }
  } catch {
    toast.error(t('attendance.err_read_file'));
  }
}
const headerOptions = computed(() => (sheet.value?.headers ?? []).map((h) => ({ value: h, label: h })));
const fieldLabel = (f: Field) => ({ employeeCode: t('attendance.lbl_map_code'), date: t('attendance.lbl_map_date'), checkIn: t('attendance.lbl_check_in'), checkOut: t('attendance.lbl_check_out') })[f];
const rows = computed<AttendanceImportRow[]>(() =>
  (sheet.value?.rows ?? []).map((r) => ({
    employeeCode: (r[mapping.value.employeeCode] ?? '').trim(),
    date: normalizeDate(r[mapping.value.date] ?? ''),
    checkIn: normalizeTime(r[mapping.value.checkIn] ?? '') || null,
    checkOut: normalizeTime(r[mapping.value.checkOut] ?? '') || null,
  })),
);
const ready = computed(() => !!mapping.value.employeeCode && !!mapping.value.date && rows.value.length > 0);
const errorLabel = (code: string) =>
  ({ unknown_employee: t('attendance.err_unknown_employee'), invalid_date: t('attendance.err_invalid_date'), invalid_time: t('attendance.err_invalid_time'), out_before_in: t('attendance.err_out_before_in') })[code] ?? code;

async function submit() {
  busy.value = true;
  try {
    const res = await workApi.importAttendance(rows.value, overwrite.value);
    result.value = res.data;
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['attendance'] });
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <BaseDialog v-model:open="open" :title="$t('attendance.title_import_dialog')" :description="$t('attendance.hint_import')" max-width="2xl">
    <div class="flex flex-col gap-5">
      <button
        type="button"
        class="flex flex-col items-center gap-2 rounded-2xl border-2 border-dashed border-slate-200 bg-slate-50/50 p-6 text-slate-500 transition-all hover:border-pr hover:bg-pr/5 hover:text-pr"
        @click="fileInput?.click()"
      >
        <FileSpreadsheet :size="28" />
        <span class="text-[13px] font-bold">{{ fileName || $t('attendance.btn_choose_file') }}</span>
        <span v-if="sheet" class="text-[11px]">{{ $t('attendance.lbl_rows_found', { n: sheet.rows.length }) }}</span>
      </button>
      <input ref="fileInput" type="file" class="hidden" accept=".xlsx,.csv" @change="pick" />

      <template v-if="sheet">
        <div class="grid grid-cols-2 gap-4">
          <FormSelect v-for="f in FIELDS" :key="f" v-model="mapping[f]" :label="fieldLabel(f)" :options="headerOptions" :required="f === 'employeeCode' || f === 'date'" hide-message />
        </div>
        <div class="max-h-[200px] overflow-auto rounded-xl border border-slate-100">
          <table class="w-full text-[11.5px]">
            <thead class="bg-slate-50 text-slate-500"><tr>
              <th class="px-3 py-1.5 text-left">{{ $t('attendance.lbl_map_code') }}</th>
              <th class="px-3 py-1.5 text-left">{{ $t('attendance.lbl_map_date') }}</th>
              <th class="px-3 py-1.5 text-left">{{ $t('attendance.lbl_check_in') }}</th>
              <th class="px-3 py-1.5 text-left">{{ $t('attendance.lbl_check_out') }}</th>
            </tr></thead>
            <tbody>
              <tr v-for="(r, i) in rows.slice(0, 20)" :key="i" class="border-t border-slate-50">
                <td class="px-3 py-1 font-mono">{{ r.employeeCode }}</td>
                <td :class="['px-3 py-1', !r.date && 'text-rose-500']">{{ r.date || '??' }}</td>
                <td class="px-3 py-1">{{ r.checkIn ?? '--' }}</td>
                <td class="px-3 py-1">{{ r.checkOut ?? '--' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <label class="flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50 p-3">
          <span class="flex flex-col">
            <span class="text-[12px] font-bold text-slate-700">{{ $t('attendance.lbl_overwrite') }}</span>
            <span class="text-[10.5px] text-slate-400">{{ $t('attendance.hint_overwrite') }}</span>
          </span>
          <SwitchToggle v-model="overwrite" size="sm" :label="$t('attendance.lbl_overwrite')" />
        </label>
      </template>

      <div v-if="result" class="rounded-xl border border-emerald-100 bg-emerald-50/50 p-3 text-[12px]">
        <p class="font-bold text-emerald-700">{{ $t('attendance.lbl_import_result', { imported: result.imported, skipped: result.skipped, errors: result.errors.length }) }}</p>
        <ul v-if="result.errors.length" class="mt-2 max-h-[120px] overflow-auto text-[11px] text-rose-600">
          <li v-for="e in result.errors" :key="e.row">{{ $t('attendance.lbl_import_error_row', { row: e.row, code: e.employeeCode }) }}: {{ e.codes.map(errorLabel).join(', ') }}</li>
        </ul>
      </div>
    </div>
    <template #footer>
      <Button variant="danger" :disabled="busy" @click="open = false"><template #leftIcon><X :size="16" /></template>{{ $t('common.btn_close') }}</Button>
      <Button variant="primary" :disabled="!ready" :loading="busy" @click="submit"><template #leftIcon><Upload :size="16" /></template>{{ $t('attendance.btn_import_now') }}</Button>
    </template>
  </BaseDialog>
</template>
