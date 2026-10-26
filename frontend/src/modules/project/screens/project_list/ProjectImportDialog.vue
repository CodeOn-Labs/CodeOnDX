<script setup lang="ts">
// Port importConfig + downloadCustomTemplate (ProjectListPage.tsx): import dự án từ Excel qua ImportWizard dùng chung.
// Mỗi dòng = 1 dự án + 1 vai trò hợp đồng; các dòng cùng tên dự án được BE gộp thành 1 dự án nhiều vai trò.
// Khách hàng / cấp độ / rủi ro / vai trò nhập theo tên (hoặc mã) danh mục — file mẫu có sheet "Danh mục" liệt kê giá trị hợp lệ.
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQueryClient } from '@tanstack/vue-query';
import ExcelJS from 'exceljs';
import ImportWizard, { type ImportBatchResult, type ImportField } from '@/shared/components/import/ImportWizard.vue';
import { projectApi } from '@/modules/project/api/project.api';
import { useClients, useMasterData } from '@/shared/composables/useMasterData';
import { normalizeDate } from '@/shared/utils/sheet-reader';
import { useToastStore } from '@/stores/toast';

const open = defineModel<boolean>('open', { required: true });
const { t } = useI18n();
const toast = useToastStore();
const queryClient = useQueryClient();
const { items: clients } = useClients();
const { items: grades } = useMasterData('project_level');
const { items: risks } = useMasterData('risk_level');
const { items: roles } = useMasterData('job_position');
const { items: levels } = useMasterData('hr_level');

const fields = computed<ImportField[]>(() => [
  { key: 'name', label: t('project_list.col_import_name'), required: true },
  { key: 'client', label: t('project_list.col_import_client'), required: true },
  { key: 'grade', label: t('project_list.col_import_grade') },
  { key: 'description', label: t('project_list.col_import_description') },
  { key: 'startDate', label: t('project_list.col_import_start') },
  { key: 'endDate', label: t('project_list.col_import_end') },
  { key: 'budget', label: t('project_list.col_import_budget') },
  { key: 'ticketSystem', label: t('project_list.col_import_ticket') },
  { key: 'sourceCode', label: t('project_list.col_import_source') },
  { key: 'risk', label: t('project_list.col_import_risk') },
  { key: 'role', label: t('project_list.col_import_role') },
  { key: 'roleLevel', label: t('project_list.col_import_role_level') },
  { key: 'quantity', label: t('project_list.col_import_quantity') },
  { key: 'fte', label: t('project_list.col_import_fte') },
  { key: 'roleStart', label: t('project_list.col_import_role_start') },
  { key: 'roleEnd', label: t('project_list.col_import_role_end') },
  { key: 'price', label: t('project_list.col_import_price') },
]);
const DATE_FIELDS = ['startDate', 'endDate', 'roleStart', 'roleEnd'];
const NUMBER_FIELDS = ['budget', 'quantity', 'fte', 'price'];

/** Kiểm tra nhanh ở trình duyệt (BE kiểm tra lại đầy đủ): ngày đọc được, số hợp lệ, FTE trong (0, 2]. */
function validateRow(v: Record<string, string>) {
  const errors: string[] = [];
  const label = (k: string) => fields.value.find((f) => f.key === k)?.label ?? k;
  for (const k of DATE_FIELDS) if (v[k] && !normalizeDate(v[k])) errors.push(t('project_list.err_import_date', { field: label(k) }));
  for (const k of NUMBER_FIELDS) if (v[k] && !Number.isFinite(Number(v[k]))) errors.push(t('project_list.err_import_number', { field: label(k) }));
  if (v.fte && (Number(v.fte) <= 0 || Number(v.fte) > 2)) errors.push(t('project_list.err_import_fte'));
  return errors;
}

/** Nhãn lỗi theo trường của BE: name → "Tên dự án", contractStaff.0.ftePerPerson → "FTE". */
const FIELD_OF: Record<string, string> = {
  name: 'name', clientId: 'client', gradeId: 'grade', description: 'description', startDate: 'startDate', endDate: 'endDate', budget: 'budget',
  ticketSystemUrl: 'ticketSystem', sourceCodeUrl: 'sourceCode', roleCode: 'role', levelId: 'roleLevel', quantity: 'quantity', ftePerPerson: 'fte', pricePerMonth: 'price',
};
async function importBatch(rows: Array<{ row: number; values: Record<string, string> }>): Promise<ImportBatchResult> {
  const payload = rows.map(({ row, values }) => ({
    row,
    values: { ...values, ...Object.fromEntries(DATE_FIELDS.map((k) => [k, values[k] ? normalizeDate(values[k] as string) : ''])) },
  }));
  const res = await projectApi.importProjects(payload);
  const errors = res.data.errors.map((e) => {
    const last = e.field?.split('.').at(-1) ?? '';
    const isStaffDate = e.field?.startsWith('contractStaff.') && (last === 'startDate' || last === 'endDate');
    const key = isStaffDate ? (last === 'startDate' ? 'roleStart' : 'roleEnd') : FIELD_OF[last];
    const label = key ? fields.value.find((f) => f.key === key)?.label : null;
    return { row: e.row, message: label ? `${label}: ${e.message}` : e.message };
  });
  if (res.data.created > 0) {
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['projects'] });
  }
  return { created: res.data.created, errors };
}

/** File mẫu: sheet dữ liệu (tiêu đề + 1 dòng ví dụ) và sheet danh mục giá trị hợp lệ lấy từ dữ liệu hiện tại. */
async function downloadTemplate() {
  const wb = new ExcelJS.Workbook();
  const ws = wb.addWorksheet(t('project_list.lbl_import_sheet_data'));
  ws.addRow(fields.value.map((f) => f.label));
  ws.getRow(1).font = { bold: true };
  ws.addRow([
    t('project_list.lbl_import_sample_name'), clients.value[0]?.name ?? '', grades.value[0]?.name ?? '', t('project_list.lbl_import_sample_description'),
    '2026-01-01', '2026-12-31', 50000, 'https://jira.example.com/browse/PROJ', 'https://github.com/example/proj', risks.value[0]?.name ?? '',
    roles.value[0]?.name ?? '', levels.value.find((l) => l.code === 'SENIOR')?.name ?? '', 1, 1, '2026-01-01', '2026-06-30', 3000,
  ]);
  ws.columns.forEach((c) => (c.width = 20));
  const lists = wb.addWorksheet(t('project_list.lbl_import_sheet_lists'));
  const columns = [
    [t('project_list.col_import_client'), clients.value.map((c) => c.name)], [t('project_list.col_import_grade'), grades.value.map((g) => g.name)],
    [t('project_list.col_import_risk'), risks.value.map((r) => r.name)], [t('project_list.col_import_role'), roles.value.map((r) => r.name)],
    [t('project_list.col_import_role_level'), levels.value.map((l) => l.name)],
  ] as const;
  columns.forEach(([title, values], i) => {
    lists.getCell(1, i + 1).value = title;
    lists.getCell(1, i + 1).font = { bold: true };
    values.forEach((v, r) => (lists.getCell(r + 2, i + 1).value = v));
    lists.getColumn(i + 1).width = 26;
  });
  const blob = new Blob([await wb.xlsx.writeBuffer()], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = `${t('project_list.lbl_import_template_filename')}.xlsx`;
  a.click();
  URL.revokeObjectURL(a.href);
}
</script>

<template>
  <ImportWizard
    v-model:open="open"
    :title="$t('project_list.title_import')"
    :fields="fields"
    :validate-row="validateRow"
    :import-batch="importBatch"
    :download-template="downloadTemplate"
    :chunk-size="5000"
  />
</template>
