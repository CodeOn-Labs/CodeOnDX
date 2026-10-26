import screens from '@shared/screens.json';

type ScreensJson = typeof screens;

export type ScreenKey = Exclude<keyof ScreensJson, '$comment'>;

/** Màn có đường dẫn riêng (khai báo `route`) — router.ts bắt buộc có component cho từng màn này. */
export type RoutedScreenKey = { [K in ScreenKey]: ScreensJson[K] extends { route: string } ? K : never }[ScreenKey];

export interface ScreenRoute {
  route: string;
  /** Bỏ trống = dùng permission của màn */
  permission?: string[];
}

export interface ScreenMeta {
  route?: string;
  public?: boolean;
  /** Đã đăng nhập thì không vào được (đăng nhập, quên mật khẩu) */
  guest_only?: boolean;
  parent?: ScreenKey;
  permission?: string[];
  /** Đường dẫn phụ hiển thị cùng màn (vd `/admin/roles/new` bên cạnh `/admin/roles/:id`) */
  extra_routes?: ScreenRoute[];
  /** false = màn con không kế thừa Insight của màn cha (ngoại lệ tab Quản trị dự án) */
  inherit_insights?: boolean;
}

export const SCREENS = screens as unknown as Record<ScreenKey, ScreenMeta>;

export const SCREEN_KEYS = Object.keys(SCREENS).filter((key) => !key.startsWith('$')) as ScreenKey[];

/** Mọi đường dẫn của một màn (đường dẫn chính trước), mỗi đường dẫn kèm quyền đã áp mặc định của màn. */
export function screenRoutes(screen: ScreenKey): Array<{ route: string; permission?: string[] }> {
  const meta = SCREENS[screen];
  if (!meta.route) return [];
  return [{ route: meta.route }, ...(meta.extra_routes ?? [])].map((r) => ({ route: r.route, permission: r.permission ?? meta.permission }));
}

/**
 * Chuỗi màn áp dụng Insight theo ADR-0001: màn hiện tại trước, rồi lần lượt các màn cha
 * (dừng khi gặp màn khai báo inherit_insights = false).
 */
export function insightScreenChain(screen: ScreenKey): ScreenKey[] {
  const chain: ScreenKey[] = [screen];
  let current = SCREENS[screen];
  while (current?.parent && current.inherit_insights !== false) {
    chain.push(current.parent);
    current = SCREENS[current.parent];
  }
  return chain;
}
