// Port useSideChatSessions.ts: nhiều phiên chat theo từng module, lưu sessionStorage.
// Bản gốc stream qua @ai-sdk/react; bản mới gọi POST /api/v1/ai/chat (BE lo connector AI / mock) và nhận cả câu trả lời.
import { computed, ref, watch, type Ref } from 'vue';
import { i18n } from '@/i18n';
import { aiApi } from '@/modules/ai/api/ai.api';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  /** Câu trả lời là thông báo lỗi (hiển thị màu đỏ). */
  error?: boolean;
}

export interface ChatSession {
  id: string;
  title: string;
  messages: ChatMessage[];
}

const sessionsKey = (module: string) => `dx_side_chat_sessions_${module}`;
const activeKey = (module: string) => `dx_side_chat_active_session_${module}`;
const { t } = i18n.global;

const hasUserMessages = (s: ChatSession) => s.messages.some((m) => m.role === 'user');

function readStorage(key: string): string | null {
  try {
    return sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStorage(key: string, value: string) {
  try {
    sessionStorage.setItem(key, value);
  } catch {
    // sessionStorage bị chặn — vẫn chạy được, chỉ không giữ lịch sử khi tải lại
  }
}

function defaultSession(id = 'default'): ChatSession {
  return {
    id,
    title: t('component.side_chat_panel.lbl_default_session'),
    messages: [{ id: '1', role: 'assistant', content: t('component.side_chat_panel.msg_greeting') }],
  };
}

function loadSessions(module: string): ChatSession[] {
  try {
    const parsed = JSON.parse(readStorage(sessionsKey(module)) ?? '[]') as ChatSession[];
    const nonEmpty = Array.isArray(parsed) ? parsed.filter(hasUserMessages) : [];
    if (nonEmpty.length > 0) return nonEmpty;
  } catch {
    // dữ liệu hỏng → tạo phiên mặc định
  }
  return [defaultSession()];
}

export function useSideChatSessions(module: Ref<string>) {
  const sessions = ref<ChatSession[]>(loadSessions(module.value));
  const activeSessionId = ref<string>('default');
  const input = ref('');
  const processing = ref(false);

  function restoreActive() {
    const saved = readStorage(activeKey(module.value));
    activeSessionId.value = saved && sessions.value.some((s) => s.id === saved) ? saved : (sessions.value[0]?.id ?? 'default');
  }
  restoreActive();

  const activeSession = computed(() => sessions.value.find((s) => s.id === activeSessionId.value) ?? sessions.value[0]);
  const messages = computed(() => activeSession.value?.messages ?? []);

  // Lưu phiên (bỏ phiên chưa có câu hỏi nào, luôn giữ ít nhất 1 phiên)
  watch(
    sessions,
    (list) => {
      const nonEmpty = list.filter(hasUserMessages);
      writeStorage(sessionsKey(module.value), JSON.stringify(nonEmpty.length > 0 ? nonEmpty : list.slice(0, 1)));
    },
    { deep: true },
  );
  watch(activeSessionId, (id) => writeStorage(activeKey(module.value), id));

  // Đổi module (sang màn khác) → nạp phiên của module đó
  watch(module, (next) => {
    sessions.value = loadSessions(next);
    restoreActive();
  });

  function updateSession(id: string, update: (s: ChatSession) => ChatSession) {
    sessions.value = sessions.value.map((s) => (s.id === id ? update(s) : s));
  }

  async function send(text: string) {
    const message = text.trim();
    const sessionId = activeSession.value?.id;
    if (!message || !sessionId || processing.value) return;
    input.value = '';
    processing.value = true;

    updateSession(sessionId, (s) => {
      const isFresh = s.messages.length <= 1 || s.title === t('component.side_chat_panel.lbl_new_session') || s.title === t('component.side_chat_panel.lbl_default_session');
      return {
        ...s,
        title: isFresh ? (message.length > 20 ? `${message.substring(0, 20)}...` : message) : s.title,
        messages: [...s.messages, { id: `${Date.now()}u`, role: 'user', content: message }],
      };
    });

    try {
      const { data } = await aiApi.chat(message, module.value);
      updateSession(sessionId, (s) => ({ ...s, messages: [...s.messages, { id: `${Date.now()}a`, role: 'assistant', content: data.reply }] }));
    } catch (err) {
      const content = err instanceof Error && err.message ? err.message : t('component.side_chat_panel.err_cannot_connect');
      updateSession(sessionId, (s) => ({ ...s, messages: [...s.messages, { id: `${Date.now()}e`, role: 'assistant', content, error: true }] }));
    } finally {
      processing.value = false;
    }
  }

  function newSession() {
    if (processing.value) return;
    const empty = sessions.value.find((s) => !hasUserMessages(s));
    if (empty) {
      activeSessionId.value = empty.id;
      return;
    }
    const id = Date.now().toString();
    sessions.value = [
      {
        id,
        title: t('component.side_chat_panel.lbl_new_session'),
        messages: [{ id: `${id}ai`, role: 'assistant', content: t('component.side_chat_panel.msg_new_session_greeting') }],
      },
      ...sessions.value,
    ];
    activeSessionId.value = id;
  }

  function deleteSession(id: string) {
    const index = sessions.value.findIndex((s) => s.id === id);
    const remaining = sessions.value.filter((s) => s.id !== id);
    if (remaining.length === 0) {
      const fallback = defaultSession(`default-${Date.now()}`);
      sessions.value = [fallback];
      activeSessionId.value = fallback.id;
      return;
    }
    sessions.value = remaining;
    const next = remaining[Math.min(index, remaining.length - 1)];
    if (id === activeSessionId.value && next) activeSessionId.value = next.id;
  }

  function selectSession(id: string) {
    if (!processing.value) activeSessionId.value = id;
  }

  return { sessions, activeSessionId, activeSession, messages, input, processing, send, newSession, deleteSession, selectSession };
}
