<script setup lang="ts">
// Port thanh chọn nhân viên của TimesheetDay/WeekPage: nhãn + chọn nhiều nhân viên (TimesheetGroupSelector) + chọn một nhân viên.
import { computed, ref, useId } from 'vue';
import { useI18n } from 'vue-i18n';
import { PopoverContent, PopoverPortal, PopoverRoot, PopoverTrigger } from 'reka-ui';
import { Users, UsersRound, X } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import type { TimesheetPeople } from '@/modules/work/api/work.api';
import { canApplyGroupSelection, type GroupMode, type GroupSelection } from '@/modules/work/utils/timesheet-rules';

const props = defineProps<{ people: TimesheetPeople; employeeId: string | null; group: GroupSelection | null; dirty?: boolean }>();
const emit = defineEmits<{ changeEmployee: [id: string]; applyGroup: [g: GroupSelection]; clearGroup: [] }>();
const { t } = useI18n();
const radioName = useId();

const employeeOptions = computed(() =>
  props.people.employees.map((e) => ({
    value: e.id,
    label: `${e.fullName} (${e.code})${e.id === props.people.myEmployeeId ? t('timesheet.lbl_myself_suffix') : ''}`,
    description: e.email,
  })),
);
const current = computed(() => props.employeeId ?? props.people.myEmployeeId);

const open = ref(false);
const mode = ref<GroupMode>('all');
const multiIds = ref<string[]>([]);
const projectId = ref<string | null>(null);
const projectOptions = computed(() => props.people.projects.map((p) => ({ value: p.id, label: p.name, description: t('timesheet.lbl_group_member_count', { count: p.employeeIds.length }) })));
const canApply = computed(() => canApplyGroupSelection(mode.value, multiIds.value, projectId.value));
function reset() {
  mode.value = 'all';
  multiIds.value = [];
  projectId.value = null;
}
function apply() {
  let ids: string[];
  let label: string;
  if (mode.value === 'multi') {
    ids = multiIds.value;
    label = t('timesheet.opt_group_multi');
  } else if (mode.value === 'project') {
    const p = props.people.projects.find((x) => x.id === projectId.value);
    ids = p?.employeeIds ?? [];
    label = p?.name ?? t('timesheet.opt_group_project');
  } else {
    ids = props.people.employees.map((e) => e.id);
    label = t('timesheet.opt_group_all');
  }
  emit('applyGroup', { mode: mode.value, employeeIds: [...ids].sort(), label, ...(mode.value === 'project' && projectId.value ? { projectId: projectId.value } : {}) });
  open.value = false;
  reset();
}
const MODES: GroupMode[] = ['all', 'multi', 'project'];
const modeLabel = (m: GroupMode) => ({ all: t('timesheet.opt_group_all'), multi: t('timesheet.opt_group_multi'), project: t('timesheet.opt_group_project') })[m];
</script>

<template>
  <div class="mt-4 flex flex-col gap-3 rounded-[16px] border border-[#E4E8F3] bg-white/90 px-4 py-3 shadow-sm md:flex-row md:items-center md:gap-3">
    <div class="flex items-center gap-3 text-[13px] font-bold text-[#4C5568]">
      <span class="flex h-8 w-8 items-center justify-center rounded-full bg-[#F3F5FB] text-[#7C6AF4]"><Users class="h-4 w-4" /></span>
      <span>{{ $t('timesheet.lbl_staff') }}</span>
    </div>
    <div class="flex flex-wrap items-center gap-2 md:ml-auto">
      <div v-if="props.group" class="flex h-8 items-center gap-2 rounded-lg border border-pr/30 bg-pr/5 px-3 text-[12px] font-semibold text-pr">
        <UsersRound :size="14" />
        <span class="whitespace-nowrap">{{ $t('timesheet.lbl_group_selected', { label: props.group.label, count: props.group.employeeIds.length }) }}</span>
        <button type="button" class="rounded p-0.5 hover:bg-pr/10" :aria-label="$t('timesheet.btn_group_clear')" @click="emit('clearGroup')"><X :size="13" /></button>
      </div>
      <PopoverRoot v-else v-model:open="open" @update:open="!$event && reset()">
        <PopoverTrigger as-child>
          <button
            type="button"
            class="flex h-8 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-[12px] font-semibold text-slate-600 transition-all hover:border-pr hover:bg-pr/5 hover:text-pr active:scale-95"
          >
            <UsersRound :size="14" />
            <span class="whitespace-nowrap">{{ $t('timesheet.btn_group_view') }}</span>
          </button>
        </PopoverTrigger>
        <PopoverPortal>
          <PopoverContent align="start" :side-offset="8" class="z-[100] w-[320px] overflow-hidden rounded-2xl border border-slate-200/60 bg-white p-0 shadow-[0_20px_50px_rgba(0,0,0,0.15)] outline-none animate-in fade-in zoom-in-95 duration-200">
            <div class="border-b border-slate-100 px-4 py-3">
              <h3 class="text-[13px] font-black text-slate-900">{{ $t('timesheet.btn_group_view') }}</h3>
            </div>
            <div class="max-h-[460px] overflow-y-auto px-4 py-4 flex flex-col gap-3">
              <div v-for="m in MODES" :key="m" class="flex flex-col gap-2">
                <label class="flex cursor-pointer items-center gap-2 text-[12.5px] font-semibold text-slate-700">
                  <input v-model="mode" type="radio" :name="radioName" :value="m" class="h-3.5 w-3.5 accent-pr" />
                  {{ modeLabel(m) }}
                </label>
                <div v-if="mode === m" class="pl-6">
                  <span v-if="m === 'all'" class="text-[11px] font-semibold text-slate-500">{{ $t('timesheet.lbl_group_member_count', { count: props.people.employees.length }) }}</span>
                  <FormSelect v-else-if="m === 'multi'" v-model="multiIds" :options="employeeOptions" :placeholder="$t('timesheet.ph_select_employees')" multiple searchable hide-message trigger-class="h-9 text-[12px]" />
                  <FormSelect v-else v-model="projectId" :options="projectOptions" :placeholder="$t('timesheet.ph_select_project')" searchable hide-message trigger-class="h-9 text-[12px]" />
                </div>
              </div>
            </div>
            <div class="flex items-center gap-2 border-t border-slate-100 px-4 py-3">
              <Button variant="danger" size="sm" class="flex-1" @click="open = false">{{ $t('common.btn_cancel') }}</Button>
              <Button size="sm" class="flex-1" :disabled="!canApply" @click="apply">{{ $t('timesheet.btn_group_apply') }}</Button>
            </div>
          </PopoverContent>
        </PopoverPortal>
      </PopoverRoot>
      <div class="md:w-[360px]">
        <FormSelect
          :model-value="current"
          :options="employeeOptions"
          :placeholder="$t('timesheet.ph_select_employee')"
          :allow-clear="false"
          searchable
          hide-message
          trigger-class="h-9 text-[12.5px]"
          @update:model-value="typeof $event === 'string' && $event && emit('changeEmployee', $event)"
        />
      </div>
    </div>
  </div>
</template>
