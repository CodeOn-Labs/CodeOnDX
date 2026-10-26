<script setup lang="ts">
// Port InsightSkeleton / InsightSkeletonList: khung chờ có cùng hình dạng thẻ InsightAlert, tránh giật bố cục.
withDefaults(defineProps<{ count?: number }>(), { count: 3 });
</script>

<template>
  <div class="flex flex-col gap-3">
    <div v-for="i in count" :key="i" class="rounded-xl border border-b2/60 bg-white shadow-[0_2px_8px_rgba(0,0,0,0.02)] p-3 animate-pulse">
      <div class="flex items-center gap-2 mb-1.5">
        <div class="w-1.5 h-1.5 rounded-full bg-bg3 shrink-0" />
        <div class="h-3 w-1/2 bg-bg2 rounded" />
      </div>
      <div class="space-y-1.5 ml-3.5">
        <div class="h-2.5 w-full bg-bg2 rounded opacity-80" />
        <div class="h-2.5 w-3/4 bg-bg2 rounded opacity-60" />
      </div>
    </div>
  </div>
</template>
