<script setup lang="ts">
// Port ChatAIFull.tsx (+ useFullChat): trang Chat AI toàn màn hình — lịch sử phiên bên trái, hội thoại + gợi ý câu hỏi bên phải.
// Mọi lời gọi LLM qua BE (D5: POST /api/v1/ai/chat, khoá AI chỉ ở server) nên bỏ cảnh báo "chưa cấu hình API key" ở trình duyệt;
// lỗi cấu hình / kết nối do BE trả về hiện thành tin nhắn lỗi. Phiên chat lưu trong trình duyệt (dùng chung useSideChatSessions, module 'general').
import { computed, nextTick, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { ArrowDown, MessageSquare, Plus, Send, X } from '@lucide/vue';
import MessageBubble from '@/modules/ai/components/MessageBubble.vue';
import { useSideChatSessions } from '@/modules/ai/composables/useSideChatSessions';

const { t } = useI18n();
const chat = useSideChatSessions(ref('general'));
const { sessions, activeSessionId, messages, input, processing } = chat;
const tags = computed(() => [
  { label: t('ai_chat.opt_team_hours'), text: t('ai_chat.opt_team_hours_text') },
  { label: t('ai_chat.opt_new_project'), text: t('ai_chat.opt_new_project_text') },
  { label: t('ai_chat.opt_missing_timesheet'), text: t('ai_chat.opt_missing_timesheet_text') },
  { label: t('ai_chat.opt_monthly_report'), text: t('ai_chat.opt_monthly_report_text') },
]);

const scrollRef = ref<HTMLElement | null>(null);
const showScrollButton = ref(false);
function scrollToBottom(smooth = false) {
  const el = scrollRef.value;
  if (el) el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
}
function onScroll() {
  const el = scrollRef.value;
  if (el) showScrollButton.value = el.scrollTop + el.clientHeight < el.scrollHeight - 80;
}
watch(() => [messages.value.length, processing.value], async () => {
  await nextTick();
  scrollToBottom();
}, { immediate: true });
function send(text: string) {
  if (!text.trim() || processing.value) return;
  void chat.send(text);
}
function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault();
    send(input.value);
  }
}
</script>

<template>
  <div class="flex h-full w-full bg-bg text-sm">
    <aside class="z-20 flex w-[200px] shrink-0 flex-col border-r border-b1 bg-bg1 p-3 pt-4 shadow-sm">
      <div class="mb-6 flex items-center justify-between px-1">
        <div class="flex items-center gap-2">
          <div class="rounded-lg bg-blbg p-1.5 text-blt"><MessageSquare :size="16" /></div>
          <span class="text-[11px] font-bold uppercase tracking-wider text-t1">{{ $t('ai_chat.lbl_history') }}</span>
        </div>
        <button
          type="button"
          :disabled="processing"
          :aria-label="$t('ai_chat.btn_new_session')"
          :title="$t('ai_chat.btn_new_session')"
          class="flex h-7 w-7 items-center justify-center rounded-lg bg-bl text-white shadow-sm transition-all hover:bg-blt active:scale-90 disabled:cursor-not-allowed disabled:opacity-30"
          @click="chat.newSession()"
        ><Plus :size="14" /></button>
      </div>
      <div class="flex flex-col gap-1.5 overflow-y-auto pr-1">
        <div
          v-for="s in sessions"
          :key="s.id"
          :class="[
            'group flex items-center justify-between rounded-xl p-2.5 text-[12px] transition-all',
            processing ? 'cursor-not-allowed opacity-50' : 'cursor-pointer',
            activeSessionId === s.id ? 'border border-bl/10 bg-white font-bold text-blt shadow-md' : 'border border-transparent font-medium text-t2 hover:bg-bg2',
          ]"
          @click="!processing && chat.selectSession(s.id)"
        >
          <div class="flex-1 truncate pl-1 text-left">{{ s.title }}</div>
          <button type="button" :aria-label="$t('ai_chat.btn_delete_session')" :title="$t('ai_chat.btn_delete_session')" class="ml-1 px-1.5 text-rd opacity-0 transition-opacity hover:text-rdt group-hover:opacity-100" @click.stop="chat.deleteSession(s.id)"><X :size="12" /></button>
        </div>
      </div>
    </aside>

    <main class="relative flex h-full flex-1 flex-col overflow-hidden">
      <div class="pointer-events-none absolute inset-0 opacity-[0.03]" style="background-image: radial-gradient(var(--bl) 1px, transparent 0); background-size: 40px 40px" aria-hidden="true" />
      <div ref="scrollRef" class="flex flex-1 flex-col gap-8 overflow-y-auto p-4 md:p-8" @scroll.passive="onScroll">
        <div v-if="messages.length === 0" class="flex flex-1 flex-col items-center justify-center p-10 text-center opacity-50">
          <MessageSquare :size="48" class="mb-4 text-bl" />
          <h3 class="mb-2 text-lg font-bold text-t1">{{ $t('ai_chat.empty_title') }}</h3>
          <p class="max-w-sm text-sm text-t3">{{ $t('ai_chat.empty_hint') }}</p>
        </div>
        <MessageBubble v-for="m in messages" :key="m.id" :message="m" variant="full" />
        <div v-if="processing" class="flex w-full max-w-[85%] flex-col gap-3 self-start rounded-2xl rounded-tl-none border border-b1 bg-white px-4 py-3 shadow-sm">
          <div class="animate-pulse text-sm text-t3">{{ $t('ai_chat.msg_thinking') }}</div>
        </div>
        <div class="h-[120px] shrink-0" />
      </div>

      <div class="absolute bottom-0 left-0 right-0 z-10 border-t border-b1 bg-bg1 p-4 shadow-lg">
        <button v-if="showScrollButton" type="button" class="absolute -top-12 left-1/2 z-20 flex h-8 w-8 -translate-x-1/2 items-center justify-center rounded-full border border-b1 bg-white text-t3 shadow-md transition-colors hover:bg-bg hover:text-bl active:scale-95" :aria-label="$t('ai_chat.btn_scroll_bottom')" @click="scrollToBottom(true)"><ArrowDown :size="16" /></button>
        <div class="relative">
          <textarea
            v-model="input"
            autofocus
            rows="2"
            :disabled="processing"
            :placeholder="$t('ai_chat.ph_input')"
            class="w-full resize-none rounded-xl border border-b1 bg-bg py-3 pl-4 pr-14 text-sm shadow-inner transition-all focus:border-bl/30 focus:bg-white focus:outline-none"
            @keydown="onKeydown"
          />
          <button type="button" :disabled="!input.trim() || processing" :aria-label="$t('ai_chat.btn_send')" class="absolute bottom-3 right-2.5 flex h-8 w-8 items-center justify-center rounded-lg bg-bl font-bold text-white transition-all hover:bg-bl/90 disabled:opacity-50" @click="send(input)"><Send :size="14" /></button>
        </div>
        <div class="no-scrollbar mt-3 flex gap-2 overflow-x-auto pb-1">
          <button v-for="tag in tags" :key="tag.label" type="button" :disabled="processing" class="cursor-pointer whitespace-nowrap rounded-lg border border-b1 bg-white px-3 py-1.5 text-[10px] font-bold text-t2 shadow-sm transition-all hover:bg-bl hover:text-white active:scale-95 disabled:cursor-not-allowed disabled:opacity-50" @click="send(tag.text)">{{ tag.label }}</button>
        </div>
      </div>
    </main>
  </div>
</template>
