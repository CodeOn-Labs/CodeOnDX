// Tiện ích kiểm tra dữ liệu vào dùng chung cho các nhóm endpoint của mock API.
// Lỗi gom theo field: { field: ['validation.required' | { key, args }] } — server.mjs dịch sang câu chữ theo Accept-Language.
import { badRequest } from './http.mjs';

export const nowIso = () => new Date().toISOString();
export const idFromPath = (url, index = -1) => decodeURIComponent(url.pathname.split('/').at(index) || '');
export const str = (value) => (typeof value === 'string' ? value.trim() : '');
export const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
export const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
export const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

export function addError(errors, field, error) {
  (errors[field] ??= []).push(error);
}
export function required(errors, field, value) {
  if (value === undefined || value === null || value === '' || (Array.isArray(value) && value.length === 0)) {
    addError(errors, field, 'validation.required');
    return false;
  }
  return true;
}
export function maxLength(errors, field, value, max) {
  if (value && value.length > max) addError(errors, field, { key: 'validation.max_length', args: { max } });
}
export function numberInRange(errors, field, value, min, max, { integer = false } = {}) {
  const n = typeof value === 'number' ? value : Number(value);
  if (value === null || value === undefined || value === '' || !Number.isFinite(n)) {
    addError(errors, field, 'validation.invalid_number');
    return null;
  }
  if (integer && !Number.isInteger(n)) addError(errors, field, 'validation.integer');
  if (min !== null && min !== undefined && n < min) addError(errors, field, { key: 'validation.min_value', args: { min } });
  if (max !== null && max !== undefined && n > max) addError(errors, field, { key: 'validation.max_value', args: { max } });
  return n;
}
export function validDate(errors, field, value, { optional = false } = {}) {
  if (!value) {
    if (!optional) addError(errors, field, 'validation.required');
    return null;
  }
  if (!DATE_PATTERN.test(value) || Number.isNaN(Date.parse(value))) {
    addError(errors, field, 'validation.invalid_date');
    return null;
  }
  return value;
}
export function validEmail(errors, field, value) {
  if (required(errors, field, value) && !EMAIL_PATTERN.test(value)) addError(errors, field, 'validation.invalid_email');
}
export function throwIfErrors(errors) {
  if (Object.keys(errors).length > 0) throw badRequest(errors);
}
/** Tìm kiếm không dấu, không phân biệt hoa thường. */
export function fold(text) {
  return String(text ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
}
export function matches(query, ...fields) {
  const q = fold(query).trim();
  return !q || fields.some((f) => fold(f).includes(q));
}
