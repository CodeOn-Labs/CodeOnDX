/* eslint-disable @typescript-eslint/no-explicit-any -- port nguyên từ bản React, dữ liệu dòng tuỳ ý */
// Port từ shared/components/export/utils/excel-helpers.ts (không phụ thuộc framework).
import type { ColumnType, ExportColumn } from './types';

export const EXCEL_FORMATS = {
  currency: '"$"#,##0.00',
  percent: '0.00%',
  date: 'dd/MM/yyyy',
  datetime: 'dd/MM/yyyy hh:mm:ss',
  number: '#,##0.00',
};

export function detectColumnType(data: any[], col: ExportColumn): ColumnType {
  if (col.type) return col.type;
  if (!data || data.length === 0) return 'string';

  const sampleSize = Math.min(data.length, 50);
  let hasNumber = false;
  let hasDate = false;
  let hasBoolean = false;

  for (let i = 0; i < sampleSize; i++) {
    const row = data[i];
    let val = col.accessorFn ? col.accessorFn(row) : row[col.accessorKey as string];
    if (col.formatter) val = col.formatter(val, row);

    if (val !== null && val !== undefined && val !== '') {
      if (typeof val === 'boolean') {
        hasBoolean = true;
      } else if (val instanceof Date || (typeof val === 'string' && /^\d{4}-\d{2}-\d{2}/.test(val) && !isNaN(Date.parse(val)))) {
        hasDate = true;
      } else if (typeof val === 'number') {
        hasNumber = true;
      } else {
        return 'string'; // If mixed or string, default to string
      }
    }
  }

  if (hasBoolean) return 'boolean';
  if (hasDate) return 'date';
  if (hasNumber) return 'number';
  return 'string';
}

export function sanitizeExcelValue(value: any): any {
  if (typeof value === 'string') {
    // Prevent CSV/Excel Macro Injection for regular strings
    if (/^[=+\-@]/.test(value)) {
      return "'" + value;
    }
  }
  return value;
}

export function isSafeFormula(formula: string): boolean {
  // Prevent execution of CMD, HYPERLINK, and WEBSERVICE malicious payloads
  const unsafePatterns = /CMD|HYPERLINK|WEBSERVICE|DDE|CALL/i;
  return !unsafePatterns.test(formula);
}

export function getExcelFormat(col: ExportColumn, detectedType?: ColumnType): string | undefined {
  if (col.excel?.numFmt) return col.excel.numFmt;
  const targetType = col.type || detectedType;
  if (!targetType) return undefined;
  return EXCEL_FORMATS[targetType as keyof typeof EXCEL_FORMATS];
}

export function autoWidthCalculator(data: any[], column: ExportColumn, minWidth = 12, maxWidth = 80): number {
  if (column.width) return column.width;
  
  let maxLen = column.header.length;
  const sampleSize = Math.min(data.length, 500);
  const detectedType = detectColumnType(data, column);
  
  for (let i = 0; i < sampleSize; i++) {
    const row = data[i];
    let val = column.accessorFn ? column.accessorFn(row) : row[column.accessorKey as string];
    if (column.formatter) {
      val = column.formatter(val, row);
    }
    
    if (val !== null && val !== undefined) {
      let len: number;
      if (detectedType === 'date' || val instanceof Date) {
        len = 12; // DD/MM/YYYY
      } else if (detectedType === 'datetime') {
        len = 20; // DD/MM/YYYY hh:mm:ss
      } else if (detectedType === 'currency' || typeof val === 'number') {
        len = String(Math.floor(Number(val))).length + 5;
      } else if (val && typeof val === 'object' && val.formula) {
        len = 15; // Rough estimate for formula results
      } else {
        const strVal = String(val);
        // Handle multi-line strings
        if (strVal.includes('\n')) {
          const lines = strVal.split('\n');
          len = Math.max(...lines.map(l => l.length));
        } else {
          len = strVal.length;
        }
      }
      
      if (len > maxLen) {
        maxLen = len;
      }
    }
  }
  
  return Math.min(Math.max(maxLen + 4, minWidth), maxWidth);
}
