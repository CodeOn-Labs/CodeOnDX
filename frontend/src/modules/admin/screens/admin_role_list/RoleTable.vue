<script setup lang="ts">
// Port RoleTable.tsx — giữ class các ô.
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { ArrowUpRight, Trash2, UserPlus, Users } from '@lucide/vue';
import CommonTable from '@/shared/components/table/CommonTable.vue';
import type { TableColumn } from '@/shared/components/table/types';
import type { PaginationController } from '@/shared/composables/usePagination';
import type { Role } from '@/modules/admin/api/system.api';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ roles: Role[]; loading: boolean; pagination: PaginationController }>();
const emit = defineEmits<{ view: [role: Role]; assign: [role: Role]; delete: [role: Role] }>();
const { t } = useI18n();

const columns = computed<TableColumn[]>(() => [
  { id: 'name', header: t('admin_role_list.col_role') },
  { id: 'type', header: t('admin_role_list.col_type'), headerClass: 'w-[120px]' },
  { id: 'permissions', header: t('admin_role_list.col_permissions') },
  { id: 'members', header: t('admin_role_list.col_members'), headerClass: 'w-[110px]' },
  { id: 'actions', header: '', cellClass: 'w-[130px] pr-4 text-right' },
]);
</script>

<template>
  <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
    <CommonTable
      :columns="columns"
      :rows="props.roles"
      :loading="props.loading"
      :empty-message="$t('admin_role_list.empty_no_role')"
      :pagination="props.pagination"
      table-class="min-w-[980px]"
      container-class="flex-1 min-h-0"
      table-wrapper-class="border-none shadow-none bg-transparent"
      :row-class="(r) => cn('cursor-pointer transition-all duration-300 ease-in-out', r.isSystem ? 'hover:bg-slate-50/40 hover:!border-slate-400' : 'bg-pr/[0.01] hover:bg-slate-50/40 hover:!border-pr')"
    >
      <template #cell-name="{ row }">
        <div class="flex flex-col gap-1.5 py-2">
          <button
            type="button"
            class="group/link inline-flex items-center gap-1.5 text-left text-[13px] font-extrabold text-slate-900 transition-all duration-300 hover:text-pr outline-none focus-visible:ring-2 focus-visible:ring-pr/30 focus-visible:ring-offset-1 rounded-md px-1 -mx-1"
            @click="emit('view', row)"
          >
            <span class="relative">
              {{ row.name }}
              <span class="absolute -bottom-0.5 left-0 h-[2px] w-0 bg-pr transition-all duration-500 group-hover/link:w-full" />
            </span>
            <ArrowUpRight :size="14" class="opacity-0 -translate-x-2 translate-y-1 transition-all duration-300 group-hover/link:opacity-100 group-hover/link:translate-x-0 group-hover/link:translate-y-0 text-pr" />
          </button>
          <div class="flex items-center gap-2.5">
            <div class="text-[11px] font-medium text-slate-500">{{ row.description || $t('admin_role_list.lbl_no_description') }}</div>
            <span v-if="!row.isSystem" class="rounded-full bg-pr/10 px-1.5 py-0.5 text-[8px] font-black uppercase tracking-wider text-pr">{{ $t('admin_role_list.opt_type_custom') }}</span>
          </div>
        </div>
      </template>
      <template #cell-type="{ row }">
        <div
          :class="cn(
            'inline-flex min-h-7 items-center justify-center rounded-full px-2.5 py-1 text-center text-[10px] font-black uppercase leading-[1.15] tracking-widest ring-1 ring-inset',
            row.isSystem ? 'bg-slate-50 text-slate-600 ring-slate-500/10' : 'bg-violet-50 text-violet-700 ring-violet-500/20',
          )"
        >
          {{ row.isSystem ? $t('admin_role_list.opt_type_system') : $t('admin_role_list.opt_type_custom') }}
        </div>
      </template>
      <template #cell-permissions="{ row }">
        <div class="flex max-w-[320px] flex-wrap items-center gap-1.5">
          <span v-if="row.permissions.length === 0" class="text-[12px] font-medium text-slate-300">—</span>
          <span v-for="p in row.permissions.slice(0, 2)" :key="p" class="rounded-lg border border-slate-200/60 bg-slate-100 px-2 py-0.5 text-[10px] font-bold text-slate-600">{{ p }}</span>
          <span v-if="row.permissions.length > 2" class="text-[10px] font-black text-slate-400">+{{ row.permissions.length - 2 }}</span>
        </div>
      </template>
      <template #cell-members="{ row }">
        <div class="inline-flex w-fit items-center gap-1.5 rounded-lg border border-slate-100/50 bg-slate-50 px-2 py-1 text-slate-600">
          <Users :size="12" class="text-slate-400" />
          <span class="text-[12px] font-black tabular-nums">{{ row.memberCount }}</span>
        </div>
      </template>
      <template #cell-actions="{ row }">
        <div class="flex justify-end gap-1.5">
          <button
            type="button"
            class="cursor-pointer p-2 text-slate-400 rounded-xl transition-all inline-flex items-center justify-center hover:text-gn hover:bg-gnbg hover:scale-110 active:scale-95 outline-none focus-visible:ring-2 focus-visible:ring-gn/30 focus-visible:ring-offset-1"
            :title="$t('admin_role_list.btn_assign')"
            @click.stop="emit('assign', row)"
          >
            <UserPlus :size="16" />
          </button>
          <button
            v-if="!row.isSystem"
            type="button"
            class="cursor-pointer p-2 text-slate-400 rounded-xl transition-all inline-flex items-center justify-center hover:text-rd hover:bg-rdbg hover:scale-110 active:scale-95 outline-none focus-visible:ring-2 focus-visible:ring-rd/30 focus-visible:ring-offset-1"
            :title="$t('admin_role_list.btn_delete')"
            @click.stop="emit('delete', row)"
          >
            <Trash2 :size="16" />
          </button>
        </div>
      </template>
    </CommonTable>
  </div>
</template>
