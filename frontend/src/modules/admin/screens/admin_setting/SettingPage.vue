<script setup lang="ts">
// Port SettingsPage.tsx — Quản trị → Cài đặt chung của tenant.
// Khác bản gốc:
//  - Bỏ nhóm "Cấu hình AI" (khoá API, model): theo D5 khoá AI chỉ nằm ở server (Integrations:Ai trong appsettings.Secrets.json).
//  - Bỏ "Ngày khóa sổ công số" và "Tỷ lệ sử dụng mục tiêu": đã có ở Quy tắc (TIMESHEET_LOCK_DATE_OF_MONTH, HR_UTILIZATION_TARGET_PERCENT).
//  - Giờ làm việc = giờ bắt đầu / kết thúc / nghỉ trưa (dùng cho Chấm công vào/ra) thay ô chọn "8 giờ/ngày".
//  - Thêm: báo cáo tháng song ngữ vi/ja (tuỳ chọn, D12), tài khoản & chấm công, ngày lễ.
import { computed, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query';
import { Building2, Clock, Eye, Mail, Save, Tags, Upload, UserCog, X } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import HolidayCard from './HolidayCard.vue';
import { configApi, firstFieldErrors, type Settings } from '@/modules/admin/api/config.api';
import { useMasterData } from '@/shared/composables/useMasterData';
import { enumLabel } from '@/shared/i18n/enum-label';
import { useToastStore } from '@/stores/toast';

const MAX_LOGO_BYTES = 300 * 1024;
const TIME_ZONES = ['Asia/Ho_Chi_Minh', 'Asia/Tokyo', 'Asia/Singapore', 'UTC'];
const WORK_WEEKS = ['MON-FRI', 'MON-SAT', 'MON-SAT_HALF'];

const { t } = useI18n();
const toast = useToastStore();
const queryClient = useQueryClient();

const settingsQuery = useQuery({ queryKey: ['settings'], queryFn: configApi.settings });
const { items: currencies } = useMasterData('currency');

const form = ref<Settings>({});
const errors = ref<Record<string, string>>({});
const previewOpen = ref(false);
watch(
  () => settingsQuery.data.value,
  (data) => {
    if (data) form.value = { ...data };
  },
  { immediate: true },
);

const isDirty = computed(() => {
  const saved = settingsQuery.data.value ?? {};
  return Object.keys(form.value).some((k) => form.value[k] !== saved[k]);
});

const timeZoneOptions = computed(() => TIME_ZONES.map((tz) => ({ value: tz, label: tz })));
const localeOptions = computed(() => [
  { value: 'vi', label: t('admin_setting.opt_locale_vi') },
  { value: 'en', label: t('admin_setting.opt_locale_en') },
]);
const workWeekOptions = computed(() => WORK_WEEKS.map((w) => ({ value: w, label: enumLabel('work_week', w) })));
const currencyOptions = computed(() => currencies.value.map((c) => ({ value: c.code, label: c.name })));
const monthOptions = computed(() => Array.from({ length: 12 }, (_, i) => ({ value: String(i + 1), label: t('admin_setting.opt_month', { month: i + 1 }) })));

const setField = (key: string, value: string | string[] | null) => {
  form.value = { ...form.value, [key]: Array.isArray(value) ? (value[0] ?? '') : (value ?? '') };
};
const boolField = (key: string) => form.value[key] === 'true';
const setBool = (key: string, value: boolean) => setField(key, value ? 'true' : 'false');

function onLogoChange(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  if (!file.type.startsWith('image/') || file.size > MAX_LOGO_BYTES) {
    errors.value = { ...errors.value, company_logo: t('admin_setting.err_logo_invalid') };
    return;
  }
  const reader = new FileReader();
  reader.onloadend = () => setField('company_logo', String(reader.result ?? ''));
  reader.readAsDataURL(file);
}

function validate(): boolean {
  const e: Record<string, string> = {};
  const f = form.value;
  if (!f.company_name?.trim()) e.company_name = t('validation.required');
  if (!f.notification_email?.trim()) e.notification_email = t('validation.required');
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.notification_email)) e.notification_email = t('validation.invalid_email');
  if (f.work_start_time && f.work_end_time && f.work_end_time <= f.work_start_time) e.work_end_time = t('admin_setting.err_end_before_start');
  errors.value = e;
  return Object.keys(e).length === 0;
}

const saveMutation = useMutation({
  mutationFn: () => {
    const saved = settingsQuery.data.value ?? {};
    const changes = Object.fromEntries(Object.entries(form.value).filter(([k, v]) => v !== saved[k]));
    return configApi.updateSettings(changes);
  },
  onSuccess: (res) => {
    toast.success(res.messageText);
    errors.value = {};
    queryClient.setQueryData(['settings'], res.data);
  },
  onError: (err: Error) => {
    errors.value = firstFieldErrors(err);
    toast.error(err.message);
  },
});

function save() {
  if (validate()) saveMutation.mutate();
}
</script>

<template>
  <PageLayout layout="C" content-padding="24px" disable-scroll>
    <div class="h-full flex flex-col min-h-0 gap-4">
      <div class="shrink-0 flex flex-col gap-3 rounded-2xl bg-white/60 backdrop-blur-xl p-4 lg:p-5 shadow-xl shadow-slate-200/40 border border-white/60 transition-all duration-300">
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div class="shrink-0 flex flex-col gap-1.5">
            <div class="flex items-center gap-2">
              <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
              <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ $t('admin_setting.lbl_kicker') }}</span>
            </div>
            <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">
              {{ $t('admin_setting.title_page') }}
            </h1>
          </div>
          <div class="flex items-center gap-4 self-start sm:self-center">
            <RouterLink to="/admin/categories" class="group flex items-center gap-2 text-[13px] font-semibold text-slate-600 hover:text-pr transition-colors whitespace-nowrap">
              <Tags :size="16" />
              {{ $t('admin_setting.btn_manage_categories') }}
            </RouterLink>
            <Button
              variant="primary"
              class="h-10 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25 whitespace-nowrap"
              :loading="saveMutation.isPending.value"
              :disabled="settingsQuery.isLoading.value || !isDirty"
              @click="save"
            >
              <template #leftIcon><Save :size="16" :stroke-width="3" /></template>
              {{ $t('admin_setting.btn_save') }}
            </Button>
          </div>
        </div>
      </div>

      <div class="flex-1 overflow-y-auto min-h-0 pb-12 pr-1 outline-none">
        <div v-if="settingsQuery.isLoading.value" class="grid grid-cols-1 lg:grid-cols-2 gap-6 w-full animate-in fade-in duration-300">
          <div v-for="i in 4" :key="i" class="bg-white border border-slate-200/80 rounded-[24px] p-6 shadow-sm flex flex-col gap-5">
            <div class="h-3.5 w-1/3 bg-[#e8ecf5] rounded animate-pulse" />
            <div v-for="j in 3" :key="j" class="flex flex-col gap-2">
              <div class="h-3 w-1/4 bg-[#e8ecf5] rounded animate-pulse" />
              <div class="h-10 w-full bg-[#f3f5fa] rounded-xl animate-pulse" />
            </div>
          </div>
        </div>

        <form v-else class="grid grid-cols-1 lg:grid-cols-2 gap-6" @submit.prevent="save">
          <!-- Nhóm 1: Thông tin chung -->
          <div class="bg-white border border-slate-200/80 rounded-[24px] p-6 shadow-sm flex flex-col gap-5">
            <div class="flex items-center gap-2.5 pb-2 border-b border-slate-100">
              <div class="p-2 rounded-xl bg-blue-50 text-blue-600 border border-blue-100"><Building2 :size="16" class="stroke-[2.5]" /></div>
              <div>
                <h3 class="text-[13px] font-black text-slate-800 tracking-tight uppercase">{{ $t('admin_setting.title_general_section') }}</h3>
                <p class="text-[10px] text-slate-400 font-medium">{{ $t('admin_setting.hint_general_section') }}</p>
              </div>
            </div>
            <div class="space-y-4">
              <FormItem :label="$t('admin_setting.lbl_company_name')" for-id="setting-company" required :error="errors.company_name">
                <BaseInput id="setting-company" :model-value="form.company_name" :error="Boolean(errors.company_name)" @update:model-value="setField('company_name', String($event ?? ''))" />
              </FormItem>

              <FormItem :label="$t('admin_setting.lbl_company_logo')" :error="errors.company_logo">
                <div class="relative w-full">
                  <input
                    type="file"
                    accept="image/*"
                    class="absolute inset-0 w-full h-full opacity-0 cursor-pointer z-10"
                    :aria-label="$t('admin_setting.lbl_company_logo')"
                    @change="onLogoChange"
                  />
                  <div class="w-full border border-slate-200 rounded-xl text-[12px] text-slate-400 bg-slate-50/50 px-3.5 py-2 outline-none flex items-center justify-between h-[38px] overflow-hidden">
                    <span class="truncate font-medium">
                      {{ form.company_logo ? $t('admin_setting.lbl_logo_selected') : $t('admin_setting.ph_choose_logo') }}
                    </span>
                    <div class="flex items-center gap-2">
                      <Upload :size="14" class="text-slate-400 shrink-0" />
                      <button
                        v-if="form.company_logo"
                        type="button"
                        class="p-1 rounded bg-indigo-50 text-indigo-600 hover:bg-indigo-100 transition-colors relative z-20"
                        :aria-label="$t('admin_setting.btn_preview_logo')"
                        @click.stop.prevent="previewOpen = true"
                      >
                        <Eye :size="12" />
                      </button>
                      <button
                        v-if="form.company_logo"
                        type="button"
                        class="p-1 rounded bg-rose-50 text-rose-500 hover:bg-rose-100 transition-colors relative z-20"
                        :aria-label="$t('admin_setting.btn_remove_logo')"
                        @click.stop.prevent="setField('company_logo', '')"
                      >
                        <X :size="12" />
                      </button>
                    </div>
                  </div>
                </div>
              </FormItem>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <FormSelect
                  :label="$t('admin_setting.lbl_time_zone')"
                  :options="timeZoneOptions"
                  :model-value="form.time_zone"
                  :allow-clear="false"
                  :error="errors.time_zone"
                  required
                  @update:model-value="setField('time_zone', $event)"
                />
                <FormSelect
                  :label="$t('admin_setting.lbl_default_locale')"
                  :options="localeOptions"
                  :model-value="form.default_locale"
                  :allow-clear="false"
                  :error="errors.default_locale"
                  required
                  @update:model-value="setField('default_locale', $event)"
                />
              </div>
            </div>
          </div>

          <!-- Nhóm 2: Vận hành & Tài chính -->
          <div class="bg-white border border-slate-200/80 rounded-[24px] p-6 shadow-sm flex flex-col gap-5">
            <div class="flex items-center gap-2.5 pb-2 border-b border-slate-100">
              <div class="p-2 rounded-xl bg-amber-50 text-amber-600 border border-amber-100"><Clock :size="16" class="stroke-[2.5]" /></div>
              <div>
                <h3 class="text-[13px] font-black text-slate-800 tracking-tight uppercase">{{ $t('admin_setting.title_operation_section') }}</h3>
                <p class="text-[10px] text-slate-400 font-medium">{{ $t('admin_setting.hint_operation_section') }}</p>
              </div>
            </div>
            <div class="space-y-4">
              <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <FormItem :label="$t('admin_setting.lbl_work_start')" for-id="setting-start" required :error="errors.work_start_time">
                  <BaseInput id="setting-start" type="time" :model-value="form.work_start_time" :error="Boolean(errors.work_start_time)" @update:model-value="setField('work_start_time', String($event ?? ''))" />
                </FormItem>
                <FormItem :label="$t('admin_setting.lbl_work_end')" for-id="setting-end" required :error="errors.work_end_time">
                  <BaseInput id="setting-end" type="time" :model-value="form.work_end_time" :error="Boolean(errors.work_end_time)" @update:model-value="setField('work_end_time', String($event ?? ''))" />
                </FormItem>
                <FormItem :label="$t('admin_setting.lbl_lunch_break')" for-id="setting-lunch" required :error="errors.lunch_break_minutes">
                  <BaseInput id="setting-lunch" type="number" min="0" max="240" :model-value="form.lunch_break_minutes" :error="Boolean(errors.lunch_break_minutes)" @update:model-value="setField('lunch_break_minutes', String($event ?? ''))" />
                </FormItem>
              </div>
              <FormSelect
                :label="$t('admin_setting.lbl_work_week')"
                :options="workWeekOptions"
                :model-value="form.work_week"
                :allow-clear="false"
                :error="errors.work_week"
                required
                @update:model-value="setField('work_week', $event)"
              />
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <FormSelect
                  :label="$t('admin_setting.lbl_currency')"
                  :options="currencyOptions"
                  :model-value="form.currency"
                  :allow-clear="false"
                  :error="errors.currency"
                  required
                  @update:model-value="setField('currency', $event)"
                />
                <FormSelect
                  :label="$t('admin_setting.lbl_fiscal_start')"
                  :options="monthOptions"
                  :model-value="form.fiscal_year_start_month"
                  :allow-clear="false"
                  :error="errors.fiscal_year_start_month"
                  required
                  @update:model-value="setField('fiscal_year_start_month', $event)"
                />
              </div>
            </div>
          </div>

          <!-- Nhóm 3: Thông báo & Báo cáo -->
          <div class="bg-white border border-slate-200/80 rounded-[24px] p-6 shadow-sm flex flex-col gap-5">
            <div class="flex items-center gap-2.5 pb-2 border-b border-slate-100">
              <div class="p-2 rounded-xl bg-emerald-50 text-emerald-600 border border-emerald-100"><Mail :size="16" class="stroke-[2.5]" /></div>
              <div>
                <h3 class="text-[13px] font-black text-slate-800 tracking-tight uppercase">{{ $t('admin_setting.title_notification_section') }}</h3>
                <p class="text-[10px] text-slate-400 font-medium">{{ $t('admin_setting.hint_notification_section') }}</p>
              </div>
            </div>
            <div class="space-y-4">
              <FormItem :label="$t('admin_setting.lbl_notification_email')" for-id="setting-email" required :error="errors.notification_email">
                <BaseInput
                  id="setting-email"
                  type="email"
                  :model-value="form.notification_email"
                  :placeholder="$t('admin_setting.ph_email')"
                  :error="Boolean(errors.notification_email)"
                  @update:model-value="setField('notification_email', String($event ?? ''))"
                />
              </FormItem>
              <div class="flex items-center justify-between p-4 bg-slate-50/60 border border-slate-100 rounded-2xl">
                <div class="flex flex-col gap-0.5 pr-4">
                  <span class="text-[13px] font-bold text-slate-700">{{ $t('admin_setting.lbl_monthly_report_ja') }}</span>
                  <span class="text-[11px] text-slate-400 font-medium">{{ $t('admin_setting.hint_monthly_report_ja') }}</span>
                </div>
                <SwitchToggle
                  :model-value="boolField('monthly_report_ja_enabled')"
                  :label="$t('admin_setting.lbl_monthly_report_ja')"
                  @update:model-value="setBool('monthly_report_ja_enabled', $event)"
                />
              </div>
            </div>
          </div>

          <!-- Nhóm 4: Tài khoản & Chấm công -->
          <div class="bg-white border border-slate-200/80 rounded-[24px] p-6 shadow-sm flex flex-col gap-5">
            <div class="flex items-center gap-2.5 pb-2 border-b border-slate-100">
              <div class="p-2 rounded-xl bg-purple-50 text-purple-600 border border-purple-100"><UserCog :size="16" class="stroke-[2.5]" /></div>
              <div>
                <h3 class="text-[13px] font-black text-slate-800 tracking-tight uppercase">{{ $t('admin_setting.title_account_section') }}</h3>
                <p class="text-[10px] text-slate-400 font-medium">{{ $t('admin_setting.hint_account_section') }}</p>
              </div>
            </div>
            <div class="space-y-4">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <FormItem :label="$t('admin_setting.lbl_password_min')" for-id="setting-password" required :error="errors.password_min_length">
                  <BaseInput id="setting-password" type="number" min="6" max="64" :model-value="form.password_min_length" :error="Boolean(errors.password_min_length)" @update:model-value="setField('password_min_length', String($event ?? ''))" />
                </FormItem>
                <FormItem :label="$t('admin_setting.lbl_invitation_hours')" for-id="setting-invite" required :error="errors.invitation_expiry_hours">
                  <BaseInput id="setting-invite" type="number" min="1" max="720" :model-value="form.invitation_expiry_hours" :error="Boolean(errors.invitation_expiry_hours)" @update:model-value="setField('invitation_expiry_hours', String($event ?? ''))" />
                </FormItem>
              </div>
              <div class="flex items-center justify-between p-4 bg-slate-50/60 border border-slate-100 rounded-2xl">
                <div class="flex flex-col gap-0.5 pr-4">
                  <span class="text-[13px] font-bold text-slate-700">{{ $t('admin_setting.lbl_allow_check_in') }}</span>
                  <span class="text-[11px] text-slate-400 font-medium">{{ $t('admin_setting.hint_allow_check_in') }}</span>
                </div>
                <SwitchToggle
                  :model-value="boolField('allow_app_check_in')"
                  :label="$t('admin_setting.lbl_allow_check_in')"
                  @update:model-value="setBool('allow_app_check_in', $event)"
                />
              </div>
            </div>
          </div>

        </form>

        <!-- Nhóm 5: Ngày lễ — nằm ngoài form vì lưu ngay từng dòng, không cần bấm Lưu cài đặt -->
        <HolidayCard v-if="!settingsQuery.isLoading.value" class="mt-6" />
      </div>
    </div>

    <div
      v-if="previewOpen && form.company_logo"
      class="fixed inset-0 z-[9999] flex items-center justify-center bg-black/60 backdrop-blur-sm animate-in fade-in duration-250 cursor-pointer"
      @click="previewOpen = false"
    >
      <div class="relative max-w-4xl max-h-[90vh] bg-white rounded-2xl shadow-2xl p-2.5 flex items-center justify-center m-4 animate-in zoom-in-95 duration-250 border border-slate-200" @click.stop>
        <button
          type="button"
          class="absolute -top-3.5 -right-3.5 w-8 h-8 rounded-full bg-white text-slate-700 shadow-lg hover:bg-slate-50 flex items-center justify-center font-bold text-lg border border-slate-200 transition-all hover:scale-105 active:scale-95"
          :aria-label="$t('common.btn_close')"
          @click="previewOpen = false"
        >
          <X :size="16" />
        </button>
        <img :src="form.company_logo" :alt="$t('admin_setting.lbl_logo_preview')" class="max-w-full max-h-[80vh] object-contain rounded-xl" />
      </div>
    </div>
  </PageLayout>
</template>
