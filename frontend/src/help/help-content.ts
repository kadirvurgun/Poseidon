import { isHelpLocale, parseHelpDocument, type HelpDocument, type HelpLocale } from './help-model'

// USER-GUIDE-01 UG-1/UG-5 loader. Each locale is a separate lazy bundle chunk (a static same-origin
// asset precached by the offline shell); loading it is never an API request, command or AI call.
// TR and EN are complete. Every other locale may hold machine-drafted translations
// (`content/<locale>/<key>.md` + `help-<locale>.ts`); a page without a file in that locale falls back,
// per page, to EN and says so (D6).
const bundles = import.meta.glob<{ default: Record<string, string> }>('./content/*/help-*.ts')
const cache = new Map<HelpLocale, Promise<ReadonlyMap<string, HelpDocument>>>()
const empty: ReadonlyMap<string, HelpDocument> = new Map()

/** The guide locale for an app language. `en-GB` reads EN guides; an unknown language reads EN with fallback. */
export function helpLocaleFor(language: string): { locale: HelpLocale; fallback: boolean } {
  if (language === 'en-GB') return { locale: 'en', fallback: false }
  if (isHelpLocale(language)) return { locale: language, fallback: false }
  return { locale: 'en', fallback: true }
}

function load(locale: HelpLocale): Promise<ReadonlyMap<string, HelpDocument>> {
  let pending = cache.get(locale)
  if (!pending) {
    const loader = bundles[`./content/${locale}/help-${locale}.ts`]
    // A draft locale with no translated guide yet has no bundle: every page falls back to EN.
    // TR and EN must always have their bundle; a missing one is an unavailable asset.
    const source = loader ? loader() : locale === 'tr' || locale === 'en' ? Promise.reject(new Error('help_locale_unavailable')) : null
    pending = !source ? Promise.resolve(empty) : source.then(module => {
      const result = new Map<string, HelpDocument>()
      for (const [path, text] of Object.entries(module.default)) {
        const document = parseHelpDocument(text, locale)
        // The file name is the navigation key; a mismatching front matter key is a content defect.
        if (path !== `./${document.meta.key}.md`) throw new Error('help_key_mismatch')
        result.set(document.meta.key, document)
      }
      return result
    })
    // A failed load is not cached, so a later open can retry once the asset is reachable.
    pending.catch(() => { if (cache.get(locale) === pending) cache.delete(locale) })
    cache.set(locale, pending)
  }
  return pending
}

/** Starts loading the locale bundle without waiting (used when the chrome mounts). */
export function preloadHelp(language: string): void { load(helpLocaleFor(language).locale).catch(() => undefined) }

/**
 * Loads one page guide. `locale` is the locale of the returned document; `fallback` is true when the
 * guide is shown in EN because the requested language has no guide for this page (D6).
 */
export async function loadHelpDocument(language: string, key: string): Promise<{ document: HelpDocument | null; locale: HelpLocale; fallback: boolean }> {
  const requested = helpLocaleFor(language)
  const own = (await load(requested.locale)).get(key)
  if (own) return { document: own, locale: requested.locale, fallback: requested.fallback }
  if (requested.locale === 'en') return { document: null, locale: 'en', fallback: requested.fallback }
  return { document: (await load('en')).get(key) ?? null, locale: 'en', fallback: true }
}
