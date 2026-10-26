<script setup lang="ts">
// Port từ FilterChips.tsx.
import { X } from '@lucide/vue';
import type { ActiveFilter } from './types';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(defineProps<{ filters: ActiveFilter[]; allowClearAll?: boolean; class?: string }>(), {
  allowClearAll: true,
  class: '',
});
const emit = defineEmits<{ clear: [id: string]; clearAll: [] }>();
</script>

<template>
  <div v-if="props.filters.length > 0" :class="cn('flex flex-wrap items-center gap-2', props.class)">
    <div class="flex items-center gap-1.5 pr-2">
      <div class="h-1.5 w-1.5 rounded-full bg-pr animate-pulse" />
      <span class="text-[11px] font-black uppercase tracking-wider text-slate-400">{{ $t('component.filter_chips.lbl_filtering_by') }}</span>
    </div>

    <span
      v-for="filter in props.filters"
      :key="filter.id"
      class="inline-flex h-8 items-center gap-2 rounded-lg border border-pr/10 bg-white px-2.5 text-[11px] font-medium text-slate-600 shadow-sm transition-all hover:border-pr/30 hover:shadow-md animate-in fade-in zoom-in-95 duration-200"
    >
      <span v-if="filter.icon" class="text-pr/60"><component :is="filter.icon" :size="12" /></span>
      <span class="text-[9px] font-black uppercase tracking-widest text-slate-400">{{ filter.label }}</span>
      <div class="h-3 w-px bg-slate-200" />
      <span class="font-bold text-pr truncate max-w-[200px]">{{ filter.displayValue }}</span>
      <button
        type="button"
        class="ml-1 rounded-md p-0.5 text-slate-400 hover:bg-red-50 hover:text-red-500 transition-colors"
        :title="$t('component.filter_chips.btn_clear', { label: filter.label })"
        @click="emit('clear', filter.id)"
      >
        <X :size="12" :stroke-width="3" />
      </button>
    </span>

    <button
      v-if="props.allowClearAll && props.filters.length > 1"
      type="button"
      class="text-[11px] font-bold text-slate-400 hover:text-pr transition-colors px-2 py-1"
      @click="emit('clearAll')"
    >
      {{ $t('component.filter_chips.btn_clear_all') }}
    </button>
  </div>
</template>
