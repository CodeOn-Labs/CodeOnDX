<script setup lang="ts">
// Port từ EmptyState.tsx.
withDefaults(
  defineProps<{
    title?: string;
    description?: string;
    compact?: boolean;
    class?: string;
  }>(),
  { title: undefined, description: undefined, compact: false, class: '' },
);
</script>

<template>
  <div :class="['flex flex-col items-center justify-center text-center', compact ? 'py-8 px-4' : 'py-16 px-8', $props.class]">
    <div class="mb-4">
      <slot name="icon">
        <svg width="64" height="64" viewBox="0 0 64 64" fill="none" xmlns="http://www.w3.org/2000/svg" class="opacity-40">
          <circle cx="32" cy="32" r="30" stroke="#c4c9d9" stroke-width="1.5" stroke-dasharray="4 3" />
          <rect x="20" y="16" width="24" height="32" rx="3" fill="#e8ecf5" stroke="#c4c9d9" stroke-width="1.5" />
          <line x1="25" y1="24" x2="39" y2="24" stroke="#b0b8cc" stroke-width="1.5" stroke-linecap="round" />
          <line x1="25" y1="30" x2="39" y2="30" stroke="#b0b8cc" stroke-width="1.5" stroke-linecap="round" />
          <line x1="25" y1="36" x2="33" y2="36" stroke="#b0b8cc" stroke-width="1.5" stroke-linecap="round" />
        </svg>
      </slot>
    </div>
    <p :class="['font-semibold text-[#5f6577] mb-1', compact ? 'text-[14px]' : 'text-[16px]']">
      {{ title ?? $t('component.empty_state.empty_no_data') }}
    </p>
    <p v-if="description" class="text-[13px] text-[#9399a8] max-w-[320px] leading-relaxed mt-1">
      {{ description }}
    </p>
    <div v-if="$slots.action" class="mt-5">
      <slot name="action" />
    </div>
  </div>
</template>
