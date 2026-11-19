import { localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { useCallback, useEffect, useState } from 'react'
import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterprisePageHeader } from './EnterprisePatterns'
import { EnterpriseChrome } from './EnterpriseChrome'
import { ApiError, session as readSession, vessels as readVessels, type Session, type Vessel } from './api'
import { isGuid } from './identifiers'
import { assessmentPath, decodeRiskAssessment, emptyAssessment, emptyControl, emptyHazard, failureKind, freezeRiskCommand, riskApi, riskLinkKinds, riskWorkTypes,
  sendRiskCommand, splitList, textOrNull, type RiskApplicableTemplates, type RiskAssessment, type RiskAssessmentContent, type RiskAssessmentPage, type RiskAttempt,
  type RiskCapabilities, type RiskLinkRequest, type RiskMethod, type RiskRating } from './risk-api'
import { RiskBanner, RiskField, RiskRatingResult, RiskReason, RiskState, RiskTriState, reasonValid, riskCopy, type RiskCopy, type RiskIssue } from './RiskWorkspaceParts'
import './risk-workspace.css'

// DEV-105 / T-RISK2-001 risk-assessment screen (18.01 row 106; 50.01 Rev3 §5-6). Server state is
// authoritative: approval, acknowledgement and rating badges come from the owning API only.
export type RiskRoute = { vessel: string | null; id: string | null }
export function readRiskRoute(location: { pathname: string; search: string }): RiskRoute {
  const match = /^\/app\/risk-assessment(?:\/([0-9a-fA-F-]{36}))?\/?$/.exec(location.pathname)
  const id = new URLSearchParams(location.search).get('id')
  const vessel = match?.[1] && isGuid(match[1].toLowerCase()) ? match[1].toLowerCase() : null
  return { vessel, id: vessel && id && isGuid(id.toLowerCase()) ? id.toLowerCase() : null }
}
const href = (route: RiskRoute) => '/app/risk-assessment' + (route.vessel ? '/' + route.vessel + (route.id ? '?id=' + route.id : '') : '')

type Loaded = { user: Session; caps?: RiskCapabilities; page?: RiskAssessmentPage; record?: RiskAssessment; methods: RiskMethod[]; vessels?: Vessel[] }
type Editor = { id: string; assessmentNo: string; content: RiskAssessmentContent; links: RiskLinkRequest[]; record: RiskAssessment | null; template: { id: string; revision: number } | null }

export function RiskAssessmentWorkspace() {
  const [language, setLanguage] = useAppLanguage()
  const [route, setRoute] = useState(() => readRiskRoute(window.location))
  useEffect(() => { const pop = () => setRoute(readRiskRoute(window.location)); window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop) }, [])
  const navigate = useCallback((target: string) => {
    if (target !== '/app' && !target.startsWith('/app/') || /[\\\p{Cc}]/u.test(target)) return
    if (/^\/app\/risk-assessment(?:\/|$|\?)/.test(target)) { window.history.pushState(null, '', target); setRoute(readRiskRoute(window.location)) }
    else window.location.assign(target)
  }, [])
  return <RiskAssessmentSurface key={route.vessel ?? 'fleet'} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate}/>
}

function RiskAssessmentSurface({ route, language, onLanguage, onNavigate }: { route: RiskRoute; language: AppLocale; onLanguage: (value: AppLocale) => void; onNavigate: (href: string) => void }) {
  const t = riskCopy[language]
  const title = localizedText(language, 'pairs.m_2ba46e791f65fb5f')
  const [loaded, setLoaded] = useState<Loaded | null>(null), [failed, setFailed] = useState<ApiError | null>(null), [attempt, setAttempt] = useState(0)
  const [editor, setEditor] = useState<Editor | null>(null), [issue, setIssue] = useState<RiskIssue>(null), [busy, setBusy] = useState(false)
  const [pending, setPending] = useState<RiskAttempt | null>(null), [offline, setOffline] = useState(typeof navigator !== 'undefined' && navigator.onLine === false)
  const [templates, setTemplates] = useState<RiskApplicableTemplates | null>(null)
  useEffect(() => { const on = () => setOffline(false), off = () => setOffline(true); window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) } }, [])
  useEffect(() => {
    const controller = new AbortController()
    ;(async () => {
      const user = await readSession(controller.signal)
      if (!route.vessel) { const list = await readVessels(0, '', controller.signal); return { user, methods: [], vessels: list.items } as Loaded }
      const [caps, page, methods] = await Promise.all([riskApi.capabilities(route.vessel, controller.signal), riskApi.list(route.vessel, 0, controller.signal),
        riskApi.methods(controller.signal).then(m => m.items).catch(() => [] as RiskMethod[])])
      const record = route.id ? await riskApi.read(route.vessel, route.id, controller.signal) : undefined
      return { user, caps, page, methods, record } as Loaded
    })().then(value => { if (!controller.signal.aborted) { setFailed(null); setLoaded(value) } })
      .catch(error => { if (!controller.signal.aborted) setFailed(error instanceof ApiError ? error : new ApiError(0, 'network')) })
    return () => controller.abort()
  }, [route.vessel, route.id, attempt])
  const reload = () => { setFailed(null); setIssue(null); setPending(null); setEditor(null); setAttempt(n => n + 1) }

  const execute = async (frozen: RiskAttempt) => {
    if (!loaded?.user.csrfToken || !route.vessel) return
    setBusy(true); setPending(frozen); setIssue(null)
    try {
      const saved = decodeRiskAssessment(await sendRiskCommand<unknown>(frozen, loaded.user.csrfToken, new AbortController().signal), route.vessel)
      setPending(null); setEditor(null)
      // Receipt confirmed: keep the canonical resource identity in the URL before any re-read.
      onNavigate(href({ vessel: route.vessel, id: saved.id })); setLoaded(current => current ? { ...current, record: saved } : current); setAttempt(n => n + 1)
    } catch (error) {
      const kind = failureKind(error); setIssue(kind)
      // The same key/body is kept only while the outcome is unknown (offline / transport).
      if (kind !== 'offline') setPending(null)
    } finally { setBusy(false) }
  }
  const command = (method: 'POST' | 'PUT', suffix: string, body: unknown) => route.vessel && execute(freezeRiskCommand(method, assessmentPath(route.vessel, suffix), body, route.vessel))

  const record = loaded?.record, caps = loaded?.caps
  const description = <span className="risk-gate-notice">{t.gateNotice}</span>
  return <>
    <EnterpriseChrome session={loaded?.user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="risk-main"/>
    <main id="risk-main" tabIndex={-1} className="risk-workspace">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={description} actions={route.vessel && caps?.canWrite && !editor && !offline ?
        <Button onClick={() => setEditor({ id: crypto.randomUUID(), assessmentNo: '', content: emptyAssessment(), links: [], record: null, template: null })}>{t.newAssessment}</Button> : undefined}/>
      {offline && <RiskBanner issue="offline" t={t}/>}
      {pending && issue === 'offline' && <Button variant="outline" disabled={busy || offline} onClick={() => execute(pending)}>{t.retry}</Button>}
      <RiskBanner issue={issue === 'offline' ? null : issue} t={t} onReload={reload} missing={issue === 'incomplete' ? record?.blockers : undefined}/>
      {failed ? <div role="alert" className="risk-banner risk-banner-error"><p>{failed.status === 0 ? t.offline : t.error}</p><Button variant="outline" onClick={reload}>{t.retry}</Button></div>
        : !loaded ? <p role="status">{t.loading}</p>
        : !route.vessel ? <VesselChooser vessels={loaded.vessels ?? []} t={t} onNavigate={onNavigate}/>
        : editor ? <AssessmentEditor editor={editor} caps={caps!} methods={loaded.methods} t={t} busy={busy || offline} vessel={route.vessel} templates={templates}
            onTemplates={workType => riskApi.applicable(route.vessel!, workType, new AbortController().signal).then(setTemplates).catch(() => setTemplates({ vesselId: route.vessel!, items: [] }))}
            onCancel={() => setEditor(null)} onSave={next => next.record
              ? command('PUT', '/' + next.record.id, { expectedVersion: next.record.version, content: next.content, links: next.links })
              : command('POST', '', next.template ? { id: next.id, assessmentNo: next.assessmentNo, content: null, links: next.links, templateId: next.template.id, templateRevision: next.template.revision }
                : { id: next.id, assessmentNo: next.assessmentNo, content: next.content, links: next.links })}/>
        : <div className="risk-layout">
            <AssessmentList page={loaded.page} t={t} selected={route.id} onOpen={id => onNavigate(href({ vessel: route.vessel, id }))}/>
            {record && <AssessmentDetail record={record} caps={caps!} t={t} busy={busy || offline} onEdit={() => setEditor({ id: record.id, assessmentNo: record.assessmentNo,
              content: structuredClone(record.content), links: record.links.map(l => ({ kind: l.kind, id: l.id })), record, template: null })} onCommand={command}/>}
          </div>}
    </main>
  </>
}

function VesselChooser({ vessels, t, onNavigate }: { vessels: Vessel[]; t: RiskCopy; onNavigate: (href: string) => void }) {
  return <section className="risk-panel"><p>{t.chooseVessel}</p>{vessels.length === 0 ? <p role="status">{t.empty}</p> :
    <ul className="risk-list">{vessels.map(v => <li key={v.id}><Button variant="link" onClick={() => onNavigate('/app/risk-assessment/' + v.id)}>{v.code} · {v.name}</Button></li>)}</ul>}</section>
}

function AssessmentList({ page, t, selected, onOpen }: { page?: RiskAssessmentPage; t: RiskCopy; selected: string | null; onOpen: (id: string) => void }) {
  if (!page || page.items.length === 0) return <section className="risk-panel"><p role="status">{t.empty}</p></section>
  return <section className="risk-panel"><ul className="risk-list">{page.items.map(item => <li key={item.id} aria-current={item.id === selected ? 'true' : undefined}>
    <Button variant="link" onClick={() => onOpen(item.id)}>{item.assessmentNo} · {item.title ?? t.unknown}</Button> <RiskState value={item.state} t={t}/>
    {item.stopWorkRequired === null ? <span className="risk-badge risk-badge-unknown">{t.stopWork}: {t.unknown}</span> : item.stopWorkRequired && <span className="risk-badge risk-badge-open">{t.stopWork}</span>}
  </li>)}</ul></section>
}

function AssessmentDetail({ record, caps, t, busy, onEdit, onCommand }: { record: RiskAssessment; caps: RiskCapabilities; t: RiskCopy; busy: boolean; onEdit: () => void;
  onCommand: (method: 'POST' | 'PUT', suffix: string, body: unknown) => void }) {
  const [reason, setReason] = useState('')
  const c = record.content
  const ackKey = ('ack_' + record.acknowledgementState) as keyof RiskCopy
  return <section className="risk-panel risk-detail" aria-labelledby="risk-detail-title">
    <h2 id="risk-detail-title">{record.assessmentNo} · {c.title ?? t.unknown}</h2>
    <p><RiskState value={record.state} t={t}/> {t.revision} {record.revision}{record.approvedRevision ? ` · ${t.st_APPROVED} ${record.approvedRevision}` : ''}</p>
    <p className="risk-ack" data-state={record.acknowledgementState}>{t[ackKey] as string}</p>
    {!record.method && <p className="risk-note">{t.methodUnconfigured}</p>}
    <dl className="risk-facts">
      <dt>{t.operationContext}</dt><dd>{c.operationContext ?? t.unknown}</dd>
      <dt>{t.workType}</dt><dd>{c.workType ?? t.unknown}</dd>
      <dt>{t.method}</dt><dd>{record.method ? `${record.method.code} r${record.method.revision}` : t.methodMissing}</dd>
      <dt>{t.stopWork}</dt><dd>{c.stopWorkRequired === null ? t.unknown : c.stopWorkRequired ? t.yes : t.no}</dd>
    </dl>
    {c.stopWorkConditions.length > 0 && <ul>{c.stopWorkConditions.map(s => <li key={s.id}>{s.description}</li>)}</ul>}
    <h3>{t.hazards}</h3>
    {c.hazards.length === 0 ? <p>{t.empty}</p> : <ol className="risk-hazards">{c.hazards.map(h => {
      const result = record.results.find(r => r.hazardId === h.id)
      return <li key={h.id}><strong>{h.description}</strong>{h.step && <span> · {h.step}</span>}
        <p>{t.consequences}: {h.consequences.join('; ') || t.unknown}</p>
        <p>{t.initialRisk}: {result ? <RiskRatingResult result={result.initial} t={t}/> : t.notAssessed} · {t.residualRisk}: {result ? <RiskRatingResult result={result.residual} t={t}/> : t.notAssessed}</p>
        <ul>{h.controls.map(x => <li key={x.id}>{x.description} · {t.controlOwner}: {x.owner ?? t.unknown}{x.required ? ` · ${t.required}` : ''}</li>)}</ul>
        {h.barrierRefs.length > 0 && <p>{t.barriers}: {h.barrierRefs.join(', ')}</p>}
      </li>
    })}</ol>}
    {record.links.length > 0 && <><h3>{t.links}</h3><ul>{record.links.map(l => <li key={l.kind + l.id} data-currency={l.currency}>{l.kind} · {l.id} · r{l.revision}
      {l.currency === 'STALE' && <span className="risk-badge risk-badge-unknown">{t.linkStale}</span>}{l.currency === 'UNAVAILABLE' && <span className="risk-badge risk-badge-unknown">{t.linkUnavailable}</span>}</li>)}</ul></>}
    {record.blockers.length > 0 && record.state !== 'APPROVED' && <><h3>{t.blockers}</h3><ul>{record.blockers.map(b => <li key={b}><code>{b}</code></li>)}</ul></>}
    <EnterpriseActions aria-label={t.eyebrow}>
      {record.canEdit && <Button variant="outline" disabled={busy} onClick={onEdit}>{t.edit}</Button>}
      {record.canSubmit && <Button disabled={busy} onClick={() => onCommand('POST', `/${record.id}/submit`, { expectedVersion: record.version, contentHash: record.hash })}>{t.submit}</Button>}
    </EnterpriseActions>
    {(record.canReview || record.canAmend || record.canAcknowledge) && <div className="risk-decision">
      <RiskReason value={reason} onChange={setReason} t={t}/>
      <EnterpriseActions>
        {record.canReview && caps.canApprove && <><Button disabled={busy || !reasonValid(reason)} onClick={() => onCommand('POST', `/${record.id}/review`, { expectedVersion: record.version, contentHash: record.hash, decision: 'APPROVE', reason })}>{t.approve}</Button>
          <Button variant="outline" disabled={busy || !reasonValid(reason)} onClick={() => onCommand('POST', `/${record.id}/review`, { expectedVersion: record.version, contentHash: record.hash, decision: 'RETURN', reason })}>{t.returnAction}</Button></>}
        {record.canAmend && <Button variant="outline" disabled={busy || !reasonValid(reason)} onClick={() => onCommand('POST', `/${record.id}/amend`, { expectedVersion: record.version, reason })}>{t.amend}</Button>}
        {record.canAcknowledge && <><Button disabled={busy} onClick={() => onCommand('POST', `/${record.id}/acknowledgements`, { revision: record.revision, contentHash: record.hash, choice: 'ACK', reason: reasonValid(reason) ? reason : null })}>{t.acknowledge}</Button>
          <Button variant="outline" disabled={busy || !reasonValid(reason)} onClick={() => onCommand('POST', `/${record.id}/acknowledgements`, { revision: record.revision, contentHash: record.hash, choice: 'DISPUTE', reason })}>{t.dispute}</Button></>}
      </EnterpriseActions>
    </div>}
    {record.decisions.length > 0 && <><h3>{t.decisions}</h3><ul>{record.decisions.map(d => <li key={d.id}>{d.decision} · r{d.revision} · {d.actorId} · {d.decidedAt}<br/>{d.reason}</li>)}</ul></>}
    {record.acknowledgements.length > 0 && <><h3>{t.acknowledgements}</h3><ul>{record.acknowledgements.map(a => <li key={a.actorId + a.revision}>{a.choice} · r{a.revision} · {a.actorId}{a.reason ? ` · ${a.reason}` : ''}</li>)}</ul></>}
    <details><summary>{t.revisions}</summary><ul>{record.revisions.map(r => <li key={r.revision}>r{r.revision} · <RiskState value={r.state} t={t}/> · {r.reasonCode} · {r.savedAt}</li>)}</ul></details>
  </section>
}

function AssessmentEditor({ editor, caps, methods, t, busy, vessel, templates, onTemplates, onCancel, onSave }: { editor: Editor; caps: RiskCapabilities; methods: RiskMethod[]; t: RiskCopy;
  busy: boolean; vessel: string; templates: RiskApplicableTemplates | null; onTemplates: (workType: string | null) => void; onCancel: () => void; onSave: (editor: Editor) => void }) {
  const [state, setState] = useState(editor)
  const c = state.content
  const set = (patch: Partial<RiskAssessmentContent>) => setState(s => ({ ...s, content: { ...s.content, ...patch } }))
  const method = methods.find(m => m.id === c.methodId && m.status === 'PUBLISHED')
  const setHazard = (index: number, patch: Partial<RiskAssessmentContent['hazards'][number]>) => set({ hazards: c.hazards.map((h, i) => i === index ? { ...h, ...patch } : h) })
  const participants = c.participants.map(p => p.actorId + ' ' + p.role).join('\n')
  const parseParticipants = (text: string) => splitList(text).map(line => { const [actorId, role] = line.split(/\s+/); return { actorId: (actorId ?? '').toLowerCase(), role: (role ?? 'PARTICIPANT').toUpperCase() } })
  const valid = (state.record !== null || state.assessmentNo.trim().length > 0) && (state.template !== null || c.hazards.every(h => h.description.trim() && h.controls.every(x => x.description.trim()))) &&
    c.participants.every(p => isGuid(p.actorId)) && state.links.every(l => isGuid(l.id)) && !(c.stopWorkRequired === false && c.stopWorkConditions.length > 0)
  const codes = (kind: 'likelihoods' | 'severities') => method ? method.content[kind] : []
  const rating = (index: number, which: 'initial' | 'residual') => {
    const h = c.hazards[index], r = h[which]
    const put = (value: RiskRating) => setHazard(index, which === 'initial' ? { initial: value } : { residual: value })
    return <fieldset className="risk-rating"><legend>{which === 'initial' ? t.initialRisk : t.residualRisk}</legend>
      <RiskField label={t.likelihood}><select disabled={!method} value={r.likelihood ?? ''} onChange={e => put({ ...r, likelihood: textOrNull(e.target.value) })}>
        <option value="">{t.unknown}</option>{codes('likelihoods').map(x => <option key={x.code} value={x.code}>{x.code} · {x.label}</option>)}</select></RiskField>
      <RiskField label={t.severity}><select disabled={!method} value={r.severity ?? ''} onChange={e => put({ ...r, severity: textOrNull(e.target.value) })}>
        <option value="">{t.unknown}</option>{codes('severities').map(x => <option key={x.code} value={x.code}>{x.code} · {x.label}</option>)}</select></RiskField>
      <RiskField label={t.rationale}><input value={r.reason ?? ''} maxLength={4000} onChange={e => put({ ...r, reason: textOrNull(e.target.value) })}/></RiskField>
    </fieldset>
  }
  return <section className="risk-panel risk-editor" aria-label={t.edit}>
    {!state.record && <RiskField label={t.assessmentNo}><input value={state.assessmentNo} maxLength={50} required onChange={e => setState(s => ({ ...s, assessmentNo: e.target.value }))}/></RiskField>}
    {!state.record && <div className="risk-template-pick">
      <Button variant="outline" disabled={busy} onClick={() => onTemplates(c.workType)}>{t.fromTemplate}</Button>
      {templates && (templates.items.length === 0 ? <p role="status">{t.noTemplates}</p> : <select aria-label={t.fromTemplate} value={state.template ? state.template.id + ':' + state.template.revision : ''}
        onChange={e => { const [id, revision] = e.target.value.split(':'); setState(s => ({ ...s, template: id ? { id, revision: Number(revision) } : null })) }}>
        <option value="">—</option>{templates.items.map(x => <option key={x.templateId} value={x.templateId + ':' + x.revision}>{x.templateCode} r{x.revision} · {x.title ?? ''}</option>)}</select>)}
    </div>}
    {state.template ? <p className="risk-note">{t.fromTemplate}</p> : <>
    <RiskField label={t.fieldTitle}><input value={c.title ?? ''} maxLength={200} onChange={e => set({ title: textOrNull(e.target.value) })}/></RiskField>
    <RiskField label={t.operationContext}><textarea rows={2} value={c.operationContext ?? ''} maxLength={4000} onChange={e => set({ operationContext: textOrNull(e.target.value) })}/></RiskField>
    <RiskField label={t.workType}><select value={c.workType ?? ''} onChange={e => set({ workType: textOrNull(e.target.value) })}><option value="">{t.unknown}</option>{riskWorkTypes.map(w => <option key={w} value={w}>{w}</option>)}</select></RiskField>
    <RiskField label={t.workScope}><textarea rows={2} value={c.workScope ?? ''} maxLength={4000} onChange={e => set({ workScope: textOrNull(e.target.value) })}/></RiskField>
    <RiskField label={t.method} hint={caps.methodConfigured ? undefined : t.methodUnconfigured}><select value={c.methodId ?? ''} onChange={e => set({ methodId: textOrNull(e.target.value) })}>
      <option value="">{t.methodMissing}</option>{methods.filter(m => m.status === 'PUBLISHED').map(m => <option key={m.id} value={m.id}>{m.code} r{m.revision} · {m.content.title}</option>)}</select></RiskField>
    <RiskTriState label={t.stopWork} value={c.stopWorkRequired} t={t} onChange={value => set({ stopWorkRequired: value })}/>
    <RiskField label={t.stopConditions}><textarea rows={2} value={c.stopWorkConditions.map(s => s.description).join('\n')}
      onChange={e => set({ stopWorkConditions: splitList(e.target.value).map((description, i) => ({ id: c.stopWorkConditions[i]?.id ?? crypto.randomUUID(), description })) })}/></RiskField>
    <RiskField label={t.participants}><textarea rows={2} defaultValue={participants} onBlur={e => set({ participants: parseParticipants(e.target.value) })}/></RiskField>
    <h3>{t.hazards}</h3>
    {c.hazards.map((h, index) => <fieldset key={h.id} className="risk-hazard-edit"><legend>{t.hazard} {index + 1}</legend>
      <RiskField label={t.step}><input value={h.step ?? ''} maxLength={200} onChange={e => setHazard(index, { step: textOrNull(e.target.value) })}/></RiskField>
      <RiskField label={t.hazard}><textarea rows={2} value={h.description} maxLength={4000} required onChange={e => setHazard(index, { description: e.target.value })}/></RiskField>
      <RiskField label={t.consequences}><textarea rows={2} value={h.consequences.join('\n')} onChange={e => setHazard(index, { consequences: splitList(e.target.value) })}/></RiskField>
      {rating(index, 'initial')}
      <h4>{t.controls}</h4>
      {h.controls.map((x, ci) => <div key={x.id} className="risk-control-edit">
        <RiskField label={t.controls}><input value={x.description} maxLength={4000} required onChange={e => setHazard(index, { controls: h.controls.map((y, j) => j === ci ? { ...y, description: e.target.value } : y) })}/></RiskField>
        <RiskField label={t.controlKind}><select value={x.kind} onChange={e => setHazard(index, { controls: h.controls.map((y, j) => j === ci ? { ...y, kind: e.target.value } : y) })}><option>EXISTING</option><option>PROPOSED</option></select></RiskField>
        <RiskField label={t.controlOwner}><input value={x.owner ?? ''} maxLength={200} onChange={e => setHazard(index, { controls: h.controls.map((y, j) => j === ci ? { ...y, owner: textOrNull(e.target.value) } : y) })}/></RiskField>
        <label className="risk-check"><input type="checkbox" checked={x.required} onChange={e => setHazard(index, { controls: h.controls.map((y, j) => j === ci ? { ...y, required: e.target.checked } : y) })}/>{t.required}</label>
        <Button variant="ghost" size="sm" onClick={() => setHazard(index, { controls: h.controls.filter((_, j) => j !== ci) })}>{t.remove}</Button>
      </div>)}
      <Button variant="outline" size="sm" onClick={() => setHazard(index, { controls: [...h.controls, emptyControl()] })}>{t.addControl}</Button>
      <RiskField label={t.barriers}><textarea rows={2} value={h.barrierRefs.join('\n')} onChange={e => setHazard(index, { barrierRefs: splitList(e.target.value) })}/></RiskField>
      {rating(index, 'residual')}
      <Button variant="ghost" size="sm" onClick={() => set({ hazards: c.hazards.filter((_, i) => i !== index) })}>{t.remove}</Button>
    </fieldset>)}
    <Button variant="outline" onClick={() => set({ hazards: [...c.hazards, emptyHazard()] })}>{t.addHazard}</Button>
    </>}
    <h3>{t.links}</h3>
    {state.links.map((l, i) => <div key={i} className="risk-link-edit">
      <select aria-label={t.linkKind} value={l.kind} onChange={e => setState(s => ({ ...s, links: s.links.map((y, j) => j === i ? { ...y, kind: e.target.value } : y) }))}>{riskLinkKinds.map(k => <option key={k}>{k}</option>)}</select>
      <input aria-label={t.linkId} value={l.id} onChange={e => setState(s => ({ ...s, links: s.links.map((y, j) => j === i ? { ...y, id: e.target.value.trim().toLowerCase() } : y) }))}/>
      <Button variant="ghost" size="sm" onClick={() => setState(s => ({ ...s, links: s.links.filter((_, j) => j !== i) }))}>{t.remove}</Button>
    </div>)}
    <Button variant="outline" size="sm" disabled={state.links.length >= 20} onClick={() => setState(s => ({ ...s, links: [...s.links, { kind: 'WorkOrder', id: '' }] }))}>{t.addLink}</Button>
    <EnterpriseActions>
      <Button variant="outline" disabled={busy} onClick={onCancel}>{t.cancel}</Button>
      <Button disabled={busy || !valid || !isGuid(vessel)} onClick={() => onSave(state)}>{t.save}</Button>
    </EnterpriseActions>
  </section>
}
