<script setup lang="ts">
// Port EffortPage.tsx + EffortGrid.tsx + EffortGridToolbar.tsx + EffortGridHeaderRow.tsx + EffortDepartmentGroup.tsx + EffortGroupHeader.tsx:
// lưới Công số theo tháng, nhóm theo phòng ban (dòng tổng nhóm), sửa ô lưu ngay khi rời ô, thêm thành viên, xoá dòng.
// Khác bản gốc (D12): Mức hiệu suất lấy từ danh mục performance_level (hệ số ở metadata.coefficient) thay bộ HV/CT1/TV/CT2 cố định;
// danh sách vai trò lấy từ danh mục Vị trí nhân sự (job_position); cột "ldap" đổi thành ezID (mã nhân sự).
// 15 cột theo đặc tả 28/10/2026: Họ và tên · ezID · Team gốc 🔒 · Role · Note · Project · Kế hoạch · Thực tế · OT Effort 🔒 ·
// Performance 🔒 · Actual · Actual1 · Free · Free1 · Thao tác. Cột 🔒 lấy từ hệ thống khác, chỉ xem trên lưới.
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Columns3, Lock, Search, Trash2, X } from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import DynamicInsights from '@/modules/ai/components/DynamicInsights.vue';
import MonthNavigator from '@/shared/components/form/MonthNavigator.vue';
import PortalTooltip from '@/shared/components/PortalTooltip.vue';
import EffortNumberCell from './EffortNumberCell.vue';
import EffortSelectCell from './EffortSelectCell.vue';
import EffortNoteCell from './EffortNoteCell.vue';
import AddMemberPopover from './AddMemberPopover.vue';
import { effortApi, type EffortMonth, type EffortPatch, type EffortRow } from '@/modules/effort/api/effort.api';
import {
  buildFreeByMemberMap, calcActual, calcActual1, calcGroupTotals, formatEffortNumber, FREE_EPSILON, roundToDecimals, type GroupTotals,
} from '@/modules/effort/utils/effort-calculations';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { cn } from '@/shared/utils/cn';

interface GridRow extends EffortRow {
  actual: number;
  actual1: number;
  free: number;
  free1: number;
}

const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const month = ref(new Date().toISOString().slice(0, 7));
const queryKey = computed(() => ['effort', month.value] as const);
const query = useQuery({ queryKey, queryFn: ({ signal }) => effortApi.month(month.value, signal) });
const data = computed(() => query.data.value);
const canEdit = computed(() => !!data.value?.canEdit);

/** Thêm Actual / Actual1 / Free / Free1 vào từng dòng (công thức ở effort-calculations.ts). */
const gridRows = computed<GridRow[]>(() => {
  const rows = data.value?.rows ?? [];
  const input = rows.map((r) => ({ employee_id: r.employee?.id ?? r.id, tt_effort: r.ttEffort, ot_effort: r.otEffort, performance: r.performance }));
  const freeByMember = buildFreeByMemberMap(input);
  return rows.map((r, i) => {
    const member = freeByMember.get(input[i]!.employee_id);
    return {
      ...r,
      actual: roundToDecimals(calcActual(r.ttEffort, r.otEffort)),
      actual1: roundToDecimals(calcActual1(r.ttEffort, r.otEffort, r.performance)),
      free: roundToDecimals(member?.free ?? 0),
      free1: roundToDecimals(member?.free1 ?? 0),
    };
  });
});

// ─── Tìm kiếm + cột phụ ─────────────────────────────────────────────────────
const search = ref('');
/** Nút "Hiện Team gốc / Role / Note" như bản gốc: mặc định ẩn, không lưu lựa chọn (tải lại trang là ẩn). */
const showExtra = ref(false);
const fold = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').toLowerCase();
const matches = (r: GridRow) => {
  const q = fold(search.value.trim());
  return !q || fold(r.employee?.fullName ?? '').includes(q) || fold(r.employee?.code ?? '').includes(q);
};
const groups = computed(() =>
  (data.value?.departments ?? [])
    .map((d) => {
      const rows = gridRows.value.filter((r) => r.departmentId === d.id);
      return { department: d, rows, visible: rows.filter(matches), totals: totalsOf(rows) };
    })
    .filter((g) => !search.value.trim() || g.visible.length > 0),
);
function totalsOf(rows: GridRow[]): GroupTotals {
  return calcGroupTotals(rows.map((r) => ({ employee_id: r.employee?.id ?? r.id, hd: r.hd, actual: r.actual, actual1: r.actual1, tt_effort: r.ttEffort, ot_effort: r.otEffort, free: r.free, free1: r.free1 })));
}

// ─── Cột ────────────────────────────────────────────────────────────────────
type ColKey = 'homeTeam' | 'role' | 'note' | 'project' | 'hd' | 'ttEffort' | 'otEffort' | 'performance' | 'actual' | 'actual1' | 'free' | 'free1';
const COLUMN_COLORS: Partial<Record<ColKey, { bg: string; text: string }>> = {
  hd: { bg: '#dbeafe', text: '#1d4ed8' },
  ttEffort: { bg: '#ccfbf1', text: '#0f766e' },
  otEffort: { bg: '#fce7f3', text: '#be185d' },
  performance: { bg: '#ede9fe', text: '#6d28d9' },
  actual: { bg: '#cffafe', text: '#0e7490' },
  actual1: { bg: '#e0e7ff', text: '#4338ca' },
  free: { bg: '#dcfce7', text: '#15803d' },
  free1: { bg: '#ecfccb', text: '#4d7c0f' },
};
const NUMERIC: ColKey[] = ['hd', 'ttEffort', 'otEffort', 'actual', 'actual1', 'free', 'free1'];
const COMPUTED: ColKey[] = ['actual', 'actual1', 'free', 'free1'];
/** Cột khoá (🔒): dữ liệu đồng bộ từ hệ thống khác, không sửa trên lưới. */
const LOCKED: ColKey[] = ['homeTeam', 'otEffort', 'performance'];
/** Team gốc ghim cạnh Họ và tên + ezID khi cuộn ngang. */
const PINNED_LEFT: Partial<Record<ColKey, string>> = { homeTeam: 'left-72' };
const WIDTH: Record<ColKey, string> = {
  homeTeam: 'w-36 min-w-36', role: 'w-32', note: 'w-40', project: 'w-44', hd: 'w-24', ttEffort: 'w-24', otEffort: 'w-24', performance: 'w-36',
  actual: 'w-20', actual1: 'w-20', free: 'w-20', free1: 'w-20',
};
const EXTRA: ColKey[] = ['homeTeam', 'role', 'note'];
const visibleCols = computed<ColKey[]>(() => [
  ...(showExtra.value ? EXTRA : []),
  'project', 'hd', 'ttEffort', 'otEffort', 'performance', 'actual', 'actual1', 'free', 'free1',
]);
const colLabel = (k: ColKey) =>
  ({
    homeTeam: t('effort.col_home_team'), role: t('effort.col_role'), note: t('effort.col_note'), project: t('effort.col_project'), hd: t('effort.col_hd'),
    ttEffort: t('effort.col_tt_effort'), otEffort: t('effort.col_ot_effort'), performance: t('effort.col_performance'),
    actual: t('effort.col_actual'), actual1: t('effort.col_actual1'), free: t('effort.col_free'), free1: t('effort.col_free1'),
  })[k];
/** Họ và tên, ezID, Role, Note, Project, Thao tác không có chú thích. */
const colTip = (k: ColKey) =>
  ({
    homeTeam: t('effort.tip_home_team'), hd: t('effort.tip_hd'), ttEffort: t('effort.tip_tt_effort'), otEffort: t('effort.tip_ot_effort'), performance: t('effort.tip_performance'),
    actual: t('effort.tip_actual'), actual1: t('effort.tip_actual1'), free: t('effort.tip_free'), free1: t('effort.tip_free1'),
  })[k as 'hd'] ?? '';
/** Performance hiện mã mức kèm hệ số, VD "TV (0.8)"; tên đầy đủ ở title của ô. */
const performanceLabel = (r: GridRow) => (r.performanceLevel ? `${r.performanceLevel.code} (${r.performance.toFixed(1)})` : r.performance.toFixed(1));
const totalValue = (k: ColKey, totals: GroupTotals): number | null =>
  ({ hd: totals.hd, ttEffort: totals.ttEffort, otEffort: totals.otEffort, actual: totals.actual, actual1: totals.actual1, free: totals.free, free1: totals.free1 })[k as 'hd'] ?? null;

const teamOptions = computed(() => (data.value?.teams ?? []).map((tm) => ({ value: tm.id, label: tm.name })));
const roleOptions = computed(() => (data.value?.roles ?? []).map((r) => ({ value: r, label: r })));
const projectOptionsFor = (departmentId: string) =>
  (data.value?.projects ?? []).filter((p) => !p.departmentId || p.departmentId === departmentId).map((p) => ({ value: p.id, label: p.name }));

// ─── Lưu ô (cập nhật cache ngay để tổng nhóm tính lại) ─────────────────────
const saving = ref<Set<string>>(new Set());
async function saveField(row: GridRow, patch: EffortPatch): Promise<boolean> {
  saving.value = new Set(saving.value).add(row.id);
  try {
    const res = await effortApi.update(row.id, { ...patch, departmentId: row.departmentId ?? undefined });
    queryClient.setQueryData<EffortMonth>(queryKey.value, (old) => {
      if (!old) return old;
      const byId = new Map(res.data.map((r) => [r.id, r]));
      return { ...old, rows: old.rows.map((r) => byId.get(r.id) ?? r) };
    });
    if (patch.projectId) void queryClient.invalidateQueries({ queryKey: ['effort'] });
    toast.success(t('effort.msg_saved', { name: row.employee?.fullName ?? '', department: row.departmentName ?? '—' }), undefined, 2000);
    return true;
  } catch (err) {
    toast.error((err as Error).message, t('effort.title_save_failed'));
    return false;
  } finally {
    const next = new Set(saving.value);
    next.delete(row.id);
    saving.value = next;
  }
}
async function remove(row: GridRow) {
  const ok = await confirm.ask({
    title: t('effort.confirm_delete_title'),
    description: t('effort.confirm_delete_desc', { name: row.employee?.fullName ?? '', project: row.projectName, month: month.value }),
    confirmText: t('effort.btn_confirm_delete'),
    type: 'danger',
  });
  if (!ok) return;
  try {
    toast.success((await effortApi.remove(row.id)).messageText);
    void queryClient.invalidateQueries({ queryKey: ['effort'] });
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const PINNED = 'sticky z-10';
const HEAD = 'sticky top-0 h-11 bg-slate-50 text-left px-3 py-2 font-bold text-slate-500 uppercase tracking-wide whitespace-nowrap border-b border-slate-200';
const GROUP_CELL = 'h-11 bg-prbg text-prt text-[12px] font-black px-3 py-1.5 whitespace-nowrap';
</script>

<template>
  <PageLayout layout="A" :insight-title="$t('effort.title_insight_panel')" content-padding="24px" insights-width="300px" disable-scroll>
    <template #insights>
      <DynamicInsights screen="effort" />
    </template>

    <div class="h-full flex flex-col min-h-0 gap-4">
      <div class="flex items-center justify-between gap-3 flex-wrap">
        <div class="flex flex-col gap-1">
          <div class="flex items-center gap-2">
            <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
            <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ $t('effort.lbl_kicker') }}</span>
          </div>
          <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">{{ $t('effort.title_page') }}</h1>
        </div>
        <MonthNavigator v-model="month" />
      </div>

      <div class="flex items-center justify-between gap-3">
        <div class="group relative flex items-center">
          <Search :size="15" class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 group-focus-within:text-pr" />
          <input
            v-model="search"
            type="text"
            :placeholder="$t('effort.ph_search')"
            :aria-label="$t('effort.ph_search')"
            class="h-9 w-60 rounded-xl border border-slate-200 bg-white pl-9 pr-8 text-[13px] font-medium text-slate-900 outline-none transition-all placeholder:text-slate-400 focus:border-pr focus:ring-4 focus:ring-pr/10"
          />
          <button v-if="search" type="button" class="absolute right-2.5 top-1/2 -translate-y-1/2 rounded-full p-0.5 text-slate-400 hover:bg-slate-100" :aria-label="$t('effort.btn_clear_search')" @click="search = ''">
            <X :size="13" />
          </button>
        </div>
        <button
          type="button"
          :aria-pressed="showExtra"
          :class="cn('flex h-9 items-center gap-2 rounded-xl border px-3 text-[12px] font-bold transition-all', showExtra ? 'border-pr/40 bg-pr/5 text-pr' : 'border-slate-200 bg-white text-slate-600 hover:border-pr hover:text-pr')"
          @click="showExtra = !showExtra"
        >
          <Columns3 :size="14" />
          {{ showExtra ? $t('effort.btn_hide_extra') : $t('effort.btn_show_extra') }}
        </button>
      </div>

      <div class="flex-1 min-h-0 overflow-auto rounded-lg border border-[#e5e8f2] bg-white">
        <table class="min-w-max border-separate border-spacing-0 text-[12px]">
          <thead>
            <tr>
              <th :class="cn(HEAD, 'z-30 left-0 w-48 min-w-48')">{{ $t('effort.col_name') }}</th>
              <th :class="cn(HEAD, 'z-30 left-48 w-24 min-w-24')">{{ $t('effort.col_code') }}</th>
              <th
                v-for="k in visibleCols"
                :key="k"
                :class="cn(HEAD, PINNED_LEFT[k] ? ['z-30', PINNED_LEFT[k]] : 'z-20 relative', WIDTH[k], NUMERIC.includes(k) && 'text-right')"
                :style="COLUMN_COLORS[k] ? { backgroundColor: COLUMN_COLORS[k]!.bg, color: COLUMN_COLORS[k]!.text } : undefined"
              >
                {{ colLabel(k) }}
                <template v-if="LOCKED.includes(k)">
                  <Lock :size="11" class="ml-1 inline-block -mt-0.5 opacity-70" aria-hidden="true" />
                  <span class="sr-only">{{ $t('effort.lbl_locked') }}</span>
                </template>
                <template v-if="colTip(k)">
                  <span class="pointer-events-none absolute top-0 right-0 h-0 w-0 border-t-[9px] border-l-[9px] border-t-amber-400 border-l-transparent" />
                  <PortalTooltip side="top" arrow-border-color="#e2e8f0">
                    <div class="absolute inset-0 cursor-help" />
                    <template #content>
                      <div class="max-w-[320px] whitespace-pre-line rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-[11px] font-normal normal-case tracking-normal text-t1 shadow-lg">{{ colTip(k) }}</div>
                    </template>
                  </PortalTooltip>
                </template>
              </th>
              <th v-if="canEdit" :class="cn(HEAD, 'z-20 w-20')">{{ $t('effort.col_actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="query.isPending.value">
              <td :colspan="visibleCols.length + 3" class="px-3 py-6 text-center text-slate-400">{{ $t('effort.lbl_loading') }}</td>
            </tr>
            <tr v-else-if="search.trim() && groups.length === 0">
              <td :colspan="visibleCols.length + 3" class="px-3 py-6 text-center text-slate-400">{{ $t('effort.empty_no_search_result', { query: search }) }}</td>
            </tr>
            <template v-for="g in groups" v-else :key="g.department.id">
              <tr class="border-b border-pr/15">
                <td :class="cn(PINNED, GROUP_CELL, 'left-0 w-48')">{{ g.department.name }}</td>
                <td :class="cn(PINNED, GROUP_CELL, 'left-48 w-24')" />
                <td v-for="k in visibleCols" :key="k" :class="cn(PINNED_LEFT[k] && [PINNED, PINNED_LEFT[k]], GROUP_CELL, WIDTH[k], NUMERIC.includes(k) && 'text-right')">
                  <span v-if="totalValue(k, g.totals) !== null" :class="cn(totalValue(k, g.totals) === 0 && 'text-prt/50')">{{ formatEffortNumber(totalValue(k, g.totals)!) }}</span>
                </td>
                <td v-if="canEdit" :class="cn(GROUP_CELL, 'w-20')" />
              </tr>
              <tr v-for="(row, i) in g.visible" :key="row.id" :class="cn(i % 2 === 0 ? 'bg-white' : 'bg-slate-50', saving.has(row.id) && 'opacity-60')">
                <td :class="cn(PINNED, 'left-0 w-48 px-3 py-1.5 whitespace-nowrap truncate border-b border-slate-100', i % 2 === 0 ? 'bg-white' : 'bg-slate-50')" :title="row.employee?.fullName">{{ row.employee?.fullName }}</td>
                <td :class="cn(PINNED, 'left-48 w-24 px-3 py-1.5 whitespace-nowrap border-b border-slate-100 text-slate-500', i % 2 === 0 ? 'bg-white' : 'bg-slate-50')">{{ row.employee?.code ?? '—' }}</td>
                <td
                  v-for="k in visibleCols"
                  :key="k"
                  :class="cn(
                    'px-2 py-1 whitespace-nowrap border-b border-slate-100', WIDTH[k],
                    PINNED_LEFT[k] && [PINNED, PINNED_LEFT[k], 'px-3 py-1.5 truncate', i % 2 === 0 ? 'bg-white' : 'bg-slate-50'],
                    NUMERIC.includes(k) && 'text-right', COMPUTED.includes(k) && 'font-bold', k === 'free' && row.free > FREE_EPSILON && 'bg-amber-50',
                  )"
                  :style="{ color: k === 'free1' && row.free1 < 0 ? '#e11d48' : COLUMN_COLORS[k]?.text }"
                >
                  <template v-if="k === 'homeTeam'">{{ row.homeTeamName ?? '—' }}</template>
                  <template v-else-if="k === 'role'">
                    <EffortSelectCell v-if="canEdit" :value="row.role" :options="roleOptions" :disabled="saving.has(row.id)" @change="saveField(row, { role: $event })" />
                    <template v-else>{{ row.role }}</template>
                  </template>
                  <template v-else-if="k === 'note'">
                    <EffortNoteCell v-if="canEdit" :value="row.note" :disabled="saving.has(row.id)" :on-commit="(v) => saveField(row, { note: v })" />
                    <template v-else>{{ row.note ?? '—' }}</template>
                  </template>
                  <template v-else-if="k === 'project'">
                    <EffortSelectCell v-if="canEdit" :value="row.projectId" :options="projectOptionsFor(g.department.id)" :disabled="saving.has(row.id)" @change="saveField(row, { projectId: $event })" />
                    <template v-else>{{ row.projectName }}</template>
                  </template>
                  <span v-else-if="k === 'performance'" :title="row.performanceLevel?.name">{{ performanceLabel(row) }}</span>
                  <template v-else-if="k === 'otEffort'">{{ formatEffortNumber(row.otEffort) }}</template>
                  <template v-else-if="k === 'hd' || k === 'ttEffort'">
                    <EffortNumberCell
                      v-if="canEdit"
                      :value="row[k]"
                      :min="0"
                      :max="2"
                      :decimals="2"
                      :disabled="saving.has(row.id)"
                      :on-commit="(v) => saveField(row, { [k]: v })"
                    />
                    <template v-else>{{ formatEffortNumber(row[k]) }}</template>
                  </template>
                  <div v-else :class="cn(k === 'free' && row.free > FREE_EPSILON && 'flex flex-col items-end gap-0.5')">
                    {{ formatEffortNumber(row[k]) }}
                    <span v-if="k === 'free' && row.free > FREE_EPSILON" class="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-semibold text-amber-700">{{ $t('effort.lbl_free_badge') }}</span>
                  </div>
                </td>
                <td v-if="canEdit" class="px-3 py-1.5 whitespace-nowrap border-b border-slate-100 w-20">
                  <button
                    type="button"
                    :aria-label="$t('effort.btn_delete')"
                    :title="$t('effort.btn_delete')"
                    class="flex h-8 w-8 items-center justify-center rounded-lg border border-slate-200 bg-slate-50 text-slate-400 transition-colors hover:border-rose-200 hover:bg-rose-50 hover:text-rose-600"
                    @click="remove(row)"
                  >
                    <Trash2 :size="16" />
                  </button>
                </td>
              </tr>
              <tr v-if="canEdit && !search.trim()">
                <td :colspan="visibleCols.length + 3" class="p-0 border-b border-slate-100">
                  <div class="sticky left-0 w-fit min-h-9 flex items-center px-3 py-2">
                    <AddMemberPopover
                      :month="month"
                      :department="g.department"
                      :team-options="teamOptions"
                      :project-options="projectOptionsFor(g.department.id)"
                      :role-options="roleOptions"
                      :rows="data?.rows ?? []"
                    />
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </div>
  </PageLayout>
</template>
