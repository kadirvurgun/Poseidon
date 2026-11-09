import { beforeEach, describe, expect, it } from 'vitest'
import en from './messages/en.json'
import { changeAppLanguage, i18n } from './index'
import { assertCompleteCatalog, localizedCopy, localizedLabel, localizedText, localizedTuple, sourceText } from './messages'

beforeEach(async () => { await changeAppLanguage('tr', false) })

describe('operational interface catalogs', () => {
  it('rejects incomplete or structurally changed catalogs before a locale can be selected', () => {
    const missing = structuredClone(en)
    const first = Object.keys(missing.pairs)[0] as keyof typeof missing.pairs
    delete (missing.pairs as Partial<typeof missing.pairs>)[first]
    expect(() => assertCompleteCatalog(missing)).toThrow('incomplete_locale_catalog')
    expect(() => assertCompleteCatalog({ ...en, unexpected: 'injected namespace' })).toThrow('incomplete_locale_catalog')
    expect(() => assertCompleteCatalog({ ...en, pairs: [] })).toThrow('incomplete_locale_catalog')
  })

  it('requires the exact interpolation variables and leaves canonical nonstring values unchanged', () => {
    const basis = { title: 'Source {{value0}} / {{value1}}', policy: false, revision: 1 }
    expect(() => assertCompleteCatalog({ title: 'Kaynak {{value1}} / {{value0}}', policy: false, revision: 1 }, basis)).not.toThrow()
    expect(() => assertCompleteCatalog({ title: 'Kaynak {{value0}}', policy: false, revision: 1 }, basis)).toThrow('invalid_locale_interpolation')
    expect(() => assertCompleteCatalog({ title: 'Kaynak {{value0}} / {{value1}}', policy: true, revision: 1 }, basis)).toThrow('locale_contract_value_changed')
  })

  it('uses the selected Arabic catalog for existing label helpers without changing source fields or global locale', async () => {
    await changeAppLanguage('ar', false)
    const label = { en: sourceText('en', 'pairs.m_442bb3be7a6d4b52'), tr: sourceText('tr', 'pairs.m_442bb3be7a6d4b52') }
    const original = structuredClone(label)
    const translated = localizedText(false, 'pairs.m_442bb3be7a6d4b52')
    expect(translated).toMatch(/\p{Script=Arabic}/u)
    expect(localizedLabel(label, false)).toBe(translated)
    expect(localizedTuple([label.en, label.tr], false, 0)).toBe(translated)
    expect(label).toEqual(original)
    expect(i18n.language).toBe('ar')
    expect(document.documentElement.dir).toBe('rtl')
  })

  it('keeps dictionary access live across distinct locales instead of freezing an import-time translation', async () => {
    const copy = localizedCopy('App_tsx_content')
    const english = copy.en.page
    await changeAppLanguage('de', false)
    expect(copy.de.page).toBe('Systemverbindung')
    expect(copy.en.page).toBe(english)
    await changeAppLanguage('en-GB', false)
    expect(i18n.language).toBe('en-GB')
    expect(document.documentElement.dir).toBe('ltr')
  })
})
