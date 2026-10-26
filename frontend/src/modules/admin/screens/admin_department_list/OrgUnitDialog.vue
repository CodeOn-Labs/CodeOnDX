<script setup lang="ts">
// Dialog thêm/sửa Phòng ban hoặc Team (cùng bộ trường: mã, tên, mô tả, người phụ trách, trạng thái).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { Check, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import { employeeOptions, orgApi, type Department, type Team } from '@/modules/org/api/org.api';
import { useFormMutation } from '@/shared/composables/useFormMutation';
import type { ErrorFullModel } from '@/api/types';

const props = defineProps<{
  kind: 'department' | 'team';
  department: Department | null;
  team: Team | null;
  parentId: string | null;
  departments: Department[];
}>();
const open = defineModel<boolean>('open', { required: true });
const { t } = useI18n();

const people = useQuery({ queryKey: ['employees', 'options'], queryFn: ({ signal }) => employeeOptions(signal), enabled: open });
const peopleOptions = computed(() => (people.data.value ?? []).map((e) => ({ label: e.fullName, value: e.id, description: e.email })));
const departmentOptions = computed(() => props.departments.map((d) => ({ label: d.name, value: d.id })));

const form = ref({ code: '', name: '', description: '', personId: '', departmentId: '', isActive: true });
const errors = ref<Record<string, string>>({});
const isEdit = computed(() => (props.kind === 'department' ? props.department : props.team) !== null);

watch(open, (isOpen) => {
  if (!isOpen) return;
  errors.value = {};
  if (props.kind === 'department') {
    const d = props.department;
    form.value = { code: d?.code ?? '', name: d?.name ?? '', description: d?.description ?? '', personId: d?.manager?.id ?? '', departmentId: '', isActive: d?.isActive ?? true };
  } else {
    const tm = props.team;
    form.value = { code: tm?.code ?? '', name: tm?.name ?? '', description: tm?.description ?? '', personId: tm?.leader?.id ?? '', departmentId: tm?.departmentId ?? props.parentId ?? '', isActive: tm?.isActive ?? true };
  }
});

const save = useFormMutation<void, unknown>({
  mutationFn: (): Promise<ErrorFullModel<unknown>> => {
    const f = form.value;
    if (props.kind === 'department') {
      const input = { code: f.code, name: f.name, description: f.description, managerId: f.personId, isActive: f.isActive };
      return props.department ? orgApi.updateDepartment(props.department.id, input) : orgApi.createDepartment(input);
    }
    const input = { departmentId: f.departmentId, code: f.code, name: f.name, description: f.description, leaderId: f.personId, isActive: f.isActive };
    return props.team ? orgApi.updateTeam(props.team.id, input) : orgApi.createTeam(input);
  },
  invalidate: [['departments'], ['employees']],
  onSuccess: () => (open.value = false),
});
watch(save.fieldErrors, (v) => {
  errors.value = { ...v, personId: v.managerId ?? v.leaderId ?? '' };
});

function submit() {
  const e: Record<string, string> = {};
  if (!form.value.code.trim()) e.code = t('validation.required');
  if (!form.value.name.trim()) e.name = t('validation.required');
  if (props.kind === 'team' && !form.value.departmentId) e.departmentId = t('validation.required');
  errors.value = e;
  if (Object.keys(e).length === 0) save.mutate(undefined);
}

const title = computed(() => {
  if (props.kind === 'department') return isEdit.value ? t('admin_department_list.title_edit_department_dialog') : t('admin_department_list.title_create_department_dialog');
  return isEdit.value ? t('admin_department_list.title_edit_team_dialog') : t('admin_department_list.title_create_team_dialog');
});
</script>

<template>
  <BaseDialog v-model:open="open" :title="title" max-width="lg">
    <form class="flex flex-col gap-5" @submit.prevent="submit">
      <FormSelect
        v-if="props.kind === 'team'"
        v-model="form.departmentId"
        :label="$t('admin_department_list.lbl_department')"
        :options="departmentOptions"
        :error="errors.departmentId"
        :allow-clear="false"
        required
      />
      <div class="grid grid-cols-3 gap-4">
        <FormItem :label="$t('admin_department_list.lbl_code')" required :error="errors.code">
          <BaseInput v-model="form.code" class="uppercase" :error="!!errors.code" />
        </FormItem>
        <FormItem class="col-span-2" :label="$t('admin_department_list.lbl_name')" required :error="errors.name">
          <BaseInput v-model="form.name" :error="!!errors.name" />
        </FormItem>
      </div>
      <FormSelect
        v-model="form.personId"
        :label="props.kind === 'department' ? $t('admin_department_list.lbl_manager') : $t('admin_department_list.lbl_leader')"
        :placeholder="$t('admin_department_list.ph_select_person')"
        :options="peopleOptions"
        :error="errors.personId"
        searchable
      />
      <FormItem :label="$t('admin_department_list.lbl_description')" :error="errors.description">
        <BaseTextarea v-model="form.description" :rows="3" />
      </FormItem>
      <div class="flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50 p-3">
        <span class="text-[12px] font-bold text-slate-700">{{ $t('admin_department_list.lbl_active') }}</span>
        <SwitchToggle v-model="form.isActive" size="sm" :label="$t('admin_department_list.lbl_active')" />
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
