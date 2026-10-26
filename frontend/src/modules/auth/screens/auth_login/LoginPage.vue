<script setup lang="ts">
// Port LoginPage.tsx: giữ nguyên phần hero (logo, tiêu đề DX Platform, vạch nhấn, mô tả, nút viên thuốc gradient, chân trang).
// Bản gốc chỉ có nút "Đăng nhập với SSO" qua hệ thống ngoài; bản mới quản lý tài khoản ngay trong phần mềm:
// đăng nhập email + mật khẩu (/api/v1/auth/login, ASP.NET Identity), quên mật khẩu qua email, quản trị mời người dùng.
import { reactive, ref } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { Eye, EyeOff, Zap } from '@lucide/vue';
import { useAuthStore } from '@/stores/auth';
import { ApiError } from '@/api/http';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const auth = useAuthStore();

// ?email=… do trang đặt mật khẩu chuyển về sau khi đặt xong
const form = reactive({ email: typeof route.query.email === 'string' ? route.query.email : '', password: '' });
const fieldErrors = reactive<{ email?: string; password?: string }>({});
const error = ref<string | null>(null);
const submitting = ref(false);
const showPassword = ref(false);

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function validate(): boolean {
  fieldErrors.email = !form.email.trim()
    ? t('validation.required')
    : EMAIL_PATTERN.test(form.email.trim())
      ? undefined
      : t('validation.invalid_email');
  fieldErrors.password = form.password ? undefined : t('validation.required');
  return !fieldErrors.email && !fieldErrors.password;
}

function redirectTarget(): string {
  const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/';
  return redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : '/';
}

async function submit() {
  error.value = null;
  if (!validate()) return;
  submitting.value = true;
  try {
    await auth.login(form.email.trim(), form.password);
    await router.replace(redirectTarget());
  } catch (err) {
    if (err instanceof ApiError && err.status === 400) {
      fieldErrors.email = err.fieldErrors.email?.[0];
      fieldErrors.password = err.fieldErrors.password?.[0];
    }
    error.value = err instanceof Error ? err.message : String(err);
  } finally {
    submitting.value = false;
  }
}

const inputClass =
  'w-full h-12 rounded-2xl border bg-white/80 backdrop-blur px-5 text-[15px] font-medium text-[#0F172A] placeholder:text-[#94A3B8] outline-none transition-[border-color,box-shadow,background-color] duration-200 focus:bg-white focus:border-[#4F7DF9] focus:ring-[3.5px] focus:ring-[#4F7DF9]/15';
</script>

<template>
  <div class="min-h-screen flex items-center justify-center p-5 bg-[radial-gradient(circle_at_top,_rgba(79,125,249,0.18),_transparent_32%),linear-gradient(135deg,_#f8f9fc_0%,_#edf2ff_48%,_#f3eeff_100%)]">
    <div class="text-center max-w-md w-full animate-[fadeInUp_0.8s_ease-out] px-8 py-10">
      <!-- Logo -->
      <div class="mb-8 animate-[float_3s_ease-in-out_infinite]">
        <div class="w-24 h-24 mx-auto drop-shadow-lg flex items-center justify-center rounded-lg bg-gradient-to-br from-[#4F7DF9] to-[#8B5CF6] text-white shadow-lg">
          <Zap :size="96" fill="currentColor" />
        </div>
      </div>

      <!-- Tiêu đề -->
      <h1 class="text-[2.5rem] font-bold tracking-tight mb-2 leading-tight">
        <span class="text-[#0F172A]">DX</span>
        <span class="bg-gradient-to-r from-[#4F7DF9] to-[#8B5CF6] bg-clip-text text-transparent"> Platform</span>
      </h1>

      <!-- Vạch nhấn -->
      <div class="w-14 h-1 bg-gradient-to-r from-[#4F7DF9] to-[#8B5CF6] rounded mx-auto mb-6 animate-[fadeInUp_0.8s_ease-out_0.2s_both]" />

      <!-- Mô tả -->
      <p class="text-[#475569] text-base leading-relaxed mb-10 font-normal animate-[fadeInUp_0.8s_ease-out_0.3s_both]">
        {{ $t('auth_login.lbl_tagline_platform') }}<br />
        {{ $t('auth_login.lbl_tagline_ai') }}
      </p>

      <!-- Form đăng nhập -->
      <form class="space-y-4 text-left animate-[fadeInUp_0.8s_ease-out_0.35s_both]" novalidate @submit.prevent="submit">
        <div class="space-y-1.5">
          <label for="login-email" class="block text-[11px] font-bold uppercase tracking-widest text-[#64748B] pl-1">{{ $t('auth_login.lbl_email') }}</label>
          <input
            id="login-email"
            v-model="form.email"
            type="email"
            autocomplete="username"
            :placeholder="$t('auth_login.ph_email')"
            :class="[inputClass, fieldErrors.email ? 'border-red-400 bg-red-50/40' : 'border-[#E2E8F0]']"
            :aria-invalid="!!fieldErrors.email"
            @input="fieldErrors.email = undefined"
          />
          <p v-if="fieldErrors.email" class="pl-1 text-[11px] font-bold text-red-500">{{ fieldErrors.email }}</p>
        </div>

        <div class="space-y-1.5">
          <div class="flex items-center justify-between pl-1">
            <label for="login-password" class="block text-[11px] font-bold uppercase tracking-widest text-[#64748B]">{{ $t('auth_login.lbl_password') }}</label>
            <RouterLink to="/auth/forgot-password" class="text-[12px] font-semibold text-[#4F7DF9] hover:text-[#8B5CF6] transition-colors">
              {{ $t('auth_login.btn_forgot_password') }}
            </RouterLink>
          </div>
          <div class="relative">
            <input
              id="login-password"
              v-model="form.password"
              :type="showPassword ? 'text' : 'password'"
              autocomplete="current-password"
              :placeholder="$t('auth_login.ph_password')"
              :class="[inputClass, 'pr-12', fieldErrors.password ? 'border-red-400 bg-red-50/40' : 'border-[#E2E8F0]']"
              :aria-invalid="!!fieldErrors.password"
              @input="fieldErrors.password = undefined"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 flex h-8 w-8 items-center justify-center rounded-xl text-[#94A3B8] hover:text-[#4F7DF9] hover:bg-[#4F7DF9]/5 transition-colors"
              :aria-label="showPassword ? $t('auth_login.btn_hide_password') : $t('auth_login.btn_show_password')"
              @click="showPassword = !showPassword"
            >
              <EyeOff v-if="showPassword" :size="18" />
              <Eye v-else :size="18" />
            </button>
          </div>
          <p v-if="fieldErrors.password" class="pl-1 text-[11px] font-bold text-red-500">{{ fieldErrors.password }}</p>
        </div>

        <!-- Nút đăng nhập (giữ kiểu nút viên thuốc gradient của bản gốc) -->
        <div class="pt-4 text-center">
          <button
            type="submit"
            :disabled="submitting"
            class="inline-flex items-center justify-center gap-2.5 bg-gradient-to-r from-[#4F7DF9] to-[#8B5CF6] text-white text-base font-semibold px-8 py-4 rounded-full cursor-pointer transition-all duration-300 shadow-[0_10px_30px_rgba(79,125,249,0.32)] no-underline animate-[fadeInUp_0.8s_ease-out_0.4s_both] hover:translate-y-[-2px] hover:shadow-[0_16px_40px_rgba(109,59,212,0.28)] active:translate-y-0 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:translate-y-0 disabled:hover:shadow-[0_10px_30px_rgba(79,125,249,0.32)]"
          >
            <svg class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
              <polyline points="10 17 15 12 10 7" />
              <line x1="15" y1="12" x2="3" y2="12" />
            </svg>
            {{ submitting ? $t('auth_login.btn_logging_in') : $t('auth_login.btn_login') }}
          </button>
        </div>
      </form>

      <p v-if="error" role="alert" class="mt-4 text-sm text-[#B91C1C] animate-[fadeInUp_0.8s_ease-out_0.45s_both]">{{ error }}</p>

      <!-- Chân trang -->
      <p class="mt-12 text-xs text-[#94A3B8] animate-[fadeInUp_0.8s_ease-out_0.5s_both]">{{ $t('auth_login.lbl_footer') }}</p>
    </div>
  </div>
</template>
