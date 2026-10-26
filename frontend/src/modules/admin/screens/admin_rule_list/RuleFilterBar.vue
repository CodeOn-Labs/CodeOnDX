<script setup lang="ts">
// Port RuleFilterBar.tsx: tiêu đề, ô tìm kiếm, lọc theo màn hình áp dụng, chip bộ lọc.
import { Search, X } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import FilterPopover from '@/shared/components/filter/FilterPopover.vue';
import FilterChips from '@/shared/components/filter/FilterChips.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import type { ActiveFilter } from '@/shared/components/filter/types';
import type { SelectOption } from '@/shared/components/form/types';
import type { RuleFilters } from './types';

const props = defineProps<{
  activeFilters: ActiveFilter[];
  advancedFilterCount: number;
  isFetching: boolean;
  moduleOptions: SelectOption[];
}>();
const draft = defineModel<RuleFilters>('draft', { required: true });
const filterOpen = defineModel<boolean>('filterOpen', { default: false });
const emit = defineEmits<{ search: []; reset: []; applyFilters: []; clearFilter: [id: string] }>();

const asArray = (value: string | string[] | null) => (Array.isArray(value) ? value : value ? [value] : []);
</script>

<template>
  <div class="shrink-0 flex flex-col gap-3 rounded-2xl bg-white/60 backdrop-blur-xl p-3 lg:p-4 shadow-xl shadow-slate-200/40 border border-white/60 transition-all duration-300">
    <div class="flex items-center justify-between">
      <div class="shrink-0 flex flex-col gap-1">
        <div class="flex items-center gap-2">
          <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
          <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ $t('admin_rule_list.lbl_kicker') }}</span>
        </div>
        <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">
          {{ $t('admin_rule_list.title_page') }}
        </h1>
      </div>
    </div>

    <form @submit.prevent="emit('search')">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="group relative flex items-center">
            <Search :size="16" class="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 transition-colors group-focus-within:text-pr" />
            <input
              type="text"
              :value="draft.search"
              :placeholder="$t('admin_rule_list.ph_search')"
              :aria-label="$t('admin_rule_list.ph_search')"
              class="h-9 lg:h-10 w-72 lg:w-80 rounded-xl border border-slate-200 bg-white pl-10 pr-9 text-[13.5px] font-medium text-slate-900 outline-none transition-all placeholder:text-slate-400 focus:border-pr focus:ring-4 focus:ring-pr/10 lg:px-5 lg:pl-11"
              @input="draft = { ...draft, search: ($event.target as HTMLInputElement).value }"
            />
            <button
              v-if="draft.search"
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 rounded-full p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition-all"
              :aria-label="$t('admin_rule_list.btn_clear_search')"
              @click="draft = { ...draft, search: '' }; emit('search')"
            >
              <X :size="14" />
            </button>
          </div>

          <div class="h-6 w-[1px] bg-slate-200 mx-1" />

          <FilterPopover
            v-model:open="filterOpen"
            :active-count="props.advancedFilterCount"
            :is-applying="props.isFetching"
            @apply="emit('applyFilters')"
            @reset="emit('reset')"
          >
            <div class="shrink-0 flex flex-col gap-4 p-1">
              <FormSelect
                :label="$t('admin_rule_list.lbl_module')"
                :options="props.moduleOptions"
                :placeholder="$t('admin_rule_list.ph_all_modules')"
                trigger-class="h-10 rounded-xl text-[13px] border-slate-200"
                :model-value="draft.modules"
                multiple
                allow-clear
                hide-message
                @update:model-value="draft = { ...draft, modules: asArray($event) }"
              />
            </div>
          </FilterPopover>

          <Button
            type="submit"
            variant="primary"
            class="text-[13px] font-bold shadow-xl shadow-pr/20 hover:shadow-pr/30 transition-all hover:-translate-y-0.5 active:translate-y-0"
            :loading="props.isFetching"
          >
            {{ $t('admin_rule_list.btn_search') }}
          </Button>
        </div>
      </div>

      <div v-if="props.activeFilters.length > 0" class="mt-2 border-t border-slate-100/50 pt-2">
        <FilterChips :filters="props.activeFilters" class="pb-1" @clear="emit('clearFilter', $event)" @clear-all="emit('reset')" />
      </div>
    </form>
  </div>
</template>
