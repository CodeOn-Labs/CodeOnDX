<script setup lang="ts">
// Port CategoryModal.tsx: 3 bước — phân loại, thẻ nhận diện (màu + thứ tự + xem trước), thuộc tính bổ sung (metadata).
// Thêm so với bản gốc: mã (code), tên tiếng Anh, mô tả, bật/tắt; loại "Mức hiệu suất" có ô Hệ số riêng (D12).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Calendar, Check, Gauge, Layers, Palette, Plus, Sparkles, Tag, Trash2, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import { CATEGORY_TYPES, type Category, type CategoryInput, type CategoryType } from '@/modules/admin/api/config.api';
import { RESERVED_META_KEYS, SUGGESTED_META, type MetaValueType } from './category-meta';
import { enumLabel } from '@/shared/i18n/enum-label';
import { PRESET_COLORS, badgeStyle, resolveColor } from '@/shared/utils/color';
import { cn } from '@/shared/utils/cn';

interface ExtraProp {
  uid: number;
  key: string;
  type: MetaValueType;
  value: string;
}

const props = defineProps<{
  item: Category | null;
  defaultType?: CategoryType;
  loading: boolean;
  /** Lỗi 400 của BE theo field */
  serverErrors: Record<string, string>;
}>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ submit: [input: CategoryInput] }>();

const { t } = useI18n();

const form = ref({
  type: 'project_level' as CategoryType,
  code: '',
  name: '',
  nameEn: '',
  description: '',
  color: 'slate',
  sortSeq: 1 as number | string,
  isActive: true,
  coefficient: '' as number | string,
});
const extras = ref<ExtraProp[]>([]);
const errors = ref<Record<string, string>>({});
let nextUid = 1;

watch(
  () => [open.value, props.item] as const,
  ([isOpen, item]) => {
    if (!isOpen) return;
    errors.value = {};
    if (item) {
      form.value = {
        type: item.type,
        code: item.code,
        name: item.name,
        nameEn: item.nameEn ?? '',
        description: item.description ?? '',
        color: item.color ?? 'slate',
        sortSeq: item.sortSeq,
        isActive: item.isActive,
        coefficient: typeof item.metadata.coefficient === 'number' ? item.metadata.coefficient : '',
      };
      extras.value = Object.entries(item.metadata)
        .filter(([k]) => !RESERVED_META_KEYS.includes(k))
        .map(([key, v]) => ({ uid: nextUid++, key, type: typeof v === 'number' ? 'number' : typeof v === 'boolean' ? 'boolean' : 'string', value: String(v) }));
    } else {
      form.value = { type: props.defaultType ?? 'project_level', code: '', name: '', nameEn: '', description: '', color: 'slate', sortSeq: 1, isActive: true, coefficient: '' };
      extras.value = [];
    }
  },
  { immediate: true },
);
watch(() => props.serverErrors, (value) => (errors.value = { ...value }));

const isEdit = computed(() => props.item !== null);
const isPerformance = computed(() => form.value.type === 'performance_level');
const typeOptions = computed(() => CATEGORY_TYPES.map((type) => ({ value: type, label: enumLabel('category_type', type) })));
const valueTypeOptions = computed(() => [
  { value: 'string', label: t('admin_category_list.opt_value_text') },
  { value: 'number', label: t('admin_category_list.opt_value_number') },
  { value: 'boolean', label: t('admin_category_list.opt_value_boolean') },
]);
const booleanOptions = computed(() => [
  { value: 'true', label: t('admin_category_list.opt_true') },
  { value: 'false', label: t('admin_category_list.opt_false') },
]);

/** Nhãn gợi ý thuộc tính — liệt kê tĩnh để không ghép key i18n động. */
const suggestionLabels = computed<Record<string, string>>(() => ({
  is_closed: t('admin_category_list.opt_meta_is_closed'),
  allow_timesheet: t('admin_category_list.opt_meta_allow_timesheet'),
  min_members: t('admin_category_list.opt_meta_min_members'),
  max_members: t('admin_category_list.opt_meta_max_members'),
  severity_score: t('admin_category_list.opt_meta_severity_score'),
  is_billable: t('admin_category_list.opt_meta_is_billable'),
  standard_hourly_rate: t('admin_category_list.opt_meta_standard_hourly_rate'),
  abbreviation: t('admin_category_list.opt_meta_abbreviation'),
  department: t('admin_category_list.opt_meta_department'),
  is_overtime: t('admin_category_list.opt_meta_is_overtime'),
  working: t('admin_category_list.opt_meta_working'),
  paid: t('admin_category_list.opt_meta_paid'),
  deduct_balance: t('admin_category_list.opt_meta_deduct_balance'),
  need_attachment: t('admin_category_list.opt_meta_need_attachment'),
  kind: t('admin_category_list.opt_meta_kind'),
  symbol: t('admin_category_list.opt_meta_symbol'),
}));
const suggestions = computed(() => SUGGESTED_META[form.value.type] ?? []);
const hasExtra = (key: string) => extras.value.some((p) => p.key.trim() === key);

function addExtra(key = '', type: MetaValueType = 'string') {
  if (key && hasExtra(key)) return;
  extras.value.push({ uid: nextUid++, key, type, value: type === 'boolean' ? 'true' : '' });
}
function removeExtra(uid: number) {
  extras.value = extras.value.filter((p) => p.uid !== uid);
}

const isCustomColor = computed(() => form.value.color.startsWith('#'));
const customHex = computed(() => (isCustomColor.value ? form.value.color : '#6366f1'));
function onHexInput(raw: string) {
  const value = raw.trim().replace('#', '');
  if (/^[0-9A-F]{0,6}$/i.test(value)) form.value.color = value ? `#${value}` : '';
}
const previewName = computed(() => form.value.name || t('admin_category_list.ph_preview_name'));

function validate(): boolean {
  const e: Record<string, string> = {};
  if (!form.value.name.trim()) e.name = t('validation.required');
  if (!form.value.code.trim()) e.code = t('validation.required');
  else if (!/^[A-Z0-9_]{1,30}$/.test(form.value.code.trim().toUpperCase())) e.code = t('admin_category_list.err_code_format');
  const sort = Number(form.value.sortSeq);
  if (!Number.isInteger(sort) || sort < 1 || sort > 999) e.sortSeq = t('admin_category_list.err_sort_range');
  if (isPerformance.value) {
    const c = Number(form.value.coefficient);
    if (form.value.coefficient === '' || !Number.isFinite(c) || c < 0 || c > 2) e.coefficient = t('admin_category_list.err_coefficient_range');
  }
  for (const p of extras.value) {
    if (!/^[a-z][a-z0-9_]{0,39}$/.test(p.key.trim())) e.metadata = t('admin_category_list.err_metadata_key');
    if (p.type === 'number' && !Number.isFinite(Number(p.value))) e.metadata = t('admin_category_list.err_metadata_number');
  }
  errors.value = e;
  return Object.keys(e).length === 0;
}

function submit() {
  if (!validate()) return;
  const metadata: Record<string, unknown> = {};
  for (const p of extras.value) {
    metadata[p.key.trim()] = p.type === 'number' ? Number(p.value) : p.type === 'boolean' ? p.value === 'true' : p.value;
  }
  if (isPerformance.value) metadata.coefficient = Number(form.value.coefficient);
  emit('submit', {
    type: form.value.type,
    code: form.value.code.trim().toUpperCase(),
    name: form.value.name.trim(),
    nameEn: form.value.nameEn.trim(),
    description: form.value.description.trim(),
    color: form.value.color,
    sortSeq: Number(form.value.sortSeq),
    metadata,
    isActive: form.value.isActive,
  });
}
</script>

<template>
  <BaseDialog
    v-model:open="open"
    :title="isEdit ? $t('admin_category_list.title_edit_dialog') : $t('admin_category_list.title_create_dialog')"
    max-width="3xl"
  >
    <form class="flex flex-col gap-6" @submit.prevent="submit">
      <!-- Bước 1: Phân loại -->
      <div class="space-y-4">
        <div class="flex items-center gap-2.5 px-1">
          <div class="p-2 rounded-xl bg-gradient-to-tr from-blue-500/10 to-indigo-500/10 text-indigo-600 shadow-sm border border-indigo-500/10">
            <Tag :size="16" class="stroke-[2.5]" />
          </div>
          <div>
            <h3 class="text-[14px] font-black text-slate-800 tracking-tight">{{ $t('admin_category_list.title_step_classify') }}</h3>
            <p class="text-[10.5px] text-slate-400 font-medium">{{ $t('admin_category_list.hint_step_classify') }}</p>
          </div>
        </div>
        <div class="bg-gradient-to-b from-white to-slate-50/20 p-5 rounded-[20px] border border-slate-200/60 shadow-sm relative overflow-hidden backdrop-blur-sm">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
            <FormItem :label="$t('admin_category_list.lbl_name')" for-id="category-name" required :error="errors.name">
              <BaseInput id="category-name" v-model="form.name" :placeholder="$t('admin_category_list.ph_name')" :error="Boolean(errors.name)" />
            </FormItem>
            <FormSelect
              :model-value="form.type"
              :label="$t('admin_category_list.lbl_type')"
              :options="typeOptions"
              :placeholder="$t('admin_category_list.ph_select_type')"
              :error="errors.type"
              :disabled="isEdit"
              :allow-clear="false"
              required
              searchable
              @update:model-value="form.type = ($event as CategoryType) ?? form.type"
            />
            <FormItem :label="$t('admin_category_list.lbl_name_en')" for-id="category-name-en" :error="errors.nameEn">
              <BaseInput id="category-name-en" v-model="form.nameEn" :placeholder="$t('admin_category_list.ph_name_en')" />
            </FormItem>
            <FormItem :label="$t('admin_category_list.lbl_code')" for-id="category-code" required :error="errors.code" :description="$t('admin_category_list.hint_code')">
              <BaseInput
                id="category-code"
                v-model="form.code"
                class="font-mono uppercase"
                :placeholder="$t('admin_category_list.ph_code')"
                :error="Boolean(errors.code)"
              />
            </FormItem>
            <FormItem class="md:col-span-2" :label="$t('admin_category_list.lbl_description')" for-id="category-desc" :error="errors.description">
              <BaseTextarea id="category-desc" v-model="form.description" :rows="2" :placeholder="$t('admin_category_list.ph_description')" />
            </FormItem>
          </div>
          <div class="mt-5 pt-4 border-t border-slate-200/60 flex items-center justify-between">
            <div class="flex flex-col gap-0.5">
              <span class="text-[13px] font-bold text-slate-700">{{ $t('admin_category_list.lbl_is_active') }}</span>
              <span class="text-[11px] text-slate-400 font-medium">{{ $t('admin_category_list.hint_is_active') }}</span>
            </div>
            <SwitchToggle v-model="form.isActive" :label="$t('admin_category_list.lbl_is_active')" />
          </div>
        </div>
      </div>

      <!-- Mức hiệu suất: hệ số dùng trong công thức Actual1 của lưới Công số -->
      <div v-if="isPerformance" class="space-y-4">
        <div class="flex items-center gap-2.5 px-1">
          <div class="p-2 rounded-xl bg-gradient-to-tr from-teal-500/10 to-emerald-500/10 text-teal-600 shadow-sm border border-teal-500/10">
            <Gauge :size="16" class="stroke-[2.5]" />
          </div>
          <div>
            <h3 class="text-[14px] font-black text-slate-800 tracking-tight">{{ $t('admin_category_list.title_step_coefficient') }}</h3>
            <p class="text-[10.5px] text-slate-400 font-medium">{{ $t('admin_category_list.hint_step_coefficient') }}</p>
          </div>
        </div>
        <div class="bg-gradient-to-b from-white to-slate-50/20 p-5 rounded-[20px] border border-slate-200/60 shadow-sm grid grid-cols-1 md:grid-cols-2 gap-5 items-start">
          <FormItem :label="$t('admin_category_list.lbl_coefficient')" for-id="category-coefficient" required :error="errors.coefficient">
            <BaseInput id="category-coefficient" v-model="form.coefficient" type="number" step="0.05" min="0" max="2" :error="Boolean(errors.coefficient)" />
          </FormItem>
          <p class="text-[11.5px] leading-relaxed text-slate-500 font-medium bg-teal-50/60 border border-teal-100 rounded-xl px-3 py-2.5">
            {{ $t('admin_category_list.hint_coefficient_formula') }}
          </p>
        </div>
      </div>

      <!-- Bước 2: Thẻ nhận diện -->
      <div class="space-y-4">
        <div class="flex items-center gap-2.5 px-1">
          <div class="p-2 rounded-xl bg-gradient-to-tr from-amber-500/10 to-orange-500/10 text-amber-600 shadow-sm border border-amber-500/10">
            <Palette :size="16" class="stroke-[2.5]" />
          </div>
          <div>
            <h3 class="text-[14px] font-black text-slate-800 tracking-tight">{{ $t('admin_category_list.title_step_design') }}</h3>
            <p class="text-[10.5px] text-slate-400 font-medium">{{ $t('admin_category_list.hint_step_design') }}</p>
          </div>
        </div>
        <div class="bg-gradient-to-b from-white to-slate-50/20 p-5 rounded-[20px] border border-slate-200/60 shadow-sm relative overflow-hidden backdrop-blur-sm">
          <div class="grid grid-cols-1 md:grid-cols-12 gap-6">
            <div class="md:col-span-5 flex flex-col justify-between bg-white p-5 rounded-2xl border border-slate-200/60 shadow-sm">
              <div class="space-y-4">
                <div class="flex items-center gap-1.5 text-[11px] font-black uppercase tracking-wider text-slate-400">
                  <Sparkles :size="13" class="text-amber-500 animate-pulse" />
                  {{ $t('admin_category_list.lbl_preview') }}
                </div>
                <div class="border border-slate-200/60 rounded-2xl bg-gradient-to-br from-slate-50 to-slate-100/50 p-4 shadow-inner relative overflow-hidden">
                  <div class="relative bg-white rounded-xl border border-slate-200 p-3.5 shadow-[0_8px_20px_-6px_rgba(0,0,0,0.05)] hover:shadow-[0_12px_24px_-8px_rgba(0,0,0,0.08)] transition-all duration-300 transform hover:-translate-y-0.5">
                    <div class="flex items-start justify-between gap-3">
                      <div class="space-y-1.5 min-w-0 flex-1">
                        <div class="flex items-center gap-1.5">
                          <span class="w-1.5 h-1.5 rounded-full bg-indigo-500 shrink-0" />
                          <span class="text-[10px] font-extrabold text-slate-400 tracking-wider uppercase font-mono">{{ form.code || $t('admin_category_list.ph_preview_code') }}</span>
                        </div>
                        <p class="text-[11.5px] font-black text-slate-700 leading-snug truncate">{{ previewName }}</p>
                      </div>
                      <span
                        class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-[11px] font-semibold capitalize tracking-wide transition-all duration-200 border border-transparent shadow-sm shrink-0"
                        :style="badgeStyle(form.color)"
                      >
                        <span class="w-1.5 h-1.5 rounded-full shrink-0" :style="{ backgroundColor: resolveColor(form.color) }" />
                        <span class="leading-none">{{ previewName }}</span>
                      </span>
                    </div>
                    <div class="flex items-center justify-between border-t border-slate-100 mt-3 pt-2.5">
                      <div class="flex items-center gap-1.5 text-[10px] text-slate-400 font-extrabold font-mono">
                        <Calendar :size="11" class="text-slate-300" />
                        <span>{{ $t('admin_category_list.lbl_preview_date') }}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
              <p class="text-[10.5px] text-slate-400 font-medium leading-relaxed mt-5">{{ $t('admin_category_list.hint_color_usage') }}</p>
            </div>

            <div class="md:col-span-7 space-y-4">
              <div class="flex items-center justify-between">
                <span class="text-[11px] font-black uppercase tracking-widest text-slate-500 block">{{ $t('admin_category_list.lbl_color_collection') }}</span>
                <span v-if="form.color" class="text-[9.5px] font-mono font-black text-slate-500 uppercase bg-slate-100 px-2 py-0.5 rounded-full border border-slate-200 shadow-sm">
                  {{ form.color }}
                </span>
              </div>
              <div class="grid grid-cols-2 sm:grid-cols-3 gap-2">
                <button
                  v-for="preset in PRESET_COLORS"
                  :key="preset"
                  type="button"
                  :class="cn(
                    'group flex items-center justify-between py-2.5 px-3 rounded-xl border text-[11px] font-semibold transition-all duration-200 hover:scale-[1.02] active:scale-95 cursor-pointer shadow-sm relative',
                    form.color === preset
                      ? 'border-indigo-600 bg-indigo-50/10 ring-2 ring-indigo-500/10 text-indigo-900 font-extrabold'
                      : 'border-slate-200 bg-white hover:bg-slate-50/50 text-slate-600',
                  )"
                  :style="form.color === preset ? { boxShadow: `0 4px 14px -3px ${resolveColor(preset)}25` } : undefined"
                  @click="form.color = preset"
                >
                  <span class="flex items-center gap-2">
                    <span class="h-3 w-3 rounded-full border border-black/5 flex-shrink-0" :style="{ backgroundColor: resolveColor(preset) }" />
                    <span class="text-[11px] font-semibold text-slate-700 tracking-tight group-hover:text-slate-900">{{ enumLabel('color_preset', preset) }}</span>
                  </span>
                  <span v-if="form.color === preset" class="h-4.5 w-4.5 bg-indigo-600 rounded-full flex items-center justify-center text-white shadow-sm shadow-indigo-600/20">
                    <Check :size="10" :stroke-width="3.5" />
                  </span>
                </button>
              </div>

              <div class="border-t border-slate-200/60 pt-4 space-y-3">
                <span class="text-[11px] font-black uppercase tracking-widest text-slate-500 block">{{ $t('admin_category_list.lbl_custom_color') }}</span>
                <div class="flex items-center gap-3">
                  <div class="relative">
                    <span
                      :class="cn(
                        'group h-9 px-3 rounded-xl border text-[11px] font-black transition-all duration-200 cursor-pointer shadow-sm relative flex items-center gap-2.5',
                        isCustomColor ? 'border-slate-800 bg-slate-50 ring-2 ring-slate-800/5 text-slate-800' : 'border-dashed border-slate-300 bg-white text-slate-400',
                      )"
                    >
                      <span class="h-4.5 w-4.5 rounded-full border border-black/10 shadow-sm shrink-0" :style="{ backgroundColor: customHex }" />
                      <span class="text-[11px] font-extrabold text-slate-600 tracking-tight">{{ $t('admin_category_list.btn_pick_color') }}</span>
                    </span>
                    <input
                      type="color"
                      :value="customHex"
                      class="absolute inset-0 opacity-0 cursor-pointer h-full w-full"
                      :aria-label="$t('admin_category_list.btn_pick_color')"
                      @input="form.color = ($event.target as HTMLInputElement).value"
                    />
                  </div>
                  <div class="relative flex items-center flex-1 max-w-[150px]">
                    <span class="absolute left-3.5 text-[11px] font-black text-slate-400 font-mono">#</span>
                    <input
                      type="text"
                      maxlength="6"
                      :value="isCustomColor ? form.color.replace('#', '').toUpperCase() : ''"
                      :placeholder="$t('admin_category_list.ph_hex')"
                      :aria-label="$t('admin_category_list.lbl_custom_color')"
                      class="w-full pl-7 pr-3 py-2 text-[11px] font-mono font-black rounded-xl border border-slate-200 bg-white text-slate-700 placeholder-slate-300 focus:outline-none focus:border-slate-400 focus:ring-4 focus:ring-slate-100 transition-all shadow-sm uppercase h-9"
                      @input="onHexInput(($event.target as HTMLInputElement).value)"
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div class="mt-5 pt-4 border-t border-slate-200/60">
            <FormItem :label="$t('admin_category_list.lbl_sort')" for-id="category-sort" required :error="errors.sortSeq">
              <BaseInput id="category-sort" v-model="form.sortSeq" type="number" min="1" max="999" :error="Boolean(errors.sortSeq)" />
            </FormItem>
          </div>
        </div>
      </div>

      <!-- Bước 3: Thuộc tính bổ sung -->
      <div class="space-y-4">
        <div class="flex flex-col gap-2 px-1">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2.5">
              <div class="p-2 rounded-xl bg-gradient-to-tr from-emerald-500/10 to-teal-500/10 text-emerald-600 shadow-sm border border-emerald-500/10">
                <Layers :size="16" class="stroke-[2.5]" />
              </div>
              <div>
                <h3 class="text-[14px] font-black text-slate-800 tracking-tight">{{ $t('admin_category_list.title_step_metadata') }}</h3>
                <p class="text-[10.5px] text-slate-400 font-medium">{{ $t('admin_category_list.hint_step_metadata') }}</p>
              </div>
            </div>
            <Button variant="outline" class="h-8 px-3 text-[11px] font-extrabold rounded-xl border-slate-200 hover:bg-slate-50 transition-all shadow-sm" @click="addExtra()">
              <template #leftIcon><Plus :size="12" :stroke-width="3" /></template>
              {{ $t('admin_category_list.btn_add_property') }}
            </Button>
          </div>

          <div v-if="suggestions.length > 0" class="flex flex-wrap items-center gap-2 bg-slate-50 border border-slate-200/50 p-3 rounded-2xl mt-1.5">
            <div class="flex items-center gap-1.5 text-[10px] font-black text-slate-400 uppercase tracking-wider mr-1">
              <Sparkles :size="12" class="text-amber-500 animate-pulse" />
              {{ $t('admin_category_list.lbl_quick_hint') }}
            </div>
            <button
              v-for="s in suggestions"
              :key="s.key"
              type="button"
              :disabled="hasExtra(s.key)"
              :class="cn(
                'h-7 px-3 rounded-xl text-[10.5px] font-bold inline-flex items-center gap-1.5 transition-all duration-200',
                hasExtra(s.key)
                  ? 'bg-slate-100 text-slate-400 cursor-not-allowed border border-transparent'
                  : 'bg-emerald-50 text-emerald-800 border border-emerald-100/50 hover:bg-emerald-100/60 hover:border-emerald-200 active:scale-95 hover:shadow-sm',
              )"
              @click="addExtra(s.key, s.type)"
            >
              <Plus :size="10" :stroke-width="3" :class="cn('text-emerald-600', hasExtra(s.key) && 'opacity-40')" />
              <span>{{ suggestionLabels[s.key] ?? s.key }}</span>
              <span class="opacity-50 text-[9px] font-mono">({{ s.key }})</span>
            </button>
          </div>
        </div>

        <div class="bg-gradient-to-b from-white to-slate-50/20 p-5 rounded-[20px] border border-slate-200/60 shadow-sm relative overflow-hidden backdrop-blur-sm">
          <div v-if="extras.length === 0" class="text-center py-6 text-slate-400 text-[12px] font-medium leading-relaxed">
            {{ $t('admin_category_list.empty_properties') }}
            <br />
            <span class="text-[11px] text-slate-400 opacity-80">{{ $t('admin_category_list.hint_empty_properties') }}</span>
          </div>
          <div v-else class="flex flex-col gap-3">
            <div class="hidden sm:grid grid-cols-[1fr_120px_1fr_40px] gap-3 px-3 mb-1 text-[10px] font-black uppercase tracking-widest text-slate-400">
              <div>{{ $t('admin_category_list.col_property_key') }}</div>
              <div>{{ $t('admin_category_list.col_property_type') }}</div>
              <div>{{ $t('admin_category_list.col_property_value') }}</div>
              <div />
            </div>
            <div
              v-for="prop in extras"
              :key="prop.uid"
              class="flex flex-col sm:grid sm:grid-cols-[1fr_120px_1fr_40px] items-center gap-3 bg-white p-3.5 rounded-2xl border border-slate-200 shadow-sm relative group"
            >
              <BaseInput v-model="prop.key" class="h-9 text-[12px] font-mono" :placeholder="$t('admin_category_list.ph_property_key')" :aria-label="$t('admin_category_list.col_property_key')" />
              <FormSelect
                :model-value="prop.type"
                :options="valueTypeOptions"
                :allow-clear="false"
                trigger-class="h-9 text-[12px] font-semibold"
                hide-message
                @update:model-value="prop.type = ($event as MetaValueType) ?? 'string'; prop.value = prop.type === 'boolean' ? 'true' : ''"
              />
              <FormSelect
                v-if="prop.type === 'boolean'"
                :model-value="prop.value"
                :options="booleanOptions"
                :allow-clear="false"
                trigger-class="h-9 text-[12px] font-semibold"
                hide-message
                @update:model-value="prop.value = String($event ?? 'true')"
              />
              <BaseInput
                v-else
                v-model="prop.value"
                :type="prop.type === 'number' ? 'number' : 'text'"
                class="h-9 text-[12px]"
                :placeholder="$t('admin_category_list.ph_property_value')"
                :aria-label="$t('admin_category_list.col_property_value')"
              />
              <div class="flex justify-end w-full sm:w-auto">
                <button
                  type="button"
                  class="h-9 w-9 flex items-center justify-center text-slate-400 hover:text-red-500 hover:bg-red-50 rounded-xl transition-all hover:scale-110 active:scale-95"
                  :title="$t('admin_category_list.btn_delete_property')"
                  :aria-label="$t('admin_category_list.btn_delete_property')"
                  @click="removeExtra(prop.uid)"
                >
                  <Trash2 :size="16" />
                </button>
              </div>
            </div>
            <p v-if="errors.metadata" class="text-[10px] font-bold text-red-500 px-1">{{ errors.metadata }}</p>
          </div>
        </div>
      </div>
    </form>

    <template #footer>
      <div class="flex justify-end gap-3 w-full px-1">
        <Button variant="danger" @click="open = false">
          <template #leftIcon><X :size="16" /></template>
          {{ $t('admin_category_list.btn_cancel') }}
        </Button>
        <Button variant="primary" :loading="props.loading" :disabled="props.loading" class="min-w-[140px] px-6 shadow-lg shadow-pr/25" @click="submit">
          <template #leftIcon><Check :size="16" :stroke-width="3" /></template>
          {{ isEdit ? $t('admin_category_list.btn_save') : $t('admin_category_list.btn_create_submit') }}
        </Button>
      </div>
    </template>
  </BaseDialog>
</template>
