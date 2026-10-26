import { request, requestFull } from '@/api/http';
import type { ScreenKey } from '@/app/screens';

export type InsightVariant = 'amber' | 'purple' | 'red' | 'teal' | 'blue' | 'green';

export interface InsightItem {
  id: string;
  /** Màn sinh ra insight (màn hiện tại hoặc màn cha được kế thừa — ADR-0001). */
  screenKey: ScreenKey;
  variant: InsightVariant;
  title: string;
  content: string;
}

export interface InsightResult {
  screenKey: ScreenKey;
  generatedAt: string;
  cached: boolean;
  items: InsightItem[];
}

export interface ChatReply {
  reply: string;
  module: string;
}

export const aiApi = {
  /** refresh = true → chạy lại phân tích (bỏ qua kết quả đã lưu). */
  insights: (screen: ScreenKey, refresh = false, signal?: AbortSignal) =>
    request<InsightResult>('/ai/insights', { query: { screen, refresh: refresh || undefined }, signal }),
  chat: (message: string, module: string) => requestFull<ChatReply>('/ai/chat', { method: 'POST', body: { message, module } }),
};
