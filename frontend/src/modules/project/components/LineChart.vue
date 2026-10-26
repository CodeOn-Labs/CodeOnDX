<script setup lang="ts">
// Biểu đồ đường SVG nhẹ dùng chung cho Chi tiết dự án (thay TradingView lightweight-charts / Recharts của bản gốc):
// nhiều đường trên cùng trục ngày, nhãn trục X theo đầu tháng (hoặc theo bước), di chuột hiện giá trị từng đường.
// Chiều rộng tối thiểu = số điểm × pointWidth → dữ liệu dài thì cuộn ngang (thay cho zoom/pan của bản gốc).
import { computed, ref } from 'vue';

export interface ChartSeries {
  key: string;
  label: string;
  color: string;
  values: number[];
  /** Vẽ dạng bậc thang (giá trị giữ nguyên tới điểm kế tiếp) — hợp với FTE theo ngày */
  step?: boolean;
}

const props = withDefaults(
  defineProps<{
    labels: string[];
    series: ChartSeries[];
    height?: number;
    pointWidth?: number;
    yMax?: number;
    /** Định dạng giá trị trong tooltip / trục Y */
    format?: (value: number) => string;
    /** Đánh dấu một nhãn (vd hôm nay) bằng vạch dọc */
    markLabel?: string | null;
    /** Nhãn trục X: 'month' = ngày đầu tháng, 'day' = mỗi 5 ngày */
    axis?: 'month' | 'day';
  }>(),
  { height: 120, pointWidth: 3, yMax: undefined, format: (v: number) => v.toFixed(2), markLabel: null, axis: 'month' },
);

const PAD = { top: 8, right: 8, bottom: 18, left: 34 };
const width = computed(() => Math.max(320, props.labels.length * props.pointWidth + PAD.left + PAD.right));
const plotW = computed(() => width.value - PAD.left - PAD.right);
const plotH = computed(() => props.height - PAD.top - PAD.bottom);
const maxY = computed(() => {
  if (props.yMax !== undefined) return props.yMax;
  const m = Math.max(0, ...props.series.flatMap((s) => s.values));
  if (m <= 0) return 1;
  const mag = 10 ** Math.floor(Math.log10(m));
  return Math.ceil((m * 1.1) / mag) * mag;
});
const x = (i: number) => PAD.left + (props.labels.length <= 1 ? 0 : (i / (props.labels.length - 1)) * plotW.value);
const y = (v: number) => PAD.top + plotH.value - (Math.min(v, maxY.value) / maxY.value) * plotH.value;

function path(s: ChartSeries) {
  let d = '';
  s.values.forEach((v, i) => {
    if (i === 0) d += `M${x(0)},${y(v)}`;
    else if (s.step) d += `H${x(i)}V${y(v)}`;
    else d += `L${x(i)},${y(v)}`;
  });
  return d;
}
const ticksY = computed(() => [0, 0.5, 1].map((r) => ({ value: maxY.value * r, y: y(maxY.value * r) })));
const ticksX = computed(() => props.labels
  .map((label, i) => ({ label, i }))
  .filter(({ label, i }) => (props.axis === 'month' ? label.endsWith('-01') || i === 0 : (i + 1) % 5 === 0 || i === 0))
  .map(({ label, i }) => ({ x: x(i), text: props.axis === 'month' ? `${label.slice(5, 7)}/${label.slice(2, 4)}` : String(Number(label.slice(8, 10))) })));
const markX = computed(() => {
  const i = props.markLabel ? props.labels.indexOf(props.markLabel) : -1;
  return i >= 0 ? x(i) : null;
});

const hover = ref<number | null>(null);
function onMove(e: MouseEvent) {
  const rect = (e.currentTarget as SVGElement).getBoundingClientRect();
  const px = ((e.clientX - rect.left) / rect.width) * width.value;
  const i = Math.round(((px - PAD.left) / Math.max(1, plotW.value)) * (props.labels.length - 1));
  hover.value = i >= 0 && i < props.labels.length ? i : null;
}
const tooltipLeft = computed(() => (hover.value === null ? 0 : Math.min(x(hover.value) + 8, width.value - 150)));
</script>

<template>
  <div class="relative overflow-x-auto no-scrollbar">
    <svg :width="width" :height="props.height" class="block select-none" @mousemove="onMove" @mouseleave="hover = null">
      <g v-for="tk in ticksY" :key="tk.value">
        <line :x1="PAD.left" :x2="width - PAD.right" :y1="tk.y" :y2="tk.y" stroke="#eef1f6" stroke-dasharray="3 3" />
        <text :x="PAD.left - 4" :y="tk.y + 3" text-anchor="end" class="fill-slate-400 text-[9px] font-semibold">{{ props.format(tk.value) }}</text>
      </g>
      <text v-for="tk in ticksX" :key="tk.x" :x="tk.x" :y="props.height - 4" text-anchor="middle" class="fill-slate-400 text-[9px] font-semibold">{{ tk.text }}</text>
      <line v-if="markX !== null" :x1="markX" :x2="markX" :y1="PAD.top" :y2="PAD.top + plotH" stroke="#8b5cf6" stroke-width="1" stroke-dasharray="4 3" />
      <path v-for="s in props.series" :key="s.key" :d="path(s)" fill="none" :stroke="s.color" stroke-width="1.6" stroke-linejoin="round" />
      <line v-if="hover !== null" :x1="x(hover)" :x2="x(hover)" :y1="PAD.top" :y2="PAD.top + plotH" stroke="#94a3b8" stroke-width="1" />
    </svg>
    <div
      v-if="hover !== null"
      class="pointer-events-none absolute top-1 z-10 min-w-[140px] rounded-lg border border-slate-200 bg-white/95 px-2.5 py-1.5 text-[11px] shadow-lg"
      :style="{ left: `${tooltipLeft}px` }"
    >
      <div class="mb-0.5 font-bold text-slate-700">{{ props.labels[hover] }}</div>
      <div v-for="s in props.series" :key="s.key" class="flex items-center justify-between gap-3">
        <span class="flex items-center gap-1.5 text-slate-500"><span class="h-[2px] w-3" :style="{ backgroundColor: s.color }" />{{ s.label }}</span>
        <span class="font-bold tabular-nums text-slate-800">{{ props.format(s.values[hover] ?? 0) }}</span>
      </div>
    </div>
  </div>
</template>
