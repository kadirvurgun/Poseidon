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
