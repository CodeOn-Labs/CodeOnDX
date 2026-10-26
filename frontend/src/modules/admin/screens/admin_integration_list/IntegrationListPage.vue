<script setup lang="ts">
// Port IntegrationsListPage.tsx + IntegrationCard.tsx — Quản trị → Cài đặt tích hợp, nhóm theo loại.
// Khác bản gốc (Q3): 5 connector cố định Ai, GitHub, GitLab, GoogleDrive, Email. Khoá bí mật / OAuth token chỉ cấu hình
// trên server (Integrations:<Key> trong appsettings.Secrets.json), nên không có luồng OAuth popup + OAuthCallbackPage;
// giao diện chỉ bật/tắt, sửa thông số không bí mật, kiểm tra kết nối và xem trước dữ liệu (thay các Explorer của bản gốc).
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Bot, CheckCircle2, Eye, FlaskConical, GitBranch, HardDrive, KeyRound, Mail, MessageSquare, Plug, Settings2, XCircle } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import IntegrationDialog from './IntegrationDialog.vue';
import IntegrationPreviewDialog from './IntegrationPreviewDialog.vue';
import { systemApi, type Integration } from '@/modules/admin/api/system.api';
import { enumLabel } from '@/shared/i18n/enum-label';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const { t } = useI18n();
const toast = useToastStore();
const queryClient = useQueryClient();
const query = useQuery({ queryKey: ['integrations'], queryFn: systemApi.integrations });

const CATEGORY_ORDER = ['ai', 'source_control', 'file_storage', 'communication'] as const;
const CATEGORY_ICONS = { ai: Bot, source_control: GitBranch, file_storage: HardDrive, communication: MessageSquare } as const;
const CATEGORY_ICON_COLORS = {
  ai: 'bg-violet-50 text-violet-600 border-violet-100',
  source_control: 'bg-orange-50 text-orange-600 border-orange-100',
  file_storage: 'bg-indigo-50 text-indigo-600 border-indigo-100',
  communication: 'bg-amber-50 text-amber-600 border-amber-100',
} as const;
const CONNECTOR_ICONS = { Ai: Bot, GitHub: GitBranch, GitLab: GitBranch, GoogleDrive: HardDrive, Email: Mail } as const;

const groups = computed(() =>
  CATEGORY_ORDER.map((category) => ({ category, items: (query.data.value ?? []).filter((i) => i.category === category) })).filter((g) => g.items.length > 0),
);
const categoryDesc = (c: (typeof CATEGORY_ORDER)[number]) =>
  ({
    ai: t('admin_integration_list.hint_category_ai'),
    source_control: t('admin_integration_list.hint_category_source_control'),
    file_storage: t('admin_integration_list.hint_category_file_storage'),
    communication: t('admin_integration_list.hint_category_communication'),
  })[c];

const busy = ref<string | null>(null);
async function toggle(item: Integration) {
  busy.value = item.key;
  try {
    const res = await systemApi.updateIntegration(item.key, { enabled: !item.enabled, settings: item.settings });
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['integrations'] });
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    busy.value = null;
  }
}
async function test(item: Integration) {
  busy.value = item.key;
  try {
    toast.success((await systemApi.testIntegration(item.key)).messageText);
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    busy.value = null;
    void queryClient.invalidateQueries({ queryKey: ['integrations'] });
  }
}
const editing = ref<Integration | null>(null);
const previewing = ref<Integration | null>(null);
</script>

<template>
  <PageLayout layout="C" ai-module="admin" content-padding="24px" disable-scroll>
    <div class="h-full flex flex-col min-h-0 gap-4">
      <div class="flex flex-col gap-1">
        <div class="flex items-center gap-2">
          <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
          <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ $t('admin_integration_list.lbl_kicker') }}</span>
        </div>
        <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">{{ $t('admin_integration_list.title_page') }}</h1>
        <p class="text-[12px] font-medium text-slate-500">{{ $t('admin_integration_list.hint_page') }}</p>
      </div>

      <div class="flex-1 min-h-0 overflow-y-auto pr-1 space-y-8 pb-6">
        <div v-if="query.isPending.value" class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <div v-for="i in 3" :key="i" class="h-48 rounded-[24px] bg-white/60 border border-slate-100 animate-pulse" />
        </div>
        <section v-for="group in groups" :key="group.category" class="space-y-4">
          <div class="flex items-center gap-3">
            <div :class="cn('flex h-10 w-10 items-center justify-center rounded-2xl border', CATEGORY_ICON_COLORS[group.category])">
              <component :is="CATEGORY_ICONS[group.category]" :size="18" />
            </div>
            <div>
              <h2 class="text-[15px] font-black text-slate-800">{{ enumLabel('integration_category', group.category) }}</h2>
              <p class="text-[11px] font-medium text-slate-400">{{ categoryDesc(group.category) }}</p>
            </div>
          </div>
          <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <div
              v-for="item in group.items"
              :key="item.key"
              :class="cn(
                'group flex flex-col gap-4 rounded-[24px] border bg-white/70 p-5 shadow-sm backdrop-blur-xl transition-all duration-300 hover:-translate-y-0.5 hover:shadow-lg',
                item.enabled ? 'border-emerald-200/70' : 'border-slate-200/70',
              )"
            >
              <div class="flex items-start justify-between gap-3">
                <div class="flex items-center gap-3">
                  <div :class="cn('flex h-11 w-11 items-center justify-center rounded-2xl border shadow-sm', item.enabled ? 'bg-emerald-50 text-emerald-600 border-emerald-100' : 'bg-slate-50 text-slate-400 border-slate-100')">
                    <component :is="CONNECTOR_ICONS[item.key]" :size="20" />
                  </div>
                  <div>
                    <h3 class="text-[14px] font-black text-slate-900">{{ enumLabel('connector', item.key, item.key) }}</h3>
                    <div class="mt-1 flex items-center gap-1.5">
                      <span :class="cn('rounded-full px-2 py-0.5 text-[9px] font-black uppercase tracking-widest', item.useMock ? 'bg-amber-50 text-amber-600' : 'bg-emerald-50 text-emerald-600')">
                        {{ item.useMock ? $t('admin_integration_list.lbl_mode_mock') : $t('admin_integration_list.lbl_mode_live') }}
                      </span>
                      <span v-if="!item.useMock" :class="cn('flex items-center gap-1 text-[10px] font-bold', item.hasSecret ? 'text-emerald-600' : 'text-rose-500')">
                        <KeyRound :size="11" />
                        {{ item.hasSecret ? $t('admin_integration_list.lbl_secret_set') : $t('admin_integration_list.lbl_secret_missing') }}
                      </span>
                    </div>
                  </div>
                </div>
                <SwitchToggle :model-value="item.enabled" :disabled="busy === item.key" :label="$t('admin_integration_list.tip_toggle')" @update:model-value="toggle(item)" />
              </div>

              <p class="text-[12px] leading-relaxed text-slate-500">{{ enumLabel('connector', `${item.key}_desc`) }}</p>

              <div class="flex flex-col gap-1 rounded-xl bg-slate-50/70 p-3 text-[11px]">
                <div class="flex items-center justify-between">
                  <span class="font-bold text-slate-400">{{ $t('admin_integration_list.lbl_last_test') }}</span>
                  <span v-if="item.lastTestAt" :class="cn('flex items-center gap-1 font-bold', item.lastTestOk ? 'text-emerald-600' : 'text-rose-500')">
                    <CheckCircle2 v-if="item.lastTestOk" :size="12" />
                    <XCircle v-else :size="12" />
                    {{ formatDate(item.lastTestAt, 'dd/MM/yyyy HH:mm') }}
                  </span>
                  <span v-else class="font-semibold text-slate-300">—</span>
                </div>
                <div class="flex items-center justify-between">
                  <span class="font-bold text-slate-400">{{ $t('admin_integration_list.lbl_last_sync') }}</span>
                  <span class="font-semibold text-slate-600">{{ formatDate(item.lastSyncAt, 'dd/MM/yyyy HH:mm') || '—' }}</span>
                </div>
              </div>

              <div class="mt-auto flex items-center gap-2">
                <Button variant="outline" size="sm" class="flex-1" @click="editing = item">
                  <template #leftIcon><Settings2 :size="14" /></template>
                  {{ $t('admin_integration_list.btn_configure') }}
                </Button>
                <Button variant="outline" size="sm" :loading="busy === item.key" :disabled="!item.enabled" :title="$t('admin_integration_list.btn_test')" @click="test(item)">
                  <template #leftIcon><FlaskConical :size="14" /></template>
                  {{ $t('admin_integration_list.btn_test') }}
                </Button>
                <Button v-if="item.key !== 'Ai'" variant="ghost" size="sm" :disabled="!item.enabled" :title="$t('admin_integration_list.btn_preview')" @click="previewing = item">
                  <Eye :size="14" />
                </Button>
              </div>
            </div>
          </div>
        </section>
        <div v-if="!query.isPending.value && groups.length === 0" class="flex flex-col items-center gap-2 py-16 text-slate-400">
          <Plug :size="28" />
          <span class="text-[13px] font-semibold">{{ $t('admin_integration_list.empty_no_integration') }}</span>
        </div>
      </div>
    </div>

    <IntegrationDialog :integration="editing" @close="editing = null" />
    <IntegrationPreviewDialog :integration="previewing" @close="previewing = null" />
  </PageLayout>
</template>
