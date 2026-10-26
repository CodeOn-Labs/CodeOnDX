<script setup lang="ts">
// Port ProjectFilterBar.tsx: tiêu đề + Import/Export/Tạo dự án, ô tìm kiếm, bộ lọc nâng cao, chip bộ lọc đang áp dụng.
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { Plus, Search, Upload, X } from '@lucide/vue';
import Button from '@/shared/components/Button.vue';
import FilterPopover from '@/shared/components/filter/FilterPopover.vue';
import FilterChips from '@/shared/components/filter/FilterChips.vue';
import FormSelect from '@/shared/components/form/FormSelect.vue';
import FormDatePicker from '@/shared/components/form/FormDatePicker.vue';
import type { ActiveFilter } from '@/shared/components/filter/types';
import type { SelectOption } from '@/shared/components/form/types';
import type { ProjectFilters } from '@/modules/project/api/project.api';

const props = defineProps<{
  activeFilters: ActiveFilter[];
  advancedFilterCount: number;
  isFetching: boolean;
  canManage: boolean;
  canCreate: boolean;
  clientOptions: SelectOption[];
  statusOptions: SelectOption[];
  gradeOptions: SelectOption[];
  riskOptions: SelectOption[];
}>();
const draft = defineModel<ProjectFilters>('draft', { required: true });
const filterOpen = defineModel<boolean>('filterOpen', { default: false });
const emit = defineEmits<{
  search: [];
  reset: [];
  applyFilters: [];
  clearFilter: [id: string];
  showImport: [];
  newProject: [];
}>();

function setField<K extends keyof ProjectFilters>(key: K, value: ProjectFilters[K]) {
  draft.value = { ...draft.value, [key]: value };
}
const asArray = (value: string | string[] | null) => (Array.isArray(value) ? value : value ? [value] : []);

const SELECT_TRIGGER = 'h-10 rounded-xl text-[13px] border-slate-200';
const { t } = useI18n();
const selects = computed(() => [
  { key: 'clientIds' as const, label: t('project_list.lbl_client'), placeholder: t('project_list.ph_all_clients'), options: props.clientOptions },
  { key: 'statusIds' as const, label: t('project_list.lbl_status'), placeholder: t('project_list.ph_all_statuses'), options: props.statusOptions },
  { key: 'gradeIds' as const, label: t('project_list.lbl_grade'), placeholder: t('project_list.ph_all_grades'), options: props.gradeOptions },
  { key: 'riskLevelIds' as const, label: t('project_list.lbl_risk'), placeholder: t('project_list.ph_all_risks'), options: props.riskOptions },
]);
</script>

<template>
  <div class="flex flex-col gap-3 rounded-2xl bg-white/60 backdrop-blur-xl p-3 lg:p-4 shadow-xl shadow-slate-200/40 border border-white/60 transition-all duration-300">
    <div class="flex items-center justify-between">
      <div class="flex flex-col gap-1">
        <div class="flex items-center gap-2">
          <div class="h-2 w-8 rounded-full bg-gradient-to-r from-pr to-bl" />
          <span class="text-[10px] font-black uppercase tracking-[0.2em] text-slate-400">{{ $t('project_list.lbl_kicker') }}</span>
        </div>
        <h1 class="text-[24px] font-black tracking-tight bg-gradient-to-br from-slate-900 to-slate-600 bg-clip-text text-transparent">
          {{ $t('project_list.title_page') }}
        </h1>
      </div>

      <div class="flex items-center gap-3">
        <div v-if="props.canManage" class="flex items-center gap-2 rounded-2xl bg-slate-100/50 p-1 backdrop-blur-md">
          <Button variant="ghost" size="sm" class="h-8 px-3 text-[12px] font-semibold text-slate-600 hover:bg-white hover:text-pr hover:shadow-sm" @click="emit('showImport')">
            <template #leftIcon><Upload :size="14" /></template>
            {{ $t('project_list.btn_import') }}
          </Button>
          <slot name="export" />
        </div>
        <Button v-if="props.canCreate" variant="primary" class="h-9 rounded-xl px-5 text-[13px] font-bold shadow-lg shadow-pr/25" @click="emit('newProject')">
          <template #leftIcon><Plus :size="16" :stroke-width="3" /></template>
          {{ $t('project_list.btn_create') }}
        </Button>
      </div>
    </div>

    <form @submit.prevent="emit('search')">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="group relative flex items-center">
            <Search :size="16" class="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 transition-colors group-focus-within:text-pr" />
            <input
              type="text"
              :value="draft.search"
              :placeholder="$t('project_list.ph_search')"
              :aria-label="$t('project_list.ph_search')"
              class="h-9 lg:h-10 w-72 lg:w-80 rounded-xl border border-slate-200 bg-white pl-10 pr-9 text-[13.5px] font-medium text-slate-900 outline-none transition-all placeholder:text-slate-400 focus:border-pr focus:ring-4 focus:ring-pr/10 lg:px-5 lg:pl-11"
              @input="setField('search', ($event.target as HTMLInputElement).value)"
            />
            <button
              v-if="draft.search"
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 rounded-full p-0.5 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
              :aria-label="$t('project_list.btn_clear_search')"
              @click="setField('search', '')"
            >
              <X :size="13" />
            </button>
          </div>

          <FilterPopover
            v-model:open="filterOpen"
            :active-count="props.advancedFilterCount"
            :is-applying="props.isFetching"
            @apply="emit('applyFilters')"
            @reset="emit('reset')"
          >
            <div class="flex flex-col gap-4 p-1">
              <FormSelect
                v-for="select in selects"
                :key="select.key"
                :label="select.label"
                :options="select.options"
                :placeholder="select.placeholder"
                :trigger-class="SELECT_TRIGGER"
                :model-value="draft[select.key]"
                searchable
                allow-clear
                multiple
                hide-message
                @update:model-value="setField(select.key, asArray($event))"
              />

              <div class="flex flex-col gap-1.5">
                <span class="text-[11px] font-bold uppercase tracking-widest text-slate-500">{{ $t('project_list.lbl_period') }}</span>
                <div class="grid grid-cols-2 gap-3">
                  <FormDatePicker
                    :model-value="draft.startFrom"
                    :placeholder="$t('project_list.ph_from_date')"
                    :class="SELECT_TRIGGER"
                    hide-message
                    @update:model-value="setField('startFrom', $event)"
                  />
                  <FormDatePicker
                    :model-value="draft.endTo"
                    :placeholder="$t('project_list.ph_to_date')"
                    :class="SELECT_TRIGGER"
                    hide-message
                    @update:model-value="setField('endTo', $event)"
                  />
                </div>
              </div>

              <div class="flex items-center justify-between p-2 bg-slate-50 rounded-xl border border-slate-100 mt-2">
                <label for="project-show-deleted" class="flex flex-col gap-0.5 cursor-pointer">
                  <span class="text-[12px] font-bold text-slate-700">{{ $t('project_list.lbl_trash') }}</span>
                  <span class="text-[10px] text-slate-400">{{ $t('project_list.hint_trash') }}</span>
                </label>
                <input
                  id="project-show-deleted"
                  type="checkbox"
                  :checked="draft.deleted"
                  class="h-5 w-9 shrink-0 cursor-pointer appearance-none rounded-full bg-slate-200 transition-all checked:bg-emerald-500 relative before:content-[''] before:absolute before:h-4 before:w-4 before:bg-white before:rounded-full before:left-0.5 before:top-0.5 before:transition-all checked:before:translate-x-4 shadow-inner"
                  @change="setField('deleted', ($event.target as HTMLInputElement).checked)"
                />
              </div>
            </div>
          </FilterPopover>

          <Button
            type="submit"
            variant="primary"
            class="h-9 lg:h-10 rounded-xl px-4 lg:px-5 text-[13px] lg:text-[13.5px] font-bold"
            :loading="props.isFetching"
            :disabled="props.isFetching"
          >
            {{ $t('project_list.btn_search') }}
          </Button>
        </div>
      </div>

      <div class="mt-2 border-slate-100/50 pt-2">
        <FilterChips :filters="props.activeFilters" class="pb-1" @clear="emit('clearFilter', $event)" @clear-all="emit('reset')" />
      </div>
    </form>
  </div>
</template>
