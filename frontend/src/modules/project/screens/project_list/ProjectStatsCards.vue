<script setup lang="ts">
// Port ProjectStatsCards.tsx: 5 chỉ số tổng hợp theo bộ lọc hiện tại (số liệu từ GET /projects/summary).
import { Activity, AlertTriangle, Briefcase, Calendar, Clock } from '@lucide/vue';
import ProjectStatSegment from '@/shared/components/StatSegment.vue';
import type { ProjectSummary } from '@/modules/project/api/project.api';

defineProps<{ summary: ProjectSummary | undefined; loading?: boolean }>();
</script>

<template>
  <div class="grid grid-cols-2 lg:grid-cols-5 gap-px rounded-2xl border border-white/60 bg-white/40 backdrop-blur-xl shadow-xl shadow-slate-200/40 overflow-hidden">
    <ProjectStatSegment
      :label="$t('project_list.stat_total')"
      :value="summary?.total ?? 0"
      tone="violet"
      :icon="Briefcase"
      :delay="0"
      :loading="loading && !summary"
      class="col-span-2 lg:col-span-1"
    />
    <ProjectStatSegment :label="$t('project_list.stat_running')" :value="summary?.running ?? 0" tone="green" :icon="Activity" :delay="0.05" :loading="loading && !summary" />
    <ProjectStatSegment :label="$t('project_list.stat_ending_soon')" :value="summary?.endingSoon ?? 0" tone="amber" :icon="Clock" :delay="0.1" :loading="loading && !summary" />
    <ProjectStatSegment :label="$t('project_list.stat_planning')" :value="summary?.planning ?? 0" tone="slate" :icon="Calendar" :delay="0.15" :loading="loading && !summary" />
    <ProjectStatSegment :label="$t('project_list.stat_at_risk')" :value="summary?.risky ?? 0" tone="red" :icon="AlertTriangle" :delay="0.2" :loading="loading && !summary" />
  </div>
</template>
