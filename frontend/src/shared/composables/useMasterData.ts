// Danh mục dùng chung qua TanStack Vue Query (thay useMasterData / useGetClientsQuery / useGetRulesQuery của bản React).
// Khoá query có locale vì BE trả tên đã dịch theo Accept-Language.
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { masterDataApi, type MasterDataType } from '@/api/master-data.api';

const LONG_STALE = 10 * 60_000;

export function useMasterData(type: MasterDataType) {
  const { locale } = useI18n();
  const query = useQuery({
    queryKey: computed(() => ['master-data', type, locale.value] as const),
    queryFn: () => masterDataApi.list([type]),
    staleTime: LONG_STALE,
  });
  return { ...query, items: computed(() => query.data.value ?? []) };
}

export function useClients() {
  const query = useQuery({ queryKey: ['clients'], queryFn: masterDataApi.clients, staleTime: LONG_STALE });
  return { ...query, items: computed(() => query.data.value ?? []) };
}

export function useSystemRules() {
  const { locale } = useI18n();
  // Tên + mô tả quy tắc do BE dịch → khoá query có locale
  const query = useQuery({
    queryKey: computed(() => ['system-rules', locale.value] as const),
    queryFn: masterDataApi.systemRules,
    staleTime: LONG_STALE,
  });

  /** Giá trị số của quy tắc; quy tắc tắt (status = 0) hoặc không có → `fallback` (giống bản React). */
  function ruleNumber(key: string, fallback: number) {
    const rule = query.data.value?.find((r) => r.key === key);
    if (!rule || rule.status === 0) return fallback;
    const value = Number(rule.value ?? rule.defaultValue ?? fallback);
    return Number.isFinite(value) ? value : fallback;
  }

  return { ...query, ruleNumber };
}
