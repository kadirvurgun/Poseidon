import { describe, expect, it } from 'vitest'
import { defaultLocale, isLocale, localeDirection, locales, localeStorageKey, nativeLocaleNames, persistLocale, readSavedLocale } from './locales'
import { commonMessages } from './common'

describe('controlled UI locale registry and preference', () => {
  it('keeps the exact requested 17 locales, including regional and script distinctions', () => {
    expect(locales).toEqual(['ar', 'cs', 'hu', 'fr', 'en', 'fi', 'en-GB', 'zh-Hans', 'hi', 'sk', 'tr', 'ru', 'pt-BR', 'it', 'es', 'de', 'zh-Hant'])
    expect(new Set(locales).size).toBe(17)
    expect(defaultLocale).toBe('tr')
    expect(nativeLocaleNames['en-GB']).not.toBe(nativeLocaleNames.en)
    expect(nativeLocaleNames['zh-Hans']).not.toBe(nativeLocaleNames['zh-Hant'])
  })
  it.each(locales)('reads %s as an exact supported preference with the correct direction', locale => {
    const observed: string[] = []
    expect(readSavedLocale({ getItem: key => { observed.push(key); return locale } })).toBe(locale)
    expect(observed).toEqual([localeStorageKey])
    expect(localeDirection(locale)).toBe(locale === 'ar' ? 'rtl' : 'ltr')
    expect(isLocale(locale)).toBe(true)
  })
  it.each([null, undefined, '', 'EN', 'en-US', 'zh', 'zh-CN', 'ar-SA', '../ar', '{"locale":"ar"}', '<script>', 42])('rejects unsupported or corrupt preference %s', value => {
    expect(isLocale(value)).toBe(false)
    expect(readSavedLocale({ getItem: () => value as string | null })).toBe('tr')
  })
  it('continues safely when storage access is blocked', () => {
    expect(readSavedLocale({ getItem: () => { throw new DOMException('blocked', 'SecurityError') } })).toBe('tr')
    expect(() => persistLocale('ar', { setItem: () => { throw new DOMException('blocked', 'SecurityError') } })).not.toThrow()
  })
  it('persists only the safelisted locale preference', () => {
    const saved: [string, string][] = []
    persistLocale('zh-Hant', { setItem: (key, value) => { saved.push([key, value]) } })
    persistLocale('en-US' as never, { setItem: (key, value) => { saved.push([key, value]) } })
    expect(saved).toEqual([['poseidon.locale', 'zh-Hant']])
  })
  it.each(locales)('provides the complete common message contract for %s', locale => {
    const messages = commonMessages[locale]
    expect(Object.keys(messages).sort()).toEqual(Object.keys(commonMessages.en).sort())
    expect(Object.keys(messages)).toHaveLength(38)
    for (const value of Object.values(messages)) {
      expect(value.trim()).not.toBe('')
      expect(value).not.toMatch(/TODO|TRANSLATE_ME|\[missing\]/)
    }
    if (locale !== 'en' && locale !== 'en-GB') expect(messages.chooseLanguage).not.toBe(commonMessages.en.chooseLanguage)
  })
})
