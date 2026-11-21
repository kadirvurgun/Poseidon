import { parseHelpDocument, type HelpDocument, type HelpLocale } from './help-model'

// USER-GUIDE-01 UG-1 loader. Each locale is a separate lazy bundle chunk (a static same-origin
// asset precached by the offline shell); loading it is never an API request, command or AI call.
// Only TR and EN content exists in UG-1; other locales fall back to EN and say so (D6).
const bundles = import.meta.glob<{ default: Record<string, string> }>('./content/*/help-*.ts')
const cache = new Map<HelpLocale, Promise<ReadonlyMap<string, HelpDocument>>>()

export function helpLocaleFor(language: string): { locale: HelpLocale; fallback: boolean } {
  if (language === 'tr') return { locale: 'tr', fallback: false }
  if (language === 'en' || language === 'en-GB') return { locale: 'en', fallback: false }
  return { locale: 'en', fallback: true }
}

function load(locale: HelpLocale): Promise<ReadonlyMap<string, HelpDocument>> {
  let pending = cache.get(locale)
  if (!pending) {
    const loader = bundles[`./content/${locale}/help-${locale}.ts`]
    pending = (loader ? loader() : Promise.reject(new Error('help_locale_unavailable'))).then(module => {
      const result = new Map<string, HelpDocument>()
      for (const [path, source] of Object.entries(module.default)) {
        const document = parseHelpDocument(source, locale)
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

export async function loadHelpDocument(language: string, key: string): Promise<{ document: HelpDocument | null; fallback: boolean }> {
  const { locale, fallback } = helpLocaleFor(language)
  const documents = await load(locale)
  return { document: documents.get(key) ?? null, fallback }
}
