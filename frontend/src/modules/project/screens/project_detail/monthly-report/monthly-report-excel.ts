// Xuất báo cáo tháng ra Excel: mỗi nhân sự 1 sheet — đầu trang (khách hàng, công ty, số đơn hàng, tên công việc, người phụ trách,
// nhân sự, tháng), bảng theo đúng thứ tự cột của báo cáo, dòng tổng cộng các cột số.
// Khác bản gốc: dựng bố cục bằng code thay cho file template 作業報告書 riêng của công ty (D12 — mẫu cấu hình hoá, tiếng Nhật tuỳ chọn);
// bỏ xuất PDF / xem trước PDF.
import ExcelJS from 'exceljs';
import type { MonthlyReport, MonthlyReportColumn, MonthlyReportDay } from '@/modules/project/api/project-detail.api';

export interface MonthlyReportExcelLabels {
  sheetTitle: string;
  client: string;
  company: string;
  orderNo: string;
  workName: string;
  responsible: string;
  member: string;
  month: string;
  total: string;
  /** Nhãn cột hệ thống theo ngôn ngữ đang xem (cột bổ sung dùng tên cột) */
  columnLabel: (column: MonthlyReportColumn) => string;
}

function cellValue(column: MonthlyReportColumn, day: MonthlyReportDay): string | number | null {
  switch (column.source) {
    case 'date':
      return `${day.date.slice(8, 10)}/${day.date.slice(5, 7)}`;
    case 'hours':
      return day.hours;
    case 'content':
      return day.content || null;
    case 'checkIn':
      return day.start;
    case 'checkOut':
      return day.end;
    default: {
      const raw = day.custom[column.id] ?? '';
      if (column.type === 'number') return raw !== '' && Number.isFinite(Number(raw)) ? Number(raw) : null;
      return raw || null;
    }
  }
}

const THIN: Partial<ExcelJS.Border> = { style: 'thin', color: { argb: 'FFB0B7C3' } };
const BORDER: Partial<ExcelJS.Borders> = { top: THIN, left: THIN, bottom: THIN, right: THIN };

export async function buildMonthlyReportWorkbook(report: MonthlyReport, labels: MonthlyReportExcelLabels): Promise<Blob> {
  const wb = new ExcelJS.Workbook();
  const used = new Set<string>();
  for (const member of report.members) {
    // Tên sheet ≤ 31 ký tự, không trùng, không chứa ký tự cấm
    let name = `${member.code} ${member.fullName}`.replace(/[\\/?*[\]:]/g, ' ').slice(0, 31);
    for (let i = 2; used.has(name); i++) name = `${name.slice(0, 28)} ${i}`;
    used.add(name);
    const ws = wb.addWorksheet(name, { pageSetup: { paperSize: 9, orientation: 'portrait', fitToPage: true, fitToWidth: 1 } });
    const cols = report.columns;
    ws.columns = cols.map((c) => ({ width: c.source === 'content' ? 48 : c.source === 'date' ? 9 : c.type === 'number' ? 10 : 16 }));

    ws.mergeCells(1, 1, 1, cols.length);
    ws.getCell(1, 1).value = labels.sheetTitle;
    ws.getCell(1, 1).font = { bold: true, size: 15 };
    ws.getCell(1, 1).alignment = { horizontal: 'center' };
    const header: Array<[string, string]> = [
      [labels.client, report.header.clientName], [labels.company, report.header.companyName], [labels.orderNo, report.header.orderNo],
      [labels.workName, report.header.workName], [labels.responsible, report.header.responsible],
      [labels.member, `${member.fullName} (${member.code})`], [labels.month, `${report.month.slice(5, 7)}/${report.month.slice(0, 4)}`],
    ];
    header.forEach(([label, value], i) => {
      const row = ws.getRow(3 + i);
      row.getCell(1).value = label;
      row.getCell(1).font = { bold: true, color: { argb: 'FF475569' } };
      ws.mergeCells(3 + i, 2, 3 + i, Math.max(2, cols.length));
      row.getCell(2).value = value || '';
    });

    const headRow = ws.getRow(11);
    cols.forEach((c, i) => {
      const cell = headRow.getCell(i + 1);
      cell.value = labels.columnLabel(c);
      cell.font = { bold: true };
      cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
      cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFF1F5F9' } };
      cell.border = BORDER;
    });
    const totals = cols.map(() => 0);
    member.days.forEach((d, r) => {
      const row = ws.getRow(12 + r);
      cols.forEach((c, i) => {
        const v = cellValue(c, d);
        const cell = row.getCell(i + 1);
        cell.value = v;
        cell.border = BORDER;
        cell.alignment = { vertical: 'top', wrapText: c.source === 'content', horizontal: c.source === 'content' || (!c.source && c.type === 'text') ? 'left' : 'center' };
        if (d.weekend) cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFF8FAFC' } };
        if (typeof v === 'number' && (c.source === 'hours' || c.type === 'number')) {
          totals[i] = (totals[i] ?? 0) + v;
          cell.numFmt = '0.0#';
        }
      });
    });
    const totalRow = ws.getRow(12 + member.days.length);
    cols.forEach((c, i) => {
      const cell = totalRow.getCell(i + 1);
      cell.border = BORDER;
      cell.font = { bold: true };
      if (i === 0) cell.value = labels.total;
      else if (c.source === 'hours' || (!c.source && c.type === 'number')) {
        cell.value = Math.round((totals[i] ?? 0) * 100) / 100;
        cell.alignment = { horizontal: 'center' };
      }
    });
  }
  if (report.members.length === 0) wb.addWorksheet(labels.sheetTitle.slice(0, 31));
  const buffer = await wb.xlsx.writeBuffer();
  return new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
}
