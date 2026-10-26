<script setup lang="ts">
// Port StatusBadge (ProjectTable.tsx): nhãn trạng thái dự án có chấm màu (nhấp nháy khi đang chạy).
import { computed } from 'vue';
import type { MasterDataRef } from '@/api/types';
import { COLOR_CLASSES, DOT_COLOR_CLASSES, hexStyle, isHexColor, statusKey, statusVariant, variantColor } from '@/modules/project/utils/project-view-model';
import { enumLabel } from '@/shared/i18n/enum-label';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ status: MasterDataRef | null }>();

const color = computed(() => props.status?.color ?? null);
const fallbackColor = computed(() => variantColor(statusVariant(props.status)));
const cls = computed(() => (!isHexColor(color.value) && color.value && COLOR_CLASSES[color.value]) || COLOR_CLASSES[fallbackColor.value]);
const dotCls = computed(() => (!isHexColor(color.value) && color.value && DOT_COLOR_CLASSES[color.value]) || DOT_COLOR_CLASSES[fallbackColor.value]);
const isRunning = computed(() => statusKey(props.status) === 'running');
const label = computed(() => enumLabel('project_status', props.status?.code ?? 'PLANNING', props.status?.name));
</script>

<template>
  <div
    :class="cn(
      'inline-flex h-7 items-center gap-1.5 rounded-full px-2.5 text-[10px] font-black uppercase tracking-widest shadow-sm ring-1 ring-inset transition-all duration-300 hover:shadow-md hover:-translate-y-0.5 cursor-default',
      cls,
    )"
    :style="isHexColor(color) ? hexStyle(color) : undefined"
  >
    <div class="relative flex h-1.5 w-1.5">
      <span v-if="isRunning" class="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
      <span
        :class="cn('relative inline-flex h-1.5 w-1.5 rounded-full', isHexColor(color) ? '' : dotCls)"
        :style="isHexColor(color) ? { backgroundColor: color } : undefined"
      />
    </div>
    {{ label }}
  </div>
</template>
