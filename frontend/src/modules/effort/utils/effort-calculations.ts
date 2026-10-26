/**
 * Công thức của lưới Công số (màn `effort`) — port effort-calculations.ts của bản React.
 *
 * Nguyên tắc (D12 — chốt 20/10/2026):
 *   - CÔNG THỨC cố định trong code (file này). Muốn đổi cách tính → sửa ở đây, chạy lại test, và sửa phần tương ứng ở BE
 *     (BE tính lại khi lưu/xuất báo cáo nên hai bên phải giống nhau).
 *   - THAM SỐ do quản trị tenant chỉnh trên giao diện, không hard-code:
 *       • Hệ số hiệu suất `performance`  → Quản trị › Danh mục, loại "Mức hiệu suất" (`performance_level`),
 *         lưu ở `metadata.coefficient` (0 – 2). Bộ mặc định: Học việc 0 · Hỗ trợ một phần 0,5 · Thử việc 0,8 · Chính thức 1.
 *       • Số giờ của 1 FTE                → Quản trị › Quy tắc, `TIMESHEET_FTE_HOURS` (mặc định 8).
 *       • Ngưỡng quá tải / chờ việc (%)   → Quản trị › Quy tắc, `HR_OVERLOAD_ALLOCATION_THRESHOLD`, `HR_MIN_ALLOCATION_THRESHOLD`.
 *
 * Thuật ngữ một dòng công số (1 nhân sự × 1 dự án × 1 vai trò × 1 tháng), đơn vị là FTE-tháng:
 *   HĐ (`hd`)              Công số theo hợp đồng với khách hàng — số được phép tính tiền.
 *   TT effort (`tt_effort`) Công số thực tế nhân sự bỏ vào dự án trong tháng (không gồm OT).
 *   OT effort (`ot_effort`) Công số làm thêm giờ, tính riêng, không cộng vào Actual.
 *   performance            Hệ số hiệu suất của nhân sự (xem trên) — 1 = làm được trọn công việc của 1 người.
 *   allocate_rate          Tỷ lệ % công số của dòng này được tính cho dự án (0 – 100). VD 50 = chỉ nửa công tính cho dự án.
 *
 * Ví dụ đầy đủ ở cuối file.
 */

export interface EffortAllocationInput {
  employee_id: string;
  hd: number;
  tt_effort: number;
  /** Hệ số hiệu suất (metadata.coefficient của mức hiệu suất) */
  performance: number;
  /** % (0 – 100) */
  allocate_rate: number;
}

export interface GroupTotalsInput {
  employee_id: string;
  hd: number;
  actual: number;
  actual1: number;
  tt_effort: number;
  ot_effort: number;
  /** Free của NHÂN SỰ (đã tính trên tất cả dòng của người đó) — xem buildFreeByMemberMap */
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

/** Sai số làm tròn số thực: |free| nhỏ hơn mức này coi như 0 (không tô màu "còn rảnh"). */
export const FREE_EPSILON = 0.001;

/**
 * Actual — công số thực tế được tính cho dự án, CHƯA xét hiệu suất.
 *
 *   Actual = TT effort × allocate_rate / 100
 *
 * VD: TT effort 1,0; allocate_rate 50 → Actual = 0,5.
 */
export function calcActual(ttEffort: number, allocateRate: number): number {
  return ttEffort * (allocateRate / 100);
}

/**
 * Actual1 — công số thực tế ĐÃ nhân hệ số hiệu suất; dùng so với HĐ để biết dự án có "lỗ công" không.
 *
 *   Actual1 = TT effort × performance × allocate_rate / 100
 *
 * VD: TT effort 1,0; performance 0,8 (Thử việc); allocate_rate 100 → Actual1 = 0,8.
 * Muốn đổi hệ số → sửa ở Quản trị › Danh mục › Mức hiệu suất, KHÔNG sửa ở đây.
 */
export function calcActual1(ttEffort: number, performance: number, allocateRate: number): number {
  return ttEffort * performance * (allocateRate / 100);
}

/**
 * Free theo NHÂN SỰ — phần công số đã bỏ ra nhưng không được tính cho dự án nào.
 *
 *   Free(nhân sự) = Σ TT effort − Σ Actual   (cộng trên mọi dòng của nhân sự đó trong tháng)
 *
 * Tính theo người chứ không theo dòng, vì một người có thể chia công cho nhiều dự án.
 * VD: An có 2 dòng: (TT 0,6; rate 100) và (TT 0,4; rate 50) → ΣTT = 1,0; ΣActual = 0,6 + 0,2 = 0,8 → Free = 0,2.
 */
export function buildFreeByMemberMap(rows: EffortAllocationInput[]): Map<string, number> {
  const ttEffortByMember = new Map<string, number>();
  const actualByMember = new Map<string, number>();

  for (const row of rows) {
    ttEffortByMember.set(row.employee_id, (ttEffortByMember.get(row.employee_id) ?? 0) + row.tt_effort);
    actualByMember.set(row.employee_id, (actualByMember.get(row.employee_id) ?? 0) + calcActual(row.tt_effort, row.allocate_rate));
  }

  const freeByMember = new Map<string, number>();
  for (const [employeeId, totalTtEffort] of ttEffortByMember) {
    freeByMember.set(employeeId, totalTtEffort - (actualByMember.get(employeeId) ?? 0));
  }
  return freeByMember;
}

/**
 * Free1 theo DÒNG — chênh lệch giữa công số thực tế tính cho dự án và công số hợp đồng.
 *
 *   Free1 = Actual − HĐ
 *
 * Dương: dự án dùng nhiều công hơn hợp đồng (vượt công). Âm: dùng ít hơn hợp đồng (còn dư công để bán).
 * VD: HĐ 1,0; Actual 0,8 → Free1 = −0,2.
 */
export function calcFree1(row: EffortAllocationInput): number {
  return calcActual(row.tt_effort, row.allocate_rate) - row.hd;
}

/**
 * Tổng của một nhóm dòng (phòng ban / team / dự án) ở hàng tổng của lưới.
 *
 * Mọi cột cộng thẳng, RIÊNG Free cộng mỗi nhân sự một lần — vì Free đã là số của cả người (buildFreeByMemberMap),
 * cộng theo dòng sẽ bị đếm trùng khi một người có nhiều dòng trong nhóm.
 */
export function calcGroupTotals(rows: GroupTotalsInput[]): GroupTotals {
  const sum = (pick: (row: GroupTotalsInput) => number) => rows.reduce((total, row) => total + pick(row), 0);

  const seenEmployeeIds = new Set<string>();
  let free = 0;
  for (const row of rows) {
    if (seenEmployeeIds.has(row.employee_id)) continue;
    seenEmployeeIds.add(row.employee_id);
    free += row.free;
  }

  return {
    hd: sum((r) => r.hd),
    ttEffort: sum((r) => r.tt_effort),
    otEffort: sum((r) => r.ot_effort),
    actual: sum((r) => r.actual),
    actual1: sum((r) => r.actual1),
    free,
    free1: sum((r) => r.free1),
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
 * Ví dụ đầy đủ — Bình, mức "Thử việc" (performance 0,8), tháng 10:
 *
 *   Dòng   HĐ    TT effort   allocate_rate   Actual            Actual1                 Free1
 *   DA-A   0,5   0,6         100             0,6×1   = 0,60    0,6×0,8×1   = 0,48      0,60 − 0,5 = +0,10
 *   DA-B   0,5   0,4         50              0,4×0,5 = 0,20    0,4×0,8×0,5 = 0,16      0,20 − 0,5 = −0,30
 *
 *   Free(Bình) = (0,6 + 0,4) − (0,60 + 0,20) = 0,20
 *   Tổng nhóm chỉ có Bình: HĐ 1,0 · TT 1,0 · Actual 0,80 · Actual1 0,64 · Free 0,20 (1 lần) · Free1 −0,20
 */
