// USER-GUIDE-01 UG-4 (UG-DOC-01): offline, illustrated, step-by-step user manual.
// Builds one self-contained HTML document per locale from the same single-source page guides the
// in-app help panel shows (D1) plus the committed UG-2 screenshots. Parsing reuses the panel's own
// modules (help-model.ts, help-markdown-blocks.ts), so the manual and the panel can never read a
// guide differently. Pure apart from loadManualInputs(); rendering is deterministic (no clock, no
// randomness, sorted inputs) and every authored character is HTML-escaped: the safe Markdown subset
// has no HTML path, links only resolve to anchors inside this manual, images come only from the
// generator (never from guide text).

import { createHash } from 'node:crypto'
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { helpSectionHeadings, parseHelpDocument, relatedSectionIndex, stepSectionIndex, type HelpDocument, type HelpLocale } from '../../../frontend/src/help/help-model.ts'
import { helpAnchorId, parseHelpBlocks, tokenizeHelpInline } from '../../../frontend/src/help/help-markdown-blocks.ts'
import { enterpriseSections } from '../../../frontend/src/product/enterprise-sections.ts'

export const manualLocales: readonly HelpLocale[] = Object.freeze(['tr', 'en'])
export const repoRoot = resolve(import.meta.dirname, '../../..')

/** Manual-only chapters, in UG-DOC-01 order around the role chapters and page guides. */
export const manualChapterKeys = Object.freeze(['getting-started', 'common-model', 'roles', 'glossary', 'troubleshooting'] as const)
export type ManualChapterKey = (typeof manualChapterKeys)[number]
export type ManualChapter = { key: ManualChapterKey; title: string; reviewed: boolean; sections: { heading: string; body: string }[] }

/**
 * 13.03 §2-9 role chapters, in handbook order. `matches` lists every `roles` front-matter value that
 * belongs to the chapter; an unmapped role is a content defect (the generator fails).
 * Product Owner and Support / Engineering are not 13.03 roles; their pages are listed under the
 * System / Tenant Admin chapter, which roles.md states explicitly.
 */
export const roleChapters = Object.freeze([
  { id: 'master', matches: ['Master'] },
  { id: 'tug-master', matches: ['Tug Master'] },
  { id: 'dpa', matches: ['DPA / HSEQ', 'DPA'] },
  { id: 'chief-engineer', matches: ['Chief Engineer', 'Chief Engineer / Technical Superintendent', 'Technical Superintendent'] },
  { id: 'crew', matches: ['Crew / OOW', 'Crew', 'Crew / OOW / Engineer'] },
  { id: 'shore-ops', matches: ['Tug Operations Coordinator', 'Shore Operations'] },
  { id: 'regulatory', matches: ['Regulatory / Assurance'] },
  { id: 'admin', matches: ['Tenant Admin', 'Product Owner', 'Support / Engineering'] },
] as const)

export type ManualScreenshot = { step: number; path: string; bytes: Uint8Array }
export type StepMapping = { guides: Record<string, { steps: { step: number; unavailable?: string }[] }> }
export type ManualInputs = {
  locale: HelpLocale
  guides: HelpDocument[]
  chapters: ManualChapter[]
  /** key -> screenshots of that guide's "Step by step", already resolved to existing files. */
  screenshots: Record<string, ManualScreenshot[]>
  mapping: StepMapping
  /** Sidebar section titles for this locale, by section key. */
  sectionTitles: Record<string, string>
  /** Digest of every input byte; printed so two copies of the manual can be compared. */
  digest: string
}

export class ManualError extends Error {}
const fail = (reason: string): never => { throw new ManualError(reason) }

const copy = {
  tr: {
    title: 'POSEIDON Kullanım Kılavuzu', subtitle: 'Gemi ve römorkör operasyonu için resimli, adım adım kullanım kılavuzu',
    draftBanner: 'TASLAK — alan uzmanı ve UX yazarı tarafından gözden geçirilmedi (UG-REV-01). Bu kılavuz emniyet kararı vermez; karar yetkili kişiye aittir.',
    draftPage: 'Taslak kılavuz — uzman incelemesi bekliyor', contents: 'İçindekiler', roleChapters: 'Rol bölümleri', pageGuides: 'Sayfa kılavuzları',
    pageGuidesIntro: 'Sayfalar, uygulamadaki menü gruplarıyla aynı sırada verilmiştir. Her sayfada önce ne işe yaradığı, sonra numaralı adımlar ve resimleri gelir.',
    rolePages: 'Bu rolün kullandığı sayfalar', roles: 'Rol bölümleri', guidesCount: 'sayfa kılavuzu', shotsCount: 'ekran görüntüsü', screenshotAlt: 'Ekran görüntüsü, adım',
    noScreenshot: 'Bu adımın resmi yok: adım yazma yetkisi gerektirir ve sentetik görüntü hattı yazma işlemi sahnelemez. Adımı metinden izleyin.',
    relatedNone: 'İlgili sayfa yok.', language: 'Dil: Türkçe', digest: 'İçerik özeti', synthetic: 'Tüm resimler sentetik test verisiyle otomatik üretilmiştir.',
    backToContents: 'İçindekilere dön', locale: 'tr-TR',
  },
  en: {
    title: 'POSEIDON User Manual', subtitle: 'Illustrated, step-by-step manual for ship and tug operations',
    draftBanner: 'DRAFT — not reviewed by a subject-matter expert or UX writer (UG-REV-01). This manual makes no safety decision; the decision belongs to the authorized person.',
    draftPage: 'Draft guide — awaiting expert review', contents: 'Contents', roleChapters: 'Role chapters', pageGuides: 'Page guides',
    pageGuidesIntro: 'Pages follow the same groups and order as the application menu. Each page starts with what it is for, followed by numbered steps and their pictures.',
    rolePages: 'Pages this role uses', roles: 'Role chapters', guidesCount: 'page guides', shotsCount: 'screenshots', screenshotAlt: 'Screenshot, step',
    noScreenshot: 'No picture for this step: it needs write permission and the synthetic capture pipeline does not stage writes. Follow the step from the text.',
    relatedNone: 'No related pages.', language: 'Language: English', digest: 'Content digest', synthetic: 'All pictures were generated automatically from synthetic test data.',
    backToContents: 'Back to contents', locale: 'en',
  },
} as const

export function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, ch => ch === '&' ? '&amp;' : ch === '<' ? '&lt;' : ch === '>' ? '&gt;' : ch === '"' ? '&quot;' : '&#39;')
}

const chapterKey = /^[a-z]+(?:-[a-z]+)*$/
/** Parses a `_manual/*.md` chapter: closed front matter (key, title, reviewed) and `## ` sections only. */
export function parseManualChapter(source: string): ManualChapter {
  const text = source.replace(/\r\n?/g, '\n')
  if (!text.startsWith('---\n')) fail('manual_front_matter_missing')
  const end = text.indexOf('\n---\n', 4)
  if (end < 0) fail('manual_front_matter_unterminated')
  const values = new Map<string, string>()
  for (const line of text.slice(4, end).split('\n')) {
    if (!line.trim()) continue
    const match = /^([a-z]+):\s*(.*)$/.exec(line)
    if (!match || values.has(match[1])) fail('manual_front_matter_line_invalid')
    values.set(match[1], match[2].trim())
  }
  if (values.size !== 3 || !values.has('key') || !values.has('title') || !values.has('reviewed')) fail('manual_front_matter_fields_invalid')
  const key = values.get('key')!
  if (!chapterKey.test(key) || !(manualChapterKeys as readonly string[]).includes(key)) fail('manual_chapter_key_invalid')
  // Same rule as the page guides (D7): only a human review flow may ever change this; AI content is false.
  if (values.get('reviewed') !== 'false') fail('manual_reviewed_must_be_false')
  if (!values.get('title')) fail('manual_title_missing')
  const sections: ManualChapter['sections'] = []
  for (const line of text.slice(end + 5).split('\n')) {
    const heading = /^## (.+)$/.exec(line)
    if (heading) sections.push({ heading: heading[1].trim(), body: '' })
    else if (sections.length) sections[sections.length - 1].body += line + '\n'
    else if (line.trim()) fail('manual_body_outside_section')
  }
  if (!sections.length) fail('manual_sections_missing')
  for (const section of sections) section.body = section.body.trim()
  return { key: key as ManualChapterKey, title: values.get('title')!, reviewed: false, sections }
}

const read = (path: string) => readFileSync(path)
const text = (path: string) => readFileSync(path, 'utf8')

/** Reads every input of one locale from the repository. Throws on a missing or malformed input. */
export function loadManualInputs(locale: HelpLocale, root: string = repoRoot): ManualInputs {
  const hash = createHash('sha256')
  const feed = (label: string, bytes: Uint8Array | string) => {
    const data = typeof bytes === 'string' ? bytes.replace(/\r\n/g, '\n') : bytes
    hash.update(label).update('\0').update(data).update('\0')
  }
  const contentDir = join(root, 'frontend/src/help/content', locale)
  const guides = readdirSync(contentDir).filter(name => name.endsWith('.md')).sort().map(name => {
    const source = text(join(contentDir, name)); feed(`guide/${locale}/${name}`, source)
    const document = parseHelpDocument(source, locale)
    if (`${document.meta.key}.md` !== name) fail(`manual_guide_key_mismatch:${name}`)
    return document
  })
  const chapters = manualChapterKeys.map(key => {
    const path = join(contentDir, '_manual', `${key}.md`)
    if (!existsSync(path)) fail(`manual_chapter_missing:${locale}/${key}`)
    const source = text(path); feed(`chapter/${locale}/${key}`, source)
    const chapter = parseManualChapter(source)
    if (chapter.key !== key) fail(`manual_chapter_key_mismatch:${key}`)
    return chapter
  })
  const mappingSource = text(join(root, 'frontend/src/help/screenshots/steps.json')); feed('steps.json', mappingSource)
  const mapping = JSON.parse(mappingSource) as StepMapping
  const screenshots: Record<string, ManualScreenshot[]> = {}
  for (const guide of guides) {
    screenshots[guide.meta.key] = guide.meta.screenshots.map(step => {
      const path = `frontend/src/help/screenshots/${locale}/${guide.meta.key}/${step}-desktop.png`
      const absolute = join(root, path)
      if (!existsSync(absolute)) fail(`manual_screenshot_missing:${path}`)
      const bytes = read(absolute); feed(path, bytes)
      return { step, path, bytes }
    })
  }
  const messagesSource = text(join(root, 'frontend/src/i18n/messages', `${locale}.json`))
  const pairs = (JSON.parse(messagesSource) as { pairs: Record<string, string> }).pairs
  const sectionTitles: Record<string, string> = {}
  for (const section of enterpriseSections) {
    const title = pairs[section.label.replace(/^pairs\./, '')]
    if (typeof title !== 'string' || !title) fail(`manual_section_title_missing:${section.key}`)
    sectionTitles[section.key] = title; feed(`section/${section.key}`, title)
  }
  feed('generator', text(join(root, 'tests/Poseidon.Browser.Tests/manual/manual.ts')))
  return { locale, guides, chapters, screenshots, mapping, sectionTitles, digest: hash.digest('hex') }
}

/** Role chapter ids a guide belongs to, from its `roles` front matter (13.03 §2-9). */
export function roleChapterIds(document: HelpDocument): string[] {
  const ids = new Set<string>()
  for (const role of document.meta.roles) {
    const chapter = roleChapters.find(item => (item.matches as readonly string[]).includes(role))
    if (!chapter) fail(`manual_role_unmapped:${document.meta.key}:${role}`)
    ids.add(chapter!.id)
  }
  return roleChapters.map(item => item.id).filter(id => ids.has(id))
}

/** Guides grouped by sidebar section, in sidebar order. Every guide must be placed exactly once. */
export function groupGuides(guides: readonly HelpDocument[]): { section: string; guides: HelpDocument[] }[] {
  const byKey = new Map(guides.map(guide => [guide.meta.key, guide]))
  const placed = new Set<string>()
  const groups = enterpriseSections.map(section => ({ section: section.key, guides: section.keys.flatMap(key => {
    const guide = byKey.get(key)
    if (!guide) return []
    if (placed.has(key)) fail(`manual_guide_placed_twice:${key}`)
    placed.add(key)
    return [guide]
  }) })).filter(group => group.guides.length)
  for (const key of byKey.keys()) if (!placed.has(key)) fail(`manual_guide_unplaced:${key}`)
  return groups
}

const pageId = (key: string) => `page-${key}`
const appRoute = /^\/app\/([a-z0-9-]+)$/
const localAnchor = /^#([a-z0-9-]+)$/

type Context = { guideKeys: ReadonlySet<string>; anchorPrefix: string }

function inline(source: string, context: Context): string {
  return tokenizeHelpInline(source).map(token => {
    if (token.kind === 'text') return escapeHtml(token.text)
    if (token.kind === 'strong') return `<strong>${escapeHtml(token.text)}</strong>`
    if (token.kind === 'code') return `<code>${escapeHtml(token.text)}</code>`
    // Links never leave the document: an app route becomes the anchor of that page's guide, a
    // "#anchor" stays inside the current chapter; anything else (external, javascript:, data:,
    // unknown route) is plain text.
    const route = appRoute.exec(token.href)
    if (route && context.guideKeys.has(route[1])) return `<a href="#${pageId(route[1])}">${escapeHtml(token.text)}</a>`
    const anchor = localAnchor.exec(token.href)
    if (anchor) return `<a href="#${context.anchorPrefix}-${anchor[1]}">${escapeHtml(token.text)}</a>`
    return escapeHtml(token.text)
  }).join('')
}

function blocks(source: string, context: Context, afterStep?: (step: number) => string): string {
  return parseHelpBlocks(source).map(block => {
    if (block.kind === 'h') return `<h5 id="${context.anchorPrefix}-${helpAnchorId(block.text).slice('help-'.length)}">${inline(block.text, context)}</h5>`
    if (block.kind === 'p') return `<p>${inline(block.text, context)}</p>`
    const items = block.items.map((item, index) => `<li>${inline(item, context)}${block.kind === 'ol' && afterStep ? afterStep(index + 1) : ''}</li>`).join('\n')
    return block.kind === 'ul' ? `<ul>\n${items}\n</ul>` : `<ol class="steps">\n${items}\n</ol>`
  }).join('\n')
}

const base64 = (bytes: Uint8Array) => Buffer.from(bytes).toString('base64')

function renderGuide(guide: HelpDocument, inputs: ManualInputs, context: Context, titles: ReadonlyMap<string, string>, roleTitles: ReadonlyMap<string, string>): string {
  const t = copy[inputs.locale]
  const key = guide.meta.key
  const shots = new Map((inputs.screenshots[key] ?? []).map(shot => [shot.step, shot]))
  const mapped = new Map((inputs.mapping.guides[key]?.steps ?? []).map(step => [step.step, step]))
  const guideContext = { ...context, anchorPrefix: pageId(key) }
  const afterStep = (step: number) => {
    const shot = shots.get(step)
    if (shot) return `\n<figure class="shot"><img src="data:image/png;base64,${base64(shot.bytes)}" alt="${escapeHtml(`${t.screenshotAlt} ${step}`)}" width="1440" height="900"><figcaption>${escapeHtml(`${guide.meta.title} · ${step}`)}</figcaption></figure>`
    if (mapped.get(step)?.unavailable) return `\n<p class="no-shot" role="note">${escapeHtml(t.noScreenshot)}</p>`
    return ''
  }
  const sections = guide.sections.map((section, index) => {
    const body = index === relatedSectionIndex
      ? (guide.meta.related.length ? `<ul class="related">\n${guide.meta.related.map(item => `<li><a href="#${pageId(item)}">${escapeHtml(titles.get(item) ?? item)}</a></li>`).join('\n')}\n</ul>` : `<p>${escapeHtml(t.relatedNone)}</p>`)
      : blocks(section.body, guideContext, index === stepSectionIndex ? afterStep : undefined)
    return `<section class="guide-section">\n<h4>${escapeHtml(section.heading)}</h4>\n${body}\n</section>`
  }).join('\n')
  return `<article class="guide" id="${pageId(key)}">
<h3>${escapeHtml(guide.meta.title)}</h3>
${guide.meta.reviewed === false ? `<p class="draft-badge" role="note">${escapeHtml(t.draftPage)}</p>\n` : ''}<p class="meta"><strong>${escapeHtml(t.roles)}:</strong> ${roleChapterIds(guide).map(id => `<a href="#role-${id}">${escapeHtml(roleTitles.get(id)!)}</a>`).join(', ')}</p>
${sections}
<p class="back"><a href="#contents">${escapeHtml(t.backToContents)}</a></p>
</article>`
}

function renderChapter(chapter: ManualChapter, context: Context): string {
  const id = `chapter-${chapter.key}`
  const chapterContext = { ...context, anchorPrefix: id }
  return `<section class="chapter" id="${id}">
<h2>${escapeHtml(chapter.title)}</h2>
${chapter.sections.map(section => `<section class="chapter-section" id="${id}-${helpAnchorId(section.heading).slice('help-'.length)}">\n<h3>${escapeHtml(section.heading)}</h3>\n${blocks(section.body, chapterContext)}\n</section>`).join('\n')}
</section>`
}

const style = `
:root{--ink:#14213d;--muted:#4b5563;--line:#d1d5db;--accent:#047857;--warn-bg:#fff7ed;--warn-ink:#7c2d12;--paper:#ffffff}
*{box-sizing:border-box}
[id]{scroll-margin-top:56px}
body{margin:0;background:var(--paper);color:var(--ink);font:16px/1.55 "Segoe UI",system-ui,-apple-system,Roboto,Arial,sans-serif}
main{max-width:980px;margin:0 auto;padding:0 16px 48px}
.draft-banner{position:sticky;top:0;z-index:2;margin:0;padding:8px 16px;background:var(--warn-bg);color:var(--warn-ink);border-bottom:2px solid #ea580c;font-weight:600;text-align:center}
.cover{min-height:60vh;display:flex;flex-direction:column;justify-content:center;border-bottom:4px solid var(--accent);margin-bottom:32px}
.cover h1{font-size:2.6rem;margin:0 0 8px}.cover .subtitle{font-size:1.25rem;color:var(--muted);margin:0 0 24px}
.cover .facts{color:var(--muted);font-size:.9rem}
h2{font-size:1.9rem;border-bottom:3px solid var(--accent);padding-bottom:6px;margin-top:48px}
h3{font-size:1.4rem;margin-top:32px}h4{font-size:1.1rem;margin:24px 0 8px;color:var(--accent)}h5{font-size:1rem;margin:16px 0 6px}
code{background:#f3f4f6;border:1px solid var(--line);border-radius:4px;padding:0 4px;font-size:.9em}
a{color:#065f46}
nav.toc ol{padding-left:22px}nav.toc li{margin:2px 0}nav.toc ol ol{font-size:.95rem}
.guide{border-top:1px solid var(--line);padding-top:8px;margin-top:40px}
.draft-badge{display:inline-block;background:var(--warn-bg);color:var(--warn-ink);border:1px solid #fdba74;border-radius:999px;padding:2px 12px;font-size:.85rem;font-weight:600}
.meta{color:var(--muted);font-size:.95rem}
ol.steps>li{margin-bottom:14px}
figure.shot{margin:10px 0 18px;border:1px solid var(--line);border-radius:8px;overflow:hidden;background:#f9fafb}
figure.shot img{display:block;width:100%;height:auto}
figure.shot figcaption{font-size:.8rem;color:var(--muted);padding:4px 10px;border-top:1px solid var(--line)}
.no-shot{font-size:.9rem;color:var(--muted);border-left:3px solid var(--line);padding-left:10px}
.back{font-size:.85rem}
footer{margin-top:48px;border-top:1px solid var(--line);padding-top:12px;color:var(--muted);font-size:.8rem}
@media print{
  @page{size:A4;margin:16mm 14mm 18mm}
  body{font-size:11pt}
  main{max-width:none;padding:0}
  .draft-banner{position:fixed;top:0;left:0;right:0;font-size:8pt;padding:2px 8px}
  .cover{min-height:auto;height:240mm;margin-top:10mm}
  .chapter,.part{break-before:page}
  .guide{break-before:page;border-top:0}
  figure.shot,.no-shot{break-inside:avoid}
  h2,h3,h4,h5{break-after:avoid}
  .back{display:none}
  a{color:inherit;text-decoration:none}
}`

/** Renders the complete manual. Deterministic: the same inputs give byte-identical output. */
export function renderManual(inputs: ManualInputs): string {
  const t = copy[inputs.locale]
  const guideKeys = new Set(inputs.guides.map(guide => guide.meta.key))
  const titles = new Map(inputs.guides.map(guide => [guide.meta.key, guide.meta.title]))
  const context: Context = { guideKeys, anchorPrefix: 'manual' }
  const chapters = new Map(inputs.chapters.map(chapter => [chapter.key, chapter]))
  const chapter = (key: ManualChapterKey) => chapters.get(key) ?? fail(`manual_chapter_missing:${key}`)
  const groups = groupGuides(inputs.guides)

  // Role chapters: 13.03 role intro (roles.md, one section per role in handbook order) + the pages
  // whose `roles` front matter names that role, in sidebar order.
  const rolesChapter = chapter('roles')
  if (rolesChapter.sections.length !== roleChapters.length) fail('manual_roles_sections_invalid')
  const roleTitles = new Map(roleChapters.map((role, index) => [role.id as string, rolesChapter.sections[index].heading]))
  const ordered = groups.flatMap(group => group.guides)
  const roleIds = new Map(ordered.map(guide => [guide.meta.key, roleChapterIds(guide)]))
  const roleHtml = roleChapters.map((role, index) => {
    const section = rolesChapter.sections[index]
    const pages = ordered.filter(guide => roleIds.get(guide.meta.key)!.includes(role.id))
    return `<section class="role" id="role-${role.id}">
<h3>${escapeHtml(section.heading)}</h3>
${blocks(section.body, { ...context, anchorPrefix: `role-${role.id}` })}
<h4>${escapeHtml(t.rolePages)}</h4>
${pages.length ? `<ul class="role-pages">\n${pages.map(guide => `<li><a href="#${pageId(guide.meta.key)}">${escapeHtml(guide.meta.title)}</a></li>`).join('\n')}\n</ul>` : `<p>${escapeHtml(t.relatedNone)}</p>`}
</section>`
  }).join('\n')

  const pagesHtml = groups.map(group => `<section class="part-group" id="group-${group.section}">
<h2 class="group-title">${escapeHtml(t.pageGuides)}: ${escapeHtml(inputs.sectionTitles[group.section] ?? fail(`manual_section_title_missing:${group.section}`))}</h2>
${group.guides.map(guide => renderGuide(guide, inputs, context, titles, roleTitles)).join('\n')}
</section>`).join('\n')

  const tocChapter = (key: ManualChapterKey) => `<li><a href="#chapter-${key}">${escapeHtml(chapter(key).title)}</a></li>`
  const toc = `<nav class="toc" id="contents" aria-labelledby="contents-title">
<h2 id="contents-title">${escapeHtml(t.contents)}</h2>
<ol>
${tocChapter('getting-started')}
${tocChapter('common-model')}
<li><a href="#chapter-roles">${escapeHtml(rolesChapter.title)}</a>
<ol>
${roleChapters.map((role, index) => `<li><a href="#role-${role.id}">${escapeHtml(rolesChapter.sections[index].heading)}</a></li>`).join('\n')}
</ol></li>
<li><a href="#page-guides">${escapeHtml(t.pageGuides)}</a>
<ol>
${groups.map(group => `<li><a href="#group-${group.section}">${escapeHtml(inputs.sectionTitles[group.section])}</a>
<ol>
${group.guides.map(guide => `<li><a href="#${pageId(guide.meta.key)}">${escapeHtml(guide.meta.title)}</a></li>`).join('\n')}
</ol></li>`).join('\n')}
</ol></li>
${tocChapter('glossary')}
${tocChapter('troubleshooting')}
</ol>
</nav>`

  const allReviewed = inputs.guides.every(guide => guide.meta.reviewed !== false) && inputs.chapters.every(item => item.reviewed)
  const guideCount = inputs.guides.length
  const shotCount = Object.values(inputs.screenshots).reduce((sum, shots) => sum + shots.length, 0)
  return `<!doctype html>
<html lang="${inputs.locale}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="generator" content="tests/Poseidon.Browser.Tests/manual/build-manual.ts">
<title>${escapeHtml(t.title)}</title>
<style>${style}</style>
</head>
<body>
${allReviewed ? '' : `<p class="draft-banner" role="note">${escapeHtml(t.draftBanner)}</p>\n`}<main>
<header class="cover">
<h1>${escapeHtml(t.title)}</h1>
<p class="subtitle">${escapeHtml(t.subtitle)}</p>
<p class="facts">${escapeHtml(t.language)} · ${guideCount} ${escapeHtml(t.guidesCount)} · ${shotCount} ${escapeHtml(t.shotsCount)}</p>
<p class="facts">${escapeHtml(t.synthetic)}</p>
<p class="facts">${escapeHtml(t.digest)}: <code>${inputs.digest.slice(0, 16)}</code></p>
</header>
${toc}
${renderChapter(chapter('getting-started'), context)}
${renderChapter(chapter('common-model'), context)}
<section class="chapter" id="chapter-roles">
<h2>${escapeHtml(rolesChapter.title)}</h2>
${roleHtml}
</section>
<section class="part" id="page-guides">
<h2>${escapeHtml(t.pageGuides)}</h2>
<p>${escapeHtml(t.pageGuidesIntro)}</p>
</section>
${pagesHtml}
${renderChapter(chapter('glossary'), context)}
${renderChapter(chapter('troubleshooting'), context)}
<footer>${escapeHtml(t.title)} · ${escapeHtml(t.digest)} ${inputs.digest}</footer>
</main>
</body>
</html>
`
}

/** Section headings every page guide must render, re-exported for the generator test. */
export { helpSectionHeadings }
