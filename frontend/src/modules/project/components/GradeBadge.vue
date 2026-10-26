<script setup lang="ts">
// Port GradeBadge (ProjectTable.tsx): ô vuông hạng dự án S/A/B/C, màu theo metadata.color của master data.
import { computed } from 'vue';
import type { MasterDataRef } from '@/api/types';
import { COLOR_CLASSES, gradeVariant, hexStyle, isHexColor, variantColor } from '@/modules/project/utils/project-view-model';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ grade: MasterDataRef | null }>();

const color = computed(() => props.grade?.color ?? null);
const cls = computed(() => (!isHexColor(color.value) && color.value && COLOR_CLASSES[color.value]) || COLOR_CLASSES[variantColor(gradeVariant(props.grade))]);
const label = computed(() => props.grade?.code || props.grade?.name || 'B');
</script>

<template>
  <div
    :class="cn('inline-flex h-7 w-7 items-center justify-center rounded-lg text-[12px] font-black shadow-sm ring-1 ring-inset transition-transform duration-300 hover:scale-110', cls)"
    :style="isHexColor(color) ? hexStyle(color) : undefined"
  >
    {{ label }}
  </div>
</template>
