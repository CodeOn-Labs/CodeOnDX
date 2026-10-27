<script setup lang="ts">
// Port PcvCriteriaPage.tsx — Quản trị → Tiêu chí PCV: biểu mẫu checklist PCV mặc định của tenant (D12).
// Thay đổi lưu ngay và chỉ áp dụng cho dự án tạo sau đó — checklist của dự án đã có giữ nguyên.
import { ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query';
import { ClipboardCheck, Info, Pencil, Plus, Trash2 } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import PcvItemDialog from './PcvItemDialog.vue';
import { configApi, firstFieldErrors, type PcvItemInput, type PcvTemplateItem } from '@/modules/admin/api/config.api';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const itemsQuery = useQuery({ queryKey: ['pcv-template-items'], queryFn: configApi.pcvItems });
const dialogOpen = ref(false);
const editing = ref<PcvTemplateItem | null>(null);
const fieldErrors = ref<Record<string, string>>({});

const saveMutation = useMutation({
  mutationFn: (input: PcvItemInput) => (editing.value ? configApi.updatePcvItem(editing.value.id, input) : configApi.createPcvItem(input)),
  onSuccess: (res) => {
    toast.success(res.messageText);
    dialogOpen.value = false;
    void queryClient.invalidateQueries({ queryKey: ['pcv-template-items'] });
  },
  onError: (err: Error) => {
    fieldErrors.value = firstFieldErrors(err);
    if (Object.keys(fieldErrors.value).length === 0) toast.error(err.message);
  },
});

function openDialog(item: PcvTemplateItem | null) {
  editing.value = item;
  fieldErrors.value = {};
  dialogOpen.value = true;
}

async function remove(item: PcvTemplateItem) {
  const ok = await confirm.ask({
    title: t('admin_pcv_criteria.confirm_delete_title'),
    description: t('admin_pcv_criteria.confirm_delete_desc', { label: item.label }),
    confirmText: t('admin_pcv_criteria.btn_confirm_delete'),
    type: 'danger',
  });
  if (!ok) return;
  try {
    const res = await configApi.deletePcvItem(item.id);
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['pcv-template-items'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}

const th = 'p-3 text-[11px] font-bold uppercase tracking-wide text-slate-500';
</script>

<template>
  <PageLayout layout="C" content-padding="24px">
    <div class="flex flex-col gap-4 rounded-[24px] border border-slate-200/80 bg-white p-6 shadow-sm">
      <div class="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-2">
        <div class="flex items-center gap-2.5">
          <div class="rounded-xl border border-violet-100 bg-violet-50 p-2 text-violet-600">
            <ClipboardCheck :size="16" class="stroke-[2.5]" />
          </div>
          <div>
            <h1 class="text-[13px] font-black uppercase tracking-tight text-slate-800">{{ $t('admin_pcv_criteria.title_page') }}</h1>
            <p class="text-[10px] font-medium text-slate-400">{{ $t('admin_pcv_criteria.lbl_subtitle') }}</p>
          </div>
        </div>
        <Button variant="outline" size="sm" @click="openDialog(null)">
          <template #leftIcon><Plus :size="14" /></template>
          {{ $t('admin_pcv_criteria.btn_add') }}
        </Button>
      </div>

      <p class="flex items-start gap-1.5 rounded-xl bg-slate-50 px-3 py-2 text-[11.5px] font-medium text-slate-500">
        <Info :size="13" class="mt-0.5 shrink-0" />
        {{ $t('admin_pcv_criteria.hint_template') }}
      </p>

      <p v-if="itemsQuery.isError.value" class="py-6 text-center text-[12.5px] font-semibold text-rose-500">{{ $t('admin_pcv_criteria.err_load') }}</p>
      <p v-else-if="!itemsQuery.isLoading.value && (itemsQuery.data.value ?? []).length === 0" class="py-6 text-center text-[12.5px] font-semibold text-slate-400">
        {{ $t('admin_pcv_criteria.empty_items') }}
      </p>
      <div v-else class="overflow-hidden rounded-xl border border-slate-200">
        <table class="w-full border-collapse text-left">
          <thead>
            <tr class="border-b border-slate-200 bg-slate-50/80">
              <th :class="cn(th, 'w-[48px] text-center')">#</th>
              <th :class="cn(th, 'w-[160px]')">{{ $t('admin_pcv_criteria.col_category') }}</th>
              <th :class="th">{{ $t('admin_pcv_criteria.col_label') }}</th>
              <th :class="cn(th, 'w-[90px] text-center')">{{ $t('admin_pcv_criteria.col_actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="(item, idx) in itemsQuery.data.value ?? []"
              :key="item.id"
              :class="cn('border-b border-slate-100 last:border-b-0', idx % 2 === 0 ? 'bg-white' : 'bg-slate-50/60')"
            >
              <td class="p-3 text-center text-[12px] font-semibold text-slate-400">{{ idx + 1 }}</td>
              <td class="p-3 text-[11px] font-black uppercase tracking-wide text-violet-500">{{ item.category }}</td>
              <td class="p-3 text-[13px] font-semibold text-slate-700">{{ item.label }}</td>
              <td class="p-3">
                <div class="flex items-center justify-center gap-1">
                  <button
                    type="button"
                    class="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-600"
                    :title="$t('admin_pcv_criteria.btn_edit')"
                    :aria-label="$t('admin_pcv_criteria.btn_edit')"
                    @click="openDialog(item)"
                  >
                    <Pencil :size="14" />
                  </button>
                  <button
                    type="button"
                    class="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-red-50 hover:text-red-500"
                    :title="$t('admin_pcv_criteria.btn_delete')"
                    :aria-label="$t('admin_pcv_criteria.btn_delete')"
                    @click="remove(item)"
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

    <PcvItemDialog
      v-model:open="dialogOpen"
      :item="editing"
      :loading="saveMutation.isPending.value"
      :server-errors="fieldErrors"
      @submit="saveMutation.mutate"
    />
  </PageLayout>
</template>
