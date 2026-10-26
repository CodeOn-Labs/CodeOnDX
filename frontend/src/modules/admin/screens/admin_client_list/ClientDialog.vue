<script setup lang="ts">
// Port ClientDialog.tsx: 3 nhóm — thông tin định danh, liên hệ, cấu hình & trạng thái.
// Khác bản gốc: thêm Mã khách hàng; bỏ nút "Thêm quốc gia/ngành nghề mới" ngay trong ô chọn (sửa ở Quản trị → Danh mục).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Check, Info, ShieldCheck, User, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import type { Client, ClientInput } from '@/modules/org/api/org.api';
import { useMasterData } from '@/shared/composables/useMasterData';

const props = defineProps<{ client: Client | null; loading: boolean; serverErrors: Record<string, string> }>();
const open = defineModel<boolean>('open', { required: true });
const emit = defineEmits<{ submit: [input: ClientInput] }>();
const { t } = useI18n();

const { items: countries } = useMasterData('country');
const { items: industries } = useMasterData('industry');
const { items: currencies } = useMasterData('currency');
const toOptions = (rows: { id: string; name: string }[]) => rows.map((r) => ({ label: r.name, value: r.id }));
const currencyOptions = computed(() => currencies.value.map((c) => ({ label: c.name, value: c.code })));

const empty = (): ClientInput => ({ name: '', code: '', countryId: '', industryId: '', contactName: '', contactEmail: '', defaultCurrency: '', isActive: true });
const form = ref<ClientInput>(empty());
const errors = ref<Record<string, string>>({});

watch(open, (isOpen) => {
  if (!isOpen) return;
  errors.value = {};
  const c = props.client;
  form.value = c
    ? { name: c.name, code: c.code, countryId: c.country?.id ?? '', industryId: c.industry?.id ?? '', contactName: c.contactName ?? '', contactEmail: c.contactEmail ?? '', defaultCurrency: c.defaultCurrency ?? '', isActive: c.isActive }
    : empty();
});
watch(() => props.serverErrors, (v) => (errors.value = { ...v }));

function submit() {
  const e: Record<string, string> = {};
  if (!form.value.name.trim()) e.name = t('validation.required');
  else if (form.value.name.length > 200) e.name = t('admin_client_list.err_name_too_long');
  if (form.value.contactEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.value.contactEmail)) e.contactEmail = t('validation.invalid_email');
  errors.value = e;
  if (Object.keys(e).length === 0) emit('submit', { ...form.value });
}
const SECTION = 'bg-slate-50/50 p-5 rounded-[24px] border border-slate-200/60';
</script>

<template>
  <BaseDialog v-model:open="open" :title="props.client ? $t('admin_client_list.title_edit_dialog') : $t('admin_client_list.title_create_dialog')" max-width="lg">
    <form class="flex flex-col gap-6" @submit.prevent="submit">
      <div class="space-y-4">
        <div class="flex items-center gap-2 px-1">
          <div class="p-1.5 rounded-lg bg-blue-50 text-blue-600"><Info :size="16" /></div>
          <h3 class="text-[14px] font-bold text-slate-900 uppercase tracking-wider">{{ $t('admin_client_list.lbl_section_identity') }}</h3>
        </div>
        <div :class="[SECTION, 'space-y-5']">
          <div class="grid grid-cols-3 gap-5">
            <FormItem class="col-span-2" :label="$t('admin_client_list.lbl_name')" required :error="errors.name">
              <BaseInput v-model="form.name" :placeholder="$t('admin_client_list.ph_name')" :error="!!errors.name" />
            </FormItem>
            <FormItem :label="$t('admin_client_list.lbl_code')" :error="errors.code" :description="$t('admin_client_list.hint_code')">
              <BaseInput v-model="form.code" :error="!!errors.code" class="uppercase" />
            </FormItem>
          </div>
          <div class="grid grid-cols-2 gap-5">
            <FormSelect v-model="form.countryId" :label="$t('admin_client_list.lbl_country')" :placeholder="$t('admin_client_list.ph_select_country')" :options="toOptions(countries)" :error="errors.countryId" searchable />
            <FormSelect v-model="form.industryId" :label="$t('admin_client_list.lbl_industry')" :placeholder="$t('admin_client_list.ph_select_industry')" :options="toOptions(industries)" :error="errors.industryId" searchable />
          </div>
        </div>
      </div>

      <div class="space-y-4">
        <div class="flex items-center gap-2 px-1">
          <div class="p-1.5 rounded-lg bg-purple-50 text-purple-600"><User :size="16" /></div>
          <h3 class="text-[14px] font-bold text-slate-900 uppercase tracking-wider">{{ $t('admin_client_list.lbl_section_contact') }}</h3>
        </div>
        <div :class="[SECTION, 'grid grid-cols-2 gap-5']">
          <FormItem :label="$t('admin_client_list.lbl_contact_name')" :error="errors.contactName">
            <BaseInput v-model="form.contactName" :placeholder="$t('admin_client_list.ph_contact_name')" :error="!!errors.contactName" />
          </FormItem>
          <FormItem :label="$t('admin_client_list.lbl_contact_email')" :error="errors.contactEmail">
            <BaseInput v-model="form.contactEmail" type="email" :placeholder="$t('admin_client_list.ph_contact_email')" :error="!!errors.contactEmail" />
          </FormItem>
        </div>
      </div>

      <div class="space-y-4">
        <div class="flex items-center gap-2 px-1">
          <div class="p-1.5 rounded-lg bg-emerald-50 text-emerald-600"><ShieldCheck :size="16" /></div>
          <h3 class="text-[14px] font-bold text-slate-900 uppercase tracking-wider">{{ $t('admin_client_list.lbl_section_config') }}</h3>
        </div>
        <div :class="[SECTION, 'flex items-center justify-between gap-8']">
          <div class="flex-1">
            <FormSelect v-model="form.defaultCurrency" :label="$t('admin_client_list.lbl_currency')" :placeholder="$t('admin_client_list.ph_select_currency')" :options="currencyOptions" :error="errors.defaultCurrency" />
          </div>
          <div class="flex flex-col gap-2 p-4 bg-white/80 rounded-xl border border-slate-100 shadow-sm w-[220px]">
            <div class="flex items-center justify-between gap-2">
              <span class="text-[12px] font-bold text-slate-600">{{ $t('admin_client_list.lbl_enable') }}</span>
              <SwitchToggle v-model="form.isActive" size="sm" :label="$t('admin_client_list.lbl_enable')" />
            </div>
            <p class="text-[10px] text-slate-400 leading-tight">{{ $t('admin_client_list.hint_enable') }}</p>
          </div>
        </div>
      </div>
      <button type="submit" class="hidden" />
    </form>

    <template #footer>
      <Button variant="danger" class="min-w-[100px]" :disabled="props.loading" @click="open = false">
        <template #leftIcon><X :size="16" /></template>
        {{ $t('common.btn_cancel') }}
      </Button>
      <Button variant="primary" class="min-w-[140px] px-6 shadow-lg shadow-indigo-500/25" :loading="props.loading" @click="submit">
        <template #leftIcon><Check :size="16" :stroke-width="3" /></template>
        {{ props.client ? $t('admin_client_list.btn_save_changes') : $t('admin_client_list.btn_complete_setup') }}
      </Button>
    </template>
  </BaseDialog>
</template>
