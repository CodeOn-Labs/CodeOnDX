// Tiện ích HTTP cho mock API: mọi response đều là ErrorFullModel { status, messageText, data } (Q2).

export class ApiError extends Error {
  /**
   * @param {number} status       HTTP status = ErrorFullModel.status
   * @param {string} messageKey   key trong backend/src/Dx.Api/Locales/*.json
   * @param {unknown} [data]
   * @param {Record<string, unknown>} [args] tham số {snake_case} của thông báo
   */
  constructor(status, messageKey, data = null, args = {}) {
    super(messageKey);
    this.status = status;
    this.messageKey = messageKey;
    this.data = data;
    this.args = args;
  }
}

export const badRequest = (errors) => new ApiError(400, 'error.validation_failed', errors);
export const unauthorized = (key = 'error.unauthorized') => new ApiError(401, key);
export const forbidden = () => new ApiError(403, 'error.forbidden');
export const notFound = () => new ApiError(404, 'error.not_found');

export function cultureOf(req) {
  const header = String(req.headers['accept-language'] || '').toLowerCase();
  return header.startsWith('en') ? 'en' : 'vi';
}

export function parseCookies(req) {
  const out = {};
  for (const part of String(req.headers.cookie || '').split(';')) {
    const idx = part.indexOf('=');
    if (idx > 0) out[part.slice(0, idx).trim()] = decodeURIComponent(part.slice(idx + 1).trim());
  }
  return out;
}

export async function readJson(req) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > 1_000_000) throw badRequest({ body: ['validation.invalid_value'] });
    chunks.push(chunk);
  }
  if (chunks.length === 0) return {};
  try {
    return JSON.parse(Buffer.concat(chunks).toString('utf8'));
  } catch {
    throw badRequest({ body: ['validation.invalid_value'] });
  }
}

/**
 * Ghi ErrorFullModel. HTTP status luôn bằng `status` trong body.
 * @param {import('node:http').ServerResponse} res
 */
export function send(res, localizer, culture, status, messageKey, data = null, args = {}, headers = {}) {
  const body = JSON.stringify({
    status,
    messageText: localizer.text(messageKey, culture, args),
    data: data ?? null,
  });
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Cache-Control': 'no-store',
    'Content-Language': culture,
    ...headers,
  });
  res.end(body);
}

/** Đọc tham số dạng danh sách: ?statusIds=a,b hoặc ?statusIds=a&statusIds=b */
export function listParam(url, name) {
  return url.searchParams.getAll(name).flatMap((v) => v.split(',')).map((v) => v.trim()).filter(Boolean);
}

/** Phân trang chuẩn: page bắt đầu từ 1; pageSize = 0 nghĩa là lấy tất cả. */
export function paginate(rows, url, defaultSize = 20) {
  const page = Math.max(1, Number.parseInt(url.searchParams.get('page') || '1', 10) || 1);
  const rawSize = url.searchParams.get('pageSize');
  const pageSize = rawSize === null ? defaultSize : Math.max(0, Math.min(500, Number.parseInt(rawSize, 10) || 0));
  const items = pageSize === 0 ? rows : rows.slice((page - 1) * pageSize, page * pageSize);
  return { items, page: pageSize === 0 ? 1 : page, pageSize, totalCount: rows.length };
}

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;
export function dateParam(url, name, errors) {
  const value = url.searchParams.get(name);
  if (!value) return null;
  if (!DATE_RE.test(value) || Number.isNaN(new Date(`${value}T00:00:00Z`).getTime())) {
    errors[name] = ['validation.invalid_date'];
    return null;
  }
  return value;
}
