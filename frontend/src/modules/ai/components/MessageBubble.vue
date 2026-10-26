<script setup lang="ts">
// Port MessageBubble.tsx (biến thể 'side' cho khung chat cạnh màn hình, 'full' cho chat toàn màn hình).
import MarkdownLite from './MarkdownLite.vue';
import type { ChatMessage } from '@/modules/ai/composables/useSideChatSessions';

withDefaults(defineProps<{ message: ChatMessage; variant?: 'full' | 'side' }>(), { variant: 'full' });
</script>

<template>
  <!-- Người dùng -->
  <div v-if="message.role === 'user'" :class="variant === 'side' ? 'flex flex-col items-end' : 'flex justify-end'">
    <div
      v-if="message.content"
      :class="variant === 'side'
        ? 'max-w-[92%] px-3 py-2 rounded-2xl text-[12px] leading-relaxed shadow-sm bg-[#EBF5FF] text-[#3B82F6] rounded-tr-none whitespace-pre-wrap'
        : 'max-w-[85%] rounded-2xl px-4 py-3 text-sm leading-relaxed whitespace-pre-wrap bg-bl text-white rounded-tr-none shadow-md'"
    >
      {{ message.content }}
    </div>
  </div>

  <!-- AI — khung chat cạnh màn hình -->
  <div v-else-if="variant === 'side'" class="flex flex-col items-start w-full">
    <div class="max-w-[92%] px-3 py-2 rounded-2xl text-[12px] leading-relaxed shadow-sm bg-white border border-b2 text-[#1F2937] rounded-tl-none flex flex-col gap-2 w-full">
      <div :class="['text-[12px] leading-relaxed [&_p+p]:mt-2 [&_ul]:mt-1 [&_ol]:mt-1', message.error ? 'text-rdt' : '']">
        <MarkdownLite :source="message.content" />
      </div>
    </div>
  </div>

  <!-- AI — chat toàn màn hình -->
  <div v-else class="w-full flex flex-col gap-3 self-start bg-white border border-b1 rounded-2xl rounded-tl-none shadow-sm max-w-[85%] px-4 py-3">
    <div :class="['text-sm leading-relaxed [&_p+p]:mt-3 [&_ul]:mt-2 [&_ol]:mt-2', message.error ? 'text-rdt' : '']">
      <MarkdownLite :source="message.content" />
    </div>
  </div>
</template>
