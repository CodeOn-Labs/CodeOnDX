<script setup lang="ts">
// Port từ AnimatedCounter.tsx: số chạy từ 0 đến giá trị khi phần tử xuất hiện trong khung nhìn.
// framer-motion spring (stiffness 60, damping 20) được xấp xỉ bằng easeOutCubic trong `duration` giây.
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(
  defineProps<{
    value: number;
    duration?: number;
    delay?: number;
    class?: string;
    formatter?: (value: number) => string | number;
  }>(),
  { duration: 1.2, delay: 0, class: '', formatter: undefined },
);

const el = ref<HTMLElement | null>(null);
const current = ref(0);
const inView = ref(false);
let observer: IntersectionObserver | null = null;
let raf: number | null = null;
let delayTimer: ReturnType<typeof setTimeout> | null = null;

function animateTo(target: number) {
  if (raf) cancelAnimationFrame(raf);
  const from = current.value;
  const start = performance.now();
  const total = props.duration * 1000;
  const step = (now: number) => {
    const t = Math.min(1, (now - start) / total);
    const eased = 1 - Math.pow(1 - t, 3);
    current.value = from + (target - from) * eased;
    if (t < 1) raf = requestAnimationFrame(step);
  };
  raf = requestAnimationFrame(step);
}

function run() {
  if (delayTimer) clearTimeout(delayTimer);
  delayTimer = setTimeout(() => animateTo(props.value), props.delay * 1000);
}

onMounted(() => {
  if (!el.value || typeof IntersectionObserver === 'undefined') {
    inView.value = true;
    run();
    return;
  }
  observer = new IntersectionObserver(
    (entries) => {
      if (entries.some((e) => e.isIntersecting)) {
        inView.value = true;
        run();
        observer?.disconnect();
      }
    },
    { rootMargin: '-20px' },
  );
  observer.observe(el.value);
});

watch(
  () => props.value,
  () => {
    if (inView.value) run();
  },
);

onBeforeUnmount(() => {
  observer?.disconnect();
  if (raf) cancelAnimationFrame(raf);
  if (delayTimer) clearTimeout(delayTimer);
});

const display = computed(() => (props.formatter ? String(props.formatter(current.value)) : String(Math.round(current.value))));
</script>

<template>
  <span
    ref="el"
    :class="cn('tabular-nums inline-block transition-[opacity,transform] duration-[400ms] ease-out', inView ? 'opacity-100 scale-100' : 'opacity-0 scale-90', props.class)"
  >
    {{ display }}
  </span>
</template>
