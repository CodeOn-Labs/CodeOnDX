<script setup lang="ts">
// Port AddMemberPopover.tsx: thêm thành viên vào phòng ban của lưới — chọn Team, Dự án, Vai trò, Nhân sự; chặn trùng (người × dự án × vai trò).
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { PopoverContent, PopoverPortal, PopoverRoot, PopoverTrigger } from 'reka-ui';
import { Plus, UserPlus, X } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import { effortApi, type EffortRow } from '@/modules/effort/api/effort.api';
import { workApi } from '@/modules/work/api/work.api';
import { useToastStore } from '@/stores/toast';

const props = defineProps<{
  month: string;
  department: { id: string; name: string };
  teamOptions: Array<{ value: string; label: string }>;
  projectOptions: Array<{ value: string; label: string }>;
  roleOptions: Array<{ value: string; label: string }>;
  rows: EffortRow[];
}>();
const { t } = useI18n();
const toast = useToastStore();
const queryClient = useQueryClient();

const open = ref(false);
const form = ref({ teamId: '', projectId: '', role: '', employeeId: '' });
const error = ref('');
const busy = ref(false);
// Danh sách nhân sự lấy từ API Nhật ký công việc (không cần quyền employee.view)
const people = useQuery({ queryKey: ['timesheets', 'people'], queryFn: workApi.timesheetPeople, enabled: open, staleTime: 5 * 60_000 });
const memberOptions = computed(() => (people.data.value?.employees ?? []).map((e) => ({ value: e.id, label: e.fullName, description: e.code })));
function reset() {
  form.value = { teamId: '', projectId: '', role: '', employeeId: '' };
  error.value = '';
}
async function submit() {
  error.value = '';
  const f = form.value;
  if (!f.teamId || !f.projectId || !f.role || !f.employeeId) {
    error.value = t('effort.err_add_required');
    return;
  }
  if (props.rows.some((r) => r.employee?.id === f.employeeId && r.projectId === f.projectId && r.role === f.role)) {
    error.value = t('effort.err_duplicate');
    return;
  }
  busy.value = true;
  try {
    const res = await effortApi.add({ month: props.month, departmentId: props.department.id, ...f });
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['effort'] });
    open.value = false;
    reset();
  } catch (err) {
    error.value = (err as Error).message;
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <PopoverRoot v-model:open="open" @update:open="!$event && reset()">
    <PopoverTrigger as-child>
      <button type="button" class="text-[12px] font-medium text-pr transition-colors hover:text-pr/80">+ {{ $t('effort.btn_add_member') }}</button>
    </PopoverTrigger>
    <PopoverPortal>
      <PopoverContent align="start" :side-offset="8" class="z-[100] w-[340px] rounded-2xl border border-slate-200/60 bg-white/95 backdrop-blur-xl shadow-[0_20px_50px_rgba(0,0,0,0.15)] outline-none animate-in fade-in zoom-in-95 duration-200">
        <div class="flex items-center justify-between rounded-t-2xl border-b border-slate-100 bg-slate-50/50 px-4 py-3">
          <div class="flex items-center gap-2">
            <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-pr/10 text-pr"><UserPlus :size="14" /></div>
            <h3 class="text-[13px] font-black text-slate-900">{{ $t('effort.title_add_member') }}</h3>
          </div>
          <button type="button" class="text-slate-400 hover:text-slate-600" :aria-label="$t('common.btn_close')" @click="open = false"><X :size="16" /></button>
        </div>
        <div class="px-4 py-4 space-y-4">
          <FormSelect :model-value="props.department.id" :label="$t('effort.lbl_department')" :options="[{ value: props.department.id, label: props.department.name }]" :allow-clear="false" disabled hide-message />
          <FormSelect v-model="form.teamId" :label="$t('effort.col_team')" :options="props.teamOptions" :placeholder="$t('effort.ph_select_team')" required searchable hide-message />
          <FormSelect v-model="form.projectId" :label="$t('effort.col_project')" :options="props.projectOptions" :placeholder="$t('effort.ph_select_project')" required searchable hide-message />
          <FormSelect v-model="form.role" :label="$t('effort.col_role')" :options="props.roleOptions" :placeholder="$t('effort.ph_select_role')" required hide-message />
          <FormSelect v-model="form.employeeId" :label="$t('effort.lbl_member')" :options="memberOptions" :placeholder="$t('effort.ph_select_member')" required searchable hide-message />
        </div>
        <p v-if="error" class="px-4 pb-3 text-[12px] font-medium text-rose-600">{{ error }}</p>
        <div class="flex items-center gap-2 rounded-b-2xl border-t border-slate-100 bg-slate-50/50 px-4 py-3">
          <Button variant="danger" size="sm" class="flex-1 rounded-xl text-[12px] font-bold h-8" @click="open = false">
            <template #leftIcon><X :size="14" /></template>{{ $t('common.btn_cancel') }}
          </Button>
          <Button variant="primary" size="sm" class="flex-1 rounded-xl text-[12px] font-bold h-8" :loading="busy" :disabled="!form.teamId || !form.projectId || !form.role || !form.employeeId" @click="submit">
            <template #leftIcon><Plus :size="14" /></template>{{ $t('effort.btn_add') }}
          </Button>
        </div>
      </PopoverContent>
    </PopoverPortal>
  </PopoverRoot>
</template>
