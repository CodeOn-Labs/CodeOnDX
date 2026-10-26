<script setup lang="ts">
// Port SideChatPanel.tsx: khung Chat AI cạnh màn hình (nhiều phiên, gợi ý câu hỏi, mở rộng toàn màn hình).
import { computed, nextTick, onBeforeUnmount, onMounted, ref, toRef, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { onClickOutside } from '@vueuse/core';
import { ArrowDown, Brain, ChevronDown, ChevronRight, Maximize2, Plus, Send, Sparkles, X } from '@lucide/vue';
import PortalTooltip from '@/shared/components/PortalTooltip.vue';
import MessageBubble from './MessageBubble.vue';
import ChatFullOverlay from './ChatFullOverlay.vue';
import { useSideChatSessions } from '@/modules/ai/composables/useSideChatSessions';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(
  defineProps<{
    width?: string;
    module?: string;
    contextMeta?: Record<string, string>;
    /** Hiện nút thu gọn (PageLayout lắng nghe sự kiện collapse) */
    collapsible?: boolean;
  }>(),
  { width: '280px', module: 'general', contextMeta: undefined, collapsible: false },
);
const emit = defineEmits<{ collapse: [] }>();
const { t } = useI18n();

const moduleRef = toRef(props, 'module');
const chat = useSideChatSessions(moduleRef);
const { sessions, activeSessionId, activeSession, messages, input, processing } = chat;

/** Gợi ý câu hỏi + placeholder theo module (port AI_CHAT_CONFIGS). */
const moduleConfig = computed(() => {
  type ChatConfig = { tags: string[]; placeholder: string };
  const configs: { general: ChatConfig } & Record<string, ChatConfig> = {
    general: {
      tags: [t('component.side_chat_panel.opt_general_summary'), t('component.side_chat_panel.opt_general_available_staff'), t('component.side_chat_panel.opt_general_urgent_projects')],
      placeholder: t('component.side_chat_panel.ph_general'),
    },
    dashboard: {
      tags: [t('component.side_chat_panel.opt_dashboard_low_utilization'), t('component.side_chat_panel.opt_dashboard_support_needed'), t('component.side_chat_panel.opt_dashboard_weekly_summary')],
      placeholder: t('component.side_chat_panel.ph_dashboard'),
    },
    projects: {
      tags: [t('component.side_chat_panel.opt_projects_most_delayed'), t('component.side_chat_panel.opt_projects_budget_vs_cost'), t('component.side_chat_panel.opt_projects_understaffed')],
      placeholder: t('component.side_chat_panel.ph_projects'),
    },
    project_detail: {
      tags: [t('component.side_chat_panel.opt_project_detail_fte_gap'), t('component.side_chat_panel.opt_project_detail_key_member'), t('component.side_chat_panel.opt_project_detail_delay_risk')],
      placeholder: t('component.side_chat_panel.ph_project_detail'),
    },
    hr: {
      tags: [t('component.side_chat_panel.opt_hr_react_skill'), t('component.side_chat_panel.opt_hr_senior_java_available'), t('component.side_chat_panel.opt_hr_bench_list')],
      placeholder: t('component.side_chat_panel.ph_hr'),
    },
    hr_detail: {
      tags: [t('component.side_chat_panel.opt_hr_detail_summary'), t('component.side_chat_panel.opt_hr_detail_allocation'), t('component.side_chat_panel.opt_hr_detail_growth')],
      placeholder: t('component.side_chat_panel.ph_hr_detail'),
    },
    timesheet: {
      tags: [t('component.side_chat_panel.opt_timesheet_anomaly'), t('component.side_chat_panel.opt_timesheet_missing_days'), t('component.side_chat_panel.opt_timesheet_task_split')],
      placeholder: t('component.side_chat_panel.ph_timesheet'),
    },
    rules: {
      tags: [t('component.side_chat_panel.opt_rules_timesheet'), t('component.side_chat_panel.opt_rules_project')],
      placeholder: t('component.side_chat_panel.ph_rules'),
    },
  };
  return configs[props.module] ?? configs.general;
});

// ─── Danh sách phiên ────────────────────────────────────────────────────────
const showSessionList = ref(false);
const sessionListRef = ref<HTMLElement | null>(null);
onClickOutside(sessionListRef, () => (showSessionList.value = false));

// ─── Gợi ý câu hỏi (ghi nhớ ẩn/hiện) ────────────────────────────────────────
const TAGS_KEY = 'dx_chat_show_tags';
function readShowTags() {
  try {
    return localStorage.getItem(TAGS_KEY) !== 'false';
  } catch {
    return true;
  }
}
const showTags = ref(readShowTags());
watch(showTags, (v) => {
  try {
    localStorage.setItem(TAGS_KEY, String(v));
  } catch {
    // bỏ qua
  }
});

// ─── Cuộn ───────────────────────────────────────────────────────────────────
const scrollRef = ref<HTMLElement | null>(null);
const inputRef = ref<HTMLTextAreaElement | null>(null);
const showScrollButton = ref(false);
let stickToBottom = true;
const THRESHOLD = 80;

function onScroll() {
  const el = scrollRef.value;
  if (!el) return;
  const atBottom = el.scrollTop + el.clientHeight >= el.scrollHeight - THRESHOLD;
  stickToBottom = atBottom;
  showScrollButton.value = !atBottom;
}

function scrollToBottom(smooth = true) {
  const el = scrollRef.value;
  if (!el) return;
  stickToBottom = true;
  showScrollButton.value = false;
  el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
}

watch(
  () => [messages.value.length, processing.value],
  async () => {
    await nextTick();
    if (stickToBottom) scrollToBottom(false);
  },
);

watch(activeSessionId, async () => {
  await nextTick();
  inputRef.value?.focus();
  scrollToBottom(false);
});

onMounted(() => {
  inputRef.value?.focus();
  scrollToBottom(false);
});
onBeforeUnmount(() => (showSessionList.value = false));

function onInputKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault();
    void chat.send(input.value);
  }
}

const fullOpen = ref(false);
</script>

<template>
  <div class="bg-white border-l border-b1 flex flex-col h-full shadow-sm" :style="{ width: props.width }">
    <header class="p-4 border-b border-b2 bg-white flex items-center justify-between gap-2">
      <div class="flex items-center gap-1.5 min-w-0 flex-1">
        <div class="w-6 h-6 rounded-lg bg-gradient-to-br from-bl to-pr flex items-center justify-center shrink-0">
          <Brain :size="14" class="text-white" />
        </div>

        <!-- Chọn phiên -->
        <div ref="sessionListRef" class="relative">
          <button
            type="button"
            :disabled="processing"
            class="flex items-center gap-1 text-bl font-bold text-sm truncate max-w-[120px] hover:bg-bl/5 rounded-md px-1.5 py-0.5 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            @click="showSessionList = !showSessionList"
          >
            <span class="truncate">{{ activeSession?.title || 'Chat' }}</span>
            <ChevronDown :size="12" :class="cn('shrink-0 transition-transform', showSessionList && 'rotate-180')" />
          </button>
          <div v-if="showSessionList" class="absolute top-full left-0 mt-1 w-48 bg-white border border-b1 rounded-xl shadow-xl z-50 py-1 max-h-48 overflow-y-auto">
            <div
              v-for="s in sessions"
              :key="s.id"
              :class="cn(
                'group flex items-center justify-between px-3 py-2 text-[12px] transition-colors',
                processing ? 'cursor-not-allowed opacity-50' : 'cursor-pointer',
                activeSessionId === s.id ? 'bg-blbg text-blt font-bold' : 'text-t2 hover:bg-bg2',
              )"
              @click="chat.selectSession(s.id); showSessionList = false"
            >
              <span class="truncate flex-1">{{ s.title }}</span>
              <button
                type="button"
                class="ml-1 p-0.5 opacity-0 group-hover:opacity-100 transition-opacity text-rd hover:text-rdt rounded shrink-0"
                :aria-label="$t('component.side_chat_panel.btn_delete_session')"
                :title="$t('component.side_chat_panel.btn_delete_session')"
                @click.stop="chat.deleteSession(s.id)"
              >
                <X :size="10" />
              </button>
            </div>
          </div>
        </div>

        <!-- Tạo chat mới -->
        <PortalTooltip side="bottom" arrow-border-color="#e2e8f0">
          <button
            type="button"
            :disabled="processing"
            class="h-6 w-6 flex items-center justify-center rounded-md text-bl hover:bg-bl/5 transition-all active:scale-90 shrink-0 disabled:opacity-30 disabled:cursor-not-allowed"
            :aria-label="$t('component.side_chat_panel.btn_new_session')"
            @click="chat.newSession()"
          >
            <Plus :size="14" />
          </button>
          <template #content>
            <div class="px-2.5 py-1.5 bg-white text-slate-800 text-[10px] font-bold rounded-lg shadow-md border border-slate-200 leading-none">
              {{ processing ? $t('component.side_chat_panel.tip_wait_response') : $t('component.side_chat_panel.btn_new_session') }}
            </div>
          </template>
        </PortalTooltip>
      </div>

      <div class="flex items-center gap-1 shrink-0">
        <PortalTooltip side="left" arrow-border-color="#e2e8f0">
          <button
            type="button"
            class="h-6 w-6 flex items-center justify-center rounded-md text-t3 hover:text-bl hover:bg-bl/5 transition-all active:scale-90"
            :aria-label="$t('component.side_chat_panel.btn_open_full')"
            @click="fullOpen = true"
          >
            <Maximize2 :size="13" />
          </button>
          <template #content>
            <div class="px-2.5 py-1.5 bg-white text-slate-800 text-[10px] font-bold rounded-lg shadow-md border border-slate-200 leading-none">
              {{ $t('component.side_chat_panel.btn_open_full') }}
            </div>
          </template>
        </PortalTooltip>

        <PortalTooltip v-if="props.collapsible" side="left" arrow-border-color="#e2e8f0">
          <button
            type="button"
            class="h-6 w-6 flex items-center justify-center rounded-md text-t3 hover:text-bl hover:bg-bl/5 transition-all active:scale-90"
            :aria-label="$t('component.side_chat_panel.btn_collapse')"
            @click="emit('collapse')"
          >
            <ChevronRight :size="15" />
          </button>
          <template #content>
            <div class="px-2.5 py-1.5 bg-white text-slate-800 text-[10px] font-bold rounded-lg shadow-md border border-slate-200 leading-none">
              {{ $t('component.side_chat_panel.btn_collapse') }}
            </div>
          </template>
        </PortalTooltip>
      </div>
    </header>

    <main ref="scrollRef" class="flex-1 overflow-y-auto p-4 flex flex-col gap-4 bg-[#FCFDFF] relative" @scroll.passive="onScroll">
      <MessageBubble v-for="m in messages" :key="m.id" :message="m" variant="side" />
      <div v-if="processing" class="flex flex-col items-start w-full">
        <div class="max-w-[92%] px-3 py-2 rounded-2xl shadow-sm bg-white border border-b2 rounded-tl-none">
          <div class="text-[11px] text-t3 animate-pulse">{{ $t('component.side_chat_panel.msg_thinking') }}</div>
        </div>
      </div>
      <button
        v-if="showScrollButton"
        type="button"
        class="absolute bottom-4 right-4 z-20 h-7 w-7 rounded-full border border-b1 bg-white text-t3 shadow-md flex items-center justify-center hover:text-bl hover:bg-bg transition-colors active:scale-95"
        :aria-label="$t('component.side_chat_panel.btn_scroll_bottom')"
        @click="scrollToBottom()"
      >
        <ArrowDown :size="14" />
      </button>
    </main>

    <section class="px-3 py-2 bg-[#FCFDFF] border-t border-b1">
      <div class="mb-2 flex items-center justify-between">
        <span class="text-[10px] font-bold uppercase tracking-wide text-t3">{{ $t('component.side_chat_panel.lbl_suggestions') }}</span>
        <button
          type="button"
          class="h-6 w-6 rounded-full border border-b2 bg-white text-t3 shadow-sm transition-all hover:border-bl hover:bg-bg hover:text-bl active:scale-95 disabled:opacity-40"
          :aria-expanded="showTags"
          :aria-label="showTags ? $t('component.side_chat_panel.btn_hide_suggestions') : $t('component.side_chat_panel.btn_show_suggestions')"
          @click="showTags = !showTags"
        >
          <ChevronDown :size="14" :class="cn('mx-auto transition-transform duration-300 ease-out', showTags && 'rotate-180')" />
        </button>
      </div>
      <div v-if="showTags" class="flex flex-wrap gap-1.5 max-h-20 overflow-y-auto">
        <button
          v-for="(tag, idx) in moduleConfig.tags"
          :key="idx"
          type="button"
          :disabled="processing"
          class="px-2 py-1 bg-white border border-b2 rounded-full text-[10px] text-t2 hover:border-bl hover:text-bl transition-all whitespace-nowrap flex items-center gap-1 shadow-sm active:scale-95 disabled:opacity-50"
          @click="chat.send(tag)"
        >
          <Sparkles :size="8" class="text-am" />
          {{ tag }}
        </button>
      </div>
    </section>

    <footer class="p-3 border-t border-b1 bg-white shadow-[0_-4px_12px_rgba(0,0,0,0.02)]">
      <div class="relative group">
        <textarea
          ref="inputRef"
          v-model="input"
          rows="2"
          class="w-full resize-none py-2 pr-10 pl-3 text-[12px] bg-bg rounded-xl border border-transparent focus:border-bl/30 transition-all font-medium focus:outline-none disabled:opacity-40"
          :placeholder="moduleConfig.placeholder"
          :disabled="processing"
          @keydown="onInputKeydown"
        />
        <button
          type="button"
          class="absolute right-2 bottom-2.5 h-6 w-6 flex items-center justify-center text-bl hover:bg-bl hover:text-white rounded-md transition-all disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-bl"
          :disabled="!input.trim() || processing"
          :aria-label="$t('component.side_chat_panel.btn_send')"
          @click="chat.send(input)"
        >
          <Send :size="12" />
        </button>
      </div>
      <p class="text-[9px] text-t3 mt-2 text-center opacity-60 italic">{{ $t('component.side_chat_panel.hint_ai_may_be_wrong') }}</p>
    </footer>

    <ChatFullOverlay
      v-model:open="fullOpen"
      v-model:input="input"
      :sessions="sessions"
      :active-session-id="activeSessionId"
      :messages="messages"
      :processing="processing"
      :tags="moduleConfig.tags"
      :placeholder="moduleConfig.placeholder"
      @select="chat.selectSession"
      @send="chat.send"
      @new-session="chat.newSession()"
      @delete-session="chat.deleteSession"
    />
  </div>
</template>
