// Đọc backend/src/Dx.Api/Locales/{vi,en}.json — cùng nguồn câu chữ mà JsonStringLocalizer của BE .NET sẽ dùng (Q1).
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

export function createLocalizer(localesDir) {
  const load = (culture) => JSON.parse(readFileSync(join(localesDir, `${culture}.json`), 'utf8'));
  const messages = { vi: load('vi'), en: load('en') };

  const lookup = (culture, key) => {
    let node = messages[culture];
    for (const part of key.split('.')) {
      if (node == null || typeof node !== 'object') return undefined;
      node = node[part];
    }
    return typeof node === 'string' ? node : undefined;
  };

  return {
    /** Thiếu key tiếng Anh → lấy tiếng Việt; thiếu cả hai → trả nguyên key và cảnh báo. */
    text(key, culture = 'vi', args = {}) {
      const template = lookup(culture, key) ?? lookup('vi', key);
      if (template === undefined) {
        console.warn(`[i18n] thiếu key BE: ${key}`);
        return key;
      }
      return template.replace(/\{([a-z0-9_]+)\}/g, (_, name) => (name in args ? String(args[name]) : `{${name}}`));
    },
  };
}
