import en from './labels/en.json' with { type: 'json' }
import label_ar from './labels/ar.json' with { type: 'json' }
import label_cs from './labels/cs.json' with { type: 'json' }
import label_de from './labels/de.json' with { type: 'json' }
import label_en_GB from './labels/en-GB.json' with { type: 'json' }
import label_en from './labels/en.json' with { type: 'json' }
import label_es from './labels/es.json' with { type: 'json' }
import label_fi from './labels/fi.json' with { type: 'json' }
import label_fr from './labels/fr.json' with { type: 'json' }
import label_hi from './labels/hi.json' with { type: 'json' }
import label_hu from './labels/hu.json' with { type: 'json' }
import label_it from './labels/it.json' with { type: 'json' }
import label_pt_BR from './labels/pt-BR.json' with { type: 'json' }
import label_ru from './labels/ru.json' with { type: 'json' }
import label_sk from './labels/sk.json' with { type: 'json' }
import label_tr from './labels/tr.json' with { type: 'json' }
import label_zh_Hans from './labels/zh-Hans.json' with { type: 'json' }
import label_zh_Hant from './labels/zh-Hant.json' with { type: 'json' }
import type { TOptions } from 'i18next'
import { locales, registerResourceBundle, text } from './index'
import type { Locale } from './index'
import { assertCompleteCatalog, resolveMessageLocale } from './messages'

export type PresentationLabel = keyof typeof en
// Explicit static imports (not import.meta.glob) so Node/Playwright can load this module too.
const resources: Record<string, { default: Record<PresentationLabel, string> } | undefined> = {
  './labels/ar.json': { default: label_ar },
  './labels/cs.json': { default: label_cs },
  './labels/de.json': { default: label_de },
  './labels/en-GB.json': { default: label_en_GB },
  './labels/en.json': { default: label_en },
  './labels/es.json': { default: label_es },
  './labels/fi.json': { default: label_fi },
  './labels/fr.json': { default: label_fr },
  './labels/hi.json': { default: label_hi },
  './labels/hu.json': { default: label_hu },
  './labels/it.json': { default: label_it },
  './labels/pt-BR.json': { default: label_pt_BR },
  './labels/ru.json': { default: label_ru },
  './labels/sk.json': { default: label_sk },
  './labels/tr.json': { default: label_tr },
  './labels/zh-Hans.json': { default: label_zh_Hans },
  './labels/zh-Hant.json': { default: label_zh_Hant },
}
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

export function codeOptionLabel(value: string, language: Locale | boolean, labels: Readonly<Record<string, PresentationLabel>>): string {
  const key = Object.hasOwn(labels, value) ? labels[value] : undefined
  // An unknown server code remains source data; a translation never converts
  // it into a supported choice or an approval. Prototype members are not codes.
  return key === undefined ? value : presentationLabel(language, key)
}
