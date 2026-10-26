/* eslint-disable @typescript-eslint/no-explicit-any -- dữ liệu dòng tuỳ ý như bản React */
// Port hooks/useExport.ts: chuẩn hoá dữ liệu ở luồng chính (bỏ hàm để structured clone được), giao cho Web Worker dựng file rồi tải về.
import { onBeforeUnmount, ref } from 'vue';
import type { ExportConfig, ExportProgressData, WorkerMessageData } from './types';
import { autoWidthCalculator, detectColumnType } from './excel-helpers';

const IDLE: ExportProgressData = { status: 'idle', progress: 0, total: 0, processed: 0 };

export function useExport<T = any>() {
  const progress = ref<ExportProgressData>({ ...IDLE });
  let worker: Worker | null = null;

  function stopWorker() {
    worker?.terminate();
    worker = null;
  }

  onBeforeUnmount(stopWorker);

  function download(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  function startExport(data: T[], config: ExportConfig<T>) {
    stopWorker();
    progress.value = { status: 'processing', progress: 0, total: data.length, processed: 0, stage: 'starting' };

    try {
      const w = new Worker(new URL('./export.worker.ts', import.meta.url), { type: 'module' });
      worker = w;

      w.onmessage = (e: MessageEvent<WorkerMessageData>) => {
        const { type, payload } = e.data;
        if (type === 'progress') {
          progress.value = { status: 'processing', progress: payload.progress, total: payload.total, processed: payload.processed, stage: payload.stage };
        } else if (type === 'success') {
          progress.value = { ...progress.value, status: 'completed', progress: 100 };
          download(payload.blob, `${config.filename}.${config.format}`);
          stopWorker();
        } else if (type === 'error') {
          progress.value = { ...progress.value, status: 'error', message: payload };
          stopWorker();
        }
      };

      w.onerror = (err) => {
        progress.value = { ...progress.value, status: 'error', message: err.message };
        stopWorker();
      };

      let sheets = config.sheets || [];
      if (sheets.length === 0 && config.columns) {
        sheets = [{ sheetName: config.sheetName || 'Data', columns: config.columns, data, zebraRows: config.zebraRows }];
      }

      const serializableSheets = sheets.map((sheet) => {
        const visibleColumns = sheet.columns.filter((c) => !c.hidden);
        const columns = visibleColumns.map((col) => {
          const type = col.type || detectColumnType(sheet.data, col);
          const width = col.width || autoWidthCalculator(sheet.data, col);
          const { accessorFn: _accessorFn, formatter: _formatter, conditionalFormat: _conditionalFormat, ...rest } = col;
          return { ...rest, type, width };
        });
        const rows = sheet.data.map((row: any) => {
          const out: Record<string, { value: any; style?: any }> = {};
          for (const col of visibleColumns) {
            let value = col.accessorFn ? col.accessorFn(row) : row[col.accessorKey as string];
            if (col.formatter) value = col.formatter(value, row);
            out[col.id] = { value, style: col.conditionalFormat?.(value, row) };
          }
          return out;
        });
        return { ...sheet, columns, data: rows };
      });

      w.postMessage({ data: [], config: { ...config, sheets: serializableSheets, columns: undefined } });
    } catch (error) {
      progress.value = { ...progress.value, status: 'error', message: error instanceof Error ? error.message : String(error) };
    }
  }

  function cancelExport() {
    if (worker) {
      stopWorker();
      progress.value = { ...IDLE };
    }
  }

  function reset() {
    progress.value = { ...IDLE };
  }

  return { progress, startExport, cancelExport, reset };
}
