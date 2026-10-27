<script setup lang="ts">
// Nút cờ ở cuối Sidebar (phía trên Đăng xuất): đổi ngôn ngữ hiển thị và lưu vào hồ sơ (preferredLocale)
// để lần đăng nhập sau tự nạp đúng ngôn ngữ (auth store áp preferredLocale khi nhận phiên).
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { PopoverContent, PopoverPortal, PopoverRoot, PopoverTrigger } from 'reka-ui';
import { Check } from '@lucide/vue';
import { accountApi } from '@/modules/auth/api/auth.api';
import { queryClient } from '@/api/query-client';
import { setLocale, type AppLocale } from '@/i18n';
import { useAuthStore } from '@/stores/auth';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';
import FlagIcon from './FlagIcon.vue';

const { locale } = useI18n();
const auth = useAuthStore();
const toast = useToastStore();
const open = ref(false);

/** Tên ngôn ngữ viết bằng chính ngôn ngữ đó — không dịch. */
const LANGS: Array<{ code: AppLocale; name: string }> = [
  { code: 'vi', name: 'Tiếng Việt' },
  { code: 'en', name: 'English' },
];
const current = computed(() => LANGS.find((l) => l.code === locale.value) ?? LANGS[0]!);

async function change(code: AppLocale) {
  open.value = false;
  if (code === locale.value) return;
  setLocale(code);
  // Dữ liệu server trả theo ngôn ngữ (thông báo, nhãn danh mục) → tải lại
  void queryClient.invalidateQueries();
  if (!auth.isAuthenticated) return;
  try {
    await accountApi.updateProfile({ preferredLocale: code });
    auth.patchUser({ preferredLocale: code });
  } catch (err) {
    // Giữ ngôn ngữ đã đổi trên máy dù chưa lưu được lên server
    toast.error((err as Error).message);
  }
}
</script>

<template>
  <PopoverRoot v-model:open="open">
    <PopoverTrigger as-child>
      <button
        type="button"
        :aria-label="$t('component.language_switcher.tip_change_language')"
        :title="$t('component.language_switcher.tip_change_language')"
        class="flex h-11 w-11 items-center justify-center rounded-2xl transition-all duration-300 hover:bg-slate-50 active:scale-95 outline-none focus-visible:ring-2 focus-visible:ring-violet-600 focus-visible:ring-offset-2"
      >
        <FlagIcon :code="current.code" :size="24" />
      </button>
    </PopoverTrigger>
    <PopoverPortal>
      <PopoverContent
        side="right"
        align="end"
        :side-offset="12"
        class="z-[100] w-48 rounded-2xl border border-slate-100 bg-white p-1.5 shadow-2xl animate-in fade-in zoom-in-95 duration-150"
      >
        <div class="px-2.5 pt-1.5 pb-2 text-[10px] font-black uppercase tracking-widest text-slate-400">{{ $t('component.language_switcher.title_language') }}</div>
        <button
          v-for="opt in LANGS"
          :key="opt.code"
          type="button"
          :class="cn(
            'flex w-full items-center gap-3 rounded-xl px-2.5 py-2 text-left transition-colors outline-none focus-visible:ring-2 focus-visible:ring-violet-600',
            locale === opt.code ? 'bg-violet-50' : 'hover:bg-slate-50',
          )"
          @click="change(opt.code)"
        >
          <FlagIcon :code="opt.code" :size="20" />
          <span :class="cn('text-[13px] font-semibold', locale === opt.code ? 'text-violet-600' : 'text-slate-700')">{{ opt.name }}</span>
          <Check v-if="locale === opt.code" :size="15" class="ml-auto text-violet-600" />
        </button>
      </PopoverContent>
    </PopoverPortal>
  </PopoverRoot>
</template>
