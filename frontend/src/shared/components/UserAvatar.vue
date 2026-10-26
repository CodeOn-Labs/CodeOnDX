<script setup lang="ts">
// Ảnh đại diện tròn: ảnh nếu có, không thì chữ cái đầu trên nền pastel (getPastelAvatarProps) hoặc màu đậm (getGoogleAvatarProps).
import { computed } from 'vue';
import { getGoogleAvatarProps, getPastelAvatarProps } from '@/shared/utils/avatar';
import { cn } from '@/shared/utils/cn';

const props = withDefaults(
  defineProps<{ name: string; id?: string; src?: string | null; size?: number; variant?: 'pastel' | 'solid'; class?: string }>(),
  { id: undefined, src: null, size: 40, variant: 'pastel', class: '' },
);
const pastel = computed(() => getPastelAvatarProps(props.name, props.id));
const solid = computed(() => getGoogleAvatarProps(props.name, props.id));
</script>

<template>
  <div
    :class="cn(
      'flex shrink-0 items-center justify-center rounded-full border font-black shadow-sm overflow-hidden',
      !props.src && props.variant === 'pastel' && pastel.colorClass,
      !props.src && props.variant === 'solid' && 'border-white/40 text-white',
      props.class,
    )"
    :style="{ width: `${props.size}px`, height: `${props.size}px`, fontSize: `${Math.round(props.size / 3)}px`, backgroundColor: !props.src && props.variant === 'solid' ? solid.backgroundColor : undefined }"
  >
    <img v-if="props.src" :src="props.src" :alt="props.name" class="h-full w-full object-cover" />
    <template v-else>{{ pastel.initial }}</template>
  </div>
</template>
