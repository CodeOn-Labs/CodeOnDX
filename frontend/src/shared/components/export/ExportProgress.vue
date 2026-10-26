<script setup lang="ts">
// Port ui/ExportProgress.tsx: thẻ tiến trình / hoàn tất / lỗi của hộp thoại xuất dữ liệu.
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { AlertTriangle, CheckCircle2, Clock, Database, FileJson, FileSpreadsheet, FileText, Loader2 } from '@lucide/vue';
import type { ExportProgressData } from './types';
import { cn } from '@/shared/utils/cn';
import { formatDate } from '@/shared/utils/date';

const props = withDefaults(defineProps<{ progress: ExportProgressData; filename?: string; format?: string; class?: string; cancelable?: boolean }>(), {
  filename: undefined,
  format: 'xlsx',
  class: '',
  cancelable: true,
});
const emit = defineEmits<{ cancel: [] }>();
const { t } = useI18n();

const formatInfo = computed(() => {
  switch (props.format) {
    case 'csv':
      return { badge: 'bg-amber-50 border-amber-200 text-amber-700', label: t('component.export_progress.lbl_format_csv') };
    case 'json':
      return { badge: 'bg-blue-50 border-blue-200 text-blue-700', label: t('component.export_progress.lbl_format_json') };
    case 'pdf':
      return { badge: 'bg-rose-50 border-rose-200 text-rose-700', label: t('component.export_progress.lbl_format_pdf') };
    default:
      return { badge: 'bg-emerald-50 border-emerald-200 text-emerald-700', label: t('component.export_progress.lbl_format_xlsx') };
  }
});

const stageMessage = computed(() => {
  const { stage, processed, total } = props.progress;
  switch (stage) {
    case 'starting':
      return t('component.export_progress.msg_stage_starting');
    case 'preparing':
      return t('component.export_progress.msg_stage_preparing');
    case 'processing':
      return t('component.export_progress.msg_stage_processing', { processed, total });
    case 'writing_xlsx':
      return t('component.export_progress.msg_stage_writing_xlsx');
    case 'writing_csv':
      return t('component.export_progress.msg_stage_writing_csv');
    default:
      return t('component.export_progress.msg_stage_default');
  }
});

const finishedAt = computed(() => (props.progress.status === 'completed' ? formatDate(new Date(), 'HH:mm - dd/MM/yyyy') : ''));
</script>

<template>
  <!-- Hoàn tất -->
  <div v-if="progress.status === 'completed'" :class="cn('flex flex-col items-center justify-center text-center py-4 px-1 animate-in fade-in zoom-in-95 duration-350 w-full', props.class)">
    <div class="relative mb-8 mt-2">
      <div class="absolute inset-0 rounded-full bg-emerald-100/70 animate-ping opacity-60 scale-125" style="animation-duration: 2s" />
      <div class="absolute -inset-3 rounded-full bg-emerald-50/50 animate-pulse opacity-40 scale-110" />
      <div class="relative flex items-center justify-center w-14 h-14 rounded-full bg-gradient-to-tr from-emerald-500 to-teal-400 shadow-lg shadow-emerald-500/20 text-white">
        <CheckCircle2 class="w-8 h-8 stroke-[2.5]" />
      </div>
    </div>

    <div class="w-full max-w-md bg-white border border-slate-200/80 rounded-2xl p-5 text-left flex flex-col gap-4 shadow-lg shadow-slate-100/50 transition-all duration-300">
      <div class="flex items-start justify-between gap-4">
        <div class="flex items-center gap-3 min-w-0">
          <div :class="cn('p-2.5 rounded-xl flex items-center justify-center border shrink-0 shadow-sm', formatInfo.badge)">
            <FileSpreadsheet v-if="format === 'xlsx'" class="w-5 h-5" />
            <FileJson v-else-if="format === 'json'" class="w-5 h-5" />
            <FileText v-else class="w-5 h-5" />
          </div>
          <div class="flex flex-col min-w-0">
            <span class="text-[13.5px] font-extrabold text-slate-850 truncate pr-2">{{ filename ? `${filename}.${format}` : `export_data.${format}` }}</span>
            <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">{{ formatInfo.label }}</span>
          </div>
        </div>
        <span :class="cn('px-2.5 py-0.5 rounded-full text-[10px] font-black uppercase tracking-wider border shrink-0 shadow-sm', formatInfo.badge)">{{ format }}</span>
      </div>

      <div class="h-px border-t border-dashed border-slate-200" />

      <div class="grid grid-cols-2 gap-4">
        <div class="flex flex-col gap-0.5">
          <span class="text-[10px] font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1">
            <Database class="w-3 h-3 text-slate-400" /> {{ $t('component.export_progress.lbl_total_records') }}
          </span>
          <span class="text-sm font-extrabold text-slate-800">{{ $t('component.export_progress.unit_rows', { count: progress.total || progress.processed }) }}</span>
        </div>
        <div class="flex flex-col gap-0.5">
          <span class="text-[10px] font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1">
            <Clock class="w-3 h-3 text-slate-400" /> {{ $t('component.export_progress.lbl_output_time') }}
          </span>
          <span class="text-sm font-extrabold text-slate-800">{{ finishedAt }}</span>
        </div>
      </div>
    </div>
  </div>

  <!-- Đang xử lý -->
  <div
    v-else-if="progress.status === 'processing'"
    :class="cn('p-5 bg-white border border-slate-100 rounded-2xl shadow-sm flex flex-col gap-4.5 animate-in fade-in slide-in-from-bottom-2 duration-300', props.class)"
  >
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-3 min-w-0">
        <div class="p-2 bg-pr/10 border border-pr/20 rounded-xl flex items-center justify-center shrink-0 animate-spin">
          <Loader2 class="w-5 h-5 text-pr" />
        </div>
        <div class="flex flex-col min-w-0">
          <span class="text-sm font-extrabold text-slate-850">{{ $t('component.export_progress.title_compiling') }}</span>
          <span class="text-xs font-semibold text-slate-400 truncate pr-2">{{ stageMessage }}</span>
        </div>
      </div>
      <span class="text-sm font-black text-pr tabular-nums">{{ progress.progress }}%</span>
    </div>

    <div class="w-full h-2.5 rounded-full bg-slate-50 overflow-hidden relative border border-slate-100">
      <div
        class="h-full rounded-full bg-gradient-to-r from-pr to-bl transition-all duration-300 ease-out shadow-[0_2px_4px_rgba(124,58,237,0.2)]"
        :style="{ width: `${progress.progress}%` }"
      />
    </div>

    <div class="flex items-center justify-between mt-0.5">
      <span class="text-[11px] font-bold text-slate-400 flex items-center gap-1.5">
        {{ $t('component.export_progress.lbl_processed') }}
        <strong class="text-slate-700 font-extrabold">{{ progress.processed }}</strong> /
        <strong class="text-slate-700 font-extrabold">{{ progress.total }}</strong>
        {{ $t('component.export_progress.unit_row') }}
      </span>
      <button
        v-if="props.cancelable"
        type="button"
        class="text-xs font-bold text-slate-400 hover:text-red-500 hover:bg-red-50/50 px-3 py-1 rounded-lg transition-all focus:outline-none"
        @click="emit('cancel')"
      >
        {{ $t('component.export_progress.btn_cancel_export') }}
      </button>
    </div>
  </div>

  <!-- Lỗi -->
  <div
    v-else-if="progress.status === 'error'"
    :class="cn('p-5 bg-white border border-slate-100 rounded-2xl shadow-sm flex flex-col gap-4 animate-in fade-in slide-in-from-bottom-2 duration-300', props.class)"
  >
    <div class="flex items-start gap-4">
      <div class="p-2.5 bg-red-50 border border-red-100 rounded-xl flex items-center justify-center shrink-0 text-red-500">
        <AlertTriangle class="w-6 h-6 stroke-[2]" />
      </div>
      <div class="flex flex-col gap-1 min-w-0">
        <span class="text-sm font-bold text-slate-850">{{ $t('component.export_progress.title_failed') }}</span>
        <span class="text-xs font-semibold text-slate-400">{{ $t('component.export_progress.hint_failed') }}</span>
      </div>
    </div>
    <div class="p-3.5 bg-red-50/40 border border-red-100/50 rounded-xl text-[12.5px] font-semibold text-red-600">
      {{ progress.message || $t('component.export_progress.err_unknown') }}
    </div>
  </div>
</template>
