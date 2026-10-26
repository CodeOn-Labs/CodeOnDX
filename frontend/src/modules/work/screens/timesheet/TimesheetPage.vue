<script setup lang="ts">
// Port TimesheetContainer.tsx — Nhật ký công việc: chuyển Ngày ⇄ Tuần (nhớ trong sessionStorage), dùng chung
// nhân viên đang xem và lựa chọn "xem nhiều nhân viên" (chế độ nhóm = màn duyệt timesheet_approval khi có quyền duyệt).
import { ref, watch } from 'vue';
import { useQuery } from '@tanstack/vue-query';
import TimesheetDayView from './TimesheetDayView.vue';
import TimesheetWeekView from './TimesheetWeekView.vue';
import { workApi } from '@/modules/work/api/work.api';
import type { GroupSelection } from '@/modules/work/utils/timesheet-rules';

const MODE_KEY = 'timesheetViewMode';
const EMPLOYEE_KEY = 'timesheetEmployeeId';
const GROUP_KEY = 'timesheetGroupSelection';

function readSession<T>(key: string, parse: (raw: string) => T, fallback: T): T {
  try {
    const raw = sessionStorage.getItem(key);
    return raw === null ? fallback : parse(raw);
  } catch {
    return fallback;
  }
}
function writeSession(key: string, value: string | null) {
  try {
    if (value === null) sessionStorage.removeItem(key);
    else sessionStorage.setItem(key, value);
  } catch {
    // bỏ qua
  }
}

const mode = ref<'day' | 'week'>(readSession(MODE_KEY, (r) => (r === 'day' ? 'day' : 'week'), 'week'));
const employeeId = ref<string | null>(readSession(EMPLOYEE_KEY, (r) => r || null, null));
const group = ref<GroupSelection | null>(readSession(GROUP_KEY, (r) => JSON.parse(r) as GroupSelection, null));

watch(mode, (v) => writeSession(MODE_KEY, v));
watch(employeeId, (v) => writeSession(EMPLOYEE_KEY, v));
watch(group, (v) => writeSession(GROUP_KEY, v ? JSON.stringify(v) : null), { deep: true });

const people = useQuery({ queryKey: ['timesheets', 'people'], queryFn: workApi.timesheetPeople, staleTime: 5 * 60_000 });
// Nhân viên đã chọn không còn trong danh sách được xem → về chính mình
watch(
  () => people.data.value,
  (p) => {
    if (p && employeeId.value && !p.employees.some((e) => e.id === employeeId.value)) employeeId.value = null;
  },
);
</script>

<template>
  <div class="relative h-full min-h-0 overflow-hidden bg-slate-50/50">
    <div
      :class="[
        'absolute inset-0 transition-all duration-500 [transition-timing-function:cubic-bezier(0.4,0,0.2,1)]',
        mode === 'day' ? 'translate-y-0 opacity-100 scale-100 blur-0' : 'pointer-events-none translate-y-4 opacity-0 scale-95 blur-sm',
      ]"
      :aria-hidden="mode !== 'day'"
    >
      <TimesheetDayView
        v-if="mode === 'day'"
        v-model:employee-id="employeeId"
        v-model:group="group"
        :people="people.data.value ?? null"
        @switch="mode = 'week'"
      />
    </div>
    <div
      :class="[
        'absolute inset-0 transition-all duration-500 [transition-timing-function:cubic-bezier(0.4,0,0.2,1)]',
        mode === 'week' ? 'translate-y-0 opacity-100 scale-100 blur-0' : 'pointer-events-none translate-y-4 opacity-0 scale-95 blur-sm',
      ]"
      :aria-hidden="mode !== 'week'"
    >
      <TimesheetWeekView
        v-if="mode === 'week'"
        v-model:employee-id="employeeId"
        v-model:group="group"
        :people="people.data.value ?? null"
        @switch="mode = 'day'"
      />
    </div>
  </div>
</template>
