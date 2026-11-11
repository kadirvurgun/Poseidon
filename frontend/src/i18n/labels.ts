import en from './labels/en.json'
import type { TOptions } from 'i18next'
import { locales, registerResourceBundle, text } from './index'
import type { Locale } from './index'
import { assertCompleteCatalog, resolveMessageLocale } from './messages'

export type PresentationLabel = keyof typeof en
const resources = import.meta.glob<{ default: Record<PresentationLabel, string> }>('./labels/*.json', { eager: true })
const expectedKeys = Object.keys(en).sort()
for (const locale of locales) {
  const resource = resources[`./labels/${locale}.json`]?.default
  if (!resource) throw new Error('incomplete_presentation_labels')
  const keys = Object.keys(resource).sort()
  if (keys.length !== expectedKeys.length || keys.some((key, index) => key !== expectedKeys[index]) ||
      Object.values(resource).some(value => typeof value !== 'string' || !value.trim())) throw new Error('incomplete_presentation_labels')
  assertCompleteCatalog(resource, en)
  registerResourceBundle(locale, 'labels', resource)
}

// Display text only. The caller retains the original enum in option values,
// commands, source pins and receipts; these words never qualify a declaration.
export function presentationLabel(language: Locale | boolean, key: PresentationLabel, options?: TOptions): string {
  return text(resolveMessageLocale(language), `labels:${key}`, options)
}
