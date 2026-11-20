import { localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { useCallback, useEffect, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterprisePageHeader } from './EnterprisePatterns'
import { EnterpriseChrome } from './EnterpriseChrome'
import { ApiError, session as readSession, vessels as readVessels, type Session, type Vessel } from './api'
import { isGuid } from './identifiers'
import { technicalApi, type PmsCommandCenter, type PmsItem, type PmsSection } from './technical-records-api'
import { label, technicalCopy, type TechnicalCopy } from './DefectLifecyclePanel'
import './technical-records.css'

// DEV-056 / T-S8-005 PMS Command Center (18.01 row 57; 59.04 pms-command-center). Advisory exception
// view: healthy items stay quiet (counted), unknown/forbidden sources are UNKNOWN/ACCESS_REQUIRED and
// never zero, stale data is labelled, and every listed item carries its "Why" chain.
export function readPmsRoute(pathname: string): { vessel: string | null; invalid: boolean } {
  const match = /^\/app\/pms-command-center(?:\/([0-9a-fA-F-]{36}))?\/?$/.exec(pathname)
  if (!match) return { vessel: null, invalid: true }
  const vessel = match[1]?.toLowerCase() ?? null
  return { vessel: vessel && isGuid(vessel) ? vessel : null, invalid: !!match[1] && !isGuid(match[1].toLowerCase()) }
}
export const sourceHref = (item: PmsItem, vessel: string) => item.kind === 'Defect' ? `/app/defects/${vessel}?id=${item.sourceId}` :
  item.kind === 'MaintenancePlan' ? `/app/maintenance-plan/${vessel}?id=${item.sourceId}` : item.kind === 'WorkOrder' ? `/app/work-order-center/${vessel}` : null

export function PmsCommandCenterWorkspace() {
  const [language, setLanguage] = useAppLanguage()
  const [route, setRoute] = useState(() => readPmsRoute(window.location.pathname))
  useEffect(() => { const pop = () => setRoute(readPmsRoute(window.location.pathname)); window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop) }, [])
  const navigate = useCallback((target: string) => {
    if (target !== '/app' && !target.startsWith('/app/') || /[\\\p{Cc}]/u.test(target)) return
    if (/^\/app\/pms-command-center(?:\/|$)/.test(target)) { window.history.pushState(null, '', target); setRoute(readPmsRoute(window.location.pathname)) }
    else window.location.assign(target)
  }, [])
  return <CommandCenterSurface key={route.vessel ?? 'fleet'} vessel={route.vessel} invalid={route.invalid} language={language} onLanguage={setLanguage} onNavigate={navigate}/>
}

type Loaded = { user: Session; center?: PmsCommandCenter; vessels?: Vessel[] }
function CommandCenterSurface({ vessel, invalid, language, onLanguage, onNavigate }: { vessel: string | null; invalid: boolean; language: AppLocale;
  onLanguage: (value: AppLocale) => void; onNavigate: (href: string) => void }) {
  const t = technicalCopy[language]
  const title = localizedText(language, 'pairs.m_8c3e1f0a5d2b7c41')
  const [loaded, setLoaded] = useState<Loaded | null>(null), [failed, setFailed] = useState<ApiError | null>(null), [attempt, setAttempt] = useState(0)
  const [offline, setOffline] = useState(typeof navigator !== 'undefined' && navigator.onLine === false)
  useEffect(() => { const on = () => setOffline(false), off = () => setOffline(true); window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) } }, [])
  useEffect(() => {
    const controller = new AbortController()
    ;(async () => {
      const user = await readSession(controller.signal)
      if (!vessel) return { user, vessels: (await readVessels(0, '', controller.signal)).items } as Loaded
      return { user, center: await technicalApi.commandCenter(vessel, controller.signal) } as Loaded
    })().then(value => { if (!controller.signal.aborted) { setFailed(null); setLoaded(value) } })
      .catch(error => { if (!controller.signal.aborted) setFailed(error instanceof ApiError ? error : new ApiError(0, 'network')) })
    return () => controller.abort()
  }, [vessel, attempt])
  const center = loaded?.center
  return <div className="product-shell enterprise-shell technical-workspace">
    <EnterpriseChrome session={loaded?.user ?? null} vessel={vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="pms-main"/>
    <main id="pms-main" tabIndex={-1} data-testid="pms-command-center">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span className="technical-note">{t.ccNotice}</span>}
        actions={vessel ? <Button variant="outline" disabled={offline} onClick={() => setAttempt(n => n + 1)}>{t.refresh}</Button> : undefined}/>
      {offline && <div className="technical-banner" role="status"><p>{t.offlineStale}</p></div>}
      {invalid ? <p role="alert">{t.invalidRoute}</p>
        : failed ? <div role="alert" className="technical-banner"><p>{failed.status === 403 ? t.forbidden : failed.status === 0 ? t.offline : t.error}</p>
            <Button variant="outline" onClick={() => setAttempt(n => n + 1)}>{t.reload}</Button></div>
        : !loaded ? <p role="status">{t.loading}</p>
        : !vessel ? <section className="technical-panel"><p>{t.chooseVessel}</p>{!loaded.vessels?.length ? <p role="status">{t.noVessels}</p> :
            <ul className="technical-list">{loaded.vessels.map(v => <li key={v.id}><Button variant="link" onClick={() => onNavigate('/app/pms-command-center/' + v.id)}>{v.code} · {v.name}</Button></li>)}</ul>}</section>
        : center && <>
            <section className="technical-panel" aria-label={t.readiness}>
              <div className="technical-status"><strong>{t.readiness}:</strong>
                <Badge tone={center.readinessState === 'BLOCKED' ? 'danger' : center.readinessState === 'NO_OPEN_EXCEPTIONS' ? 'neutral' : 'warning'} data-state={center.readinessState}>
                  {label(t, 'rs_', center.readinessState)}</Badge>
                <span>{t.lastRefresh}: <time dateTime={center.lastRefreshAt}>{center.lastRefreshAt}</time></span>
              </div>
            </section>
            <div className="technical-sections">{center.sections.map(s => <CommandSection key={s.key} section={s} vessel={vessel} t={t} onNavigate={onNavigate}/>)}</div>
          </>}
    </main>
  </div>
}

function CommandSection({ section, vessel, t, onNavigate }: { section: PmsSection; vessel: string; t: TechnicalCopy; onNavigate: (href: string) => void }) {
  const unknown = section.state === 'UNKNOWN' || section.state === 'ACCESS_REQUIRED'
  return <section className="technical-panel technical-section" data-state={section.state} data-section={section.key} aria-label={label(t, 'sec_', section.key)}>
    <h2>{label(t, 'sec_', section.key)} {!unknown && <span>({section.count})</span>}</h2>
    {unknown ? <p role="status">{label(t, 'sst_', section.state)}{section.reason && <> · <code>{section.reason}</code></>}</p>
      : section.state === 'QUIET' ? <p>{t.sst_QUIET}</p> : null}
    {section.freshness === 'STALE' && <p className="technical-banner" role="status">{t.fr_STALE}</p>}
    {!unknown && section.quietCount > 0 && <p className="technical-note">{t.quiet.replace('{{count}}', String(section.quietCount))}</p>}
    {section.items.length > 0 && <ul className="technical-list">{section.items.map(item => {
      const target = sourceHref(item, vessel)
      return <li key={item.kind + item.sourceId}>
        <div className="technical-status"><Badge tone={item.severity === 'CRITICAL' ? 'danger' : 'warning'} data-severity={item.severity}>{label(t, 'sv_', item.severity)}</Badge>
          <span>{item.kind} r{item.sourceRevision ?? '?'}</span>
          {item.freshness !== 'CURRENT' && <Badge tone="neutral" data-freshness={item.freshness}>{label(t, 'fr_', item.freshness)}</Badge>}</div>
        <details><summary>{t.why}</summary><ul>{item.why.map((w, i) => <li key={w.code + i}><code>{w.code}</code>{w.detail && <> · {w.detail}</>}</li>)}</ul>
          {item.observedAt && <p>{t.observedAt}: <time dateTime={item.observedAt}>{item.observedAt}</time></p>}</details>
        {target && <Button variant="link" onClick={() => onNavigate(target)}>{t.openSource}</Button>}
      </li>
    })}</ul>}
  </section>
}
