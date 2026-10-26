<script setup lang="ts">
// Port từ PaginationFooter.tsx (bản `full` và `compact`).
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from '@lucide/vue';
import PageSizeSelect from './PageSizeSelect.vue';
import type { PaginationController } from '@/shared/composables/usePagination';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(
  defineProps<{ pagination: PaginationController; variant?: 'full' | 'compact'; class?: string }>(),
  { variant: 'full', class: '' },
);

const { locale } = useI18n();

const p = props.pagination;
const pageIndex = p.pageIndex;
const pageCount = p.pageCount;

/** Giống getVisiblePages của bản React. */
const visiblePages = computed<(number | '...')[]>(() => {
  const count = pageCount.value;
  const index = pageIndex.value;
  if (count <= 5) return Array.from({ length: count }, (_, i) => i);
  if (index <= 2) return [0, 1, 2, 3, '...', count - 1];
  if (index >= count - 3) return [0, '...', count - 4, count - 3, count - 2, count - 1];
  return [0, '...', index - 1, index, index + 1, '...', count - 1];
});

const totalText = computed(() => p.totalItems.value.toLocaleString(locale.value));

const navBtn =
  'inline-flex h-8 w-8 cursor-pointer items-center justify-center rounded-lg text-slate-400 transition-all hover:bg-slate-100 hover:text-slate-700 disabled:cursor-not-allowed disabled:opacity-30 outline-none focus-visible:ring-2 focus-visible:ring-pr/30 focus-visible:ring-offset-1';
</script>

<template>
  <div v-if="props.variant === 'compact'" :class="cn('flex items-center justify-between gap-2 border-t border-slate-100 px-4 py-2', props.class)">
    <span class="text-[11.5px] font-semibold text-slate-400">{{ p.from.value }}–{{ p.to.value }} / {{ p.totalItems.value }}</span>
    <div class="flex items-center gap-0.5">
      <button
        type="button"
        class="inline-flex h-7 w-7 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700 disabled:cursor-not-allowed disabled:opacity-30"
        :disabled="!p.canPreviousPage.value"
        :aria-label="$t('component.pagination_footer.btn_previous_page')"
        @click="p.goToPreviousPage()"
      >
        <ChevronLeft :size="14" />
      </button>
      <template v-for="(page, index) in visiblePages" :key="page === '...' ? `ellipsis-${index}` : page">
        <span v-if="page === '...'" class="w-5 text-center text-[11px] text-slate-300">···</span>
        <button
          v-else
          type="button"
          :aria-current="page === pageIndex ? 'page' : undefined"
          :class="cn(
            'inline-flex h-7 min-w-[28px] items-center justify-center rounded-md px-1.5 text-[12px] font-semibold transition-colors',
            page === pageIndex ? 'bg-pr text-white' : 'text-slate-500 hover:bg-slate-100 hover:text-slate-800',
          )"
          @click="p.goToPage(page)"
        >
          {{ page + 1 }}
        </button>
      </template>
      <button
        type="button"
        class="inline-flex h-7 w-7 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700 disabled:cursor-not-allowed disabled:opacity-30"
        :disabled="!p.canNextPage.value"
        :aria-label="$t('component.pagination_footer.btn_next_page')"
        @click="p.goToNextPage()"
      >
        <ChevronRight :size="14" />
      </button>
    </div>
  </div>

  <div v-else :class="cn('flex items-center justify-between border-t border-slate-200 bg-white px-5 py-3', props.class)">
    <div class="flex items-center gap-2">
      <span class="text-[12px] font-semibold text-slate-700">{{ $t('component.pagination_footer.lbl_showing') }}</span>
      <span class="inline-flex items-center rounded-lg bg-slate-100 px-2.5 py-1 text-[12px] font-bold text-slate-900">
        {{ p.from.value }} – {{ p.to.value }}
      </span>
      <span class="text-[12px] font-medium text-slate-400">{{ $t('component.pagination_footer.lbl_total_records', { total: totalText }) }}</span>
    </div>

    <div class="flex items-center gap-3">
      <div class="flex items-center gap-2">
        <span class="hidden text-[12px] font-medium text-slate-400 sm:inline">{{ $t('component.pagination_footer.lbl_rows') }}</span>
        <PageSizeSelect :value="p.pageSize.value" :options="p.pageSizeOptions" @change="p.setPageSize" />
      </div>

      <div class="h-5 w-px bg-slate-200" />

      <div class="flex items-center gap-0.5">
        <button
          type="button"
          :class="navBtn"
          :disabled="!p.canPreviousPage.value"
          :aria-label="$t('component.pagination_footer.btn_first_page')"
          :title="$t('component.pagination_footer.btn_first_page')"
          @click="p.goToPage(0)"
        >
          <ChevronsLeft :size="15" />
        </button>
        <button
          type="button"
          :class="navBtn"
          :disabled="!p.canPreviousPage.value"
          :aria-label="$t('component.pagination_footer.btn_previous_page')"
          :title="$t('component.pagination_footer.btn_previous_page')"
          @click="p.goToPreviousPage()"
        >
          <ChevronLeft :size="15" />
        </button>

        <div class="mx-1 flex items-center gap-0.5">
          <template v-for="(page, index) in visiblePages" :key="page === '...' ? `ellipsis-${index}` : page">
            <span v-if="page === '...'" class="flex h-8 w-6 items-center justify-center text-[12px] text-slate-300">···</span>
            <button
              v-else
              type="button"
              :aria-current="page === pageIndex ? 'page' : undefined"
              :class="cn(
                'inline-flex h-8 min-w-[32px] cursor-pointer items-center justify-center rounded-lg px-2 text-[13px] font-semibold transition-all duration-150 outline-none focus-visible:ring-2 focus-visible:ring-pr/30 focus-visible:ring-offset-1',
                page === pageIndex ? 'bg-pr text-white shadow-sm shadow-pr/30' : 'text-slate-500 hover:bg-slate-100 hover:text-slate-800',
              )"
              @click="p.goToPage(page)"
            >
              {{ page + 1 }}
            </button>
          </template>
        </div>

        <button
          type="button"
          :class="navBtn"
          :disabled="!p.canNextPage.value"
          :aria-label="$t('component.pagination_footer.btn_next_page')"
          :title="$t('component.pagination_footer.btn_next_page')"
          @click="p.goToNextPage()"
        >
          <ChevronRight :size="15" />
        </button>
        <button
          type="button"
          :class="navBtn"
          :disabled="!p.canNextPage.value"
          :aria-label="$t('component.pagination_footer.btn_last_page')"
          :title="$t('component.pagination_footer.btn_last_page')"
          @click="p.goToPage(pageCount - 1)"
        >
          <ChevronsRight :size="15" />
        </button>
      </div>
    </div>
  </div>
</template>
