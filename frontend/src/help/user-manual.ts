// HELP-MANUAL-DOWNLOAD-01 (USER-GUIDE-01 UG-4, D7): the TR/EN user manual PDFs shipped as static build assets.
// The PDFs are produced by tests/Poseidon.Browser.Tests/manual/build-manual.ts in the non-required
// help-screenshots workflow and committed with manual/provenance.json (sha256, bytes, generator commit,
// reviewed:false). Vite emits them as content-hashed files under assets/; the offline shell precaches only
// js/css/woff2, so the PDFs are never part of the offline inventory. Downloading changes nothing.
import trUrl from './manual/poseidon-user-manual-tr.pdf?url'
import enUrl from './manual/poseidon-user-manual-en.pdf?url'
import provenance from './manual/provenance.json'

export type UserManualLocale = 'tr' | 'en'
export type UserManualDownload = { locale: UserManualLocale; url: string; fileName: string; bytes: number }

export const userManualProvenance = provenance
/** Draft until a human expert review (USER-GUIDE-01 UG-6 / UG-REV-01) is recorded; never claimed by the app. */
export const userManualReviewed = provenance.reviewed === true

const urls: Record<UserManualLocale, string> = { tr: trUrl, en: enUrl }

/** Both shipped manuals; the reader's own language (TR or EN) first. Pure. */
export function userManualDownloads(appLocale: string): UserManualDownload[] {
  const own: UserManualLocale = appLocale === 'tr' ? 'tr' : 'en'
  return (['tr', 'en'] as const).map(locale => ({ locale, url: urls[locale], fileName: provenance.files[locale].file, bytes: provenance.files[locale].bytes }))
    .sort((a, b) => Number(b.locale === own) - Number(a.locale === own))
}

/** True when the reader's language is neither TR nor EN (the manual exists only in those two). */
export function userManualOtherLocale(appLocale: string): boolean {
  return appLocale !== 'tr' && appLocale !== 'en' && appLocale !== 'en-GB'
}

/** Localised megabytes with one decimal, e.g. "5,4 MB" / "5.4 MB". */
export function formatManualSize(bytes: number, appLocale: string): string {
  return new Intl.NumberFormat(appLocale, { style: 'unit', unit: 'megabyte', maximumFractionDigits: 1, minimumFractionDigits: 1 }).format(bytes / 1_048_576)
}
