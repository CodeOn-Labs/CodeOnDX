<script setup lang="ts">
// Port AssignRoleUsersModal.tsx: chọn người dùng để gán vào vai trò + danh sách thành viên hiện có (gỡ được).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Loader2, ShieldAlert, Trash2, UserPlus } from '@lucide/vue';
import BaseDialog from '@/shared/components/BaseDialog.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import { systemApi, type Role } from '@/modules/admin/api/system.api';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ role: Role | null }>();
const emit = defineEmits<{ close: [] }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const open = computed({ get: () => props.role !== null, set: (v) => !v && emit('close') });
const roleId = computed(() => props.role?.id ?? '');
const members = useQuery({ queryKey: computed(() => ['roles', 'users', roleId.value] as const), queryFn: ({ signal }) => systemApi.roleUsers(roleId.value, signal), enabled: open });
const users = useQuery({ queryKey: ['users'], queryFn: ({ signal }) => systemApi.users(signal), enabled: open });
const selected = ref<string | null>(null);
const assigning = ref(false);
watch(open, () => (selected.value = null));

const options = computed(() =>
  (users.data.value ?? [])
    .filter((u) => !(members.data.value ?? []).some((m) => m.id === u.id))
    .map((u) => ({ value: u.id, label: `${u.fullName || u.email} (${u.email})` })),
);
const initials = (name: string) => {
  const parts = name.trim().split(/\s+/);
  const first = parts[0] ?? '';
  const last = parts[parts.length - 1] ?? '';
  return parts.length === 1 ? first.slice(0, 2).toUpperCase() : (first.charAt(0) + last.charAt(0)).toUpperCase();
};
function refresh() {
  void queryClient.invalidateQueries({ queryKey: ['roles'] });
}
async function assign() {
  if (!selected.value || !props.role) return;
  assigning.value = true;
  try {
    toast.success((await systemApi.assignUser(props.role.id, selected.value)).messageText);
    selected.value = null;
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    assigning.value = false;
  }
}
async function removeUser(userId: string) {
  if (!props.role) return;
  const ok = await confirm.ask({ title: t('admin_role_list.confirm_remove_user_title'), description: t('admin_role_list.confirm_remove_user_desc'), confirmText: t('admin_role_list.btn_confirm_remove'), type: 'warning' });
  if (!ok) return;
  try {
    toast.success((await systemApi.removeUser(props.role.id, userId)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
</script>

<template>
  <BaseDialog v-model:open="open" :title="$t('admin_role_list.title_assign_dialog')" title-class="!text-[19px] !font-black" max-width="md" body-class="px-6 py-5">
    <template #description>
      {{ $t('admin_role_list.lbl_current_role') }}
      <span class="text-indigo-600 font-extrabold bg-indigo-50 px-1.5 py-0.5 rounded-md text-[11px] ml-1.5 border border-indigo-100/50">{{ props.role?.name }}</span>
    </template>
    <div class="space-y-6">
      <div class="space-y-3">
        <div class="flex items-center gap-2">
          <div class="w-5 h-5 rounded-md bg-indigo-50 flex items-center justify-center shrink-0"><span class="text-[10px] font-black text-indigo-600">1</span></div>
          <span class="text-[11px] font-black text-slate-500 uppercase tracking-widest">{{ $t('admin_role_list.lbl_select_member') }}</span>
        </div>
        <div class="rounded-2xl border border-slate-100 bg-slate-50/30 p-4 space-y-4">
          <FormSelect v-model="selected" :label="$t('admin_role_list.lbl_user_name_or_email')" :placeholder="$t('admin_role_list.ph_search_user')" :options="options" searchable hide-message />
          <button
            type="button"
            :disabled="!selected || assigning"
            :class="cn(
              'group/btn relative flex w-full items-center justify-center gap-2 rounded-xl py-3 text-[12.5px] font-black tracking-wide text-white transition-all duration-300 active:scale-[0.98]',
              selected && !assigning
                ? 'bg-gradient-to-r from-violet-600 to-indigo-600 shadow-[0_10px_20px_rgba(99,102,241,0.25)] hover:from-violet-500 hover:to-indigo-500 cursor-pointer'
                : 'bg-slate-100 text-slate-400 cursor-not-allowed border border-slate-200',
            )"
            @click="assign"
          >
            <Loader2 v-if="assigning" :size="16" class="animate-spin" />
            <UserPlus v-else :size="16" class="transition-transform duration-300 group-hover/btn:scale-110" />
            <span>{{ assigning ? $t('admin_role_list.lbl_assigning') : $t('admin_role_list.btn_assign_to_role') }}</span>
          </button>
        </div>
      </div>

      <div class="space-y-3">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <div class="w-5 h-5 rounded-md bg-indigo-50 flex items-center justify-center shrink-0"><span class="text-[10px] font-black text-indigo-600">2</span></div>
            <span class="text-[11px] font-black text-slate-500 uppercase tracking-widest">{{ $t('admin_role_list.lbl_member_list') }}</span>
          </div>
          <span class="rounded-full bg-slate-100 px-2.5 py-0.5 text-[11px] font-extrabold text-slate-600 shadow-sm border border-slate-200/50">
            {{ $t('admin_role_list.lbl_member_count', { count: members.data.value?.length ?? 0 }) }}
          </span>
        </div>
        <div class="max-h-[220px] min-h-[100px] rounded-2xl border border-slate-100 bg-slate-50/20 p-3 overflow-y-auto space-y-2 shadow-inner shadow-slate-100">
          <TransitionGroup enter-active-class="transition duration-200" enter-from-class="opacity-0 translate-y-3 scale-95" leave-active-class="transition duration-150" leave-to-class="opacity-0 scale-90">
            <div
              v-for="user in members.data.value ?? []"
              :key="user.id"
              class="group/user-item flex items-center justify-between rounded-xl border border-slate-50 bg-white p-2.5 transition-all duration-300 hover:border-slate-200/80 hover:shadow-md hover:shadow-slate-100/50"
            >
              <div class="flex items-center gap-3 min-w-0">
                <div class="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500/10 to-indigo-600/10 font-black text-indigo-600 border border-indigo-100/40 text-[11px]">
                  {{ initials(user.fullName || user.email) }}
                </div>
                <div class="flex flex-col gap-0.5 min-w-0">
                  <div class="text-[12px] font-black text-slate-700 truncate leading-snug">{{ user.fullName }}</div>
                  <div class="text-[10px] font-bold text-slate-400 truncate leading-none">{{ user.email }}</div>
                </div>
              </div>
              <button
                type="button"
                class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-transparent text-slate-400 transition-all duration-300 hover:border-red-100 hover:bg-red-50 hover:text-red-500 active:scale-90 cursor-pointer"
                :title="$t('admin_role_list.btn_remove_from_role')"
                @click="removeUser(user.id)"
              >
                <Trash2 :size="14" class="transition-transform duration-300 group-hover/user-item:rotate-6" />
              </button>
            </div>
          </TransitionGroup>
          <div v-if="(members.data.value ?? []).length === 0 && !members.isPending.value" class="flex flex-col items-center justify-center py-6 text-center">
            <div class="flex h-10 w-10 items-center justify-center rounded-2xl bg-slate-50 text-slate-400 border border-slate-100 mb-2"><ShieldAlert :size="20" /></div>
            <span class="text-[11.5px] font-bold text-slate-400">{{ $t('admin_role_list.empty_no_member') }}</span>
            <span class="text-[10px] text-slate-400/80 mt-0.5">{{ $t('admin_role_list.hint_add_member') }}</span>
          </div>
        </div>
      </div>
    </div>
  </BaseDialog>
</template>
