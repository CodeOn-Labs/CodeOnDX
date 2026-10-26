// Port nguyên shared/utils/avatar.ts của bản gốc: chữ cái đầu + màu ổn định theo tên/id.
// Google Profile Avatar background colors list (solid, for large profile avatars)
export const GOOGLE_AVATAR_COLORS = [
  '#AA47BD', // Deep Orchid
  '#7B1FA2', // Royal Purple
  '#77919D', // Slate Gray
  '#455A65', // Charcoal Blue
  '#EC417A', // Neon Pink
  '#C1175C', // Crimson Rose
  '#5D6AC0', // Indigo Blue
  '#0388D2', // Sky Blue
  '#00579B', // Navy
  '#0098A7', // Teal Blue
  '#00897B', // Deep Teal
  '#004D40', // Forest Green
  '#68A039', // Leaf Green
  '#34691E', // Olive Green
  '#8C6E63', // Taupe Brown
];

/**
 * Pastel avatar palette (light background + dark text + border) for table row avatars.
 * Returns Tailwind CSS class string instead of hex color.
 */
export const PASTEL_AVATAR_COLORS = [
  'bg-blue-100 text-blue-700 border-blue-200',
  'bg-indigo-100 text-indigo-700 border-indigo-200',
  'bg-violet-100 text-violet-700 border-violet-200',
  'bg-fuchsia-100 text-fuchsia-700 border-fuchsia-200',
  'bg-rose-100 text-rose-700 border-rose-200',
  'bg-orange-100 text-orange-700 border-orange-200',
  'bg-emerald-100 text-emerald-700 border-emerald-200',
  'bg-cyan-100 text-cyan-700 border-cyan-200',
];

function hashName(key: string): number {
  let hash = 0;
  for (let i = 0; i < key.length; i++) {
    hash = key.charCodeAt(i) + ((hash << 5) - hash);
  }
  return Math.abs(hash);
}

/**
 * Deterministically generates avatar initials and a solid background color matching Google style.
 * Use for large profile avatars and compact slot badges (e.g. EmployeeDetailPage, AssignmentTable).
 * @param name The display name of the user
 * @param keyId Optional unique identifier (e.g. employee ID) to make the hash even more stable
 */
export function getGoogleAvatarProps(name: string, keyId?: string) {
  const trimmed = name.trim();
  if (!trimmed) {
    return {
      initial: 'U',
      backgroundColor: '#77919D',
    };
  }

  const initial = trimmed.charAt(0).toUpperCase();
  const key = keyId || trimmed;
  const colorIndex = hashName(key) % GOOGLE_AVATAR_COLORS.length;
  const backgroundColor = GOOGLE_AVATAR_COLORS[colorIndex];

  return {
    initial,
    backgroundColor,
  };
}

/**
 * Deterministically generates avatar initials and a pastel Tailwind CSS class string.
 * Use for table row avatars where a softer, light-background style is preferred (e.g. EmployeeTable).
 * @param name The display name of the user
 * @param keyId Optional unique identifier to stabilise the hash
 */
export function getPastelAvatarProps(name: string, keyId?: string) {
  const trimmed = name?.trim() ?? '';
  if (!trimmed) {
    return {
      initial: 'U',
      colorClass: PASTEL_AVATAR_COLORS[0],
    };
  }

  const initial = trimmed.charAt(0).toUpperCase();
  const key = keyId || trimmed;
  const colorClass = PASTEL_AVATAR_COLORS[hashName(key) % PASTEL_AVATAR_COLORS.length];

  return {
    initial,
    colorClass,
  };
}
