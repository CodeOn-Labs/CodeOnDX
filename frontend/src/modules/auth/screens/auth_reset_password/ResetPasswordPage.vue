<script setup lang="ts">
// Màn mới (D4): đặt mật khẩu từ link email — dùng chung cho "quên mật khẩu" và "nhận lời mời tạo tài khoản".
// Chính sách mật khẩu (độ dài tối thiểu) lấy từ Cài đặt chung qua BE; BE kiểm tra lại khi lưu.
import { computed, reactive, ref } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { ArrowLeft, Eye, EyeOff, XCircle } from '@lucide/vue';
import AuthShell from '@/modules/auth/components/AuthShell.vue';
import { accountApi } from '@/modules/auth/api/auth.api';
import { ApiError } from '@/api/http';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToastStore();
const token = computed(() => String(route.params.token ?? ''));

const info = useQuery({ queryKey: computed(() => ['reset-token', token.value] as const), queryFn: () => accountApi.resetTokenInfo(token.value), retry: false, meta: { silent: true } });
const form = reactive({ password: '', confirmPassword: '' });
const errors = reactive<{ password?: string; confirmPassword?: string }>({});
const error = ref<string | null>(null);
const submitting = ref(false);
const show = ref(false);

/** Các điều kiện hiển thị dạng checklist — cùng luật với BE (account-routes.mjs#validatePassword). */
const rules = computed(() => {
  const min = info.data.value?.passwordMinLength ?? 8;
  return [
    { ok: form.password.length >= min, label: t('auth_reset_password.lbl_rule_length', { min }) },
    { ok: /[A-Z]/.test(form.password) && /[a-z]/.test(form.password), label: t('auth_reset_password.lbl_rule_case') },
    { ok: /\d/.test(form.password), label: t('auth_reset_password.lbl_rule_digit') },
  ];
});

async function submit() {
  error.value = null;
  errors.password = !form.password ? t('validation.required') : rules.value.every((r) => r.ok) ? undefined : t('auth_reset_password.err_weak');
  errors.confirmPassword = form.confirmPassword !== form.password ? t('auth_reset_password.err_mismatch') : undefined;
  if (errors.password || errors.confirmPassword) return;
  submitting.value = true;
  try {
    const res = await accountApi.resetPassword(token.value, form.password, form.confirmPassword);
    toast.success(res.messageText);
    await router.replace({ path: '/auth/login', query: info.data.value?.email ? { email: info.data.value.email } : {} });
  } catch (err) {
    if (err instanceof ApiError && err.status === 400) {
      errors.password = err.fieldErrors.password?.[0];
      errors.confirmPassword = err.fieldErrors.confirmPassword?.[0];
    } else error.value = (err as Error).message;
  } finally {
    submitting.value = false;
  }
}
const inputClass =
  'w-full h-12 rounded-2xl border bg-white/80 backdrop-blur px-5 pr-12 text-[15px] font-medium text-[#0F172A] placeholder:text-[#94A3B8] outline-none transition-[border-color,box-shadow,background-color] duration-200 focus:bg-white focus:border-[#4F7DF9] focus:ring-[3.5px] focus:ring-[#4F7DF9]/15';
const title = computed(() => (info.data.value?.kind === 'invite' ? t('auth_reset_password.title_invite') : t('auth_reset_password.title_page')));
</script>

<template>
  <AuthShell :title="title" :subtitle="info.data.value ? $t('auth_reset_password.lbl_subtitle', { email: info.data.value.email }) : undefined">
    <div v-if="info.isPending.value" class="py-10 text-[13px] text-slate-400 animate-pulse">{{ $t('auth_reset_password.lbl_checking') }}</div>
    <div v-else-if="info.error.value" class="flex flex-col items-center gap-3 rounded-3xl border border-slate-100 bg-white/80 p-6 shadow-sm">
      <XCircle :size="36" class="text-slate-300" />
      <p class="text-[14px] font-semibold text-slate-600">{{ info.error.value.message }}</p>
      <RouterLink to="/auth/forgot-password" class="text-[13px] font-semibold text-[#4F7DF9] hover:text-[#8B5CF6]">{{ $t('auth_reset_password.btn_request_again') }}</RouterLink>
    </div>
    <form v-else class="space-y-4 text-left" novalidate @submit.prevent="submit">
      <div class="space-y-1.5">
        <label for="reset-password" class="block text-[11px] font-bold uppercase tracking-widest text-[#64748B] pl-1">{{ $t('auth_reset_password.lbl_password') }}</label>
        <div class="relative">
          <input id="reset-password" v-model="form.password" :type="show ? 'text' : 'password'" autocomplete="new-password" :class="[inputClass, errors.password ? 'border-red-400 bg-red-50/40' : 'border-[#E2E8F0]']" />
          <button
            type="button"
            class="absolute right-3 top-1/2 -translate-y-1/2 flex h-8 w-8 items-center justify-center rounded-xl text-[#94A3B8] hover:text-[#4F7DF9] hover:bg-[#4F7DF9]/5"
            :aria-label="show ? $t('auth_reset_password.btn_hide_password') : $t('auth_reset_password.btn_show_password')"
            @click="show = !show"
          >
            <EyeOff v-if="show" :size="18" />
            <Eye v-else :size="18" />
          </button>
        </div>
        <p v-if="errors.password" class="pl-1 text-[11px] font-bold text-red-500">{{ errors.password }}</p>
        <ul class="pl-1 pt-1 space-y-0.5">
          <li v-for="rule in rules" :key="rule.label" :class="cn('text-[11px] font-semibold flex items-center gap-1.5', rule.ok ? 'text-emerald-600' : 'text-slate-400')">
            <span :class="cn('h-1.5 w-1.5 rounded-full', rule.ok ? 'bg-emerald-500' : 'bg-slate-300')" />
            {{ rule.label }}
          </li>
        </ul>
      </div>
      <div class="space-y-1.5">
        <label for="reset-confirm" class="block text-[11px] font-bold uppercase tracking-widest text-[#64748B] pl-1">{{ $t('auth_reset_password.lbl_confirm_password') }}</label>
        <input id="reset-confirm" v-model="form.confirmPassword" :type="show ? 'text' : 'password'" autocomplete="new-password" :class="[inputClass, errors.confirmPassword ? 'border-red-400 bg-red-50/40' : 'border-[#E2E8F0]']" />
        <p v-if="errors.confirmPassword" class="pl-1 text-[11px] font-bold text-red-500">{{ errors.confirmPassword }}</p>
      </div>
      <div class="pt-3 text-center">
        <button
          type="submit"
          :disabled="submitting"
          class="inline-flex items-center justify-center gap-2.5 bg-gradient-to-r from-[#4F7DF9] to-[#8B5CF6] text-white text-base font-semibold px-8 py-4 rounded-full cursor-pointer transition-all duration-300 shadow-[0_10px_30px_rgba(79,125,249,0.32)] hover:translate-y-[-2px] active:translate-y-0 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {{ submitting ? $t('auth_reset_password.btn_saving') : $t('auth_reset_password.btn_save') }}
        </button>
      </div>
      <p v-if="error" role="alert" class="text-center text-sm text-[#B91C1C]">{{ error }}</p>
    </form>
    <RouterLink to="/auth/login" class="mt-8 inline-flex items-center gap-1.5 text-[13px] font-semibold text-[#4F7DF9] hover:text-[#8B5CF6]">
      <ArrowLeft :size="14" />
      {{ $t('auth_reset_password.btn_back_to_login') }}
    </RouterLink>
  </AuthShell>
</template>
