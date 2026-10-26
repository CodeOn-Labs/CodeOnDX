<script setup lang="ts" generic="TRow">
// Port ui/ExportDialog.tsx: chọn tên file, định dạng, phạm vi xuất và các cột trước khi xuất.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Check, CheckCircle2, Download, FileDown, FileSpreadsheet, FileText, Filter, Globe, Layers, Search, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import ExportProgress from './ExportProgress.vue';
import type { ExportColumn, ExportConfig, ExportFormat, ExportMode, ExportProgressData } from './types';
import { cn } from '@/shared/utils/cn';
import { formatDate } from '@/shared/utils/date';

const props = withDefaults(
  defineProps<{
    open: boolean;
    columns: ExportColumn<TRow>[];
    progress: ExportProgressData;
    defaultFilename?: string;
    totalRows?: number;
    filteredRows?: number;
    selectedRows?: number;
    hideScope?: boolean;
  }>(),
  { defaultFilename: 'Export_Data', totalRows: 0, filteredRows: 0, selectedRows: 0, hideScope: false },
);
const emit = defineEmits<{ close: []; export: [config: ExportConfig<TRow>]; cancel: [] }>();
const { t } = useI18n();

const withTimestamp = () => `${props.defaultFilename}_${formatDate(new Date(), 'ddMMyyyy_HHmmss')}`;

const filename = ref(withTimestamp());
const format = ref<ExportFormat>('xlsx');
const mode = ref<ExportMode>('filtered');
const columnSearch = ref('');
const visibility = ref<Record<string, boolean>>({});

function resetVisibility() {
  visibility.value = Object.fromEntries(props.columns.map((c) => [c.id, !c.hidden]));
}
resetVisibility();

watch(
  () => props.open,
  (open) => {
    if (open) filename.value = withTimestamp();
  },
);
watch(() => props.columns, resetVisibility);

const activeCount = computed(() => Object.values(visibility.value).filter(Boolean).length);
const filteredColumns = computed(() => props.columns.filter((c) => c.header.toLowerCase().includes(columnSearch.value.toLowerCase())));

const formatOptions = computed(() => [
  { value: 'xlsx', label: 'Excel (.xlsx)', icon: FileSpreadsheet, iconClass: 'text-emerald-600', description: t('component.export_dialog.opt_xlsx_hint') },
  { value: 'csv', label: 'CSV (.csv)', icon: FileText, iconClass: 'text-slate-650', description: t('component.export_dialog.opt_csv_hint') },
]);

const scopeOptions = computed(() => [
  { mode: 'all' as const, icon: Globe, title: t('component.export_dialog.opt_scope_all'), hint: t('component.export_dialog.hint_scope_all'), badge: t('component.export_dialog.unit_rows', { count: props.totalRows }), disabled: false },
  { mode: 'filtered' as const, icon: Filter, title: t('component.export_dialog.opt_scope_filtered'), hint: t('component.export_dialog.hint_scope_filtered'), badge: t('component.export_dialog.unit_rows', { count: props.filteredRows }), disabled: false },
  { mode: 'selected' as const, icon: Layers, title: t('component.export_dialog.opt_scope_selected'), hint: t('component.export_dialog.hint_scope_selected'), badge: t('component.export_dialog.unit_rows', { count: props.selectedRows }), disabled: props.selectedRows === 0 },
  { mode: 'current_page' as const, icon: FileDown, title: t('component.export_dialog.opt_scope_current_page'), hint: t('component.export_dialog.hint_scope_current_page'), badge: t('component.export_dialog.lbl_show'), disabled: false },
]);

const title = computed(() => {
  switch (props.progress.status) {
    case 'completed':
      return t('component.export_dialog.title_completed');
    case 'processing':
      return t('component.export_dialog.title_processing');
    case 'error':
      return t('component.export_dialog.title_error');
    default:
      return t('component.export_dialog.title_options');
  }
});

function setAll(value: boolean) {
  visibility.value = Object.fromEntries(props.columns.map((c) => [c.id, value]));
}

function startExport() {
  emit('export', {
    filename: filename.value.trim() || props.defaultFilename,
    format: format.value,
    mode: mode.value,
    columns: props.columns.map((c) => ({ ...c, hidden: !visibility.value[c.id] })),
  });
}

function onOpenChange(open: boolean) {
  if (!open && props.progress.status !== 'processing') emit('close');
}
</script>

<template>
  <BaseDialog :open="props.open" :title="title" max-width="2xl" body-class="p-6" @update:open="onOpenChange">
    <template #description>
      <span v-if="progress.status === 'completed'" class="text-emerald-650 font-semibold">{{ $t('component.export_dialog.hint_completed') }}</span>
      <span v-else-if="progress.status === 'processing'" class="text-pr font-semibold">{{ $t('component.export_dialog.hint_processing') }}</span>
      <span v-else-if="progress.status === 'error'" class="text-rose-600 font-semibold">{{ $t('component.export_dialog.hint_error') }}</span>
      <template v-else>{{ $t('component.export_dialog.hint_options') }}</template>
    </template>

    <div class="flex flex-col gap-6">
      <div v-if="progress.status !== 'idle'" class="py-4">
        <ExportProgress :progress="progress" :filename="filename" :format="format" class="border-0 shadow-none p-0 bg-transparent" @cancel="emit('cancel')" />
      </div>

      <template v-else>
        <!-- Cấu hình file -->
        <div class="grid grid-cols-1 md:grid-cols-12 gap-5 items-end">
          <div class="md:col-span-7 flex flex-col gap-2">
            <label for="export-filename" class="text-[13px] font-bold text-slate-800 flex items-center gap-1.5">{{ $t('component.export_dialog.lbl_filename') }}</label>
            <div class="relative flex items-center group">
              <div class="absolute left-3.5 text-slate-400 group-focus-within:text-pr transition-colors z-10">
                <FileDown class="w-4.5 h-4.5" />
              </div>
              <BaseInput id="export-filename" v-model="filename" type="text" class="pl-10 pr-16 lg:pl-10 lg:pr-16 h-10 w-full" :placeholder="$t('component.export_dialog.ph_filename')" />
              <div class="absolute right-3.5 bg-slate-100 text-slate-600 px-2 py-0.5 rounded-md text-[10px] font-extrabold uppercase select-none tracking-wider z-10">.{{ format }}</div>
            </div>
          </div>
          <div class="md:col-span-5 flex flex-col gap-2">
            <span class="text-[13px] font-bold text-slate-800">{{ $t('component.export_dialog.lbl_format') }}</span>
            <FormSelect
              :model-value="format"
              :options="formatOptions"
              :allow-clear="false"
              trigger-class="h-10 lg:h-10 text-[13.5px] w-full"
              hide-message
              @update:model-value="format = ($event as ExportFormat) || 'xlsx'"
            />
          </div>
        </div>

        <!-- Phạm vi xuất -->
        <div v-if="!props.hideScope" class="flex flex-col gap-3 pt-1">
          <span class="text-[13px] font-bold text-slate-800 flex items-center gap-1.5">{{ $t('component.export_dialog.lbl_scope') }}</span>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <button
              v-for="option in scopeOptions"
              :key="option.mode"
              type="button"
              :disabled="option.disabled"
              :class="cn(
                'flex items-start gap-3 p-3.5 border rounded-xl text-left transition-all duration-200 group focus:outline-none relative overflow-hidden',
                option.disabled ? 'opacity-45 cursor-not-allowed border-slate-100 bg-slate-50/50' : 'focus:ring-2 focus:ring-pr/15 cursor-pointer',
                mode === option.mode
                  ? 'bg-pr-50/30 border-pr text-slate-900 shadow-sm shadow-pr/5'
                  : option.disabled ? 'bg-slate-50/50' : 'bg-white border-slate-200 hover:border-slate-300 hover:bg-slate-50/30',
              )"
              @click="!option.disabled && (mode = option.mode)"
            >
              <div :class="cn('p-2 rounded-lg transition-all duration-200 shrink-0', mode === option.mode ? 'bg-pr text-white' : 'bg-slate-100 text-slate-500')">
                <component :is="option.icon" class="w-4 h-4" />
              </div>
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2">
                  <span class="text-sm font-bold text-slate-800">{{ option.title }}</span>
                  <span :class="cn('text-[10px] font-extrabold px-1.5 py-0.5 rounded-md', mode === option.mode ? 'bg-pr/10 text-pr' : 'bg-slate-100 text-slate-600')">
                    {{ option.badge }}
                  </span>
                </div>
                <span class="text-xs text-slate-500 mt-0.5 block leading-normal">{{ option.hint }}</span>
              </div>
              <CheckCircle2 v-if="mode === option.mode" class="w-4 h-4 text-pr absolute top-3.5 right-3.5" />
            </button>
          </div>
        </div>

        <!-- Chọn cột -->
        <div class="flex flex-col gap-4 border-t border-slate-100 pt-5">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div>
              <span class="text-[13px] font-bold text-slate-800">{{ $t('component.export_dialog.lbl_columns', { active: activeCount, total: props.columns.length }) }}</span>
              <div class="text-[11.5px] text-slate-500 mt-0.5">{{ $t('component.export_dialog.hint_columns') }}</div>
            </div>
            <div class="flex items-center gap-2 shrink-0">
              <button type="button" class="text-xs font-bold text-pr hover:underline transition-colors focus:outline-none px-1.5 py-1 hover:bg-slate-50 rounded-lg" @click="setAll(true)">
                {{ $t('component.export_dialog.btn_select_all') }}
              </button>
              <span class="text-slate-200 text-xs">|</span>
              <button type="button" class="text-xs font-bold text-slate-500 hover:text-slate-700 transition-colors focus:outline-none px-1.5 py-1 hover:bg-slate-50 rounded-lg" @click="setAll(false)">
                {{ $t('component.export_dialog.btn_deselect_all') }}
              </button>
            </div>
          </div>

          <div class="relative group">
            <div class="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 group-focus-within:text-pr transition-colors z-10">
              <Search class="w-4 h-4" />
            </div>
            <BaseInput v-model="columnSearch" type="text" :placeholder="$t('component.export_dialog.ph_search_column')" class="w-full pl-10 pr-10 lg:pl-10 lg:pr-10 h-10 bg-slate-50/40 focus:bg-white" />
            <button
              v-if="columnSearch"
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 p-1.5 text-slate-400 hover:text-slate-650 rounded-lg hover:bg-slate-100 transition-all focus:outline-none z-10"
              :aria-label="$t('component.export_dialog.btn_clear_search')"
              @click="columnSearch = ''"
            >
              <X class="w-3.5 h-3.5" />
            </button>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 max-h-[190px] overflow-y-auto p-1">
            <div v-if="filteredColumns.length === 0" class="col-span-full text-center py-8 text-sm text-slate-400 font-medium">
              {{ $t('component.export_dialog.empty_no_column') }}
            </div>
            <template v-else>
              <label
                v-for="col in filteredColumns"
                :key="col.id"
                :class="cn(
                  'group flex items-center gap-3 px-3 py-2.5 border rounded-xl cursor-pointer transition-all duration-150 select-none',
                  visibility[col.id] ? 'bg-white border-slate-300 text-slate-850 shadow-sm' : 'bg-transparent border-transparent text-slate-500 hover:bg-slate-50/50 hover:text-slate-700',
                )"
              >
                <input v-model="visibility[col.id]" type="checkbox" class="sr-only" />
                <div
                  :class="cn(
                    'flex h-4 w-4 flex-shrink-0 items-center justify-center rounded border transition-colors',
                    visibility[col.id] ? 'border-pr bg-pr/10 text-pr' : 'border-slate-300 bg-white group-hover:border-pr',
                  )"
                >
                  <Check v-if="visibility[col.id]" :size="11" :stroke-width="3.5" />
                </div>
                <span class="text-[13px] font-semibold truncate leading-none pt-0.5">{{ col.header }}</span>
              </label>
            </template>
          </div>
        </div>
      </template>
    </div>

    <template v-if="progress.status === 'idle' || progress.status === 'completed'" #footer>
      <div v-if="progress.status === 'idle'" class="flex items-center justify-between w-full">
        <span class="text-xs text-slate-500 font-semibold hidden sm:inline-block">
          <i18n-t keypath="component.export_dialog.lbl_selected_columns" scope="global">
            <template #active><strong class="text-slate-800 font-bold">{{ activeCount }}</strong></template>
            <template #total><strong class="text-slate-800 font-bold">{{ props.columns.length }}</strong></template>
          </i18n-t>
        </span>
        <div class="flex items-center gap-2.5 ml-auto">
          <Button variant="danger" class="rounded-xl px-4 py-2 transition-all font-semibold" @click="emit('close')">
            <template #leftIcon><X class="w-4 h-4" /></template>
            {{ $t('component.export_dialog.btn_cancel') }}
          </Button>
          <Button
            variant="primary"
            class="rounded-xl bg-gradient-to-r from-pr to-bl text-white shadow-md shadow-pr/10 hover:shadow-lg hover:shadow-pr/15 hover:opacity-95 active:scale-[0.98] transition-all font-bold px-6 h-10 disabled:opacity-50 disabled:pointer-events-none border-0"
            :disabled="activeCount === 0 || !filename.trim()"
            @click="startExport"
          >
            <template #leftIcon><Download class="w-4 h-4" /></template>
            {{ $t('component.export_dialog.btn_start_export') }}
          </Button>
        </div>
      </div>
      <div v-else class="flex justify-end w-full">
        <Button
          variant="primary"
          class="rounded-xl bg-gradient-to-r from-pr to-bl text-white hover:opacity-95 shadow-md shadow-pr/15 active:scale-[0.98] transition-all font-bold px-6 h-10 border-0"
          @click="emit('close')"
        >
          <template #leftIcon><Check class="w-4 h-4" /></template>
          {{ $t('component.export_dialog.btn_done') }}
        </Button>
      </div>
    </template>
  </BaseDialog>
</template>
