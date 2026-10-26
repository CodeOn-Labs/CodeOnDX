<script setup lang="ts">
// Port ClientTable.tsx — giữ nguyên class các ô: tên + mã, ngành nghề, liên hệ, quốc gia, số dự án, trạng thái, thao tác.
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { ArrowUpRight, FolderKanban, Globe, Mail, RotateCcw, Settings, Trash2, User } from '@lucide/vue';
import CommonTable from '@/shared/components/table/CommonTable.vue';
import type { TableColumn } from '@/shared/components/table/types';
import type { PaginationController } from '@/shared/composables/usePagination';
import type { Client } from '@/modules/org/api/org.api';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ clients: Client[]; loading: boolean; pagination: PaginationController }>();
const emit = defineEmits<{ edit: [client: Client]; delete: [client: Client]; restore: [client: Client]; viewProjects: [client: Client] }>();
const { t } = useI18n();

const columns = computed<TableColumn[]>(() => [
  { id: 'name', header: t('admin_client_list.col_client') },
  { id: 'industry', header: t('admin_client_list.col_industry') },
  { id: 'contact', header: t('admin_client_list.col_contact') },
  { id: 'country', header: t('admin_client_list.col_country') },
  { id: 'projects', header: t('admin_client_list.col_projects') },
  { id: 'status', header: t('admin_client_list.col_status') },
  { id: 'actions', header: '', cellClass: 'text-right' },
]);

const ACTION = 'p-2 text-slate-400 rounded-xl transition-all hover:scale-110 active:scale-95 outline-none focus-visible:ring-2 focus-visible:ring-offset-1';
</script>

<template>
  <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
    <CommonTable
      :columns="columns"
      :rows="props.clients"
      :loading="props.loading"
      :empty-message="$t('admin_client_list.empty_no_client')"
      :pagination="props.pagination"
      table-class="min-w-[1000px]"
      container-class="flex-1 min-h-0"
      table-wrapper-class="border-none shadow-none bg-transparent"
      :row-class="(row) => cn('cursor-pointer transition-all duration-300 ease-in-out hover:bg-slate-50/40', row.isActive ? 'hover:!border-emerald-500' : 'hover:!border-rose-500')"
    >
      <template #cell-name="{ row }">
        <button
          type="button"
          :disabled="row.isDeleted"
          class="flex flex-col gap-1.5 py-2 group/link cursor-pointer text-left w-full outline-none focus-visible:ring-2 focus-visible:ring-pr/30 focus-visible:ring-offset-1 rounded-md px-1 -mx-1"
          @click="!row.isDeleted && emit('edit', row)"
        >
          <div class="inline-flex items-center gap-2 text-[13px] font-extrabold text-slate-900 transition-all duration-300 group-hover/link:text-pr">
            <span class="relative">
              {{ row.name }}
              <span class="absolute -bottom-0.5 left-0 h-[2px] w-0 bg-pr transition-all duration-500 group-hover/link:w-full" />
            </span>
            <ArrowUpRight :size="14" class="opacity-0 -translate-x-2 translate-y-1 transition-all duration-300 group-hover/link:opacity-100 group-hover/link:translate-x-0 group-hover/link:translate-y-0 text-pr" />
          </div>
          <div class="flex items-center gap-1.5">
            <span class="text-[10px] text-slate-400 font-mono tracking-wider bg-slate-50 px-1.5 py-0.5 rounded border border-slate-100">{{ row.code }}</span>
            <span v-if="row.isDeleted" class="inline-flex items-center rounded-full bg-rose-50 px-2 py-0.5 text-[9px] font-bold text-rose-600 ring-1 ring-inset ring-rose-500/20">
              {{ $t('admin_client_list.lbl_deleted') }}
            </span>
          </div>
        </button>
      </template>

      <template #cell-industry="{ row }">
        <div class="flex items-center gap-2">
          <div :class="cn('h-1.5 w-1.5 rounded-full', row.industry ? 'bg-indigo-400/60' : 'bg-slate-300')" />
          <span :class="cn('text-[11px] font-bold uppercase tracking-widest', row.industry ? 'text-slate-600' : 'text-slate-400 italic')">{{ row.industry?.name ?? '--' }}</span>
        </div>
      </template>

      <template #cell-contact="{ row }">
        <div class="flex flex-col gap-1">
          <div :class="cn('flex items-center gap-1.5 text-[12px] font-bold', row.contactName ? 'text-slate-700' : 'text-slate-400 italic')">
            <User :size="12" class="text-slate-400" />
            {{ row.contactName || '--' }}
          </div>
          <div :class="cn('flex items-center gap-1.5 text-[11px] font-medium', row.contactEmail ? 'text-slate-400' : 'text-slate-300 italic')">
            <Mail :size="11" />
            {{ row.contactEmail || '--' }}
          </div>
        </div>
      </template>

      <template #cell-country="{ row }">
        <div class="flex items-center gap-2 px-3 py-1 bg-slate-50 border border-slate-100 rounded-xl w-fit">
          <Globe :size="12" class="text-indigo-500" />
          <span :class="cn('text-[11px] font-bold', row.country ? 'text-slate-600' : 'text-slate-400 italic')">{{ row.country?.name ?? '--' }}</span>
        </div>
      </template>

      <template #cell-projects="{ row }">
        <button
          type="button"
          class="flex items-center gap-2 group/projects cursor-pointer outline-none focus-visible:ring-2 focus-visible:ring-pr/30 focus-visible:ring-offset-1 rounded-md px-1 -mx-1"
          :aria-label="$t('admin_client_list.tip_view_projects')"
          @click.stop="emit('viewProjects', row)"
        >
          <div class="flex items-center justify-center w-7 h-7 rounded-lg bg-pr/5 text-pr border border-pr/10 font-black text-[12px] shadow-sm transition-all duration-300">{{ row.projectsCount }}</div>
          <FolderKanban :size="14" class="text-slate-300 group-hover/projects:text-pr transition-colors" />
        </button>
      </template>

      <template #cell-status="{ row }">
        <div
          :class="cn(
            'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-xl text-[10px] font-black tracking-wider uppercase border-2 shadow-sm',
            row.isActive ? 'bg-emerald-50/50 text-emerald-600 border-emerald-100/50 shadow-emerald-100/20' : 'bg-rose-50/50 text-rose-600 border-rose-100/50 shadow-rose-100/20',
          )"
        >
          <div :class="cn('h-1 w-1 rounded-full', row.isActive ? 'bg-emerald-600' : 'bg-rose-600')" />
          {{ row.isActive ? $t('admin_client_list.opt_status_active') : $t('admin_client_list.opt_status_inactive') }}
        </div>
      </template>

      <template #cell-actions="{ row }">
        <div class="flex justify-end gap-1.5">
          <template v-if="row.isDeleted">
            <button type="button" :class="cn(ACTION, 'hover:text-emerald-500 hover:bg-emerald-50 focus-visible:ring-emerald-500/30')" :title="$t('admin_client_list.btn_restore')" @click.stop="emit('restore', row)">
              <RotateCcw :size="16" />
            </button>
            <button type="button" :class="cn(ACTION, 'hover:text-rose-500 hover:bg-rose-50 focus-visible:ring-rose-500/30')" :title="$t('admin_client_list.btn_purge')" @click.stop="emit('delete', row)">
              <Trash2 :size="16" />
            </button>
          </template>
          <template v-else>
            <button type="button" :class="cn(ACTION, 'hover:text-indigo-500 hover:bg-indigo-50 focus-visible:ring-indigo-500/30')" :title="$t('admin_client_list.btn_edit')" @click.stop="emit('edit', row)">
              <Settings :size="16" />
            </button>
            <button type="button" :class="cn(ACTION, 'hover:text-rose-500 hover:bg-rose-50 focus-visible:ring-rose-500/30')" :title="$t('admin_client_list.btn_delete')" @click.stop="emit('delete', row)">
              <Trash2 :size="16" />
            </button>
          </template>
        </div>
      </template>
    </CommonTable>
  </div>
</template>
