<script setup lang="ts">
// Port AdminLayout.tsx: thanh tab ngang của khu Quản trị + hiệu ứng chuyển tab.
// Thêm: tab nhóm (Cài đặt HRM, Insight) — nhóm đang mở hiện thanh tab phụ với các màn con ngay bên dưới.
import { computed } from 'vue';
import { RouterLink, useRoute } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { cn } from '@/shared/utils/cn';
import { ADMIN_TABS, isNavGroup, navPath, navPermission, type NavItem } from '@/app/navigation';

const route = useRoute();
const auth = useAuthStore();

const isActive = (path: string) => route.path === path || route.path.startsWith(`${path}/`);
const toLink = (item: NavItem) => ({ path: navPath(item), icon: item.icon, label: item.label() });

// Danh sách tab khai báo ở app/navigation.ts; đường dẫn + quyền lấy từ shared/screens.json
const tabs = computed(() =>
  ADMIN_TABS.flatMap((tab) => {
    if (!isNavGroup(tab)) return auth.can(navPermission(tab)) ? [{ key: navPath(tab), ...toLink(tab), children: [] }] : [];
    const children = tab.children.filter((item) => auth.can(navPermission(item))).map(toLink);
    if (children.length === 0) return [];
    return [{ key: tab.key, path: children[0]!.path, icon: tab.icon, label: tab.label(), children }];
  }),
);
const tabActive = (tab: (typeof tabs.value)[number]) => (tab.children.length ? tab.children.some((c) => isActive(c.path)) : isActive(tab.path));
const activeGroup = computed(() => tabs.value.find((tab) => tab.children.length > 0 && tabActive(tab)));
</script>

<template>
  <div class="flex h-full min-h-0 flex-col overflow-hidden">
    <div class="flex-shrink-0 flex items-center gap-1 border-b border-slate-100 px-4 overflow-x-auto no-scrollbar">
      <RouterLink
        v-for="tab in tabs"
        :key="tab.key"
        :to="tab.path"
        :class="cn(
          'flex items-center gap-2 whitespace-nowrap px-3 py-3 text-[13px] font-semibold border-b-2 -mb-px transition-colors duration-200 outline-none focus-visible:ring-2 focus-visible:ring-violet-600 focus-visible:ring-offset-2',
          tabActive(tab) ? 'border-violet-600 text-violet-600' : 'border-transparent text-slate-400 hover:text-slate-600',
        )"
      >
        <component :is="tab.icon" :size="16" :stroke-width="2.5" />
        {{ tab.label }}
      </RouterLink>
    </div>

    <!-- Thanh tab phụ của nhóm đang mở -->
    <div v-if="activeGroup" class="flex-shrink-0 flex items-center gap-1 border-b border-slate-100 bg-slate-50/60 px-4 py-1.5 overflow-x-auto no-scrollbar">
      <RouterLink
        v-for="child in activeGroup.children"
        :key="child.path"
        :to="child.path"
        :class="cn(
          'flex items-center gap-1.5 whitespace-nowrap rounded-xl px-3 py-1.5 text-[12px] font-semibold transition-colors duration-200 outline-none focus-visible:ring-2 focus-visible:ring-violet-600',
          isActive(child.path) ? 'bg-white text-violet-600 shadow-sm ring-1 ring-violet-100' : 'text-slate-400 hover:bg-white/70 hover:text-slate-600',
        )"
      >
        <component :is="child.icon" :size="14" :stroke-width="2.5" />
        {{ child.label }}
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
