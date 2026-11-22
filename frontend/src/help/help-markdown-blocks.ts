// USER-GUIDE-01 UG-1/UG-4: the strict help Markdown subset, parsed into plain data.
// Shared by the in-app panel renderer (HelpMarkdown.tsx) and the offline manual generator
// (tests/Poseidon.Browser.Tests/manual), so both read authored guides with identical semantics.
// This module is pure and dependency free (Node can load it directly with type stripping).
// Supported: paragraphs, "- " and "1. " lists, "### " sub-headings, **bold**, `code` and
// [text](href) links. There is no HTML construct: every character is text for the renderer.

export type HelpInline =
  | { kind: 'text'; text: string }
  | { kind: 'strong'; text: string }
  | { kind: 'code'; text: string }
  | { kind: 'link'; text: string; href: string }

export type HelpBlock = { kind: 'p'; text: string } | { kind: 'h'; text: string } | { kind: 'ul' | 'ol'; items: string[] }

export function helpAnchorId(text: string): string {
  const slug = text.toLocaleLowerCase('en').normalize('NFKD').replace(/[̀-ͯ]/g, '').replace(/ı/g, 'i')
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '')
  return `help-${slug || 'section'}`
}

export function tokenizeHelpInline(text: string): HelpInline[] {
  const out: HelpInline[] = []
  const pattern = /\*\*([^*]+)\*\*|`([^`]+)`|\[([^\]]+)\]\(([^)\s]+)\)/g
  let last = 0
  for (const match of text.matchAll(pattern)) {
    if (match.index > last) out.push({ kind: 'text', text: text.slice(last, match.index) })
    if (match[1] !== undefined) out.push({ kind: 'strong', text: match[1] })
    else if (match[2] !== undefined) out.push({ kind: 'code', text: match[2] })
    else out.push({ kind: 'link', text: match[3], href: match[4] })
    last = match.index + match[0].length
  }
  if (last < text.length) out.push({ kind: 'text', text: text.slice(last) })
  return out
}

export function parseHelpBlocks(source: string): HelpBlock[] {
  const result: HelpBlock[] = []
  let paragraph: string[] = []
  let list: { kind: 'ul' | 'ol'; items: string[] } | null = null
  const flush = () => {
    if (paragraph.length) result.push({ kind: 'p', text: paragraph.join(' ') })
    if (list) result.push(list)
    paragraph = []; list = null
  }
  for (const raw of source.split('\n')) {
    const line = raw.trim()
    if (!line) { flush(); continue }
    const heading = /^### (.+)$/.exec(line)
    const bullet = /^- (.+)$/.exec(line)
    const numbered = /^\d+\. (.+)$/.exec(line)
    if (heading) { flush(); result.push({ kind: 'h', text: heading[1] }); continue }
    const item = bullet ? { kind: 'ul' as const, text: bullet[1] } : numbered ? { kind: 'ol' as const, text: numbered[1] } : null
    if (item) {
      if (paragraph.length || (list && list.kind !== item.kind)) flush()
      list ??= { kind: item.kind, items: [] }
      list.items.push(item.text)
      continue
    }
    if (list) {
      // A continuation line belongs to the previous list item.
      list.items[list.items.length - 1] += ' ' + line
      continue
    }
    paragraph.push(line)
  }
  flush()
  return result
}
