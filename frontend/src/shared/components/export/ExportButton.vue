<script setup lang="ts" generic="TRow">
// Port ui/ExportButton.tsx: nút mở hộp thoại xuất; lấy dữ liệu theo phạm vi qua `dataFn` rồi giao cho useExport.
import { ref } from 'vue';
import { Download } from '@lucide/vue';
import Button, { type ButtonSize, type ButtonVariant } from '@/shared/components/Button.vue';
import ExportDialog from './ExportDialog.vue';
import { useExport } from './useExport';
import type { ExportColumn, ExportConfig, ExportMode } from './types';
import { useToastStore } from '@/stores/toast';

const props = withDefaults(
  defineProps<{
    columns: ExportColumn<TRow>[];
    dataFn: (mode: ExportMode) => Promise<TRow[]> | TRow[];
    defaultFilename?: string;
    totalRows?: number;
    filteredRows?: number;
    selectedRows?: number;
    class?: string;
    variant?: ButtonVariant;
    size?: ButtonSize;
    label?: string;
    hideScope?: boolean;
  }>(),
  {
    defaultFilename: undefined,
    totalRows: undefined,
    filteredRows: undefined,
    selectedRows: undefined,
    class: undefined,
    variant: 'outline',
    size: undefined,
    label: undefined,
    hideScope: false,
  },
);

const open = ref(false);
const { progress, startExport, cancelExport, reset } = useExport<TRow>();
const toast = useToastStore();

async function handleExport(config: ExportConfig<TRow>) {
  try {
    startExport(await props.dataFn(config.mode), config);
  } catch (err) {
    // Lỗi gọi API đã có toast của query client; ở đây báo các lỗi còn lại
    if (!(err instanceof Error && err.name === 'ApiError')) toast.error(err instanceof Error ? err.message : String(err));
  }
}

function close() {
  open.value = false;
  reset();
}
</script>

<template>
  <Button :variant="props.variant" :size="props.size" :class="props.class" @click="open = true">
    <template #leftIcon>
      <slot name="icon"><Download class="w-4 h-4 mr-2" /></slot>
    </template>
    {{ props.label ?? $t('component.export_button.btn_export') }}
  </Button>

  <ExportDialog
    :open="open"
    :columns="props.columns"
    :progress="progress"
    :default-filename="props.defaultFilename"
    :total-rows="props.totalRows"
    :filtered-rows="props.filteredRows"
    :selected-rows="props.selectedRows"
    :hide-scope="props.hideScope"
    @close="close"
    @export="handleExport"
    @cancel="cancelExport"
  />
</template>
