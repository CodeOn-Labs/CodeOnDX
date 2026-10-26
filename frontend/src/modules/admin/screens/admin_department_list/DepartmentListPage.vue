<script setup lang="ts">
// Màn mới (D4): Quản trị → Phòng ban & Team. Bản gốc chỉ đọc phòng ban/team từ hệ thống ngoài
// (service admin-departments-teams dùng trong bộ lọc nhân sự/effort) nên không có giao diện để chuyển nguyên —
// bố cục theo phong cách thẻ của các màn quản trị khác (PcvCriteriaPage, SettingsPage).
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Building2, ChevronDown, Crown, Network, Pencil, Plus, Search, Trash2, UserRound, Users } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import EmptyState from '@/shared/components/EmptyState.vue';
import StatSegment from '@/shared/components/StatSegment.vue';
import OrgUnitDialog from './OrgUnitDialog.vue';
import { EMPTY_EMPLOYEE_FILTERS, orgApi, type Department, type Team } from '@/modules/org/api/org.api';
import { useAuthStore } from '@/stores/auth';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { useUiStore } from '@/stores/ui';
import { cn } from '@/shared/utils/cn';

const { t } = useI18n();
const router = useRouter();
const auth = useAuthStore();
const ui = useUiStore();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();
const canManage = computed(() => auth.can('employee.manage'));

const query = useQuery({ queryKey: ['departments'], queryFn: orgApi.departments });
const search = ref('');
const collapsed = ref<Set<string>>(new Set());

const departments = computed(() => {
  const q = search.value.trim().toLowerCase();
  const all = query.data.value ?? [];
  if (!q) return all;
  return all
    .map((d) => {
      const selfMatch = [d.name, d.code].some((v) => v.toLowerCase().includes(q));
      const teams = d.teams.filter((tm) => [tm.name, tm.code].some((v) => v.toLowerCase().includes(q)));
      return selfMatch ? d : teams.length ? { ...d, teams } : null;
    })
    .filter((d): d is Department => d !== null);
});
const stats = computed(() => {
  const all = query.data.value ?? [];
  return {
    departments: all.length,
    teams: all.reduce((s, d) => s + d.teams.length, 0),
    members: all.reduce((s, d) => s + d.memberCount, 0),
    noLeader: all.reduce((s, d) => s + d.teams.filter((tm) => !tm.leader).length + (d.manager ? 0 : 1), 0),
  };
});

function toggle(id: string) {
  const next = new Set(collapsed.value);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  collapsed.value = next;
}

// ─── Thêm / sửa / xoá ───────────────────────────────────────────────────────
const dialog = ref<{ open: boolean; kind: 'department' | 'team'; department: Department | null; team: Team | null; parentId: string | null }>({
  open: false,
  kind: 'department',
  department: null,
  team: null,
  parentId: null,
});
function openDepartment(department: Department | null) {
  dialog.value = { open: true, kind: 'department', department, team: null, parentId: null };
}
function openTeam(parentId: string, team: Team | null) {
  dialog.value = { open: true, kind: 'team', department: null, team, parentId };
}

async function remove(kind: 'department' | 'team', item: Department | Team) {
  const ok = await confirm.ask({
    title: kind === 'department' ? t('admin_department_list.confirm_delete_department_title') : t('admin_department_list.confirm_delete_team_title'),
    description: t('admin_department_list.confirm_delete_desc', { name: item.name }),
    confirmText: t('admin_department_list.btn_confirm_delete'),
    type: 'danger',
  });
  if (!ok) return;
  try {
    const res = kind === 'department' ? await orgApi.deleteDepartment(item.id) : await orgApi.deleteTeam(item.id);
    toast.success(res.messageText);
    void queryClient.invalidateQueries({ queryKey: ['departments'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}

/** Mở danh sách nhân sự đã lọc theo phòng ban / team. */
function viewMembers(departmentId: string, teamId?: string) {
  ui.setFilters('employee_list', {
    filters: { ...EMPTY_EMPLOYEE_FILTERS, departmentIds: teamId ? [] : [departmentId], teamIds: teamId ? [teamId] : [] },
    pageSize: 20,
  });
  void router.push('/admin/employees');
}
</script>

<template>
  <PageLayout layout="C" content-padding="24px" disable-scroll>
    <div class="h-full flex flex-col min-h-0 gap-3">
      <div class="flex flex-col gap-3 rounded-2xl bg-white/60 backdrop-blur-xl p-3 lg:p-4 shadow-xl shadow-slate-200/40 border border-white/60">
        <div class="flex items-center justify-between gap-3 flex-wrap">
          <div class="flex flex-col gap-1">
            <div class="flex items-center gap-2">
              <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
              <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ $t('admin_department_list.lbl_kicker') }}</span>
            </div>
            <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">{{ $t('admin_department_list.title_page') }}</h1>
          </div>
          <div class="flex items-center gap-3">
            <div class="group relative flex items-center">
              <Search :size="16" class="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 group-focus-within:text-pr" />
              <input
                v-model="search"
                type="text"
                :placeholder="$t('admin_department_list.ph_search')"
                :aria-label="$t('admin_department_list.ph_search')"
                class="h-9 lg:h-10 w-64 rounded-xl border border-slate-200 bg-white pl-10 pr-4 text-[13.5px] font-medium text-slate-900 outline-none transition-all placeholder:text-slate-400 focus:border-pr focus:ring-4 focus:ring-pr/10"
              />
            </div>
            <Button v-if="canManage" variant="primary" class="h-9 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" @click="openDepartment(null)">
              <template #leftIcon><Plus :size="16" :stroke-width="3" /></template>
              {{ $t('admin_department_list.btn_create_department') }}
            </Button>
          </div>
        </div>
      </div>

      <div class="grid grid-cols-2 lg:grid-cols-4 gap-px rounded-2xl border border-white/60 bg-white/40 backdrop-blur-xl shadow-xl shadow-slate-200/40 overflow-hidden">
        <StatSegment :label="$t('admin_department_list.stat_departments')" :value="stats.departments" tone="violet" :icon="Building2" :loading="query.isPending.value" />
        <StatSegment :label="$t('admin_department_list.stat_teams')" :value="stats.teams" tone="blue" :icon="Network" :delay="0.05" :loading="query.isPending.value" />
        <StatSegment :label="$t('admin_department_list.stat_members')" :value="stats.members" tone="green" :icon="Users" :delay="0.1" :loading="query.isPending.value" />
        <StatSegment :label="$t('admin_department_list.stat_no_leader')" :value="stats.noLeader" tone="amber" :icon="Crown" :delay="0.15" :loading="query.isPending.value" />
      </div>

      <div class="flex-1 min-h-0 overflow-y-auto pr-1 space-y-3">
        <div v-if="query.isPending.value" class="space-y-3">
          <div v-for="i in 3" :key="i" class="h-28 rounded-[24px] bg-white/60 border border-slate-100 animate-pulse" />
        </div>
        <div v-else-if="departments.length === 0" class="rounded-[24px] border border-slate-200/80 bg-white/60">
          <EmptyState :title="$t('admin_department_list.empty_no_department')" compact />
        </div>

        <div
          v-for="dept in departments"
          :key="dept.id"
          class="rounded-[24px] border border-slate-200/80 bg-white/70 shadow-sm backdrop-blur-xl overflow-hidden transition-all hover:shadow-md"
        >
          <div class="flex items-center gap-4 px-5 py-4">
            <button
              type="button"
              class="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-slate-50 text-slate-400 transition-all hover:bg-pr/10 hover:text-pr"
              :aria-label="$t('admin_department_list.tip_toggle')"
              @click="toggle(dept.id)"
            >
              <ChevronDown :size="16" :class="cn('transition-transform duration-300', collapsed.has(dept.id) && '-rotate-90')" />
            </button>
            <div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-indigo-50 to-violet-100/60 text-pr border border-indigo-100">
              <Building2 :size="18" />
            </div>
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <h3 class="truncate text-[15px] font-black text-slate-900">{{ dept.name }}</h3>
                <span class="rounded-md border border-slate-100 bg-slate-50 px-1.5 py-0.5 font-mono text-[10px] font-bold tracking-wider text-slate-400">{{ dept.code }}</span>
                <span v-if="!dept.isActive" class="rounded-full bg-slate-100 px-2 py-0.5 text-[9px] font-black uppercase tracking-widest text-slate-500">{{ $t('admin_department_list.lbl_inactive') }}</span>
              </div>
              <div class="mt-1 flex items-center gap-3 text-[11px] font-bold text-slate-400">
                <span class="flex items-center gap-1.5"><UserRound :size="12" />{{ dept.manager?.fullName ?? $t('admin_department_list.lbl_no_manager') }}</span>
                <span class="h-1 w-1 rounded-full bg-slate-200" />
                <span>{{ $t('admin_department_list.lbl_team_count', { count: dept.teams.length }) }}</span>
              </div>
            </div>
            <button
              type="button"
              class="flex items-center gap-2 rounded-xl border border-slate-100 bg-white px-3 py-1.5 text-[12px] font-bold text-slate-600 transition-all hover:border-pr/30 hover:text-pr"
              @click="viewMembers(dept.id)"
            >
              <Users :size="14" />
              {{ $t('admin_department_list.lbl_member_count', { count: dept.memberCount }) }}
            </button>
            <div v-if="canManage" class="flex items-center gap-1">
              <button type="button" class="p-2 rounded-xl text-slate-400 transition-all hover:bg-pr/10 hover:text-pr" :title="$t('admin_department_list.btn_add_team')" @click="openTeam(dept.id, null)">
                <Plus :size="16" />
              </button>
              <button type="button" class="p-2 rounded-xl text-slate-400 transition-all hover:bg-indigo-50 hover:text-indigo-500" :title="$t('admin_department_list.btn_edit')" @click="openDepartment(dept)">
                <Pencil :size="15" />
              </button>
              <button type="button" class="p-2 rounded-xl text-slate-400 transition-all hover:bg-rose-50 hover:text-rose-500" :title="$t('admin_department_list.btn_delete')" @click="remove('department', dept)">
                <Trash2 :size="15" />
              </button>
            </div>
          </div>

          <div v-if="!collapsed.has(dept.id)" class="border-t border-slate-100 bg-slate-50/40 px-5 py-3">
            <p v-if="dept.teams.length === 0" class="py-2 text-[12px] font-medium italic text-slate-400">{{ $t('admin_department_list.empty_no_team') }}</p>
            <div class="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
              <div
                v-for="team in dept.teams"
                :key="team.id"
                class="group flex items-center gap-3 rounded-2xl border border-slate-200/60 bg-white px-4 py-3 shadow-sm transition-all hover:border-pr/30 hover:shadow-md"
              >
                <div class="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-blue-600"><Network :size="16" /></div>
                <div class="min-w-0 flex-1">
                  <div class="flex items-center gap-1.5">
                    <span class="truncate text-[13px] font-extrabold text-slate-800">{{ team.name }}</span>
                    <span class="font-mono text-[9px] font-bold text-slate-300">{{ team.code }}</span>
                  </div>
                  <div class="mt-0.5 flex items-center gap-1.5 text-[11px] font-semibold text-slate-400">
                    <Crown :size="11" :class="team.leader ? 'text-amber-500' : 'text-slate-300'" />
                    {{ team.leader?.fullName ?? $t('admin_department_list.lbl_no_leader') }}
                  </div>
                </div>
                <button type="button" class="rounded-lg bg-slate-50 px-2 py-1 text-[11px] font-black text-slate-500 hover:bg-pr/10 hover:text-pr" @click="viewMembers(dept.id, team.id)">
                  {{ team.memberCount }}
                </button>
                <div v-if="canManage" class="flex items-center opacity-0 transition-opacity group-hover:opacity-100">
                  <button type="button" class="p-1.5 rounded-lg text-slate-400 hover:bg-indigo-50 hover:text-indigo-500" :title="$t('admin_department_list.btn_edit')" @click="openTeam(dept.id, team)">
                    <Pencil :size="13" />
                  </button>
                  <button type="button" class="p-1.5 rounded-lg text-slate-400 hover:bg-rose-50 hover:text-rose-500" :title="$t('admin_department_list.btn_delete')" @click="remove('team', team)">
                    <Trash2 :size="13" />
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <OrgUnitDialog
      v-model:open="dialog.open"
      :kind="dialog.kind"
      :department="dialog.department"
      :team="dialog.team"
      :parent-id="dialog.parentId"
      :departments="query.data.value ?? []"
    />
  </PageLayout>
</template>
