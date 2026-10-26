<script setup lang="ts">
// Khung chung của các trang tài khoản công khai (quên / đặt lại mật khẩu): giữ phần hero của LoginPage.
import { Zap } from '@lucide/vue';

defineProps<{ title: string; subtitle?: string }>();
</script>

<template>
  <div class="min-h-screen flex items-center justify-center p-5 bg-[radial-gradient(circle_at_top,_rgba(79,125,249,0.18),_transparent_32%),linear-gradient(135deg,_#f8f9fc_0%,_#edf2ff_48%,_#f3eeff_100%)]">
    <div class="text-center max-w-md w-full animate-[fadeInUp_0.8s_ease-out] px-8 py-10">
      <div class="mb-6">
        <div class="w-16 h-16 mx-auto drop-shadow-lg flex items-center justify-center rounded-lg bg-gradient-to-br from-[#4F7DF9] to-[#8B5CF6] text-white shadow-lg">
          <Zap :size="40" fill="currentColor" />
        </div>
      </div>
      <h1 class="text-[1.9rem] font-bold tracking-tight mb-2 leading-tight text-[#0F172A]">{{ title }}</h1>
      <div class="w-14 h-1 bg-gradient-to-r from-[#4F7DF9] to-[#8B5CF6] rounded mx-auto mb-5" />
      <p v-if="subtitle" class="text-[#475569] text-[15px] leading-relaxed mb-8">{{ subtitle }}</p>
      <slot />
      <p class="mt-12 text-xs text-[#94A3B8]">{{ $t('component.auth_shell.lbl_footer') }}</p>
    </div>
  </div>
</template>
