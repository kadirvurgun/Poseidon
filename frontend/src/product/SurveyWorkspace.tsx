import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterpriseField, EnterpriseFormGrid, EnterprisePageHeader, EnterpriseToolbar } from './EnterprisePatterns'
import * as api from './api'
import { isGuid } from './identifiers'
import { decodeTodayVessels } from './today-api'
import { certificatesApi, type CertificateDto } from './certificates-api'
import { unsavedState, useUnsavedChangesSource } from './unsaved-changes'
import { Codes, FitBadge, PreSurveyChecklistPanel, ReadinessBadge, WindowBadge, surveyCopy, type SurveyCopy } from './PreSurveyChecklistPanel'
import { surveyApi, type Planner, type PreSurveyCapabilities, type PreSurveyChecklist, type PreSurveyPage } from './survey-api'
import './certificates-enterprise.css'
import './product.css'

// DEV-104 / DEV-112 workspace. Presentation only: the server authorizes every read and write and
// computes window, readiness and fit. Nothing here renders a ready/fit/green state.
export type SurveyRoute = { page: 'planner' | 'checklist'; vessel: string | null; id: string | null; certificateId: string | null; invalid: boolean }
export function readSurveyLocation(pathname: string, search: string): SurveyRoute {
  const planner = /^\/app\/survey-planner\/?$/.exec(pathname), checklist = /^\/app\/pre-survey-checklist(?:\/([0-9a-f-]{36}))?\/?$/.exec(pathname)
  const params = new URLSearchParams(search), id = params.get('id'), certificateId = params.get('certificateId')
  if (planner) return { page: 'planner', vessel: null, id: null, certificateId: null, invalid: [...params.keys()].length > 0 }
  if (!checklist) return { page: 'checklist', vessel: null, id: null, certificateId: null, invalid: true }
  const vessel = checklist[1] ?? null
  return { page: 'checklist', vessel, id, certificateId, invalid: !!vessel && !isGuid(vessel) || id !== null && (!isGuid(id) || !vessel) || certificateId !== null && (!isGuid(certificateId) || !vessel) }
}
export const checklistHref = (vessel: string | null, id?: string | null, certificateId?: string | null) => {
  const params = new URLSearchParams(); if (id) params.set('id', id); if (certificateId) params.set('certificateId', certificateId)
  return `/app/pre-survey-checklist${vessel ? `/${vessel}` : ''}${params.size ? `?${params}` : ''}`
}
function useOnline() {
  const [online, setOnline] = useState(() => navigator.onLine)
  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update); window.addEventListener('offline', update)
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update) }
  }, [])
  return online
}
const isoDay = (d: Date) => d.toISOString().slice(0, 10)
const addDays = (days: number) => { const d = new Date(); d.setUTCDate(d.getUTCDate() + days); return isoDay(d) }

export function SurveyWorkspace() {
  const [language, setLanguage] = useAppLanguage(), t = surveyCopy[language], online = useOnline()
  const [route, setRoute] = useState<SurveyRoute>(() => readSurveyLocation(window.location.pathname, window.location.search))
  const [identity, setIdentity] = useState<{ user?: api.Session; error?: boolean } | null>(null), [attempt, retry] = useState(0)
  const dirty = useRef(false)
  useUnsavedChangesSource(() => unsavedState(dirty.current, false))
  const onDirty = useCallback((value: boolean) => { dirty.current = value }, [])
  useEffect(() => {
    if (route.invalid || !online) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    api.session(c.signal).then(user => { if (active) setIdentity({ user }) }).catch(() => { if (active) setIdentity({ error: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [route.invalid, online, attempt])
  const title = route.page === 'planner' ? t.plannerTitle : t.checklistTitle
  useEffect(() => { document.title = `POSEIDON · ${title}` }, [title])
  useEffect(() => {
    const pop = () => setRoute(readSurveyLocation(window.location.pathname, window.location.search))
    window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop)
  }, [])
  const go = (href: string) => {
    if (dirty.current && !window.confirm(t.discard)) return
    dirty.current = false
    // Persist the canonical resource identity in the URL before any optional read.
    history.pushState(null, '', href); const [p, q = ''] = href.split('?'); setRoute(readSurveyLocation(p, q ? '?' + q : ''))
  }
  const user = identity?.user ?? null, allowed = !!user?.authenticated && !!user.canRead
  return <div className="product-shell enterprise-shell"><EnterpriseChrome session={user} vessel={route.vessel} language={language} onLanguageChange={setLanguage}
      onNavigate={(href, committed) => { committed?.(); window.location.assign(href) }} title={title} mainId="survey-main"/>
    <main id="survey-main" tabIndex={-1} data-testid={route.page === 'planner' ? 'survey-planner' : 'pre-survey-checklist'}>
      <EnterprisePageHeader title={title} description={route.page === 'planner' ? t.plannerDescription : t.checklistDescription}
        actions={<Button variant="outline" disabled={!online} onClick={() => retry(n => n + 1)}>{t.retry}</Button>}/>
      {!online && <p role="status" data-state="offline">{t.offline}</p>}
      {route.invalid ? <p role="alert">{t.notFound}</p> : !identity ? (online ? <p role="status">{t.loading}</p> : null) : identity.error ? <p role="alert">{t.loadError}</p> : !allowed ? <p role="alert">{t.denied}</p> :
        route.page === 'planner' ? <PlannerView key={`${user?.tenantId}:${user?.actorId}:${attempt}`} online={online} t={t} onOpen={go}/> : <>
          <VesselPicker vessel={route.vessel} online={online} t={t} onSelect={vessel => go(checklistHref(vessel))}/>
          {route.vessel ? route.id
            ? <ChecklistDetail key={`${user?.actorId}:${route.vessel}:${route.id}:${attempt}`} vessel={route.vessel} id={route.id} online={online} t={t} language={language} onDirty={onDirty} onBack={() => go(checklistHref(route.vessel))}/>
            : <ChecklistList key={`${user?.actorId}:${route.vessel}:${attempt}`} vessel={route.vessel} certificateId={route.certificateId} online={online} t={t} onOpen={id => go(checklistHref(route.vessel, id))}/>
            : <p role="status">{t.chooseVessel}</p>}
        </>}
    </main></div>
}

function VesselPicker({ vessel, online, t, onSelect }: { vessel: string | null; online: boolean; t: SurveyCopy; onSelect: (id: string) => void }) {
  const [rows, setRows] = useState<api.Vessel[] | null>(null), id = useId()
  useEffect(() => {
    if (!online) return
    const c = new AbortController(); let active = true
    api.vessels(0, '', c.signal).then(v => decodeTodayVessels(v, 0)).then(v => { if (active) setRows(v.items) }).catch(() => { if (active) setRows([]) })
    return () => { active = false; c.abort() }
  }, [online])
  return <EnterpriseToolbar><label htmlFor={id}>{t.vessel}</label><select id={id} value={vessel ?? ''} disabled={!online} onChange={e => { if (e.target.value) onSelect(e.target.value) }}>
    <option value="">{t.chooseVessel}</option>
    {vessel && !rows?.some(r => r.id.toLowerCase() === vessel) && <option value={vessel}>{vessel}</option>}
    {rows?.map(r => <option key={r.id} value={r.id.toLowerCase()}>{r.code} · {r.name}</option>)}
  </select></EnterpriseToolbar>
}

type Loaded<T> = { scope: string; data?: T; error?: number }
// Results are keyed by their request scope, so a stale result never renders for new inputs.
function useLoad<T>(load: (signal: AbortSignal) => Promise<T>, online: boolean, deps: unknown[]) {
  const scope = JSON.stringify(deps)
  const [result, setResult] = useState<Loaded<T> | null>(null)
  useEffect(() => {
    if (!online) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 15000)
    load(c.signal).then(data => { if (active) setResult({ scope, data }) }).catch(e => { if (active) setResult({ scope, error: e instanceof api.ApiError ? e.status : 0 }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [online, scope])
  return result?.scope === scope ? result : null
}
const failureText = (t: SurveyCopy, status?: number) => status === 403 ? t.denied : status === 404 ? t.notFound : t.loadError

export function PlannerView({ online, t, onOpen }: { online: boolean; t: SurveyCopy; onOpen: (href: string) => void }) {
  const id = useId(), [range, setRange] = useState(() => ({ from: addDays(-30), to: addDays(365) })), [draft, setDraft] = useState(range)
  const result = useLoad<Planner>(signal => surveyApi.planner(range, signal), online, [range.from, range.to])
  const valid = /^\d{4}-\d{2}-\d{2}$/.test(draft.from) && /^\d{4}-\d{2}-\d{2}$/.test(draft.to) && draft.to >= draft.from
  return <>
    <form noValidate aria-label={t.range} onSubmit={e => { e.preventDefault(); if (valid) setRange(draft) }}>
      <EnterpriseFormGrid disabled={!online}><legend>{t.range}</legend>
        <EnterpriseField htmlFor={`${id}-from`} label={t.from}><input id={`${id}-from`} type="date" value={draft.from} onChange={e => setDraft(d => ({ ...d, from: e.target.value }))}/></EnterpriseField>
        <EnterpriseField htmlFor={`${id}-to`} label={t.to}><input id={`${id}-to`} type="date" value={draft.to} onChange={e => setDraft(d => ({ ...d, to: e.target.value }))}/></EnterpriseField>
      </EnterpriseFormGrid>
      <EnterpriseActions align="start" aria-label={t.range}><Button type="submit" disabled={!online || !valid}>{t.apply}</Button></EnterpriseActions>
    </form>
    <p className="certificate-field-hint">{t.coordination}</p>
    {!result ? (online ? <p role="status">{t.loading}</p> : null) : !result.data ? <p role="alert">{failureText(t, result.error)}</p> : <PlannerCalendar planner={result.data} t={t} onOpen={onOpen}/>}
  </>
}
function PlannerCalendar({ planner, t, onOpen }: { planner: Planner; t: SurveyCopy; onOpen: (href: string) => void }) {
  if (planner.items.length === 0) return <p role="status" data-state="empty">{t.empty}</p>
  // Month buckets of the declared window start; undeclared windows are a separate UNKNOWN group.
  const groups = new Map<string, Planner['items']>()
  for (const row of planner.items) { const key = row.window.start ? row.window.start.slice(0, 7) : ''; groups.set(key, [...(groups.get(key) ?? []), row]) }
  const c = planner.counts
  return <section aria-label={t.plannerTitle}>
    <p role="status">{t.counts.replace('{{open}}', String(c.windowOpen)).replace('{{upcoming}}', String(c.windowUpcoming)).replace('{{overdue}}', String(c.windowOverdue)).replace('{{unknown}}', String(c.windowUnknown))}</p>
    {(planner.truncated || planner.vesselsTruncated) && <p role="note">{t.truncated}</p>}
    {[...groups.entries()].map(([month, rows]) => <section key={month || 'unknown'} aria-label={month || t.unknownMonth} className="survey-month">
      <h2><time dateTime={month || undefined}>{month || t.unknownMonth}</time></h2>
      <ul className="survey-rows">{rows.map(row => <li key={`${row.vesselId}/${row.certificateId}`} data-window={row.window.state} data-fit={row.fit.state}>
        <h3>{row.vesselCode} · {row.vesselName} — {row.certificateType ?? t.certificate} {row.certificateNumber && <bdi dir="ltr">{row.certificateNumber}</bdi>}</h3>
        <dl className="survey-summary">
          <div><dt>{t.window}</dt><dd><WindowBadge state={row.window.state} t={t}/> {row.window.start ?? '—'} → {row.window.end ?? '—'}<Codes codes={row.window.qualifiers}/></dd></div>
          <div><dt>{t.expiry}</dt><dd>{row.expiryDate ?? '—'}</dd></div>
          <div><dt>{t.fit}</dt><dd><FitBadge state={row.fit.state} t={t}/><Codes codes={row.fit.reasons}/></dd></div>
          <div><dt>{t.issuer}</dt><dd>{row.issuer ?? '—'}</dd></div>
          <div><dt>{t.flag}</dt><dd>{row.declaredFlag ?? '—'}</dd></div>
          <div><dt>{t.basis}</dt><dd>{row.regulatoryBasis ?? '—'}</dd></div>
          <div><dt>{t.checklist}</dt><dd>{row.checklist ? <><ReadinessBadge state={row.checklist.readiness} t={t}/> <Button variant="outline" onClick={() => onOpen(row.checklist!.href)}>{t.openChecklist}</Button></>
            : <><span>{t.noChecklist}</span> <Button variant="outline" onClick={() => onOpen(checklistHref(row.vesselId, null, row.certificateId))}>{t.create}</Button></>}</dd></div>
        </dl>
        <a href={row.href}>{t.openCertificate}</a>
      </li>)}</ul>
    </section>)}
  </section>
}

function ChecklistList({ vessel, certificateId, online, t, onOpen }: { vessel: string; certificateId: string | null; online: boolean; t: SurveyCopy; onOpen: (id: string) => void }) {
  const result = useLoad<{ page: PreSurveyPage; capabilities: PreSurveyCapabilities }>(async signal => ({ page: await surveyApi.list(vessel, 0, signal), capabilities: await surveyApi.capabilities(vessel, signal) }), online, [vessel])
  if (!result) return online ? <p role="status">{t.loading}</p> : null
  if (!result.data) return <p role="alert">{failureText(t, result.error)}</p>
  const { page, capabilities } = result.data
  return <>
    <section aria-labelledby="survey-list"><h2 id="survey-list">{t.list}</h2>
      {page.items.length === 0 ? <p role="status" data-state="empty">{t.noChecklists}</p> : <ul className="survey-rows">{page.items.map(item => <li key={item.id}>
        <Button variant="outline" onClick={() => onOpen(item.id)}><code>{item.templateCode}</code> · r{item.revision}</Button> <ReadinessBadge state={item.readiness.state} t={t}/> <FitBadge state={item.voyageFit.state} t={t}/>
      </li>)}</ul>}
    </section>
    {capabilities.canWrite && <CreateChecklist vessel={vessel} certificateId={certificateId} capabilities={capabilities} online={online} t={t} onCreated={onOpen}/>}
  </>
}

function CreateChecklist({ vessel, certificateId, capabilities, online, t, onCreated }: { vessel: string; certificateId: string | null; capabilities: PreSurveyCapabilities; online: boolean; t: SurveyCopy; onCreated: (id: string) => void }) {
  const id = useId()
  const certificates = useLoad<CertificateDto[]>(async signal => (await certificatesApi.center(vessel, 'Vessel', 0, null, null, signal)).items.filter(c => c.state !== 'SUPERSEDED'), online, [vessel])
  const [certificate, setCertificate] = useState(certificateId ?? ''), [template, setTemplate] = useState(capabilities.templates[0]?.code ?? '')
  const [pending, setPending] = useState<{ key: string; body: string; id: string } | null>(null), [busy, setBusy] = useState(false), [failure, setFailure] = useState<api.ApiError | null>(null)
  const chosen = certificates?.data?.find(c => c.id === certificate)
  async function submit() {
    if (busy || !online || !chosen && !pending) return
    setBusy(true); setFailure(null)
    const c = new AbortController(); const timer = setTimeout(() => c.abort(), 20000)
    let attempt = pending, sent = false
    try {
      if (!attempt) {
        const newId = crypto.randomUUID()
        attempt = { key: crypto.randomUUID(), id: newId, body: JSON.stringify({ id: newId, expectedVersion: 0, certificateId: chosen!.id, certificateRevision: chosen!.revision, certificateVersion: chosen!.version, templateCode: template,
          plan: { plannedStart: null, plannedEnd: null, portCallId: null, portCallRevision: null, locationText: null } }) }
      }
      const fresh = await api.session(c.signal)
      setPending(attempt); sent = true
      const receipt = await surveyApi.create({ key: attempt.key, id: attempt.id, vessel, body: attempt.body }, fresh.csrfToken, c.signal)
      setPending(null); onCreated(receipt.id)
    } catch (error) {
      const problem = error instanceof api.ApiError ? error : new api.ApiError(0)
      setFailure(problem); if (!sent || problem.status > 0 && problem.status < 500) setPending(null)
    } finally { clearTimeout(timer); setBusy(false) }
  }
  return <form noValidate aria-label={t.create} onSubmit={e => { e.preventDefault(); void submit() }}>
    <EnterpriseFormGrid disabled={!online || busy || !!pending}><legend>{t.create}</legend>
      <EnterpriseField htmlFor={`${id}-certificate`} label={t.certificate}><select id={`${id}-certificate`} value={certificate} onChange={e => setCertificate(e.target.value)}>
        <option value="">{t.chooseCertificate}</option>
        {certificates?.data?.map(c => <option key={c.id} value={c.id}>{c.content.documentName ?? c.content.metadata.declaration.certificateTypeReference ?? c.id.slice(0, 8)} · r{c.revision}</option>)}
      </select></EnterpriseField>
      <EnterpriseField htmlFor={`${id}-template`} label={t.template}><select id={`${id}-template`} value={template} onChange={e => setTemplate(e.target.value)}>
        {capabilities.templates.map(x => <option key={x.code} value={x.code}>{x.code} · {x.surveyType}</option>)}
      </select></EnterpriseField>
    </EnterpriseFormGrid>
    <p className="certificate-field-hint">{t.templateStatus}</p>
    {certificates && !certificates.data && <p role="alert">{failureText(t, certificates.error)}</p>}
    {failure && <p role="alert">{pending ? t.unknownOutcome : t.failed}{failure.code && <> · <code>{failure.code}</code></>}</p>}
    <EnterpriseActions align="start" aria-label={t.create}><Button type="submit" disabled={!online || busy || !pending && (!chosen || !template)}>{pending ? t.retrySame : t.startCreate}</Button></EnterpriseActions>
  </form>
}

function ChecklistDetail({ vessel, id, online, t, language, onDirty, onBack }: { vessel: string; id: string; online: boolean; t: SurveyCopy; language: AppLocale; onDirty: (value: boolean) => void; onBack: () => void }) {
  const [reload, setReload] = useState(0)
  // A receipt is immutable and read-only; editability returns only with a current owning GET.
  const result = useLoad<{ record: PreSurveyChecklist; capabilities: PreSurveyCapabilities }>(async signal => ({ record: await surveyApi.read(vessel, id, signal), capabilities: await surveyApi.capabilities(vessel, signal) }), online, [vessel, id, reload])
  if (!result) return online ? <p role="status">{t.loading}</p> : null
  if (!result.data) return <><p role="alert">{failureText(t, result.error)}</p><Button variant="outline" onClick={onBack}>{t.back}</Button></>
  const { record, capabilities } = result.data
  return <>
    <EnterpriseActions align="start" aria-label={t.back}><Button variant="outline" onClick={onBack}>{t.back}</Button></EnterpriseActions>
    <PreSurveyChecklistPanel key={`${record.id}/${record.revision}`} vessel={vessel} record={record} capabilities={capabilities} language={language} onDirty={onDirty}
      onApplied={() => setReload(n => n + 1)} onReload={() => setReload(n => n + 1)}/>
  </>
}
