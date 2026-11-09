import type { TOptions } from 'i18next'
import en from './messages/en.json'
import tr from './messages/tr.json'
import { i18n, isLocale, locales, registerLocaleLoader, registerResourceBundle, text } from './index'
import type { Locale } from './index'

type Catalog = typeof en
type CopyKey = keyof Catalog['copies']
type LanguageInput = Locale | boolean
const catalogs = new Map<Locale, Catalog>([['en', en], ['tr', tr]])
const loading = new Map<Locale, Promise<void>>()
const modules = import.meta.glob<{ default: Catalog }>('./messages/*.json')
const pairs = new Map<string, string>()
for (const key of Object.keys(en.pairs) as (keyof typeof en.pairs)[]) {
  pairs.set(`${en.pairs[key]}\0${tr.pairs[key]}`, key)
}

// Catalogs are local build assets. Check completeness before activating a locale;
// a partial catalog must not silently mix languages in operational forms.
export function assertCompleteCatalog(candidate: unknown, basis: unknown = en): asserts candidate is Catalog {
  if (typeof basis === 'string') {
    if (typeof candidate !== 'string' || candidate.length === 0) throw new Error('incomplete_locale_catalog')
    const tokens = (value: string) => [...value.matchAll(/\{\{\s*([^{}]+?)\s*\}\}/g)].map(match => match[1]).sort().join('\0')
    if (tokens(candidate) !== tokens(basis)) throw new Error('invalid_locale_interpolation')
    return
  }
  if (basis === null || typeof basis !== 'object') {
    if (candidate !== basis) throw new Error('locale_contract_value_changed')
    return
  }
  if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate) !== Array.isArray(basis)) throw new Error('incomplete_locale_catalog')
  const expected = Object.keys(basis).sort(), actual = Object.keys(candidate).sort()
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) throw new Error('incomplete_locale_catalog')
  for (const key of expected) assertCompleteCatalog((candidate as Record<string, unknown>)[key], (basis as Record<string, unknown>)[key])
}

assertCompleteCatalog(tr)
for (const [locale, catalog] of catalogs) registerResourceBundle(locale, 'messages', catalog)
registerLocaleLoader(async locale => {
  if (catalogs.has(locale)) return
  let pending = loading.get(locale)
  if (!pending) {
    pending = (async () => {
      const load = modules[`./messages/${locale}.json`]
      if (!load) throw new Error('locale_catalog_unavailable')
      const resource = (await load()).default
      assertCompleteCatalog(resource)
      registerResourceBundle(locale, 'messages', resource)
      catalogs.set(locale, resource)
    })()
    loading.set(locale, pending)
  }
  try { await pending } finally { if (loading.get(locale) === pending) loading.delete(locale) }
})

function resolveLanguage(value: LanguageInput): Locale {
  if (typeof value !== 'boolean') return value
  const current = i18n.resolvedLanguage ?? i18n.language
  // Legacy helpers accept an English flag. Their isolated TR/EN behavior stays
  // stable while the selected locale supplies all additional UI languages.
  return isLocale(current) && current !== 'tr' && current !== 'en' ? current : value ? 'en' : 'tr'
}

export function sourceText(locale: 'en' | 'tr', key: string): string { return text(locale, `messages:${key}`) }
export function localizedText(language: LanguageInput, key: string, options?: TOptions): string { return text(resolveLanguage(language), `messages:${key}`, options) }

export function localizedLabel(label: { en: string; tr: string }, language: LanguageInput): string
export function localizedLabel(label: { en: string; tr: string } | undefined, language: LanguageInput): string | undefined
export function localizedLabel(label: { en: string; tr: string } | undefined, language: LanguageInput): string | undefined {
  if (!label) return undefined
  const locale = resolveLanguage(language), key = pairs.get(`${label.en}\0${label.tr}`)
  return key ? text(locale, `messages:pairs.${key}`) : locale === 'tr' ? label.tr : label.en
}
export function localizedTuple(label: readonly string[], language: LanguageInput, englishIndex: 0 | 1): string
export function localizedTuple(label: readonly string[] | undefined, language: LanguageInput, englishIndex: 0 | 1): string | undefined
export function localizedTuple(label: readonly string[] | undefined, language: LanguageInput, englishIndex: 0 | 1): string | undefined {
  if (!label) return undefined
  if (label.length !== 2) throw new Error('invalid_locale_label_tuple')
  return localizedLabel({ en: label[englishIndex], tr: label[englishIndex === 0 ? 1 : 0] }, language)
}

// Getters read the selected catalog at render time, so module-level label
// dictionaries cannot freeze an old language before lazy resources arrive.
export function localizedCopy<K extends CopyKey>(key: K): Readonly<Record<Locale, Catalog['copies'][K]>> {
  const result = {} as Record<Locale, Catalog['copies'][K]>
  for (const locale of locales) Object.defineProperty(result, locale, { enumerable: true, get: () => {
      const catalog = catalogs.get(locale)
      if (!catalog) throw new Error('locale_catalog_not_loaded')
      return catalog.copies[key]
    } })
  return Object.freeze(result)
}
