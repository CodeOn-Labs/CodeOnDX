<script setup lang="ts">
// Port ResizeHandle (Separator của react-resizable-panels): thanh kéo đổi độ rộng panel.
// Phát sự kiện `resize` với độ dịch chuyển ngang (px) mỗi lần kéo; bàn phím ←/→ dịch 16px.
import { ref } from 'vue';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(defineProps<{ class?: string; label?: string }>(), { class: '', label: undefined });
const emit = defineEmits<{ resize: [deltaX: number]; resizeStart: []; resizeEnd: [] }>();

const dragging = ref(false);
let lastX = 0;

function onPointerDown(event: PointerEvent) {
  if (event.button !== 0) return;
  dragging.value = true;
  lastX = event.clientX;
  (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  document.body.style.cursor = 'col-resize';
  document.body.style.userSelect = 'none';
  emit('resizeStart');
}

function onPointerMove(event: PointerEvent) {
  if (!dragging.value) return;
  const delta = event.clientX - lastX;
  if (delta !== 0) {
    lastX = event.clientX;
    emit('resize', delta);
  }
}

function onPointerUp(event: PointerEvent) {
  if (!dragging.value) return;
  dragging.value = false;
  (event.currentTarget as HTMLElement).releasePointerCapture(event.pointerId);
  document.body.style.cursor = '';
  document.body.style.userSelect = '';
  emit('resizeEnd');
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'ArrowLeft') emit('resize', -16);
  else if (event.key === 'ArrowRight') emit('resize', 16);
  else return;
  event.preventDefault();
}
</script>

<template>
  <div
    role="separator"
    aria-orientation="vertical"
    :aria-label="props.label"
    tabindex="0"
    :data-separator="dragging ? 'active' : 'inactive'"
    :class="cn(
      'relative flex w-1.5 items-center justify-center bg-transparent transition-all hover:bg-black/5 active:bg-black/10 cursor-col-resize select-none group/handle touch-none outline-none focus-visible:bg-black/5',
      props.class,
    )"
    @pointerdown="onPointerDown"
    @pointermove="onPointerMove"
    @pointerup="onPointerUp"
    @pointercancel="onPointerUp"
    @keydown="onKeydown"
  >
    <!-- Đường giữa -->
    <div class="absolute inset-y-0 w-[1px] bg-border-b1 group-hover/handle:bg-bl/50 group-active/handle:bg-bl transition-colors duration-150" />
    <!-- Núm kéo (hiện khi rê chuột) -->
    <div class="z-10 flex h-6 w-3 items-center justify-center rounded bg-white border border-b2 shadow-sm opacity-0 group-hover/handle:opacity-100 group-active/handle:opacity-100 transition-opacity duration-150 pointer-events-none">
      <div class="flex flex-col gap-[3px]">
        <div class="w-1 h-1 rounded-full bg-t3/40" />
        <div class="w-1 h-1 rounded-full bg-t3/40" />
        <div class="w-1 h-1 rounded-full bg-t3/40" />
      </div>
    </div>
  </div>
</template>
