// Màu của danh mục (master data): tên preset hoặc mã HEX. Port PRESET_HEX_MAP / getBadgeStyles của CategoryTable.tsx.

export const PRESET_COLORS = ['slate', 'blue', 'indigo', 'violet', 'amber', 'emerald', 'rose', 'red'] as const;

const PRESET_HEX: Record<string, string> = {
  slate: '#64748b',
  blue: '#2563eb',
  indigo: '#4f46e5',
  violet: '#7c3aed',
  pr: '#7c3aed',
  amber: '#d97706',
  rose: '#e11d48',
  emerald: '#059669',
  green: '#059669',
  teal: '#0d9488',
  red: '#dc2626',
};

const FALLBACK = '#64748b';

/** 'violet' → '#7c3aed'; '#AABBCC' giữ nguyên; giá trị lạ → xám. */
export function resolveColor(color: string | null | undefined): string {
  if (!color) return FALLBACK;
  return PRESET_HEX[color] ?? (/^#[0-9a-fA-F]{6}$/.test(color) ? color : FALLBACK);
}

/** Style huy hiệu nhạt (nền ~4%, viền trong ~10%) như bản gốc. */
export function badgeStyle(color: string | null | undefined) {
  const hex = resolveColor(color);
  return { backgroundColor: `${hex}0a`, color: hex, boxShadow: `inset 0 0 0 1px ${hex}18` };
}
