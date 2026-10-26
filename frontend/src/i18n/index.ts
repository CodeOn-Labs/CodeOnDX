import { createI18n } from 'vue-i18n';
import vi from '@/locales/vi.json';
import en from '@/locales/en.json';

/** vi.json là nguồn chuẩn của kiểu key (IDE gợi ý key). Key không tồn tại bị chặn bởi eslint no-missing-keys và tools/i18n-check (Q1). */
export type MessageSchema = typeof vi;
export type AppLocale = 'vi' | 'en';
export const SUPPORTED_LOCALES: AppLocale[] = ['vi', 'en'];

const LOCALE_STORAGE_KEY = 'dx_locale';

function initialLocale(): AppLocale {
  try {
    const saved = localStorage.getItem(LOCALE_STORAGE_KEY);
    if (saved === 'vi' || saved === 'en') return saved;
  } catch {
    // localStorage có thể bị chặn (chế độ riêng tư) — dùng mặc định
  }
  return 'vi';
}

const dateFormats = {
  date_short: { day: '2-digit', month: '2-digit', year: 'numeric' },
  date_time: { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' },
  month_year: { month: '2-digit', year: 'numeric' },
  time_short: { hour: '2-digit', minute: '2-digit' },
} as const;

const numberFormats = {
  integer: { maximumFractionDigits: 0 },
  decimal: { maximumFractionDigits: 2 },
  fte: { minimumFractionDigits: 1, maximumFractionDigits: 1 },
  percent: { style: 'percent', maximumFractionDigits: 1 },
  currency_usd: { style: 'currency', currency: 'USD', maximumFractionDigits: 0 },
  currency_vnd: { style: 'currency', currency: 'VND' },
} as const;

export const i18n = createI18n<[MessageSchema], AppLocale, false>({
  legacy: false,
  globalInjection: true,
  locale: initialLocale(),
  fallbackLocale: 'vi',
  missingWarn: import.meta.env.DEV,
  fallbackWarn: false,
  messages: { vi, en },
  datetimeFormats: { vi: dateFormats, en: dateFormats },
  numberFormats: { vi: numberFormats, en: numberFormats },
});

export function currentLocale(): AppLocale {
  return i18n.global.locale.value;
}

export function setLocale(locale: AppLocale) {
  i18n.global.locale.value = locale;
  document.documentElement.lang = locale;
  try {
    localStorage.setItem(LOCALE_STORAGE_KEY, locale);
  } catch {
    // bỏ qua nếu không ghi được
  }
}

document.documentElement.lang = initialLocale();
