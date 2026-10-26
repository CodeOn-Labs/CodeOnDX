<script setup lang="ts">
// Port StatusBadge của EmployeeTable.tsx: trạng thái phân bổ (quá tải / ổn định / phân bổ thấp / chờ việc / đã nghỉ).
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import type { AllocationStatus } from '@/modules/org/api/org.api';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ status: AllocationStatus; deleted?: boolean }>();
const { t } = useI18n();

const STYLE: Record<AllocationStatus, { className: string; dot: string }> = {
  overloaded: { className: 'bg-red-50/80 text-red-700 ring-red-500/30', dot: 'bg-red-500' },
  normal: { className: 'bg-emerald-50/80 text-emerald-700 ring-emerald-500/30', dot: 'bg-emerald-500' },
  low: { className: 'bg-blue-50/80 text-blue-700 ring-blue-500/30', dot: 'bg-blue-500' },
  bench: { className: 'bg-amber-50/80 text-amber-700 ring-amber-500/30', dot: 'bg-amber-500' },
  resigned: { className: 'bg-slate-50 text-slate-500 ring-slate-500/20', dot: 'bg-slate-400' },
};
const label = computed(() => {
  if (props.deleted) return t('component.allocation_badge.lbl_deleted');
  return {
    overloaded: t('component.allocation_badge.lbl_overloaded'),
    normal: t('component.allocation_badge.lbl_normal'),
    low: t('component.allocation_badge.lbl_low'),
    bench: t('component.allocation_badge.lbl_bench'),
    resigned: t('component.allocation_badge.lbl_resigned'),
  }[props.status];
});
const style = computed(() => (props.deleted ? { className: 'bg-rose-50 text-rose-700 ring-rose-500/20', dot: 'bg-rose-500' } : STYLE[props.status]));
</script>

<template>
  <span :class="cn('inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[10px] font-black uppercase tracking-widest ring-1 ring-inset shadow-sm backdrop-blur-sm whitespace-nowrap', style.className)">
    <span :class="cn('h-1.5 w-1.5 rounded-full', style.dot)" />
    {{ label }}
  </span>
</template>
