<script setup lang="ts">
// Port từ FormDatePicker.tsx: ô chọn ngày + popover gồm lối tắt, chọn nhanh năm, lịch và chân popover.
// Giá trị v-model là chuỗi 'yyyy-MM-dd' (hoặc null) giống field.value của bản gốc.
import { computed, ref, useId } from 'vue';
import { useI18n } from 'vue-i18n';
import { PopoverContent, PopoverPortal, PopoverRoot, PopoverTrigger } from 'reka-ui';
import { addDays, endOfMonth, getYear, isSameDay, setYear, startOfDay, startOfMonth, subDays } from 'date-fns';
import { Calendar as CalendarIcon, ChevronDown, RotateCcw, X } from '@lucide/vue';
import FormItem from './FormItem.vue';
import DxCalendar from './DxCalendar.vue';
import { cn } from '@/shared/utils/cn';
import { formatDate, parseDate, toIsoDate } from '@/shared/utils/date';

const props = withDefaults(
  defineProps<{
    label?: string;
    description?: string;
    placeholder?: string;
    required?: boolean;
    class?: string;
    error?: string;
    hideMessage?: boolean;
    allowClear?: boolean;
  }>(),
  {
    label: undefined,
    description: undefined,
    placeholder: undefined,
    required: false,
    class: '',
    error: undefined,
    hideMessage: false,
    allowClear: true,
  },
);

const model = defineModel<string | null>({ default: null });
const { t } = useI18n();
const id = useId();
const open = ref(false);
const calendarMonth = ref<Date>(new Date());

const dateValue = computed(() => parseDate(model.value));
const currentYear = getYear(new Date());

const presets = computed(() => [
  { key: 'today', label: t('component.form_date_picker.opt_today'), value: startOfDay(new Date()) },
  { key: 'yesterday', label: t('component.form_date_picker.opt_yesterday'), value: startOfDay(subDays(new Date(), 1)) },
  { key: 'tomorrow', label: t('component.form_date_picker.opt_tomorrow'), value: startOfDay(addDays(new Date(), 1)) },
  { key: 'month_start', label: t('component.form_date_picker.opt_month_start'), value: startOfMonth(new Date()) },
  { key: 'month_end', label: t('component.form_date_picker.opt_month_end'), value: startOfDay(endOfMonth(new Date())) },
]);

function select(date: Date | undefined) {
  if (date) {
    model.value = toIsoDate(date);
    calendarMonth.value = date;
    setTimeout(() => (open.value = false), 100);
  } else {
    model.value = null;
  }
}

function pickYear(year: number) {
  const next = setYear(dateValue.value ?? new Date(), year);
  model.value = toIsoDate(next);
  calendarMonth.value = next;
}

function onOpenChange(next: boolean) {
  open.value = next;
  if (next && dateValue.value) calendarMonth.value = dateValue.value;
}
</script>

<template>
  <FormItem :label="props.label" :for-id="id" :required="props.required" :description="props.description" :error="props.error" :hide-message="props.hideMessage">
    <PopoverRoot :open="open" @update:open="onOpenChange">
      <PopoverTrigger as-child>
        <button
          :id="id"
          type="button"
          :class="cn(
            'flex h-9 lg:h-10 w-full items-center justify-between rounded-xl border border-slate-200 bg-white px-4 lg:px-5 py-2 text-left text-[13.5px] font-medium text-slate-900 transition-all duration-200 hover:border-slate-300 outline-none focus:border-pr focus:ring-4 focus:ring-pr/10 group relative',
            props.class,
            !model && 'text-slate-400 font-normal',
            !!props.error && '!border-red-500 !bg-red-50/30 focus:ring-red-500/10 focus:border-red-500',
          )"
        >
          <div class="flex items-center gap-3">
            <CalendarIcon :class="cn('h-4 w-4 text-slate-400 transition-colors group-hover:text-pr', !!props.error && 'text-red-500')" />
            <span v-if="dateValue" class="text-slate-900">{{ formatDate(dateValue) }}</span>
            <span v-else>{{ props.placeholder ?? $t('component.form_date_picker.ph_select_date') }}</span>
          </div>
          <div class="flex items-center gap-2">
            <div
              v-if="dateValue && props.allowClear"
              role="button"
              tabindex="0"
              class="p-1 hover:bg-slate-100 rounded-md transition-colors text-slate-400 hover:text-red-500"
              :aria-label="$t('component.form_date_picker.btn_clear')"
              @pointerdown.prevent.stop
              @click.stop="select(undefined)"
              @keydown.enter.stop.prevent="select(undefined)"
              @keydown.space.stop.prevent="select(undefined)"
            >
              <X class="h-3.5 w-3.5" />
            </div>
            <ChevronDown class="h-4 w-4 text-slate-300 transition-transform duration-300 group-data-[state=open]:rotate-180 group-data-[state=open]:text-pr" />
          </div>
        </button>
      </PopoverTrigger>

      <PopoverPortal>
        <PopoverContent
          align="start"
          :side-offset="8"
          class="z-[9999] rounded-[24px] border border-slate-200/60 bg-white/98 backdrop-blur-2xl text-slate-900 shadow-[0_20px_40px_-12px_rgba(0,0,0,0.12)] outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 duration-200 w-auto p-0 flex flex-col sm:flex-row overflow-hidden"
        >
          <!-- Lối tắt -->
          <div class="w-full sm:w-[140px] border-b sm:border-b-0 sm:border-r border-slate-100 p-3 flex flex-col gap-1 bg-slate-50/50">
            <div class="text-[10px] font-bold text-slate-400 uppercase tracking-widest px-2 mb-2">{{ $t('component.form_date_picker.lbl_shortcut') }}</div>
            <button
              v-for="preset in presets"
              :key="preset.key"
              type="button"
              :class="cn(
                'w-full text-left px-3 py-2 rounded-lg text-[13px] font-medium transition-all',
                dateValue && isSameDay(dateValue, preset.value)
                  ? 'bg-pr text-white shadow-md shadow-pr/20'
                  : 'text-slate-600 hover:bg-white hover:text-pr hover:shadow-sm',
              )"
              @click="select(preset.value)"
            >
              {{ preset.label }}
            </button>

            <!-- Chọn nhanh năm -->
            <div class="mt-4 pt-4 border-t border-slate-100 px-2 flex flex-col gap-2">
              <div class="text-[10px] font-bold text-slate-400 uppercase tracking-widest mb-1">{{ $t('component.form_date_picker.lbl_year') }}</div>
              <div class="flex flex-wrap gap-1">
                <button
                  v-for="year in [currentYear - 1, currentYear, currentYear + 1]"
                  :key="year"
                  type="button"
                  :class="cn(
                    'px-2 py-1 rounded-md text-[11px] font-bold border transition-all',
                    dateValue && getYear(dateValue) === year
                      ? 'bg-pr border-pr text-white'
                      : 'bg-white border-slate-100 text-slate-500 hover:border-pr hover:text-pr',
                  )"
                  @click="pickYear(year)"
                >
                  {{ year }}
                </button>
              </div>
            </div>
          </div>

          <!-- Lịch -->
          <div class="flex flex-col">
            <DxCalendar v-model:month="calendarMonth" :selected="dateValue" @select="select" />

            <div class="p-3 border-t border-slate-100 flex items-center justify-between gap-4 bg-slate-50/30">
              <button
                type="button"
                class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] font-bold text-pr hover:bg-prbg transition-colors"
                @click="select(startOfDay(new Date()))"
              >
                <RotateCcw class="h-3.5 w-3.5" />
                {{ $t('component.form_date_picker.btn_today') }}
              </button>
              <div class="flex items-center gap-2">
                <button
                  v-if="dateValue"
                  type="button"
                  class="px-3 py-1.5 rounded-lg text-[12px] font-bold text-slate-500 hover:bg-slate-100 transition-colors"
                  @click="select(undefined)"
                >
                  {{ $t('component.form_date_picker.btn_clear') }}
                </button>
                <button
                  type="button"
                  class="px-4 py-1.5 rounded-lg bg-slate-900 text-white text-[12px] font-bold hover:bg-slate-800 transition-colors shadow-sm"
                  @click="open = false"
                >
                  {{ $t('component.form_date_picker.btn_close') }}
                </button>
              </div>
            </div>
          </div>
        </PopoverContent>
      </PopoverPortal>
    </PopoverRoot>
  </FormItem>
</template>
