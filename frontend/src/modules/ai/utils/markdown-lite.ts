/**
 * Phân tích Markdown tối giản cho câu trả lời AI (đoạn văn, danh sách -, 1., **đậm**, `code`).
 * Trả về cấu trúc dữ liệu để template render bằng thẻ Vue — không dùng v-html, không có nguy cơ XSS.
 */
export type InlineNode = { kind: 'text' | 'bold' | 'code'; text: string };
export type BlockNode =
  | { kind: 'paragraph'; lines: InlineNode[][] }
  | { kind: 'list'; ordered: boolean; items: InlineNode[][] };

const INLINE = /(\*\*[^*]+\*\*|`[^`]+`)/g;

export function parseInline(text: string): InlineNode[] {
  const nodes: InlineNode[] = [];
  let last = 0;
  for (const match of text.matchAll(INLINE)) {
    const index = match.index ?? 0;
    if (index > last) nodes.push({ kind: 'text', text: text.slice(last, index) });
    const token = match[0];
    nodes.push(token.startsWith('**') ? { kind: 'bold', text: token.slice(2, -2) } : { kind: 'code', text: token.slice(1, -1) });
    last = index + token.length;
  }
  if (last < text.length) nodes.push({ kind: 'text', text: text.slice(last) });
  return nodes;
}

export function parseMarkdownLite(source: string): BlockNode[] {
  const blocks: BlockNode[] = [];
  let paragraph: InlineNode[][] = [];
  let list: { ordered: boolean; items: InlineNode[][] } | null = null;

  const flush = () => {
    if (paragraph.length) blocks.push({ kind: 'paragraph', lines: paragraph });
    if (list) blocks.push({ kind: 'list', ...list });
    paragraph = [];
    list = null;
  };

  for (const raw of source.replace(/\r\n/g, '\n').split('\n')) {
    const line = raw.trimEnd();
    const bullet = /^\s*[-*•]\s+(.*)$/.exec(line);
    const numbered = /^\s*\d+[.)]\s+(.*)$/.exec(line);
    if (!line.trim()) {
      flush();
    } else if (bullet || numbered) {
      const ordered = Boolean(numbered);
      if (paragraph.length || (list && list.ordered !== ordered)) flush();
      list ??= { ordered, items: [] };
      list.items.push(parseInline((bullet ?? numbered)![1] ?? ''));
    } else {
      if (list) flush();
      paragraph.push(parseInline(line));
    }
  }
  flush();
  return blocks;
}
