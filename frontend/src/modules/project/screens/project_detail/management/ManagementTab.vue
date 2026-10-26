<script setup lang="ts">
// Port ProjectManagementTab + ManagementSubTabBar: 4 màn con Tài liệu · Thay đổi (CR) · Issue · Rủi ro (?subtab=),
// mỗi màn giữ từ khoá tìm kiếm riêng (đổi màn không mang từ khoá sang; mất khi rời tab).
// Khác bản gốc: ô tìm kiếm lọc ngay danh sách của màn (bỏ popup gợi ý kết quả + nhảy tới dòng).
import { computed, reactive } from 'vue';
import { useI18n } from 'vue-i18n';
import { FileWarning, FolderOpen, GitPullRequest, Search, ShieldAlert, X } from '@lucide/vue';
import TabBar, { type TabItem } from '@/shared/components/TabBar.vue';
import DocumentsScreen from './DocumentsScreen.vue';
import ChangeScreen from './ChangeScreen.vue';
import IssueScreen from './IssueScreen.vue';
import RiskScreen from './RiskScreen.vue';
import type { ProjectDetail } from '@/modules/project/api/project-detail.api';

type Sub = 'documents' | 'change' | 'issue' | 'risk';
const props = defineProps<{ project: ProjectDetail }>();
const subtab = defineModel<string>('subtab', { required: true });
const { t } = useI18n();
const SUBS: Sub[] = ['documents', 'change', 'issue', 'risk'];
const current = computed<Sub>({
  get: () => (SUBS.includes(subtab.value as Sub) ? (subtab.value as Sub) : 'documents'),
  set: (v) => (subtab.value = v),
});
const tabs = computed<TabItem<Sub>[]>(() => [
  { key: 'documents', label: t('project_detail.management_section.lbl_documents'), icon: FolderOpen },
  { key: 'change', label: t('project_detail.management_section.lbl_change'), icon: GitPullRequest },
  { key: 'issue', label: t('project_detail.management_section.lbl_issue'), icon: FileWarning },
  { key: 'risk', label: t('project_detail.management_section.lbl_risk'), icon: ShieldAlert },
]);
const queries = reactive<Record<Sub, string>>({ documents: '', change: '', issue: '', risk: '' });
</script>

<template>
  <div class="flex flex-col gap-4 pb-6">
    <div class="flex flex-wrap items-end justify-between gap-3 border-b border-slate-100">
      <TabBar v-model="current" :tabs="tabs" size="sm" class="border-b-0" />
      <div class="relative mb-1.5 w-full max-w-[280px]">
        <Search :size="14" class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
        <input v-model="queries[current]" :placeholder="$t('project_detail.management_section.ph_search')" class="h-9 w-full rounded-xl border border-slate-200 bg-white pl-8 pr-8 text-[12.5px] outline-none focus:border-pr focus:ring-2 focus:ring-pr/15" />
        <button v-if="queries[current]" type="button" class="absolute right-2 top-1/2 -translate-y-1/2 rounded-full p-0.5 text-slate-400 hover:bg-slate-100" :aria-label="$t('project_detail.btn_clear_search')" @click="queries[current] = ''"><X :size="13" /></button>
      </div>
    </div>
    <DocumentsScreen v-if="current === 'documents'" :project="props.project" :query="queries.documents" />
    <ChangeScreen v-else-if="current === 'change'" :project="props.project" :query="queries.change" />
    <IssueScreen v-else-if="current === 'issue'" :project="props.project" :query="queries.issue" />
    <RiskScreen v-else :project="props.project" :query="queries.risk" />
  </div>
</template>
