<script setup lang="ts">
// Port InsightPanel (shared/components/InsightPanel.tsx): khung cột Insight bên phải.
withDefaults(defineProps<{ title?: string; subtitle?: string; width?: string }>(), {
  title: 'Insights',
  subtitle: undefined,
  width: '240px',
});
</script>

<template>
  <div class="bg-white border-l border-b1 py-4 flex flex-col gap-4 flex-shrink-0" :style="{ width }">
    <div v-if="title || subtitle" class="px-4">
      <div v-if="title" class="font-semibold text-t3 uppercase tracking-wider text-[10px] mb-1">{{ title }}</div>
      <div v-if="subtitle" class="text-[9px] text-t3 mb-2">{{ subtitle }}</div>
    </div>
    <div class="flex-1 flex flex-col gap-2 overflow-y-auto overflow-x-hidden px-4">
      <slot />
    </div>
  </div>
</template>
