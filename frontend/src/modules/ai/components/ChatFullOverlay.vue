<script setup lang="ts">
// Port ChatFullOverlay.tsx: chat AI toàn màn hình (lịch sử phiên bên trái, hội thoại bên phải).
import { nextTick, ref, watch } from 'vue';
import { DialogClose, DialogContent, DialogOverlay, DialogPortal, DialogRoot, DialogTitle } from 'reka-ui';
import { ArrowDown, MessageSquare, Plus, Send, X } from '@lucide/vue';
import MessageBubble from './MessageBubble.vue';
import type { ChatMessage, ChatSession } from '@/modules/ai/composables/useSideChatSessions';

const props = defineProps<{
  sessions: ChatSession[];
  activeSessionId: string;
  messages: ChatMessage[];
  processing: boolean;
  tags: string[];
  placeholder: string;
}>();
const open = defineModel<boolean>('open', { default: false });
const input = defineModel<string>('input', { default: '' });
const emit = defineEmits<{ select: [id: string]; send: [text: string]; newSession: []; deleteSession: [id: string] }>();

const scrollRef = ref<HTMLElement | null>(null);
const inputRef = ref<HTMLTextAreaElement | null>(null);
const showScrollButton = ref(false);

function scrollToBottom(smooth = false) {
  const el = scrollRef.value;
  if (el) el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
}

function onScroll() {
  const el = scrollRef.value;
  if (el) showScrollButton.value = el.scrollTop + el.clientHeight < el.scrollHeight - 80;
}

watch(open, (value) => {
  if (value) {
    setTimeout(() => {
      inputRef.value?.focus();
      scrollToBottom();
    }, 100);
  }
});

watch(
  () => [props.messages.length, props.processing],
  async () => {
    await nextTick();
    scrollToBottom();
  },
);

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault();
    emit('send', input.value);
  }
}
</script>

<template>
  <DialogRoot v-model:open="open">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-[99] bg-black/40 backdrop-blur-sm data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
      <DialogContent
        :aria-describedby="undefined"
        class="fixed inset-0 z-[100] flex w-full h-full bg-bg outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:slide-out-to-bottom-2 data-[state=open]:slide-in-from-bottom-2"
      >
        <DialogTitle class="sr-only">{{ $t('component.chat_full_overlay.title_dialog') }}</DialogTitle>

        <!-- Lịch sử -->
        <aside class="w-[200px] bg-bg1 border-r border-b1 p-3 flex flex-col pt-4 shadow-sm z-20 shrink-0">
          <div class="flex justify-between items-center mb-6 px-1">
            <div class="flex items-center gap-2">
              <div class="p-1.5 bg-blbg text-blt rounded-lg">
                <MessageSquare :size="16" />
              </div>
              <span class="text-[11px] font-bold text-t1 uppercase tracking-wider">{{ $t('component.chat_full_overlay.lbl_history') }}</span>
            </div>
            <button
              type="button"
              :disabled="props.processing"
              :aria-label="$t('component.chat_full_overlay.btn_new_session')"
              :title="$t('component.chat_full_overlay.btn_new_session')"
              class="w-7 h-7 flex items-center justify-center rounded-lg bg-bl text-white hover:bg-blt transition-all shadow-sm active:scale-90 disabled:opacity-30 disabled:cursor-not-allowed"
              @click="emit('newSession')"
            >
              <Plus :size="14" />
            </button>
          </div>
          <div class="flex flex-col gap-1.5 overflow-y-auto pr-1">
            <div
              v-for="s in props.sessions"
              :key="s.id"
              :class="[
                'p-2.5 flex items-center justify-between rounded-xl text-[12px] group transition-all',
                props.processing ? 'cursor-not-allowed opacity-50' : 'cursor-pointer',
                props.activeSessionId === s.id
                  ? 'bg-white border border-bl/10 text-blt font-bold shadow-md'
                  : 'hover:bg-bg2 text-t2 font-medium border border-transparent',
              ]"
              @click="emit('select', s.id)"
            >
              <div class="truncate flex-1 pl-1 text-left">{{ s.title }}</div>
              <button
                type="button"
                :aria-label="$t('component.chat_full_overlay.btn_delete_session')"
                :title="$t('component.chat_full_overlay.btn_delete_session')"
                class="ml-1 px-1.5 opacity-0 group-hover:opacity-100 transition-opacity text-rd hover:text-rdt"
                @click.stop="emit('deleteSession', s.id)"
              >
                <X :size="12" />
              </button>
            </div>
          </div>
        </aside>

        <!-- Hội thoại -->
        <div class="flex-1 flex flex-col relative h-full overflow-hidden">
          <DialogClose
            class="absolute top-4 right-4 z-20 h-9 w-9 flex items-center justify-center rounded-xl bg-white border border-b1 text-t3 shadow-md hover:text-red-500 hover:bg-red-50 hover:border-red-100 transition-all active:scale-90"
            :aria-label="$t('component.chat_full_overlay.btn_minimize')"
          >
            <X :size="18" />
          </DialogClose>

          <div
            class="absolute inset-0 opacity-[0.03] pointer-events-none"
            style="background-image: radial-gradient(var(--bl) 1px, transparent 0); background-size: 40px 40px"
            aria-hidden="true"
          />

          <div ref="scrollRef" class="flex-1 overflow-y-auto p-4 md:p-8 flex flex-col gap-8" @scroll.passive="onScroll">
            <div v-if="props.messages.length === 0" class="flex-1 flex flex-col items-center justify-center text-center p-10 opacity-50">
              <MessageSquare :size="48" class="text-bl mb-4" />
              <h3 class="text-lg font-bold text-t1 mb-2">{{ $t('component.chat_full_overlay.empty_title') }}</h3>
              <p class="text-sm text-t3 max-w-sm">{{ $t('component.chat_full_overlay.empty_hint') }}</p>
            </div>
            <MessageBubble v-for="m in props.messages" :key="m.id" :message="m" variant="full" />
            <div v-if="props.processing" class="w-full flex flex-col gap-3 self-start bg-white border border-b1 rounded-2xl rounded-tl-none shadow-sm max-w-[85%] px-4 py-3">
              <div class="text-sm text-t3 animate-pulse">{{ $t('component.chat_full_overlay.msg_thinking') }}</div>
            </div>
            <div class="h-[120px] shrink-0" />
          </div>

          <div class="absolute bottom-0 left-0 right-0 p-4 bg-bg1 border-t border-b1 shadow-lg z-10">
            <button
              v-if="showScrollButton"
              type="button"
              class="absolute left-1/2 -translate-x-1/2 -top-12 z-20 h-8 w-8 rounded-full border border-b1 bg-white text-t3 shadow-md flex items-center justify-center hover:text-bl hover:bg-bg transition-colors active:scale-95"
              :aria-label="$t('component.chat_full_overlay.btn_scroll_bottom')"
              @click="scrollToBottom(true)"
            >
              <ArrowDown :size="16" />
            </button>
            <div class="relative">
              <textarea
                ref="inputRef"
                v-model="input"
                rows="2"
                class="w-full resize-none py-3 pr-14 pl-4 text-sm bg-bg shadow-inner focus:bg-white transition-all rounded-xl focus:outline-none border border-b1 focus:border-bl/30"
                :placeholder="props.placeholder"
                :disabled="props.processing"
                @keydown="onKeydown"
              />
              <button
                type="button"
                :disabled="!input.trim() || props.processing"
                :aria-label="$t('component.chat_full_overlay.btn_send')"
                class="absolute right-2.5 bottom-3 h-8 w-8 font-bold text-white bg-bl rounded-lg hover:bg-bl/90 disabled:opacity-50 transition-all flex items-center justify-center"
                @click="emit('send', input)"
              >
                <Send :size="14" />
              </button>
            </div>
            <div class="flex gap-2 mt-3 overflow-x-auto pb-1 no-scrollbar">
              <button
                v-for="(tag, idx) in props.tags"
                :key="idx"
                type="button"
                :disabled="props.processing"
                class="whitespace-nowrap text-[10px] font-bold text-t2 bg-white px-3 py-1.5 rounded-lg cursor-pointer hover:bg-bl hover:text-white transition-all border border-b1 shadow-sm active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed"
                @click="emit('send', tag)"
              >
                {{ tag }}
              </button>
            </div>
          </div>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
