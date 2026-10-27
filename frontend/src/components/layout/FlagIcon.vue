<script setup lang="ts">
// Cờ dạng SVG cho nút đổi ngôn ngữ — emoji cờ (🇻🇳 🇬🇧) không hiện trên Windows (chỉ ra chữ "VN" / "GB").
import { useId } from 'vue';
import type { AppLocale } from '@/i18n';

const props = withDefaults(defineProps<{ code: AppLocale; size?: number }>(), { size: 22 });
const uid = useId();
</script>

<template>
  <span class="inline-flex shrink-0 overflow-hidden rounded-[4px] shadow-[0_0_0_1px_rgba(15,23,42,0.08)]" :style="{ width: `${props.size}px`, height: `${Math.round((props.size * 2) / 3)}px` }" aria-hidden="true">
    <!-- Việt Nam: nền đỏ, sao vàng năm cánh -->
    <svg v-if="props.code === 'vi'" viewBox="0 0 30 20" class="h-full w-full" preserveAspectRatio="none">
      <rect width="30" height="20" fill="#DA251D" />
      <polygon
        fill="#FFFF00"
        points="15,4 16.35,8.15 20.71,8.15 17.18,10.71 18.53,14.85 15,12.29 11.47,14.85 12.82,10.71 9.29,8.15 13.65,8.15"
      />
    </svg>
    <!-- Anh (Union Jack) -->
    <svg v-else viewBox="0 0 60 30" class="h-full w-full" preserveAspectRatio="none">
      <clipPath :id="`${uid}-s`"><path d="M0,0 v30 h60 v-30 z" /></clipPath>
      <clipPath :id="`${uid}-t`"><path d="M30,15 h30 v15 z v15 h-30 z h-30 v-15 z v-15 h30 z" /></clipPath>
      <g :clip-path="`url(#${uid}-s)`">
        <path d="M0,0 v30 h60 v-30 z" fill="#012169" />
        <path d="M0,0 L60,30 M60,0 L0,30" stroke="#fff" stroke-width="6" />
        <path d="M0,0 L60,30 M60,0 L0,30" :clip-path="`url(#${uid}-t)`" stroke="#C8102E" stroke-width="4" />
        <path d="M30,0 v30 M0,15 h60" stroke="#fff" stroke-width="10" />
        <path d="M30,0 v30 M0,15 h60" stroke="#C8102E" stroke-width="6" />
      </g>
    </svg>
  </span>
</template>
