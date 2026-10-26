<script setup lang="ts">
// Port RoleDetailPage.tsx + RoleDetailHeader.tsx: xem / sửa / tạo vai trò — tên, mô tả, quyền theo cụm.
// Vai trò mặc định không đổi tên được; sửa quyền của vai trò đã có phải xác nhận (ghi đè toàn bộ quyền).
import { computed, ref, watch } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery } from '@tanstack/vue-query';
import { ArrowLeft, Check, Edit3, X } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import Checkbox from '@/shared/components/form/Checkbox.vue';
import { systemApi, type RoleInput } from '@/modules/admin/api/system.api';
import { areStringSetsEqual, diffPermissionCounts, groupPermissions } from '@/modules/admin/utils/role-permissions';
import { enumLabel } from '@/shared/i18n/enum-label';
import { useFormMutation } from '@/shared/composables/useFormMutation';
import { useConfirmStore } from '@/stores/confirm';
import { cn } from '@/shared/utils/cn';

const route = useRoute();
const router = useRouter();
const { t, locale } = useI18n();
const confirm = useConfirmStore();

const id = computed(() => String(route.params.id ?? 'new'));
const isNew = computed(() => id.value === 'new' || route.path.endsWith('/new'));
const editing = computed(() => isNew.value || route.query.mode === 'edit');

const permissions = useQuery({ queryKey: computed(() => ['permissions', locale.value] as const), queryFn: systemApi.permissions });
const roleQuery = useQuery({
  queryKey: computed(() => ['roles', 'detail', id.value, locale.value] as const),
  queryFn: ({ signal }) => systemApi.role(id.value, signal),
  enabled: computed(() => !isNew.value),
});
const role = computed(() => roleQuery.data.value);
const clusters = computed(() => groupPermissions(permissions.data.value ?? []));

const form = ref<RoleInput>({ name: '', description: '', permissions: [] });
const errors = ref<Record<string, string>>({});
watch(role, (r) => {
  if (r) form.value = { name: r.name, description: r.description ?? '', permissions: [...r.permissions] };
}, { immediate: true });

const dirty = computed(() => {
  const base = role.value;
  return (
    form.value.name !== (base?.name ?? '') ||
    form.value.description !== (base?.description ?? '') ||
    !areStringSetsEqual(form.value.permissions, base?.permissions ?? [])
  );
});

function toggle(key: string, checked: boolean) {
  form.value.permissions = checked ? [...new Set([...form.value.permissions, key])] : form.value.permissions.filter((k) => k !== key);
}

const save = useFormMutation({
  mutationFn: (input: RoleInput) => (isNew.value ? systemApi.createRole(input) : systemApi.updateRole(id.value, input)),
  invalidate: [['roles']],
  onSuccess: () => void router.push('/admin/roles'),
});
watch(save.fieldErrors, (v) => (errors.value = { ...v }));

async function submit() {
  const e: Record<string, string> = {};
  if (!form.value.name.trim()) e.name = t('validation.required');
  if (form.value.permissions.length === 0) e.permissions = t('admin_role_form.err_no_permission');
  errors.value = e;
  if (Object.keys(e).length) return;
  if (!isNew.value && role.value) {
    const d = diffPermissionCounts(role.value.permissions, form.value.permissions);
    const ok = await confirm.ask({
      title: t('admin_role_form.confirm_save_title'),
      description: t('admin_role_form.confirm_save_desc', { name: form.value.name, added: d.added, removed: d.removed }),
      confirmText: t('admin_role_form.btn_confirm_save'),
      type: 'warning',
    });
    if (!ok) return;
  }
  save.mutate({ ...form.value, name: form.value.name.trim() });
}

async function cancel() {
  if (editing.value && dirty.value) {
    const ok = await confirm.ask({ title: t('admin_role_form.confirm_discard_title'), description: t('admin_role_form.confirm_discard_desc'), confirmText: t('admin_role_form.btn_discard'), type: 'warning' });
    if (!ok) return;
  }
  void router.push('/admin/roles');
}
const title = computed(() => (isNew.value ? t('admin_role_form.title_create') : form.value.name || role.value?.name || ''));
</script>

<template>
  <PageLayout layout="C" ai-module="admin" content-padding="24px" disable-scroll chat-width="300px">
    <div v-if="permissions.isPending.value || (!isNew && roleQuery.isPending.value)" class="flex h-full items-center justify-center py-20">
      <div class="animate-pulse text-[13px] font-medium text-slate-400">{{ $t('admin_role_form.lbl_loading') }}</div>
    </div>

    <div v-else class="flex h-full flex-col overflow-hidden">
      <div class="z-40 -mx-6 -mt-6 mb-4 border-b border-slate-100 bg-white/80 px-6 pb-3 pt-4 backdrop-blur-xl">
        <div class="shrink-0 flex flex-col gap-1 animate-in fade-in slide-in-from-top-4 duration-500">
          <div class="flex items-center gap-2 text-[11px] font-bold uppercase tracking-widest text-slate-400 mb-1">
            <RouterLink to="/admin/roles" class="hover:text-pr transition-colors flex items-center gap-1 group">
              <ArrowLeft :size="14" class="group-hover:-translate-x-0.5 transition-transform" />
              {{ $t('admin_role_form.lbl_breadcrumb') }}
            </RouterLink>
            <span class="opacity-40">/</span>
            <span class="text-slate-600">{{ title }}</span>
          </div>
          <div class="flex items-center justify-between">
            <div class="shrink-0 flex flex-col gap-0.5">
              <div class="flex items-center gap-3">
                <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent m-0 p-0">{{ title }}</h1>
                <div
                  v-if="!isNew"
                  :class="cn('inline-flex h-6 items-center justify-center rounded-[4px] px-2 text-[10px] font-black uppercase tracking-wider shadow-sm shrink-0', role?.isSystem ? 'bg-slate-100 text-slate-700' : 'bg-violet-50 text-violet-700')"
                >
                  {{ role?.isSystem ? $t('admin_role_form.lbl_type_system') : $t('admin_role_form.lbl_type_custom') }}
                </div>
              </div>
              <div v-if="form.description" class="flex items-center gap-3 text-[11px] font-bold text-slate-500 tracking-wide mt-1">
                <span class="text-slate-700">{{ form.description }}</span>
              </div>
            </div>
            <Button
              v-if="!editing"
              variant="primary"
              class="h-10 rounded-xl px-6 text-[14px] font-bold shadow-lg shadow-pr/25"
              @click="router.replace({ query: { mode: 'edit' } })"
            >
              <template #leftIcon><Edit3 :size="16" /></template>
              {{ $t('admin_role_form.btn_edit') }}
            </Button>
          </div>
        </div>
      </div>

      <form class="flex flex-1 flex-col min-h-0" novalidate @submit.prevent="submit">
        <div class="flex-1 overflow-y-auto pr-1 flex flex-col gap-4">
          <div class="grid grid-cols-1 gap-4 md:grid-cols-2">
            <FormItem :label="$t('admin_role_form.lbl_name')" required :error="errors.name" :description="role?.isSystem ? $t('admin_role_form.hint_system_name') : undefined">
              <BaseInput v-model="form.name" :placeholder="$t('admin_role_form.ph_name')" :disabled="!editing || role?.isSystem" :error="!!errors.name" />
            </FormItem>
            <FormItem :label="$t('admin_role_form.lbl_description')" :error="errors.description">
              <BaseInput v-model="form.description" :placeholder="$t('admin_role_form.ph_description')" :disabled="!editing" />
            </FormItem>
          </div>

          <div class="mt-2 text-[16px] font-black tracking-tight text-slate-800">{{ $t('admin_role_form.title_permissions') }}</div>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-7">
            <div v-for="cluster in clusters" :key="cluster.group" class="flex flex-col gap-2.5 rounded-xl border border-slate-200/80 bg-white p-4 shadow-sm transition-all hover:shadow-md">
              <div class="border-b border-slate-100 pb-2 text-[12px] font-black uppercase tracking-wider text-pr">{{ enumLabel('permission_group', cluster.group, cluster.group) }}</div>
              <div class="flex flex-col gap-2">
                <label v-for="p in cluster.permissions" :key="p.key" :class="cn('flex items-start gap-2', editing ? 'cursor-pointer' : 'cursor-default')" :title="p.name">
                  <Checkbox :model-value="form.permissions.includes(p.key)" :disabled="!editing" class="mt-0.5" @update:model-value="toggle(p.key, $event)" />
                  <span class="flex flex-col">
                    <span class="text-[12px] font-medium text-slate-700 hover:text-slate-900 transition-colors">{{ p.key }}</span>
                    <span class="text-[10px] text-slate-400">{{ p.name }}</span>
                  </span>
                </label>
              </div>
            </div>
          </div>
          <p v-if="errors.permissions" class="text-[12px] font-bold text-red-500">{{ errors.permissions }}</p>
        </div>

        <div class="flex items-center justify-end gap-3 border-t border-slate-100 pt-4 mt-2 bg-white shrink-0 z-10">
          <Button variant="danger" class="min-w-[120px] shadow-sm" @click="cancel">
            <template #leftIcon><X :size="16" /></template>
            {{ $t('common.btn_cancel') }}
          </Button>
          <Button v-if="editing" type="submit" variant="primary" class="min-w-[160px] shadow-lg shadow-pr/25 px-6" :loading="save.isPending.value" :disabled="!dirty">
            {{ $t('admin_role_form.btn_save') }}
            <template #rightIcon><Check :size="16" /></template>
          </Button>
        </div>
      </form>
    </div>
  </PageLayout>
</template>
