<script setup lang="ts">
// Port RuleModal.tsx: thông tin quy tắc, công tắc áp dụng, ô giá trị (số trong khoảng min–max hoặc bật/tắt), đặt lại mặc định.
// FE kiểm tra nhanh khoảng giá trị; BE kiểm tra lại và trả lỗi 400 theo field `value`.
import { computed, ref, watch, type Component } from 'vue';
import { useI18n } from 'vue-i18n';
import { Briefcase, CalendarOff, Check, Clock, Layout, RotateCcw, Timer, UserCheck, Users, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import type { SystemRule } from '@/api/types';
import { enumLabel } from '@/shared/i18n/enum-label';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ rule: SystemRule | null; loading: boolean }>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ submit: [value: { value: number | boolean; status: number }] }>();

const { t } = useI18n();

const MODULE_ICON: Record<string, { icon: Component; color: string }> = {
  timesheet: { icon: Clock, color: 'text-blue-500' },
  project: { icon: Briefcase, color: 'text-emerald-500' },
  hr: { icon: Users, color: 'text-purple-500' },
  leave: { icon: CalendarOff, color: 'text-amber-500' },
  overtime: { icon: Timer, color: 'text-rose-500' },
  attendance: { icon: UserCheck, color: 'text-cyan-500' },
};

const numberValue = ref<string | number | null>('');
const boolValue = ref(false);
const enabled = ref(true);
const error = ref('');

watch(
  () => [open.value, props.rule] as const,
  ([isOpen, rule]) => {
    if (!isOpen || !rule) return;
    const current = rule.value ?? rule.defaultValue;
    numberValue.value = typeof current === 'number' ? current : '';
    boolValue.value = Boolean(current);
    enabled.value = rule.status !== 0;
    error.value = '';
  },
  { immediate: true },
);

const isBoolean = computed(() => props.rule?.valueType === 'boolean');
const currentValue = computed<number | boolean | null>(() => {
  if (isBoolean.value) return boolValue.value;
  const n = Number(numberValue.value);
  return numberValue.value === '' || numberValue.value === null || !Number.isFinite(n) ? null : n;
});
const isDirty = computed(() => {
  const rule = props.rule;
  if (!rule) return false;
  return currentValue.value !== (rule.value ?? rule.defaultValue) || enabled.value !== (rule.status !== 0);
});
const differsFromDefault = computed(() => props.rule !== null && currentValue.value !== props.rule.defaultValue);
const moduleIcon = computed(() => MODULE_ICON[props.rule?.module ?? ''] ?? { icon: Layout, color: 'text-slate-400' });

function restoreDefault() {
  if (!props.rule) return;
  if (isBoolean.value) boolValue.value = Boolean(props.rule.defaultValue);
  else numberValue.value = Number(props.rule.defaultValue);
  enabled.value = true;
  error.value = '';
}

function validate(): boolean {
  const rule = props.rule;
  if (!rule || isBoolean.value) return true;
  const value = currentValue.value;
  if (value === null) error.value = t('validation.required');
  else if (rule.min !== null && (value as number) < rule.min) error.value = t('admin_rule_list.err_min_value', { min: rule.min });
  else if (rule.max !== null && (value as number) > rule.max) error.value = t('admin_rule_list.err_max_value', { max: rule.max });
  else error.value = '';
  return !error.value;
}

function submit() {
  if (!validate() || currentValue.value === null) return;
  emit('submit', { value: currentValue.value, status: isBoolean.value || enabled.value ? 1 : 0 });
}

/** Lỗi 400 từ BE (field `value`) do trang cha chuyển xuống qua toast; ở đây chỉ cần xoá lỗi khi người dùng sửa. */
watch(numberValue, () => (error.value = ''));
</script>

<template>
  <BaseDialog v-model:open="open" :title="$t('admin_rule_list.title_rule_dialog')" max-width="lg">
    <div v-if="props.rule" class="relative py-2">
      <div v-if="props.loading" class="absolute inset-0 bg-white/60 backdrop-blur-[1px] flex items-center justify-center z-50 rounded-xl">
        <div class="flex flex-col items-center gap-3">
          <span class="h-6 w-6 animate-spin rounded-full border-2 border-indigo-600 border-t-transparent" />
          <span class="text-[11px] font-bold text-slate-500">{{ $t('admin_rule_list.msg_saving') }}</span>
        </div>
      </div>

      <form class="space-y-5" @submit.prevent="submit">
        <div class="grid grid-cols-1 gap-y-4 p-4 bg-slate-50/80 border border-slate-100 rounded-2xl">
          <div class="flex flex-col gap-1 min-w-0">
            <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">{{ $t('admin_rule_list.lbl_config_name') }}</span>
            <span class="text-[13px] font-medium text-slate-700">{{ props.rule.name }}</span>
          </div>
          <div class="flex flex-col gap-1 min-w-0">
            <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">{{ $t('admin_rule_list.col_key') }}</span>
            <span class="text-[12px] font-bold text-indigo-600 bg-indigo-50/80 border border-indigo-100 px-2 py-0.5 rounded-lg w-fit font-mono whitespace-nowrap overflow-x-auto max-w-full scrollbar-none">
              {{ props.rule.key }}
            </span>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">{{ $t('admin_rule_list.col_module') }}</span>
            <div class="flex items-center gap-2 mt-1">
              <component :is="moduleIcon.icon" :size="14" :class="cn('stroke-[2.2]', moduleIcon.color)" />
              <span class="text-[13.5px] font-bold text-slate-800">{{ enumLabel('rule_module', props.rule.module) }}</span>
            </div>
          </div>
          <div class="flex flex-col gap-1.5 border-t border-slate-200/50 pt-3 mt-1">
            <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">{{ $t('admin_rule_list.lbl_description') }}</span>
            <p class="text-[13px] text-slate-500 font-medium leading-relaxed">{{ props.rule.description }}</p>
          </div>
        </div>

        <div
          v-if="!isBoolean"
          class="flex items-center justify-between p-4 bg-white border border-slate-200 rounded-2xl shadow-[0_2px_8px_-4px_rgba(0,0,0,0.05)] hover:border-indigo-200 transition-all duration-300 group mb-1"
        >
          <div class="flex flex-col gap-1">
            <span class="text-[14px] font-bold text-slate-700 group-hover:text-slate-900 transition-colors">{{ $t('admin_rule_list.lbl_apply_rule') }}</span>
            <span class="text-[12px] text-slate-500 font-medium">
              {{ enabled ? $t('admin_rule_list.hint_rule_active') : $t('admin_rule_list.hint_rule_inactive') }}
            </span>
          </div>
          <SwitchToggle v-model="enabled" :label="$t('admin_rule_list.lbl_apply_rule')" />
        </div>

        <div :class="cn('space-y-4 pt-1 transition-all duration-300', !enabled && !isBoolean && 'opacity-40 pointer-events-none select-none')">
          <div v-if="!isBoolean" class="space-y-2 p-1">
            <FormItem :label="$t('admin_rule_list.col_value')" for-id="rule-value" required :error="error">
              <BaseInput id="rule-value" v-model="numberValue" type="number" step="any" :error="Boolean(error)" />
            </FormItem>
            <p class="text-[11px] text-slate-500 font-medium px-1 flex items-center gap-1.5 mt-1">
              <span class="w-1.5 h-1.5 rounded-full bg-amber-400" />
              {{ $t('admin_rule_list.lbl_limit_range') }}:
              <span class="font-bold text-slate-700">{{ props.rule.min ?? '–' }} - {{ props.rule.max ?? '–' }}</span>
              <span class="text-slate-400 ml-1">({{ $t('admin_rule_list.lbl_default') }}: {{ String(props.rule.defaultValue) }})</span>
            </p>
          </div>

          <div
            v-else
            class="flex items-center justify-between p-4 bg-white border border-slate-200 rounded-2xl shadow-[0_2px_8px_-4px_rgba(0,0,0,0.05)] hover:border-indigo-200 hover:shadow-[0_4px_12px_-4px_rgba(99,102,241,0.1)] transition-all duration-300 group"
          >
            <div class="flex flex-col gap-1">
              <span class="text-[14px] font-bold text-slate-700 group-hover:text-slate-900 transition-colors">{{ $t('admin_rule_list.lbl_enable_config') }}</span>
              <span class="text-[12px] text-slate-500 font-medium">
                ({{ $t('admin_rule_list.lbl_default') }}: {{ props.rule.defaultValue ? $t('admin_rule_list.lbl_on') : $t('admin_rule_list.lbl_off') }})
              </span>
            </div>
            <SwitchToggle v-model="boolValue" :label="$t('admin_rule_list.lbl_enable_config')" />
          </div>
        </div>
      </form>
    </div>

    <template #footer>
      <div class="flex justify-between items-center w-full px-1">
        <div>
          <Button
            v-if="differsFromDefault"
            variant="outline"
            class="h-9 rounded-xl font-bold text-amber-600 border-amber-200 hover:bg-amber-50 hover:border-amber-300 hover:shadow-sm cursor-pointer transition-all duration-200 active:scale-95 text-[12px] px-3"
            @click="restoreDefault"
          >
            <template #leftIcon><RotateCcw :size="14" /></template>
            {{ $t('admin_rule_list.btn_restore_default') }}
          </Button>
        </div>
        <div class="flex gap-2">
          <Button
            variant="danger"
            class="min-w-[80px] h-9 rounded-xl font-bold border-0 text-white shadow-[0_2px_8px_rgba(251,146,60,0.2)] hover:shadow-[0_4px_12px_rgba(251,146,60,0.35)] cursor-pointer transition-all duration-200 active:scale-95 text-[12px] px-3"
            @click="open = false"
          >
            <template #leftIcon><X :size="14" /></template>
            {{ $t('admin_rule_list.btn_cancel') }}
          </Button>
          <Button
            variant="primary"
            :loading="props.loading"
            :disabled="props.loading || !isDirty"
            class="min-w-[110px] h-9 rounded-xl font-bold border-0 text-white transition-all duration-300 text-[12px] px-4 bg-gradient-to-r from-indigo-600 to-violet-600 shadow-[0_4px_12px_-4px_rgba(99,102,241,0.5)] hover:shadow-[0_6px_16px_-4px_rgba(99,102,241,0.6)] hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:shadow-none disabled:cursor-not-allowed disabled:hover:scale-100"
            @click="submit"
          >
            <template #leftIcon><Check :size="14" :stroke-width="3" /></template>
            {{ $t('admin_rule_list.btn_save') }}
          </Button>
        </div>
      </div>
    </template>
  </BaseDialog>
</template>
