import type { Component } from 'vue';

/** Một bộ lọc đang áp dụng, hiển thị dạng chip (port ActiveFilter của FilterChips.tsx). */
export interface ActiveFilter {
  id: string;
  label: string;
  displayValue: string;
  icon?: Component;
}
