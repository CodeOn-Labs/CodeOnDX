#!/usr/bin/env node
/**
 * i18n-check — các luật đa ngôn ngữ riêng của dự án (Q1 trong claude/dx-conventions.md) mà plugin eslint không có.
 *
 *   node tools/i18n-check/i18n-check.mjs        # chạy từ gốc repo (hoặc `npm run i18n:check` trong frontend/)
 *
 * Không cần cài thư viện (Node ≥ 20). Lỗi → exit code 1 (fail CI).
 *
 * FE (frontend/src/locales/*.json):
 *   1. Cấp 1 là screen_key trong shared/screens.json hoặc 1 trong 6 vùng chung.
 *   2. Mọi đoạn key là snake_case; tối đa 3 cấp; khối cấp 2 (nếu có) kết thúc _dialog/_drawer/_panel/_section.
 *   3. Key lá bắt đầu bằng tiền tố hợp lệ (trừ vùng menu, enum, validation, error).
 *   4. vi.json và en.json có cùng bộ key, cùng tham số {x}; giá trị không rỗng.
 *   5. Mọi màn trong shared/screens.json có `<screen_key>.title_page`.
 *   6. Quét mã nguồn: key trong $t / t / i18n.global.t / te / <i18n-t keypath> phải tồn tại;
 *      không ghép key động (trừ src/shared/i18n/); luật chống xung đột theo thư mục:
 *        - screens/<screen_key>/…   → chỉ <screen_key>.* + vùng chung
 *        - file .vue trong components/ → chỉ component.<tên_file_snake>.* + common/menu/enum/validation/error
 *          (riêng components/layout/ được dùng mọi component.*)
 * BE (backend/src/Dx.Api/Locales/*.json): snake_case, tối đa 2 cấp, vi/en cùng key + tham số.
 */
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { dirname, join, relative, resolve, sep, basename, extname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const FE_SRC = join(ROOT, 'frontend/src');
const FE_LOCALES = join(FE_SRC, 'locales');
const BE_LOCALES = join(ROOT, 'backend/src/Dx.Api/Locales');
const SCREENS = JSON.parse(readFileSync(join(ROOT, 'shared/screens.json'), 'utf8'));

const SHARED_AREAS = ['common', 'menu', 'enum', 'validation', 'error', 'component'];
const NO_PREFIX_AREAS = ['menu', 'enum', 'validation', 'error'];
const PREFIXES = ['title', 'btn', 'tab', 'lbl', 'col', 'ph', 'opt', 'tip', 'hint', 'stat', 'unit', 'empty', 'confirm', 'msg', 'err'];
const BLOCK_SUFFIXES = ['_dialog', '_drawer', '_panel', '_section'];
const SEGMENT = /^[a-z0-9_]+$/;
const DYNAMIC_ALLOWED_DIR = join(FE_SRC, 'shared/i18n') + sep;

const screenKeys = Object.keys(SCREENS).filter((k) => !k.startsWith('$'));
const errors = [];
const warnings = [];
const err = (msg) => errors.push(msg);

// ─── Tiện ích ────────────────────────────────────────────────────────────────

function flatten(obj, prefix = '', out = {}) {
  for (const [k, v] of Object.entries(obj)) {
    const key = prefix ? `${prefix}.${k}` : k;
    if (v !== null && typeof v === 'object' && !Array.isArray(v)) flatten(v, key, out);
    else out[key] = v;
  }
  return out;
}

const paramsOf = (text) => [...String(text).matchAll(/\{\s*([a-zA-Z0-9_]+)\s*\}/g)].map((m) => m[1]).sort().join(',');
const hasPrefix = (leaf) => PREFIXES.some((p) => leaf === p || leaf.startsWith(`${p}_`));
const snake = (name) => name.replace(/([a-z0-9])([A-Z])/g, '$1_$2').replace(/([A-Z])([A-Z][a-z])/g, '$1_$2').toLowerCase();

function readLocale(dir, lang) {
  const file = join(dir, `${lang}.json`);
  if (!existsSync(file)) {
    err(`Thiếu file ${relative(ROOT, file)}`);
    return {};
  }
  try {
    return flatten(JSON.parse(readFileSync(file, 'utf8')));
  } catch (e) {
    err(`${relative(ROOT, file)}: JSON không hợp lệ — ${e.message}`);
    return {};
  }
}

function compareLocales(label, vi, en) {
  for (const key of Object.keys(vi)) {
    if (!(key in en)) err(`${label}: "${key}" có trong vi.json nhưng thiếu trong en.json`);
    else if (paramsOf(vi[key]) !== paramsOf(en[key])) err(`${label}: "${key}" tham số khác nhau giữa vi (${paramsOf(vi[key]) || '—'}) và en (${paramsOf(en[key]) || '—'})`);
    if (typeof vi[key] !== 'string' || !vi[key].trim()) err(`${label}: "${key}" trong vi.json phải là chuỗi không rỗng`);
  }
  for (const key of Object.keys(en)) {
    if (!(key in vi)) err(`${label}: "${key}" có trong en.json nhưng thiếu trong vi.json`);
    if (typeof en[key] !== 'string' || !en[key].trim()) err(`${label}: "${key}" trong en.json phải là chuỗi không rỗng`);
  }
}

// ─── 1–5. Cấu trúc locale FE ─────────────────────────────────────────────────

const feVi = readLocale(FE_LOCALES, 'vi');
const feEn = readLocale(FE_LOCALES, 'en');

for (const key of Object.keys(feVi)) {
  const parts = key.split('.');
  const [top] = parts;
  if (parts.some((p) => !SEGMENT.test(p))) err(`FE: "${key}" — mọi đoạn key phải là snake_case (a-z, 0-9, _)`);
  if (parts.length < 2 || parts.length > 3) {
    err(`FE: "${key}" — key phải có 2 hoặc 3 cấp`);
    continue;
  }
  if (!screenKeys.includes(top) && !SHARED_AREAS.includes(top)) {
    err(`FE: "${key}" — cấp 1 "${top}" không phải screen_key trong shared/screens.json hay vùng dùng chung`);
    continue;
  }
  const leaf = parts[parts.length - 1];
  if (top === 'component' || top === 'enum') {
    if (parts.length !== 3) err(`FE: "${key}" — vùng ${top} phải có dạng ${top}.<tên>.<key>`);
  } else if (parts.length === 3 && !BLOCK_SUFFIXES.some((s) => parts[1].endsWith(s))) {
    err(`FE: "${key}" — khối "${parts[1]}" phải kết thúc bằng ${BLOCK_SUFFIXES.join(' / ')}`);
  }
  if (!NO_PREFIX_AREAS.includes(top) && !hasPrefix(leaf)) err(`FE: "${key}" — key lá "${leaf}" phải bắt đầu bằng tiền tố: ${PREFIXES.join(', ')}`);
}

compareLocales('FE', feVi, feEn);

for (const screen of screenKeys) {
  if (!(`${screen}.title_page` in feVi)) err(`FE: màn "${screen}" chưa có key "${screen}.title_page"`);
}

// ─── 6. Quét mã nguồn FE ─────────────────────────────────────────────────────

function walk(dir, out = []) {
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules' || name.startsWith('.')) continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) walk(full, out);
    else if (['.vue', '.ts'].includes(extname(name)) && !name.endsWith('.d.ts')) out.push(full);
  }
  return out;
}

const STATIC_CALL = /(?:\$t|\$te|\bt|\bte|i18n\.global\.t|i18n\.global\.te)\(\s*(['"])([^'"]+)\1/g;
const KEYPATH = /keypath\s*=\s*"([^"]+)"/g;
const DYNAMIC_CALL = /(?:\$t|\bt|i18n\.global\.t)\(\s*(?![\s'")])/g;

function allowedFor(file) {
  const rel = relative(FE_SRC, file).split(sep).join('/');
  const screenMatch = /(?:^|\/)screens\/([a-z0-9_]+)\//.exec(rel);
  if (screenMatch) {
    const screen = screenMatch[1];
    if (!screenKeys.includes(screen)) err(`${rel}: thư mục screens/${screen}/ không khớp screen_key nào trong shared/screens.json`);
    return { kind: 'screen', test: (key) => key.startsWith(`${screen}.`) || SHARED_AREAS.includes(key.split('.')[0]), rule: `${screen}.* hoặc vùng dùng chung` };
  }
  if (rel.startsWith('components/layout/')) {
    return { kind: 'layout', test: (key) => ['menu', 'common', 'component'].includes(key.split('.')[0]), rule: 'menu.* / common.* / component.*' };
  }
  if (extname(file) === '.vue' && /(?:^|\/)components\//.test(rel)) {
    const own = `component.${snake(basename(file, '.vue'))}.`;
    return {
      kind: 'component',
      test: (key) => key.startsWith(own) || NO_PREFIX_AREAS.concat('common').includes(key.split('.')[0]),
      rule: `${own}* hoặc common/menu/enum/validation/error`,
    };
  }
  return { kind: 'other', test: (key) => SHARED_AREAS.includes(key.split('.')[0]), rule: 'vùng dùng chung (key của màn chỉ dùng trong screens/<screen_key>/)' };
}

const usedKeys = new Set();
for (const file of walk(FE_SRC)) {
  if (file.startsWith(FE_LOCALES)) continue;
  const rel = relative(ROOT, file);
  const source = readFileSync(file, 'utf8');
  const allowed = allowedFor(file);
  const keys = [...source.matchAll(STATIC_CALL)].map((m) => m[2]).concat([...source.matchAll(KEYPATH)].map((m) => m[1]));

  for (const key of keys) {
    usedKeys.add(key);
    if (!(key in feVi)) err(`${rel}: key "${key}" không có trong frontend/src/locales/vi.json`);
    if (!allowed.test(key)) err(`${rel}: dùng key "${key}" — file này chỉ được dùng ${allowed.rule} (luật chống xung đột)`);
  }

  if (!file.startsWith(DYNAMIC_ALLOWED_DIR)) {
    for (const m of source.matchAll(DYNAMIC_CALL)) {
      const line = source.slice(0, m.index).split('\n').length;
      err(`${rel}:${line}: ghép key i18n động — chỉ được làm trong src/shared/i18n/ (enumLabel, screenTitle)`);
    }
  }
}

// Key không còn dùng (bỏ qua enum.* và *.title_page vì được dùng qua enumLabel / screenTitle)
for (const key of Object.keys(feVi)) {
  if (usedKeys.has(key) || key.startsWith('enum.') || key.endsWith('.title_page')) continue;
  warnings.push(`FE: key "${key}" không thấy được dùng trong mã nguồn`);
}

// ─── BE ─────────────────────────────────────────────────────────────────────

const beVi = readLocale(BE_LOCALES, 'vi');
const beEn = readLocale(BE_LOCALES, 'en');
for (const key of Object.keys(beVi)) {
  const parts = key.split('.');
  if (parts.length !== 2) err(`BE: "${key}" — key BE có dạng <chức_năng>_<hành_động>.<key> (2 cấp)`);
  if (parts.some((p) => !SEGMENT.test(p))) err(`BE: "${key}" — mọi đoạn key phải là snake_case`);
  const [group, leaf] = parts;
  // Mẫu email (connector Email): email_<mẫu>.subject / .body
  if (group?.startsWith('email_')) {
    if (!['subject', 'body'].includes(leaf)) err(`BE: "${key}" — mẫu email chỉ có key lá subject và body`);
    continue;
  }
  if (!['common', 'error', 'validation'].includes(group) && leaf && !/^(msg_success|err_[a-z0-9_]+)$/.test(leaf)) {
    err(`BE: "${key}" — key lá phải là msg_success hoặc err_<lý_do>`);
  }
}
compareLocales('BE', beVi, beEn);

// ─── Kết quả ────────────────────────────────────────────────────────────────

for (const w of warnings) console.warn(`⚠ ${w}`);
for (const e of errors) console.error(`✖ ${e}`);
console.log(
  `\ni18n-check: FE ${Object.keys(feVi).length} key, BE ${Object.keys(beVi).length} key, ${usedKeys.size} key được dùng trong mã nguồn — ${errors.length} lỗi, ${warnings.length} cảnh báo.`,
);
process.exit(errors.length > 0 ? 1 : 0);
