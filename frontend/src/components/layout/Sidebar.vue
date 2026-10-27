<script setup lang="ts">
// Port Sidebar.tsx: thanh điều hướng dọc 72px — logo, các mục theo quyền, Quản trị, hồ sơ, ngôn ngữ (cờ), đăng xuất.
import { computed } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { LogOut, Settings, User, Zap } from '@lucide/vue';
import PortalTooltip from '@/shared/components/PortalTooltip.vue';
import LanguageSwitcher from './LanguageSwitcher.vue';
import { useAuthStore } from '@/stores/auth';
import { cn } from '@/shared/utils/cn';
import { ADMIN_PERMISSIONS, ADMIN_PREFIX, SIDEBAR_ITEMS, navPath, navPermission } from '@/app/navigation';

const route = useRoute();
const router = useRouter();
const auth = useAuthStore();

// Danh sách mục khai báo ở app/navigation.ts; đường dẫn + quyền lấy từ shared/screens.json
const navItems = computed(() =>
  SIDEBAR_ITEMS.filter((item) => auth.can(navPermission(item))).map((item) => ({ to: navPath(item), icon: item.icon, label: item.label() })),
);

const hasAdminAccess = computed(() => auth.can(ADMIN_PERMISSIONS));
const isAdmin = computed(() => route.path.startsWith(ADMIN_PREFIX));
const isActive = (to: string) => route.path.startsWith(to);
const displayName = computed(() => auth.user?.fullName?.trim() || auth.user?.email || 'DX User');

async function signOut() {
  await auth.logout();
  await router.replace('/auth/login');
}

const TOOLTIP_CLASS = 'rounded-xl bg-slate-900 px-3 py-2 text-[12px] font-bold text-white shadow-2xl border border-slate-950/20';
const NAV_CLASS =
  'group relative flex h-11 w-11 items-center justify-center rounded-2xl transition-all duration-300 outline-none focus-visible:ring-2 focus-visible:ring-violet-600 focus-visible:ring-offset-2';
const NAV_ACTIVE = 'bg-violet-50 text-violet-600 shadow-[0_8px_20px_rgba(124,58,237,0.06)]';
const NAV_IDLE = 'text-slate-400 hover:bg-slate-50 hover:text-slate-600';
</script>

<template>
  <div class="z-50 flex w-[72px] flex-shrink-0 flex-col items-center border-r border-slate-100 bg-white py-4 transition-all duration-300 h-full overflow-hidden">
    <!-- Logo -->
    <div class="flex-shrink-0 mb-4 flex items-center justify-center">
      <div class="group relative flex h-11 w-11 cursor-pointer items-center justify-center rounded-2xl bg-violet-600 text-white shadow-[0_12px_24px_rgba(124,58,237,0.25)] transition-all duration-300 hover:scale-105 active:scale-95">
        <Zap :size="22" fill="currentColor" class="transition-transform duration-500 group-hover:rotate-12" />
      </div>
    </div>

    <!-- Điều hướng chính -->
    <nav class="flex-1 w-full overflow-y-auto no-scrollbar px-2 flex flex-col items-center gap-2">
      <div v-for="item in navItems" :key="item.to" class="relative w-full flex flex-col items-center">
        <PortalTooltip :content="item.label" side="right" :offset="12" :content-class="TOOLTIP_CLASS" arrow-bg="#0f172a" arrow-border-color="transparent">
          <RouterLink :to="item.to" :aria-label="item.label" :class="cn(NAV_CLASS, isActive(item.to) ? NAV_ACTIVE : NAV_IDLE)">
            <component :is="item.icon" :size="22" :stroke-width="isActive(item.to) ? 2.5 : 2" class="transition-all duration-300" />
            <!-- Vạch đánh dấu mục đang chọn -->
            <span
              :class="cn(
                'absolute left-[-14px] top-1/2 h-8 w-1 -translate-y-1/2 rounded-r-full bg-violet-600 transition-all duration-500 shadow-[2px_0_8px_rgba(124,58,237,0.3)]',
                isActive(item.to) ? 'opacity-100 scale-y-100' : 'opacity-0 scale-y-50',
              )"
            />
          </RouterLink>
        </PortalTooltip>
      </div>
    </nav>

    <!-- Thao tác cuối thanh -->
    <div class="flex-shrink-0 mt-auto pt-4 border-t border-slate-100 w-full flex flex-col items-center gap-3 px-2 pb-2">
      <PortalTooltip
        v-if="hasAdminAccess"
        :content="$t('menu.admin')"
        side="right"
        :offset="12"
        :content-class="TOOLTIP_CLASS"
        arrow-bg="#0f172a"
        arrow-border-color="transparent"
      >
        <RouterLink to="/admin" :aria-label="$t('menu.admin')" :class="cn(NAV_CLASS, isAdmin ? NAV_ACTIVE : NAV_IDLE)">
          <Settings :size="22" :stroke-width="isAdmin ? 2.5 : 2" class="transition-all duration-300" />
        </RouterLink>
      </PortalTooltip>

      <RouterLink to="/profile" :aria-label="$t('menu.profile')" class="group relative flex flex-col items-center outline-none mt-2">
        <div class="flex h-11 w-11 items-center justify-center rounded-2xl border border-slate-100 bg-slate-50 text-slate-500 transition-all duration-300 hover:border-violet-200 hover:bg-violet-50 hover:text-violet-600 shadow-sm active:scale-95 group-focus-visible:ring-2 group-focus-visible:ring-violet-600 group-focus-visible:ring-offset-2 overflow-hidden">
          <img v-if="auth.user?.avatarUrl" :src="auth.user.avatarUrl" :alt="displayName" class="h-full w-full object-cover" />
          <User v-else :size="22" />
        </div>
      </RouterLink>

      <LanguageSwitcher />

      <PortalTooltip :content="$t('menu.sign_out')" side="right" :offset="12" :content-class="TOOLTIP_CLASS" arrow-bg="#0f172a" arrow-border-color="transparent">
        <button
          type="button"
          :aria-label="$t('menu.sign_out')"
          class="flex h-11 w-11 items-center justify-center rounded-2xl text-slate-400 transition-all duration-300 hover:bg-red-50 hover:text-red-500 active:scale-95 outline-none focus-visible:ring-2 focus-visible:ring-red-500 focus-visible:ring-offset-2"
          @click="signOut"
        >
          <LogOut :size="22" />
        </button>
      </PortalTooltip>
    </div>
  </div>
</template>
