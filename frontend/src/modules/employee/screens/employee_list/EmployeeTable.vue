<script setup lang="ts">
// Port EmployeeTable.tsx — giữ class các ô. Thay cột SSO bằng Mã nhân sự + trạng thái tài khoản; thêm cột thao tác (D4).
import { computed } from 'vue';
import { RouterLink } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { ArrowUpRight, MailPlus, Pencil, RotateCcw, Trash2 } from '@lucide/vue';
import CommonTable from '@/shared/components/table/CommonTable.vue';
import ColorBadge from '@/shared/components/ColorBadge.vue';
import UserAvatar from '@/shared/components/UserAvatar.vue';
import AllocationBadge from '@/modules/employee/components/AllocationBadge.vue';
import type { TableColumn } from '@/shared/components/table/types';
import type { PaginationController } from '@/shared/composables/usePagination';
import type { Employee } from '@/modules/org/api/org.api';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ employees: Employee[]; loading: boolean; pagination: PaginationController; canManage: boolean }>();
const emit = defineEmits<{ edit: [e: Employee]; delete: [e: Employee]; restore: [e: Employee]; invite: [e: Employee] }>();
const { t } = useI18n();

const columns = computed<TableColumn[]>(() => {
  const cols: TableColumn[] = [
    { id: 'name', header: t('employee_list.col_employee') },
    { id: 'code', header: t('employee_list.col_code') },
    { id: 'position', header: t('employee_list.col_position') },
    { id: 'level', header: t('employee_list.col_level'), headerClass: 'text-center', cellClass: 'text-center' },
    { id: 'department', header: t('employee_list.col_department') },
    { id: 'team', header: t('employee_list.col_team') },
    { id: 'projects', header: t('employee_list.col_projects') },
    { id: 'fte', header: t('employee_list.col_fte'), headerClass: 'w-[110px]' },
    { id: 'status', header: t('employee_list.col_status'), headerClass: 'text-center', cellClass: 'text-center' },
  ];
  if (props.canManage) cols.push({ id: 'actions', header: '', cellClass: 'text-right' });
  return cols;
});

function rowClass(e: Employee) {
  if (e.isDeleted) return 'cursor-default transition-all duration-300 ease-in-out opacity-60 hover:bg-slate-50/40 hover:!border-rose-300';
  if (e.allocationStatus === 'resigned') return 'cursor-default transition-all duration-300 ease-in-out opacity-75 hover:bg-slate-50/40 hover:!border-slate-300';
  return cn(
    'cursor-default transition-all duration-300 ease-in-out',
    e.allocationStatus === 'overloaded'
      ? 'bg-red-50/20 hover:bg-red-50/40 hover:!border-red-500'
      : e.allocationStatus === 'bench'
        ? 'bg-amber-50/20 hover:bg-amber-50/40 hover:!border-amber-500'
        : 'hover:!border-pr hover:bg-slate-50/40',
  );
}
const ACTION = 'p-1.5 rounded-lg text-slate-400 transition-all hover:scale-110 active:scale-95';
</script>

<template>
  <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
    <CommonTable
      :columns="columns"
      :rows="props.employees"
      :loading="props.loading"
      :empty-message="$t('employee_list.empty_no_employee')"
      :pagination="props.pagination"
      table-class="min-w-[1100px]"
      :row-class="rowClass"
      container-class="flex-1 min-h-0"
      table-wrapper-class="border-none shadow-none bg-transparent"
    >
      <template #cell-name="{ row }">
        <div class="flex items-center gap-3 py-1">
          <UserAvatar :name="row.fullName" :id="row.id" :src="row.avatarUrl" :size="40" class="text-[13px]" />
          <div class="flex flex-col gap-0.5">
            <RouterLink
              :to="`/admin/employees/${row.id}`"
              class="group/link inline-flex items-center gap-1.5 text-[13.5px] font-extrabold text-slate-900 transition-all duration-300 hover:text-pr outline-none focus-visible:ring-2 focus-visible:ring-pr/30 focus-visible:ring-offset-1 rounded-md px-1 -mx-1"
            >
              <span class="relative">
                {{ row.fullName }}
                <span class="absolute -bottom-0.5 left-0 h-[2px] w-0 bg-pr transition-all duration-500 group-hover/link:w-full" />
              </span>
              <ArrowUpRight :size="14" class="opacity-0 -translate-x-2 translate-y-1 transition-all duration-300 group-hover/link:opacity-100 group-hover/link:translate-x-0 group-hover/link:translate-y-0 text-pr" />
            </RouterLink>
            <span class="text-[11px] font-medium text-slate-400">{{ row.email }}</span>
          </div>
        </div>
      </template>
      <template #cell-code="{ row }">
        <div class="flex flex-col gap-1">
          <span class="text-[12px] font-bold text-slate-500 tracking-tight">{{ row.code }}</span>
          <span v-if="!row.account" class="text-[9px] font-black uppercase tracking-widest text-slate-300">{{ $t('employee_list.lbl_no_account') }}</span>
          <span v-else-if="row.account.status === 'invited'" class="text-[9px] font-black uppercase tracking-widest text-amber-500">{{ $t('employee_list.lbl_invited') }}</span>
        </div>
      </template>
      <template #cell-position="{ row }"><span class="text-[12px] font-bold text-slate-700">{{ row.position?.name ?? '—' }}</span></template>
      <template #cell-level="{ row }">
        <ColorBadge v-if="row.level" :label="row.level.name" :color="row.level.color" class="rounded-md text-[10px] uppercase tracking-widest shadow-sm" />
        <span v-else class="text-slate-300">—</span>
      </template>
      <template #cell-department="{ row }">
        <span :class="cn('text-[11px] font-bold', row.department ? 'text-slate-500' : 'text-slate-300 italic')">{{ row.department?.name ?? '—' }}</span>
      </template>
      <template #cell-team="{ row }">
        <span :class="cn('text-[11px] font-bold', row.team ? 'text-slate-500' : 'text-slate-300 italic')">{{ row.team?.name ?? '—' }}</span>
      </template>
      <template #cell-projects="{ row }">
        <span v-if="row.projects.length === 0" class="text-[11px] text-slate-300 font-medium italic">—</span>
        <div v-else class="flex flex-wrap gap-1">
          <span v-for="p in row.projects.slice(0, 2)" :key="p.id" class="inline-flex rounded-full bg-violet-50 px-2 py-0.5 text-[10px] font-bold text-violet-700 ring-1 ring-inset ring-violet-500/20">{{ p.name }}</span>
          <span v-if="row.projects.length > 2" class="inline-flex rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-bold text-slate-500">+{{ row.projects.length - 2 }}</span>
        </div>
      </template>
      <template #cell-fte="{ row }">
        <div class="flex flex-col gap-0.5 tabular-nums">
          <span :class="cn('text-[13px] font-black', row.totalFte > 1 ? 'text-red-600' : 'text-slate-800')">{{ row.totalFte.toFixed(1) }} {{ $t('employee_list.unit_fte') }}</span>
          <span :class="cn('text-[11px] font-semibold', row.totalFte > 1 ? 'text-red-500' : 'text-slate-400')">{{ row.allocationPercent }}%</span>
        </div>
      </template>
      <template #cell-status="{ row }"><AllocationBadge :status="row.allocationStatus" :deleted="row.isDeleted" /></template>
      <template #cell-actions="{ row }">
        <div class="flex justify-end gap-1">
          <button v-if="row.isDeleted" type="button" :class="cn(ACTION, 'hover:text-emerald-500 hover:bg-emerald-50')" :title="$t('employee_list.btn_restore')" @click="emit('restore', row)">
            <RotateCcw :size="15" />
          </button>
          <template v-else>
            <button v-if="!row.account" type="button" :class="cn(ACTION, 'hover:text-pr hover:bg-pr/10')" :title="$t('employee_list.btn_invite')" @click="emit('invite', row)">
              <MailPlus :size="15" />
            </button>
            <button type="button" :class="cn(ACTION, 'hover:text-indigo-500 hover:bg-indigo-50')" :title="$t('employee_list.btn_edit')" @click="emit('edit', row)">
              <Pencil :size="15" />
            </button>
            <button type="button" :class="cn(ACTION, 'hover:text-rose-500 hover:bg-rose-50')" :title="$t('employee_list.btn_delete')" @click="emit('delete', row)">
              <Trash2 :size="15" />
            </button>
          </template>
        </div>
      </template>
    </CommonTable>
  </div>
</template>
