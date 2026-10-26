<script setup lang="ts">
// Port MonthYearPicker trong FormDatePicker.tsx (chọn nhanh tháng / năm ở tiêu đề lịch).
import { ref } from 'vue';
import { PopoverContent, PopoverPortal, PopoverRoot, PopoverTrigger } from 'reka-ui';
import { Check, ChevronDown } from '@lucide/vue';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ value: number; options: { value: number; label: string }[] }>();
const emit = defineEmits<{ change: [value: number] }>();
const open = ref(false);

function pick(value: number) {
  emit('change', value);
  open.value = false;
}
</script>

<template>
  <PopoverRoot v-model:open="open">
    <PopoverTrigger as-child>
      <button
        type="button"
        class="flex items-center gap-1 px-2 py-1 rounded-lg hover:bg-slate-100 transition-colors text-[14px] font-bold text-slate-900 group"
      >
        <span class="capitalize">{{ props.options.find((o) => o.value === props.value)?.label }}</span>
        <ChevronDown class="h-3 w-3 text-slate-400 group-hover:text-pr transition-transform group-data-[state=open]:rotate-180" />
      </button>
    </PopoverTrigger>
    <PopoverPortal>
      <PopoverContent
        align="start"
        :side-offset="8"
        class="z-[9999] rounded-[24px] border border-slate-200/60 bg-white/98 backdrop-blur-2xl text-slate-900 shadow-[0_20px_40px_-12px_rgba(0,0,0,0.12)] outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 duration-200 w-40 p-1 max-h-60 overflow-y-auto scrollbar-thin scrollbar-thumb-slate-200"
      >
        <div class="flex flex-col gap-0.5">
          <button
            v-for="option in props.options"
            :key="option.value"
            type="button"
            :class="cn(
              'flex items-center justify-between px-3 py-2 rounded-lg text-sm font-medium transition-all',
              option.value === props.value ? 'bg-prbg text-pr' : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900',
            )"
            @click="pick(option.value)"
          >
            {{ option.label }}
            <Check v-if="option.value === props.value" class="h-3.5 w-3.5" />
          </button>
        </div>
      </PopoverContent>
    </PopoverPortal>
  </PopoverRoot>
</template>
