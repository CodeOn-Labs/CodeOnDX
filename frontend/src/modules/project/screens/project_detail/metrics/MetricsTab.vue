<script setup lang="ts">
// Port ProjectMetrixTab.tsx: 6 màn con — Timeline · EE · PCV · CSS · Defect · Leakage (?subtab=).
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import TabBar, { type TabItem } from '@/shared/components/TabBar.vue';
import TimelineScreen from './TimelineScreen.vue';
import EeScreen from './EeScreen.vue';
import PcvScreen from './PcvScreen.vue';
import CssScreen from './CssScreen.vue';
import QualityScreen from './QualityScreen.vue';
import type { ProjectDetail } from '@/modules/project/api/project-detail.api';

type Sub = 'timeline' | 'ee' | 'pcv' | 'css' | 'defect' | 'leakage';
const props = defineProps<{ project: ProjectDetail }>();
const subtab = defineModel<string>('subtab', { required: true });
const { t } = useI18n();
const SUBS: Sub[] = ['timeline', 'ee', 'pcv', 'css', 'defect', 'leakage'];
const current = computed<Sub>({
  get: () => (SUBS.includes(subtab.value as Sub) ? (subtab.value as Sub) : 'timeline'),
  set: (v) => (subtab.value = v),
});
const tabs = computed<TabItem<Sub>[]>(() => [
  { key: 'timeline', label: t('project_detail.tab_timeline') },
  { key: 'ee', label: 'EE' },
  { key: 'pcv', label: 'PCV' },
  { key: 'css', label: 'CSS' },
  { key: 'defect', label: 'Defect' },
  { key: 'leakage', label: 'Leakage' },
]);
</script>

<template>
  <div class="flex flex-col gap-4 pb-6">
    <TabBar v-model="current" :tabs="tabs" size="sm" />
    <TimelineScreen v-if="current === 'timeline'" :project="props.project" />
    <EeScreen v-else-if="current === 'ee'" :project="props.project" />
    <PcvScreen v-else-if="current === 'pcv'" :project="props.project" />
    <CssScreen v-else-if="current === 'css'" :project="props.project" />
    <QualityScreen v-else :key="current" :project="props.project" :kind="current" />
  </div>
</template>
