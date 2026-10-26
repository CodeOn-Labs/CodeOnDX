<script setup lang="ts">
// Port EmployeeDetailPage.tsx + EmployeeMetrics.tsx: hồ sơ (thẻ dọc), 3 chỉ số (thời gian đồng hành, FTE, số dự án),
// lộ trình đồng hành, dự án đang tham gia. Thêm (D4): sửa hồ sơ, mời tạo tài khoản, quỹ phép năm.
import { computed, ref } from 'vue';
import { RouterLink, useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import {
  Activity, ArrowLeft, Award, Briefcase, Building2, Calendar, CalendarOff, CheckCircle2, Clock, Mail, MailPlus, Pencil, Phone, Trash2, TrendingUp, User, XCircle,
} from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import AnimatedCounter from '@/shared/components/AnimatedCounter.vue';
import Button from '@/shared/components/Button.vue';
import UserAvatar from '@/shared/components/UserAvatar.vue';
import EmployeeFormDialog from '@/modules/employee/components/EmployeeFormDialog.vue';
import AllocationBadge from '@/modules/employee/components/AllocationBadge.vue';
import { orgApi } from '@/modules/org/api/org.api';
import { useAuthStore } from '@/stores/auth';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const route = useRoute();
const { t, locale } = useI18n();
const auth = useAuthStore();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();
const canManage = computed(() => auth.can('employee.manage'));

const id = computed(() => String(route.params.id));
const query = useQuery({
  queryKey: computed(() => ['employees', 'detail', id.value, locale.value] as const),
  queryFn: ({ signal }) => orgApi.employee(id.value, signal),
});
const employee = computed(() => query.data.value);
const working = computed(() => !!employee.value && employee.value.allocationStatus !== 'resigned' && !employee.value.isDeleted);

/** Số ngày đồng hành: từ ngày vào làm đến hôm nay (hoặc ngày nghỉ việc). */
const tenureDays = computed(() => {
  const e = employee.value;
  if (!e?.joinedDate) return null;
  const start = new Date(e.joinedDate);
  const end = !working.value && e.leftDate ? new Date(e.leftDate) : new Date();
  return Math.max(0, Math.ceil((end.getTime() - start.getTime()) / 86_400_000));
});
/** Port formatTenureFromDays: < 30 ngày → ngày; < 12 tháng → tháng; còn lại năm + tháng (1 tháng ≈ 30,41 ngày). */
function formatTenure(days: number) {
  const d = Math.round(days);
  if (d < 30) return t('employee_detail.unit_days', { n: d });
  const months = Math.floor(d / 30.41);
  if (months < 12) return t('employee_detail.unit_months', { n: months });
  const years = Math.floor(months / 12);
  const rest = months % 12;
  return rest === 0 ? t('employee_detail.unit_years', { n: years }) : t('employee_detail.unit_years_months', { y: years, m: rest });
}

const capacity = computed(() => {
  const fte = employee.value?.totalFte ?? 0;
  if (fte > 1) return { color: 'text-rose-600', label: t('employee_detail.lbl_capacity_overloaded'), badge: 'bg-rose-50 text-rose-600 border border-rose-100/50' };
  if (fte === 0) return { color: 'text-amber-600', label: t('employee_detail.lbl_capacity_idle'), badge: 'bg-amber-50 text-amber-600 border border-amber-100/50' };
  if (fte < 1) return { color: 'text-blue-600', label: t('employee_detail.lbl_capacity_low'), badge: 'bg-blue-50 text-blue-600 border border-blue-100/50' };
  return { color: 'text-emerald-600', label: t('employee_detail.lbl_capacity_optimal'), badge: 'bg-emerald-50 text-emerald-600 border border-emerald-100/50' };
});

const profileItems = computed(() => {
  const e = employee.value;
  if (!e) return [];
  return [
    { icon: Award, label: t('employee_detail.lbl_level'), value: e.level?.name },
    { icon: Building2, label: t('employee_detail.lbl_department'), value: e.department?.name },
    { icon: Building2, label: t('employee_detail.lbl_team'), value: e.team?.name },
    { icon: Mail, label: t('employee_detail.lbl_email'), value: e.email },
    { icon: Phone, label: t('employee_detail.lbl_phone'), value: e.phone },
    { icon: User, label: t('employee_detail.lbl_code'), value: e.code },
  ].filter((i) => i.value);
});

const leaveLeft = computed(() => {
  const b = employee.value?.leaveBalance;
  return b ? b.entitledDays + b.carriedOverDays + b.adjustedDays : null;
});

const editOpen = ref(false);
async function invite() {
  const e = employee.value;
  if (!e) return;
  const ok = await confirm.ask({ title: t('employee_detail.confirm_invite_title'), description: t('employee_detail.confirm_invite_desc', { email: e.email }), confirmText: t('employee_detail.btn_invite'), type: 'info' });
  if (!ok) return;
  try {
    toast.success((await orgApi.inviteEmployee(e.id)).messageText);
    void queryClient.invalidateQueries({ queryKey: ['employees'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const CARD = 'border border-slate-200/50 bg-white/60 backdrop-blur-md rounded-3xl shadow-sm hover:shadow-md hover:bg-white/80 transition-all duration-300';
</script>

<template>
  <PageLayout
    layout="B"
    :insight-title="$t('employee_detail.title_insight_panel')"
    insight-screen="employee_detail"
    ai-module="hr_detail"
    :ai-context-meta="employee ? { personName: employee.fullName, personId: employee.id } : undefined"
    content-padding="24px"
    disable-scroll
    insights-width="340px"
    chat-width="300px"
  >
    <div v-if="query.isPending.value" class="flex h-full items-center justify-center py-20">
      <div class="animate-pulse text-[13px] font-medium text-slate-400">{{ $t('employee_detail.lbl_loading') }}</div>
    </div>
    <div v-else-if="!employee" class="flex h-full items-center justify-center py-20">
      <div class="text-[13px] font-medium text-slate-500">{{ $t('employee_detail.empty_not_found') }}</div>
    </div>

    <div v-else class="flex flex-col h-full overflow-hidden relative animate-in fade-in zoom-in-[0.98] duration-500">
      <div class="absolute top-0 left-1/4 w-72 h-72 bg-indigo-100/10 rounded-full blur-3xl pointer-events-none z-0" />
      <div class="absolute bottom-1/4 right-1/4 w-80 h-80 bg-emerald-100/5 rounded-full blur-3xl pointer-events-none z-0" />

      <div class="z-40 -mx-6 -mt-6 mb-6 bg-white/80 px-6 pt-3.5 pb-4 backdrop-blur-xl border-b border-slate-100">
        <div class="flex items-center gap-2 text-[11px] font-bold uppercase tracking-widest text-slate-400 mb-1.5">
          <RouterLink to="/admin/employees" class="hover:text-pr transition-colors flex items-center gap-1.5 group">
            <ArrowLeft :size="14" class="group-hover:-translate-x-0.5 transition-transform" />
            {{ $t('employee_detail.lbl_breadcrumb') }}
          </RouterLink>
          <span class="opacity-40">/</span>
          <span class="text-slate-600 font-extrabold">{{ employee.fullName }}</span>
        </div>
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div class="flex flex-col gap-1">
            <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent m-0 p-0">{{ employee.fullName }}</h1>
            <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] font-bold text-slate-500 tracking-wide mt-1">
              <span class="text-slate-800 font-extrabold uppercase tracking-widest">{{ employee.position?.name ?? $t('employee_detail.lbl_unknown_position') }}</span>
              <template v-if="employee.level">
                <span class="w-1.5 h-1.5 rounded-full bg-slate-200" />
                <span class="text-slate-500 font-medium">{{ employee.level.name }}</span>
              </template>
              <template v-if="employee.department">
                <span class="w-1.5 h-1.5 rounded-full bg-slate-200" />
                <span class="text-slate-500 font-medium">{{ employee.department.name }}{{ employee.team ? ` / ${employee.team.name}` : '' }}</span>
              </template>
            </div>
          </div>
          <div v-if="canManage && !employee.isDeleted" class="flex items-center gap-2">
            <Button v-if="!employee.account" variant="outline" size="sm" @click="invite">
              <template #leftIcon><MailPlus :size="14" /></template>
              {{ $t('employee_detail.btn_invite') }}
            </Button>
            <Button variant="primary" size="sm" @click="editOpen = true">
              <template #leftIcon><Pencil :size="14" /></template>
              {{ $t('employee_detail.btn_edit') }}
            </Button>
          </div>
        </div>
      </div>

      <div class="flex-1 overflow-y-auto min-h-0 pb-8 pr-1 z-10 relative">
        <div class="flex flex-col lg:flex-row gap-6 items-start w-full">
          <!-- Thẻ hồ sơ -->
          <div class="w-full lg:w-[320px] xl:w-[340px] shrink-0 flex flex-col gap-6 lg:sticky lg:top-2">
            <div :class="cn(CARD, 'p-8 flex flex-col items-center relative overflow-hidden group text-center')">
              <div class="absolute inset-0 bg-gradient-to-b from-indigo-50/20 via-white/10 to-white pointer-events-none" />
              <div class="relative flex items-center justify-center select-none mb-6 mt-4 cursor-default">
                <div class="relative">
                  <UserAvatar
                    :name="employee.fullName"
                    :id="employee.id"
                    :src="employee.avatarUrl"
                    :size="128"
                    :class="cn('!rounded-[2.5rem] z-10 relative transition-all duration-500 group-hover:scale-105', !working && '!bg-slate-100 !text-slate-400 !border-slate-200')"
                  />
                  <div
                    :class="cn(
                      'absolute -bottom-2 -right-2 z-20 w-10 h-10 rounded-2xl border-4 border-white flex items-center justify-center shadow-sm transition-transform duration-500 group-hover:scale-110',
                      employee.isDeleted ? 'bg-rose-50 text-rose-500' : working ? 'bg-emerald-50 text-emerald-500' : 'bg-slate-50 text-slate-500',
                    )"
                  >
                    <Trash2 v-if="employee.isDeleted" :size="16" />
                    <CheckCircle2 v-else-if="working" :size="16" />
                    <XCircle v-else :size="16" />
                  </div>
                </div>
              </div>
              <h2 class="text-[22px] font-black tracking-tight text-slate-800 leading-tight mb-1 relative z-10">{{ employee.fullName }}</h2>
              <div class="text-[12px] font-bold text-slate-500 uppercase tracking-widest mb-3 relative z-10">{{ employee.position?.name ?? $t('employee_detail.lbl_unknown_position') }}</div>
              <div class="mb-5 relative z-10 flex items-center gap-2">
                <AllocationBadge :status="employee.allocationStatus" :deleted="employee.isDeleted" />
                <span v-if="employee.employmentStatus" class="rounded-full bg-slate-50 px-2 py-0.5 text-[10px] font-bold text-slate-500 ring-1 ring-slate-200">{{ employee.employmentStatus.name }}</span>
              </div>
              <div class="w-full flex flex-col relative z-10 border-t border-slate-100/80 pt-2">
                <div v-for="item in profileItems" :key="item.label" class="flex items-center justify-between w-full py-2.5 border-b border-slate-100/50 last:border-0 group/item">
                  <div class="flex items-center gap-3 text-slate-500 transition-colors group-hover/item:text-slate-700">
                    <div class="w-8 h-8 rounded-xl bg-slate-50/50 border border-slate-100/80 flex items-center justify-center shadow-sm group-hover/item:bg-white transition-all duration-300 group-hover/item:scale-105">
                      <component :is="item.icon" :size="14" :stroke-width="2.5" />
                    </div>
                    <span class="text-[11px] font-bold tracking-wide uppercase">{{ item.label }}</span>
                  </div>
                  <span class="text-[13px] font-black text-slate-800 truncate max-w-[150px] text-right" :title="item.value ?? ''">{{ item.value }}</span>
                </div>
              </div>
              <div v-if="employee.skills.length" class="w-full mt-4 flex flex-wrap gap-1.5 justify-center relative z-10">
                <span v-for="s in employee.skills" :key="s" class="rounded-full bg-indigo-50 px-2.5 py-0.5 text-[10px] font-bold text-indigo-600 ring-1 ring-indigo-100">{{ s }}</span>
              </div>
            </div>
          </div>

          <div class="flex-1 flex flex-col gap-6 w-full min-w-0">
            <!-- 3 chỉ số -->
            <div class="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-1 2xl:grid-cols-3 gap-6 w-full">
              <div :class="cn(CARD, 'p-6 flex flex-col justify-between group min-h-[140px]')">
                <div class="flex items-center justify-between w-full">
                  <span class="text-[10px] text-slate-400 font-black uppercase tracking-widest leading-none">{{ $t('employee_detail.stat_tenure') }}</span>
                  <div class="w-8 h-8 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center border border-indigo-100/50 group-hover:scale-110 transition-transform duration-300 shadow-sm shrink-0"><Calendar :size="16" /></div>
                </div>
                <div class="mt-4 z-10">
                  <div class="text-[20px] font-black tracking-tight text-slate-800 leading-tight">
                    <AnimatedCounter v-if="tenureDays !== null" :value="tenureDays" :formatter="formatTenure" :duration="1" />
                    <template v-else>{{ $t('employee_detail.lbl_not_updated') }}</template>
                  </div>
                  <span class="text-[10px] text-slate-500 font-bold mt-1.5 block">{{ $t('employee_detail.lbl_joined', { date: formatDate(employee.joinedDate) || '--' }) }}</span>
                </div>
              </div>
              <div :class="cn(CARD, 'p-6 flex flex-col justify-between group min-h-[140px]', employee.totalFte > 1 && 'border-rose-100 bg-rose-50/10')">
                <div class="flex items-center justify-between w-full">
                  <span class="text-[10px] text-slate-400 font-black uppercase tracking-widest leading-none">{{ $t('employee_detail.stat_capacity') }}</span>
                  <div :class="cn('w-8 h-8 rounded-xl flex items-center justify-center border group-hover:scale-110 transition-transform duration-300 shadow-sm shrink-0', capacity.badge)"><TrendingUp :size="16" /></div>
                </div>
                <div class="mt-4 z-10">
                  <div :class="cn('text-[20px] font-black tracking-tight leading-none flex items-baseline gap-1.5', capacity.color)">
                    <AnimatedCounter :value="employee.totalFte" :formatter="(v: number) => v.toFixed(2)" :duration="1" />
                    <span class="text-[11px] font-black text-slate-400 uppercase">{{ $t('employee_detail.unit_fte') }}</span>
                  </div>
                  <span :class="cn('text-[10px] font-black mt-2 inline-flex items-center px-2 py-0.5 rounded bg-slate-50 border border-slate-100 uppercase tracking-wide', capacity.color)">{{ capacity.label }}</span>
                </div>
              </div>
              <div :class="cn(CARD, 'p-6 flex flex-col justify-between group min-h-[140px]')">
                <div class="flex items-center justify-between w-full">
                  <span class="text-[10px] text-slate-400 font-black uppercase tracking-widest leading-none">{{ $t('employee_detail.stat_leave') }}</span>
                  <div class="w-8 h-8 rounded-xl bg-violet-50 text-violet-600 flex items-center justify-center border border-violet-100/50 group-hover:scale-110 transition-transform duration-300 shadow-sm shrink-0"><CalendarOff :size="16" /></div>
                </div>
                <div class="mt-4 z-10">
                  <div class="text-[20px] font-black tracking-tight text-slate-800 leading-tight">
                    <template v-if="leaveLeft !== null">
                      <AnimatedCounter :value="leaveLeft" :duration="1" />
                      <span class="text-[11px] font-black text-slate-400 uppercase ml-1.5">{{ $t('employee_detail.unit_leave_days') }}</span>
                    </template>
                    <template v-else>—</template>
                  </div>
                  <span v-if="employee.leaveBalance" class="text-[10px] font-bold mt-2 inline-flex items-center px-2 py-0.5 rounded bg-indigo-50/50 border border-indigo-100/50 text-indigo-600 uppercase tracking-wide">
                    {{ $t('employee_detail.lbl_leave_breakdown', { entitled: employee.leaveBalance.entitledDays, carried: employee.leaveBalance.carriedOverDays }) }}
                  </span>
                </div>
              </div>
            </div>

            <!-- Lộ trình đồng hành -->
            <div :class="cn(CARD, 'p-7 flex flex-col')">
              <h3 class="text-[14px] font-black text-slate-800 tracking-wider uppercase mb-6 flex items-center gap-2.5">
                <Clock :size="16" class="text-indigo-500" />
                {{ $t('employee_detail.title_timeline') }}
              </h3>
              <div class="w-full py-4 px-1 select-none">
                <div class="relative flex justify-between items-center w-full">
                  <div class="absolute left-6 right-6 top-1/2 -translate-y-1/2 h-[3px] bg-slate-100 rounded-full z-0 overflow-hidden">
                    <div :class="cn('h-full rounded-full bg-gradient-to-r animate-in slide-in-from-left duration-1000', working ? 'from-emerald-400 to-indigo-500' : 'from-slate-300 to-slate-400')" />
                  </div>
                  <div class="flex flex-col items-center z-10">
                    <div :class="cn('w-11 h-11 rounded-2xl flex items-center justify-center border shadow-sm bg-white transition-transform hover:scale-110', working ? 'border-emerald-200 text-emerald-500' : 'border-slate-200 text-slate-400')">
                      <Calendar :size="18" />
                    </div>
                    <span class="text-[10px] text-slate-400 uppercase font-black tracking-widest mt-2">{{ $t('employee_detail.lbl_join') }}</span>
                    <span class="text-[12px] font-extrabold text-slate-800 mt-0.5 whitespace-nowrap">{{ formatDate(employee.joinedDate) || '--/--/----' }}</span>
                  </div>
                  <div class="flex flex-col items-center z-10">
                    <div :class="cn('px-3 py-1 rounded-full border text-[10px] font-black uppercase tracking-wider shadow-sm bg-white whitespace-nowrap', working ? 'border-indigo-100 text-indigo-600 bg-indigo-50/60' : 'border-slate-200 text-slate-500 bg-slate-50/60')">
                      {{ working ? $t('employee_detail.lbl_tenure_active') : $t('employee_detail.lbl_tenure_resigned') }}
                    </div>
                  </div>
                  <div class="flex flex-col items-center z-10">
                    <div :class="cn('w-11 h-11 rounded-2xl flex items-center justify-center border shadow-sm bg-white transition-transform hover:scale-110', working ? 'border-indigo-200 text-indigo-500' : 'border-rose-200 text-rose-500')">
                      <CheckCircle2 v-if="working" :size="18" />
                      <XCircle v-else :size="18" />
                    </div>
                    <span class="text-[10px] text-slate-400 uppercase font-black tracking-widest mt-2">{{ $t('employee_detail.lbl_status') }}</span>
                    <span class="text-[12px] font-extrabold text-slate-800 mt-0.5 whitespace-nowrap">{{ working ? $t('employee_detail.lbl_present') : formatDate(employee.leftDate) || $t('employee_detail.lbl_tenure_resigned') }}</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Dự án đang tham gia -->
            <div :class="cn(CARD, 'p-7 flex flex-col')">
              <div class="flex justify-between items-center mb-6">
                <h3 class="text-[14px] font-black text-slate-800 tracking-wider uppercase flex items-center gap-2.5">
                  <Activity :size="16" class="text-indigo-500" />
                  {{ $t('employee_detail.title_projects') }}
                </h3>
                <span class="text-[10px] text-indigo-600 font-extrabold bg-indigo-50/80 border border-indigo-100/60 px-3 py-1 rounded-full shadow-sm uppercase tracking-wider">
                  {{ $t('employee_detail.lbl_project_count', { count: employee.projects.length }) }}
                </span>
              </div>
              <div class="flex flex-wrap gap-4 w-full">
                <RouterLink
                  v-for="project in employee.projects"
                  :key="project.id"
                  :to="{ path: `/projects/${project.id}`, query: { highlightEmployeeId: employee.id } }"
                  class="w-full sm:w-[calc(50%-8px)] lg:w-full 2xl:w-[calc(50%-8px)] flex items-center gap-3.5 p-4 rounded-2xl border border-slate-200/60 bg-white/60 backdrop-blur-sm shadow-sm transition-all duration-300 hover:shadow-md hover:bg-white hover:border-indigo-300 hover:-translate-y-0.5 group"
                >
                  <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-50 to-indigo-100/50 border border-indigo-100 flex items-center justify-center text-indigo-600 shadow-inner">
                    <Briefcase :size="16" />
                  </div>
                  <div class="flex-1 min-w-0">
                    <h4 class="text-[13px] font-black text-slate-800 truncate group-hover:text-indigo-600 transition-colors">{{ project.name }}</h4>
                    <span class="text-[9px] text-slate-400 font-black uppercase tracking-widest block mt-0.5">{{ project.role }} · {{ project.fte.toFixed(2) }} {{ $t('employee_detail.unit_fte') }}</span>
                  </div>
                </RouterLink>
                <div v-if="employee.projects.length === 0" class="text-[13px] text-slate-500 font-medium py-8 flex flex-col items-center gap-2 w-full justify-center border border-dashed border-slate-200 rounded-2xl bg-slate-50/20">
                  <Calendar :size="20" class="text-slate-400" />
                  <span>{{ $t('employee_detail.empty_no_project') }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <EmployeeFormDialog v-model:open="editOpen" :employee="employee ?? null" />
  </PageLayout>
</template>
