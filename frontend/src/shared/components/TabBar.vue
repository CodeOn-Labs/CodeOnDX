<script setup lang="ts" generic="TKey extends string">
// Thanh tab gạch chân (port TabButton.tsx / ManagementSubTabBar.tsx / tab của RoleDetailPage).
import type { Component } from 'vue';
import { cn } from '@/shared/utils/cn';

export interface TabItem<K extends string = string> {
  key: K;
  label: string;
  icon?: Component;
  count?: number;
}

const props = withDefaults(defineProps<{ tabs: TabItem<TKey>[]; size?: 'sm' | 'md'; class?: string }>(), { size: 'md', class: '' });
const model = defineModel<TKey>({ required: true });
</script>

<template>
  <div :class="cn('flex items-center gap-1 border-b border-slate-100 overflow-x-auto no-scrollbar', props.class)" role="tablist">
    <button
      v-for="tab in props.tabs"
      :key="tab.key"
      type="button"
      role="tab"
      :aria-selected="model === tab.key"
      :class="cn(
        'flex items-center gap-2 whitespace-nowrap border-b-2 -mb-px font-semibold transition-colors duration-200 outline-none focus-visible:ring-2 focus-visible:ring-pr/30',
        props.size === 'sm' ? 'px-2.5 py-2 text-[12px]' : 'px-3 py-3 text-[13px]',
        model === tab.key ? 'border-pr text-pr' : 'border-transparent text-slate-400 hover:text-slate-600',
      )"
      @click="model = tab.key"
    >
      <component :is="tab.icon" v-if="tab.icon" :size="props.size === 'sm' ? 14 : 16" :stroke-width="2.5" />
      {{ tab.label }}
      <span
        v-if="tab.count !== undefined"
        :class="cn('rounded-full px-1.5 py-0.5 text-[10px] font-black leading-none', model === tab.key ? 'bg-pr/10 text-pr' : 'bg-slate-100 text-slate-400')"
      >
        {{ tab.count }}
      </span>
    </button>
  </div>
</template>
