<script setup lang="ts">
// Màn mới (D4): quên mật khẩu — nhập email, BE gửi link đặt lại mật khẩu qua connector Email.
// Luôn hiện cùng một thông báo dù email có tồn tại hay không (BE quyết định), tránh dò tài khoản.
import { ref } from 'vue';
import { RouterLink } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { ArrowLeft, MailCheck } from '@lucide/vue';
import AuthShell from '@/modules/auth/components/AuthShell.vue';
import { accountApi } from '@/modules/auth/api/auth.api';
import { ApiError } from '@/api/http';

const { t } = useI18n();
const email = ref('');
const fieldError = ref<string | undefined>();
const error = ref<string | null>(null);
const sentMessage = ref<string | null>(null);
const submitting = ref(false);

async function submit() {
  error.value = null;
  const value = email.value.trim();
  fieldError.value = !value ? t('validation.required') : /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value) ? undefined : t('validation.invalid_email');
  if (fieldError.value) return;
  submitting.value = true;
  try {
    sentMessage.value = (await accountApi.forgotPassword(value)).messageText;
  } catch (err) {
    if (err instanceof ApiError && err.status === 400) fieldError.value = err.fieldErrors.email?.[0];
    else error.value = (err as Error).message;
  } finally {
    submitting.value = false;
  }
}
const inputClass =
  'w-full h-12 rounded-2xl border bg-white/80 backdrop-blur px-5 text-[15px] font-medium text-[#0F172A] placeholder:text-[#94A3B8] outline-none transition-[border-color,box-shadow,background-color] duration-200 focus:bg-white focus:border-[#4F7DF9] focus:ring-[3.5px] focus:ring-[#4F7DF9]/15';
</script>

<template>
  <AuthShell :title="$t('auth_forgot_password.title_page')" :subtitle="sentMessage ? undefined : $t('auth_forgot_password.lbl_subtitle')">
    <div v-if="sentMessage" class="flex flex-col items-center gap-4 rounded-3xl border border-emerald-100 bg-white/80 p-6 shadow-sm">
      <div class="flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-50 text-emerald-500"><MailCheck :size="28" /></div>
      <p class="text-[14px] font-medium text-slate-600 leading-relaxed">{{ sentMessage }}</p>
      <p class="text-[12px] text-slate-400">{{ $t('auth_forgot_password.hint_check_spam') }}</p>
    </div>
    <form v-else class="space-y-4 text-left" novalidate @submit.prevent="submit">
      <div class="space-y-1.5">
        <label for="forgot-email" class="block text-[11px] font-bold uppercase tracking-widest text-[#64748B] pl-1">{{ $t('auth_forgot_password.lbl_email') }}</label>
        <input
          id="forgot-email"
          v-model="email"
          type="email"
          autocomplete="username"
          :placeholder="$t('auth_forgot_password.ph_email')"
          :class="[inputClass, fieldError ? 'border-red-400 bg-red-50/40' : 'border-[#E2E8F0]']"
          :aria-invalid="!!fieldError"
          @input="fieldError = undefined"
        />
        <p v-if="fieldError" class="pl-1 text-[11px] font-bold text-red-500">{{ fieldError }}</p>
      </div>
      <div class="pt-3 text-center">
        <button
          type="submit"
          :disabled="submitting"
          class="inline-flex items-center justify-center gap-2.5 bg-gradient-to-r from-[#4F7DF9] to-[#8B5CF6] text-white text-base font-semibold px-8 py-4 rounded-full cursor-pointer transition-all duration-300 shadow-[0_10px_30px_rgba(79,125,249,0.32)] hover:translate-y-[-2px] active:translate-y-0 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {{ submitting ? $t('auth_forgot_password.btn_sending') : $t('auth_forgot_password.btn_send') }}
        </button>
      </div>
      <p v-if="error" role="alert" class="text-center text-sm text-[#B91C1C]">{{ error }}</p>
    </form>
    <RouterLink to="/auth/login" class="mt-8 inline-flex items-center gap-1.5 text-[13px] font-semibold text-[#4F7DF9] hover:text-[#8B5CF6]">
      <ArrowLeft :size="14" />
      {{ $t('auth_forgot_password.btn_back_to_login') }}
    </RouterLink>
  </AuthShell>
</template>
