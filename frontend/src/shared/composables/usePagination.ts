// Port usePagination (React) sang Vue: pageIndex bắt đầu từ 0 trên giao diện; API dùng page = pageIndex + 1.
import { computed, ref, watch, type Ref } from 'vue';

export const DEFAULT_PAGE_SIZE = 20;
export const DEFAULT_PAGE_SIZE_OPTIONS = [20, 50, 100];

export interface PaginationController {
  pageIndex: Ref<number>;
  pageSize: Ref<number>;
  totalItems: Ref<number>;
  pageCount: Readonly<Ref<number>>;
  from: Readonly<Ref<number>>;
  to: Readonly<Ref<number>>;
  canPreviousPage: Readonly<Ref<boolean>>;
  canNextPage: Readonly<Ref<boolean>>;
  pageSizeOptions: number[];
  goToPage: (pageIndex: number) => void;
  goToNextPage: () => void;
  goToPreviousPage: () => void;
  setPageSize: (pageSize: number) => void;
}

export function usePagination(options: { initialPageIndex?: number; initialPageSize?: number; pageSizeOptions?: number[] } = {}): PaginationController {
  const pageIndex = ref(options.initialPageIndex ?? 0);
  const pageSize = ref(options.initialPageSize ?? DEFAULT_PAGE_SIZE);
  const totalItems = ref(0);

  const pageCount = computed(() => Math.max(1, Math.ceil(totalItems.value / pageSize.value)));
  const from = computed(() => (totalItems.value === 0 ? 0 : pageIndex.value * pageSize.value + 1));
  const to = computed(() => (totalItems.value === 0 ? 0 : Math.min(totalItems.value, from.value + pageSize.value - 1)));

  // Tổng số bản ghi giảm (đổi bộ lọc) → kéo trang hiện tại về trang cuối hợp lệ
  watch(pageCount, (count) => {
    if (pageIndex.value > count - 1) pageIndex.value = Math.max(0, count - 1);
  });

  return {
    pageIndex,
    pageSize,
    totalItems,
    pageCount,
    from,
    to,
    canPreviousPage: computed(() => pageIndex.value > 0),
    canNextPage: computed(() => pageIndex.value < pageCount.value - 1),
    pageSizeOptions: options.pageSizeOptions ?? DEFAULT_PAGE_SIZE_OPTIONS,
    goToPage: (index: number) => {
      pageIndex.value = Math.min(Math.max(index, 0), pageCount.value - 1);
    },
    goToNextPage: () => {
      pageIndex.value = Math.min(pageIndex.value + 1, pageCount.value - 1);
    },
    goToPreviousPage: () => {
      pageIndex.value = Math.max(pageIndex.value - 1, 0);
    },
    setPageSize: (size: number) => {
      pageSize.value = size;
      pageIndex.value = 0;
    },
  };
}
