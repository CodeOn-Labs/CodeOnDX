<script setup lang="ts">
// Port ProjectDetailHeader.tsx: breadcrumb, tên + hạng, khách hàng, thời gian, trạng thái; nút Xuất dữ liệu / Report / Xoá / Khôi phục / Chỉnh sửa.
import { RouterLink } from 'vue-router';
import { ArrowLeft, Edit3, FileDown, FileText, RefreshCw, Trash2 } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import GradeBadge from '@/modules/project/components/GradeBadge.vue';
import type { ProjectDetail } from '@/modules/project/api/project-detail.api';
import { statusKey } from '@/modules/project/utils/project-view-model';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ project: ProjectDetail }>();
const emit = defineEmits<{ export: []; report: []; delete: []; restore: [] }>();
const running = () => statusKey(props.project.status) === 'running';
</script>

<template>
  <div class="flex flex-col gap-1 animate-in fade-in slide-in-from-top-4 duration-500">
    <div class="mb-1 flex items-center gap-2 text-[11px] font-bold uppercase tracking-widest text-slate-400">
      <RouterLink to="/projects" class="group flex items-center gap-1 transition-colors hover:text-pr">
        <ArrowLeft :size="14" class="transition-transform group-hover:-translate-x-0.5" />{{ $t('project_detail.lbl_breadcrumb') }}
      </RouterLink>
      <span class="opacity-40">/</span>
      <span class="text-slate-600">{{ props.project.name }}</span>
      <span v-if="props.project.isDeleted" class="inline-flex items-center gap-1 rounded-full bg-rose-50 px-2 py-0.5 text-[9px] font-black uppercase tracking-widest text-rose-500 ring-1 ring-rose-200">
        <Trash2 :size="9" />{{ $t('project_detail.lbl_deleted') }}
      </span>
    </div>

    <div class="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
      <div class="flex min-w-0 flex-col gap-0.5">
        <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
          <h1 class="m-0 break-words bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text p-0 text-[24px] font-black tracking-tight text-transparent">{{ props.project.name }}</h1>
          <GradeBadge :grade="props.project.grade" />
        </div>
        <div class="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1.5 text-[11px] font-bold tracking-wide text-slate-500">
          <span class="font-extrabold uppercase tracking-widest text-slate-800">{{ props.project.client?.name ?? '—' }}</span>
          <span class="h-1.5 w-1.5 rounded-full bg-slate-200" />
          <span class="flex items-center gap-1.5 rounded-md border border-slate-100 bg-slate-50 px-2 py-0.5 shadow-sm">
            <span v-if="props.project.startDate" class="tabular-nums text-slate-700">{{ formatDate(props.project.startDate) }}</span>
            <span v-else class="italic text-slate-400">{{ $t('project_detail.lbl_no_start') }}</span>
            <span class="mx-0.5 text-slate-300">–</span>
            <span v-if="props.project.endDate" class="tabular-nums text-slate-700">{{ formatDate(props.project.endDate) }}</span>
            <span v-else class="inline-flex items-center gap-1 rounded border border-amber-100 bg-amber-50 px-1.5 py-0.5 text-[10px] font-black uppercase tracking-wider text-amber-600">{{ $t('project_detail.lbl_no_end') }}</span>
          </span>
          <span class="h-1.5 w-1.5 rounded-full bg-slate-200" />
          <span class="flex items-center gap-1.5 rounded-md border border-slate-100 bg-white px-2 py-0.5 text-[10px] font-black uppercase tracking-widest shadow-sm">
            <span :class="cn('h-1.5 w-1.5 rounded-full', running() ? 'bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]' : 'bg-amber-500 shadow-[0_0_8px_rgba(245,158,11,0.5)]')" />
            <span :class="running() ? 'text-emerald-700' : 'text-amber-700'">{{ props.project.status?.name ?? '—' }}</span>
          </span>
        </div>
      </div>

      <div class="flex flex-wrap items-center gap-2.5">
        <Button variant="outline" class="h-10 rounded-xl border-[#e0f2fe] bg-[#f0f9ff] px-4 text-[14px] font-bold text-[#0369a1] shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:border-[#bae6fd] hover:bg-[#e0f2fe] hover:text-[#0284c7] hover:shadow-md sm:px-6" @click="emit('export')">
          <template #leftIcon><FileDown :size="16" class="text-[#0ea5e9]" /></template>{{ $t('project_detail.btn_export') }}
        </Button>
        <Button v-if="props.project.canManage && !props.project.isDeleted" variant="outline" class="h-10 rounded-xl border-[#ede9fe] bg-[#f5f3ff] px-4 text-[14px] font-bold text-[#6d28d9] shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:border-[#ddd6fe] hover:bg-[#ede9fe] hover:text-[#5b21b6] hover:shadow-md sm:px-6" @click="emit('report')">
          <template #leftIcon><FileText :size="16" class="text-[#7c3aed]" /></template>{{ $t('project_detail.btn_report') }}
        </Button>
        <template v-if="props.project.canManage">
          <Button v-if="props.project.isDeleted" variant="primary" class="h-10 rounded-xl px-4 text-[14px] font-bold shadow-lg shadow-pr/25 sm:px-6" @click="emit('restore')">
            <template #leftIcon><RefreshCw :size="16" /></template>{{ $t('project_detail.btn_restore') }}
          </Button>
          <template v-else>
            <Button variant="danger" class="h-10 rounded-xl px-4 text-[14px] font-bold shadow-sm sm:px-6" @click="emit('delete')">
              <template #leftIcon><Trash2 :size="16" /></template>{{ $t('project_detail.btn_delete') }}
            </Button>
            <RouterLink :to="`/projects/${props.project.id}/edit`">
              <Button variant="primary" class="h-10 rounded-xl px-4 text-[14px] font-bold shadow-lg shadow-pr/25 sm:px-6">
                <template #leftIcon><Edit3 :size="16" /></template>{{ $t('project_detail.btn_edit') }}
              </Button>
            </RouterLink>
          </template>
        </template>
      </div>
    </div>
  </div>
</template>
