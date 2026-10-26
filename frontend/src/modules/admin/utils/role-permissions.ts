// Port utils/permissionSet.ts + utils/bulkEditMatrix.ts + groupPermissionsByCluster (roleViewModel.ts) của bản gốc.
import type { Permission, Role } from '@/modules/admin/api/system.api';

export type PermissionMap = Record<string, string[]>;
export type SelectionState = 'all' | 'some' | 'none';

export interface PermissionCluster {
  group: string;
  permissions: Permission[];
}

/** Gom quyền theo nhóm (phần trước dấu chấm của key, cột `group` trong bảng permissions), giữ thứ tự sortSeq. */
export function groupPermissions(permissions: Permission[]): PermissionCluster[] {
  const clusters = new Map<string, Permission[]>();
  for (const p of [...permissions].sort((a, b) => a.sortSeq - b.sortSeq)) {
    clusters.set(p.group, [...(clusters.get(p.group) ?? []), p]);
  }
  return [...clusters.entries()].map(([group, items]) => ({ group, permissions: items }));
}

export function areStringSetsEqual(a: string[], b: string[]): boolean {
  if (a.length !== b.length) return false;
  const set = new Set(a);
  return b.every((x) => set.has(x));
}

export function diffPermissionCounts(before: string[], after: string[]) {
  const b = new Set(before);
  const a = new Set(after);
  return { added: after.filter((x) => !b.has(x)).length, removed: before.filter((x) => !a.has(x)).length };
}

export function buildMatrixState(roles: Role[]): PermissionMap {
  return Object.fromEntries(roles.map((r) => [r.id, [...r.permissions]]));
}

export function getDirtyRoleIds(initial: PermissionMap, current: PermissionMap): string[] {
  const ids = new Set([...Object.keys(initial), ...Object.keys(current)]);
  return [...ids].filter((id) => !areStringSetsEqual(initial[id] ?? [], current[id] ?? []));
}

export function getRoleSelectionState(roleId: string, keys: string[], current: PermissionMap): SelectionState {
  if (keys.length === 0) return 'none';
  const set = new Set(current[roleId] ?? []);
  const count = keys.filter((k) => set.has(k)).length;
  return count === 0 ? 'none' : count === keys.length ? 'all' : 'some';
}

export function isCellDirty(roleId: string, key: string, initial: PermissionMap, current: PermissionMap): boolean {
  return (initial[roleId] ?? []).includes(key) !== (current[roleId] ?? []).includes(key);
}

export function toggleCell(current: PermissionMap, roleId: string, key: string): PermissionMap {
  const list = current[roleId] ?? [];
  return { ...current, [roleId]: list.includes(key) ? list.filter((k) => k !== key) : [...list, key] };
}

export function toggleAllForRole(current: PermissionMap, roleId: string, keys: string[], checked: boolean): PermissionMap {
  if (checked) return { ...current, [roleId]: [...new Set([...(current[roleId] ?? []), ...keys])] };
  const drop = new Set(keys);
  return { ...current, [roleId]: (current[roleId] ?? []).filter((k) => !drop.has(k)) };
}
