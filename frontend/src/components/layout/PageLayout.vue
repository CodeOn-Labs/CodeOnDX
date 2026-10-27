<script setup lang="ts">
// Port PageLayout.tsx — khung trang với 2 bố cục (đã bỏ Chat AI nên không còn bố cục B / D của bản gốc):
//   A: nội dung + Insight      C: chỉ nội dung (mặc định)
// Insight chỉ hiện từ màn hình xl (≥1280px); panel thu gọn nằm ở dải dọc bên phải.
// Thay react-resizable-panels bằng panel độ rộng px + ResizeHandle (giữ min/max như bản gốc, nội dung chính ≥ 30%).
import { computed, ref } from 'vue';
import { useMediaQuery } from '@vueuse/core';
import { Activity, ChevronRight } from '@lucide/vue';
import ResizeHandle from './ResizeHandle.vue';
import InsightPanel from '@/shared/components/insight/InsightPanel.vue';
import PortalTooltip from '@/shared/components/PortalTooltip.vue';
import DynamicInsights from '@/modules/ai/components/DynamicInsights.vue';
import type { ScreenKey } from '@/app/screens';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(
  defineProps<{
    layout?: 'A' | 'C';
    insightTitle?: string;
    insightSubtitle?: string;
    /** Màn lấy Insight khi không truyền slot #insights (GET /ai/insights?screen=…). */
    insightScreen?: ScreenKey;
    contentPadding?: string;
    insightsWidth?: string;
    /** Tắt cuộn vùng nội dung chính */
    disableScroll?: boolean;
  }>(),
  {
    layout: 'C',
    insightTitle: undefined,
    insightSubtitle: undefined,
    insightScreen: undefined,
    contentPadding: '16px',
    insightsWidth: '180px',
    disableScroll: false,
  },
);

const isXL = useMediaQuery('(min-width: 1280px)');
const showInsight = computed(() => props.layout === 'A' && isXL.value);

const insightCollapsed = ref(false);
const animating = ref(false);

const INSIGHT_LIMITS = { min: 100, max: 450 };
const MAIN_MIN_RATIO = 0.3;

const px = (value: string, fallback: number) => {
  const n = Number.parseFloat(value);
  return Number.isFinite(n) ? n : fallback;
};
const insightWidth = ref(px(props.insightsWidth, 180));
const containerRef = ref<HTMLElement | null>(null);

function setCollapsed(collapsed: boolean) {
  animating.value = true;
  insightCollapsed.value = collapsed;
  setTimeout(() => (animating.value = false), 300);
}

/** Kéo thanh chia: panel Insight rộng thêm khi kéo sang trái; giữ trong khoảng min/max và nội dung chính ≥ 30%. */
function resizeInsight(deltaX: number) {
  const container = containerRef.value?.clientWidth ?? window.innerWidth;
  const maxByMain = container * (1 - MAIN_MIN_RATIO);
  insightWidth.value = Math.round(Math.max(INSIGHT_LIMITS.min, Math.min(insightWidth.value - deltaX, INSIGHT_LIMITS.max, maxByMain)));
}

const panelTransition = computed(() => animating.value && 'transition-all duration-300 ease-in-out');
</script>

<template>
  <!-- Không có panel phụ: bố cục chuẩn -->
  <div v-if="!showInsight" class="flex h-full w-full min-h-0 bg-bg1">
    <div :class="cn('min-w-0 flex-1', props.disableScroll ? 'overflow-hidden' : 'overflow-y-auto')" :style="{ padding: props.contentPadding }">
      <div :class="cn('flex flex-col', props.disableScroll ? 'h-full gap-0' : 'min-h-full gap-4')">
        <slot />
      </div>
    </div>
  </div>

  <div v-else class="relative flex h-full w-full min-h-0 bg-bg1">
    <div ref="containerRef" class="flex h-full w-full min-w-0 overflow-hidden">
      <!-- Nội dung chính -->
      <div :class="cn('min-w-0 flex-1 basis-0', panelTransition)" style="min-width: 30%">
        <div :class="cn('w-full h-full min-w-0', props.disableScroll ? 'overflow-hidden' : 'overflow-y-auto')" :style="{ padding: props.contentPadding }">
          <div :class="cn('flex flex-col', props.disableScroll ? 'h-full gap-0' : 'min-h-full gap-4')">
            <slot />
          </div>
        </div>
      </div>

      <!-- Insight -->
      <template v-if="!insightCollapsed">
        <ResizeHandle :label="$t('component.page_layout.tip_resize_insight')" @resize="resizeInsight" />
        <div :class="cn('shrink-0 overflow-hidden', panelTransition)" :style="{ width: `${insightWidth}px` }">
          <div class="relative w-full h-full min-w-0 flex">
            <InsightPanel :title="props.insightTitle" :subtitle="props.insightSubtitle" width="100%">
              <slot name="insights">
                <DynamicInsights v-if="props.insightScreen" :screen="props.insightScreen" />
              </slot>
            </InsightPanel>

            <PortalTooltip side="left" arrow-border-color="#e2e8f0">
              <button
                type="button"
                class="absolute top-2 right-2 p-1 hover:bg-black/5 text-t3 hover:text-t1 rounded-md transition-colors"
                :aria-label="$t('component.page_layout.tip_collapse_insight')"
                @click="setCollapsed(true)"
              >
                <ChevronRight :size="16" />
              </button>
              <template #content>
                <div class="px-2.5 py-1.5 bg-white text-slate-800 text-[10px] font-bold rounded-lg shadow-md border border-slate-200 leading-none">
                  {{ $t('component.page_layout.tip_collapse_insight') }}
                </div>
              </template>
            </PortalTooltip>
          </div>
        </div>
      </template>
    </div>

    <!-- Dải dọc chứa panel Insight đang thu gọn -->
    <div
      v-if="insightCollapsed"
      class="w-10 h-full border-l border-b1 bg-white/90 backdrop-blur-md flex flex-col items-center py-4 gap-6 shrink-0 z-40 shadow-[-2px_0_8px_rgba(0,0,0,0.02)] select-none animate-in slide-in-from-right duration-200"
    >
      <PortalTooltip side="left" arrow-border-color="#e2e8f0">
        <button
          type="button"
          class="p-2 text-t3 hover:text-bl hover:bg-bl/5 rounded-xl transition-all active:scale-95 flex flex-col items-center gap-2 group/dock-btn"
          :aria-label="$t('component.page_layout.tip_open_insight')"
          @click="setCollapsed(false)"
        >
          <Activity :size="16" class="text-am group-hover/dock-btn:animate-pulse" />
          <span class="text-[9px] font-bold uppercase tracking-wider select-none text-t3 group-hover/dock-btn:text-bl whitespace-nowrap rotate-180" style="writing-mode: vertical-rl">
            {{ $t('component.page_layout.lbl_insights') }}
          </span>
        </button>
        <template #content>
          <div class="px-2.5 py-1.5 bg-white text-slate-800 text-[10px] font-bold rounded-lg shadow-md border border-slate-200 leading-none">
            {{ $t('component.page_layout.tip_open_insight') }}
          </div>
        </template>
      </PortalTooltip>
    </div>
  </div>
</template>
