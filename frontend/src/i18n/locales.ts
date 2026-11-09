export const locales = ['ar', 'cs', 'hu', 'fr', 'en', 'fi', 'en-GB', 'zh-Hans', 'hi', 'sk', 'tr', 'ru', 'pt-BR', 'it', 'es', 'de', 'zh-Hant'] as const
export type Locale = typeof locales[number]
export const defaultLocale: Locale = 'tr'
export const localeStorageKey = 'poseidon.locale'
export const nativeLocaleNames: Record<Locale, string> = {
  ar: 'العربية', cs: 'Čeština', hu: 'Magyar', fr: 'Français', en: 'English', fi: 'Suomi',
  'en-GB': 'English (United Kingdom)', 'zh-Hans': '简体中文', hi: 'हिन्दी', sk: 'Slovenčina',
  tr: 'Türkçe', ru: 'Русский', 'pt-BR': 'Português (Brasil)', it: 'Italiano', es: 'Español',
  de: 'Deutsch', 'zh-Hant': '繁體中文',
}
export function isLocale(value: unknown): value is Locale {
  return typeof value === 'string' && locales.some(locale => locale === value)
}
export function localeDirection(locale: Locale): 'rtl' | 'ltr' { return locale === 'ar' ? 'rtl' : 'ltr' }
// No identity or business data is persisted with this UI preference.
export function readSavedLocale(storage?: Pick<Storage, 'getItem'>): Locale {
  try {
    const saved = (storage ?? (typeof window === 'undefined' ? undefined : window.localStorage))?.getItem(localeStorageKey)
    return isLocale(saved) ? saved : defaultLocale
  } catch { return defaultLocale }
}
export function persistLocale(locale: Locale, storage?: Pick<Storage, 'setItem'>): void {
  if (!isLocale(locale)) return
  try { (storage ?? (typeof window === 'undefined' ? undefined : window.localStorage))?.setItem(localeStorageKey, locale) }
  catch { /* Blocked storage must not prevent a usable local language selection. */ }
}
