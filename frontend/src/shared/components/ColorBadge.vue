<script setup lang="ts">
// Huy hiệu màu theo danh mục (master data): nền nhạt + chữ cùng màu — port getBadgeStyles của CategoryTable.tsx.
import { computed } from 'vue';
import { badgeStyle } from '@/shared/utils/color';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(defineProps<{ color?: string | null; label: string; dot?: boolean; class?: string }>(), { color: null, dot: false, class: '' });
const style = computed(() => badgeStyle(props.color));
</script>

<template>
  <span :class="cn('inline-flex items-center gap-1.5 rounded-lg px-2 py-0.5 text-[11px] font-bold whitespace-nowrap', props.class)" :style="style">
    <span v-if="props.dot" class="h-1.5 w-1.5 rounded-full" :style="{ backgroundColor: style.color }" />
    {{ props.label }}
  </span>
</template>
