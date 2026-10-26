import { i18n } from '@/i18n';
import type { ScreenKey } from '@/app/screens';

/** Tiêu đề màn: `<screen_key>.title_page` (mọi màn trong shared/screens.json bắt buộc có key này). */
export function screenTitle(screen: ScreenKey): string {
  return i18n.global.t(`${screen}.title_page`);
}
