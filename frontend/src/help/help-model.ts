// USER-GUIDE-01 UG-1: in-app page guide content model (docs/tasks/USER-GUIDE-01.md section 4).
// Content is bundled Markdown authored in this repository. Parsing is pure and side-effect free:
// opening the help panel never issues an API request, command or AI call (D2, 65.03 p59).

export type HelpLocale = 'tr' | 'en'
export type HelpReview = false | { by: string; date: string }
export type HelpFrontMatter = {
  key: string
  title: string
  roles: string[]
  permissions: string[]
  related: string[]
  screenshots: string[]
  reviewed: HelpReview
}
export type HelpSection = { heading: string; body: string }
export type HelpDocument = { meta: HelpFrontMatter; sections: HelpSection[] }

/** The 8 fixed section headings, in order. Each guide must contain all of them exactly once. */
export const helpSectionHeadings: Readonly<Record<HelpLocale, readonly string[]>> = Object.freeze({
  tr: Object.freeze(['Bu sayfa ne işe yarar', 'Kim kullanır', 'Başlamadan önce', 'Adım adım', 'Durumlar ne anlama gelir', 'Yapamazsınız / dikkat', 'Sorun olursa', 'İlgili sayfalar']),
  en: Object.freeze(['What this page is for', 'Who uses it', 'Before you start', 'Step by step', 'What the states mean', 'What you cannot do / caution', 'If something goes wrong', 'Related pages']),
})
/** Index of the "Related pages" section; its body is computed from front matter and the caller's own navigation. */
export const relatedSectionIndex = 7

/**
 * Pages whose screen has its own record-level "Why" disclosure. The help panel only points to it;
 * reading a Why payload is an owning-screen request and is never started by the panel.
 */
export const pagesWithWhyView: ReadonlySet<string> = new Set(['today', 'tug-job-board'])

const frontMatterKeys = ['key', 'title', 'roles', 'permissions', 'related', 'screenshots', 'reviewed'] as const
const pageKey = /^[a-z0-9]+(?:-[a-z0-9]+)*$/

export class HelpContentError extends Error {}

function fail(reason: string): never { throw new HelpContentError(reason) }

function parseList(raw: string, field: string): string[] {
  if (!raw.startsWith('[') || !raw.endsWith(']')) fail(`help_front_matter_list_required:${field}`)
  const inner = raw.slice(1, -1).trim()
  if (!inner) return []
  const items = inner.split(',').map(item => item.trim())
  if (items.some(item => !item)) fail(`help_front_matter_list_invalid:${field}`)
  return items
}

function parseReview(raw: string): HelpReview {
  if (raw === 'false') return false
  // A human SME/UX review (UG-REV-01) is recorded as `{ by: <name>, date: YYYY-MM-DD }`.
  // Only a human reviewer may write it; an AI author never sets this field.
  const match = /^\{\s*by:\s*([^,{}]+?)\s*,\s*date:\s*(\d{4}-\d{2}-\d{2})\s*\}$/.exec(raw)
  if (!match) fail('help_front_matter_reviewed_invalid')
  return { by: match[1], date: match[2] }
}

/** Parses one guide file. Throws HelpContentError on any structural deviation (closed shape). */
export function parseHelpDocument(source: string, locale: HelpLocale): HelpDocument {
  const text = source.replace(/\r\n?/g, '\n')
  if (!text.startsWith('---\n')) fail('help_front_matter_missing')
  const end = text.indexOf('\n---\n', 4)
  if (end < 0) fail('help_front_matter_unterminated')
  const values = new Map<string, string>()
  for (const line of text.slice(4, end).split('\n')) {
    if (!line.trim()) continue
    const match = /^([a-z]+):\s*(.*)$/.exec(line)
    if (!match || values.has(match[1])) fail('help_front_matter_line_invalid')
    values.set(match[1], match[2].trim())
  }
  if (values.size !== frontMatterKeys.length || frontMatterKeys.some(field => !values.has(field))) fail('help_front_matter_fields_invalid')
  const key = values.get('key')!, title = values.get('title')!
  if (!pageKey.test(key)) fail('help_front_matter_key_invalid')
  if (!title) fail('help_front_matter_title_missing')
  const related = parseList(values.get('related')!, 'related')
  if (related.some(item => !pageKey.test(item) || item === key) || new Set(related).size !== related.length) fail('help_front_matter_related_invalid')
  const meta: HelpFrontMatter = {
    key, title, roles: parseList(values.get('roles')!, 'roles'), permissions: parseList(values.get('permissions')!, 'permissions'),
    related, screenshots: parseList(values.get('screenshots')!, 'screenshots'), reviewed: parseReview(values.get('reviewed')!),
  }
  const sections: HelpSection[] = []
  let current: HelpSection | null = null
  const lead: string[] = []
  for (const line of text.slice(end + 5).split('\n')) {
    const heading = /^## (.+)$/.exec(line)
    if (heading) {
      current = { heading: heading[1].trim(), body: '' }
      sections.push(current)
    } else if (current) current.body += line + '\n'
    else lead.push(line)
  }
  if (lead.some(line => line.trim())) fail('help_body_outside_section')
  const expected = helpSectionHeadings[locale]
  if (sections.length !== expected.length || sections.some((section, index) => section.heading !== expected[index])) fail('help_sections_invalid')
  for (const section of sections) section.body = section.body.trim()
  return { meta, sections }
}
