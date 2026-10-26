<script setup lang="ts">
// Port RiskBadge (ProjectTable.tsx): mức rủi ro kèm biểu tượng khiên.
import { computed } from 'vue';
import { ShieldAlert, ShieldCheck, ShieldQuestion } from '@lucide/vue';
import type { MasterDataRef } from '@/api/types';
import { COLOR_CLASSES, hexStyle, isHexColor, riskKey, riskVariant, variantColor } from '@/modules/project/utils/project-view-model';
import { enumLabel } from '@/shared/i18n/enum-label';
import { cn } from '@/shared/utils/cn';

const props = defineProps<{ risk: MasterDataRef | null }>();

const color = computed(() => props.risk?.color ?? null);
const variant = computed(() => riskVariant(props.risk));
const cls = computed(() => (!isHexColor(color.value) && color.value && COLOR_CLASSES[color.value]) || COLOR_CLASSES[variantColor(variant.value)]);
const label = computed(() => enumLabel('risk_level', props.risk?.code, props.risk?.name));
</script>

<template>
  <span v-if="riskKey(props.risk) === 'none'" class="text-[12px] text-slate-400 font-medium">—</span>
  <div
    v-else
    :class="cn('inline-flex h-7 items-center gap-1.5 rounded-lg px-2.5 text-[10px] font-bold uppercase tracking-wider transition-all duration-300', cls)"
    :style="isHexColor(color) ? hexStyle(color) : undefined"
  >
    <ShieldAlert v-if="variant === 'red'" :size="14" class="animate-pulse" />
    <ShieldQuestion v-else-if="variant === 'amber'" :size="14" />
    <ShieldCheck v-else :size="14" />
    {{ label }}
  </div>
</template>
