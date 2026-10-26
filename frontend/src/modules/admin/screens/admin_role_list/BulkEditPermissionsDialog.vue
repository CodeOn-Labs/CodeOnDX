<script setup lang="ts">
// Port BulkEditRolePermissionsModal.tsx + BulkPermissionMatrix.tsx: ma trận vai trò × quyền, ô đổi được đánh dấu,
// chọn/bỏ tất cả theo vai trò, cảnh báo vai trò không còn quyền nào, xác nhận tóm tắt thay đổi trước khi lưu.
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { AlertTriangle, Check, X } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import Button from '@/shared/components/Button.vue';
import Checkbox from '@/shared/components/form/Checkbox.vue';
import { EMPTY_ROLE_FILTERS, systemApi } from '@/modules/admin/api/system.api';
import {
  buildMatrixState, diffPermissionCounts, getDirtyRoleIds, getRoleSelectionState, groupPermissions, isCellDirty, toggleAllForRole, toggleCell, type PermissionMap,
} from '@/modules/admin/utils/role-permissions';
import { enumLabel } from '@/shared/i18n/enum-label';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const open = defineModel<boolean>('open', { required: true });
const { t, locale } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const roles = useQuery({ queryKey: computed(() => ['roles', 'all', locale.value] as const), queryFn: () => systemApi.roles(EMPTY_ROLE_FILTERS, 1, 0).then((r) => r.items), enabled: open });
const permissions = useQuery({ queryKey: computed(() => ['permissions', locale.value] as const), queryFn: systemApi.permissions, enabled: open });
const clusters = computed(() => groupPermissions(permissions.data.value ?? []));
const allKeys = computed(() => clusters.value.flatMap((c) => c.permissions.map((p) => p.key)));
const roleList = computed(() => roles.data.value ?? []);

const initial = ref<PermissionMap>({});
const value = ref<PermissionMap>({});
const saving = ref(false);
watch([open, () => roles.data.value], ([isOpen, data]) => {
  if (isOpen && data) {
    initial.value = buildMatrixState(data);
    value.value = buildMatrixState(data);
  }
});

const dirtyIds = computed(() => getDirtyRoleIds(initial.value, value.value));
const emptyRoleIds = computed(() => dirtyIds.value.filter((id) => (value.value[id] ?? []).length === 0));
const emptyNames = computed(() => roleList.value.filter((r) => emptyRoleIds.value.includes(r.id)).map((r) => r.name));

async function close() {
  if (dirtyIds.value.length > 0) {
    const ok = await confirm.ask({ title: t('admin_role_list.confirm_discard_title'), description: t('admin_role_list.confirm_discard_desc'), confirmText: t('admin_role_list.btn_discard'), type: 'warning' });
    if (!ok) return;
  }
  open.value = false;
}

async function save() {
  if (emptyRoleIds.value.length > 0 || dirtyIds.value.length === 0) return;
  const lines = roleList.value
    .filter((r) => dirtyIds.value.includes(r.id))
    .map((r) => {
      const d = diffPermissionCounts(initial.value[r.id] ?? [], value.value[r.id] ?? []);
      return t('admin_role_list.lbl_change_line', { name: r.name, added: d.added, removed: d.removed });
    });
  const ok = await confirm.ask({
    title: t('admin_role_list.confirm_save_title'),
    description: `${t('admin_role_list.confirm_save_intro', { count: dirtyIds.value.length })}\n${lines.join('\n')}`,
    confirmText: t('admin_role_list.btn_confirm_save'),
    type: 'warning',
  });
  if (!ok) return;
  saving.value = true;
  try {
    const res = await systemApi.bulkUpdateRoles(dirtyIds.value.map((roleId) => ({ roleId, permissions: value.value[roleId] ?? [] })));
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['roles'] });
    open.value = false;
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    saving.value = false;
  }
}

const HEADER_CELL = 'border-b border-slate-200 py-2.5';
const FIRST_COLUMN_EDGE = 'border-r border-slate-200 shadow-[2px_0_4px_-2px_rgba(15,23,42,0.08)]';
const CLUSTER_ROW_CELL = 'border-y border-slate-200 bg-slate-50 px-3 py-2';
const BODY_CELL = 'border-b border-slate-100 py-2';
</script>

<template>
  <BaseDialog :open="open" :title="$t('admin_role_list.title_bulk_dialog')" :description="$t('admin_role_list.hint_bulk_dialog')" max-width="4xl" @update:open="!$event && close()">
    <div v-if="roles.isPending.value || permissions.isPending.value" class="py-16 text-center text-[13px] text-slate-400 animate-pulse">{{ $t('admin_role_list.lbl_loading') }}</div>
    <div v-else class="flex flex-col gap-3">
      <div v-if="emptyNames.length" class="flex items-start gap-2 rounded-xl border border-rose-100 bg-rose-50 px-3 py-2 text-[12px] font-semibold text-rose-600">
        <AlertTriangle :size="14" class="mt-0.5 shrink-0" />
        {{ $t('admin_role_list.err_empty_roles', { names: emptyNames.join(', ') }) }}
      </div>
      <div class="max-h-[60vh] overflow-auto rounded-xl border border-slate-200">
        <table class="w-full border-separate border-spacing-0 text-[12.5px]">
          <thead>
            <tr>
              <th :class="cn('sticky left-0 top-0 z-30 bg-white px-3 text-left text-[10.5px] font-bold uppercase tracking-wide text-slate-400', HEADER_CELL, FIRST_COLUMN_EDGE)">
                {{ $t('admin_role_list.col_permission') }}
              </th>
              <th
                v-for="(role, index) in roleList"
                :key="role.id"
                :class="cn('sticky top-0 z-20 bg-white min-w-[100px] px-2 text-center font-bold text-slate-700', HEADER_CELL, index > 0 && 'border-l border-slate-100', emptyRoleIds.includes(role.id) && 'bg-rose-50 text-rose-600')"
              >
                <div class="flex flex-col items-center gap-1.5">
                  <div class="max-w-[160px] truncate mx-auto" :title="role.name">{{ role.name }}</div>
                  <Checkbox
                    :model-value="getRoleSelectionState(role.id, allKeys, value) === 'all'"
                    :indeterminate="getRoleSelectionState(role.id, allKeys, value) === 'some'"
                    :label="$t('admin_role_list.tip_select_all_for_role', { role: role.name })"
                    @update:model-value="value = toggleAllForRole(value, role.id, allKeys, getRoleSelectionState(role.id, allKeys, value) !== 'all')"
                  />
                </div>
              </th>
            </tr>
          </thead>
          <tbody>
            <template v-for="cluster in clusters" :key="cluster.group">
              <tr>
                <td :class="cn('sticky left-0 z-10 text-[10px] font-bold uppercase tracking-[0.08em] text-pr', CLUSTER_ROW_CELL, FIRST_COLUMN_EDGE)">
                  {{ enumLabel('permission_group', cluster.group, cluster.group) }}
                </td>
                <td :colspan="roleList.length" :class="CLUSTER_ROW_CELL" />
              </tr>
              <tr v-for="permission in cluster.permissions" :key="permission.key" class="group">
                <td :class="cn('sticky left-0 z-10 bg-white px-3 font-medium text-slate-600 transition-colors group-hover:bg-slate-50', BODY_CELL, FIRST_COLUMN_EDGE)" :title="permission.name">
                  {{ permission.key }}
                </td>
                <td
                  v-for="(role, index) in roleList"
                  :key="`${role.id}-${permission.key}`"
                  :class="cn('relative px-2 text-center transition-colors', BODY_CELL, index > 0 && 'border-l border-slate-100', isCellDirty(role.id, permission.key, initial, value) ? 'bg-prbg group-hover:bg-prbg' : 'group-hover:bg-slate-50')"
                >
                  <div class="flex justify-center">
                    <Checkbox
                      :model-value="(value[role.id] ?? []).includes(permission.key)"
                      :label="`${role.name} — ${permission.key}`"
                      @update:model-value="value = toggleCell(value, role.id, permission.key)"
                    />
                  </div>
                  <span v-if="isCellDirty(role.id, permission.key, initial, value)" class="pointer-events-none absolute right-1 top-1 h-1.5 w-1.5 rounded-full bg-pr" aria-hidden="true" />
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </div>
    <template #footer>
      <span class="mr-auto text-[12px] font-semibold text-slate-400">{{ $t('admin_role_list.lbl_dirty_count', { count: dirtyIds.length }) }}</span>
      <Button variant="danger" class="min-w-[100px]" :disabled="saving" @click="close">
        <template #leftIcon><X :size="16" /></template>
        {{ $t('common.btn_cancel') }}
      </Button>
      <Button variant="primary" class="min-w-[140px]" :loading="saving" :disabled="dirtyIds.length === 0 || emptyRoleIds.length > 0" @click="save">
        <template #leftIcon><Check :size="16" /></template>
        {{ $t('admin_role_list.btn_save_changes') }}
      </Button>
    </template>
  </BaseDialog>
</template>
