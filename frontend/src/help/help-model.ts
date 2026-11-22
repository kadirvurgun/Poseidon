// USER-GUIDE-01 UG-1: in-app page guide content model (docs/tasks/USER-GUIDE-01.md section 4).
// Content is bundled Markdown authored in this repository. Parsing is pure and side-effect free:
// opening the help panel never issues an API request, command or AI call (D2, 65.03 p59).

/**
 * Guide content locales (USER-GUIDE-01 D6). `en-GB` reads the `en` guides. TR and EN are the
 * authored languages; every other locale holds machine-drafted translations that always show the
 * "translation not reviewed" notice and fall back to EN, per page, where a file does not exist.
 */
export const helpLocales = ['tr', 'en', 'ar', 'cs', 'de', 'es', 'fi', 'fr', 'hi', 'hu', 'it', 'pt-BR', 'ru', 'sk', 'zh-Hans', 'zh-Hant'] as const
export type HelpLocale = typeof helpLocales[number]
/** Locales whose guides are authored directly (not machine-drafted translations of EN). */
export const authoredHelpLocales: ReadonlySet<HelpLocale> = new Set<HelpLocale>(['tr', 'en'])
export function isHelpLocale(value: string): value is HelpLocale { return (helpLocales as readonly string[]).includes(value) }
/** True for a locale whose guides are unreviewed machine-drafted translations (UG-5, D6). */
export function isDraftTranslationLocale(locale: HelpLocale): boolean { return !authoredHelpLocales.has(locale) }
export type HelpReview = false | { by: string; date: string }
export type HelpFrontMatter = {
  key: string
  title: string
  roles: string[]
  permissions: string[]
  related: string[]
  /** "Step by step" step numbers that have a generated screenshot (USER-GUIDE-01 UG-2, D4). */
  screenshots: number[]
  reviewed: HelpReview
}
export type HelpSection = { heading: string; body: string }
export type HelpDocument = { meta: HelpFrontMatter; sections: HelpSection[] }

/**
 * The 8 fixed section headings, in order, for every guide locale. Each guide must contain all of
 * them exactly once. This is the single heading table translators use (UG-5 translator brief).
 */
export const helpSectionHeadings: Readonly<Record<HelpLocale, readonly string[]>> = Object.freeze({
  tr: Object.freeze(['Bu sayfa ne işe yarar', 'Kim kullanır', 'Başlamadan önce', 'Adım adım', 'Durumlar ne anlama gelir', 'Yapamazsınız / dikkat', 'Sorun olursa', 'İlgili sayfalar']),
  en: Object.freeze(['What this page is for', 'Who uses it', 'Before you start', 'Step by step', 'What the states mean', 'What you cannot do / caution', 'If something goes wrong', 'Related pages']),
  ar: Object.freeze(['الغرض من هذه الصفحة', 'من يستخدمها', 'قبل أن تبدأ', 'خطوة بخطوة', 'معنى الحالات', 'ما لا يمكنك فعله / تنبيه', 'إذا حدث خطأ ما', 'صفحات ذات صلة']),
  cs: Object.freeze(['K čemu tato stránka slouží', 'Kdo ji používá', 'Než začnete', 'Krok za krokem', 'Co znamenají stavy', 'Co nemůžete dělat / upozornění', 'Když se něco pokazí', 'Související stránky']),
  de: Object.freeze(['Wofür diese Seite dient', 'Wer sie verwendet', 'Bevor Sie beginnen', 'Schritt für Schritt', 'Was die Zustände bedeuten', 'Was Sie nicht tun können / Vorsicht', 'Wenn etwas schiefgeht', 'Verwandte Seiten']),
  es: Object.freeze(['Para qué sirve esta página', 'Quién la usa', 'Antes de empezar', 'Paso a paso', 'Qué significan los estados', 'Lo que no puede hacer / precaución', 'Si algo sale mal', 'Páginas relacionadas']),
  fi: Object.freeze(['Mihin tätä sivua käytetään', 'Kuka sitä käyttää', 'Ennen kuin aloitat', 'Vaihe vaiheelta', 'Mitä tilat tarkoittavat', 'Mitä et voi tehdä / varoitus', 'Jos jokin menee vikaan', 'Liittyvät sivut']),
  fr: Object.freeze(['À quoi sert cette page', 'Qui l’utilise', 'Avant de commencer', 'Étape par étape', 'Signification des états', 'Ce que vous ne pouvez pas faire / prudence', 'En cas de problème', 'Pages associées']),
  hi: Object.freeze(['यह पृष्ठ किसलिए है', 'इसका उपयोग कौन करता है', 'शुरू करने से पहले', 'चरण दर चरण', 'स्थितियों का अर्थ', 'आप क्या नहीं कर सकते / सावधानी', 'अगर कुछ गलत हो जाए', 'संबंधित पृष्ठ']),
  hu: Object.freeze(['Mire szolgál ez az oldal', 'Ki használja', 'Mielőtt elkezdené', 'Lépésről lépésre', 'Mit jelentenek az állapotok', 'Amit nem tehet meg / figyelem', 'Ha valami nem sikerül', 'Kapcsolódó oldalak']),
  it: Object.freeze(['A cosa serve questa pagina', 'Chi la usa', 'Prima di iniziare', 'Passo dopo passo', 'Cosa significano gli stati', 'Cosa non si può fare / attenzione', 'Se qualcosa va storto', 'Pagine correlate']),
  'pt-BR': Object.freeze(['Para que serve esta página', 'Quem usa', 'Antes de começar', 'Passo a passo', 'O que significam os estados', 'O que você não pode fazer / cuidado', 'Se algo der errado', 'Páginas relacionadas']),
  ru: Object.freeze(['Для чего эта страница', 'Кто ею пользуется', 'Прежде чем начать', 'Пошагово', 'Что означают состояния', 'Чего делать нельзя / осторожно', 'Если что-то пошло не так', 'Связанные страницы']),
  sk: Object.freeze(['Na čo slúži táto stránka', 'Kto ju používa', 'Skôr než začnete', 'Krok za krokom', 'Čo znamenajú stavy', 'Čo nemôžete robiť / upozornenie', 'Ak sa niečo pokazí', 'Súvisiace stránky']),
  'zh-Hans': Object.freeze(['本页用途', '谁使用本页', '开始之前', '操作步骤', '状态含义', '不能做的事 / 注意', '出现问题时', '相关页面']),
  'zh-Hant': Object.freeze(['本頁用途', '誰使用本頁', '開始之前', '操作步驟', '狀態含義', '不能做的事 / 注意', '發生問題時', '相關頁面']),
})
/** Index of the "Related pages" section; its body is computed from front matter and the caller's own navigation. */
export const relatedSectionIndex = 7
/** Index of the "Step by step" section; its numbered items receive the generated screenshots (UG-2). */
export const stepSectionIndex = 3

/** Number of top-level numbered items in a guide's "Step by step" section. */
export function helpStepCount(document: HelpDocument): number {
  return document.sections[stepSectionIndex].body.split('\n').filter(line => /^\d+\. /.test(line.trim())).length
}

/**
 * Pages whose screen has its own record-level "Why" disclosure. The help panel only points to it;
 * reading a Why payload is an owning-screen request and is never started by the panel.
 */
export const pagesWithWhyView: ReadonlySet<string> = new Set(['today', 'tug-job-board'])

const frontMatterKeys = ['key', 'title', 'roles', 'permissions', 'related', 'screenshots', 'reviewed'] as const
const pageKey = /^[a-z0-9]+(?:-[a-z0-9]+)*$/

export class HelpContentError extends Error {}

function fail(reason: string): never { throw new HelpContentError(reason) }

function parseList(raw: string, field: string): string[] {
  if (!raw.startsWith('[') || !raw.endsWith(']')) fail(`help_front_matter_list_required:${field}`)
  const inner = raw.slice(1, -1).trim()
  if (!inner) return []
  const items = inner.split(',').map(item => item.trim())
  if (items.some(item => !item)) fail(`help_front_matter_list_invalid:${field}`)
  return items
}

function parseScreenshots(raw: string): number[] {
  const steps = parseList(raw, 'screenshots').map(item => /^[1-9][0-9]?$/.test(item) ? Number(item) : fail('help_front_matter_screenshots_invalid'))
  if (steps.some((step, index) => index > 0 && step <= steps[index - 1])) fail('help_front_matter_screenshots_invalid')
  return steps
}

function parseReview(raw: string): HelpReview {
  if (raw === 'false') return false
  // A human SME/UX review (UG-REV-01) is recorded as `{ by: <name>, date: YYYY-MM-DD }`.
  // Only a human reviewer may write it; an AI author never sets this field.
  const match = /^\{\s*by:\s*([^,{}]+?)\s*,\s*date:\s*(\d{4}-\d{2}-\d{2})\s*\}$/.exec(raw)
  if (!match) fail('help_front_matter_reviewed_invalid')
  return { by: match[1], date: match[2] }
}

/** Parses one guide file. Throws HelpContentError on any structural deviation (closed shape). */
export function parseHelpDocument(source: string, locale: HelpLocale): HelpDocument {
  const text = source.replace(/\r\n?/g, '\n')
  if (!text.startsWith('---\n')) fail('help_front_matter_missing')
  const end = text.indexOf('\n---\n', 4)
  if (end < 0) fail('help_front_matter_unterminated')
  const values = new Map<string, string>()
  for (const line of text.slice(4, end).split('\n')) {
    if (!line.trim()) continue
    const match = /^([a-z]+):\s*(.*)$/.exec(line)
    if (!match || values.has(match[1])) fail('help_front_matter_line_invalid')
    values.set(match[1], match[2].trim())
  }
  if (values.size !== frontMatterKeys.length || frontMatterKeys.some(field => !values.has(field))) fail('help_front_matter_fields_invalid')
  const key = values.get('key')!, title = values.get('title')!
  if (!pageKey.test(key)) fail('help_front_matter_key_invalid')
  if (!title) fail('help_front_matter_title_missing')
  const related = parseList(values.get('related')!, 'related')
  if (related.some(item => !pageKey.test(item) || item === key) || new Set(related).size !== related.length) fail('help_front_matter_related_invalid')
  const meta: HelpFrontMatter = {
    key, title, roles: parseList(values.get('roles')!, 'roles'), permissions: parseList(values.get('permissions')!, 'permissions'),
    related, screenshots: parseScreenshots(values.get('screenshots')!), reviewed: parseReview(values.get('reviewed')!),
  }
  const sections: HelpSection[] = []
  let current: HelpSection | null = null
  const lead: string[] = []
  for (const line of text.slice(end + 5).split('\n')) {
    const heading = /^## (.+)$/.exec(line)
    if (heading) {
      current = { heading: heading[1].trim(), body: '' }
      sections.push(current)
    } else if (current) current.body += line + '\n'
    else lead.push(line)
  }
  if (lead.some(line => line.trim())) fail('help_body_outside_section')
  const expected = helpSectionHeadings[locale]
  if (sections.length !== expected.length || sections.some((section, index) => section.heading !== expected[index])) fail('help_sections_invalid')
  for (const section of sections) section.body = section.body.trim()
  return { meta, sections }
}
