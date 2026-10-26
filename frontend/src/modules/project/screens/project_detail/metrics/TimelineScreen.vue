<script setup lang="ts">
// Port ProjectTimelineScreen + TimelinessCard + ProjectTimelineOverview + ProjectMilestoneTimeline:
// tỷ lệ bàn giao đúng hạn trong khoảng chọn (BE tính), thanh timeline toàn dự án tô màu theo trạng thái mốc + vạch hôm nay,
// sửa nhanh ngày dự án, danh sách milestone (thêm / sửa / đánh dấu bàn giao / giải trình trễ hạn / xoá).
// Trạng thái mốc do BE tính: done-ontime, done-late, overdue, next, upcoming (port milestoneViewModel.ts).
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { CalendarDays, Check, Flag, Pencil, Plus, Save, Trash2, X } from '@lucide/vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import BaseTextarea from '@/shared/components/form/BaseTextarea.vue';
import Button from '@/shared/components/Button.vue';
import { projectDetailApi, type Milestone, type MilestoneInput, type MilestoneStatus, type ProjectDetail } from '@/modules/project/api/project-detail.api';
import { firstFieldErrors } from '@/api/http';
import { useConfirmStore } from '@/stores/confirm';
import { useToastStore } from '@/stores/toast';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ project: ProjectDetail }>();
const { t } = useI18n();
const toast = useToastStore();
const confirm = useConfirmStore();
const queryClient = useQueryClient();

const range = ref<{ from: string; to: string }>({ from: '', to: '' });
const query = useQuery({
  queryKey: computed(() => ['projects', 'milestones', props.project.id, range.value.from, range.value.to] as const),
  queryFn: ({ signal }) => projectDetailApi.milestones(props.project.id, { from: range.value.from || undefined, to: range.value.to || undefined }, { signal }),
  placeholderData: (prev) => prev,
});
const data = computed(() => query.data.value);
watch(data, (d) => {
  if (d && !range.value.from) range.value = { from: d.timeliness.from, to: d.timeliness.to };
}, { immediate: true });
const canEdit = computed(() => !!data.value?.canEdit);
const refresh = () => {
  void queryClient.invalidateQueries({ queryKey: ['projects', 'milestones', props.project.id] });
  void queryClient.invalidateQueries({ queryKey: ['ai', 'insights'] });
};

const STATUS_COLORS: Record<MilestoneStatus, string> = { 'done-ontime': '#34d399', 'done-late': '#fbbf24', overdue: '#fb7185', next: '#8b5cf6', upcoming: '#cbd5e1' };
const statusLabel = (m: Milestone) => ({
  'done-ontime': t('project_detail.timeline_section.lbl_ms_done_ontime'),
  'done-late': t('project_detail.timeline_section.lbl_ms_done_late', { days: m.delayDays }),
  overdue: t('project_detail.timeline_section.lbl_ms_overdue', { days: m.delayDays }),
  next: t('project_detail.timeline_section.lbl_ms_next'),
  upcoming: t('project_detail.timeline_section.lbl_ms_upcoming'),
})[m.status];
const legend = computed<Array<{ status: MilestoneStatus; label: string }>>(() => [
  { status: 'done-ontime', label: t('project_detail.timeline_section.lbl_ms_done_ontime') }, { status: 'done-late', label: t('project_detail.timeline_section.lbl_ms_legend_done_late') },
  { status: 'overdue', label: t('project_detail.timeline_section.lbl_ms_legend_overdue') }, { status: 'next', label: t('project_detail.timeline_section.lbl_ms_next') }, { status: 'upcoming', label: t('project_detail.timeline_section.lbl_ms_upcoming') },
]);

// ─── Thanh timeline toàn dự án ───
const ms = (d: string) => Date.parse(`${d}T00:00:00Z`);
/**
 * Mỗi mốc là 1 đoạn từ mốc trước (hoặc ngày bắt đầu dự án) đến ngày dự kiến của nó, kẹp trong [bắt đầu, kết thúc]
 * để tổng độ rộng không vượt 100%; phần sau mốc cuối tới ngày kết thúc tô màu "sắp tới".
 */
const bar = computed(() => {
  const d = data.value;
  if (!d) return null;
  const start = ms(d.startDate);
  const end = ms(d.endDate);
  const total = end - start;
  if (total <= 0) return null;
  const clamp = (x: string) => Math.min(end, Math.max(start, ms(x)));
  const sorted = [...d.items].sort((a, b) => a.plannedDate.localeCompare(b.plannedDate));
  let cursor = start;
  const segments = sorted.map((m) => {
    const to = clamp(m.plannedDate);
    const seg = { id: m.id, label: m.name, width: Math.max(0, ((to - cursor) / total) * 100), color: STATUS_COLORS[m.status] };
    cursor = Math.max(cursor, to);
    return seg;
  });
  const tail = Math.max(0, ((end - cursor) / total) * 100);
  const elapsed = ((ms(d.today) - start) / total) * 100;
  return { segments, tail, elapsed: elapsed >= 0 && elapsed <= 100 ? elapsed : null, daysLeft: Math.round((end - ms(d.today)) / 86_400_000) };
});

const editingDates = ref(false);
const draftStart = ref('');
const draftEnd = ref('');
function startEditDates() {
  draftStart.value = props.project.startDate ?? '';
  draftEnd.value = props.project.endDate ?? '';
  editingDates.value = true;
}
async function saveDates() {
  try {
    toast.success((await projectDetailApi.updateDates(props.project.id, draftStart.value || null, draftEnd.value || null)).messageText);
    editingDates.value = false;
    void queryClient.invalidateQueries({ queryKey: ['projects'] });
  } catch (err) {
    toast.error(Object.values(firstFieldErrors(err))[0] ?? (err as Error).message);
  }
}

// ─── Milestone ───
const editingId = ref<string | null>(null);
const draft = ref<MilestoneInput>({ name: '', plannedDate: null, actualDate: null, justification: '' });
const errors = ref<Record<string, string>>({});
const saving = ref(false);
const adding = ref(false);
function startEdit(m: Milestone | null) {
  errors.value = {};
  editingId.value = m?.id ?? null;
  adding.value = !m;
  draft.value = m ? { name: m.name, plannedDate: m.plannedDate, actualDate: m.actualDate, justification: m.justification ?? '' } : { name: '', plannedDate: null, actualDate: null, justification: '' };
}
function cancel() {
  editingId.value = null;
  adding.value = false;
}
async function save() {
  saving.value = true;
  try {
    const res = editingId.value ? await projectDetailApi.milestone.update(props.project.id, editingId.value, draft.value) : await projectDetailApi.milestone.create(props.project.id, draft.value);
    toast.success(res.messageText);
    cancel();
    refresh();
  } catch (err) {
    errors.value = firstFieldErrors(err);
    if (Object.keys(errors.value).length === 0) toast.error((err as Error).message);
  } finally {
    saving.value = false;
  }
}
async function markDelivered(m: Milestone) {
  try {
    toast.success((await projectDetailApi.milestone.update(props.project.id, m.id, { name: m.name, plannedDate: m.plannedDate, actualDate: new Date().toISOString().slice(0, 10), justification: m.justification ?? '' })).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
async function remove(m: Milestone) {
  const ok = await confirm.ask({ type: 'danger', title: t('project_detail.confirm_delete_milestone_title'), description: t('project_detail.confirm_delete_milestone_desc', { name: m.name }), confirmText: t('project_detail.btn_confirm_delete') });
  if (!ok) return;
  try {
    toast.success((await projectDetailApi.milestone.remove(props.project.id, m.id)).messageText);
    refresh();
  } catch (err) {
    toast.error((err as Error).message);
  }
}
const lateDraft = computed(() => !!draft.value.actualDate && !!draft.value.plannedDate && draft.value.actualDate > draft.value.plannedDate);
const DATE_INPUT = 'rounded-lg border border-slate-200 px-2 py-1 text-[12px] font-semibold text-slate-600 outline-none focus:border-violet-400 focus:ring-2 focus:ring-violet-100';
</script>

<template>
  <div v-if="!props.project.startDate || !props.project.endDate" class="flex flex-col items-center gap-2 rounded-2xl border border-dashed border-slate-200 bg-white px-6 py-10 text-center">
    <CalendarDays :size="28" class="text-slate-300" />
    <p class="text-[13px] font-bold text-slate-600">{{ $t('project_detail.timeline_section.empty_dates_title') }}</p>
    <p class="text-[12px] text-slate-400">{{ $t('project_detail.timeline_section.empty_dates_desc') }}</p>
  </div>
  <div v-else-if="data" class="flex flex-col gap-5">
    <!-- Timeliness -->
    <div class="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div class="flex flex-col gap-1">
        <span class="text-[11px] font-bold uppercase tracking-wide text-slate-400">{{ $t('project_detail.timeline_section.lbl_timeliness_title') }}</span>
        <div class="flex items-center gap-1.5 text-[12px] font-semibold text-slate-500">
          {{ $t('project_detail.timeline_section.lbl_timeliness_from') }}
          <input v-model="range.from" type="date" :max="range.to" :class="DATE_INPUT" />
          {{ $t('project_detail.timeline_section.lbl_timeliness_to') }}
          <input v-model="range.to" type="date" :min="range.from" :class="DATE_INPUT" />
        </div>
      </div>
      <div class="text-right">
        <div :class="cn('text-[28px] font-black tracking-tight', data.timeliness.percent === null ? 'text-slate-300' : data.timeliness.percent >= 90 ? 'text-emerald-600' : data.timeliness.percent >= 70 ? 'text-amber-500' : 'text-rose-600')">
          {{ data.timeliness.percent === null ? '—' : `${data.timeliness.percent}%` }}
        </div>
        <div class="text-[11.5px] font-semibold text-slate-500">{{ $t('project_detail.timeline_section.lbl_timeliness_ratio', { onTime: data.timeliness.onTimeCount, due: data.timeliness.dueCount }) }}</div>
        <div class="text-[10.5px] text-slate-400">{{ $t('project_detail.timeline_section.lbl_timeliness_hint') }}</div>
      </div>
    </div>

    <!-- Timeline toàn dự án -->
    <section class="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <h3 class="text-[13px] font-black text-slate-800">{{ $t('project_detail.timeline_section.lbl_overview_title') }}</h3>
      <div class="flex flex-wrap items-center justify-between gap-2">
        <div class="flex items-center gap-2 text-[13px] font-bold text-slate-600">
          <CalendarDays :size="15" class="text-slate-400" />
          <div v-if="editingDates" class="flex items-center gap-1.5">
            <input v-model="draftStart" type="date" :class="DATE_INPUT" />
            <span class="text-slate-300">–</span>
            <input v-model="draftEnd" type="date" :class="DATE_INPUT" />
          </div>
          <span v-else>{{ formatDate(props.project.startDate) }} – {{ formatDate(props.project.endDate) }}</span>
        </div>
        <div class="flex items-center gap-2">
          <span v-if="!editingDates && bar" :class="cn('rounded-full px-3 py-1 text-[11.5px] font-bold', bar.daysLeft < 0 ? 'bg-rose-50 text-rose-600' : bar.daysLeft <= 14 ? 'bg-amber-50 text-amber-600' : 'bg-emerald-50 text-emerald-600')">
            {{ bar.daysLeft < 0 ? $t('project_detail.timeline_section.lbl_overdue_days', { days: Math.abs(bar.daysLeft) }) : $t('project_detail.timeline_section.lbl_days_left', { days: bar.daysLeft }) }}
          </span>
          <template v-if="canEdit">
            <div v-if="editingDates" class="flex items-center gap-1">
              <button type="button" class="flex h-7 w-7 items-center justify-center rounded-lg text-emerald-600 hover:bg-emerald-50" :aria-label="$t('common.btn_save')" @click="saveDates"><Save :size="14" /></button>
              <button type="button" class="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-50" :aria-label="$t('common.btn_cancel')" @click="editingDates = false"><X :size="14" /></button>
            </div>
            <button v-else type="button" class="flex items-center gap-1.5 rounded-full bg-violet-50 px-3 py-1 text-[11.5px] font-bold text-violet-600 transition-colors hover:bg-violet-100" @click="startEditDates">
              <Pencil :size="12" :stroke-width="2.5" />{{ $t('common.btn_edit') }}
            </button>
          </template>
        </div>
      </div>
      <div v-if="bar" class="relative pt-5 pb-1">
        <template v-if="bar.elapsed !== null">
          <span class="absolute top-5 h-2 w-[2px] -translate-x-1/2 bg-violet-600 transition-all" :style="{ left: `${bar.elapsed}%` }" />
          <span class="absolute top-0 whitespace-nowrap rounded-full bg-violet-600 px-2 py-0.5 text-[10px] font-bold text-white shadow-sm transition-all" :style="{ left: `${bar.elapsed}%`, transform: `translateX(${bar.elapsed < 8 ? '0%' : bar.elapsed > 92 ? '-100%' : '-50%'})` }">{{ $t('project_detail.lbl_today') }}</span>
        </template>
        <div class="flex h-3 w-full overflow-hidden rounded-full border border-slate-200 bg-slate-100">
          <div v-for="s in bar.segments" :key="s.id" class="h-full" :title="s.label" :style="{ width: `${s.width}%`, backgroundColor: s.color }" />
          <div v-if="bar.tail > 0" class="h-full" :style="{ width: `${bar.tail}%`, backgroundColor: STATUS_COLORS.upcoming }" />
        </div>
      </div>
      <div class="flex flex-wrap items-center gap-x-3.5 gap-y-1 text-[11px] font-semibold text-slate-400">
        <span v-for="l in legend" :key="l.status" class="flex items-center gap-1.5"><span class="h-2 w-2 rounded-full" :style="{ backgroundColor: STATUS_COLORS[l.status] }" />{{ l.label }}</span>
      </div>
    </section>

    <!-- Milestone -->
    <section class="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div class="flex items-center justify-between">
        <h3 class="text-[13px] font-black text-slate-800">{{ $t('project_detail.timeline_section.lbl_milestone_title') }}</h3>
        <Button v-if="canEdit && !adding" variant="outline" size="sm" @click="startEdit(null)"><template #leftIcon><Plus :size="14" /></template>{{ $t('project_detail.btn_add_milestone') }}</Button>
      </div>
      <p v-if="data.items.length === 0 && !adding" class="py-6 text-center text-[13px] font-bold text-slate-400">{{ $t('project_detail.empty_milestones') }}</p>
      <ol class="flex flex-col gap-2">
        <li v-for="m in data.items" :key="m.id" class="rounded-xl border border-slate-100 p-3">
          <div v-if="editingId !== m.id" class="flex flex-wrap items-start gap-3">
            <span class="mt-1 h-3 w-3 shrink-0 rounded-full" :style="{ backgroundColor: STATUS_COLORS[m.status] }" />
            <div class="min-w-0 flex-1">
              <div class="text-[13px] font-bold text-slate-800">{{ m.name }}</div>
              <div class="text-[11.5px] text-slate-500">
                {{ $t('project_detail.timeline_section.lbl_ms_planned_label', { date: formatDate(m.plannedDate) }) }}<template v-if="m.actualDate"> • {{ $t('project_detail.timeline_section.lbl_ms_actual_label', { date: formatDate(m.actualDate) }) }}</template>
              </div>
              <p v-if="m.justification" class="mt-1 whitespace-pre-line rounded-lg bg-amber-50/60 px-2 py-1 text-[11.5px] text-amber-800">
                {{ m.justification }}<span v-if="m.justificationBy" class="text-amber-600/70"> — {{ m.justificationBy }}</span>
              </p>
            </div>
            <span class="rounded-full px-2.5 py-0.5 text-[11px] font-bold" :style="{ backgroundColor: `${STATUS_COLORS[m.status]}26`, color: m.status === 'upcoming' ? '#64748b' : STATUS_COLORS[m.status] }">{{ statusLabel(m) }}</span>
            <div v-if="canEdit" class="flex items-center gap-1">
              <button v-if="!m.actualDate" type="button" class="flex h-7 items-center gap-1 rounded-lg px-2 text-[11px] font-bold text-emerald-600 hover:bg-emerald-50" :title="$t('project_detail.btn_mark_delivered')" @click="markDelivered(m)"><Flag :size="13" />{{ $t('project_detail.btn_mark_delivered') }}</button>
              <button type="button" class="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100" :aria-label="$t('common.btn_edit')" @click="startEdit(m)"><Pencil :size="13" /></button>
              <button type="button" class="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-rose-50 hover:text-rose-500" :aria-label="$t('common.btn_delete')" @click="remove(m)"><Trash2 :size="13" /></button>
            </div>
          </div>
          <div v-else class="flex flex-col gap-2">
            <BaseInput v-model="draft.name" :placeholder="$t('project_detail.ph_milestone_name')" :error="!!errors.name" />
            <div class="flex flex-wrap items-center gap-3 text-[12px] font-semibold text-slate-500">
              <label class="flex items-center gap-1.5">{{ $t('project_detail.timeline_section.lbl_ms_planned') }}<input v-model="draft.plannedDate" type="date" :class="DATE_INPUT" /></label>
              <label class="flex items-center gap-1.5">{{ $t('project_detail.timeline_section.lbl_ms_actual') }}<input v-model="draft.actualDate" type="date" :class="DATE_INPUT" /></label>
              <button v-if="draft.actualDate" type="button" class="text-[11px] font-bold text-slate-400 hover:text-rose-500" @click="draft.actualDate = null">{{ $t('project_detail.timeline_section.lbl_ms_clear_actual') }}</button>
            </div>
            <div v-if="lateDraft || draft.justification">
              <span class="text-[11px] font-bold text-amber-700">{{ $t('project_detail.timeline_section.lbl_ms_justification') }}</span>
              <BaseTextarea v-model="draft.justification" :rows="2" :placeholder="$t('project_detail.ph_justification')" :error="!!errors.justification" />
            </div>
            <p v-for="(e, k) in errors" :key="k" class="text-[11.5px] font-semibold text-rose-600">{{ e }}</p>
            <div class="flex justify-end gap-2">
              <Button variant="outline" size="sm" @click="cancel">{{ $t('common.btn_cancel') }}</Button>
              <Button variant="primary" size="sm" :loading="saving" @click="save"><template #leftIcon><Check :size="14" /></template>{{ $t('common.btn_save') }}</Button>
            </div>
          </div>
        </li>
        <li v-if="adding" class="flex flex-col gap-2 rounded-xl border border-violet-200 bg-violet-50/30 p-3">
          <BaseInput v-model="draft.name" :placeholder="$t('project_detail.ph_milestone_name')" :error="!!errors.name" />
          <label class="flex items-center gap-1.5 text-[12px] font-semibold text-slate-500">{{ $t('project_detail.timeline_section.lbl_ms_planned') }}<input v-model="draft.plannedDate" type="date" :class="DATE_INPUT" /></label>
          <p v-for="(e, k) in errors" :key="k" class="text-[11.5px] font-semibold text-rose-600">{{ e }}</p>
          <div class="flex justify-end gap-2">
            <Button variant="outline" size="sm" @click="cancel">{{ $t('common.btn_cancel') }}</Button>
            <Button variant="primary" size="sm" :loading="saving" @click="save"><template #leftIcon><Plus :size="14" /></template>{{ $t('project_detail.btn_add_milestone') }}</Button>
          </div>
        </li>
      </ol>
    </section>
  </div>
  <div v-else class="h-40 animate-pulse rounded-2xl bg-[#e8ecf5]" />
</template>
