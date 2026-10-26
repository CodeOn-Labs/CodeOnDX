/* eslint-disable @typescript-eslint/no-explicit-any -- port nguyên kiểu dữ liệu của module export bản React (dữ liệu dòng tuỳ ý) */
// Port từ shared/components/export/core/types.ts.
export type ColumnType = 'string' | 'number' | 'date' | 'datetime' | 'currency' | 'percent' | 'boolean' | 'hyperlink' | 'richtext';

export interface ExcelStyleConfig {
  alignment?: 'left' | 'center' | 'right';
  wrapText?: boolean;
  backgroundColor?: string;
  color?: string;
  bold?: boolean;
  numFmt?: string;
}

export type ConditionalFormatRule<T = any> = (value: any, row: T) => ExcelStyleConfig | undefined;

export interface ExcelFormula {
  formula: string;
  result?: any;
}

export interface ExportColumn<T = any> {
  id: string;
  header: string;
  accessorFn?: (originalRow: T) => any; // Can return ExcelFormula
  accessorKey?: keyof T;
  type?: ColumnType; // Can be auto-detected if omitted
  width?: number;
  hidden?: boolean;
  
  formatter?: (value: any, row: T) => any;
  excel?: ExcelStyleConfig;
  conditionalFormat?: ConditionalFormatRule<T>;
}

export type ExportFormat = 'xlsx' | 'csv' | 'pdf' | 'json';
export type ExportMode = 'all' | 'filtered' | 'selected' | 'current_page' | 'grouped' | 'tree';

export interface ExportSheet<T = any> {
  sheetName: string;
  data: T[];
  columns: ExportColumn<T>[];
  
  // Advanced Features
  title?: string; // Report title (will merge cells across columns)
  subTitle?: string;
  mergedCells?: string[]; // e.g. ['A1:C1', 'D1:F1']
  zebraRows?: boolean;

  /**
   * Auto-merge consecutive data rows with the same value.
   * Provide 1-based column indices (e.g. [1, 2] merges col A and B).
   * Merging is applied after all data rows are written.
   */
  rowMergeColumns?: number[];
  
  // Future architecture ready
  chartsMetadata?: any;
  pivotTables?: any;
}

export interface ExportConfig<T = any> {
  filename: string;
  format: ExportFormat;
  mode: ExportMode;
  
  // Multi-sheet Support
  sheets?: ExportSheet<any>[];
  
  // Fallback for single-sheet (backward compatibility with basic usage)
  sheetName?: string;
  columns?: ExportColumn<T>[];
  zebraRows?: boolean;
  
  // Localization
  locale?: 'vi-VN' | 'en-US';
  timezoneOffset?: number; // Minutes
}

/** Giai đoạn xử lý — UI dịch sang câu chữ theo ngôn ngữ (worker không dùng được vue-i18n). */
export type ExportStage = 'starting' | 'preparing' | 'processing' | 'writing_xlsx' | 'writing_csv';

export interface ExportProgressData {
  status: 'idle' | 'processing' | 'completed' | 'error';
  progress: number;
  total: number;
  processed: number;
  stage?: ExportStage;
  /** Chi tiết lỗi kỹ thuật (status = 'error') */
  message?: string;
}

export interface WorkerMessageData {
  type: 'progress' | 'success' | 'error';
  payload?: any;
}
