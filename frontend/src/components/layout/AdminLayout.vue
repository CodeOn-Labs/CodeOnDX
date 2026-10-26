<script setup lang="ts">
// Port AdminLayout.tsx: thanh tab ngang của khu Quản trị + hiệu ứng chuyển tab.
import { computed } from 'vue';
import { RouterLink, useRoute } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { cn } from '@/shared/utils/cn';
import { ADMIN_TABS, navPath, navPermission } from '@/app/navigation';

const route = useRoute();
const auth = useAuthStore();

// Danh sách tab khai báo ở app/navigation.ts; đường dẫn + quyền lấy từ shared/screens.json
const tabs = computed(() =>
  ADMIN_TABS.filter((tab) => auth.can(navPermission(tab))).map((tab) => ({ path: navPath(tab), icon: tab.icon, label: tab.label() })),
);

const isActive = (path: string) => route.path === path || route.path.startsWith(`${path}/`);
</script>

<template>
  <div class="flex h-full min-h-0 flex-col overflow-hidden">
    <div class="flex-shrink-0 flex items-center gap-1 border-b border-slate-100 px-4 overflow-x-auto no-scrollbar">
      <RouterLink
        v-for="tab in tabs"
        :key="tab.path"
        :to="tab.path"
        :class="cn(
          'flex items-center gap-2 whitespace-nowrap px-3 py-3 text-[13px] font-semibold border-b-2 -mb-px transition-colors duration-200 outline-none focus-visible:ring-2 focus-visible:ring-violet-600 focus-visible:ring-offset-2',
          isActive(tab.path) ? 'border-violet-600 text-violet-600' : 'border-transparent text-slate-400 hover:text-slate-600',
        )"
      >
        <component :is="tab.icon" :size="16" :stroke-width="2.5" />
        {{ tab.label }}
      </RouterLink>
    </div>

    <div class="relative min-h-0 flex-1 overflow-hidden">
      <RouterView v-slot="{ Component, route: current }">
        <Transition
          mode="out-in"
          enter-active-class="transition duration-200 ease-out"
          enter-from-class="opacity-0 scale-[0.98]"
          leave-active-class="transition duration-200 ease-out"
          leave-to-class="opacity-0 scale-[0.98]"
        >
          <div :key="current.path" class="absolute inset-0 flex flex-col min-h-0 overflow-hidden">
            <component :is="Component" />
          </div>
        </Transition>
      </RouterView>
    </div>
  </div>
</template>
