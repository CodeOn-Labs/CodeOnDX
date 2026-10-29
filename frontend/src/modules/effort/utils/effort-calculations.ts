/**
 * Công thức của lưới Công số (màn `effort`).
 *
 * Nguyên tắc (D12 — chốt 20/10/2026):
 *   - CÔNG THỨC cố định trong code (file này). Muốn đổi cách tính → sửa ở đây, chạy lại test, và sửa phần tương ứng ở BE
 *     nếu BE có tính lại (hiện BE chỉ lưu số liệu nhập, Actual / Actual1 / Free / Free1 tính ở FE).
 *   - THAM SỐ do quản trị tenant chỉnh trên giao diện, không hard-code:
 *       • Hệ số `performance`             → Quản trị › Danh mục, loại "Mức hiệu suất" (`performance_level`),
 *         lưu ở `metadata.coefficient` (0 – 2). Bộ mặc định của tenant demo theo loại hợp đồng: Thử việc TV 0,8 ·
 *         Chính thức CT 1,0 · Cộng tác viên CTV 0,5 · Thực tập sinh TTS 0,5 (dòng mới mặc định CT).
 *       • Số giờ của 1 FTE                → Quản trị › Quy tắc, `TIMESHEET_FTE_HOURS` (mặc định 8).
 *
 * Thuật ngữ một dòng công số (1 nhân sự × 1 dự án × 1 vai trò × 1 tháng), đơn vị MM (man-month, 1 = trọn công 1 người/tháng):
 *   Kế hoạch (`hd`)            Công số PMO dự kiến nhân sự làm cho dự án trong tháng (0 – 2). Nhập Kế hoạch → Thực tế tự bằng giá trị này.
 *   Thực tế (`tt_effort`)      Công số thực tế nhân sự làm cho dự án trong tháng, CHƯA gồm OT. PMO sửa khi kế hoạch thay đổi.
 *   OT Effort (`ot_effort`)    Công số làm thêm ngoài giờ quy định — đồng bộ từ hệ thống OT, khoá trên lưới.
 *   Performance (`performance`) Hệ số theo loại hợp đồng của nhân sự (xem trên) — đồng bộ từ hồ sơ nhân sự, khoá trên lưới.
 *
 * Ví dụ đầy đủ ở cuối file.
 */

export interface EffortAllocationInput {
  employee_id: string;
  tt_effort: number;
  ot_effort: number;
  /** Hệ số performance (metadata.coefficient của mức hiệu suất) */
  performance: number;
}

export interface GroupTotalsInput {
  employee_id: string;
  hd: number;
  tt_effort: number;
  ot_effort: number;
  actual: number;
  actual1: number;
  /** Free / Free1 của NHÂN SỰ (đã tính trên tất cả dòng của người đó) — xem buildFreeByMemberMap */
  free: number;
  free1: number;
}

export interface GroupTotals {
  hd: number;
  ttEffort: number;
  otEffort: number;
  actual: number;
  actual1: number;
  free: number;
  free1: number;
}

export interface MemberFree {
  free: number;
  free1: number;
}

/** Công số trọn vẹn của 1 người trong 1 tháng (1 MM) — mốc để tính Free / Free1. */
export const FULL_MONTH_EFFORT = 1;

/** Sai số làm tròn số thực: |free| nhỏ hơn mức này coi như 0 (không tô màu "còn rảnh"). */
export const FREE_EPSILON = 0.001;

/**
 * Actual — công số của 1 người trong dự án, GỒM CẢ OT, CHƯA nhân Performance. Kế toán dùng số này để tính chi phí.
 *
 *   Actual = Thực tế + OT
 *
 * VD: Thực tế 0,6; OT 0,1 → Actual = 0,7.
 */
export function calcActual(ttEffort: number, otEffort: number): number {
  return ttEffort + otEffort;
}

/**
 * Actual1 — công số ĐÃ nhân Performance.
 *
 *   Actual1 = (Thực tế + OT) × Performance
 *
 * VD: Thực tế 0,6; OT 0,1; Performance 0,8 (Thử việc) → Actual1 = 0,7 × 0,8 = 0,56.
 * Muốn đổi hệ số → sửa ở Quản trị › Danh mục › Mức hiệu suất, KHÔNG sửa ở đây.
 */
export function calcActual1(ttEffort: number, otEffort: number, performance: number): number {
  return calcActual(ttEffort, otEffort) * performance;
}

/**
 * Free / Free1 theo NHÂN SỰ (theo ezID) — công số còn rảnh của người đó trong tháng.
 *
 *   Free  = 1 − Σ Actual  (cộng trên mọi dòng của nhân sự đó trong tháng; chưa nhân Performance)
 *   Free1 = 1 − Σ Actual1 (đã nhân Performance)
 *
 * Tính theo người chứ không theo dòng, vì một người có thể chia công cho nhiều dự án; mọi dòng của người đó hiện cùng một số.
 * Âm = người đó đã làm quá 1 MM trong tháng (Free1 âm tô đỏ trên lưới).
 * VD: An (Performance 1) có 2 dòng: (Thực tế 0,6; OT 0) và (Thực tế 0,3; OT 0,2) → Σ Actual = 1,1 → Free = −0,1; Free1 = −0,1.
 */
export function buildFreeByMemberMap(rows: EffortAllocationInput[]): Map<string, MemberFree> {
  const actualByMember = new Map<string, number>();
  const actual1ByMember = new Map<string, number>();

  for (const row of rows) {
    actualByMember.set(row.employee_id, (actualByMember.get(row.employee_id) ?? 0) + calcActual(row.tt_effort, row.ot_effort));
    actual1ByMember.set(row.employee_id, (actual1ByMember.get(row.employee_id) ?? 0) + calcActual1(row.tt_effort, row.ot_effort, row.performance));
  }

  const freeByMember = new Map<string, MemberFree>();
  for (const [employeeId, totalActual] of actualByMember) {
    freeByMember.set(employeeId, {
      free: FULL_MONTH_EFFORT - totalActual,
      free1: FULL_MONTH_EFFORT - (actual1ByMember.get(employeeId) ?? 0),
    });
  }
  return freeByMember;
}

/**
 * Tổng của một nhóm dòng (phòng ban) ở hàng tổng của lưới.
 *
 * Kế hoạch / Thực tế / OT / Actual / Actual1 cộng thẳng theo dòng. RIÊNG Free và Free1 cộng mỗi nhân sự một lần — vì đó là số
 * của cả người (buildFreeByMemberMap), cộng theo dòng sẽ bị đếm trùng khi một người có nhiều dòng trong nhóm.
 */
export function calcGroupTotals(rows: GroupTotalsInput[]): GroupTotals {
  const sum = (pick: (row: GroupTotalsInput) => number) => rows.reduce((total, row) => total + pick(row), 0);

  const seenEmployeeIds = new Set<string>();
  let free = 0;
  let free1 = 0;
  for (const row of rows) {
    if (seenEmployeeIds.has(row.employee_id)) continue;
    seenEmployeeIds.add(row.employee_id);
    free += row.free;
    free1 += row.free1;
  }

  return {
    hd: sum((r) => r.hd),
    ttEffort: sum((r) => r.tt_effort),
    otEffort: sum((r) => r.ot_effort),
    actual: sum((r) => r.actual),
    actual1: sum((r) => r.actual1),
    free,
    free1,
  };
}

/** Đổi số giờ sang FTE-ngày: giờ / số giờ của 1 FTE (quy tắc TIMESHEET_FTE_HOURS, mặc định 8). */
export function hoursToFte(hours: number, fteHours: number): number {
  return fteHours > 0 ? hours / fteHours : 0;
}

// ─── Hiển thị ───────────────────────────────────────────────────────────────

/** Làm tròn để hiển thị (mặc định 2 chữ số thập phân). Chỉ dùng khi hiển thị — không làm tròn giữa các bước tính. */
export function roundToDecimals(value: number, decimals = 2): number {
  const factor = 10 ** decimals;
  return Math.round(value * factor) / factor;
}

export function formatEffortNumber(value: number, decimals = 2): string {
  return value.toFixed(decimals);
}

/** Gom dòng theo phòng ban (null = chưa gán phòng ban). */
export function groupByDepartment<T extends { department_id: string | null }>(rows: T[]): Map<string | null, T[]> {
  const grouped = new Map<string | null, T[]>();
  for (const row of rows) {
    const existing = grouped.get(row.department_id) ?? [];
    existing.push(row);
    grouped.set(row.department_id, existing);
  }
  return grouped;
}

/*
 * Ví dụ đầy đủ — Bình, Thử việc (Performance 0,8), tháng 10:
 *
 *   Dòng   Kế hoạch   Thực tế   OT     Actual              Actual1
 *   DA-A   0,6        0,6       0,1    0,6 + 0,1 = 0,70    0,70 × 0,8 = 0,56
 *   DA-B   0,4        0,3       0      0,3 + 0   = 0,30    0,30 × 0,8 = 0,24
 *
 *   Free(Bình)  = 1 − (0,70 + 0,30) = 0,00
 *   Free1(Bình) = 1 − (0,56 + 0,24) = 0,20
 *   Tổng nhóm chỉ có Bình: Kế hoạch 1,0 · Thực tế 0,9 · OT 0,1 · Actual 1,00 · Actual1 0,80 · Free 0,00 (1 lần) · Free1 0,20 (1 lần)
 */
