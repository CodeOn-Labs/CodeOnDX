<script setup lang="ts">
// Thêm / sửa hồ sơ nhân sự (D4 — nhân sự tự quản lý, thay dữ liệu đồng bộ từ HRM của bản gốc).
// Dùng ở danh sách nhân sự và trang chi tiết.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { Briefcase, Check, IdCard, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import { orgApi, type Employee, type EmployeeInput } from '@/modules/org/api/org.api';
import { useMasterData } from '@/shared/composables/useMasterData';
import { useFormMutation } from '@/shared/composables/useFormMutation';

const props = defineProps<{ employee: Employee | null }>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ saved: [employee: Employee] }>();
const { t } = useI18n();

const { items: positions } = useMasterData('job_position');
const { items: levels } = useMasterData('hr_level');
const { items: statuses } = useMasterData('employee_status');
const departments = useQuery({ queryKey: ['departments'], queryFn: orgApi.departments, enabled: open });

const empty = (): EmployeeInput => ({
  code: '', fullName: '', email: '', phone: '', positionCode: '', levelId: '', departmentId: '', teamId: '', statusCode: 'PROBATION', joinedDate: null, leftDate: null, skills: [],
});
const form = ref<EmployeeInput>(empty());
const skillsText = ref('');
const errors = ref<Record<string, string>>({});

watch(open, (isOpen) => {
  if (!isOpen) return;
  errors.value = {};
  const e = props.employee;
  form.value = e
    ? {
        code: e.code, fullName: e.fullName, email: e.email, phone: e.phone ?? '', positionCode: e.position?.code ?? '', levelId: e.level?.id ?? '',
        departmentId: e.department?.id ?? '', teamId: e.team?.id ?? '', statusCode: e.employmentStatus?.code ?? 'ACTIVE', joinedDate: e.joinedDate, leftDate: e.leftDate, skills: [...e.skills],
      }
    : empty();
  skillsText.value = form.value.skills.join(', ');
});

const positionOptions = computed(() => positions.value.map((p) => ({ label: p.name, value: p.code })));
const levelOptions = computed(() => levels.value.map((l) => ({ label: l.name, value: l.id })));
const statusOptions = computed(() => statuses.value.map((s) => ({ label: s.name, value: s.code })));
const departmentOptions = computed(() => (departments.data.value ?? []).map((d) => ({ label: d.name, value: d.id })));
const teamOptions = computed(() => (departments.data.value ?? []).find((d) => d.id === form.value.departmentId)?.teams.map((tm) => ({ label: tm.name, value: tm.id })) ?? []);
watch(() => form.value.departmentId, () => {
  if (form.value.teamId && !teamOptions.value.some((o) => o.value === form.value.teamId)) form.value.teamId = '';
});

const save = useFormMutation({
  mutationFn: (input: EmployeeInput) => (props.employee ? orgApi.updateEmployee(props.employee.id, input) : orgApi.createEmployee(input)),
  invalidate: [['employees'], ['departments']],
  onSuccess: (data) => {
    open.value = false;
    emit('saved', data);
  },
});
watch(save.fieldErrors, (v) => (errors.value = { ...v }));

function submit() {
  const e: Record<string, string> = {};
  const f = form.value;
  if (!f.code.trim()) e.code = t('validation.required');
  if (!f.fullName.trim()) e.fullName = t('validation.required');
  if (!f.email.trim()) e.email = t('validation.required');
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email)) e.email = t('validation.invalid_email');
  if (!f.positionCode) e.positionCode = t('validation.required');
  if (!f.joinedDate) e.joinedDate = t('validation.required');
  errors.value = e;
  if (Object.keys(e).length > 0) return;
  save.mutate({ ...f, skills: skillsText.value.split(',').map((s) => s.trim()).filter(Boolean) });
}
const SECTION = 'bg-slate-50/50 p-5 rounded-[24px] border border-slate-200/60';
</script>

<template>
  <BaseDialog v-model:open="open" :title="props.employee ? $t('component.employee_form_dialog.title_edit') : $t('component.employee_form_dialog.title_create')" max-width="2xl">
    <form class="flex flex-col gap-6" @submit.prevent="submit">
      <div class="space-y-4">
        <div class="flex items-center gap-2 px-1">
          <div class="p-1.5 rounded-lg bg-blue-50 text-blue-600"><IdCard :size="16" /></div>
          <h3 class="text-[14px] font-bold text-slate-900 uppercase tracking-wider">{{ $t('component.employee_form_dialog.lbl_section_profile') }}</h3>
        </div>
        <div :class="[SECTION, 'grid grid-cols-2 gap-5']">
          <FormItem :label="$t('component.employee_form_dialog.lbl_code')" required :error="errors.code">
            <BaseInput v-model="form.code" class="uppercase" :placeholder="$t('component.employee_form_dialog.ph_code')" :error="!!errors.code" />
          </FormItem>
          <FormItem :label="$t('component.employee_form_dialog.lbl_full_name')" required :error="errors.fullName">
            <BaseInput v-model="form.fullName" :error="!!errors.fullName" />
          </FormItem>
          <FormItem :label="$t('component.employee_form_dialog.lbl_email')" required :error="errors.email">
            <BaseInput v-model="form.email" type="email" :error="!!errors.email" />
          </FormItem>
          <FormItem :label="$t('component.employee_form_dialog.lbl_phone')" :error="errors.phone">
            <BaseInput v-model="form.phone" :error="!!errors.phone" />
          </FormItem>
        </div>
      </div>

      <div class="space-y-4">
        <div class="flex items-center gap-2 px-1">
          <div class="p-1.5 rounded-lg bg-purple-50 text-purple-600"><Briefcase :size="16" /></div>
          <h3 class="text-[14px] font-bold text-slate-900 uppercase tracking-wider">{{ $t('component.employee_form_dialog.lbl_section_work') }}</h3>
        </div>
        <div :class="[SECTION, 'grid grid-cols-2 gap-5']">
          <FormSelect v-model="form.positionCode" :label="$t('component.employee_form_dialog.lbl_position')" :options="positionOptions" :error="errors.positionCode" required searchable />
          <FormSelect v-model="form.levelId" :label="$t('component.employee_form_dialog.lbl_level')" :options="levelOptions" :error="errors.levelId" />
          <FormSelect v-model="form.departmentId" :label="$t('component.employee_form_dialog.lbl_department')" :options="departmentOptions" :error="errors.departmentId" />
          <FormSelect v-model="form.teamId" :label="$t('component.employee_form_dialog.lbl_team')" :options="teamOptions" :error="errors.teamId" :disabled="!form.departmentId" />
          <FormSelect v-model="form.statusCode" :label="$t('component.employee_form_dialog.lbl_status')" :options="statusOptions" :error="errors.statusCode" :allow-clear="false" />
          <FormItem :label="$t('component.employee_form_dialog.lbl_skills')" :description="$t('component.employee_form_dialog.hint_skills')">
            <BaseInput v-model="skillsText" />
          </FormItem>
          <FormDatePicker v-model="form.joinedDate" :label="$t('component.employee_form_dialog.lbl_joined_date')" :error="errors.joinedDate" required />
          <FormDatePicker v-model="form.leftDate" :label="$t('component.employee_form_dialog.lbl_left_date')" :error="errors.leftDate" />
        </div>
      </div>
      <button type="submit" class="hidden" />
    </form>
    <template #footer>
      <Button variant="danger" class="min-w-[100px]" :disabled="save.isPending.value" @click="open = false">
        <template #leftIcon><X :size="16" /></template>
        {{ $t('common.btn_cancel') }}
      </Button>
      <Button variant="primary" class="min-w-[120px]" :loading="save.isPending.value" @click="submit">
        <template #leftIcon><Check :size="16" :stroke-width="3" /></template>
        {{ $t('common.btn_save') }}
      </Button>
    </template>
  </BaseDialog>
</template>
