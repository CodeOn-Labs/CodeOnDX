<script setup lang="ts">
// Port ProjectNewPage.tsx + ProjectEditPage.tsx (một màn cho cả tạo và sửa): thông tin chung, nhân sự hợp đồng
// (vai trò × cấp độ × số lượng × FTE/người × thời gian × đơn giá), thanh thao tác dính đáy.
// Khác bản gốc: kiểm tra dữ liệu ở BE (lỗi 400 gắn theo ô, kể cả từng dòng nhân sự hợp đồng `contractStaff.{i}.{field}`);
// "Thêm vai trò mới" ngay trong ô chọn bỏ — vai trò thêm ở Quản trị → Danh mục → Vị trí công việc.
import { computed, ref, watch } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { AlertCircle, ArrowLeft, Check, Info, Plus, Trash2, X } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import ClientDialog from '@/modules/admin/screens/admin_client_list/ClientDialog.vue';
import { projectDetailApi, type ContractStaffInput, type ProjectInput } from '@/modules/project/api/project-detail.api';
import { orgApi, type ClientInput } from '@/modules/org/api/org.api';
import { useClients, useMasterData } from '@/shared/composables/useMasterData';
import { firstFieldErrors } from '@/api/http';
import { useAuthStore } from '@/stores/auth';
import { useToastStore } from '@/stores/toast';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const toast = useToastStore();
const auth = useAuthStore();
const queryClient = useQueryClient();

const projectId = computed(() => (route.params.id ? String(route.params.id) : null));
const isEdit = computed(() => !!projectId.value);
const canAddClient = computed(() => auth.can('admin.settings'));

const { items: clients, refetch: refetchClients } = useClients();
const { items: grades } = useMasterData('project_level');
const { items: statuses } = useMasterData('project_status');
const { items: roles } = useMasterData('job_position');
const { items: levels } = useMasterData('hr_level');
const clientOptions = computed(() => clients.value.map((c) => ({ value: c.id, label: c.name })));
const toOptions = (rows: Array<{ id: string; name: string }>) => rows.map((r) => ({ value: r.id, label: r.name }));
const roleOptions = computed(() => roles.value.map((r) => ({ value: r.code, label: r.name })));

const empty = (): ProjectInput => ({ name: '', clientId: '', gradeId: '', statusId: '', description: '', startDate: null, endDate: null, budget: '', ticketSystemUrl: '', sourceCodeUrl: '', contractStaff: [] });
const form = ref<ProjectInput>(empty());
const errors = ref<Record<string, string>>({});
const saving = ref(false);

const detail = useQuery({
  queryKey: computed(() => ['projects', 'detail', projectId.value] as const),
  queryFn: ({ signal }) => projectDetailApi.detail(projectId.value as string, { signal }),
  enabled: isEdit,
});
watch(
  () => detail.data.value,
  (p) => {
    if (!p) return;
    form.value = {
      name: p.name, clientId: p.client?.id ?? '', gradeId: p.grade?.id ?? '', statusId: p.status?.id ?? '', description: p.description,
      startDate: p.startDate, endDate: p.endDate, budget: p.budget ?? '', ticketSystemUrl: p.ticketSystemUrl ?? '', sourceCodeUrl: p.sourceCodeUrl ?? '',
      contractStaff: p.contractStaff.map((cs) => ({ id: cs.id, roleCode: cs.roleCode, levelId: cs.level?.id ?? '', quantity: cs.quantity, ftePerPerson: cs.ftePerPerson, startDate: cs.startDate, endDate: cs.endDate, pricePerMonth: cs.pricePerMonth })),
    };
  },
  { immediate: true },
);

function addRow() {
  // Dòng mới mặc định cấp Junior, 1 người × 1.0 FTE, theo thời gian dự án (giống bản gốc)
  const junior = levels.value.find((l) => l.code === 'JUNIOR') ?? levels.value[0];
  form.value.contractStaff.push({ roleCode: '', levelId: junior?.id ?? '', quantity: 1, ftePerPerson: 1, startDate: form.value.startDate, endDate: form.value.endDate, pricePerMonth: '' });
}
function removeRow(index: number) {
  form.value.contractStaff.splice(index, 1);
}
const totalFte = (row: ContractStaffInput) => Number(row.quantity || 0) * Number(row.ftePerPerson || 0);
const rowError = (index: number, field: string) => errors.value[`contractStaff.${index}.${field}`];
const ROW_FIELDS = ['roleCode', 'levelId', 'quantity', 'ftePerPerson', 'startDate', 'endDate', 'pricePerMonth'] as const;
const rowLabel = computed<Record<(typeof ROW_FIELDS)[number], string>>(() => ({
  roleCode: t('project_form.col_role'), levelId: t('project_form.col_level'), quantity: t('project_form.col_quantity'), ftePerPerson: t('project_form.col_fte_per_person'),
  startDate: t('project_form.lbl_start_date'), endDate: t('project_form.lbl_end_date'), pricePerMonth: t('project_form.col_unit_price'),
}));
function rowErrors(index: number) {
  return ROW_FIELDS.filter((f) => rowError(index, f)).map((f) => ({ label: rowLabel.value[f], message: rowError(index, f) }));
}

async function submit() {
  saving.value = true;
  errors.value = {};
  try {
    const input = { ...form.value, budget: form.value.budget === '' ? null : form.value.budget };
    const res = isEdit.value ? await projectDetailApi.update(projectId.value as string, input) : await projectDetailApi.create(input);
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['projects'] });
    void router.push(`/projects/${res.data.id}`);
  } catch (err) {
    errors.value = firstFieldErrors(err);
    if (Object.keys(errors.value).length === 0) toast.error((err as Error).message);
    else toast.error(t('project_form.msg_fix_errors'));
  } finally {
    saving.value = false;
  }
}

// Thêm khách hàng ngay trong form (cần quyền Quản trị → Cài đặt)
const clientOpen = ref(false);
const clientSaving = ref(false);
const clientErrors = ref<Record<string, string>>({});
async function createClient(input: ClientInput) {
  clientSaving.value = true;
  try {
    const res = await orgApi.createClient(input);
    toast.success(res.messageText);
    await refetchClients();
    form.value.clientId = res.data.id;
    clientOpen.value = false;
  } catch (err) {
    clientErrors.value = firstFieldErrors(err);
    if (Object.keys(clientErrors.value).length === 0) toast.error((err as Error).message);
  } finally {
    clientSaving.value = false;
  }
}
const CELL_INPUT = '!h-9 lg:!h-10 !border-slate-100 !bg-transparent hover:!border-pr';
</script>

<template>
  <PageLayout layout="C" content-padding="24px">
    <form novalidate class="flex min-h-0 flex-col gap-8" @submit.prevent="submit">
      <div class="flex items-center justify-between">
        <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">
          {{ isEdit ? $t('project_form.title_edit') : $t('project_form.title_create') }}
        </h1>
        <RouterLink :to="isEdit ? `/projects/${projectId}` : '/projects'" class="group flex items-center gap-2 text-sm font-semibold text-slate-600 hover:text-pr transition-colors">
          <ArrowLeft :size="18" class="transition-transform group-hover:-translate-x-1" />
          {{ isEdit ? $t('project_form.btn_back_detail') : $t('project_form.btn_back_list') }}
        </RouterLink>
      </div>

      <div v-if="isEdit && detail.isPending.value" class="py-16 text-center text-[13px] font-medium text-slate-400 animate-pulse">{{ $t('project_form.lbl_loading') }}</div>
      <template v-else>
        <div class="flex flex-col gap-6">
          <div class="grid grid-cols-1 gap-6 md:grid-cols-3">
            <FormItem :label="$t('project_form.lbl_name')" required :error="errors.name">
              <BaseInput v-model="form.name" :placeholder="$t('project_form.ph_name')" :error="!!errors.name" />
            </FormItem>
            <div class="flex flex-col gap-1">
              <FormSelect v-model="form.clientId" :label="$t('project_form.lbl_client')" :options="clientOptions" :placeholder="$t('project_form.ph_client')" :error="errors.clientId" required searchable />
              <button v-if="canAddClient" type="button" class="self-start text-[12px] font-bold text-pr hover:underline" @click="clientErrors = {}; clientOpen = true">+ {{ $t('project_form.btn_add_client') }}</button>
            </div>
            <FormSelect v-model="form.gradeId" :label="$t('project_form.lbl_grade')" :options="toOptions(grades)" :placeholder="$t('project_form.ph_grade')" :error="errors.gradeId" required searchable />
          </div>

          <FormItem :label="$t('project_form.lbl_description')" :error="errors.description">
            <BaseTextarea v-model="form.description" :rows="4" :placeholder="$t('project_form.ph_description')" :error="!!errors.description" />
          </FormItem>

          <div :class="['grid grid-cols-1 gap-6', isEdit ? 'md:grid-cols-4' : 'md:grid-cols-3']">
            <FormDatePicker v-model="form.startDate" :label="$t('project_form.lbl_start_date')" :error="errors.startDate" />
            <FormDatePicker v-model="form.endDate" :label="$t('project_form.lbl_end_date')" :error="errors.endDate" />
            <FormItem :label="$t('project_form.lbl_budget')" :error="errors.budget">
              <BaseInput v-model="form.budget" type="number" min="0" placeholder="0" :error="!!errors.budget" />
            </FormItem>
            <FormSelect v-if="isEdit" v-model="form.statusId" :label="$t('project_form.lbl_status')" :options="toOptions(statuses)" :placeholder="$t('project_form.ph_status')" :error="errors.statusId" required />
          </div>

          <div class="grid grid-cols-1 gap-6 md:grid-cols-2">
            <FormItem :label="$t('project_form.lbl_ticket_url')" :error="errors.ticketSystemUrl">
              <BaseInput v-model="form.ticketSystemUrl" placeholder="https://jira.example.com/..." :error="!!errors.ticketSystemUrl" />
            </FormItem>
            <FormItem :label="$t('project_form.lbl_source_url')" :error="errors.sourceCodeUrl">
              <BaseInput v-model="form.sourceCodeUrl" placeholder="https://github.com/..." :error="!!errors.sourceCodeUrl" />
            </FormItem>
          </div>
        </div>

        <section class="flex flex-col gap-4">
          <h2 class="text-[16px] font-black tracking-tight text-slate-800">{{ $t('project_form.title_contract_staff') }}</h2>
          <div class="overflow-x-auto rounded-xl border border-slate-200">
            <table class="w-full border-collapse text-left">
              <thead>
                <tr class="border-b border-slate-200 bg-slate-50/80">
                  <th class="p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_form.col_role') }}</th>
                  <th class="p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_form.col_level') }}</th>
                  <th class="p-3 text-center text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_form.col_quantity') }}</th>
                  <th class="p-3 text-center text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_form.col_fte_per_person') }}</th>
                  <th class="p-3 text-center text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_form.col_total_fte') }}</th>
                  <th class="p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_form.col_period') }}</th>
                  <th class="p-3 text-right text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('project_form.col_unit_price') }}</th>
                  <th class="p-3 w-12" />
                </tr>
              </thead>
              <tbody>
                <tr v-if="form.contractStaff.length === 0">
                  <td colspan="8" class="p-6 text-center text-[12.5px] font-semibold text-slate-400">{{ $t('project_form.empty_contract_staff') }}</td>
                </tr>
                <template v-for="(row, index) in form.contractStaff" :key="index">
                  <tr :class="['border-b border-slate-100', rowErrors(index).length > 0 && 'bg-red-50/30']">
                    <td class="p-2 min-w-[150px]">
                      <FormSelect v-model="row.roleCode" :options="roleOptions" :placeholder="$t('project_form.ph_role')" :trigger-class="CELL_INPUT" :error="rowError(index, 'roleCode')" hide-message searchable />
                    </td>
                    <td class="p-2 min-w-[120px]">
                      <FormSelect v-model="row.levelId" :options="toOptions(levels)" :placeholder="$t('project_form.col_level')" :trigger-class="CELL_INPUT" :error="rowError(index, 'levelId')" hide-message searchable />
                    </td>
                    <td class="p-2"><div class="w-16 mx-auto"><BaseInput v-model="row.quantity" type="number" min="1" :class="`${CELL_INPUT} text-center font-bold`" :error="!!rowError(index, 'quantity')" /></div></td>
                    <td class="p-2"><div class="w-20 mx-auto"><BaseInput v-model="row.ftePerPerson" type="number" step="0.25" min="0.25" max="2" placeholder="1.0" :class="`${CELL_INPUT} text-center font-bold text-pr`" :error="!!rowError(index, 'ftePerPerson')" /></div></td>
                    <td class="p-2">
                      <div class="w-16 mx-auto">
                        <div :class="['text-center font-black text-[13px] py-1.5 px-2 rounded-lg', totalFte(row) > 0 ? 'bg-pr/10 text-pr' : 'bg-slate-50 text-slate-400']">{{ totalFte(row) > 0 ? totalFte(row).toFixed(2) : '—' }}</div>
                      </div>
                    </td>
                    <td class="p-2">
                      <div class="flex items-center gap-1.5 min-w-[260px]">
                        <FormDatePicker v-model="row.startDate" :class="CELL_INPUT" :error="rowError(index, 'startDate')" hide-message />
                        <span class="text-slate-400 shrink-0">→</span>
                        <FormDatePicker v-model="row.endDate" :class="CELL_INPUT" :error="rowError(index, 'endDate')" hide-message />
                      </div>
                    </td>
                    <td class="p-2"><div class="w-28 ml-auto"><BaseInput v-model="row.pricePerMonth" type="number" min="0" placeholder="0" :class="`${CELL_INPUT} text-right font-black text-emerald-600`" :error="!!rowError(index, 'pricePerMonth')" /></div></td>
                    <td class="p-2">
                      <button type="button" class="flex h-8 w-8 items-center justify-center rounded-lg text-slate-300 hover:bg-red-50 hover:text-red-500 transition-all" :aria-label="$t('project_form.btn_remove_row')" @click="removeRow(index)"><Trash2 :size="16" /></button>
                    </td>
                  </tr>
                  <tr v-if="rowErrors(index).length > 0" class="bg-red-50/30">
                    <td colspan="8" class="px-4 pb-4">
                      <div v-for="e in rowErrors(index)" :key="e.label" class="flex items-center gap-2.5 text-[12px] font-bold text-red-600">
                        <div class="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-red-50 text-red-500 shadow-sm"><AlertCircle :size="13" /></div>
                        <span class="tracking-tight"><span class="opacity-60 uppercase text-[10px] mr-1">{{ e.label }}:</span>{{ e.message }}</span>
                      </div>
                    </td>
                  </tr>
                </template>
              </tbody>
            </table>
          </div>
          <div class="flex">
            <Button variant="outline" class="h-10 rounded-xl border-dashed border-2 border-slate-200 bg-slate-50/50 px-6 text-[14px] font-black text-slate-500 hover:border-pr hover:bg-pr/5 hover:text-pr transition-all" @click="addRow">
              <template #leftIcon><Plus :size="16" /></template>
              {{ $t('project_form.btn_add_role') }}
            </Button>
          </div>
          <p v-if="errors.contractStaff" class="text-[12px] font-bold text-red-500 flex items-center gap-1.5 mt-1 bg-red-50/50 p-3 rounded-xl border border-red-100">
            <AlertCircle :size="14" />{{ errors.contractStaff }}
          </p>
        </section>

        <div v-if="!isEdit" class="flex items-start gap-3 rounded-2xl bg-amber-50/70 border border-amber-200/60 p-4 shadow-sm">
          <Info :size="18" class="text-amber-600 mt-0.5 shrink-0" />
          <div class="flex flex-col gap-0.5">
            <span class="text-[13px] font-bold text-amber-900">{{ $t('project_form.lbl_note_title') }}</span>
            <p class="text-xs text-amber-700 leading-relaxed">{{ $t('project_form.lbl_note_desc') }}</p>
          </div>
        </div>

        <div class="sticky bottom-[-24px] mx-[-24px] mb-[-24px] mt-2 z-10 flex items-center justify-end gap-3 bg-white/80 px-6 py-4 shadow-[0_-8px_16px_-6px_rgba(0,0,0,0.05)] backdrop-blur-xl">
          <Button variant="danger" class="min-w-[120px] shadow-sm" @click="router.push(isEdit ? `/projects/${projectId}` : '/projects')">
            <template #leftIcon><X :size="16" /></template>
            {{ $t('common.btn_cancel') }}
          </Button>
          <Button type="submit" variant="primary" class="min-w-[160px] shadow-lg shadow-pr/25 px-6" :loading="saving">
            <template #leftIcon><Check :size="16" /></template>
            {{ isEdit ? $t('project_form.btn_save') : $t('project_form.btn_create') }}
          </Button>
        </div>
      </template>
    </form>

    <ClientDialog v-model:open="clientOpen" :client="null" :loading="clientSaving" :server-errors="clientErrors" @submit="createClient" />
  </PageLayout>
</template>
