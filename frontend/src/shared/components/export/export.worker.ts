/* eslint-disable @typescript-eslint/no-explicit-any -- port nguyên từ bản React, dữ liệu dòng tuỳ ý */
// Port từ shared/components/export/workers/export.worker.ts: dựng file Excel/CSV trong Web Worker để không khoá giao diện.
// Khác bản gốc: tiến trình gửi `stage` (mã giai đoạn) thay vì câu tiếng Việt — UI dịch theo ngôn ngữ đang chọn.
import ExcelJS from 'exceljs';
import type { ExportConfig, ExportStage, WorkerMessageData } from './types';
import { getExcelFormat, isSafeFormula, sanitizeExcelValue } from './excel-helpers';

const CHUNK_SIZE = 1000;

self.onmessage = async (e: MessageEvent) => {
  const { data, config } = e.data as { data: any[]; config: ExportConfig };

  try {
    postProgress(0, 100, 'preparing');

    let sheetsToProcess = config.sheets || [];
    if (sheetsToProcess.length === 0 && config.columns && data) {
      sheetsToProcess = [{ sheetName: config.sheetName || 'Data', columns: config.columns, data, zebraRows: config.zebraRows }];
    }

    if (config.format === 'xlsx') {
      postSuccess(await generateExcel(sheetsToProcess));
    } else if (config.format === 'csv') {
      // CSV chỉ hỗ trợ sheet đầu tiên
      postSuccess(await generateCSV(sheetsToProcess[0]));
    }
  } catch (error: any) {
    self.postMessage({ type: 'error', payload: error?.message || '' } as WorkerMessageData);
  }
};

function postProgress(processed: number, total: number, stage: ExportStage) {
  self.postMessage({
    type: 'progress',
    payload: { processed, total, progress: total > 0 ? Math.round((processed / total) * 100) : 100, stage },
  } as WorkerMessageData);
}

function postSuccess(blob: Blob) {
  self.postMessage({ type: 'success', payload: { blob } } as WorkerMessageData);
}

async function generateExcel(sheets: any[]): Promise<Blob> {
  const workbook = new ExcelJS.Workbook();
  workbook.creator = 'DX Platform';
  workbook.created = new Date();

  const totalDataLength = sheets.reduce((acc, sheet) => acc + sheet.data.length, 0);
  let processedItems = 0;

  for (const sheetData of sheets) {
    const data = sheetData.data || [];
    const visibleColumns = (sheetData.columns || []).filter((c: any) => !c.hidden);

    const hasTitle = !!sheetData.title;
    const hasSubTitle = !!sheetData.subTitle;
    let headerRowIndex = 1;
    if (hasTitle) headerRowIndex++;
    if (hasSubTitle) headerRowIndex++;

    const sheet = workbook.addWorksheet(sheetData.sheetName || 'Data', {
      views: [{ state: 'frozen', ySplit: headerRowIndex }],
    });

    sheet.columns = visibleColumns.map((col: any) => {
      const type = col.type || 'string';
      return {
        header: '',
        key: col.id,
        width: col.width || 12,
        style: {
          numFmt: getExcelFormat(col, type),
          alignment: { vertical: 'middle', horizontal: col.excel?.alignment || 'left', wrapText: col.excel?.wrapText },
          font: { bold: col.excel?.bold, color: col.excel?.color ? { argb: col.excel.color.replace('#', 'FF') } : undefined },
          fill: col.excel?.backgroundColor
            ? { type: 'pattern', pattern: 'solid', fgColor: { argb: col.excel.backgroundColor.replace('#', 'FF') } }
            : undefined,
        },
      };
    });

    let currentRowOffset = 1;
    if (hasTitle) {
      sheet.mergeCells(currentRowOffset, 1, currentRowOffset, visibleColumns.length || 1);
      const titleCell = sheet.getCell(currentRowOffset, 1);
      titleCell.value = sheetData.title;
      titleCell.font = { bold: true, size: 16 };
      titleCell.alignment = { horizontal: 'center', vertical: 'middle' };
      currentRowOffset++;
    }
    if (hasSubTitle) {
      sheet.mergeCells(currentRowOffset, 1, currentRowOffset, visibleColumns.length || 1);
      const subCell = sheet.getCell(currentRowOffset, 1);
      subCell.value = sheetData.subTitle;
      subCell.font = { italic: true, size: 12, color: { argb: 'FF64748B' } };
      subCell.alignment = { horizontal: 'center', vertical: 'middle' };
      currentRowOffset++;
    }

    const headerRow = sheet.getRow(currentRowOffset);
    visibleColumns.forEach((col: any, index: number) => {
      headerRow.getCell(index + 1).value = col.header;
    });
    headerRow.font = { bold: true, color: { argb: 'FFFFFFFF' } };
    headerRow.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1E293B' } };
    headerRow.alignment = { vertical: 'middle', horizontal: 'center' };

    if (visibleColumns.length > 0) {
      sheet.autoFilter = { from: { row: currentRowOffset, column: 1 }, to: { row: currentRowOffset, column: visibleColumns.length } };
    }

    if (Array.isArray(sheetData.mergedCells)) {
      sheetData.mergedCells.forEach((range: string) => {
        try {
          sheet.mergeCells(range);
        } catch {
          /* bỏ qua vùng gộp không hợp lệ */
        }
      });
    }

    const dataStartRow = currentRowOffset + 1;

    for (let i = 0; i < data.length; i += CHUNK_SIZE) {
      const chunk = data.slice(i, i + CHUNK_SIZE);
      for (let r = 0; r < chunk.length; r++) {
        const sheetRow = sheet.getRow(dataStartRow + i + r);
        const processedRow = chunk[r];

        visibleColumns.forEach((col: any, colIndex: number) => {
          const cellData = processedRow[col.id] || { value: null };
          let val = cellData.value;
          const style = cellData.style;
          const detectedType = col.type || 'string';

          if ((detectedType === 'date' || detectedType === 'datetime') && val && !(val instanceof Date) && typeof val !== 'object') {
            val = new Date(val);
          } else if (detectedType === 'boolean') {
            val = val ? 'Yes' : 'No';
          } else if (!(val && typeof val === 'object' && val.formula)) {
            val = sanitizeExcelValue(val);
          }

          const cell = sheetRow.getCell(colIndex + 1);
          if (val && typeof val === 'object' && val.formula) {
            cell.value = isSafeFormula(val.formula) ? { formula: val.formula, result: val.result } : '#UNSAFE_FORMULA';
          } else {
            cell.value = val;
          }

          if (style) {
            if (style.backgroundColor) {
              cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: style.backgroundColor.replace('#', 'FF') } };
            }
            if (style.color || style.bold !== undefined) {
              cell.font = { ...cell.font, color: style.color ? { argb: style.color.replace('#', 'FF') } : cell.font?.color, bold: style.bold ?? cell.font?.bold };
            }
            if (style.alignment || style.wrapText !== undefined) {
              cell.alignment = { ...cell.alignment, horizontal: style.alignment || cell.alignment?.horizontal, wrapText: style.wrapText ?? cell.alignment?.wrapText };
            }
          }
        });

        if (sheetData.zebraRows && (i + r) % 2 === 1) {
          visibleColumns.forEach((_: any, colIndex: number) => {
            const cell = sheetRow.getCell(colIndex + 1);
            if (!cell.fill || (cell.fill as any).type === 'none') {
              cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFF8FAFC' } };
            }
          });
        }
        sheetRow.commit();
      }

      processedItems += chunk.length;
      await new Promise((resolve) => setTimeout(resolve, 0));
      postProgress(processedItems, totalDataLength, 'processing');
    }

    // Gộp các ô liên tiếp cùng giá trị ở các cột chỉ định
    if (sheetData.rowMergeColumns?.length > 0 && data.length > 1) {
      for (const colIdx of sheetData.rowMergeColumns) {
        let mergeStart = dataStartRow;
        let currentVal = sheet.getCell(dataStartRow, colIdx).value;
        const mergeRange = (from: number, to: number) => {
          if (to <= from) return;
          try {
            sheet.mergeCells(from, colIdx, to, colIdx);
            sheet.getCell(from, colIdx).alignment = { vertical: 'middle', horizontal: 'center', wrapText: true };
          } catch {
            /* ô đã gộp */
          }
        };
        for (let r = dataStartRow + 1; r <= dataStartRow + data.length - 1; r++) {
          const cellVal = sheet.getCell(r, colIdx).value;
          if (cellVal === currentVal) continue;
          mergeRange(mergeStart, r - 1);
          mergeStart = r;
          currentVal = cellVal;
        }
        mergeRange(mergeStart, dataStartRow + data.length - 1);
      }
    }
  }

  postProgress(totalDataLength, totalDataLength, 'writing_xlsx');
  const buffer = await workbook.xlsx.writeBuffer();
  return new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
}

async function generateCSV(sheetData: any): Promise<Blob> {
  const data = sheetData.data || [];
  const visibleColumns = (sheetData.columns || []).filter((c: any) => !c.hidden);
  const headers = visibleColumns.map((c: any) => `"${c.header.replace(/"/g, '""')}"`).join(',');

  let csvContent = `${headers}\n`;
  let processedItems = 0;

  for (let i = 0; i < data.length; i += CHUNK_SIZE) {
    const chunk = data.slice(i, i + CHUNK_SIZE);
    const rows = chunk.map((processedRow: any) =>
      visibleColumns
        .map((col: any) => {
          const cellData = processedRow[col.id] || { value: null };
          let val = cellData.value;
          if (val === null || val === undefined) return '""';
          if (val && typeof val === 'object' && val.formula) {
            val = isSafeFormula(val.formula) ? `=${val.formula}` : '#UNSAFE_FORMULA';
          } else {
            val = sanitizeExcelValue(val);
          }
          return `"${String(val).replace(/"/g, '""')}"`;
        })
        .join(','),
    );
    csvContent += `${rows.join('\n')}\n`;
    processedItems += chunk.length;
    await new Promise((resolve) => setTimeout(resolve, 0));
    postProgress(processedItems, data.length, 'processing');
  }

  postProgress(data.length, data.length, 'writing_csv');
  // Thêm BOM UTF-8 để Excel hiển thị đúng tiếng Việt
  return new Blob([new Uint8Array([0xef, 0xbb, 0xbf]), csvContent], { type: 'text/csv;charset=utf-8;' });
}
