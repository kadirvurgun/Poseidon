import { useEffect, useId, useMemo, useState } from 'react'
import { ArrowLeft, ArrowUpRight, Search } from 'lucide-react'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { localizedCopy, localizedText } from '../i18n/messages'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from '../product/EnterpriseChrome'
import { EnterprisePageHeader } from '../product/EnterprisePatterns'
import { foldMenuText } from '../product/EnterpriseSidebarNav'
import { enterpriseSections } from '../product/enterprise-sections'
import { useEnterpriseNavigation, type EnterpriseDestination } from '../product/enterprise-navigation'
import type { Session } from '../product/directory-contract'
import * as api from '../product/api'
import { HelpGuideContent, type HelpDestination } from './HelpPanel'
import type { HelpLinkPolicy } from './HelpMarkdown'
import { helpLocaleFor, loadHelpDocument } from './help-content'
import { isDraftTranslationLocale, type HelpDocument, type HelpLocale } from './help-model'
import '../product/enterprise.css'
import './help-panel.css'

// HELP-CENTER-01 (USER-GUIDE-01 UG-4 "/app/help"): searchable, grouped list of the page guides.
// D3 / 65.03 p50: only guides of pages in the caller's own server-granted navigation are listed;
// a page the user cannot open contributes nothing (no title, no count, no placeholder). While the
// navigation is unknown (loading, failed, signed out, offline) no guide is listed at all.
// D2: reading guides is a bundled static asset; it starts no command or AI call.

const copy = localizedCopy('HelpCenter_tsx_copy')
const panelCopy = localizedCopy('HelpPanel_tsx_copy')
export const helpCenterPath = '/app/help'

export type HelpCenterEntry = { key: string; href: string; document: HelpDocument; locale: HelpLocale; fallback: boolean; text: string }
export type HelpCenterGroup = { key: string; label: string; entries: HelpCenterEntry[] }

/** Groups entries by the sidebar sections (same order as the menu and the user manual). Pure. */
export function groupHelpEntries(entries: readonly HelpCenterEntry[], query: string, language: AppLocale): HelpCenterGroup[] {
  const needle = foldMenuText(query, language)
  const visible = needle ? entries.filter(entry => entry.text.includes(needle)) : entries
  return enterpriseSections.map(section => ({
    key: section.key,
    label: localizedText(language, section.label),
    entries: section.keys.flatMap(key => visible.find(entry => entry.key === key) ?? []),
  })).filter(group => group.entries.length > 0)
}

function searchText(document: HelpDocument, language: AppLocale): string {
  return foldMenuText([document.meta.title, ...document.sections.map(section => section.body)].join('\n'), language)
}

function selectedFromUrl(): string | null {
  const value = new URLSearchParams(window.location.search).get('page')
  return value && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value) ? value : null
}

type Navigation = { destinations: readonly EnterpriseDestination[]; loading: boolean; failed: boolean }
type Props = { language: AppLocale; signedIn: boolean | null; online: boolean; navigation: Navigation; onNavigate: (href: string) => void }

export function HelpCenter({ language, signedIn, online, navigation, onNavigate }: Props) {
  const t = copy[language], p = panelCopy[language]
  const id = useId()
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState<string | null>(selectedFromUrl)
  const [loaded, setLoaded] = useState<{ owner: string; entries?: HelpCenterEntry[]; error?: boolean } | null>(null)
  const granted = navigation.destinations
  const owner = `${language}\0${granted.map(item => `${item.key}=${item.href}`).join('\0')}`
  useEffect(() => {
    const pop = () => setSelected(selectedFromUrl())
    window.addEventListener('popstate', pop)
    return () => window.removeEventListener('popstate', pop)
  }, [])
  useEffect(() => {
    if (!granted.length) return
    let active = true
    Promise.all(granted.map(item => loadHelpDocument(language, item.key).then(result => result.document
      ? [{ key: item.key, href: item.href, document: result.document, locale: result.locale, fallback: result.fallback, text: searchText(result.document, language) }]
      : []))).then(
      rows => { if (active) setLoaded({ owner, entries: rows.flat() }) },
      () => { if (active) setLoaded({ owner, error: true }) })
    return () => { active = false }
  }, [language, owner, granted])
  const current = loaded?.owner === owner ? loaded : null
  const entries = useMemo(() => current?.entries ?? [], [current])
  const groups = useMemo(() => groupHelpEntries(entries, query, language), [entries, query, language])
  const destinations = useMemo(() => new Map<string, HelpDestination>(entries.map(entry => [entry.key, { key: entry.key, href: entry.href, label: entry.document.meta.title }])), [entries])
  const draftLocale = isDraftTranslationLocale(helpLocaleFor(language).locale)

  const open = (key: string | null) => {
    history.pushState(null, '', key ? `${helpCenterPath}?page=${key}` : helpCenterPath)
    setSelected(key)
    document.querySelector('.help-center')?.scrollIntoView?.({ block: 'start' })
  }
  const policy: HelpLinkPolicy = {
    allowedRoutes: new Set(entries.map(entry => entry.href.split('?')[0])),
    onNavigate,
    onAnchor: target => {
      const element = document.querySelector<HTMLElement>(`.help-center-guide [id="${target}"]`) // target matches help-[a-z0-9-]+
      if (element) { element.tabIndex = -1; element.focus(); element.scrollIntoView?.({ block: 'start' }) }
    },
  }

  const status = !online ? <p role="status">{t.offline}</p>
    : signedIn === false ? <p role="alert">{t.signedOut}</p>
    : signedIn === null || navigation.loading ? <p role="status">{t.loading}</p>
    : navigation.failed ? <p role="alert">{t.navigationUnavailable}</p>
    : !granted.length ? <p role="status">{t.noPages}</p>
    : current?.error ? <p role="alert">{t.failed}</p>
    : !current ? <p role="status">{t.loading}</p>
    : null
  const entry = selected ? entries.find(item => item.key === selected) : undefined

  return <div className="help-center">
    {draftLocale && <p className="help-guide-draft" role="note" data-testid="help-center-translation-unreviewed">{t.draftLocale}</p>}
    {status ?? (selected ? <section className="help-center-guide" aria-labelledby={`${id}-guide`}>
      <div className="help-center-guide-actions">
        <Button variant="ghost" onClick={() => open(null)}><ArrowLeft aria-hidden="true"/>{t.backToList}</Button>
        {entry && <Button variant="outline" asChild><a href={entry.href} onClick={event => { event.preventDefault(); onNavigate(entry.href) }}>{t.openPage}<ArrowUpRight aria-hidden="true"/></a></Button>}
      </div>
      {entry ? <>
        <h2 id={`${id}-guide`}>{entry.document.meta.title}</h2>
        <HelpGuideContent load={{ state: 'ready', document: entry.document, locale: entry.locale, fallback: entry.fallback }} t={p} policy={policy} granted={destinations} onNavigate={onNavigate}/>
      </> : <p id={`${id}-guide`} role="alert">{t.notAvailable}</p>}
    </section> : <section className="help-center-list" aria-label={t.listTitle}>
      <div className="help-center-search" role="search">
        <label htmlFor={`${id}-search`}>{t.searchLabel}</label>
        <div className="help-center-search-field"><Search aria-hidden="true"/>
          <input id={`${id}-search`} type="search" autoComplete="off" spellCheck={false} maxLength={80} value={query} aria-controls={`${id}-results`}
            onChange={event => setQuery(event.target.value)} onKeyDown={event => { if (event.key === 'Escape' && query) { event.preventDefault(); setQuery('') } }}/>
        </div>
      </div>
      <div id={`${id}-results`} aria-live="polite">
        {groups.length === 0 ? <p role="status">{t.noResults}</p> : groups.map(group => <section key={group.key} className="help-center-group" aria-labelledby={`${id}-g-${group.key}`}>
          <h3 id={`${id}-g-${group.key}`}>{group.label}</h3>
          <ul>{group.entries.map(item => <li key={item.key}>
            <a href={`${helpCenterPath}?page=${item.key}`} onClick={event => { event.preventDefault(); open(item.key) }}>{item.document.meta.title}</a>
            {item.document.meta.reviewed === false && <span className="help-center-badge">{t.draftBadge}</span>}
          </li>)}</ul>
        </section>)}
      </div>
    </section>)}
    <section className="help-center-manual" aria-labelledby={`${id}-manual`}>
      <h2 id={`${id}-manual`}>{t.manualTitle}</h2>
      <p>{t.manualBody}</p>
    </section>
  </div>
}

export function HelpCenterWorkspace() {
  const [language, setLanguage] = useAppLanguage()
  const [identity, setIdentity] = useState<{ user?: Session; error?: boolean } | null>(null)
  const online = navigator.onLine
  useEffect(() => {
    if (!online) return
    const controller = new AbortController()
    let active = true
    const timer = setTimeout(() => controller.abort(), 10000)
    api.session(controller.signal).then(user => { if (active) setIdentity({ user }) }, () => { if (active) setIdentity({ error: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; controller.abort(); clearTimeout(timer) }
  }, [online])
  const user = identity?.user ?? null
  const navigation = useEnterpriseNavigation(user)
  const title = copy[language].title
  useEffect(() => { document.title = `POSEIDON · ${title}` }, [title])
  const signedIn = identity?.error ? false : !user ? null : user.authenticated && user.canRead
  const move = (href: string, committed?: () => void) => { committed?.(); window.location.assign(href) }
  return <div className="product-shell enterprise-shell"><EnterpriseChrome session={user} language={language} onLanguageChange={setLanguage} onNavigate={move} title={title} mainId="help-center-main"/>
    <main id="help-center-main" tabIndex={-1}>
      <EnterprisePageHeader title={title} eyebrow={copy[language].eyebrow} description={copy[language].description}/>
      <HelpCenter language={language} signedIn={signedIn} online={online} navigation={navigation} onNavigate={href => move(href)}/>
    </main>
  </div>
}
