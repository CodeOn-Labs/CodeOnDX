<script setup lang="ts">
// Port AssignmentTable.tsx: mỗi nhóm = vai trò hợp đồng × cấp độ (gộp ô), mỗi dòng = 1 vị trí (slot) hợp đồng.
// Cột người được assign: nhân sự + các giai đoạn FTE (tổng HĐ / KH / TT tính theo FTE-ngày do BE tính sẵn),
// cảnh báo nghỉ việc / sắp nghỉ / quá tải phân bổ. Cột biểu đồ: 3 đường HĐ / KH / TT theo ngày.
// Khác bản gốc: biểu đồ SVG nhẹ cuộn ngang thay TradingView (không zoom bằng con lăn); bỏ kéo đổi độ rộng cột.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQueryClient } from '@tanstack/vue-query';
import { ChevronsDownUp, ChevronsUpDown, Pencil, UserMinus, UserPlus } from '@lucide/vue';
import UserAvatar from '@/shared/components/UserAvatar.vue';
import EmptyState from '@/shared/components/EmptyState.vue';
import LineChart from '@/modules/project/components/LineChart.vue';
import AssignMemberDialog from './AssignMemberDialog.vue';
import { projectDetailApi, type ProjectDetail, type StaffAssignment, type StaffGroup, type StaffSlot, type Staffing } from '@/modules/project/api/project-detail.api';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ project: ProjectDetail; staffing: Staffing; canManage: boolean }>();
const { t } = useI18n();
const confirm = useConfirmStore();
const toast = useToastStore();
const queryClient = useQueryClient();
const compact = ref(false);
const today = new Date().toISOString().slice(0, 10);

type BadgeKind = 'outside' | 'not_assigned' | 'shortfall' | 'full' | 'excess';
/** Nhãn đủ / thiếu người của nhóm: so số vị trí hợp đồng (kế hoạch) với số vị trí đã có người. */
function staffingBadge(g: StaffGroup): { kind: BadgeKind; text: string; cls: string } {
  const plan = g.contractCount;
  const actual = g.assignedCount;
  if (plan === 0) return { kind: 'outside', text: t('project_detail.lbl_badge_outside_contract', { n: actual }), cls: 'bg-slate-100 text-slate-600' };
  if (actual === 0) return { kind: 'not_assigned', text: t('project_detail.lbl_badge_not_assigned', { plan }), cls: 'bg-amber-50 text-amber-700' };
  if (actual < plan) return { kind: 'shortfall', text: t('project_detail.lbl_badge_shortfall', { actual, plan }), cls: 'bg-blue-50 text-blue-700' };
  if (actual === plan) return { kind: 'full', text: t('project_detail.lbl_badge_full', { actual, plan }), cls: 'bg-emerald-50 text-emerald-700' };
  return { kind: 'excess', text: t('project_detail.lbl_badge_excess', { actual, plan }), cls: 'bg-rose-50 text-rose-700' };
}

const series = (slot: StaffSlot) => [
  { key: 'contract', label: t('project_detail.lbl_contract'), color: '#22c55e', values: slot.series.map((p) => p.contract), step: true },
  { key: 'planned', label: t('project_detail.lbl_plan'), color: '#3565f6', values: slot.series.map((p) => p.planned), step: true },
  { key: 'actual', label: t('project_detail.lbl_actual'), color: '#ef4444', values: slot.series.map((p) => p.actual) },
];
const labels = (slot: StaffSlot) => slot.series.map((p) => p.date);

const dialogOpen = ref(false);
const editing = ref<{ group: StaffGroup; slot: StaffSlot; assignment: StaffAssignment | null } | null>(null);
function openAssign(group: StaffGroup, slot: StaffSlot, assignment: StaffAssignment | null) {
  if (!props.canManage || props.project.isDeleted) return;
  editing.value = { group, slot, assignment };
  dialogOpen.value = true;
}
function refresh() {
  void queryClient.invalidateQueries({ queryKey: ['projects', 'staffing', props.project.id] });
  void queryClient.invalidateQueries({ queryKey: ['projects', 'detail', props.project.id] });
}
async function unassign(group: StaffGroup, a: StaffAssignment, intervalIndex: number) {
  const interval = a.intervals[intervalIndex];
  const ok = await confirm.ask({
    type: 'danger',
    title: t('project_detail.confirm_unassign_title'),
    description: t('project_detail.confirm_unassign_desc', {
      phase: intervalIndex + 1, name: a.employee?.fullName ?? '', role: group.roleName,
      from: formatDate(interval?.startDate), to: formatDate(interval?.endDate),
    }),
    confirmText: t('project_detail.btn_confirm'),
  });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.removeAssignment(props.project.id, a.id, a.intervals.length > 1 ? interval?.id : undefined)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const totalSlots = computed(() => props.staffing.groups.reduce((s, g) => s + g.slots.length, 0));
const CELL = 'align-top border-b border-r border-slate-100';
</script>

<template>
  <div class="flex h-full min-h-0 flex-col">
    <div class="flex-1 min-h-0 overflow-auto rounded-[12px] border border-[#e8ecf5] bg-white shadow-sm">
      <table class="w-full border-separate border-spacing-0">
        <thead>
          <tr>
            <th class="sticky left-0 top-0 z-30 w-[180px] min-w-[180px] border-b border-r border-slate-200 bg-slate-50/90 p-[6px_10px] text-left text-[11px] font-extrabold uppercase tracking-widest text-slate-500 backdrop-blur-md">{{ $t('project_detail.col_contract_role') }}</th>
            <th class="sticky left-[180px] top-0 z-30 w-[300px] min-w-[300px] border-b border-r border-slate-200 bg-slate-50/90 p-[6px_10px] text-left text-[11px] font-extrabold uppercase tracking-widest text-slate-500 backdrop-blur-md">{{ $t('project_detail.col_assigned_person') }}</th>
            <th class="sticky top-0 z-20 border-b border-slate-200 bg-slate-50/90 p-[6px_10px] text-left text-[11px] font-extrabold uppercase tracking-widest text-slate-500 backdrop-blur-md">
              <div class="flex items-center justify-between gap-2">
                <span>{{ $t('project_detail.col_chart') }}</span>
                <button type="button" class="flex items-center gap-1 rounded-md px-2 py-0.5 text-[10px] font-bold normal-case tracking-normal text-slate-500 hover:bg-white hover:text-pr" @click="compact = !compact">
                  <component :is="compact ? ChevronsUpDown : ChevronsDownUp" :size="12" />{{ compact ? $t('project_detail.btn_expand') : $t('project_detail.btn_collapse') }}
                </button>
              </div>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="totalSlots === 0">
            <td colspan="3" class="p-6">
              <EmptyState compact :title="$t('project_detail.empty_staffing_title')" :description="$t('project_detail.empty_staffing_desc')" />
            </td>
          </tr>
          <template v-for="group in props.staffing.groups" :key="group.key">
            <tr v-for="(slot, index) in group.slots" :key="slot.key">
              <td v-if="index === 0" :rowspan="group.slots.length" :class="cn(CELL, 'sticky left-0 z-10 bg-white', compact ? 'p-[6px_8px]' : 'p-[10px_12px]')">
                <div class="flex flex-col gap-2">
                  <strong :class="cn('block font-semibold leading-snug text-slate-900 tracking-tight', compact ? 'text-[11.5px]' : 'text-[13px]')">{{ group.roleName }}</strong>
                  <div class="flex flex-wrap items-center gap-1.5">
                    <span v-if="group.level" class="inline-flex items-center rounded px-1.5 py-0.5 text-[10px] font-bold uppercase bg-slate-100 text-slate-600">{{ group.level.name }}</span>
                    <span v-if="!compact" :class="cn('inline-flex items-center px-2 py-0.5 rounded text-[12px] font-medium', staffingBadge(group).cls)">{{ staffingBadge(group).text }}</span>
                  </div>
                </div>
              </td>
              <td :class="cn(CELL, 'sticky left-[180px] z-10 bg-white', compact ? 'p-[4px_6px]' : 'p-[6px_10px]')">
                <div v-if="slot.assignments.length > 0" class="flex flex-col gap-2">
                  <div v-for="a in slot.assignments" :key="a.id" class="flex flex-col gap-1">
                    <div class="flex flex-wrap items-center gap-2">
                      <UserAvatar :name="a.employee?.fullName ?? '?'" :id="a.employee?.id" :src="a.employee?.avatarUrl" :size="compact ? 20 : 24" />
                      <span :class="cn('font-bold', compact ? 'text-[11px]' : 'text-[12px]', a.employee?.resigned ? 'text-[#8c8c8c] line-through opacity-60' : a.employee?.overloaded ? 'text-[#faad14]' : 'text-[#262626]')">{{ a.employee?.fullName }}</span>
                      <span class="text-[10.5px] text-slate-400">{{ a.employee?.code }}</span>
                      <span v-if="a.employee?.resigned" class="text-[9px] font-extrabold uppercase bg-rose-50 text-rose-600 px-1 rounded border border-rose-200">{{ $t('project_detail.lbl_resigned') }}</span>
                      <span v-else-if="a.employee?.leavingSoon" class="text-[9px] font-extrabold uppercase bg-amber-50 text-amber-600 px-1 rounded border border-amber-200" :title="formatDate(a.employee.leftDate)">{{ $t('project_detail.lbl_leaving_soon', { date: formatDate(a.employee.leftDate) }) }}</span>
                      <span
                        v-if="a.employee?.overloaded"
                        class="text-[9px] font-extrabold uppercase bg-orange-50 text-orange-600 px-1 rounded border border-orange-200"
                        :title="$t('project_detail.tip_overload', { current: a.employee.allocationPercent, limit: a.employee.overloadThreshold })"
                      >{{ $t('project_detail.lbl_overload') }}</span>
                      <button v-if="props.canManage && !props.project.isDeleted" type="button" class="ml-auto flex h-6 w-6 items-center justify-center rounded-md text-slate-400 hover:bg-slate-100 hover:text-pr" :title="$t('project_detail.btn_edit_assignment')" @click="openAssign(group, slot, a)"><Pencil :size="12" /></button>
                    </div>
                    <div v-if="!compact" class="flex flex-col gap-1.5 border-t border-slate-100 pt-1.5">
                      <div v-for="(iv, i) in a.intervals" :key="iv.id" :class="cn('group/phase relative flex flex-col rounded-xl border p-2 shadow-sm', iv.startDate <= today && iv.endDate >= today ? 'bg-slate-50 border-slate-200' : 'bg-white border-slate-200')">
                        <div class="flex flex-wrap items-center gap-1.5 pr-7 text-[11px] font-semibold text-slate-700">
                          <span class="text-[9px] uppercase font-bold tracking-wider px-1.5 py-0.5 rounded text-slate-500 bg-slate-100 ring-1 ring-inset ring-slate-500/10">{{ $t('project_detail.lbl_phase', { n: i + 1 }) }}</span>
                          <span class="whitespace-nowrap">{{ formatDate(iv.startDate) }} → {{ formatDate(iv.endDate) }}</span>
                          <span class="font-bold text-pr">{{ iv.fte }} FTE</span>
                        </div>
                        <div class="mt-1 flex flex-wrap gap-2 text-[10px] font-medium text-slate-500">
                          <span class="flex items-center gap-1">{{ $t('project_detail.lbl_contract_short') }} <span class="rounded border border-emerald-500/20 bg-emerald-50 px-1.5 py-0.5 font-bold leading-none text-emerald-700">{{ iv.sums.contract.toFixed(1) }}</span></span>
                          <span class="flex items-center gap-1">{{ $t('project_detail.lbl_plan_short') }} <span class="rounded border border-blue-500/20 bg-blue-50 px-1.5 py-0.5 font-bold leading-none text-blue-700">{{ iv.sums.planned.toFixed(1) }}</span></span>
                          <span class="flex items-center gap-1">{{ $t('project_detail.lbl_actual_short') }} <span class="rounded border border-rose-500/20 bg-rose-50 px-1.5 py-0.5 font-bold leading-none text-rose-700">{{ iv.sums.actual.toFixed(1) }}</span></span>
                        </div>
                        <button
                          v-if="props.canManage && !props.project.isDeleted"
                          type="button"
                          class="absolute right-1.5 top-1.5 flex h-6 w-6 items-center justify-center rounded-md text-slate-300 opacity-0 transition-opacity hover:bg-rose-50 hover:text-rose-500 group-hover/phase:opacity-100"
                          :title="$t('project_detail.btn_unassign')"
                          @click="unassign(group, a, i)"
                        ><UserMinus :size="12" /></button>
                      </div>
                    </div>
                  </div>
                </div>
                <button
                  v-else
                  type="button"
                  :disabled="!props.canManage || props.project.isDeleted"
                  class="flex w-full items-center gap-2 rounded-lg border border-dashed border-slate-200 px-2 py-2 text-left transition-colors enabled:hover:border-pr enabled:hover:bg-pr/5"
                  :title="$t('project_detail.tip_empty_slot')"
                  @click="openAssign(group, slot, null)"
                >
                  <UserPlus :size="14" class="text-slate-400" />
                  <span class="text-[12px] font-semibold text-[#bfbfbf]">{{ $t('project_detail.lbl_not_assigned') }}</span>
                  <span class="text-[12px] text-[#8c8c8c]">{{ $t('project_detail.lbl_vacant') }}</span>
                </button>
              </td>
              <td :class="cn('align-top border-b border-slate-100', compact ? 'p-1' : 'p-2')">
                <div class="max-w-[calc(100vw-900px)] min-w-[320px]">
                  <LineChart :labels="labels(slot)" :series="series(slot)" :height="compact ? 64 : 104" :point-width="2" :y-max="2" :mark-label="today" />
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
    <p class="mt-1.5 text-[11px] text-slate-400">{{ $t('project_detail.hint_table') }}</p>

    <AssignMemberDialog
      v-model:open="dialogOpen"
      :project-id="props.project.id"
      :project-start="props.project.startDate"
      :project-end="props.project.endDate"
      :group="editing?.group ?? null"
      :position="editing?.slot ?? null"
      :assignment="editing?.assignment ?? null"
      @saved="refresh"
    />
  </div>
</template>
