<script setup lang="ts">
// Port CategoryTable.tsx: danh mục (màu + tên + mã + mô tả), phân loại, trạng thái, thứ tự, thẻ hiển thị, thuộc tính bổ sung, ngày cập nhật.
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { ArrowUpRight, Settings, Trash2 } from '@lucide/vue';
import CommonTable from '@/shared/components/table/CommonTable.vue';
import type { TableColumn } from '@/shared/components/table/types';
import type { PaginationController } from '@/shared/composables/usePagination';
import type { Category } from '@/modules/admin/api/config.api';
import { CATEGORY_TYPE_STYLE, RESERVED_META_KEYS } from './category-meta';
import { enumLabel } from '@/shared/i18n/enum-label';
import { badgeStyle, resolveColor } from '@/shared/utils/color';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ items: Category[]; loading: boolean; pagination: PaginationController }>();
const emit = defineEmits<{ edit: [item: Category]; delete: [item: Category] }>();

const { t, locale } = useI18n();

const columns = computed<TableColumn[]>(() => [
  { id: 'name', header: t('admin_category_list.col_name') },
  { id: 'type', header: t('admin_category_list.col_type') },
  { id: 'status', header: t('admin_category_list.col_status') },
  { id: 'sortSeq', header: t('admin_category_list.col_sort'), headerClass: 'text-center', cellClass: 'text-center' },
  { id: 'color', header: t('admin_category_list.col_color') },
  { id: 'metadata', header: t('admin_category_list.col_metadata') },
  { id: 'updatedAt', header: t('admin_category_list.col_updated') },
  { id: 'actions', header: '', cellClass: 'text-right' },
]);

const displayName = (item: Category) => (locale.value === 'en' && item.nameEn ? item.nameEn : item.name);
/** Thuộc tính mở rộng hiển thị ở cột "Cấu hình bổ sung" — hệ số hiệu suất đứng đầu. */
function extraMeta(item: Category): Array<[string, unknown]> {
  const entries = Object.entries(item.metadata).filter(([k]) => !RESERVED_META_KEYS.includes(k));
  return item.metadata.coefficient !== undefined ? [['coefficient', item.metadata.coefficient], ...entries] : entries;
}
</script>

<template>
  <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
    <CommonTable
      :columns="columns"
      :rows="props.items"
      :loading="props.loading"
      :empty-message="$t('admin_category_list.empty_categories')"
      :pagination="props.pagination"
      table-class="min-w-[1000px]"
      container-class="flex-1 min-h-0"
      table-wrapper-class="border-none shadow-none bg-transparent"
      :row-class="(row) => cn('cursor-pointer transition-all duration-300 ease-in-out group/row', row.isActive ? 'hover:bg-slate-50/40 hover:!border-emerald-500' : 'hover:bg-slate-50/40 hover:!border-slate-300')"
      @row-click="(row) => emit('edit', row)"
    >
      <template #cell-name="{ row }">
        <button
          type="button"
          class="flex flex-col gap-1 py-1 cursor-pointer max-w-[280px] text-left w-full outline-none focus-visible:ring-2 focus-visible:ring-pr/30 focus-visible:ring-offset-1 rounded-md px-1 -mx-1"
          @click.stop="emit('edit', row)"
        >
          <div class="inline-flex items-center gap-2 flex-wrap">
            <div
              v-if="row.color"
              class="h-2.5 w-2.5 rounded-full shadow-sm flex-shrink-0 transition-all duration-300 group-hover/row:scale-125"
              :style="{ backgroundColor: resolveColor(row.color), boxShadow: `0 0 6px ${resolveColor(row.color)}66` }"
            />
            <span class="text-[13px] font-extrabold text-slate-800 group-hover/row:text-indigo-600 transition-all duration-300 relative leading-tight">
              {{ displayName(row) }}
            </span>
            <ArrowUpRight
              :size="13"
              class="opacity-0 -translate-x-1 translate-y-1 transition-all duration-300 group-hover/row:opacity-100 group-hover/row:translate-x-0 group-hover/row:translate-y-0 text-indigo-500"
            />
          </div>
          <div class="flex items-center gap-1.5 mt-0.5">
            <span class="text-[9px] text-slate-500 font-mono font-black bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200/50 uppercase tracking-wide">
              {{ $t('admin_category_list.lbl_code_prefix', { code: row.code }) }}
            </span>
          </div>
          <span v-if="row.description" class="text-[11px] text-slate-400 font-medium line-clamp-1 mt-0.5" :title="row.description">{{ row.description }}</span>
          <span v-else class="text-[10px] text-slate-300 italic font-medium mt-0.5">{{ $t('admin_category_list.empty_description') }}</span>
        </button>
      </template>

      <template #cell-type="{ row }">
        <div
          class="inline-flex items-center gap-2 px-3 py-1 rounded-xl text-[11px] font-extrabold tracking-tight border transition-all duration-300"
          :style="{
            backgroundColor: `${CATEGORY_TYPE_STYLE[row.type].color}0a`,
            color: CATEGORY_TYPE_STYLE[row.type].color,
            borderColor: `${CATEGORY_TYPE_STYLE[row.type].color}1c`,
            boxShadow: `0 2px 8px -2px ${CATEGORY_TYPE_STYLE[row.type].color}15`,
          }"
        >
          <component :is="CATEGORY_TYPE_STYLE[row.type].icon" :size="12" class="shrink-0 stroke-[2.5]" />
          <span>{{ enumLabel('category_type', row.type) }}</span>
        </div>
      </template>

      <template #cell-status="{ row }">
        <div class="flex items-center py-1">
          <span
            :class="cn(
              'inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-xl text-[10.5px] font-extrabold border shadow-sm transition-all duration-300',
              row.isActive ? 'bg-emerald-500/10 text-emerald-700 border-emerald-500/20' : 'bg-slate-500/10 text-slate-500 border-slate-500/20',
            )"
          >
            <span :class="cn('w-1.5 h-1.5 rounded-full shrink-0', row.isActive ? 'bg-emerald-500 animate-pulse' : 'bg-slate-400')" />
            <span>{{ row.isActive ? $t('admin_category_list.lbl_active') : $t('admin_category_list.lbl_inactive') }}</span>
          </span>
        </div>
      </template>

      <template #cell-sortSeq="{ row }">
        <div class="flex items-center justify-center py-1">
          <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[11px] font-extrabold border bg-indigo-50/40 text-indigo-700 border-indigo-100/40 shadow-sm shadow-indigo-100/10 tracking-tight transition-all duration-300 group-hover/row:scale-105">
            <span class="w-1.5 h-1.5 rounded-full bg-indigo-500 shrink-0" />
            <span>{{ row.sortSeq }}</span>
          </span>
        </div>
      </template>

      <template #cell-color="{ row }">
        <span v-if="!row.color" class="text-slate-300 text-[11px] font-medium">–</span>
        <div v-else class="flex flex-col gap-1 py-1">
          <span
            class="inline-flex items-center gap-1.5 rounded-xl px-3 py-1 text-[11px] font-bold tracking-tight border border-transparent transition-all duration-300 w-fit shadow-sm"
            :style="badgeStyle(row.color)"
          >
            <span class="w-1.5 h-1.5 rounded-full shrink-0 animate-pulse" :style="{ backgroundColor: resolveColor(row.color) }" />
            <span class="leading-none">{{ displayName(row) }}</span>
          </span>
          <span class="text-[9.5px] text-slate-400 font-extrabold tracking-wider uppercase px-1">
            {{ row.color.startsWith('#') ? row.color : enumLabel('color_preset', row.color) }}
          </span>
        </div>
      </template>

      <template #cell-metadata="{ row }">
        <span v-if="extraMeta(row).length === 0" class="text-slate-300 text-[11px] font-medium">–</span>
        <div v-else class="flex flex-wrap gap-1.5 max-w-[300px] py-1">
          <div
            v-for="[key, val] in extraMeta(row)"
            :key="key"
            class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-xl text-[10.5px] font-mono border border-slate-200/60 bg-slate-50/40 text-slate-600 shadow-sm transition-all duration-300 hover:border-slate-300 hover:bg-slate-50"
          >
            <span class="font-extrabold text-slate-500">{{ key }}:</span>
            <span
              v-if="typeof val === 'boolean'"
              :class="cn(
                'inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded-lg text-[9px] font-black uppercase tracking-wider border',
                val ? 'bg-emerald-500/10 text-emerald-700 border-emerald-500/20' : 'bg-rose-500/10 text-rose-700 border-rose-500/20',
              )"
            >
              {{ val ? 'TRUE' : 'FALSE' }}
            </span>
            <span v-else class="text-slate-800 font-extrabold truncate max-w-[120px]" :title="String(val)">{{ String(val) }}</span>
          </div>
        </div>
      </template>

      <template #cell-updatedAt="{ row }">
        <span v-if="!row.updatedAt" class="text-slate-300 text-[11px] font-medium">–</span>
        <div v-else class="flex flex-col py-1">
          <span class="text-[12px] font-bold text-slate-700 leading-none">{{ formatDate(row.updatedAt) }}</span>
          <span class="text-[9px] text-slate-400 font-medium font-mono mt-0.5">{{ formatDate(row.updatedAt, 'HH:mm') }}</span>
        </div>
      </template>

      <template #cell-actions="{ row }">
        <div class="flex justify-end gap-2 pr-1">
          <button
            type="button"
            class="p-2 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50/50 border border-transparent hover:border-indigo-100/55 rounded-xl transition-all hover:scale-105 active:scale-95 outline-none focus-visible:ring-2 focus-visible:ring-indigo-500/30 focus-visible:ring-offset-1"
            :title="$t('admin_category_list.btn_edit')"
            :aria-label="$t('admin_category_list.btn_edit')"
            @click.stop="emit('edit', row)"
          >
            <Settings :size="15" class="stroke-[2.2]" />
          </button>
          <button
            v-if="!row.isSystem"
            type="button"
            class="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50/50 border border-transparent hover:border-rose-100/55 rounded-xl transition-all hover:scale-105 active:scale-95 outline-none focus-visible:ring-2 focus-visible:ring-rose-500/30 focus-visible:ring-offset-1"
            :title="$t('admin_category_list.btn_delete')"
            :aria-label="$t('admin_category_list.btn_delete')"
            @click.stop="emit('delete', row)"
          >
            <Trash2 :size="15" class="stroke-[2.2]" />
          </button>
        </div>
      </template>
    </CommonTable>
  </div>
</template>
