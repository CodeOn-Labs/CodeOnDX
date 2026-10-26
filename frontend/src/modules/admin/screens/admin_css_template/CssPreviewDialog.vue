<script setup lang="ts">
// Port CssFormPreviewDialog (CssDialogs.tsx) — xem trước form khảo sát đúng như khách hàng sẽ nhận (tháng hiện tại).
import { computed } from 'vue';
import { Eye } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import CssCustomerForm from '@/modules/project/components/CssCustomerForm.vue';
import type { CssFormContent, CssQuestion } from '@/modules/admin/api/config.api';

const props = defineProps<{ content: CssFormContent; questions: CssQuestion[] }>();
const open = defineModel<boolean>('open', { required: true });

const periodMonth = computed(() => {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`;
});
</script>

<template>
  <BaseDialog v-model:open="open" :title="$t('admin_css_template.btn_preview')" max-width="3xl" body-class="bg-violet-50/40 px-5 py-5 flex-1 overflow-y-auto">
    <div class="mb-3 flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-2.5 text-[12.5px] font-semibold text-amber-700">
      <Eye :size="15" /> {{ $t('admin_css_template.hint_preview_banner') }}
    </div>
    <CssCustomerForm :content="props.content" :questions="props.questions" :period-month="periodMonth" preview />
  </BaseDialog>
</template>
