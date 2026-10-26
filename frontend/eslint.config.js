// ESLint (flat config) — Vue 3 + TypeScript + luật i18n của vue-i18n (Q1).
// Luật riêng của dự án (cấp 1 theo screen_key, tiền tố, chống xung đột) nằm ở tools/i18n-check (npm run i18n:check).
import pluginVue from 'eslint-plugin-vue';
import vueI18n from '@intlify/eslint-plugin-vue-i18n';
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript';

const i18nRecommended = vueI18n.configs['flat/recommended'] ?? vueI18n.configs.recommended;

export default defineConfigWithVueTs(
  { name: 'app/files-to-lint', files: ['**/*.{ts,mts,vue}'] },
  { name: 'app/files-to-ignore', ignores: ['**/dist/**', '**/coverage/**', '**/node_modules/**'] },

  pluginVue.configs['flat/essential'],
  vueTsConfigs.recommended,
  ...[i18nRecommended].flat(),

  {
    name: 'app/rules',
    settings: {
      'vue-i18n': {
        localeDir: './src/locales/*.json',
        messageSyntaxVersion: '^11.0.0',
      },
    },
    rules: {
      // Giữ tên component như bản React (Button, Sidebar…)
      'vue/multi-word-component-names': 'off',
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_', ignoreRestSiblings: true }],

      // ── i18n (Q1) ──
      '@intlify/vue-i18n/key-format-style': ['error', 'snake_case', { splitByDots: true }],
      '@intlify/vue-i18n/no-dynamic-keys': 'error',
      '@intlify/vue-i18n/no-missing-keys': 'error',
      '@intlify/vue-i18n/no-missing-keys-in-other-locales': 'error',
      '@intlify/vue-i18n/no-duplicate-keys-in-locale': 'error',
      '@intlify/vue-i18n/no-deprecated-tc': 'error',
      '@intlify/vue-i18n/no-v-html': 'error',
      // enum.* và *.title_page được dùng qua enumLabel / screenTitle (key động có kiểm soát)
      '@intlify/vue-i18n/no-unused-keys': ['warn', { extensions: ['.ts', '.vue'], ignores: ['/^enum\\./', '/\\.title_page$/'] }],
      // Chữ hiển thị phải qua $t; bỏ qua ký hiệu, số và tên thương hiệu
      '@intlify/vue-i18n/no-raw-text': [
        'warn',
        { ignorePattern: '^[-–—#:()&%/+.,|·×*\\s0-9]+$', ignoreText: ['DX', 'Platform', 'Chat', 'Excel (.xlsx)', 'CSV (.csv)', 'TRUE', 'FALSE'] },
      ],
    },
  },

  // Nơi DUY NHẤT được ghép key i18n động
  { name: 'app/i18n-helpers', files: ['src/shared/i18n/**'], rules: { '@intlify/vue-i18n/no-dynamic-keys': 'off' } },
);
