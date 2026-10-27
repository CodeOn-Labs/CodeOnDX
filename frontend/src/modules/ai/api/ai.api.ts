import { request } from '@/api/http';
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

export const aiApi = {
  /** refresh = true → chạy lại phân tích (bỏ qua kết quả đã lưu); projectId = màn của một dự án (/projects/:id…). */
  insights: (screen: ScreenKey, refresh = false, signal?: AbortSignal, projectId?: string) =>
    request<InsightResult>('/ai/insights', { query: { screen, refresh: refresh || undefined, projectId }, signal }),
};
