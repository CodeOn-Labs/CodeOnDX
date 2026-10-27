<script setup lang="ts">
// Sửa thông số không bí mật của một connector (port phần cấu hình của IntegrationModal.tsx).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { Check, KeyRound, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import { systemApi, type Integration } from '@/modules/admin/api/system.api';
import { enumLabel } from '@/shared/i18n/enum-label';
import { useFormMutation } from '@/shared/composables/useFormMutation';

const props = defineProps<{ integration: Integration | null }>();
const emit = defineEmits<{ close: [] }>();
const { t } = useI18n();
const open = computed({ get: () => props.integration !== null, set: (v) => !v && emit('close') });

const enabled = ref(false);
const settings = ref<Record<string, string>>({});
const errors = ref<Record<string, string>>({});
watch(
  () => props.integration,
  (i) => {
    if (!i) return;
    enabled.value = i.enabled;
    settings.value = { ...i.settings };
    errors.value = {};
  },
);

/** Nhãn từng thông số — liệt kê tĩnh để không ghép key i18n động. */
const fieldLabel = (key: string) =>
  ({
    baseUrl: t('admin_integration_list.lbl_field_base_url'),
    accountEmail: t('admin_integration_list.lbl_field_account_email'),
    projectKeys: t('admin_integration_list.lbl_field_project_keys'),
  })[key] ?? key;

const save = useFormMutation({
  mutationFn: () => systemApi.updateIntegration(props.integration!.key, { enabled: enabled.value, settings: settings.value }),
  invalidate: [['integrations']],
  onSuccess: () => emit('close'),
});
watch(save.fieldErrors, (v) => (errors.value = { ...v }));
</script>

<template>
  <BaseDialog
    v-model:open="open"
    :title="$t('admin_integration_list.title_config_dialog', { name: props.integration ? enumLabel('connector', props.integration.key, props.integration.key) : '' })"
    :description="$t('admin_integration_list.hint_config_dialog')"
    max-width="lg"
  >
    <div v-if="props.integration" class="flex flex-col gap-5">
      <div class="flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50 p-3">
        <span class="text-[12px] font-bold text-slate-700">{{ $t('admin_integration_list.lbl_enabled') }}</span>
        <SwitchToggle v-model="enabled" size="sm" :label="$t('admin_integration_list.lbl_enabled')" />
      </div>
      <FormItem v-for="(_, key) in settings" :key="key" :label="fieldLabel(String(key))" :error="errors[key]">
        <BaseInput v-model="settings[key]" :error="!!errors[key]" />
      </FormItem>
      <div class="flex items-start gap-2 rounded-xl border border-amber-100 bg-amber-50/60 p-3 text-[11px] font-medium text-amber-700">
        <KeyRound :size="14" class="mt-0.5 shrink-0" />
        <span>{{ $t('admin_integration_list.hint_secret', { key: props.integration.key }) }}</span>
      </div>
    </div>
    <template #footer>
      <Button variant="danger" :disabled="save.isPending.value" @click="emit('close')">
        <template #leftIcon><X :size="16" /></template>
        {{ $t('common.btn_cancel') }}
      </Button>
      <Button variant="primary" :loading="save.isPending.value" @click="save.mutate(undefined)">
        <template #leftIcon><Check :size="16" /></template>
        {{ $t('common.btn_save') }}
      </Button>
    </template>
  </BaseDialog>
</template>
