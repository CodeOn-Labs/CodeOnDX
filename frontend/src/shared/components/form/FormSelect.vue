<script setup lang="ts">
// Port từ FormSelect.tsx:
// - searchable hoặc multiple → ô chọn có ô tìm kiếm trong Popover (SearchableSelectInner của bản React);
// - còn lại → Select chuẩn (Radix Select → Reka UI Select), cùng class.
import { computed, nextTick, ref, useId } from 'vue';
import { useI18n } from 'vue-i18n';
import {
  PopoverContent, PopoverPortal, PopoverRoot, PopoverTrigger,
  SelectContent, SelectIcon, SelectItem, SelectItemIndicator, SelectItemText, SelectPortal, SelectRoot, SelectTrigger, SelectValue, SelectViewport,
} from 'reka-ui';
import { Check, ChevronDown, Info, Search, X } from '@lucide/vue';
import FormItem from './FormItem.vue';
import type { SelectOption } from './types';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(
  defineProps<{
    options: SelectOption[];
    label?: string;
    placeholder?: string;
    searchPlaceholder?: string;
    description?: string;
    error?: string;
    required?: boolean;
    hideMessage?: boolean;
    triggerClass?: string;
    allowClear?: boolean;
    searchable?: boolean;
    multiple?: boolean;
    disabled?: boolean;
  }>(),
  {
    label: undefined,
    placeholder: undefined,
    searchPlaceholder: undefined,
    description: undefined,
    error: undefined,
    required: false,
    hideMessage: false,
    triggerClass: '',
    allowClear: true,
    searchable: false,
    multiple: false,
    disabled: false,
  },
);

const model = defineModel<string | string[] | null>({ default: null });
const { t } = useI18n();
const id = useId();

const open = ref(false);
const search = ref('');
const searchInput = ref<HTMLInputElement | null>(null);

const values = computed<string[]>(() => {
  const v = model.value;
  if (Array.isArray(v)) return v;
  return v ? [v] : [];
});
const selectedLabels = computed(() => props.options.filter((o) => values.value.includes(o.value)).map((o) => o.label));
const filtered = computed(() => {
  const q = search.value.trim().toLowerCase();
  if (!q) return props.options;
  return props.options.filter((o) => o.label.toLowerCase().includes(q) || (o.description?.toLowerCase().includes(q) ?? false));
});

function toggleValue(value: string) {
  if (props.multiple) {
    model.value = values.value.includes(value) ? values.value.filter((v) => v !== value) : [...values.value, value];
  } else {
    model.value = value;
    open.value = false;
  }
}

function clear() {
  model.value = props.multiple ? [] : '';
}

async function onOpenChange(next: boolean) {
  open.value = next;
  if (next) {
    await nextTick();
    setTimeout(() => searchInput.value?.focus(), 50);
  } else {
    search.value = '';
  }
}

const usePopover = computed(() => props.searchable || props.multiple);
const triggerBase =
  'flex h-9 lg:h-10 w-full items-center justify-between rounded-xl border border-slate-200 bg-white px-4 lg:px-5 py-2 text-[13.5px] font-medium text-slate-900 outline-none ring-0 ring-transparent focus:outline-none focus-visible:outline-none focus:ring-offset-0 transition-[border-color,background-color,box-shadow] duration-200 hover:border-slate-400/50 focus:border-pr focus:ring-[3.5px] focus:ring-pr/15 focus:bg-white disabled:cursor-not-allowed disabled:opacity-50 group relative';
const placeholderText = computed(() => props.placeholder ?? t('component.form_select.ph_select'));
</script>

<template>
  <FormItem :label="props.label" :for-id="id" :required="props.required" :description="props.description" :error="props.error" :hide-message="props.hideMessage">
    <!-- Ô chọn có tìm kiếm / chọn nhiều -->
    <PopoverRoot v-if="usePopover" :open="open" @update:open="onOpenChange">
      <PopoverTrigger as-child>
        <button
          :id="id"
          type="button"
          :disabled="props.disabled"
          :class="cn(triggerBase, props.triggerClass, !!props.error && '!border-red-500 !bg-red-50/30 focus:ring-red-500/15 focus:border-red-500')"
        >
          <div class="flex items-center gap-3 overflow-hidden flex-1">
            <slot name="prefix" />
            <div class="flex items-center gap-2 flex-1 truncate">
              <div v-if="props.multiple && values.length > 0" class="flex items-center gap-1.5">
                <span class="inline-flex h-5 items-center justify-center rounded-md bg-pr/10 px-1.5 text-[11px] font-bold text-pr animate-in zoom-in-75 duration-200">
                  {{ values.length }}
                </span>
                <span class="text-slate-600 truncate">{{ selectedLabels.join(', ') }}</span>
              </div>
              <span v-else :class="cn('truncate text-left', values.length === 0 && 'text-slate-400 font-normal')">
                {{ selectedLabels[0] || placeholderText }}
              </span>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <span
              v-if="values.length > 0 && props.allowClear"
              role="button"
              tabindex="0"
              class="flex h-5 w-5 items-center justify-center rounded-full text-slate-400 hover:bg-red-50 hover:text-red-500 transition-all cursor-pointer active:scale-90"
              :aria-label="$t('component.form_select.btn_clear')"
              @pointerdown.prevent.stop
              @click.prevent.stop="clear"
              @keydown.enter.prevent.stop="clear"
              @keydown.space.prevent.stop="clear"
            >
              <X :size="12" :stroke-width="3" />
            </span>
            <ChevronDown :class="cn('h-4 w-4 text-slate-400 transition-transform duration-300', open && 'rotate-180 text-pr')" />
          </div>
        </button>
      </PopoverTrigger>

      <PopoverPortal>
        <PopoverContent
          align="start"
          :side-offset="6"
          :align-offset="0"
          class="z-[9999] w-[var(--reka-popover-trigger-width)] min-w-[240px] rounded-2xl border border-slate-200/60 bg-white shadow-[0_20px_50px_rgba(0,0,0,0.2)] outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 duration-300 overflow-hidden"
          @open-auto-focus.prevent
        >
          <div class="flex items-center gap-3 px-3.5 py-2 border-b border-slate-100 bg-slate-50/50">
            <Search class="h-4 w-4 text-slate-400 flex-shrink-0" />
            <input
              ref="searchInput"
              v-model="search"
              type="text"
              :placeholder="props.searchPlaceholder ?? $t('component.form_select.ph_search')"
              class="flex-1 text-[13px] text-slate-900 bg-transparent outline-none placeholder:text-slate-400"
            />
            <button
              v-if="search"
              type="button"
              class="text-slate-400 hover:text-red-500 transition-colors outline-none focus:outline-none"
              :aria-label="$t('component.form_select.btn_clear_search')"
              @click="search = ''"
            >
              <X :size="14" :stroke-width="2.5" />
            </button>
          </div>

          <div class="max-h-72 overflow-y-auto p-1.5 space-y-0.5" @wheel.stop>
            <div v-if="filtered.length === 0" class="flex flex-col items-center justify-center py-10 px-4 text-center animate-in fade-in duration-300">
              <div class="h-10 w-10 rounded-full bg-slate-50 flex items-center justify-center mb-3 text-slate-300">
                <Info :size="20" />
              </div>
              <p class="text-xs font-semibold text-slate-400">{{ $t('component.form_select.empty_no_result') }}</p>
              <p class="text-[10px] text-slate-300 mt-1">{{ $t('component.form_select.hint_try_other_keyword') }}</p>
            </div>
            <template v-else>
            <button
              v-for="option in filtered"
              :key="option.value"
              type="button"
              :disabled="option.disabled"
              :class="cn(
                'relative flex w-full items-center rounded-xl py-2 pl-10 pr-3 text-[12.5px] font-medium transition-all group text-left outline-none focus:outline-none focus:bg-slate-50',
                values.includes(option.value) ? 'bg-pr/5 text-pr font-black' : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900',
                option.disabled && 'opacity-40 cursor-not-allowed grayscale',
              )"
              @click="toggleValue(option.value)"
            >
              <span class="absolute left-3.5 flex h-4 w-4 items-center justify-center">
                <Check v-if="values.includes(option.value)" class="h-4 w-4 stroke-[3.5] animate-in zoom-in-50 duration-200" />
                <div v-else-if="props.multiple" class="h-4 w-4 rounded border-2 border-slate-200 group-hover:border-pr/30 transition-colors" />
              </span>
              <div class="flex flex-col items-start gap-0.5 overflow-hidden flex-1">
                <div class="flex items-center gap-2 w-full">
                  <span v-if="option.icon" :class="cn('text-slate-400 group-hover:text-pr transition-colors shrink-0', values.includes(option.value) && 'text-pr')">
                    <component :is="option.icon" :class="cn('w-4 h-4', option.iconClass)" />
                  </span>
                  <span class="truncate flex-1">{{ option.label }}</span>
                </div>
                <span
                  v-if="option.description"
                  :class="cn('text-[11px] font-normal leading-tight truncate w-full', values.includes(option.value) ? 'text-pr/70' : 'text-slate-400 group-hover:text-slate-500')"
                >
                  {{ option.description }}
                </span>
              </div>
              <div
                v-if="values.includes(option.value)"
                class="absolute left-0 top-1/2 -translate-y-1/2 h-6 w-1 rounded-r-full bg-pr animate-in slide-in-from-left-full duration-300"
              />
            </button>
            </template>
          </div>
        </PopoverContent>
      </PopoverPortal>
    </PopoverRoot>

    <!-- Select chuẩn -->
    <SelectRoot
      v-else
      :model-value="typeof model === 'string' && model ? model : undefined"
      :disabled="props.disabled"
      @update:model-value="model = $event == null ? '' : String($event)"
    >
      <SelectTrigger
        :id="id"
        :class="cn(triggerBase, !!props.error && 'border-red-500 bg-red-50/30 focus:border-red-500 focus:ring-red-500/15', props.triggerClass)"
      >
        <div class="flex items-center gap-2.5 overflow-hidden">
          <slot name="prefix" />
          <div class="flex-1 overflow-hidden text-left truncate">
            <SelectValue :placeholder="placeholderText">{{ props.options.find((o) => o.value === model)?.label }}</SelectValue>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <span
            v-if="!!model && props.allowClear"
            role="button"
            tabindex="0"
            class="flex h-5 w-5 items-center justify-center rounded-full text-slate-400 hover:bg-red-50 hover:text-red-500 transition-all cursor-pointer active:scale-90"
            :aria-label="$t('component.form_select.btn_clear')"
            @pointerdown.prevent.stop
            @click.prevent.stop="clear"
            @keydown.enter.prevent.stop="clear"
            @keydown.space.prevent.stop="clear"
          >
            <X :size="12" :stroke-width="3" />
          </span>
          <SelectIcon as-child>
            <ChevronDown class="h-4 w-4 text-slate-400 transition-transform duration-300 group-data-[state=open]:rotate-180 group-data-[state=open]:text-pr" />
          </SelectIcon>
        </div>
      </SelectTrigger>
      <SelectPortal>
        <SelectContent
          position="popper"
          class="relative z-[9999] max-h-96 min-w-[8rem] overflow-hidden rounded-2xl border border-slate-200/60 bg-white/95 backdrop-blur-xl text-slate-900 shadow-[0_20px_50px_rgba(0,0,0,0.15)] outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 duration-300 data-[side=bottom]:translate-y-2 data-[side=left]:-translate-x-2 data-[side=right]:translate-x-2 data-[side=top]:-translate-y-2"
        >
          <SelectViewport class="p-1.5 h-[var(--reka-select-content-available-height)] w-full min-w-[var(--reka-select-trigger-width)]">
            <SelectItem
              v-for="option in props.options"
              :key="option.value"
              :value="option.value"
              :disabled="option.disabled"
              :class="cn(
                'relative flex w-full cursor-pointer select-none items-center rounded-xl py-2 pl-10 pr-3 text-[12.5px] font-medium text-slate-600 outline-none transition-all',
                'hover:bg-slate-50 hover:text-slate-900',
                'focus:bg-slate-50 focus:text-slate-900',
                'data-[state=checked]:bg-pr/5 data-[state=checked]:text-pr data-[state=checked]:font-black',
                'data-[disabled]:pointer-events-none data-[disabled]:opacity-40 group',
              )"
            >
              <span class="absolute left-3.5 flex h-4 w-4 items-center justify-center">
                <SelectItemIndicator>
                  <Check class="h-4 w-4 stroke-[3.5] animate-in zoom-in-50 duration-200" />
                </SelectItemIndicator>
              </span>
              <div class="flex flex-col gap-0.5">
                <div class="flex items-center gap-2">
                  <span v-if="option.icon" class="text-slate-400 group-focus:text-pr transition-colors">
                    <component :is="option.icon" :class="cn('w-4 h-4', option.iconClass)" />
                  </span>
                  <SelectItemText>{{ option.label }}</SelectItemText>
                </div>
                <span v-if="option.description" class="text-[11px] font-normal text-slate-400 leading-tight group-focus:text-pr/70">
                  {{ option.description }}
                </span>
              </div>
              <div class="absolute left-0 top-1/2 -translate-y-1/2 h-6 w-1 rounded-r-full bg-pr scale-y-0 group-data-[state=checked]:scale-y-100 transition-transform duration-200" />
            </SelectItem>
          </SelectViewport>
        </SelectContent>
      </SelectPortal>
    </SelectRoot>
  </FormItem>
</template>
