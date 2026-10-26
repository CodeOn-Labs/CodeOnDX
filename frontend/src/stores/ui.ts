import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { ScreenKey } from '@/app/screens';

/**
 * Trạng thái giao diện giữ qua các lần chuyển trang (thay uiSlice của Redux):
 * bộ lọc đã áp dụng của từng màn, để quay lại màn vẫn thấy đúng bộ lọc cũ.
 */
export const useUiStore = defineStore('ui', () => {
  const filters = ref<Partial<Record<ScreenKey, unknown>>>({});

  function getFilters<T>(screen: ScreenKey): T | undefined {
    return filters.value[screen] as T | undefined;
  }

  function setFilters<T>(screen: ScreenKey, value: T) {
    filters.value = { ...filters.value, [screen]: value };
  }

  return { filters, getFilters, setFilters };
});
