import type { Component } from 'vue';

export interface SelectOption {
  label: string;
  value: string;
  icon?: Component;
  iconClass?: string;
  description?: string;
  disabled?: boolean;
}
