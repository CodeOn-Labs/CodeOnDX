<script setup lang="ts">
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue';
import ToastContainer from '@/shared/components/ToastContainer.vue';

const isTestEnv = import.meta.env.VITE_APP_ENV === 'test';
</script>

<template>
  <div
    v-if="isTestEnv"
    class="pointer-events-none fixed top-0 left-1/2 z-[9998] -translate-x-1/2 rounded-b-md bg-amber-500 px-3 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white shadow-sm"
  >
    {{ $t('common.lbl_test_environment') }}
  </div>
  <RouterView />
  <ToastContainer />
  <ConfirmDialog />
</template>
