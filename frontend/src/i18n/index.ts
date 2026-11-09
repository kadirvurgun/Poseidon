import { useCallback, useSyncExternalStore } from 'react'
import { createInstance } from 'i18next'
import type { ResourceLanguage, TOptions } from 'i18next'
import { initReactI18next } from 'react-i18next'
import { commonMessages } from './common'
import { defaultLocale, isLocale, localeDirection, locales, persistLocale, readSavedLocale } from './locales'
import type { Locale } from './locales'

export { defaultLocale, isLocale, localeDirection, locales, localeStorageKey, nativeLocaleNames, readSavedLocale } from './locales'
export type { Locale } from './locales'

// Local resources only: no detection service, remote backend, HTML translation or
// persisted application data. In particular, en-GB and both Chinese scripts stay distinct.
export const i18n = createInstance()
void i18n.use(initReactI18next).init({
  lng: defaultLocale, supportedLngs: [...locales], nonExplicitSupportedLngs: false,
  load: 'currentOnly', fallbackLng: false, defaultNS: 'common', ns: ['common'],
  resources: Object.fromEntries(locales.map(locale => [locale, { common: commonMessages[locale] }])),
  initAsync: false, debug: false, saveMissing: false, returnNull: false,
  interpolation: { escapeValue: false }, react: { useSuspense: false },
})

type LanguageStatus = { locale: Locale; pending: Locale | null; failed: boolean }
let status: LanguageStatus = { locale: defaultLocale, pending: null, failed: false }
const listeners = new Set<() => void>()
const loaders = new Set<(locale: Locale) => Promise<void>>()
let requestGeneration = 0
let startup: Promise<void> | undefined
function emit(next: LanguageStatus) { status = next; for (const listener of listeners) listener() }
function documentLanguage(locale: Locale) {
  if (typeof document === 'undefined') return
  document.documentElement.lang = locale
  document.documentElement.dir = localeDirection(locale)
}
documentLanguage(defaultLocale)
i18n.on('languageChanged', language => {
  if (!isLocale(language)) return
  documentLanguage(language)
  emit({ ...status, locale: language })
})
const subscribe = (listener: () => void) => { listeners.add(listener); return () => { listeners.delete(listener) } }
const snapshot = () => status

// Register during module import, before initializeAppLanguage() renders App.
// Root-owned product namespaces can lazy-import a fixed map of reviewed catalogs.
export function registerLocaleLoader(loader: (locale: Locale) => Promise<void>): () => void {
  loaders.add(loader)
  return () => { loaders.delete(loader) }
}
export function registerResourceBundle(locale: Locale, namespace: string, resources: ResourceLanguage): void {
  if (!isLocale(locale) || !/^[a-z][a-z0-9-]{0,63}$/.test(namespace)) throw new Error('invalid_translation_resource')
  i18n.addResourceBundle(locale, namespace, resources, true, true)
}
export async function changeAppLanguage(locale: Locale, persist = true): Promise<void> {
  if (!isLocale(locale)) return
  const generation = ++requestGeneration
  emit({ ...status, pending: locale, failed: false })
  try {
    await Promise.all([...loaders].map(load => load(locale)))
    if (generation !== requestGeneration) return
    // All registered namespaces are local and ready, so changeLanguage commits
    // synchronously. No React key, route, actor or draft state changes here.
    await i18n.changeLanguage(locale)
    if (generation !== requestGeneration) return
    if (persist) persistLocale(locale)
    emit({ locale, pending: null, failed: false })
  } catch {
    if (generation === requestGeneration) emit({ ...status, pending: null, failed: true })
  }
}
export function initializeAppLanguage(): Promise<void> {
  startup ??= changeAppLanguage(readSavedLocale(), false)
  return startup
}
export function useLanguageStatus(): Readonly<LanguageStatus> { return useSyncExternalStore(subscribe, snapshot, snapshot) }
export function useAppLanguage(): [Locale, (locale: Locale) => void] {
  const current = useLanguageStatus()
  const setLocale = useCallback((locale: Locale) => { void changeAppLanguage(locale) }, [])
  return [current.locale, setLocale]
}
// Explicit-language lookup is pure with respect to the global selection. React
// renders the returned string as text; callers must not inject it as HTML.
export function text(locale: Locale, key: string, options?: TOptions): string {
  const translated = i18n.t(key, { ...options, lng: locale, returnObjects: false, returnDetails: false })
  if (typeof translated !== 'string') throw new Error('invalid_translation_value')
  return translated
}
