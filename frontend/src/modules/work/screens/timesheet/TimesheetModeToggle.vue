<script setup lang="ts">
// Port TimesheetModeToggle.tsx: công tắc Ngày / Tuần.
import Button from '@/shared/components/Button.vue';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ mode: 'day' | 'week' }>();
const emit = defineEmits<{ switch: [] }>();
const btn = (active: boolean) =>
  cn('h-7 lg:h-8 px-4 rounded-[10px] text-[12px] lg:text-[12.5px] font-bold', active ? 'shadow-[0_6px_16px_rgba(74,222,128,0.28)]' : 'text-[#6E7891] hover:text-[#1F2430]');
</script>

<template>
  <div class="flex rounded-[14px] bg-white/80 p-1 shadow-sm border border-[#E4E8F3] h-9 lg:h-10 items-center shrink-0">
    <Button :variant="props.mode === 'day' ? 'success' : 'ghost'" :class="btn(props.mode === 'day')" @click="props.mode !== 'day' && emit('switch')">
      {{ $t('timesheet.btn_mode_day') }}
    </Button>
    <Button :variant="props.mode === 'week' ? 'success' : 'ghost'" :class="btn(props.mode === 'week')" @click="props.mode !== 'week' && emit('switch')">
      {{ $t('timesheet.btn_mode_week') }}
    </Button>
  </div>
</template>
