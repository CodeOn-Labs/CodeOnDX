<script setup lang="ts">
// Ngày lễ của tenant: không tính vào số ngày nghỉ phép và ngày công (mục 3.6 kế hoạch). Thêm / xoá lưu ngay.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query';
import { CalendarDays, ChevronLeft, ChevronRight, Plus, Trash2 } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import { configApi, firstFieldErrors, type Holiday } from '@/modules/admin/api/config.api';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const year = ref(new Date().getFullYear());
const holidaysQuery = useQuery({
  queryKey: computed(() => ['holidays', year.value] as const),
  queryFn: () => configApi.holidays(year.value),
});
const holidays = computed(() => holidaysQuery.data.value ?? []);

const newDate = ref<string | null>(null);
const newName = ref('');
const errors = ref<Record<string, string>>({});

const createMutation = useMutation({
  mutationFn: () => configApi.createHoliday({ date: newDate.value ?? '', name: newName.value.trim(), isPaid: true }),
  onSuccess: (res) => {
    toast.success(res.messageText);
    newDate.value = null;
    newName.value = '';
    errors.value = {};
    void queryClient.invalidateQueries({ queryKey: ['holidays'] });
  },
  onError: (err: Error) => {
    errors.value = firstFieldErrors(err);
    if (Object.keys(errors.value).length === 0) toast.error(err.message);
  },
});

function add() {
  const e: Record<string, string> = {};
  if (!newDate.value) e.date = t('validation.required');
  if (!newName.value.trim()) e.name = t('validation.required');
  errors.value = e;
  if (Object.keys(e).length === 0) createMutation.mutate();
}

async function remove(holiday: Holiday) {
  const ok = await confirm.ask({
    title: t('admin_setting.confirm_delete_holiday_title'),
    description: t('admin_setting.confirm_delete_holiday_desc', { name: holiday.name, date: formatDate(holiday.date) }),
    type: 'danger',
  });
  if (!ok) return;
  try {
    const res = await configApi.deleteHoliday(holiday.id);
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['holidays'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}
</script>

<template>
  <div class="bg-white border border-slate-200/80 rounded-[24px] p-6 shadow-sm flex flex-col gap-5">
    <div class="flex items-center justify-between gap-2.5 pb-2 border-b border-slate-100">
      <div class="flex items-center gap-2.5">
        <div class="p-2 rounded-xl bg-rose-50 text-rose-600 border border-rose-100"><CalendarDays :size="16" class="stroke-[2.5]" /></div>
        <div>
          <h3 class="text-[13px] font-black text-slate-800 tracking-tight uppercase">{{ $t('admin_setting.title_holiday_section') }}</h3>
          <p class="text-[10px] text-slate-400 font-medium">{{ $t('admin_setting.hint_holiday_section') }}</p>
        </div>
      </div>
      <div class="flex items-center gap-1 rounded-xl border border-slate-200 bg-white p-0.5">
        <button
          type="button"
          class="h-7 w-7 flex items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-600"
          :aria-label="$t('admin_setting.btn_prev_year')"
          @click="year--"
        >
          <ChevronLeft :size="14" />
        </button>
        <span class="px-2 text-[12.5px] font-black text-slate-700 tabular-nums">{{ year }}</span>
        <button
          type="button"
          class="h-7 w-7 flex items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-600"
          :aria-label="$t('admin_setting.btn_next_year')"
          @click="year++"
        >
          <ChevronRight :size="14" />
        </button>
      </div>
    </div>

    <form class="grid grid-cols-1 md:grid-cols-[200px_1fr_auto] gap-3 items-start" @submit.prevent="add">
      <FormDatePicker v-model="newDate" :label="$t('admin_setting.lbl_holiday_date')" :error="errors.date" required />
      <FormItem :label="$t('admin_setting.lbl_holiday_name')" for-id="holiday-name" required :error="errors.name">
        <BaseInput id="holiday-name" v-model="newName" :placeholder="$t('admin_setting.ph_holiday_name')" :error="Boolean(errors.name)" />
      </FormItem>
      <Button type="submit" variant="outline" class="md:mt-[26px]" :loading="createMutation.isPending.value">
        <template #leftIcon><Plus :size="14" /></template>
        {{ $t('admin_setting.btn_add_holiday') }}
      </Button>
    </form>

    <div class="overflow-hidden rounded-xl border border-slate-200">
      <p v-if="holidaysQuery.isLoading.value" class="py-6 text-center text-[12.5px] font-semibold text-slate-400">{{ $t('admin_setting.msg_loading') }}</p>
      <p v-else-if="holidays.length === 0" class="py-6 text-center text-[12.5px] font-semibold text-slate-400">{{ $t('admin_setting.empty_holidays', { year }) }}</p>
      <table v-else class="w-full border-collapse text-left">
        <thead>
          <tr class="border-b border-slate-200 bg-slate-50/80">
            <th class="p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500 w-[160px]">{{ $t('admin_setting.col_holiday_date') }}</th>
            <th class="p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500">{{ $t('admin_setting.col_holiday_name') }}</th>
            <th class="p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500 w-[90px] text-center">{{ $t('admin_setting.col_actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(h, idx) in holidays" :key="h.id" :class="cn('border-b border-slate-100 last:border-b-0', idx % 2 === 0 ? 'bg-white' : 'bg-slate-50/60')">
            <td class="p-3 text-[12.5px] font-bold text-slate-700 tabular-nums">{{ formatDate(h.date, 'EEE, dd/MM/yyyy') }}</td>
            <td class="p-3 text-[13px] font-semibold text-slate-700">{{ h.name }}</td>
            <td class="p-3">
              <div class="flex items-center justify-center">
                <button
                  type="button"
                  class="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-red-50 hover:text-red-500"
                  :title="$t('admin_setting.btn_delete_holiday')"
                  :aria-label="$t('admin_setting.btn_delete_holiday')"
                  @click="remove(h)"
                >
                  <Trash2 :size="14" />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
