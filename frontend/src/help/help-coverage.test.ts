import { describe, expect, it } from 'vitest'
import { enterpriseDestinationKeys } from '../product/enterprise-navigation'
import { helpLocales, helpSectionHeadings, helpStepCount, isDraftTranslationLocale, parseHelpDocument, type HelpLocale } from './help-model'
import { pendingHelpBaseline, pendingHelpKeys } from './help-pending'

// USER-GUIDE-01 UG-HELP-03: every navigation destination has a TR and EN guide with the 8 fixed
// sections, except keys in the explicit, shrink-only pending allowlist.
const raw = import.meta.glob<string>('./content/*/*.md', { eager: true, query: '?raw', import: 'default' })
const locales: HelpLocale[] = ['tr', 'en']
const draftLocales = helpLocales.filter(isDraftTranslationLocale)
const localeOf = (path: string) => /^\.\/content\/([^/]+)\//.exec(path)![1] as HelpLocale
const bundleFiles = Object.keys(import.meta.glob('./content/*/help-*.ts'))
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

  it('has no guide file for a key outside navigation, only known locale folders, and one bundle per folder', () => {
    const folders = new Set<string>()
    for (const path of Object.keys(raw)) {
      const match = /^\.\/content\/([^/]+)\/([^/]+)\.md$/.exec(path)
      expect(match, path).not.toBeNull()
      expect(helpLocales, path).toContain(match![1])
      expect(navigation.has(match![2]), `${path} is not a navigation destination`).toBe(true)
      folders.add(match![1])
    }
    expect(bundleFiles.sort()).toEqual([...folders].sort().map(locale => `./content/${locale}/help-${locale}.ts`))
  })

  it('keeps every guide reviewed:false until the human UG-REV-01 sign-off flow replaces this guard (D7)', () => {
    // An AI author can never record SME/UX approval. When the human review flow lands, it replaces this
    // assertion explicitly; until then a `reviewed: { by, date }` value in a guide is a defect.
    for (const [path, text] of Object.entries(raw)) {
      expect(parseHelpDocument(text, localeOf(path)).meta.reviewed, path).toBe(false)
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

// USER-GUIDE-01 UG-5 (D6): a machine-drafted translation is optional per page (a missing file falls
// back to EN and is NOT listed in help-pending.ts), but any file that exists must be a faithful
// structural copy of its EN guide: same front matter except the title, reviewed:false, the locale's
// 8 headings in order, the same number of steps and non-empty sections.
const draftGuides = Object.keys(raw).filter(path => isDraftTranslationLocale(localeOf(path))).sort()

describe('help translation drafts (UG-5)', () => {
  it('defines 8 distinct, non-empty headings for every guide locale', () => {
    expect(Object.keys(helpSectionHeadings).sort()).toEqual([...helpLocales].sort())
    for (const locale of helpLocales) {
      const headings = helpSectionHeadings[locale]
      expect(headings, locale).toHaveLength(8)
      expect(new Set(headings).size, locale).toBe(8)
      for (const heading of headings) expect(heading.trim(), locale).toBe(heading)
    }
    expect(draftLocales).not.toContain('tr')
    expect(draftLocales).not.toContain('en')
    expect(draftLocales).toHaveLength(14)
  })

  it('keeps at least the de/today example draft so the path stays exercised', () => {
    expect(draftGuides).toContain('./content/de/today.md')
  })

  it.each(draftGuides)('%s mirrors its EN guide', path => {
    const locale = localeOf(path)
    const key = /\/([^/]+)\.md$/.exec(path)![1]
    const english = source('en', key)
    expect(english, `${path} has no EN original`).toBeTypeOf('string')
    const draft = parseHelpDocument(raw[path], locale)
    const original = parseHelpDocument(english!, 'en')
    const { title: draftTitle, ...draftMeta } = draft.meta
    const { title: originalTitle, ...originalMeta } = original.meta
    expect(draftMeta, `${path}: front matter must equal EN except title`).toEqual(originalMeta)
    expect(draftTitle.trim(), path).not.toBe('')
    expect(originalTitle).toBeTruthy()
    expect(draft.meta.reviewed, path).toBe(false)
    expect(raw[path], path).toMatch(/^reviewed: false$/m)
    expect(draft.sections.map(section => section.heading)).toEqual(helpSectionHeadings[locale])
    expect(helpStepCount(draft), `${path}: step count must equal EN`).toBe(helpStepCount(original))
    for (const [index, section] of draft.sections.entries())
      if (index !== 7) expect(section.body, `${path}: empty "${section.heading}"`).not.toBe('')
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
    expect(mapping.committedViewports).toContain('desktop')
    for (const key of Object.keys(mapping.guides)) for (const locale of locales) expect(source(locale, key), `${key}/${locale}`).toBeTypeOf('string')
  })

  it.each(locales)('maps every step of every captured %s guide and lists exactly the captured steps', locale => {
    for (const [key, guide] of Object.entries(mapping.guides)) {
      const text = source(locale, key)!
      expect(guide.steps.map(step => step.step), `${key}: mapping must follow the guide steps`).toEqual(Array.from({ length: stepItems(text, locale) }, (_, index) => index + 1))
      const captured = guide.steps.filter(step => !step.unavailable).map(step => step.step)
      expect(parseHelpDocument(text, locale).meta.screenshots, `${key}/${locale}: screenshots front matter`).toEqual(captured)
      for (const step of captured) {
        const base = `./screenshots/${locale}/${key}/${step}-desktop`
        expect(pngs.has(base + '.png'), base + '.png').toBe(true)
        const record = provenance[base + '.json']
        expect(record, base + '.json').toBeDefined()
        expect(record).toMatchObject({ generator: 'tests/Poseidon.Browser.Tests/screenshots/capture.ts', key, locale, step, viewport: 'desktop' })
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
      const match = /^\.\/content\/(tr|en)\/([^/]+)\.md$/.exec(path)
      if (!match) continue // A draft translation reuses the EN screenshots (front matter equality above).
      const [, locale, key] = match
      if (parseHelpDocument(text, locale as HelpLocale).meta.screenshots.length) expect(mapping.guides[key], path).toBeDefined()
    }
  })
})
