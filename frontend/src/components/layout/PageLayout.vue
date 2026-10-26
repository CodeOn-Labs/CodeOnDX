<script setup lang="ts">
// Port PageLayout.tsx — khung trang với 4 bố cục:
//   A: nội dung + Insight      B: nội dung + Insight + Chat AI
//   C: chỉ nội dung (mặc định) D: nội dung + Chat AI
// Insight chỉ hiện từ màn hình xl (≥1280px), Chat AI từ lg (≥1024px); panel thu gọn nằm ở dải dọc bên phải.
// Thay react-resizable-panels bằng panel độ rộng px + ResizeHandle (giữ min/max như bản gốc, nội dung chính ≥ 30%).
import { computed, ref } from 'vue';
import { useMediaQuery } from '@vueuse/core';
import { Activity, Brain, ChevronRight } from '@lucide/vue';
import ResizeHandle from './ResizeHandle.vue';
import InsightPanel from '@/shared/components/insight/InsightPanel.vue';
import PortalTooltip from '@/shared/components/PortalTooltip.vue';
import DynamicInsights from '@/modules/ai/components/DynamicInsights.vue';
import SideChatPanel from '@/modules/ai/components/SideChatPanel.vue';
import type { ScreenKey } from '@/app/screens';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(
  defineProps<{
    layout?: 'A' | 'B' | 'C' | 'D';
    insightTitle?: string;
    insightSubtitle?: string;
    /** Màn lấy Insight khi không truyền slot #insights (GET /ai/insights?screen=…). */
    insightScreen?: ScreenKey;
    contentPadding?: string;
    insightsWidth?: string;
    chatWidth?: string;
    /** Ngữ cảnh Chat AI: 'projects' | 'hr' | 'project_detail' | 'general'… */
    aiModule?: string;
    aiContextMeta?: Record<string, string>;
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
    chatWidth: '280px',
    aiModule: 'general',
    aiContextMeta: undefined,
    disableScroll: false,
  },
);

const isDesktop = useMediaQuery('(min-width: 1024px)');
const isXL = useMediaQuery('(min-width: 1280px)');

const showInsight = computed(() => (props.layout === 'A' || props.layout === 'B') && isXL.value);
const showChat = computed(() => (props.layout === 'B' || props.layout === 'D') && isDesktop.value);

const insightCollapsed = ref(false);
const chatCollapsed = ref(true);
const animating = ref(false);

const INSIGHT_LIMITS = { min: 100, max: 450 };
const CHAT_LIMITS = { min: 200, max: 600 };
const MAIN_MIN_RATIO = 0.3;

const px = (value: string, fallback: number) => {
  const n = Number.parseFloat(value);
  return Number.isFinite(n) ? n : fallback;
};
const insightWidth = ref(px(props.insightsWidth, 180));
const chatWidth = ref(px(props.chatWidth, 280));
const containerRef = ref<HTMLElement | null>(null);

function setCollapsed(target: 'insight' | 'chat', collapsed: boolean) {
  animating.value = true;
  if (target === 'insight') insightCollapsed.value = collapsed;
  else chatCollapsed.value = collapsed;
  setTimeout(() => (animating.value = false), 300);
}

/** Kéo thanh chia: panel bên phải rộng thêm khi kéo sang trái; giữ trong khoảng min/max và nội dung chính ≥ 30%. */
function resizePanel(target: 'insight' | 'chat', deltaX: number) {
  const container = containerRef.value?.clientWidth ?? window.innerWidth;
  const other = target === 'insight' ? (showChat.value && !chatCollapsed.value ? chatWidth.value : 0) : showInsight.value && !insightCollapsed.value ? insightWidth.value : 0;
  const limits = target === 'insight' ? INSIGHT_LIMITS : CHAT_LIMITS;
  const current = target === 'insight' ? insightWidth.value : chatWidth.value;
  const maxByMain = container * (1 - MAIN_MIN_RATIO) - other;
  const next = Math.round(Math.max(limits.min, Math.min(current - deltaX, limits.max, maxByMain)));
  if (target === 'insight') insightWidth.value = next;
  else chatWidth.value = next;
}

const hasSidePanels = computed(() => showInsight.value || showChat.value);
const hasCollapsedPanels = computed(() => (showInsight.value && insightCollapsed.value) || (showChat.value && chatCollapsed.value));
const panelTransition = computed(() => animating.value && 'transition-all duration-300 ease-in-out');
</script>

<template>
  <!-- Không có panel phụ: bố cục chuẩn -->
  <div v-if="!hasSidePanels" class="flex h-full w-full min-h-0 bg-bg1">
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
      <template v-if="showInsight && !insightCollapsed">
        <ResizeHandle :label="$t('component.page_layout.tip_resize_insight')" @resize="resizePanel('insight', $event)" />
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
                @click="setCollapsed('insight', true)"
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

      <!-- Chat AI -->
      <template v-if="showChat && !chatCollapsed">
        <ResizeHandle :label="$t('component.page_layout.tip_resize_chat')" @resize="resizePanel('chat', $event)" />
        <div :class="cn('shrink-0 overflow-hidden', panelTransition)" :style="{ width: `${chatWidth}px` }">
          <div class="w-full h-full min-w-0 flex-col flex">
            <SideChatPanel :module="props.aiModule" :context-meta="props.aiContextMeta" width="100%" collapsible @collapse="setCollapsed('chat', true)" />
          </div>
        </div>
      </template>
    </div>

    <!-- Dải dọc chứa các panel đang thu gọn -->
    <div
      v-if="hasCollapsedPanels"
      class="w-10 h-full border-l border-b1 bg-white/90 backdrop-blur-md flex flex-col items-center py-4 gap-6 shrink-0 z-40 shadow-[-2px_0_8px_rgba(0,0,0,0.02)] select-none animate-in slide-in-from-right duration-200"
    >
      <PortalTooltip v-if="showInsight && insightCollapsed" side="left" arrow-border-color="#e2e8f0">
        <button
          type="button"
          class="p-2 text-t3 hover:text-bl hover:bg-bl/5 rounded-xl transition-all active:scale-95 flex flex-col items-center gap-2 group/dock-btn"
          :aria-label="$t('component.page_layout.tip_open_insight')"
          @click="setCollapsed('insight', false)"
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

      <PortalTooltip v-if="showChat && chatCollapsed" side="left" arrow-border-color="#e2e8f0">
        <button
          type="button"
          class="p-2 text-t3 hover:text-bl hover:bg-bl/5 rounded-xl transition-all active:scale-95 flex flex-col items-center gap-2 group/dock-btn"
          :aria-label="$t('component.page_layout.tip_open_chat')"
          @click="setCollapsed('chat', false)"
        >
          <Brain :size="16" class="text-bl" />
          <span class="text-[9px] font-bold uppercase tracking-wider select-none text-t3 group-hover/dock-btn:text-bl whitespace-nowrap rotate-180" style="writing-mode: vertical-rl">
            {{ $t('component.page_layout.lbl_chat_ai') }}
          </span>
        </button>
        <template #content>
          <div class="px-2.5 py-1.5 bg-white text-slate-800 text-[10px] font-bold rounded-lg shadow-md border border-slate-200 leading-none">
            {{ $t('component.page_layout.tip_open_chat') }}
          </div>
        </template>
      </PortalTooltip>
    </div>
  </div>
</template>
