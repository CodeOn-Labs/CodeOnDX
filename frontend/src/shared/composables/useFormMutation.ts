// Bọc useMutation cho thao tác ghi từ form/dialog: thành công → toast messageText của BE + làm mới cache;
// lỗi 400 → gắn lỗi theo field (không toast); lỗi khác → toast.
import { ref } from 'vue';
import { useMutation, useQueryClient, type QueryKey } from '@tanstack/vue-query';
import type { ErrorFullModel } from '@/api/types';
import { firstFieldErrors } from '@/api/http';
import { useToastStore } from '@/stores/toast';

export function useFormMutation<TInput, TData>(options: {
  mutationFn: (input: TInput) => Promise<ErrorFullModel<TData>>;
  /** Các query cần làm mới sau khi ghi thành công */
  invalidate?: QueryKey[];
  onSuccess?: (data: TData, input: TInput) => void;
  /** Không hiện toast thành công (màn tự hiện) */
  quiet?: boolean;
}) {
  const toast = useToastStore();
  const queryClient = useQueryClient();
  const fieldErrors = ref<Record<string, string>>({});

  const mutation = useMutation({
    mutationFn: options.mutationFn,
    meta: { silent: true },
    onMutate: () => {
      fieldErrors.value = {};
    },
    onSuccess: (res, input) => {
      if (res.messageText && !options.quiet) toast.success(res.messageText);
      for (const key of options.invalidate ?? []) void queryClient.invalidateQueries({ queryKey: key });
      options.onSuccess?.(res.data, input);
    },
    onError: (err: Error) => {
      fieldErrors.value = firstFieldErrors(err);
      if (Object.keys(fieldErrors.value).length === 0) toast.error(err.message);
    },
  });

  return { ...mutation, fieldErrors };
}
