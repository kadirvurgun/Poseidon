import type { ReactNode } from 'react'
import { helpAnchorId, parseHelpBlocks, tokenizeHelpInline } from './help-markdown-blocks'

// USER-GUIDE-01 UG-1: strict Markdown subset rendered to React elements.
// There is no HTML path at all: no dangerouslySetInnerHTML, no raw HTML pass-through, no
// script/style/iframe/image element. Every character of authored text becomes a React text node
// (escaped by React); step screenshots are appended only by the caller (afterStep), never by content.
// Parsing is shared with the offline manual generator (help-markdown-blocks.ts, UG-4). A link is
// rendered only when its href is an internal route the current user's navigation already contains,
// or a relative "#anchor" inside the panel; any other target (external URL, javascript:, data:,
// hidden route) renders as plain text.

export { helpAnchorId }

export type HelpLinkPolicy = {
  /** Route paths (without query) the current user's navigation contains. */
  allowedRoutes: ReadonlySet<string>
  onNavigate: (href: string) => void
  onAnchor: (id: string) => void
}

const appRoute = /^\/app(?:\/[a-z0-9-]+)+$/
const anchor = /^#[a-z0-9-]+$/

function inline(text: string, policy: HelpLinkPolicy, prefix: string): ReactNode[] {
  return tokenizeHelpInline(text).map((token, index) => {
    const key = `${prefix}-${index}`
    if (token.kind === 'text') return token.text
    if (token.kind === 'strong') return <strong key={key}>{token.text}</strong>
    if (token.kind === 'code') return <code key={key}>{token.text}</code>
    const { text: label, href } = token
    if (appRoute.test(href) && policy.allowedRoutes.has(href))
      return <a key={key} href={href} onClick={event => { event.preventDefault(); policy.onNavigate(href) }}>{label}</a>
    if (anchor.test(href))
      return <a key={key} href={href} onClick={event => { event.preventDefault(); policy.onAnchor(`help-${href.slice(1)}`) }}>{label}</a>
    return label
  })
}

/**
 * `afterStep` lets the caller append trusted, non-Markdown content (the build-time screenshot of
 * that step) under each item of a numbered list. Authored text can never produce an image itself.
 */
export function HelpMarkdown({ source, policy, idPrefix, afterStep }: { source: string; policy: HelpLinkPolicy; idPrefix: string; afterStep?: (step: number) => ReactNode }) {
  return <>{parseHelpBlocks(source).map((block, index) => {
    const key = `${idPrefix}-${index}`
    if (block.kind === 'h') return <h4 key={key} id={helpAnchorId(block.text)}>{inline(block.text, policy, key)}</h4>
    if (block.kind === 'p') return <p key={key}>{inline(block.text, policy, key)}</p>
    const items = block.items.map((item, itemIndex) => <li key={`${key}-${itemIndex}`}>{inline(item, policy, `${key}-${itemIndex}`)}
      {block.kind === 'ol' && afterStep?.(itemIndex + 1)}</li>)
    return block.kind === 'ul' ? <ul key={key}>{items}</ul> : <ol key={key}>{items}</ol>
  })}</>
}
