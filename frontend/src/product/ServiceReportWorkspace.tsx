import { localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { useCallback, useEffect, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterprisePageHeader } from './EnterprisePatterns'
import { EnterpriseChrome } from './EnterpriseChrome'
import { ApiError, session as readSession, vessels as readVessels, type Session, type Vessel } from './api'
import { isGuid } from './identifiers'
import { decodeServiceReport, emptyServiceContent, freezeTechnicalCommand, measurementResults, sendTechnicalCommand, servicePath, technicalApi, technicalFailure,
  textOrNull, type ServiceReport, type ServiceReportCapabilities, type ServiceReportContent, type ServiceReportPage, type TechnicalAttempt, type TechnicalIssue } from './technical-records-api'
import { label, technicalCopy, type TechnicalCopy } from './DefectLifecyclePanel'
import { TechnicalBanner, TechnicalField } from './TechnicalRecordParts'
import './technical-records.css'

// DEV-103 / T-PMS2-001 Service & Engineer Report (18.01 row 104; 59.04 service-engineer-report).
// Verification is a human record by a non-author; server state is authoritative and history is immutable.
export type ServiceRoute = { vessel: string | null; id: string | null }
export function readServiceRoute(location: { pathname: string; search: string }): ServiceRoute {
  const match = /^\/app\/service-engineer-report(?:\/([0-9a-fA-F-]{36}))?\/?$/.exec(location.pathname)
  const id = new URLSearchParams(location.search).get('id')
  const vessel = match?.[1] && isGuid(match[1].toLowerCase()) ? match[1].toLowerCase() : null
  return { vessel, id: vessel && id && isGuid(id.toLowerCase()) ? id.toLowerCase() : null }
}
const href = (route: ServiceRoute) => '/app/service-engineer-report' + (route.vessel ? '/' + route.vessel + (route.id ? '?id=' + route.id : '') : '')
export const guidLines = (value: string) => value.split(/\r?\n/).map(x => x.trim().toLowerCase()).filter(Boolean)

export function ServiceReportWorkspace() {
  const [language, setLanguage] = useAppLanguage()
  const [route, setRoute] = useState(() => readServiceRoute(window.location))
  useEffect(() => { const pop = () => setRoute(readServiceRoute(window.location)); window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop) }, [])
  const navigate = useCallback((target: string) => {
    if (target !== '/app' && !target.startsWith('/app/') || /[\\\p{Cc}]/u.test(target)) return
    if (/^\/app\/service-engineer-report(?:\/|$|\?)/.test(target)) { window.history.pushState(null, '', target); setRoute(readServiceRoute(window.location)) }
    else window.location.assign(target)
  }, [])
  return <ServiceSurface key={route.vessel ?? 'fleet'} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate}/>
}

type Loaded = { user: Session; caps?: ServiceReportCapabilities; page?: ServiceReportPage; record?: ServiceReport; vessels?: Vessel[] }
type Editor = { id: string; reportNo: string; content: ServiceReportContent; assetId: string; workOrderId: string; defects: string; evidence: string; record: ServiceReport | null }

function ServiceSurface({ route, language, onLanguage, onNavigate }: { route: ServiceRoute; language: AppLocale; onLanguage: (value: AppLocale) => void; onNavigate: (href: string) => void }) {
  const t = technicalCopy[language]
  const title = localizedText(language, 'pairs.m_8c3e1f0a5d2b7c42')
  const [loaded, setLoaded] = useState<Loaded | null>(null), [failed, setFailed] = useState<ApiError | null>(null), [attempt, setAttempt] = useState(0)
  const [editor, setEditor] = useState<Editor | null>(null), [issue, setIssue] = useState<TechnicalIssue>(null), [busy, setBusy] = useState(false)
  const [pending, setPending] = useState<TechnicalAttempt | null>(null), [offline, setOffline] = useState(typeof navigator !== 'undefined' && navigator.onLine === false)
  useEffect(() => { const on = () => setOffline(false), off = () => setOffline(true); window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) } }, [])
  useEffect(() => {
    const controller = new AbortController()
    ;(async () => {
      const user = await readSession(controller.signal)
      if (!route.vessel) return { user, vessels: (await readVessels(0, '', controller.signal)).items } as Loaded
      const [caps, page] = await Promise.all([technicalApi.serviceCapabilities(route.vessel, controller.signal), technicalApi.serviceList(route.vessel, 0, controller.signal)])
      const record = route.id ? await technicalApi.serviceRead(route.vessel, route.id, controller.signal) : undefined
      return { user, caps, page, record } as Loaded
    })().then(value => { if (!controller.signal.aborted) { setFailed(null); setLoaded(value) } })
      .catch(error => { if (!controller.signal.aborted) setFailed(error instanceof ApiError ? error : new ApiError(0, 'network')) })
    return () => controller.abort()
  }, [route.vessel, route.id, attempt])
  const reload = () => { setFailed(null); setIssue(null); setPending(null); setEditor(null); setAttempt(n => n + 1) }

  const execute = async (frozen: TechnicalAttempt) => {
    if (!loaded?.user.csrfToken || !route.vessel) return
    setBusy(true); setPending(frozen); setIssue(null)
    try {
      const saved = decodeServiceReport(await sendTechnicalCommand<unknown>(frozen, loaded.user.csrfToken, new AbortController().signal), route.vessel)
      setPending(null); setEditor(null)
      // Receipt confirmed: keep the canonical resource identity in the URL before any re-read.
      onNavigate(href({ vessel: route.vessel, id: saved.id })); setLoaded(current => current ? { ...current, record: saved } : current); setAttempt(n => n + 1)
    } catch (error) {
      const kind = technicalFailure(error); setIssue(kind)
      if (kind !== 'offline') setPending(null)
    } finally { setBusy(false) }
  }
  const command = (method: 'POST' | 'PUT', suffix: string, body: unknown) => route.vessel && execute(freezeTechnicalCommand(method, servicePath(route.vessel, suffix), body))
  const record = loaded?.record, caps = loaded?.caps
  const editFrom = (r: ServiceReport | null): Editor => r ? { id: r.id, reportNo: r.reportNo, content: structuredClone(r.content), assetId: r.equipment?.id ?? '', workOrderId: r.workOrder?.id ?? '',
    defects: r.defects.map(d => d.id).join('\n'), evidence: r.evidence.map(e => e.id).join('\n'), record: r }
    : { id: crypto.randomUUID(), reportNo: '', content: emptyServiceContent(), assetId: '', workOrderId: '', defects: '', evidence: '', record: null }

  return <div className="product-shell enterprise-shell technical-workspace">
    <EnterpriseChrome session={loaded?.user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="service-main"/>
    <main id="service-main" tabIndex={-1} data-testid="service-engineer-report">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span className="technical-note">{t.srNotice}</span>}
        actions={route.vessel && caps?.canWrite && !editor && !offline ? <Button onClick={() => setEditor(editFrom(null))}>{t.srNew}</Button> : undefined}/>
      {offline && <TechnicalBanner issue="offline" t={t}/>}
      {pending && issue === 'offline' && <Button variant="outline" disabled={busy || offline} onClick={() => execute(pending)}>{t.retry}</Button>}
      <TechnicalBanner issue={issue === 'offline' ? null : issue} t={t} onReload={reload} missing={issue === 'incomplete' ? record?.missing : undefined}/>
      {failed ? <div role="alert" className="technical-banner"><p>{failed.status === 403 ? t.forbidden : failed.status === 0 ? t.offline : t.error}</p><Button variant="outline" onClick={reload}>{t.reload}</Button></div>
        : !loaded ? <p role="status">{t.loading}</p>
        : !route.vessel ? <section className="technical-panel"><p>{t.chooseVessel}</p>{!loaded.vessels?.length ? <p role="status">{t.noVessels}</p> :
            <ul className="technical-list">{loaded.vessels.map(v => <li key={v.id}><Button variant="link" onClick={() => onNavigate('/app/service-engineer-report/' + v.id)}>{v.code} · {v.name}</Button></li>)}</ul>}</section>
        : editor ? <ServiceEditor editor={editor} t={t} busy={busy || offline} caps={caps!} onChange={setEditor} onCancel={() => setEditor(null)} onSave={next => {
            const links = { assetId: textOrNull(next.assetId), workOrderId: textOrNull(next.workOrderId), defectIds: guidLines(next.defects), evidenceFileIds: guidLines(next.evidence) }
            if (next.record) command('PUT', '/' + next.record.id, { expectedVersion: next.record.version, content: next.content, links })
            else command('POST', '', { id: next.id, reportNo: next.reportNo, content: next.content, links })
          }}/>
        : <div className="technical-layout">
            <section className="technical-panel" aria-label={t.srList}>{!loaded.page?.items.length ? <p role="status">{t.srEmpty}</p> :
              <ul className="technical-list">{loaded.page.items.map(i => <li key={i.id} aria-current={i.id === route.id ? 'true' : undefined}>
                <Button variant="link" onClick={() => onNavigate(href({ vessel: route.vessel, id: i.id }))}>{i.reportNo} · {i.serviceProvider ?? t.notSet}</Button>
                <Badge tone="neutral" data-state={i.state}>{label(t, 'ss_', i.state)}</Badge></li>)}</ul>}</section>
            {record && <ServiceDetail record={record} t={t} busy={busy || offline} onEdit={() => setEditor(editFrom(record))} onCommand={command}/>}
          </div>}
    </main>
  </div>
}

function ServiceDetail({ record, t, busy, onEdit, onCommand }: { record: ServiceReport; t: TechnicalCopy; busy: boolean; onEdit: () => void;
  onCommand: (method: 'POST' | 'PUT', suffix: string, body: unknown) => void }) {
  const [reason, setReason] = useState('')
  const c = record.content
  const reasonOk = reason.trim().length >= 10
  return <section className="technical-panel" aria-labelledby="service-detail-title">
    <h2 id="service-detail-title">{record.reportNo}</h2>
    <div className="technical-status"><Badge tone="neutral" data-state={record.state}>{label(t, 'ss_', record.state)}</Badge>
      <Badge tone={record.verificationState === 'VERIFIED' ? 'neutral' : 'warning'} data-verification={record.verificationState}>{label(t, 'vs_', record.verificationState)}</Badge>
      <span>r{record.revision}</span></div>
    <dl className="technical-facts">
      <dt>{t.provider}</dt><dd>{c.serviceProvider ?? t.notSet}</dd><dt>{t.engineer}</dt><dd>{c.engineerName ?? t.notSet}</dd>
      <dt>{t.serviceDate}</dt><dd>{c.serviceDate ?? t.notSet}</dd><dt>{t.scope}</dt><dd>{c.reasonScope ?? t.notSet}</dd>
      <dt>{t.work}</dt><dd>{c.workPerformed ?? t.notSet}</dd><dt>{t.defectsFound}</dt><dd>{c.defectsFound ?? t.notSet}</dd>
      <dt>{t.recommendations}</dt><dd>{c.recommendations ?? t.notSet}</dd><dt>{t.nextActionDue}</dt><dd>{c.nextActionDue ?? t.notSet}</dd>
    </dl>
    <h3>{t.parts}</h3>{c.parts.length === 0 ? <p>{t.notSet}</p> : <ul>{c.parts.map((p, i) => <li key={i}>{p.description} {p.partNumber && <code>{p.partNumber}</code>} {p.quantity ?? ''} {p.unit ?? ''}</li>)}</ul>}
    <h3>{t.measurements}</h3>{c.measurements.length === 0 ? <p>{t.notSet}</p> : <ul>{c.measurements.map((m, i) => <li key={i}>{m.name}: {m.value ?? t.notSet} {m.unit ?? ''}{m.result && <> · {label(t, 'mr_', m.result)}</>}</li>)}</ul>}
    <h3>{t.links}</h3><ul>
      {record.equipment && <li>{t.equipmentId}: <code>{record.equipment.id}</code> r{record.equipment.revision} · {label(t, 'cu_', record.equipment.currency)}</li>}
      {record.workOrder && <li>{t.workOrderId}: <code>{record.workOrder.id}</code> r{record.workOrder.revision} · {label(t, 'cu_', record.workOrder.currency)}</li>}
      {record.defects.map(d => <li key={d.id}>{t.defectLink}: <code>{d.id}</code> r{d.revision} · {label(t, 'cu_', d.currency)}</li>)}
      {record.evidence.map(e => <li key={e.id}>{t.evidence}: <code>{e.id}</code> r{e.revision} · {t.evidenceUnverified}</li>)}
    </ul>
    {record.missing.length > 0 && <><h3>{t.missing}</h3><ul>{record.missing.map(m => <li key={m}><code>{m}</code></li>)}</ul></>}
    <div className="technical-actions">
      {record.canEdit && <Button variant="outline" disabled={busy} onClick={onEdit}>{t.edit}</Button>}
      {record.canEdit && <Button disabled={busy || !record.canSubmit} onClick={() => onCommand('POST', `/${record.id}/submit`, { expectedVersion: record.version, contentHash: record.hash })}>{t.submitReport}</Button>}
    </div>
    {(record.canVerify || record.canAmend) && <div className="technical-form">
      <TechnicalField label={t.reason} hint={t.minimumTen}><textarea rows={2} maxLength={1000} value={reason} onChange={e => setReason(e.target.value)}/></TechnicalField>
      <div className="technical-actions">
        {record.canVerify && <Button disabled={busy || !reasonOk} onClick={() => onCommand('POST', `/${record.id}/verify`, { expectedVersion: record.version, contentHash: record.hash, decision: 'VERIFY', reason })}>{t.verifyReport}</Button>}
        {record.canVerify && <Button variant="outline" disabled={busy || !reasonOk} onClick={() => onCommand('POST', `/${record.id}/verify`, { expectedVersion: record.version, contentHash: record.hash, decision: 'RETURN', reason })}>{t.returnReport}</Button>}
        {record.canAmend && <Button variant="outline" disabled={busy || !reasonOk} onClick={() => onCommand('POST', `/${record.id}/amend`, { expectedVersion: record.version, reason })}>{t.amend}</Button>}
      </div></div>}
    {record.verifications.length > 0 && <><h3>{t.verification}</h3><ul>{record.verifications.map(v => <li key={v.id}>{label(t, 'dc_', v.decision)} · r{v.revision} · <code>{v.actorId}</code> · <time dateTime={v.decidedAt}>{v.decidedAt}</time><p className="technical-text">{v.reason}</p></li>)}</ul></>}
    <h3>{t.revisions}</h3><ol className="technical-records">{record.revisions.map(r => <li key={r.revision}>r{r.revision} · {label(t, 'ss_', r.state)} · <code>{r.reasonCode}</code> · <time dateTime={r.savedAt}>{r.savedAt}</time></li>)}</ol>
  </section>
}

function ServiceEditor({ editor, t, busy, caps, onChange, onCancel, onSave }: { editor: Editor; t: TechnicalCopy; busy: boolean; caps: ServiceReportCapabilities;
  onChange: (value: Editor) => void; onCancel: () => void; onSave: (value: Editor) => void }) {
  const c = editor.content
  const set = (patch: Partial<ServiceReportContent>) => onChange({ ...editor, content: { ...c, ...patch } })
  const text = (key: 'serviceProvider' | 'engineerName' | 'reasonScope' | 'workPerformed' | 'defectsFound' | 'recommendations', multiline = false) =>
    multiline ? <textarea rows={3} maxLength={4000} value={c[key] ?? ''} onChange={e => set({ [key]: textOrNull(e.target.value) })}/>
      : <input maxLength={200} value={c[key] ?? ''} onChange={e => set({ [key]: textOrNull(e.target.value) })}/>
  const ids = [editor.assetId, editor.workOrderId].filter(Boolean).concat(guidLines(editor.defects), guidLines(editor.evidence))
  const valid = (editor.record || editor.reportNo.trim().length > 0) && ids.every(isGuid) && c.parts.every(p => p.description.trim()) && c.measurements.every(m => m.name.trim())
  return <form className="technical-panel" aria-label={editor.record ? t.edit : t.srNew} onSubmit={e => { e.preventDefault(); if (valid) onSave(editor) }}>
    {!editor.record && <TechnicalField label={t.reportNo}><input maxLength={50} value={editor.reportNo} onChange={e => onChange({ ...editor, reportNo: e.target.value })}/></TechnicalField>}
    <TechnicalField label={t.provider}>{text('serviceProvider')}</TechnicalField>
    <TechnicalField label={t.engineer}>{text('engineerName')}</TechnicalField>
    <TechnicalField label={t.serviceDate}><input type="date" value={c.serviceDate ?? ''} onChange={e => set({ serviceDate: e.target.value || null })}/></TechnicalField>
    <TechnicalField label={t.scope}>{text('reasonScope', true)}</TechnicalField>
    <TechnicalField label={t.work}>{text('workPerformed', true)}</TechnicalField>
    <h3>{t.parts}</h3>
    {c.parts.map((p, i) => <div className="technical-row" key={i}>
      <TechnicalField label={t.partDescription}><input value={p.description} onChange={e => set({ parts: c.parts.map((x, j) => j === i ? { ...x, description: e.target.value } : x) })}/></TechnicalField>
      <TechnicalField label={t.partNumber}><input value={p.partNumber ?? ''} onChange={e => set({ parts: c.parts.map((x, j) => j === i ? { ...x, partNumber: textOrNull(e.target.value) } : x) })}/></TechnicalField>
      <TechnicalField label={t.quantity}><input inputMode="decimal" value={p.quantity ?? ''} onChange={e => set({ parts: c.parts.map((x, j) => j === i ? { ...x, quantity: textOrNull(e.target.value) } : x) })}/></TechnicalField>
      <Button variant="outline" size="sm" onClick={() => set({ parts: c.parts.filter((_, j) => j !== i) })}>{t.remove}</Button></div>)}
    <Button variant="outline" size="sm" onClick={() => set({ parts: [...c.parts, { description: '', partNumber: null, quantity: null, unit: null }] })}>{t.addPart}</Button>
    <h3>{t.measurements}</h3>
    {c.measurements.map((m, i) => <div className="technical-row" key={i}>
      <TechnicalField label={t.measurementName}><input value={m.name} onChange={e => set({ measurements: c.measurements.map((x, j) => j === i ? { ...x, name: e.target.value } : x) })}/></TechnicalField>
      <TechnicalField label={t.value}><input value={m.value ?? ''} onChange={e => set({ measurements: c.measurements.map((x, j) => j === i ? { ...x, value: textOrNull(e.target.value) } : x) })}/></TechnicalField>
      <TechnicalField label={t.result}><select value={m.result ?? ''} onChange={e => set({ measurements: c.measurements.map((x, j) => j === i ? { ...x, result: e.target.value || null } : x) })}>
        <option value="">{t.unknown}</option>{measurementResults.map(r => <option key={r} value={r}>{label(t, 'mr_', r)}</option>)}</select></TechnicalField>
      <Button variant="outline" size="sm" onClick={() => set({ measurements: c.measurements.filter((_, j) => j !== i) })}>{t.remove}</Button></div>)}
    <Button variant="outline" size="sm" onClick={() => set({ measurements: [...c.measurements, { name: '', value: null, unit: null, result: null }] })}>{t.addMeasurement}</Button>
    <TechnicalField label={t.defectsFound}>{text('defectsFound', true)}</TechnicalField>
    <TechnicalField label={t.recommendations}>{text('recommendations', true)}</TechnicalField>
    <TechnicalField label={t.nextActionDue}><input type="date" value={c.nextActionDue ?? ''} onChange={e => set({ nextActionDue: e.target.value || null })}/></TechnicalField>
    <h3>{t.links}</h3>
    <TechnicalField label={t.equipmentId}><input value={editor.assetId} onChange={e => onChange({ ...editor, assetId: e.target.value.trim().toLowerCase() })}/></TechnicalField>
    {caps.canLinkWorkOrder && <TechnicalField label={t.workOrderId}><input value={editor.workOrderId} onChange={e => onChange({ ...editor, workOrderId: e.target.value.trim().toLowerCase() })}/></TechnicalField>}
    {caps.canLinkDefect && <TechnicalField label={t.defectIds}><textarea rows={2} value={editor.defects} onChange={e => onChange({ ...editor, defects: e.target.value })}/></TechnicalField>}
    {caps.canAttachEvidence && <TechnicalField label={t.evidenceIds} hint={t.evidenceUnverified}><textarea rows={2} value={editor.evidence} onChange={e => onChange({ ...editor, evidence: e.target.value })}/></TechnicalField>}
    <div className="technical-actions"><Button type="submit" disabled={busy || !valid}>{t.save}</Button><Button variant="outline" disabled={busy} onClick={onCancel}>{t.cancel}</Button></div>
  </form>
}
