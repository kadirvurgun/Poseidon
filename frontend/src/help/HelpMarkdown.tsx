import type { ReactNode } from 'react'

// USER-GUIDE-01 UG-1: strict Markdown subset rendered to React elements.
// There is no HTML path at all: no dangerouslySetInnerHTML, no raw HTML pass-through, no
// script/style/iframe/image element. Every character of authored text becomes a React text node
// (escaped by React); step screenshots are appended only by the caller (afterStep), never by content. Supported: paragraphs, "- " and "1. " lists, "### " sub-headings,
// **bold**, `code` and [text](href) links. A link is rendered only when its href is an internal
// route the current user's navigation already contains, or a relative "#anchor" inside the
// panel; any other target (external URL, javascript:, data:, hidden route) renders as plain text.

export type HelpLinkPolicy = {
  /** Route paths (without query) the current user's navigation contains. */
  allowedRoutes: ReadonlySet<string>
  onNavigate: (href: string) => void
  onAnchor: (id: string) => void
}

const appRoute = /^\/app(?:\/[a-z0-9-]+)+$/
const anchor = /^#[a-z0-9-]+$/

export function helpAnchorId(text: string): string {
  const slug = text.toLocaleLowerCase('en').normalize('NFKD').replace(/[̀-ͯ]/g, '').replace(/ı/g, 'i')
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '')
  return `help-${slug || 'section'}`
}

function inline(text: string, policy: HelpLinkPolicy, prefix: string): ReactNode[] {
  const out: ReactNode[] = []
  const pattern = /\*\*([^*]+)\*\*|`([^`]+)`|\[([^\]]+)\]\(([^)\s]+)\)/g
  let last = 0, index = 0
  for (const match of text.matchAll(pattern)) {
    if (match.index > last) out.push(text.slice(last, match.index))
    const key = `${prefix}-${index++}`
    if (match[1] !== undefined) out.push(<strong key={key}>{match[1]}</strong>)
    else if (match[2] !== undefined) out.push(<code key={key}>{match[2]}</code>)
    else {
      const label = match[3], href = match[4]
      if (appRoute.test(href) && policy.allowedRoutes.has(href))
        out.push(<a key={key} href={href} onClick={event => { event.preventDefault(); policy.onNavigate(href) }}>{label}</a>)
      else if (anchor.test(href))
        out.push(<a key={key} href={href} onClick={event => { event.preventDefault(); policy.onAnchor(`help-${href.slice(1)}`) }}>{label}</a>)
      else out.push(label)
    }
    last = match.index + match[0].length
  }
  if (last < text.length) out.push(text.slice(last))
  return out
}

type Block = { kind: 'p'; text: string } | { kind: 'h'; text: string } | { kind: 'ul' | 'ol'; items: string[] }

function blocks(source: string): Block[] {
  const result: Block[] = []
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

/**
 * `afterStep` lets the caller append trusted, non-Markdown content (the build-time screenshot of
 * that step) under each item of a numbered list. Authored text can never produce an image itself.
 */
export function HelpMarkdown({ source, policy, idPrefix, afterStep }: { source: string; policy: HelpLinkPolicy; idPrefix: string; afterStep?: (step: number) => ReactNode }) {
  return <>{blocks(source).map((block, index) => {
    const key = `${idPrefix}-${index}`
    // A heading without Latin letters (ar, hi, ru, zh) has no slug; its id stays unique by position.
    if (block.kind === 'h') { const id = helpAnchorId(block.text); return <h4 key={key} id={id === 'help-section' ? `help-${key}` : id}>{inline(block.text, policy, key)}</h4> }
    if (block.kind === 'p') return <p key={key}>{inline(block.text, policy, key)}</p>
    const items = block.items.map((item, itemIndex) => <li key={`${key}-${itemIndex}`}>{inline(item, policy, `${key}-${itemIndex}`)}
      {block.kind === 'ol' && afterStep?.(itemIndex + 1)}</li>)
    return block.kind === 'ul' ? <ul key={key}>{items}</ul> : <ol key={key}>{items}</ol>
  })}</>
}
