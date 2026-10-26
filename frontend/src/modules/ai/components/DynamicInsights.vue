<script setup lang="ts">
// Port DynamicInsights (shared/components/DynamicInsights.tsx).
// Bản gốc tự gom ngữ cảnh + gọi LLM ở trình duyệt; bản mới chỉ gọi GET /api/v1/ai/insights?screen=…
// — BE (hoặc mock API) lo việc chọn cấu hình Insight theo màn/kế thừa màn cha (ADR-0001) và gọi connector AI.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { RefreshCcw, Sparkles } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import InsightAlert from '@/shared/components/insight/InsightAlert.vue';
import InsightSkeletonList from '@/shared/components/insight/InsightSkeletonList.vue';
import { aiApi } from '@/modules/ai/api/ai.api';
import type { ScreenKey } from '@/app/screens';

export interface DynamicInsightsStatus {
  busy: boolean;
  canRefresh: boolean;
}

const props = withDefaults(
  defineProps<{
    screen: ScreenKey;
    /** Ẩn nút "Phân tích lại" khi trang tự đặt nút ở chỗ khác (điều khiển qua refreshSignal). */
    hideRefreshButton?: boolean;
    /** Ẩn khung "Không có thông báo mới từ AI". */
    hideEmptyState?: boolean;
    /** Tăng giá trị = chạy lại phân tích (lần mount đầu không tính). */
    refreshSignal?: number;
  }>(),
  { hideRefreshButton: false, hideEmptyState: false, refreshSignal: 0 },
);
const emit = defineEmits<{ status: [status: DynamicInsightsStatus] }>();

const { locale } = useI18n();
const queryClient = useQueryClient();
const queryKey = computed(() => ['ai', 'insights', props.screen, locale.value] as const);

const query = useQuery({
  queryKey,
  queryFn: ({ signal }) => aiApi.insights(props.screen, false, signal),
  staleTime: 5 * 60_000,
  retry: false,
  meta: { silent: true },
});

const refreshing = ref(false);
const refreshError = ref<Error | null>(null);

async function refresh() {
  refreshing.value = true;
  refreshError.value = null;
  try {
    queryClient.setQueryData(queryKey.value, await aiApi.insights(props.screen, true));
  } catch (err) {
    refreshError.value = err instanceof Error ? err : new Error(String(err));
  } finally {
    refreshing.value = false;
  }
}

const loading = computed(() => query.isPending.value || refreshing.value);
const errorMessage = computed(() => (refreshError.value ?? query.error.value)?.message ?? null);
const items = computed(() => query.data.value?.items ?? []);
const hasContent = computed(() => items.value.length > 0);
const canRefresh = computed(() => !errorMessage.value && (hasContent.value || loading.value));

watch(
  () => ({ busy: loading.value, canRefresh: canRefresh.value }),
  (status, prev) => {
    if (prev && prev.busy === status.busy && prev.canRefresh === status.canRefresh) return;
    emit('status', status);
  },
  { immediate: true },
);

watch(
  () => props.refreshSignal,
  () => void refresh(),
);

defineExpose({ refresh });
</script>

<template>
  <div class="flex flex-col gap-3">
    <div v-if="canRefresh && !props.hideRefreshButton" class="flex items-center justify-end">
      <button
        type="button"
        :disabled="loading"
        class="flex items-center gap-1 px-2 py-1 text-[10px] font-medium text-t3 hover:text-blt hover:bg-blbg/50 rounded-md transition-all disabled:opacity-40 disabled:cursor-not-allowed"
        :title="$t('component.dynamic_insights.btn_reanalyze')"
        @click="refresh"
      >
        <RefreshCcw :class="['w-3 h-3', loading ? 'animate-spin' : '']" />
        {{ loading ? $t('component.dynamic_insights.msg_analyzing') : $t('component.dynamic_insights.btn_reanalyze') }}
      </button>
    </div>

    <InsightSkeletonList v-if="loading" :count="3" />

    <div v-else-if="errorMessage" class="flex flex-col items-center justify-center py-6 px-4 text-center bg-rdbg/10 rounded-xl border border-rdbg/20 mt-2">
      <p class="text-[11px] text-rdt mb-3">{{ errorMessage }}</p>
      <Button variant="outline" size="sm" class="h-7 text-[10px] px-3 bg-white hover:bg-white" @click="refresh">
        <template #leftIcon><RefreshCcw class="w-3 h-3" /></template>
        {{ $t('component.dynamic_insights.btn_try_again') }}
      </Button>
    </div>

    <template v-else-if="!hasContent">
      <div
        v-if="!props.hideEmptyState"
        class="flex flex-col items-center justify-center py-8 px-4 text-center bg-hover/20 rounded-xl border border-dashed border-border/40 mt-2"
      >
        <Sparkles class="w-4 h-4 text-t3/40 mb-2" />
        <p class="text-[11px] text-t3 font-medium">{{ $t('component.dynamic_insights.empty_no_insight') }}</p>
        <p class="text-[10px] text-t3/60 mt-1">{{ $t('component.dynamic_insights.hint_auto_update') }}</p>
      </div>
    </template>

    <template v-else>
      <InsightAlert v-for="ins in items" :key="ins.id" :variant="ins.variant" :title="ins.title" :content="ins.content" />
    </template>
  </div>
</template>
