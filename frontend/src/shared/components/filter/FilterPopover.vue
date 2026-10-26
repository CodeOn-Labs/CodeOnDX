<script setup lang="ts">
// Port từ FilterPopover.tsx (Radix Popover → Reka UI Popover).
import { PopoverContent, PopoverPortal, PopoverRoot, PopoverTrigger } from 'reka-ui';
import { RotateCcw, Search, SlidersHorizontal, X } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(
  defineProps<{
    open: boolean;
    activeCount?: number;
    isApplying?: boolean;
    contentClass?: string;
    triggerClass?: string;
  }>(),
  { activeCount: 0, isApplying: false, contentClass: '', triggerClass: '' },
);

const emit = defineEmits<{ 'update:open': [open: boolean]; apply: []; reset: [] }>();
</script>

<template>
  <PopoverRoot :open="props.open" @update:open="emit('update:open', $event)">
    <PopoverTrigger as-child>
      <button
        type="button"
        :class="cn(
          'relative flex h-9 lg:h-10 items-center gap-2 rounded-xl border border-slate-200 bg-white px-3.5 lg:px-4 text-[13px] font-bold text-slate-600 transition-all hover:border-pr hover:bg-pr/5 hover:text-pr active:scale-95',
          props.open && 'border-pr bg-pr/5 text-pr ring-4 ring-pr/10',
          props.activeCount > 0 && 'border-pr/30 bg-pr/5 text-pr',
          props.triggerClass,
        )"
      >
        <SlidersHorizontal :size="16" :stroke-width="2.5" :class="cn('transition-transform duration-300', props.open && 'rotate-180')" />
        <span>{{ $t('component.filter_popover.btn_open') }}</span>
        <span
          v-if="props.activeCount > 0"
          class="absolute -right-2 -top-2 flex h-5 min-w-[20px] items-center justify-center rounded-full bg-pr px-1.5 text-[10px] font-bold text-white shadow-lg ring-2 ring-white animate-in zoom-in duration-300"
        >
          {{ props.activeCount }}
        </span>
      </button>
    </PopoverTrigger>

    <PopoverPortal>
      <PopoverContent
        align="end"
        :side-offset="8"
        :class="cn(
          'z-[100] w-[340px] overflow-hidden rounded-2xl border border-slate-200/60 bg-white/95 p-0 backdrop-blur-xl shadow-[0_20px_50px_rgba(0,0,0,0.15)] outline-none animate-in fade-in zoom-in-95 duration-300',
          props.contentClass,
        )"
      >
        <div class="flex items-center justify-between border-b border-slate-100 bg-slate-50/50 px-4 py-3">
          <div class="flex items-center gap-2">
            <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-pr/10 text-pr">
              <SlidersHorizontal :size="14" />
            </div>
            <h3 class="text-[13px] font-black text-slate-900">{{ $t('component.filter_popover.title_popover') }}</h3>
          </div>
          <button
            type="button"
            class="flex items-center gap-1.5 text-[10px] font-black uppercase tracking-wider text-slate-400 transition-colors hover:text-red-500"
            @click="emit('reset')"
          >
            <RotateCcw :size="12" :stroke-width="2.5" />
            {{ $t('component.filter_popover.btn_reset') }}
          </button>
        </div>

        <div class="max-h-[400px] overflow-y-auto px-4 py-4">
          <div class="space-y-4">
            <slot />
          </div>
        </div>

        <div class="flex items-center gap-2 border-t border-slate-100 bg-slate-50/50 px-4 py-3">
          <Button variant="danger" size="sm" class="flex-1 rounded-xl text-[12px] font-bold h-8" @click="emit('update:open', false)">
            <template #leftIcon><X :size="14" /></template>
            {{ $t('component.filter_popover.btn_cancel') }}
          </Button>
          <Button
            size="sm"
            class="flex-1 rounded-xl shadow-lg shadow-pr/20 text-[12px] font-bold h-8"
            :loading="props.isApplying"
            :disabled="props.isApplying"
            @click="emit('apply')"
          >
            <template #leftIcon><Search :size="14" /></template>
            {{ $t('component.filter_popover.btn_apply') }}
          </Button>
        </div>
      </PopoverContent>
    </PopoverPortal>
  </PopoverRoot>
</template>
