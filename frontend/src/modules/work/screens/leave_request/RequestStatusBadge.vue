<script setup lang="ts">
// Trạng thái đơn nghỉ / OT: chờ duyệt, đã duyệt, từ chối, đã huỷ.
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import type { RequestStatus } from '@/modules/work/api/work.api';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ status: RequestStatus; approver?: string | null }>();
const { t } = useI18n();
const STYLE: Record<RequestStatus, string> = {
  pending: 'bg-amber-50 text-amber-600 ring-amber-500/20',
  approved: 'bg-emerald-50 text-emerald-600 ring-emerald-500/20',
  rejected: 'bg-rose-50 text-rose-600 ring-rose-500/20',
  cancelled: 'bg-slate-100 text-slate-500 ring-slate-500/10',
};
const label = computed(() => ({ pending: t('leave_request.opt_status_pending'), approved: t('leave_request.opt_status_approved'), rejected: t('leave_request.opt_status_rejected'), cancelled: t('leave_request.opt_status_cancelled') })[props.status]);
</script>

<template>
  <div class="flex flex-col gap-0.5">
    <span :class="cn('inline-flex w-fit items-center rounded-full px-2 py-0.5 text-[10.5px] font-black uppercase tracking-wider ring-1 ring-inset', STYLE[props.status])">{{ label }}</span>
    <span v-if="props.approver && props.status !== 'pending'" class="text-[10px] text-slate-400">{{ props.approver }}</span>
  </div>
</template>
