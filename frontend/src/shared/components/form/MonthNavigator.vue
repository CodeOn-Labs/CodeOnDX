<script setup lang="ts">
// Chọn tháng: ‹ [Tháng] [Năm] › — dùng ở Chấm công, Công số, Báo cáo tháng (thay MonthYearSelect.tsx của bản gốc).
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { ChevronLeft, ChevronRight } from '@lucide/vue';
import MonthYearPicker from './MonthYearPicker.vue';

/** v-model dạng 'yyyy-MM' */
const model = defineModel<string>({ required: true });
const { t } = useI18n();

const year = computed(() => Number(model.value.slice(0, 4)));
const month = computed(() => Number(model.value.slice(5, 7)));
const set = (y: number, m: number) => {
  const d = new Date(y, m - 1, 1);
  model.value = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
};
const monthOptions = computed(() => Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: t('component.month_navigator.lbl_month', { month: i + 1 }) })));
const yearOptions = computed(() => {
  const now = new Date().getFullYear();
  return Array.from({ length: 8 }, (_, i) => now - 5 + i).map((y) => ({ value: y, label: String(y) }));
});
</script>

<template>
  <div class="flex items-center gap-1 rounded-xl border border-[#E4E8F3] bg-white px-1 h-9 lg:h-10 shadow-sm">
    <button type="button" class="flex h-7 w-7 items-center justify-center rounded-lg text-[#8994AD] hover:bg-slate-50 hover:text-[#4C5568]" :aria-label="$t('component.month_navigator.btn_prev')" @click="set(year, month - 1)">
      <ChevronLeft class="h-4 w-4" />
    </button>
    <MonthYearPicker :value="month" :options="monthOptions" @change="set(year, $event)" />
    <MonthYearPicker :value="year" :options="yearOptions" @change="set($event, month)" />
    <button type="button" class="flex h-7 w-7 items-center justify-center rounded-lg text-[#8994AD] hover:bg-slate-50 hover:text-[#4C5568]" :aria-label="$t('component.month_navigator.btn_next')" @click="set(year, month + 1)">
      <ChevronRight class="h-4 w-4" />
    </button>
  </div>
</template>
