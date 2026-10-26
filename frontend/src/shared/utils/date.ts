import { format, isValid, parseISO } from 'date-fns';
import { enUS, vi } from 'date-fns/locale';
import type { Locale } from 'date-fns';
import { currentLocale } from '@/i18n';

/** Locale date-fns theo ngôn ngữ đang chọn (thay dateLocale của LocaleProvider bản React). */
export function dateFnsLocale(): Locale {
  return currentLocale() === 'en' ? enUS : vi;
}

/** 'yyyy-MM-dd' | ISO → Date (undefined nếu rỗng hoặc sai định dạng). */
export function parseDate(value?: string | null): Date | undefined {
  if (!value) return undefined;
  const d = parseISO(value);
  return isValid(d) ? d : undefined;
}

/** Date → 'yyyy-MM-dd' — định dạng ngày gửi lên API. */
export function toIsoDate(date: Date): string {
  return format(date, 'yyyy-MM-dd');
}

/** Hiển thị ngày theo mẫu date-fns, mặc định dd/MM/yyyy như bản gốc. Trả '' nếu không có ngày hợp lệ. */
export function formatDate(value: string | Date | null | undefined, pattern = 'dd/MM/yyyy'): string {
  const d = value instanceof Date ? value : parseDate(value);
  return d && isValid(d) ? format(d, pattern, { locale: dateFnsLocale() }) : '';
}
