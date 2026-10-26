// Tìm kiếm trong tab Quản trị dự án: không dấu, không phân biệt hoa thường (giống filter-change-requests.ts của bản gốc).
export function fold(text: string | null | undefined): string {
  return String(text ?? '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase();
}

export function matchesQuery(query: string, ...fields: Array<string | null | undefined>): boolean {
  const q = fold(query).trim();
  return !q || fields.some((f) => fold(f).includes(q));
}
