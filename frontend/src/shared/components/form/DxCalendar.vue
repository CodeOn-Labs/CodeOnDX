<script setup lang="ts">
// Thay Calendar (react-day-picker) của FormDatePicker.tsx — dựng lại cùng cấu trúc bảng
// và cùng bộ class Tailwind đã khai báo trong classNames của bản gốc.
import { computed } from 'vue';
import {
  addDays, addMonths, eachDayOfInterval, endOfMonth, endOfWeek, format, getMonth, getYear,
  isSameDay, isSameMonth, isToday, setMonth, setYear, startOfMonth, startOfWeek, subMonths,
} from 'date-fns';
import { ChevronLeft, ChevronRight } from '@lucide/vue';
import MonthYearPicker from './MonthYearPicker.vue';
import { cn } from '@/shared/utils/cn';
import { dateFnsLocale } from '@/shared/utils/date';

const props = withDefaults(defineProps<{ selected?: Date; showOutsideDays?: boolean }>(), {
  selected: undefined,
  showOutsideDays: true,
});
const month = defineModel<Date>('month', { default: () => new Date() });
const emit = defineEmits<{ select: [date: Date] }>();

const locale = computed(() => dateFnsLocale());
const weekStartsOn = computed(() => (locale.value.options?.weekStartsOn ?? 1) as 0 | 1 | 2 | 3 | 4 | 5 | 6);

const weeks = computed(() => {
  const start = startOfWeek(startOfMonth(month.value), { weekStartsOn: weekStartsOn.value });
  const end = endOfWeek(endOfMonth(month.value), { weekStartsOn: weekStartsOn.value });
  const days = eachDayOfInterval({ start, end });
  const rows: Date[][] = [];
  for (let i = 0; i < days.length; i += 7) rows.push(days.slice(i, i + 7));
  return rows;
});

const weekdays = computed(() => {
  const start = startOfWeek(new Date(), { weekStartsOn: weekStartsOn.value });
  return Array.from({ length: 7 }, (_, i) => format(addDays(start, i), 'cccccc', { locale: locale.value }));
});

const monthOptions = computed(() => {
  const intlLocale = locale.value.code || 'vi-VN';
  const fmt = new Intl.DateTimeFormat(intlLocale, { month: 'long' });
  return Array.from({ length: 12 }, (_, i) => ({ value: i, label: fmt.format(setMonth(new Date(), i)) }));
});
const yearOptions = computed(() => {
  const now = getYear(new Date());
  return Array.from({ length: 101 }, (_, i) => ({ value: now - 50 + i, label: String(now - 50 + i) }));
});

function pickDay(day: Date) {
  if (!isSameMonth(day, month.value)) month.value = startOfMonth(day);
  emit('select', day);
}

// Modifier classes giữ nguyên classNames.selected / classNames.today của bản gốc
const selectedClass =
  "[&_button]:!bg-pr [&_button]:!text-white [&_button]:shadow-xl [&_button]:shadow-pr/25 [&_button]:after:!bg-white animate-in zoom-in-95 duration-300";
const todayClass =
  "[&_button]:text-pr [&_button]:after:content-[''] [&_button]:after:absolute [&_button]:after:bottom-[6px] [&_button]:after:left-1/2 [&_button]:after:-translate-x-1/2 [&_button]:after:h-1.5 [&_button]:after:w-1.5 [&_button]:after:rounded-full [&_button]:after:bg-pr transition-all";

const navButton =
  'h-8 w-8 flex items-center justify-center rounded-xl text-slate-400 transition-all border border-slate-100 bg-white outline-none hover:border-pr hover:text-pr hover:bg-prbg active:scale-95 shadow-sm';
</script>

<template>
  <div class="p-4">
    <div class="flex flex-col sm:flex-row space-y-4 sm:space-y-0">
      <div class="space-y-5">
        <div class="flex items-center justify-between h-10 px-1 relative mb-4">
          <div class="flex items-center gap-1" role="status" aria-live="polite">
            <MonthYearPicker :value="getMonth(month)" :options="monthOptions" @change="month = setMonth(month, $event)" />
            <MonthYearPicker :value="getYear(month)" :options="yearOptions" @change="month = setYear(month, $event)" />
          </div>
          <nav class="flex items-center gap-1">
            <button type="button" :class="navButton" :aria-label="$t('component.dx_calendar.btn_previous_month')" @click="month = subMonths(month, 1)">
              <ChevronLeft class="h-4 w-4 stroke-[2.5]" />
            </button>
            <button type="button" :class="navButton" :aria-label="$t('component.dx_calendar.btn_next_month')" @click="month = addMonths(month, 1)">
              <ChevronRight class="h-4 w-4 stroke-[2.5]" />
            </button>
          </nav>
        </div>

        <table role="grid" class="w-full border-collapse">
          <thead aria-hidden="true">
            <tr class="flex mb-3">
              <th v-for="name in weekdays" :key="name" scope="col" class="text-slate-400 rounded-md w-10 font-bold text-[10px] uppercase tracking-[0.15em] text-center">
                {{ name }}
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(week, wi) in weeks" :key="wi" class="flex w-full mt-1.5">
              <td
                v-for="day in week"
                :key="day.toISOString()"
                role="gridcell"
                :aria-selected="!!props.selected && isSameDay(day, props.selected)"
                :class="cn(
                  'h-10 w-10 p-0 relative flex items-center justify-center',
                  !!props.selected && isSameDay(day, props.selected) && selectedClass,
                  isToday(day) && todayClass,
                  !isSameMonth(day, month) && (props.showOutsideDays ? 'text-slate-300 opacity-20' : 'invisible'),
                )"
              >
                <button
                  v-if="props.showOutsideDays || isSameMonth(day, month)"
                  type="button"
                  class="h-10 w-10 p-0 font-semibold text-slate-600 rounded-xl transition-all inline-flex items-center justify-center cursor-pointer relative outline-none hover:bg-slate-100 hover:text-slate-900 focus:bg-slate-100 focus:text-slate-900"
                  :aria-label="format(day, 'PPPP', { locale: dateFnsLocale() })"
                  @click="pickDay(day)"
                >
                  {{ format(day, 'd') }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
