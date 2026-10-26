<script setup lang="ts">
// Khung đầu trang của mọi màn danh sách (gộp *FilterBar.tsx của bản gốc: ProjectFilterBar, ClientFilterBar, EmployeeFilterBar…):
// tiêu đề + kicker, nhóm nút thao tác, ô tìm kiếm, bộ lọc nâng cao (slot #filters), nút Tìm kiếm, chip bộ lọc đang áp dụng.
import { Search, X } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import FilterPopover from './FilterPopover.vue';
import FilterChips from './FilterChips.vue';
import type { ActiveFilter } from './types';

const props = withDefaults(
  defineProps<{
    title: string;
    kicker?: string;
    searchPlaceholder?: string;
    activeFilters?: ActiveFilter[];
    advancedFilterCount?: number;
    isFetching?: boolean;
    /** Ẩn ô tìm kiếm (màn chỉ có bộ lọc) */
    hideSearch?: boolean;
  }>(),
  { kicker: undefined, searchPlaceholder: undefined, activeFilters: () => [], advancedFilterCount: 0, isFetching: false, hideSearch: false },
);
const search = defineModel<string>('search', { default: '' });
const filterOpen = defineModel<boolean>('filterOpen', { default: false });
const emit = defineEmits<{ search: []; reset: []; applyFilters: []; clearFilter: [id: string] }>();
</script>

<template>
  <div class="flex flex-col gap-3 rounded-2xl bg-white/60 backdrop-blur-xl p-3 lg:p-4 shadow-xl shadow-slate-200/40 border border-white/60 transition-all duration-300">
    <div class="flex items-center justify-between gap-3 flex-wrap">
      <div class="flex flex-col gap-1">
        <div v-if="props.kicker" class="flex items-center gap-2">
          <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
          <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ props.kicker }}</span>
        </div>
        <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">
          {{ props.title }}
        </h1>
      </div>
      <div class="flex items-center gap-3">
        <div v-if="$slots.tools" class="flex items-center gap-2 rounded-2xl bg-slate-100/50 p-1 backdrop-blur-md">
          <slot name="tools" />
        </div>
        <slot name="actions" />
      </div>
    </div>

    <form @submit.prevent="emit('search')">
      <div class="flex items-center justify-between gap-3 flex-wrap">
        <div class="flex items-center gap-3 flex-wrap">
          <div v-if="!props.hideSearch" class="group relative flex items-center">
            <Search :size="16" class="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 transition-colors group-focus-within:text-pr" />
            <input
              v-model="search"
              type="text"
              :placeholder="props.searchPlaceholder"
              :aria-label="props.searchPlaceholder"
              class="h-9 lg:h-10 w-72 lg:w-80 rounded-xl border border-slate-200 bg-white pl-10 pr-9 text-[13.5px] font-medium text-slate-900 outline-none transition-all placeholder:text-slate-400 focus:border-pr focus:ring-4 focus:ring-pr/10 lg:px-5 lg:pl-11"
            />
            <button
              v-if="search"
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 rounded-full p-0.5 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
              :aria-label="$t('component.list_toolbar.btn_clear_search')"
              @click="search = ''"
            >
              <X :size="13" />
            </button>
          </div>

          <slot name="inline" />

          <FilterPopover
            v-if="$slots.filters"
            v-model:open="filterOpen"
            :active-count="props.advancedFilterCount"
            :is-applying="props.isFetching"
            @apply="emit('applyFilters')"
            @reset="emit('reset')"
          >
            <div class="flex flex-col gap-4 p-1">
              <slot name="filters" />
            </div>
          </FilterPopover>

          <Button
            type="submit"
            variant="primary"
            class="h-9 lg:h-10 rounded-xl px-4 lg:px-5 text-[13px] lg:text-[13.5px] font-bold"
            :loading="props.isFetching"
            :disabled="props.isFetching"
          >
            {{ $t('component.list_toolbar.btn_search') }}
          </Button>
        </div>
        <slot name="right" />
      </div>

      <div v-if="props.activeFilters.length" class="mt-2 border-slate-100/50 pt-2">
        <FilterChips :filters="props.activeFilters" class="pb-1" @clear="emit('clearFilter', $event)" @clear-all="emit('reset')" />
      </div>
    </form>
  </div>
</template>
