<script setup lang="ts">
// Port MainLayout.tsx: Sidebar + vùng nội dung có hiệu ứng chuyển trang (thay framer-motion bằng <Transition>).
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import Sidebar from './Sidebar.vue';
import RouteProgressBar from '@/shared/components/RouteProgressBar.vue';

const route = useRoute();
/** Chỉ hiệu ứng khi đổi nhóm trang (/projects → /admin), giống key={baseRoute} của bản gốc. */
const baseRoute = computed(() => route.path.split('/')[1] || 'home');
</script>

<template>
  <div class="flex h-full w-full overflow-hidden bg-bg text-t1 font-sans text-xs">
    <RouteProgressBar />
    <Sidebar />
    <main class="relative flex min-w-0 flex-1 overflow-hidden bg-bg1">
      <RouterView v-slot="{ Component }">
        <Transition
          mode="out-in"
          enter-active-class="transition duration-[250ms] ease-out"
          enter-from-class="opacity-0 scale-[0.97]"
          leave-active-class="transition duration-[250ms] ease-out"
          leave-to-class="opacity-0 scale-[0.97]"
        >
          <div :key="baseRoute" class="flex-1 h-full w-full min-h-0 flex flex-col overflow-hidden">
            <component :is="Component" />
          </div>
        </Transition>
      </RouterView>
    </main>
  </div>
</template>
