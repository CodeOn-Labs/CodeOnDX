// Đọc file Excel (.xlsx) / CSV ở trình duyệt thành mảng dòng { tiêu đề cột: giá trị } — dùng cho Import (chấm công, dự án…).
// Port phần đọc file của shared/services/import/excel-parser.ts bản gốc (bản gốc dùng SheetJS; bản mới dùng exceljs đã có).

export type SheetRow = Record<string, string>;

export interface SheetData {
  headers: string[];
  rows: SheetRow[];
}

const MAX_ROWS = 5000;

/** Giá trị ô exceljs → chuỗi: ngày → yyyy-MM-dd (hoặc HH:mm nếu chỉ có giờ), công thức → kết quả. */
function cellText(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (value instanceof Date) {
    const iso = value.toISOString();
    // Excel lưu giờ thuần là ngày 1899-12-30 + phần giờ
    if (iso.startsWith('1899-12-30') || iso.startsWith('1899-12-31')) return iso.slice(11, 16);
    return iso.slice(0, 10);
  }
  if (typeof value === 'object') {
    const v = value as { result?: unknown; text?: unknown; richText?: Array<{ text: string }> };
    if (v.result !== undefined) return cellText(v.result);
    if (typeof v.text === 'string') return v.text;
    if (Array.isArray(v.richText)) return v.richText.map((r) => r.text).join('');
  }
  return String(value).trim();
}

function parseCsv(text: string): string[][] {
  const out: string[][] = [];
  let row: string[] = [];
  let cur = '';
  let quoted = false;
  const sep = text.split('\n', 1)[0]?.includes(';') && !text.split('\n', 1)[0]?.includes(',') ? ';' : ',';
  for (let i = 0; i < text.length; i += 1) {
    const ch = text[i];
    if (quoted) {
      if (ch === '"' && text[i + 1] === '"') {
        cur += '"';
        i += 1;
      } else if (ch === '"') quoted = false;
      else cur += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === sep) {
      row.push(cur.trim());
      cur = '';
    } else if (ch === '\n' || ch === '\r') {
      if (ch === '\r' && text[i + 1] === '\n') i += 1;
      row.push(cur.trim());
      if (row.some((c) => c !== '')) out.push(row);
      row = [];
      cur = '';
    } else cur += ch;
  }
  row.push(cur.trim());
  if (row.some((c) => c !== '')) out.push(row);
  return out;
}

function toSheet(matrix: string[][]): SheetData {
  const [head = [], ...body] = matrix;
  const headers = head.map((h, i) => h || `#${i + 1}`);
  const rows = body.slice(0, MAX_ROWS).map((cells) => Object.fromEntries(headers.map((h, i) => [h, cells[i] ?? ''])));
  return { headers, rows };
}

/** Đọc sheet đầu tiên; dòng 1 là tiêu đề cột. */
export async function readSheet(file: File): Promise<SheetData> {
  const name = file.name.toLowerCase();
  if (name.endsWith('.csv')) {
    return toSheet(parseCsv((await file.text()).replace(/^﻿/, '')));
  }
  const { default: ExcelJS } = await import('exceljs');
  const wb = new ExcelJS.Workbook();
  await wb.xlsx.load(await file.arrayBuffer());
  const ws = wb.worksheets[0];
  if (!ws) return { headers: [], rows: [] };
  const matrix: string[][] = [];
  ws.eachRow({ includeEmpty: false }, (row) => {
    const values = Array.isArray(row.values) ? row.values.slice(1) : [];
    matrix.push(values.map(cellText));
  });
  return toSheet(matrix);
}

/** Chuẩn hoá ngày người dùng gõ: dd/MM/yyyy, d/M/yyyy, yyyy-MM-dd → yyyy-MM-dd ('' nếu sai). */
export function normalizeDate(text: string): string {
  const v = text.trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(v)) return v;
  const m = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/.exec(v);
  if (!m) return '';
  return `${m[3]}-${m[2]!.padStart(2, '0')}-${m[1]!.padStart(2, '0')}`;
}

/** Chuẩn hoá giờ: 8:05, 08:05:30 → 08:05 ('' nếu trống, null-like). */
export function normalizeTime(text: string): string {
  const m = /^(\d{1,2}):(\d{2})(?::\d{2})?$/.exec(text.trim());
  return m ? `${m[1]!.padStart(2, '0')}:${m[2]}` : '';
}
