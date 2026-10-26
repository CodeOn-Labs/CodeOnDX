<script setup lang="ts">
// Port RuleTable.tsx: tên + mô tả, tên biến, màn hình áp dụng, giá trị hiện tại, công tắc áp dụng, nút thiết lập.
import { computed, type Component } from 'vue';
import { useI18n } from 'vue-i18n';
import { ArrowUpRight, Briefcase, CalendarOff, Check, Clock, Key, Layout, Settings, Timer, UserCheck, Users, X } from '@lucide/vue';
import CommonTable from '@/shared/components/table/CommonTable.vue';
import SwitchToggle from '@/shared/components/form/SwitchToggle.vue';
import type { TableColumn } from '@/shared/components/table/types';
import type { PaginationController } from '@/shared/composables/usePagination';
import type { SystemRule } from '@/api/types';
import { enumLabel } from '@/shared/i18n/enum-label';

const props = defineProps<{ rules: SystemRule[]; loading: boolean; canManage: boolean; pagination: PaginationController }>();
const emit = defineEmits<{ edit: [rule: SystemRule]; toggleStatus: [rule: SystemRule] }>();

const { t } = useI18n();

const MODULE_STYLE: Record<string, { badge: string; icon: Component }> = {
  timesheet: { badge: 'bg-blue-50 text-blue-700 border-blue-100', icon: Clock },
  project: { badge: 'bg-emerald-50 text-emerald-700 border-emerald-100', icon: Briefcase },
  hr: { badge: 'bg-purple-50 text-purple-700 border-purple-100', icon: Users },
  leave: { badge: 'bg-amber-50 text-amber-700 border-amber-100', icon: CalendarOff },
  overtime: { badge: 'bg-rose-50 text-rose-700 border-rose-100', icon: Timer },
  attendance: { badge: 'bg-cyan-50 text-cyan-700 border-cyan-100', icon: UserCheck },
};
const moduleStyle = (module: string) => MODULE_STYLE[module] ?? { badge: 'bg-slate-50 text-slate-700 border-slate-100', icon: Layout };

const columns = computed<TableColumn[]>(() => [
  { id: 'name', header: t('admin_rule_list.col_name') },
  { id: 'key', header: t('admin_rule_list.col_key') },
  { id: 'module', header: t('admin_rule_list.col_module') },
  { id: 'value', header: t('admin_rule_list.col_value') },
  { id: 'apply', header: t('admin_rule_list.col_apply') },
  { id: 'actions', header: '', cellClass: 'text-right' },
]);

/** Quy tắc đang tắt vẫn hiện giá trị đã lưu nhưng gạch ngang (hệ thống bỏ qua quy tắc này). */
const displayValue = (rule: SystemRule) => rule.value ?? null;
</script>

<template>
  <div class="relative flex flex-1 flex-col min-h-0 overflow-hidden rounded-[24px] border border-slate-200/80 bg-white/60 shadow-xl shadow-slate-200/40 backdrop-blur-xl">
    <CommonTable
      :columns="columns"
      :rows="props.rules"
      :row-key="(rule) => rule.key"
      :loading="props.loading"
      :empty-message="$t('admin_rule_list.empty_rules')"
      :pagination="props.pagination"
      table-class="min-w-[900px]"
      container-class="flex-1 min-h-0"
      table-wrapper-class="border-none shadow-none bg-transparent"
      :row-class="() => 'transition-all duration-300 ease-in-out group/row hover:bg-slate-50/40 hover:!border-pr'"
    >
      <template #cell-name="{ row }">
        <div class="flex flex-col gap-1 py-1">
          <button
            type="button"
            class="group/link inline-flex items-center gap-1.5 text-left text-[13px] font-extrabold text-slate-900 transition-all duration-300 hover:text-pr outline-none focus-visible:ring-2 focus-visible:ring-pr/30 focus-visible:ring-offset-1 rounded-md px-1 -mx-1 w-fit cursor-pointer"
            @click="emit('edit', row)"
          >
            <span class="relative">
              {{ row.name }}
              <span class="absolute -bottom-0.5 left-0 h-[2px] w-0 bg-pr transition-all duration-500 group-hover/link:w-full" />
            </span>
            <ArrowUpRight
              :size="14"
              class="opacity-0 -translate-x-2 translate-y-1 transition-all duration-300 group-hover/link:opacity-100 group-hover/link:translate-x-0 group-hover/link:translate-y-0 text-pr"
            />
          </button>
          <span class="text-[11.5px] text-slate-400 leading-normal max-w-lg">{{ row.description }}</span>
        </div>
      </template>

      <template #cell-key="{ row }">
        <code class="text-[10.5px] font-mono font-bold text-slate-500 bg-slate-50 border border-slate-100 px-2 py-1 rounded-lg flex items-center gap-1.5 w-fit whitespace-nowrap max-w-full overflow-x-auto scrollbar-none">
          <Key :size="10" class="text-slate-400 shrink-0" />
          {{ row.key }}
        </code>
      </template>

      <template #cell-module="{ row }">
        <span :class="['text-[11px] font-bold uppercase tracking-wider border px-2 py-0.5 rounded-md flex items-center gap-1 w-fit', moduleStyle(row.module).badge]">
          <component :is="moduleStyle(row.module).icon" :size="10" class="stroke-[2.5]" />
          {{ enumLabel('rule_module', row.module) }}
        </span>
      </template>

      <template #cell-value="{ row }">
        <div :class="row.status === 0 ? 'opacity-45 line-through decoration-slate-400 decoration-[1.5px] flex items-center w-fit' : ''">
          <span
            v-if="displayValue(row) === null"
            class="text-[12.5px] font-bold text-slate-400 bg-slate-50 border border-slate-100 px-2 py-0.5 rounded-md"
          >
            {{ String(row.defaultValue) }} ({{ $t('admin_rule_list.lbl_default') }})
          </span>
          <template v-else-if="row.valueType === 'boolean'">
            <span
              v-if="row.value"
              class="inline-flex items-center gap-1.5 text-[13px] font-bold text-emerald-600 bg-emerald-50/50 border border-emerald-100/50 px-2.5 py-1 rounded-xl uppercase tracking-wide"
            >
              <Check :size="12" class="text-emerald-600" :stroke-width="3" />
              {{ $t('admin_rule_list.lbl_on') }}
            </span>
            <span
              v-else
              class="inline-flex items-center gap-1.5 text-[13px] font-bold text-slate-500 bg-slate-50/50 border border-slate-100/50 px-2.5 py-1 rounded-xl uppercase tracking-wide"
            >
              <X :size="12" class="text-slate-400" :stroke-width="3" />
              {{ $t('admin_rule_list.lbl_off') }}
            </span>
          </template>
          <span v-else class="text-[13px] font-bold text-indigo-600 bg-indigo-50/50 border border-indigo-100/50 px-2.5 py-1 rounded-xl">
            {{ String(row.value) }}
          </span>
        </div>
      </template>

      <template #cell-apply="{ row }">
        <!-- Quy tắc kiểu bật/tắt thì chính giá trị là công tắc — không cần công tắc "áp dụng" riêng (giống bản gốc) -->
        <SwitchToggle
          v-if="row.valueType !== 'boolean'"
          size="sm"
          :model-value="row.status !== 0"
          :disabled="!props.canManage"
          :label="row.status !== 0 ? $t('admin_rule_list.btn_turn_off') : $t('admin_rule_list.btn_turn_on')"
          @update:model-value="emit('toggleStatus', row)"
        />
      </template>

      <template #cell-actions="{ row }">
        <div v-if="props.canManage" class="flex justify-end items-center pr-1">
          <button
            type="button"
            class="p-2 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50/50 border border-transparent hover:border-indigo-100/55 rounded-xl transition-all hover:scale-105 active:scale-95 outline-none focus-visible:ring-2 focus-visible:ring-indigo-500/30 focus-visible:ring-offset-1 cursor-pointer"
            :title="$t('admin_rule_list.btn_edit')"
            :aria-label="$t('admin_rule_list.btn_edit')"
            @click.stop="emit('edit', row)"
          >
            <Settings :size="15" class="stroke-[2.2]" />
          </button>
        </div>
      </template>
    </CommonTable>
  </div>
</template>
