import type { MessageSchema } from './index';

// Khai báo kiểu message toàn cục để IDE gợi ý key cho $t / t.
declare module 'vue-i18n' {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type -- mẫu module augmentation của vue-i18n
  export interface DefineLocaleMessage extends MessageSchema {}
}
