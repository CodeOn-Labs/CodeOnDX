<script setup lang="ts">
// Port từ RouteProgressBar.tsx: thanh tiến trình mảnh chạy mỗi lần đổi route.
import { onBeforeUnmount, ref, watch } from 'vue';
import { useRoute } from 'vue-router';

const route = useRoute();
const progress = ref(0);
const visible = ref(false);
let interval: ReturnType<typeof setInterval> | null = null;
let timer: ReturnType<typeof setTimeout> | null = null;
let hideTimer: ReturnType<typeof setTimeout> | null = null;

function clear() {
  if (interval) clearInterval(interval);
  if (timer) clearTimeout(timer);
  if (hideTimer) clearTimeout(hideTimer);
}

function start() {
  clear();
  progress.value = 0;
  visible.value = true;
  let current = 0;
  interval = setInterval(() => {
    const increment = current < 30 ? 8 : current < 60 ? 4 : current < 80 ? 2 : 0.5;
    current = Math.min(current + increment, 90);
    progress.value = current;
  }, 50);
  timer = setTimeout(() => {
    if (interval) clearInterval(interval);
    progress.value = 100;
    hideTimer = setTimeout(() => {
      visible.value = false;
    }, 400);
  }, 400);
}

watch(() => route.path, start, { immediate: true });
onBeforeUnmount(clear);
</script>

<template>
  <div v-if="visible" class="fixed top-0 left-0 right-0 z-[9999] h-[3px] pointer-events-none" style="background: transparent">
    <div
      class="h-full rounded-full shadow-[0_0_8px_rgba(79,125,249,0.7)]"
      :style="{
        width: `${progress}%`,
        background: 'linear-gradient(90deg, #4f7df9, #7c6ff7, #4f7df9)',
        backgroundSize: '200% 100%',
        transition: progress === 100 ? 'width 0.2s ease-out' : 'width 0.1s linear',
        animation: 'shimmerBar 1.5s linear infinite',
      }"
    />
  </div>
</template>
