/** Cấu trúc response cố định của mọi API (Q2). JSON camelCase: { status, messageText, data }. */
export interface ErrorFullModel<T = unknown> {
  status: number;
  messageText: string;
  data: T;
}

/** data của API danh sách có phân trang (page bắt đầu từ 1; pageSize = 0 là lấy tất cả). */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Lỗi 400: { "<field>": ["<thông báo>"] } — FE gắn vào form. */
export type ValidationErrors = Record<string, string[]>;

export interface MasterDataRef {
  id: string;
  code: string;
  name: string;
  color: string | null;
}

export interface MasterDataItem extends MasterDataRef {
  type: string;
  sortSeq: number;
  metadata: Record<string, unknown>;
}

export interface ClientOption {
  id: string;
  code: string;
  name: string;
  countryCode: string | null;
  projectsCount: number;
}

/** Quy tắc hệ thống (GET /system-rules). `name`, `description` đã dịch theo Accept-Language. */
export interface SystemRule {
  key: string;
  /** timesheet | project | hr | leave | overtime | attendance */
  module: string;
  valueType: 'number' | 'boolean';
  value: number | boolean | null;
  defaultValue: number | boolean | null;
  min: number | null;
  max: number | null;
  /** 1 = đang áp dụng, 0 = tạm tắt (hệ thống bỏ qua kiểm tra, dùng giá trị mặc định trong code) */
  status: number;
  name: string;
  description: string;
  updatedAt: string | null;
}
