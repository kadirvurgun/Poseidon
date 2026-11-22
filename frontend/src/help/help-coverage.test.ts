import { describe, expect, it } from 'vitest'
import { enterpriseDestinationKeys } from '../product/enterprise-navigation'
import { helpSectionHeadings, parseHelpDocument, type HelpLocale } from './help-model'
import { pendingHelpBaseline, pendingHelpKeys } from './help-pending'

// USER-GUIDE-01 UG-HELP-03: every navigation destination has a TR and EN guide with the 8 fixed
// sections, except keys in the explicit, shrink-only pending allowlist.
const raw = import.meta.glob<string>('./content/*/*.md', { eager: true, query: '?raw', import: 'default' })
const locales: HelpLocale[] = ['tr', 'en']
const source = (locale: HelpLocale, key: string) => raw[`./content/${locale}/${key}.md`]
const navigation = new Set(enterpriseDestinationKeys)

describe('help content coverage guard', () => {
  it('reads the navigation contract it guards', () => {
    expect(enterpriseDestinationKeys.length).toBeGreaterThanOrEqual(53)
    expect(navigation.size).toBe(enterpriseDestinationKeys.length)
  })

  it.each([...enterpriseDestinationKeys].sort())('%s has a complete TR + EN guide or is explicitly pending', key => {
    if (pendingHelpKeys.has(key)) {
      // Shrink-only: once any guide for an allowlisted key exists, the key must leave the allowlist.
      for (const locale of locales) expect(source(locale, key), `${key} has ${locale} content: remove it from help-pending.ts`).toBeUndefined()
      return
    }
    for (const locale of locales) {
      const text = source(locale, key)
      expect(text, `missing ${locale} guide for ${key}`).toBeTypeOf('string')
      const document = parseHelpDocument(text!, locale)
      expect(document.meta.key).toBe(key)
      expect(document.sections.map(section => section.heading)).toEqual(helpSectionHeadings[locale])
      for (const [index, section] of document.sections.entries())
        if (index !== 7) expect(section.body, `${key}/${locale}: empty "${section.heading}"`).not.toBe('')
      for (const related of document.meta.related) expect(navigation.has(related), `${key}/${locale}: unknown related ${related}`).toBe(true)
    }
    const [tr, en] = locales.map(locale => parseHelpDocument(source(locale, key)!, locale).meta)
    expect(tr.related).toEqual(en.related)
    expect(tr.permissions).toEqual(en.permissions)
    expect(tr.reviewed).toEqual(en.reviewed)
  })

  it('keeps the pending allowlist shrink-only and limited to real destinations', () => {
    for (const key of pendingHelpKeys) expect(navigation.has(key), `stale pending key ${key}`).toBe(true)
    expect(pendingHelpKeys.size).toBeLessThanOrEqual(pendingHelpBaseline)
    expect(pendingHelpBaseline).toBeLessThanOrEqual(50)
    for (const key of ['today', 'tug-job-board', 'companies']) expect(pendingHelpKeys.has(key)).toBe(false)
  })

  it('has no guide file for a key outside navigation and only TR/EN folders in UG-1', () => {
    for (const path of Object.keys(raw)) {
      const match = /^\.\/content\/([^/]+)\/([^/]+)\.md$/.exec(path)
      expect(match, path).not.toBeNull()
      expect(locales).toContain(match![1])
      expect(navigation.has(match![2]), `${path} is not a navigation destination`).toBe(true)
    }
  })

  it('keeps every guide reviewed:false until the human UG-REV-01 sign-off flow replaces this guard (D7)', () => {
    // An AI author can never record SME/UX approval. When the human review flow lands, it replaces this
    // assertion explicitly; until then a `reviewed: { by, date }` value in a guide is a defect.
    for (const [path, text] of Object.entries(raw)) {
      const locale = /^\.\/content\/(tr|en)\//.exec(path)?.[1] as HelpLocale
      expect(parseHelpDocument(text, locale).meta.reviewed, path).toBe(false)
      expect(text, path).toMatch(/^reviewed: false$/m)
    }
  })

  it('keeps authored content inside the safe Markdown subset with no raw HTML or external link', () => {
    for (const [path, text] of Object.entries(raw)) {
      expect(text, path).not.toMatch(/<\/?[a-z!][^>]*>/i)
      expect(text, path).not.toMatch(/\]\((?!\/app\/|#)[^)]*\)/)
      expect(text, path).not.toMatch(/javascript:|data:|https?:\/\//i)
    }
  })

  it('never describes UNKNOWN as No / Ready / Not applicable and never lets the guide decide safety (D8, UG-HELP-05)', () => {
    for (const [path, text] of Object.entries(raw)) {
      const body = text.toLocaleLowerCase('tr')
      expect(body, path).not.toMatch(/bilinmiyor\s*(=|ise|demek|anlamına gelir)\s*(hayır|hazır)/)
      expect(body, path).not.toMatch(/unknown\s*(=|means|is treated as)\s*(no|ready|not applicable)\b/)
      expect(body, path).not.toMatch(/(gemi|römorkör)\s+kalkabilir|is safe to (sail|depart)|may depart/)
    }
  })
})

// USER-GUIDE-01 UG-2 (UG-DOC-02 / UG-DOC-03): every "Step by step" item of a guide with a capture
// mapping has a generated screenshot (or an explicit, reasoned "unavailable" entry), the front matter
// lists exactly those steps, and every committed PNG carries its provenance record.
type StepMapping = { committedViewports: string[]; guides: Record<string, { steps: { step: number; unavailable?: string }[] }> }
const mappingSource = import.meta.glob<StepMapping>('./screenshots/steps.json', { eager: true, import: 'default' })
const mapping = mappingSource['./screenshots/steps.json']
const pngs = new Set(Object.keys(import.meta.glob('./screenshots/*/*/*.png', { query: '?url', import: 'default' })))
const provenance = import.meta.glob<{ generator: string; key: string; locale: string; step: number; viewport: string; datasets: string[] }>(
  './screenshots/*/*/*.json', { eager: true, import: 'default' })
const stepItems = (text: string, locale: HelpLocale) => parseHelpDocument(text, locale).sections[3].body.split('\n').filter(line => /^\d+\. /.test(line.trim())).length

describe('help screenshot coverage (UG-2)', () => {
  it('reads a capture mapping that only names real guides', () => {
    expect(mapping).toBeDefined()
    expect([...mapping.committedViewports].sort()).toEqual(['desktop', 'mobile'])
    for (const key of Object.keys(mapping.guides)) for (const locale of locales) expect(source(locale, key), `${key}/${locale}`).toBeTypeOf('string')
  })

  it.each(locales)('maps every step of every captured %s guide and lists exactly the captured steps', locale => {
    for (const [key, guide] of Object.entries(mapping.guides)) {
      const text = source(locale, key)!
      expect(guide.steps.map(step => step.step), `${key}: mapping must follow the guide steps`).toEqual(Array.from({ length: stepItems(text, locale) }, (_, index) => index + 1))
      const captured = guide.steps.filter(step => !step.unavailable).map(step => step.step)
      expect(parseHelpDocument(text, locale).meta.screenshots, `${key}/${locale}: screenshots front matter`).toEqual(captured)
      // D4: every captured step has both committed viewports (1440x900 desktop, 390x844 mobile).
      for (const step of captured) for (const viewport of mapping.committedViewports) {
        const base = `./screenshots/${locale}/${key}/${step}-${viewport}`
        expect(pngs.has(base + '.png'), base + '.png').toBe(true)
        const record = provenance[base + '.json']
        expect(record, base + '.json').toBeDefined()
        expect(record).toMatchObject({ generator: 'tests/Poseidon.Browser.Tests/screenshots/capture.ts', key, locale, step, viewport })
      }
    }
  })

  it('has no orphan screenshot and no screenshot for a guide without a capture mapping', () => {
    for (const path of [...pngs, ...Object.keys(provenance)]) {
      const match = /^\.\/screenshots\/(tr|en)\/([a-z0-9-]+)\/(\d+)-(desktop|mobile)\.(png|json)$/.exec(path)
      expect(match, path).not.toBeNull()
      const [, locale, key, step, viewport] = match!
      expect(mapping.committedViewports, path).toContain(viewport)
      expect(parseHelpDocument(source(locale as HelpLocale, key)!, locale as HelpLocale).meta.screenshots, path).toContain(Number(step))
    }
    for (const [path, text] of Object.entries(raw)) {
      const [, locale, key] = /^\.\/content\/(tr|en)\/([^/]+)\.md$/.exec(path)!
      if (parseHelpDocument(text, locale as HelpLocale).meta.screenshots.length) expect(mapping.guides[key], path).toBeDefined()
    }
  })
})
