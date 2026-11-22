import { useEffect, useId, useRef, useState } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent } from 'react'
import { X } from 'lucide-react'
import { Button } from '../components/ui/button'
import { trapTab, useInertBackground } from '../components/a11y/modal'
import { localizedCopy } from '../i18n/messages'
import type { Locale as AppLocale } from '../i18n'
import { HelpMarkdown, helpAnchorId, type HelpLinkPolicy } from './HelpMarkdown'
import { helpLocaleFor, loadHelpDocument } from './help-content'
import { HelpScreenshot, helpScreenshotUrl } from './help-screenshots'
import { pagesWithWhyView, relatedSectionIndex, stepSectionIndex, type HelpDocument, type HelpLocale } from './help-model'
import './help-panel.css'

// USER-GUIDE-01 UG-1 / 65.03 paragraphs 57-59: help panel with separate Guide, Why and AI tabs.
// Opening it starts no API request, command or AI call: guide text is a bundled static asset and
// the Why tab only points to the owning screen's own disclosure (D2).

const copy = localizedCopy('HelpPanel_tsx_copy')
export type HelpDestination = { key: string; href: string; label: string }
type Tab = 'guide' | 'why' | 'ai'
const tabs: Tab[] = ['guide', 'why', 'ai']
type Load = { state: 'loading' } | { state: 'ready'; document: HelpDocument; fallback: boolean } | { state: 'missing' } | { state: 'error' }

const mobileQuery = '(max-width: 767px)'
function useFullPage() {
  const [full, setFull] = useState(() => typeof window.matchMedia === 'function' && window.matchMedia(mobileQuery).matches)
  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return
    const media = window.matchMedia(mobileQuery)
    const update = () => setFull(media.matches)
    media.addEventListener('change', update)
    return () => media.removeEventListener('change', update)
  }, [])
  return full
}

type Props = {
  pageKey: string | undefined
  title: string
  language: AppLocale
  /** The current user's server-granted navigation, already labelled. Nothing else is ever named. */
  destinations: HelpDestination[]
  onNavigate: (href: string) => void
  onClose: () => void
}

export function HelpPanel({ pageKey, title, language, destinations, onNavigate, onClose }: Props) {
  const t = copy[language]
  const id = useId()
  const panel = useRef<HTMLDivElement>(null)
  const closeButton = useRef<HTMLButtonElement>(null)
  const tabRefs = useRef<Record<Tab, HTMLButtonElement | null>>({ guide: null, why: null, ai: null })
  const [tab, setTab] = useState<Tab>('guide')
  const [loaded, setLoaded] = useState<{ owner: string; value: Load } | null>(null)
  const fullPage = useFullPage()
  const owner = `${language}\0${pageKey ?? ''}`
  useInertBackground(panel)
  useEffect(() => { closeButton.current?.focus() }, [])
  useEffect(() => {
    if (!pageKey) return
    let active = true
    loadHelpDocument(language, pageKey).then(
      result => { if (active) setLoaded({ owner, value: result.document ? { state: 'ready', document: result.document, fallback: result.fallback } : { state: 'missing' } }) },
      () => { if (active) setLoaded({ owner, value: { state: 'error' } }) })
    return () => { active = false }
  }, [language, pageKey, owner])
  const load: Load = !pageKey ? { state: 'missing' } : loaded?.owner === owner ? loaded.value : { state: 'loading' }

  const granted = new Map(destinations.map(item => [item.key, item]))
  const policy: HelpLinkPolicy = {
    allowedRoutes: new Set(destinations.map(item => item.href.split('?')[0])),
    onNavigate,
    onAnchor: target => {
      const element = panel.current?.querySelector<HTMLElement>(`[id="${target}"]`) // target matches help-[a-z0-9-]+
      if (element) { element.tabIndex = -1; element.focus(); element.scrollIntoView?.({ block: 'start' }) }
    },
  }
  const onKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); onClose(); return }
    trapTab(event, panel.current)
  }
  const onTabKey = (event: ReactKeyboardEvent<HTMLButtonElement>, current: Tab) => {
    const index = tabs.indexOf(current)
    const next = event.key === 'ArrowRight' ? tabs[(index + 1) % tabs.length] : event.key === 'ArrowLeft' ? tabs[(index + tabs.length - 1) % tabs.length]
      : event.key === 'Home' ? tabs[0] : event.key === 'End' ? tabs[tabs.length - 1] : null
    if (!next) return
    event.preventDefault()
    setTab(next)
    tabRefs.current[next]?.focus()
  }
  const labels: Record<Tab, string> = { guide: t.tabGuide, why: t.tabWhy, ai: t.tabAi }

  return <div ref={panel} className={`help-panel ${fullPage ? 'help-panel--page' : 'help-panel--side'}`} role="dialog" aria-modal="true" aria-labelledby={`${id}-title`} onKeyDown={onKeyDown} data-testid="help-panel">
    <div className="help-panel-header">
      <h2 id={`${id}-title`}><span>{t.dialog}</span> {title}</h2>
      <Button ref={closeButton} variant="ghost" size="icon" className="help-panel-close" aria-label={t.close} onClick={onClose}><X aria-hidden="true"/></Button>
    </div>
    <div className="help-panel-tabs" role="tablist" aria-label={t.dialog}>
      {tabs.map(item => <button key={item} ref={element => { tabRefs.current[item] = element }} type="button" role="tab" id={`${id}-tab-${item}`} aria-controls={`${id}-panel-${item}`}
        aria-selected={tab === item} tabIndex={tab === item ? 0 : -1} onClick={() => setTab(item)} onKeyDown={event => onTabKey(event, item)}>{labels[item]}</button>)}
    </div>
    <div className="help-panel-body" role="tabpanel" id={`${id}-panel-${tab}`} aria-labelledby={`${id}-tab-${tab}`} tabIndex={0}>
      {tab === 'guide' && <GuideTab load={load} locale={helpLocaleFor(language).locale} t={t} policy={policy} granted={granted} onNavigate={onNavigate}/>}
      {tab === 'why' && <p className="help-panel-note">{pageKey && pagesWithWhyView.has(pageKey) ? t.whyHint : t.whyEmpty}</p>}
      {tab === 'ai' && <p className="help-panel-note">{t.aiDisabled}</p>}
    </div>
    <p className="help-panel-shortcut">{t.shortcut}</p>
  </div>
}

type Copy = (typeof copy)['en']
function GuideTab({ load, locale, t, policy, granted, onNavigate }: { load: Load; locale: HelpLocale; t: Copy; policy: HelpLinkPolicy; granted: Map<string, HelpDestination>; onNavigate: (href: string) => void }) {
  if (load.state === 'loading') return <p role="status">{t.loading}</p>
  if (load.state === 'error') return <p role="alert">{t.failed}</p>
  if (load.state === 'missing') return <p className="help-panel-note" role="status">{t.pending}</p>
  const { document, fallback } = load
  // D3 / 65.03 p50: related pages are intersected with the caller's own navigation; a related key the
  // user cannot open contributes nothing - no name, no count, no placeholder.
  const related = document.meta.related.flatMap(key => granted.get(key) ?? [])
  // UG-2: a step shows its generated screenshot only when the guide lists it and the file exists.
  const shots = new Set(document.meta.screenshots)
  const afterStep = (step: number) => {
    const src = shots.has(step) ? helpScreenshotUrl(fallback ? 'en' : locale, document.meta.key, step) : undefined
    return src ? <HelpScreenshot src={src} step={step} t={t}/> : null
  }
  return <article className="help-guide" lang={fallback ? 'en' : undefined}>
    {document.meta.reviewed === false && <p className="help-guide-draft" role="note">{t.draft}</p>}
    {fallback && <p className="help-panel-note" role="note">{t.fallback}</p>}
    {document.sections.map((section, index) => <section key={section.heading} aria-labelledby={helpAnchorId(section.heading)}>
      <h3 id={helpAnchorId(section.heading)}>{section.heading}</h3>
      {index === relatedSectionIndex
        ? related.length ? <ul className="help-guide-related">{related.map(item => <li key={item.key}><a href={item.href} onClick={event => { event.preventDefault(); onNavigate(item.href) }}>{item.label}</a></li>)}</ul> : <p>{t.relatedNone}</p>
        : <HelpMarkdown source={section.body} policy={policy} idPrefix={`s${index}`} afterStep={index === stepSectionIndex ? afterStep : undefined}/>}
    </section>)}
  </article>
}
