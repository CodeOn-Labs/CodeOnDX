/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** development | test | production — "test" hiện dải "Môi trường kiểm thử" */
  readonly VITE_APP_ENV?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
