<script setup lang="ts">
// Xem trước dữ liệu connector trả về (issue Jira, công việc Base, deal Bitrix24 — dữ liệu giả lập ở chế độ mock).
import { computed } from 'vue';
import { useQuery } from '@tanstack/vue-query';
import { ExternalLink, FileText } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import EmptyState from '@/shared/components/EmptyState.vue';
import { systemApi, type Integration } from '@/modules/admin/api/system.api';
import { enumLabel } from '@/shared/i18n/enum-label';
import { formatDate } from '@/shared/utils/date';

const props = defineProps<{ integration: Integration | null }>();
const emit = defineEmits<{ close: [] }>();
const open = computed({ get: () => props.integration !== null, set: (v) => !v && emit('close') });
const key = computed(() => props.integration?.key);
const query = useQuery({
  queryKey: computed(() => ['integrations', 'preview', key.value] as const),
  queryFn: () => systemApi.previewIntegration(key.value!),
  enabled: computed(() => !!key.value),
  meta: { silent: true },
});
</script>

<template>
  <BaseDialog v-model:open="open" :title="$t('admin_integration_list.title_preview_dialog', { name: key ? enumLabel('connector', key, key) : '' })" max-width="2xl">
    <div v-if="query.isPending.value" class="space-y-2">
      <div v-for="i in 4" :key="i" class="h-12 rounded-xl bg-slate-100 animate-pulse" />
    </div>
    <div v-else-if="query.error.value" class="rounded-xl border border-rose-100 bg-rose-50 p-4 text-[12px] font-semibold text-rose-600">{{ query.error.value.message }}</div>
    <EmptyState v-else-if="(query.data.value ?? []).length === 0" :title="$t('admin_integration_list.empty_preview')" compact />
    <ul v-else class="divide-y divide-slate-100 rounded-xl border border-slate-100">
      <li v-for="(item, i) in query.data.value" :key="i" class="flex items-center gap-3 px-4 py-3 hover:bg-slate-50/60">
        <FileText :size="16" class="shrink-0 text-slate-400" />
        <div class="min-w-0 flex-1">
          <div class="truncate text-[13px] font-bold text-slate-800">{{ item.title }}</div>
          <div class="truncate text-[11px] text-slate-400">{{ item.subtitle }}</div>
        </div>
        <span class="text-[11px] font-semibold text-slate-400">{{ formatDate(item.updatedAt, 'dd/MM/yyyy') }}</span>
        <a v-if="item.url" :href="item.url" target="_blank" rel="noopener noreferrer" class="p-1.5 rounded-lg text-slate-400 hover:bg-pr/10 hover:text-pr" :aria-label="$t('admin_integration_list.btn_open_link')">
          <ExternalLink :size="14" />
        </a>
      </li>
    </ul>
  </BaseDialog>
</template>
