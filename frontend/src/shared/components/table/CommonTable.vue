<script setup lang="ts" generic="TRow extends object">
// Port từ CommonTable.tsx — giữ nguyên class của bảng, skeleton, empty state và hiệu ứng hover dòng.
// Khác bản React: không dùng TanStack Table; ô render qua slot `cell-<id>` (dễ đọc hơn render function).
import EmptyState from '@/shared/components/EmptyState.vue';
import PaginationFooter from './PaginationFooter.vue';
import type { PaginationController } from '@/shared/composables/usePagination';
import type { TableColumn } from './types';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(
  defineProps<{
    columns: TableColumn[];
    rows: TRow[];
    rowKey?: (row: TRow, index: number) => string;
    loading?: boolean;
    emptyMessage?: string;
    containerClass?: string;
    tableWrapperClass?: string;
    tableClass?: string;
    headerRowClass?: string;
    rowClass?: (row: TRow, index: number) => string;
    pagination?: PaginationController;
  }>(),
  {
    rowKey: undefined,
    loading: false,
    emptyMessage: undefined,
    containerClass: '',
    tableWrapperClass: '',
    tableClass: '',
    headerRowClass: '',
    rowClass: undefined,
    pagination: undefined,
  },
);

const emit = defineEmits<{ rowClick: [row: TRow, index: number] }>();

defineSlots<{
  [key: `cell-${string}`]: (props: { row: TRow; index: number }) => unknown;
  [key: `header-${string}`]: (props: { column: TableColumn }) => unknown;
}>();

function keyOf(row: TRow, index: number): string {
  if (props.rowKey) return props.rowKey(row, index);
  const id = (row as { id?: unknown }).id;
  return typeof id === 'string' || typeof id === 'number' ? String(id) : String(index);
}

function cellValue(row: TRow, columnId: string): unknown {
  return (row as Record<string, unknown>)[columnId];
}

function skeletonWidth(colIdx: number) {
  if (colIdx === 0) return '70%';
  return colIdx === props.columns.length - 1 ? '40%' : '80%';
}
</script>

<template>
  <div :class="cn('flex flex-col overflow-hidden', props.containerClass)">
    <div
      :class="cn('overflow-x-auto overflow-y-auto rounded-2xl border border-slate-200 bg-white shadow-sm flex-1 min-h-0', props.tableWrapperClass)"
      style="contain: layout paint; scrollbar-gutter: stable"
    >
      <table :class="cn('w-full border-separate border-spacing-0 text-left', props.tableClass)" :aria-busy="props.loading">
        <thead class="sticky top-0 z-20">
          <tr :class="cn('bg-slate-50/80 backdrop-blur-md text-[11px] font-bold uppercase tracking-widest text-slate-500', props.headerRowClass)">
            <th v-for="col in props.columns" :key="col.id" :class="cn('border-b border-slate-200 px-4 py-2.5', col.headerClass)">
              <slot :name="`header-${col.id}`" :column="col">{{ col.header }}</slot>
            </th>
          </tr>
        </thead>
        <tbody>
          <template v-if="props.loading">
            <tr v-for="rowIdx in 5" :key="`skeleton-${rowIdx}`" class="bg-white">
              <td
                v-for="(col, colIdx) in props.columns"
                :key="col.id"
                :class="cn('border-b border-slate-100 px-6 py-5 align-middle', col.cellClass)"
              >
                <div class="flex flex-col gap-2">
                  <div
                    class="h-4 animate-pulse rounded bg-slate-100"
                    :style="{ width: skeletonWidth(colIdx), animationDelay: `${(rowIdx - 1) * 60}ms` }"
                  />
                </div>
              </td>
            </tr>
          </template>
          <tr v-else-if="props.rows.length === 0">
            <td :colspan="props.columns.length" class="px-6 py-16 text-center">
              <EmptyState :title="props.emptyMessage" compact />
            </td>
          </tr>
          <template v-else>
            <tr
              v-for="(row, rowIndex) in props.rows"
              :key="keyOf(row, rowIndex)"
              :class="cn(
                'group group/row relative border-l-2 border-transparent transition-all duration-300 ease-in-out',
                'hover:z-[1] hover:border-pr/40 hover:bg-slate-50/40 hover:shadow-lg hover:shadow-slate-200/40',
                props.rowClass?.(row, rowIndex),
              )"
              @click="emit('rowClick', row, rowIndex)"
            >
              <td
                v-for="col in props.columns"
                :key="col.id"
                :class="cn('border-b border-slate-100 px-4 py-1.5 align-middle text-[12.5px] font-medium text-slate-700 transition-colors', col.cellClass)"
              >
                <slot :name="`cell-${col.id}`" :row="row" :index="rowIndex">{{ cellValue(row, col.id) }}</slot>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <PaginationFooter v-if="props.pagination" :pagination="props.pagination" />
  </div>
</template>
