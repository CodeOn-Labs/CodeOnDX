<script setup lang="ts">
// Port LanguagePage.tsx — chọn ngôn ngữ hiển thị (lưu vào hồ sơ người dùng: preferredLocale).
// Ngôn ngữ mặc định của cả tenant đặt ở Quản trị → Cài đặt (default_locale).
import { RouterLink } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { Check } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import { accountApi } from '@/modules/auth/api/auth.api';
import { setLocale, type AppLocale } from '@/i18n';
import { useAuthStore } from '@/stores/auth';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const { locale } = useI18n();
const auth = useAuthStore();
const toast = useToastStore();

const OPTIONS: Array<{ code: AppLocale; flag: string; name: string }> = [
  { code: 'vi', flag: '🇻🇳', name: 'Tiếng Việt' },
  { code: 'en', flag: '🇬🇧', name: 'English' },
];

async function change(code: AppLocale) {
  setLocale(code);
  try {
    const res = await accountApi.updateProfile({ preferredLocale: code });
    auth.patchUser({ preferredLocale: code });
    toast.success(res.messageText);
  } catch (err) {
    toast.error((err as Error).message);
  }
}
</script>

<template>
  <PageLayout layout="C" ai-module="admin" content-padding="24px">
    <div class="flex flex-col gap-4">
      <h1 class="text-[18px] font-bold text-slate-900">{{ $t('admin_language.title_page') }}</h1>
      <p class="text-[12px] font-medium text-slate-500">{{ $t('admin_language.hint_page') }}</p>
      <div class="flex flex-wrap gap-3">
        <button
          v-for="opt in OPTIONS"
          :key="opt.code"
          type="button"
          :class="cn(
            'flex items-center gap-3 min-w-[180px] rounded-2xl border p-4 text-left transition-all duration-200 outline-none focus-visible:ring-2 focus-visible:ring-violet-600 focus-visible:ring-offset-2',
            locale === opt.code ? 'border-violet-200 bg-violet-50 shadow-sm' : 'border-slate-100 bg-white hover:border-violet-200 hover:bg-violet-50/50',
          )"
          @click="change(opt.code)"
        >
          <span class="text-[28px] leading-none">{{ opt.flag }}</span>
          <span :class="cn('text-[14px] font-semibold', locale === opt.code ? 'text-violet-600' : 'text-slate-700')">{{ opt.name }}</span>
          <Check v-if="locale === opt.code" :size="16" class="ml-auto text-violet-600" />
        </button>
      </div>
      <RouterLink to="/admin/settings" class="w-fit text-[12px] font-semibold text-pr hover:underline">{{ $t('admin_language.btn_tenant_default') }}</RouterLink>
    </div>
  </PageLayout>
</template>
