<script setup lang="ts">
// Port từ shared/components/PortalTooltip.tsx: tooltip render vào body, tự lật phía khi sát mép màn hình,
// hiện sau 80ms, ẩn sau 100ms, có mũi tên. Nội dung truyền qua prop `content` (chuỗi) hoặc slot #content.
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useSlots, type CSSProperties } from 'vue';

type TooltipSide = 'top' | 'bottom' | 'left' | 'right';

const props = withDefaults(
  defineProps<{
    content?: string;
    side?: TooltipSide;
    offset?: number;
    contentClass?: string;
    showArrow?: boolean;
    arrowBg?: string;
    arrowBorderColor?: string;
    disabled?: boolean;
  }>(),
  {
    content: '',
    side: 'top',
    offset: 8,
    contentClass: '',
    showArrow: true,
    arrowBg: '#ffffff',
    arrowBorderColor: '#fecdd3',
    disabled: false,
  },
);

const slots = useSlots();
const ARROW = 5;

const wrapperRef = ref<HTMLElement | null>(null);
const tooltipRef = ref<HTMLElement | null>(null);
const mounted = ref(false);
const shown = ref(false);
const pos = ref({ top: -9999, left: -9999 });
const activeSide = ref<TooltipSide>(props.side);

let showTimer: ReturnType<typeof setTimeout> | null = null;
let hideTimer: ReturnType<typeof setTimeout> | null = null;
let rafId: number | null = null;

function triggerElement(): HTMLElement | null {
  const wrapper = wrapperRef.value;
  return (wrapper?.firstElementChild as HTMLElement | null) ?? wrapper;
}

function computePosition() {
  const triggerEl = triggerElement();
  if (!triggerEl || !tooltipRef.value) return null;
  const trigger = triggerEl.getBoundingClientRect();
  const tooltip = tooltipRef.value.getBoundingClientRect();
  const vw = window.innerWidth;
  const vh = window.innerHeight;
  const gap = props.offset + (props.showArrow ? ARROW : 0);

  let effectiveSide: TooltipSide = props.side;
  if (props.side === 'top' && trigger.top < tooltip.height + gap + 8) effectiveSide = 'bottom';
  else if (props.side === 'bottom' && trigger.bottom + tooltip.height + gap + 8 > vh) effectiveSide = 'top';
  else if (props.side === 'left' && trigger.left < tooltip.width + gap + 8) effectiveSide = 'right';
  else if (props.side === 'right' && trigger.right + tooltip.width + gap + 8 > vw) effectiveSide = 'left';

  let top = 0;
  let left = 0;
  switch (effectiveSide) {
    case 'top':
      top = trigger.top + window.scrollY - tooltip.height - gap;
      left = trigger.left + window.scrollX + trigger.width / 2 - tooltip.width / 2;
      break;
    case 'bottom':
      top = trigger.bottom + window.scrollY + gap;
      left = trigger.left + window.scrollX + trigger.width / 2 - tooltip.width / 2;
      break;
    case 'left':
      top = trigger.top + window.scrollY + trigger.height / 2 - tooltip.height / 2;
      left = trigger.left + window.scrollX - tooltip.width - gap;
      break;
    case 'right':
      top = trigger.top + window.scrollY + trigger.height / 2 - tooltip.height / 2;
      left = trigger.right + window.scrollX + gap;
      break;
  }
  left = Math.max(8, Math.min(left, vw - tooltip.width - 8));
  top = Math.max(8, Math.min(top, vh + window.scrollY - tooltip.height - 8));
  return { top, left, effectiveSide };
}

function clearTimers() {
  if (showTimer) clearTimeout(showTimer);
  if (hideTimer) clearTimeout(hideTimer);
  if (rafId) cancelAnimationFrame(rafId);
}

function onEnter() {
  if (props.disabled) return;
  if (hideTimer) clearTimeout(hideTimer);
  showTimer = setTimeout(async () => {
    mounted.value = true;
    await nextTick();
    // RAF 1: đo kích thước → tính vị trí (vẫn ẩn); RAF 2: bật transition từ đúng vị trí
    rafId = requestAnimationFrame(() => {
      const result = computePosition();
      if (result) {
        pos.value = { top: result.top, left: result.left };
        activeSide.value = result.effectiveSide;
      }
      rafId = requestAnimationFrame(() => {
        shown.value = true;
      });
    });
  }, 80);
}

function onLeave() {
  if (showTimer) clearTimeout(showTimer);
  if (rafId) cancelAnimationFrame(rafId);
  hideTimer = setTimeout(() => {
    shown.value = false;
    mounted.value = false;
    pos.value = { top: -9999, left: -9999 };
  }, 100);
}

// Gắn sự kiện thẳng vào phần tử kích hoạt (con đầu tiên của slot), vì span bọc ngoài dùng display: contents
let boundTrigger: HTMLElement | null = null;
onMounted(() => {
  boundTrigger = triggerElement();
  boundTrigger?.addEventListener('mouseenter', onEnter);
  boundTrigger?.addEventListener('mouseleave', onLeave);
});

onBeforeUnmount(() => {
  clearTimers();
  boundTrigger?.removeEventListener('mouseenter', onEnter);
  boundTrigger?.removeEventListener('mouseleave', onLeave);
});

const tooltipStyle = computed<CSSProperties>(() => ({
  position: 'absolute',
  top: `${pos.value.top}px`,
  left: `${pos.value.left}px`,
  zIndex: 99999,
  opacity: shown.value ? 1 : 0,
  transform: shown.value ? 'scale(1)' : 'scale(0.94)',
  transformOrigin:
    activeSide.value === 'top' ? 'center bottom' : activeSide.value === 'bottom' ? 'center top' : activeSide.value === 'left' ? 'right center' : 'left center',
  transition: shown.value ? 'opacity 0.13s ease-out, transform 0.13s ease-out' : 'none',
  isolation: 'isolate',
}));

const arrowStyle = computed<CSSProperties>(() => {
  const base: CSSProperties = { position: 'absolute', width: `${ARROW * 2}px`, height: `${ARROW * 2}px`, background: props.arrowBg };
  const border = `1px solid ${props.arrowBorderColor}`;
  switch (activeSide.value) {
    case 'top':
      return { ...base, bottom: `-${ARROW}px`, left: '50%', transform: 'translateX(-50%) rotate(45deg)', borderRight: border, borderBottom: border };
    case 'bottom':
      return { ...base, top: `-${ARROW}px`, left: '50%', transform: 'translateX(-50%) rotate(45deg)', borderLeft: border, borderTop: border };
    case 'left':
      return { ...base, right: `-${ARROW}px`, top: '50%', transform: 'translateY(-50%) rotate(45deg)', borderTop: border, borderRight: border };
    default:
      return { ...base, left: `-${ARROW}px`, top: '50%', transform: 'translateY(-50%) rotate(45deg)', borderBottom: border, borderLeft: border };
  }
});

const hasContent = computed(() => Boolean(props.content) || Boolean(slots.content));
</script>

<template>
  <span ref="wrapperRef" class="contents">
    <slot />
  </span>
  <Teleport v-if="mounted && hasContent" to="body">
    <div
      ref="tooltipRef"
      :class="['pointer-events-auto', props.contentClass]"
      :style="tooltipStyle"
      @mouseenter="onEnter"
      @mouseleave="onLeave"
    >
      <slot name="content">{{ props.content }}</slot>
      <div v-if="props.showArrow" :style="arrowStyle" />
    </div>
  </Teleport>
</template>
