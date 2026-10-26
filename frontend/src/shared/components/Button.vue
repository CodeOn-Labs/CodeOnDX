<script setup lang="ts">
// Port từ shared/components/Button.tsx — giữ nguyên biến thể, kích thước và class.
import { computed, useSlots, type HTMLAttributes } from 'vue';
import { cn } from '@/shared/utils/cn';

export type ButtonVariant = 'primary' | 'danger' | 'success' | 'secondary' | 'outline' | 'ghost';
export type ButtonSize = 'sm' | 'md' | 'lg';

const props = withDefaults(
  defineProps<{
    variant?: ButtonVariant;
    size?: ButtonSize;
    loading?: boolean;
    disabled?: boolean;
    type?: 'button' | 'submit' | 'reset';
    class?: HTMLAttributes['class'];
  }>(),
  { variant: 'primary', size: 'md', loading: false, disabled: false, type: 'button', class: undefined },
);

const slots = useSlots();

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary: [
    'bg-gradient-to-r from-[#6366f1] to-[#9333ea] text-white',
    'shadow-[0_2px_8px_rgba(99,102,241,0.25)]',
    'hover:shadow-[0_4px_14px_rgba(99,102,241,0.35)] hover:-translate-y-0.5',
    'active:translate-y-0 active:shadow-sm',
    'disabled:from-[#a5b4fc] disabled:to-[#c4b5fd] disabled:shadow-none disabled:hover:translate-y-0',
  ].join(' '),
  danger: [
    'bg-gradient-to-r from-[#fb923c] to-[#f43f5e] text-white',
    'shadow-[0_2px_8px_rgba(251,146,60,0.2)]',
    'hover:shadow-[0_4px_12px_rgba(251,146,60,0.35)] hover:-translate-y-0.5',
    'active:translate-y-0 active:shadow-sm',
    'disabled:opacity-50 disabled:hover:translate-y-0',
  ].join(' '),
  success: [
    'bg-gradient-to-r from-[#4ade80] to-[#22c55e] text-white',
    'shadow-[0_2px_8px_rgba(34,197,94,0.2)]',
    'hover:shadow-[0_4px_12px_rgba(34,197,94,0.3)] hover:-translate-y-0.5',
    'active:translate-y-0 active:shadow-sm',
    'disabled:opacity-50 disabled:hover:translate-y-0',
  ].join(' '),
  secondary: [
    'bg-gradient-to-r from-[#3b82f6] to-[#2563eb] text-white',
    'shadow-[0_2px_8px_rgba(59,130,246,0.25)]',
    'hover:shadow-[0_4px_14px_rgba(59,130,246,0.35)] hover:-translate-y-0.5',
    'active:translate-y-0 active:shadow-sm',
    'disabled:from-[#93c5fd] disabled:to-[#60a5fa] disabled:shadow-none disabled:hover:translate-y-0',
  ].join(' '),
  outline: [
    'bg-white border border-[#e5e8f2] text-[#4c5568]',
    'hover:bg-[#f0f2f5] hover:border-[#d1d5e0] hover:text-[#1f2430]',
    'active:bg-[#e8eaf0]',
    'disabled:opacity-50',
  ].join(' '),
  ghost: [
    'bg-transparent text-[#8b93a6]',
    'hover:bg-[#f0f2f5] hover:text-[#1f2430]',
    'active:bg-[#e8eaf0]',
    'disabled:opacity-40',
  ].join(' '),
};

const SIZE_CLASSES: Record<ButtonSize, string> = {
  sm: 'h-8 px-3 text-[12px] rounded-xl',
  md: 'h-9 lg:h-10 px-4 lg:px-5 text-[13px] lg:text-[13.5px] rounded-xl',
  lg: 'h-11 lg:h-12 px-8 text-[14px] lg:text-[15px] rounded-xl',
};

const classes = computed(() =>
  cn(
    'inline-flex items-center justify-center gap-1.5 font-bold cursor-pointer whitespace-nowrap outline-none',
    'transition-[transform,translate,scale,box-shadow,opacity] duration-200 will-change-transform',
    'focus:outline-none focus:ring-0 focus-visible:ring-2 focus-visible:ring-[#7a78f6]/50 focus-visible:ring-offset-1',
    'disabled:cursor-not-allowed select-none',
    VARIANT_CLASSES[props.variant] ?? VARIANT_CLASSES.primary,
    SIZE_CLASSES[props.size] ?? SIZE_CLASSES.md,
    props.class,
  ),
);
</script>

<template>
  <button :type="props.type" :disabled="props.disabled || props.loading" :class="classes">
    <svg v-if="props.loading" class="animate-spin h-4 w-4 shrink-0" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
    </svg>
    <span v-if="!props.loading && slots.leftIcon" class="shrink-0"><slot name="leftIcon" /></span>
    <span v-if="slots.default" class="shrink-0"><slot /></span>
    <span v-if="!props.loading && slots.rightIcon" class="shrink-0"><slot name="rightIcon" /></span>
  </button>
</template>
