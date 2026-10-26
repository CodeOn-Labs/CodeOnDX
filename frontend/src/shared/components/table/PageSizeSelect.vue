<script setup lang="ts">
// Port từ PageSizeSelect.tsx (Radix Select → Reka UI Select, giữ nguyên class và data-state).
import { SelectContent, SelectIcon, SelectItem, SelectItemIndicator, SelectItemText, SelectPortal, SelectRoot, SelectTrigger, SelectValue, SelectViewport } from 'reka-ui';
import { Check, ChevronDown, Rows3 } from '@lucide/vue';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ value: number; options: number[] }>();
const emit = defineEmits<{ change: [size: number] }>();

const isAll = (size: number) => size >= 9999 || size === -1;
</script>

<template>
  <SelectRoot :model-value="String(props.value)" @update:model-value="emit('change', Number($event))">
    <SelectTrigger
      :class="cn(
        'group inline-flex h-8 cursor-pointer items-center gap-2 rounded-lg border border-slate-200 bg-white',
        'pl-2.5 pr-2 text-[13px] font-semibold text-slate-700 shadow-sm outline-none',
        'transition-all hover:border-pr/40 hover:bg-pr/[0.03] hover:text-pr',
        'focus:border-pr focus:ring-2 focus:ring-pr/20',
        'data-[state=open]:border-pr data-[state=open]:bg-pr/[0.04] data-[state=open]:ring-2 data-[state=open]:ring-pr/20',
      )"
      :aria-label="$t('component.page_size_select.lbl_rows_per_page')"
    >
      <Rows3 :size="13" class="text-slate-400 transition-colors group-hover:text-pr group-data-[state=open]:text-pr" />
      <SelectValue>
        {{ isAll(props.value) ? $t('component.page_size_select.opt_no_pagination') : $t('component.page_size_select.unit_rows', { count: props.value }) }}
      </SelectValue>
      <SelectIcon>
        <ChevronDown
          :size="13"
          class="text-slate-400 transition-all duration-200 group-hover:text-pr group-data-[state=open]:rotate-180 group-data-[state=open]:text-pr"
        />
      </SelectIcon>
    </SelectTrigger>

    <SelectPortal>
      <SelectContent
        position="popper"
        :side-offset="6"
        align="end"
        :class="cn(
          'z-[999] min-w-[130px] overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl shadow-slate-200/60',
          'animate-in fade-in-0 zoom-in-95 data-[side=top]:slide-in-from-bottom-2 data-[side=bottom]:slide-in-from-top-2',
        )"
      >
        <div class="border-b border-slate-100 px-3 py-2">
          <span class="text-[10px] font-black uppercase tracking-widest text-slate-400">{{ $t('component.page_size_select.title_menu') }}</span>
        </div>
        <SelectViewport class="p-1.5">
          <SelectItem
            v-for="size in props.options"
            :key="size"
            :value="String(size)"
            :class="cn(
              'relative flex cursor-pointer select-none items-center justify-between rounded-lg px-3 py-2 text-[13px] font-semibold text-slate-700',
              'outline-none transition-colors hover:bg-pr/5 hover:text-pr',
              'data-[highlighted]:bg-pr/5 data-[highlighted]:text-pr',
              'data-[state=checked]:bg-pr/10 data-[state=checked]:text-pr',
            )"
          >
            <SelectItemText>
              <span v-if="isAll(size)">{{ $t('component.page_size_select.opt_no_pagination') }}</span>
              <template v-else>
                <span>{{ size }}</span>
                <span class="ml-1.5 text-[11px] font-medium text-slate-400 data-[state=checked]:text-pr/60">{{ $t('component.page_size_select.unit_row') }}</span>
              </template>
            </SelectItemText>
            <SelectItemIndicator>
              <Check :size="13" class="text-pr" />
            </SelectItemIndicator>
          </SelectItem>
        </SelectViewport>
      </SelectContent>
    </SelectPortal>
  </SelectRoot>
</template>
