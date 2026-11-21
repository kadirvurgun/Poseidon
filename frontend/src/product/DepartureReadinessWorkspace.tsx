import { useCallback, useEffect, useRef, useState } from 'react'
import { pilotJourneyAttempt, pilotJourneyCompleted } from './pilot-telemetry'
import { localizedCopy, localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterprisePageHeader } from './EnterprisePatterns'
import { ApiError, session as readSession, vessels as readVessels, type Session, type Vessel } from './api'
import { isGuid } from './identifiers'
import { send, type Command } from './tug-readiness-api'
import { departureApi, type DepartureDecisionCode, type DepartureReadiness, type DepartureResult, type DepartureState } from './departure-readiness-api'

// S9 WP-A DEV-058 departure readiness (crew/technical/statutory/emergency/safety + "Why" list) and DEV-059 Master sign-off.
// UNKNOWN/REVIEW_REQUIRED is never shown as ready; READY is informational (no green "go" tone). A sign-off is the
// appointed Master's human record bound to the exact assessment; it is not a departure clearance.
const copy = localizedCopy('DepartureReadiness_tsx_copy')
type Copy = (typeof copy)[AppLocale]
type Notice = '' | 'done' | 'uncertain' | 'conflict' | 'stale' | 'blocked' | 'denied' | 'failed'
const word = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key

export type DepartureRoute = { vessel: string | null; from: string; to: string; invalid: boolean }
const day = (v: string) => /^\d{4}-\d{2}-\d{2}$/.test(v) && !Number.isNaN(Date.parse(v + 'T00:00:00Z'))
export function readDepartureRoute(href: string): DepartureRoute {
  const url = new URL(href, 'http://local'), params = url.searchParams
  const match = /^\/app\/departure-signoff(?:\/([0-9a-fA-F-]{36}))?\/?$/.exec(url.pathname)
  const vessel = match?.[1]?.toLowerCase() ?? null
  const from = params.get('from') ?? '', to = params.get('to') ?? ''
  const invalid = !match || !!match[1] && !isGuid(vessel) || [...params.keys()].some(k => !['from', 'to'].includes(k) || params.getAll(k).length !== 1) ||
    (from !== '' && !day(from)) || (to !== '' && !day(to)) || (from !== '') !== (to !== '') || from !== '' && to < from
  return { vessel: vessel && isGuid(vessel) ? vessel : null, from, to, invalid }
}
export const departureHref = (vessel: string, from?: string, to?: string) => `/app/departure-signoff/${vessel}${from && to ? `?from=${from}&to=${to}` : ''}`

export function DepartureStateBadge({ state, t }: { state: DepartureState; t: Copy }) {
  return <Badge tone={state === 'NOT_READY' ? 'danger' : state === 'READY' ? 'info' : 'warning'} data-readiness={state}>{word(t, state)}</Badge>
}
function ResultBadge({ result, t }: { result: DepartureResult; t: Copy }) {
  return <Badge tone={result === 'FAIL' ? 'danger' : result === 'PASS' ? 'neutral' : 'warning'} data-result={result}>{word(t, 'result_' + result)}</Badge>
}

export function DepartureReadinessWorkspace() {
  const [lang, setLanguage] = useAppLanguage(), t = copy[lang]
  const title = localizedText(lang, 'pairs.m_9d2a4c6e8f0b1d35')
  const [route, setRoute] = useState(() => readDepartureRoute(window.location.href))
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(true), [error, setError] = useState<ApiError | null>(null), [offline, setOffline] = useState(!navigator.onLine)
  const [fleet, setFleet] = useState<Vessel[] | null>(null), [data, setData] = useState<DepartureReadiness | null>(null)
  const [notice, setNotice] = useState<Notice>(''), [busy, setBusy] = useState(false), [retained, setRetained] = useState<Command | null>(null)
  const [now, setNow] = useState(() => Date.now())
  const lock = useRef(false), mounted = useRef(true)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true), pop = () => { setRoute(readDepartureRoute(window.location.href)); setReload(n => n + 1) }
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop)
    const tick = setInterval(() => setNow(Date.now()), 5000)
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop); clearInterval(tick) }
  }, [])
  const move = useCallback((href: string) => {
    if (retained || busy) return
    if (!/^\/app\/departure-signoff(?:\/|\?|$)/.test(href)) { window.location.assign(href); return }
    history.pushState(null, '', href); setRoute(readDepartureRoute(window.location.href)); setNotice(''); setReload(n => n + 1)
  }, [retained, busy])
  useEffect(() => {
    const c = new AbortController()
    ;(async () => {
      await Promise.resolve()
      if (c.signal.aborted) return
      setLoading(true); setError(null); setData(null); setFleet(null)
      if (route.invalid) throw new ApiError(400, 'invalid_route')
      const s = await readSession(c.signal); setUser(s)
      if (!route.vessel) { setFleet((await readVessels(0, 'Ship', c.signal)).items); return }
      if (route.from && route.to) setData(await departureApi.readiness(route.vessel, route.from, route.to, c.signal))
    })().then(() => { if (!c.signal.aborted) { setLoading(false); setNow(Date.now()) } })
      .catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e : new ApiError(0, 'network')); setLoading(false) })
    return () => c.abort()
  }, [route, reload])

  // One command at a time; an uncertain outcome keeps the exact key/body for an identical retry.
  const run = useCallback(async (command: Command) => {
    if (lock.current || offline || !navigator.onLine) return
    lock.current = true; setBusy(true); setNotice(''); setRetained(command)
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000)
    let sent = false
    try {
      const s = await readSession(c.signal)
      if (!s.authenticated || !s.csrfToken || user && `${s.tenantId}/${s.actorId}` !== `${user.tenantId}/${user.actorId}`) throw new ApiError(403, 'identity_boundary_changed')
      sent = true
      pilotJourneyAttempt('SHIP', 'SHIP_DEPARTURE')
      await send(command, s.csrfToken, c.signal)
      pilotJourneyCompleted('SHIP', 'SHIP_DEPARTURE')
      if (!mounted.current) return
      setRetained(null); setNotice('done'); setReload(n => n + 1)
    } catch (e) {
      if (!mounted.current) return
      const definite = e instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(e.status)
      if (!sent || definite) setRetained(null)
      const code = e instanceof ApiError ? e.code : ''
      setNotice(sent && !definite ? 'uncertain' : code === 'assessment_stale' ? 'stale' : code.startsWith('departure_release_') ? 'blocked'
        : e instanceof ApiError && e.status === 409 ? 'conflict' : e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : 'failed')
    } finally { clearTimeout(timeout); lock.current = false; if (mounted.current) setBusy(false) }
  }, [offline, user])

  const stale = !!data && Date.parse(data.validUntil) <= now
  const disabled = busy || offline || stale || !!retained && notice !== 'uncertain'
  const message = notice ? word(t, 'notice_' + notice) : ''
  return <div className="product-shell enterprise-shell departure-workspace">
    <EnterpriseChrome session={user} vessel={route.vessel} language={lang} onLanguageChange={setLanguage} onNavigate={href => move(href)} title={title} mainId="departure-main"/>
    <main id="departure-main" tabIndex={-1} aria-busy={loading || busy} data-testid="departure-signoff">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span>{t.boundary}</span>}
        actions={route.vessel && data ? <Button variant="outline" disabled={offline || busy} onClick={() => { setNotice(''); setReload(n => n + 1) }}>{t.refresh}</Button> : undefined}/>
      {offline && <p role="alert" data-state="offline">{t.offline}</p>}
      {message && <div role={notice === 'done' ? 'status' : 'alert'} data-notice={notice}><p>{message}</p>
        {notice === 'uncertain' && retained && <Button type="button" disabled={busy || offline} onClick={() => void run(retained)}>{t.retry}</Button>}
        {['conflict', 'stale'].includes(notice) && <Button type="button" variant="outline" onClick={() => { setNotice(''); setReload(n => n + 1) }}>{t.reload}</Button>}</div>}
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error && <div role="alert" data-state={error.status === 403 ? 'forbidden' : 'error'}>
        <p>{error.code === 'invalid_route' ? t.invalidRoute : error.status === 403 ? t.forbidden : error.status === 404 ? t.notFound
          : error.status === 400 ? t.invalidWindow : error.status === 0 ? t.offline : t.error}</p>
        {error.code !== 'invalid_route' && <Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.retry}</Button>}</div>}
      {!loading && !error && fleet && <section aria-label={t.chooseVessel}><h2>{t.chooseVessel}</h2>
        {fleet.length === 0 ? <p role="status" data-state="empty">{t.noVessels}</p> :
          <ul>{fleet.map(v => <li key={v.id}><Button variant="link" onClick={() => move(departureHref(v.id))}>{v.code} · {v.name}</Button></li>)}</ul>}</section>}
      {!loading && !error && route.vessel && <WindowForm key={route.from + route.to} vessel={route.vessel} from={route.from} to={route.to} t={t} disabled={busy} onSubmit={move}/>}
      {!loading && !error && route.vessel && !data && <p role="status" data-state="empty">{t.chooseWindow}</p>}
      {!loading && !error && data && <ReadinessPanel data={data} vessel={route.vessel!} t={t} stale={stale} disabled={disabled} run={run}/>}
    </main>
  </div>
}

function WindowForm({ vessel, from, to, t, disabled, onSubmit }: { vessel: string; from: string; to: string; t: Copy; disabled: boolean; onSubmit: (href: string) => void }) {
  const today = new Date().toISOString().slice(0, 10)
  const [start, setStart] = useState(from || today), [end, setEnd] = useState(to || today)
  const valid = day(start) && day(end) && end >= start
  return <form aria-label={t.window} onSubmit={e => { e.preventDefault(); if (valid) onSubmit(departureHref(vessel, start, end)) }}>
    <fieldset><legend>{t.window}</legend>
      <label htmlFor="departure-from">{t.from}</label><input id="departure-from" type="date" required value={start} onChange={e => setStart(e.target.value)}/>
      <label htmlFor="departure-to">{t.to}</label><input id="departure-to" type="date" required min={start} value={end} onChange={e => setEnd(e.target.value)}/>
    </fieldset>
    {!valid && <p role="alert">{t.invalidWindow}</p>}
    <EnterpriseActions layout="form" aria-label={t.window}><Button type="submit" disabled={disabled || !valid}>{t.assess}</Button></EnterpriseActions>
  </form>
}

function ReadinessPanel({ data, vessel, t, stale, disabled, run }: { data: DepartureReadiness; vessel: string; t: Copy; stale: boolean; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const [decision, setDecision] = useState<DepartureDecisionCode | ''>(''), [reason, setReason] = useState('')
  const [authority, setAuthority] = useState(data.authorities.length === 1 ? data.authorities[0].id : '')
  const releaseAllowed = data.allowedDecisions.includes('RELEASE')
  const reasonOk = reason.trim().length >= 10 && reason.trim().length <= 2000
  return <section aria-label={t.assessment}>
    <p>{t.assessment}: <DepartureStateBadge state={data.state} t={t}/> <Badge tone="neutral" data-mode={data.mode}>{word(t, 'mode_' + data.mode)}</Badge> <small>{t.advisory}</small></p>
    {data.mode === 'FORECAST' && <p data-state="forecast">{t.forecastNote}</p>}
    <p>{t.assessedAt}: <time dateTime={data.assessedAt}>{data.assessedAt}</time> · {t.validUntil}: <time dateTime={data.validUntil}>{data.validUntil}</time></p>
    {stale && <p role="alert" data-state="stale">{t.staleAssessment}</p>}
    <table className="departure-categories"><thead><tr><th scope="col">{t.category}</th><th scope="col">{t.result}</th></tr></thead>
      <tbody>{data.categories.map(c => <tr key={c.category} data-category={c.category}><td>{word(t, 'cat_' + c.category)}</td><td><ResultBadge result={c.result} t={t}/></td></tr>)}</tbody></table>
    <h2>{t.why}</h2>
    {data.why.length === 0 ? <p>{t.whyEmpty}</p> :
      <ol className="departure-why" aria-label={t.why}>{data.why.map((w, i) => <li key={`${w.category}-${w.reason}-${w.sourceId ?? ''}-${i}`} data-result={w.result}>
        <ResultBadge result={w.result} t={t}/> {word(t, 'cat_' + w.category)} · <code>{w.reason}</code>
        {w.sourceKind && <> · <span>{w.sourceKind}{w.sourceRevision !== null ? ` r${w.sourceRevision}` : ''}</span></>}</li>)}</ol>}
    <h2>{t.decisions}</h2>
    {data.decisions.length === 0 ? <p data-state="empty">{t.noDecisions}</p> :
      <ul aria-label={t.decisions}>{data.decisions.map(d => <li key={d.id} data-decision={d.decision} data-validity={d.validity}>
        #{d.sequence} · {word(t, 'decision_' + d.decision)} · {word(t, d.assessedState)} · {d.windowFrom}–{d.windowTo} · <Badge tone={d.validity === 'STALE' ? 'warning' : 'neutral'}>{word(t, 'validity_' + d.validity)}</Badge>
        {' '}· {d.reason} · <time dateTime={d.recordedAt}>{d.recordedAt}</time></li>)}</ul>}
    {!data.canDecide ? <p data-state="not-appointed">{t.notAppointed}</p> :
      <form aria-label={t.decide} onSubmit={e => { e.preventDefault(); if (decision && authority && reasonOk && !disabled) void run(departureApi.signoff(vessel, data, decision, authority, reason.trim())) }}>
        <h2>{t.decide}</h2>
        <fieldset><legend>{t.decide}</legend>
          {(['HOLD', 'DENY', 'RELEASE'] as const).map(d => <label key={d}><input type="radio" name="departure-decision" value={d} checked={decision === d}
            disabled={d === 'RELEASE' && !releaseAllowed} aria-describedby={d === 'RELEASE' ? 'departure-release-hint' : undefined} onChange={() => setDecision(d)}/>{word(t, 'decision_' + d)}</label>)}
        </fieldset>
        {!releaseAllowed && <p id="departure-release-hint">{t.releaseBlocked}</p>}
        <label htmlFor="departure-authority">{t.authority}</label>
        <select id="departure-authority" required value={authority} disabled={disabled} onChange={e => setAuthority(e.target.value)}>
          <option value="">—</option>{data.authorities.map(a => <option key={a.id} value={a.id}>{word(t, 'duty_' + a.duty)}</option>)}</select>
        <label htmlFor="departure-reason">{t.reason}</label>
        <textarea id="departure-reason" required minLength={10} maxLength={2000} aria-describedby="departure-reason-hint" value={reason} onChange={e => setReason(e.target.value)}/>
        <p id="departure-reason-hint">{t.reasonHint}</p>
        <EnterpriseActions layout="form" aria-label={t.decide}>
          <Button type="submit" disabled={disabled || !decision || !authority || !reasonOk || decision === 'RELEASE' && !releaseAllowed}>{t.record}</Button></EnterpriseActions>
      </form>}
  </section>
}
