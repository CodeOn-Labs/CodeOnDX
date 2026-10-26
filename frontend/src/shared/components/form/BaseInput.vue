<script setup lang="ts">
// Port Input của FormInput.tsx.
import { cn } from '@/shared/utils/cn';

const props = withDefaults(defineProps<{ error?: boolean; class?: string }>(), { error: false, class: '' });
const model = defineModel<string | number | null>({ default: '' });
</script>

<template>
  <input
    v-model="model"
    :class="cn(
      'flex h-9 lg:h-10 w-full rounded-xl border border-slate-200 bg-white px-4 lg:px-5 py-2 text-[13.5px] font-medium text-slate-900 outline-none ring-0 ring-transparent focus:outline-none focus-visible:outline-none focus:ring-offset-0 transition-[border-color,background-color,box-shadow] duration-200 placeholder:text-slate-400 hover:border-slate-400/50 focus:border-pr focus:ring-[3.5px] focus:ring-pr/15 focus:bg-white disabled:cursor-not-allowed disabled:opacity-50',
      props.error && 'border-red-500 bg-red-50/30 focus:border-red-500 focus:ring-red-500/15',
      props.class,
    )"
  />
</template>
