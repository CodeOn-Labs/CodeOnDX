<script setup lang="ts">
// Khung chung của một field (port FormItem + FormLabel + FormDescription + FormMessage của Form.tsx).
// Dùng được độc lập (v-model) hoặc với VeeValidate (truyền `error` từ useField).
withDefaults(
  defineProps<{
    label?: string;
    forId?: string;
    required?: boolean;
    description?: string;
    error?: string;
    hideMessage?: boolean;
  }>(),
  { label: undefined, forId: undefined, required: false, description: undefined, error: undefined, hideMessage: false },
);
</script>

<template>
  <div class="space-y-1.5 flex flex-col gap-1.5">
    <label
      v-if="label"
      :for="forId"
      :class="[
        'text-[11px] font-bold uppercase tracking-widest transition-colors',
        error ? 'text-red-500' : 'text-slate-500',
        'peer-disabled:cursor-not-allowed peer-disabled:opacity-70',
      ]"
    >
      {{ label }}<span v-if="required" class="ml-1 text-red-500">*</span>
    </label>
    <slot />
    <p v-if="description" class="text-[10px] text-slate-400 font-medium">{{ description }}</p>
    <p v-if="error && !hideMessage" class="text-[10px] font-bold text-red-500 animate-in fade-in slide-in-from-top-1 duration-200">{{ error }}</p>
  </div>
</template>
