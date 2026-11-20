import { localizedCopy } from '../i18n/messages'
import { type Locale as AppLocale } from '../i18n'
import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import { ApiError, session, type Session } from './api'
import { isGuid } from './identifiers'
import { CertificateDocumentPicker } from './CertificateDocumentPicker'
import { criticalSpareApi } from './critical-spares-api'
import { emptyEvidence, evidenceKinds, freezeEvidence, freezeTransition, lexicalNumber, positiveQuantity, returnReasons, validEvidence, workOrderExecutionApi,
  type Evidence, type EvidenceKind, type Execution, type ExecutionAction, type ExecutionCommand, type ExecutionView, type ReturnReason, type TransitionInput } from './work-order-execution-api'
import { WorkOrderOfflinePanel } from './WorkOrderOfflinePanel'

// DEV-054 / T-S8-003 work-order-detail execution and evidence. The server decides every gate (state,
// required evidence, verifier/closer != executor, Human-only, version and evidence-hash fences).
// No state renders as a green "done": CLOSED is a recorded human closure, not return to service.
export const executionCopy = localizedCopy('WorkOrderExecution_tsx_copy')
export type ExecutionCopy = (typeof executionCopy)['en']
type SpareChoice = { id: string; revision: number; label: string }
const identity = (user?: Session | null) => user?.authenticated && isGuid(user.tenantId) && isGuid(user.actorId) ? user.tenantId + '/' + user.actorId : null
const tone = (state: string) => state === 'EVIDENCE_SUBMITTED' || state === 'VERIFIED' || state === 'CLOSED' ? 'info' : state === 'RELEASED' ? 'neutral' : 'warning'

function useOnline() {
  const [online, setOnline] = useState(() => navigator.onLine)
  useEffect(() => {
    const on = () => setOnline(true), off = () => setOnline(false)
    window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) }
  }, [])
  return online
}

export function EvidenceEditor({ value, onChange, disabled, vessel, owner, spares, language, t }: { value: Evidence; onChange: (value: Evidence) => void; disabled: boolean; vessel: string
  owner: string; spares: SpareChoice[] | null; language: AppLocale; t: ExecutionCopy }) {
  const [picking, setPicking] = useState<'PHOTO' | 'DOCUMENT' | null>(null)
  const set = (patch: Partial<Evidence>) => onChange({ ...value, ...patch })
  return <fieldset className="work-order-evidence" disabled={disabled}>
    <legend>{t.evidenceHeading}</legend>
    <h4>{t.readings}</h4>
    {value.readings.map((r, i) => <div key={i} className="work-order-evidence__row">
      <label>{t.readingCode}<input value={r.code} maxLength={64} onChange={e => set({ readings: value.readings.map((x, j) => j === i ? { ...x, code: e.target.value } : x) })}/></label>
      <label>{t.readingValue}<input value={r.value} maxLength={48} inputMode="decimal" aria-invalid={!lexicalNumber(r.value)} onChange={e => set({ readings: value.readings.map((x, j) => j === i ? { ...x, value: e.target.value } : x) })}/></label>
      <label>{t.readingUnit}<input value={r.unit ?? ''} maxLength={32} onChange={e => set({ readings: value.readings.map((x, j) => j === i ? { ...x, unit: e.target.value || null } : x) })}/></label>
      <Button type="button" variant="outline" size="sm" onClick={() => set({ readings: value.readings.filter((_, j) => j !== i) })}>{t.remove}</Button>
    </div>)}
    <Button type="button" variant="outline" size="sm" disabled={value.readings.length >= 50} onClick={() => set({ readings: [...value.readings, { code: '', value: '', unit: null, takenAt: null, note: null }] })}>{t.addReading}</Button>
    <p className="work-order-hint">{t.readingHint}</p>
    <h4>{t.spares}</h4>
    {value.spares.map((s, i) => <div key={i} className="work-order-evidence__row">
      <label>{t.spareItem}<select value={s.spareId ?? ''} onChange={e => { const choice = spares?.find(x => x.id === e.target.value)
        set({ spares: value.spares.map((x, j) => j === i ? { ...x, spareId: choice?.id ?? null, spareRevision: choice?.revision ?? null } : x) }) }}>
        <option value="">{t.spareUncatalogued}</option>
        {s.spareId && !spares?.some(x => x.id === s.spareId) && <option value={s.spareId}>{s.spareId.slice(0, 8)} r{s.spareRevision}</option>}
        {(spares ?? []).map(x => <option key={x.id} value={x.id}>{x.label}</option>)}
      </select></label>
      <label>{t.spareQuantity}<input value={s.quantity} inputMode="decimal" maxLength={30} aria-invalid={!positiveQuantity(s.quantity)} onChange={e => set({ spares: value.spares.map((x, j) => j === i ? { ...x, quantity: e.target.value } : x) })}/></label>
      <label>{t.spareDescription}<input value={s.description ?? ''} maxLength={200} onChange={e => set({ spares: value.spares.map((x, j) => j === i ? { ...x, description: e.target.value || null } : x) })}/></label>
      <Button type="button" variant="outline" size="sm" onClick={() => set({ spares: value.spares.filter((_, j) => j !== i) })}>{t.remove}</Button>
    </div>)}
    <Button type="button" variant="outline" size="sm" disabled={value.spares.length >= 50} onClick={() => set({ spares: [...value.spares, { spareId: null, spareRevision: null, quantity: '1', description: null }] })}>{t.addSpare}</Button>
    {spares === null && <p className="work-order-hint">{t.sparesCatalogUnavailable}</p>}
    <h4>{t.files}</h4>
    {value.files.length ? <ul>{value.files.map(f => <li key={f.file.id}><Badge tone="neutral">{f.kind === 'PHOTO' ? t.photo : t.document}</Badge> <code>{f.file.id.slice(0, 8)} r{f.file.revision}</code>{' '}
      <Button type="button" variant="outline" size="sm" onClick={() => set({ files: value.files.filter(x => x.file.id !== f.file.id) })}>{t.remove}</Button></li>)}</ul> : <p>{t.noFiles}</p>}
    <EnterpriseActions align="start" aria-label={t.files}>
      <Button type="button" variant="outline" size="sm" disabled={value.files.length >= 20} onClick={() => setPicking(picking === 'PHOTO' ? null : 'PHOTO')}>{t.addPhoto}</Button>
      <Button type="button" variant="outline" size="sm" disabled={value.files.length >= 20} onClick={() => setPicking(picking === 'DOCUMENT' ? null : 'DOCUMENT')}>{t.addDocument}</Button>
    </EnterpriseActions>
    {picking && <CertificateDocumentPicker vessel={vessel} owner={owner} value={null} language={language} disabled={disabled}
      onChange={pin => { if (pin && !value.files.some(f => f.file.id === pin.id)) set({ files: [...value.files, { kind: picking, file: pin, caption: null }] }); setPicking(null) }}/>}
    <p className="work-order-hint">{t.filesHint}</p>
    <label>{t.workDone}<textarea maxLength={4000} value={value.workDone ?? ''} onChange={e => set({ workDone: e.target.value || null })}/></label>
    <label>{t.findings}<textarea maxLength={4000} value={value.findings ?? ''} onChange={e => set({ findings: e.target.value || null })}/></label>
  </fieldset>
}

export function WorkOrderExecutionPanel({ vessel, workOrder, language }: { vessel: string; workOrder: string; language: AppLocale }) {
  const t = executionCopy[language], heading = useId(), online = useOnline()
  const [view, setView] = useState<ExecutionView | null>(null), [loadError, setLoadError] = useState<number | null>(null), [reload, setReload] = useState(0)
  const [owner, setOwner] = useState<string | null>(null), [spares, setSpares] = useState<SpareChoice[] | null>(null)
  const [evidence, setEvidence] = useState<Evidence>(emptyEvidence()), [required, setRequired] = useState<EvidenceKind[]>([]), [configured, setConfigured] = useState(false)
  const [reason, setReason] = useState<ReturnReason | ''>(''), [pending, setPending] = useState<ExecutionCommand | null>(null), [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<ApiError | null>(null), [receipt, setReceipt] = useState<Execution | null>(null)
  const mounted = useRef(false), writer = useRef<AbortController | null>(null)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => {
    if (!online) return
    const c = new AbortController()
    Promise.all([workOrderExecutionApi.view(vessel, workOrder, c.signal), session(c.signal)]).then(([value, user]) => {
      if (!mounted.current) return
      setView(value); setOwner(identity(user)); setLoadError(null)
      setEvidence(value.current ? structuredClone(value.current.evidence) : emptyEvidence())
      setRequired(value.current?.requiredEvidence ?? []); setConfigured(!!value.current?.requiredEvidence)
    }).catch(error => { if (mounted.current) setLoadError(error instanceof ApiError ? error.status : 0) })
    criticalSpareApi.list(vessel, 0, c.signal).then(page => { if (mounted.current) setSpares(page.items.map(s => ({ id: s.id, revision: s.revision, label: `${s.definition.partNumber} · ${s.definition.name} (${s.definition.unit})` }))) })
      .catch(() => { if (mounted.current) setSpares(null) })
    return () => c.abort()
  }, [vessel, workOrder, reload, online])

  const current = view?.current ?? null, caps = view?.capabilities
  const dirty = !!current && JSON.stringify(evidence) !== JSON.stringify(current.evidence)
  const conflict = failure?.status === 409 && !pending
  const may = online && !!owner && !busy && !conflict
  const run = useCallback(async (make: () => ExecutionCommand) => {
    if (!owner || busy || !online) return
    const c = new AbortController(); writer.current = c; const timer = setTimeout(() => c.abort(), 20000)
    setBusy(true); setFailure(null); let attempt = pending, sent = false
    try {
      attempt ??= make()
      const fresh = await session(c.signal)
      if (identity(fresh) !== owner) throw new ApiError(403, 'identity_changed')
      setPending(attempt); sent = true
      const saved = await workOrderExecutionApi.send(attempt, fresh.csrfToken, c.signal)
      if (!mounted.current) return
      setPending(null); setReceipt(saved); setReload(n => n + 1)
    } catch (error) {
      if (!mounted.current) return
      const problem = error instanceof ApiError ? error : new ApiError(0)
      setFailure(problem)
      // A definite 4xx releases the attempt; network/5xx keep the exact key and body for a same-key retry.
      if (!sent || problem.status > 0 && problem.status < 500) setPending(null)
    } finally { clearTimeout(timer); writer.current = null; if (mounted.current) setBusy(false) }
  }, [owner, busy, online, pending])
  const pendingAction = pending?.kind === 'transition' ? (JSON.parse(pending.body) as { action: ExecutionAction }).action : null
  const transition = (input: TransitionInput) => view && void run(() => freezeTransition(vessel, view, input))

  if (!online && !view) return <section className="work-order-execution" aria-labelledby={heading}><h2 id={heading}>{t.title}</h2><p role="status">{t.offlineNoData}</p>
    <WorkOrderOfflinePanel vessel={vessel} workOrder={workOrder} state={null} language={language} t={t}/></section>
  if (loadError !== null) return <section className="work-order-execution" aria-labelledby={heading}><h2 id={heading}>{t.title}</h2>
    {[401, 403].includes(loadError) ? <p role="status" data-state="forbidden">{t.forbidden}</p> : <p role="alert">{loadError === 404 ? t.notFound : t.loadError}</p>}
    <Button variant="outline" disabled={!online} onClick={() => setReload(n => n + 1)}>{t.retry}</Button></section>
  if (!view || !caps) return <section className="work-order-execution" aria-labelledby={heading}><h2 id={heading}>{t.title}</h2><p role="status" aria-live="polite">{t.loading}</p></section>
  const stale = !!current && !current.currentRevision
  const toggle = (kind: EvidenceKind) => setRequired(list => list.includes(kind) ? list.filter(k => k !== kind) : evidenceKinds.filter(k => k === kind || list.includes(k)))
  const requirementPicker = <fieldset className="work-order-required" disabled={!may}>
    <legend>{t.requiredHeading}</legend>
    <label><input type="radio" checked={!configured} onChange={() => setConfigured(false)}/>{t.requiredUnconfigured}</label>
    <label><input type="radio" checked={configured} onChange={() => setConfigured(true)}/>{t.requiredDeclare}</label>
    {configured && evidenceKinds.map(kind => <label key={kind}><input type="checkbox" checked={required.includes(kind)} onChange={() => toggle(kind)}/>{t[`kind${kind}`]}</label>)}
    <p className="work-order-hint">{t.requiredHint}</p>
  </fieldset>
  const act = (action: ExecutionAction, label: string, extra?: Partial<TransitionInput>, disabled = false) =>
    <Button type="button" variant={action === 'RETURN' ? 'outline' : 'default'} disabled={!may || stale || (pending ? pendingAction !== action : disabled)} onClick={() => transition({ action, ...extra })}>
      {pendingAction === action ? t.retrySame : label}</Button>

  return <section className="work-order-execution" aria-labelledby={heading} aria-busy={busy}>
    <h2 id={heading}>{t.title}</h2>
    <p className="work-order-hint">{t.qualification}</p>
    {!online && <p role="status">{t.offlineReadOnly}</p>}
    {stale && <p role="alert">{t.stale}</p>}
    {failure && <p role="alert" data-code={failure.code}>{failure.status === 409 ? t.conflict : [401, 403].includes(failure.status) ? t.denied : failure.status === 422 ? t.gateRejected : failure.status >= 500 || failure.status === 0 ? t.uncertain : t.invalid}
      {failure.code ? <> <code>{failure.code}</code></> : null}</p>}
    {receipt && !failure && <p role="status">{t.recorded} r{receipt.revision}</p>}
    {!current ? <>
      <p role="status">{t.notReleased}</p>
      {caps.canRelease ? <>{requirementPicker}<EnterpriseActions align="start" aria-label={t.release}>{act('RELEASE', t.release, { requiredEvidence: configured ? required : null }, configured && required.length === 0)}</EnterpriseActions></>
        : <p>{t.releaseNotPermitted}</p>}
    </> : <>
      <dl className="work-order-summary-grid">
        <div><dt>{t.state}</dt><dd><Badge tone={tone(current.state)} data-state={current.state}>{t[`state${current.state}`]}</Badge></dd></div>
        <div><dt>{t.gate}</dt><dd><Badge tone={current.gate.state === 'MISSING' ? 'danger' : 'warning'} data-gate={current.gate.state}>{t[`gate${current.gate.state}`]}</Badge></dd></div>
        <div><dt>{t.required}</dt><dd>{current.requiredEvidence ? current.requiredEvidence.map(k => t[`kind${k}`]).join(', ') : t.requiredUnconfigured}</dd></div>
        {current.gate.missing.length > 0 && <div><dt>{t.missing}</dt><dd>{current.gate.missing.map(k => t[`kind${k}`]).join(', ')}</dd></div>}
        <div><dt>{t.revision}</dt><dd>r{current.revision} · {t.workOrderRevision} r{current.workOrderRevision}</dd></div>
        {current.returnReason && <div><dt>{t.returned}</dt><dd>{t[`reason${current.returnReason}`]}</dd></div>}
      </dl>
      {current.executedByCurrentActor && <p className="work-order-hint">{t.youExecuted}</p>}
      <details><summary>{t.blockers}</summary><ul>{current.blockers.map(code => <li key={code}><code>{code}</code></li>)}</ul></details>
      {current.state === 'IN_PROGRESS' && caps.canExecute ? <form onSubmit={e => { e.preventDefault(); if (current && validEvidence(evidence)) void run(() => freezeEvidence(vessel, current, evidence)) }}>
        <EvidenceEditor value={evidence} onChange={setEvidence} disabled={!may || stale || pending?.kind === 'transition'} vessel={vessel} owner={owner ?? ''} spares={spares} language={language} t={t}/>
        {!validEvidence(evidence) && <p role="alert">{t.evidenceInvalid}</p>}
        <EnterpriseActions aria-label={t.saveEvidence}>
          {conflict && <Button type="button" variant="outline" onClick={() => { setFailure(null); setReload(n => n + 1) }}>{t.reload}</Button>}
          <Button type="submit" disabled={!may || stale || (!pending && !dirty) || !validEvidence(evidence) || pending?.kind === 'transition'}>{pending?.kind === 'evidence' ? t.retrySame : t.saveEvidence}</Button>
          {act('SUBMIT', t.submit, undefined, dirty || current.gate.state === 'MISSING')}
        </EnterpriseActions>
        {dirty && <p className="work-order-hint">{t.saveBeforeSubmit}</p>}
      </form> : <EvidenceSummary value={current.evidence} t={t}/>}
      <EnterpriseActions align="start" aria-label={t.actions}>
        {current.state === 'RELEASED' && caps.canExecute && act('START', t.start)}
        {current.state === 'EVIDENCE_SUBMITTED' && caps.canVerify && !current.executedByCurrentActor && <>
          {act('VERIFY', t.verify, undefined, current.gate.state !== 'PRESENT_PENDING_HUMAN_VERIFICATION')}
          <label>{t.returnReason}<select value={reason} disabled={!may} onChange={e => setReason(e.target.value as ReturnReason | '')}><option value="">—</option>
            {returnReasons.map(r => <option key={r} value={r}>{t[`reason${r}`]}</option>)}</select></label>
          {act('RETURN', t.returnWork, { reasonCode: reason || null }, !reason)}
        </>}
        {current.state === 'VERIFIED' && caps.canClose && !current.executedByCurrentActor && act('CLOSE', t.close)}
      </EnterpriseActions>
      {current.state === 'EVIDENCE_SUBMITTED' && current.gate.state === 'REVIEW_REQUIRED' && <p role="note">{t.verifyNeedsPolicy}</p>}
      {caps.canRelease && current.state !== 'VERIFIED' && current.state !== 'CLOSED' && !current.executedByCurrentActor && <details><summary>{t.changeRequired}</summary>{requirementPicker}
        <EnterpriseActions align="start" aria-label={t.changeRequired}>{act('REQUIREMENTS', t.saveRequired, { requiredEvidence: required }, !configured || required.length === 0)}</EnterpriseActions></details>}
      {caps.offline.supported && <WorkOrderOfflinePanel vessel={vessel} workOrder={workOrder} state={current.state} language={language} t={t} onSynced={() => setReload(n => n + 1)}/>}
      {!caps.offline.supported && <p className="work-order-hint">{t.offlineUnavailable}</p>}
    </>}
  </section>
}

export function EvidenceSummary({ value, t }: { value: Evidence; t: ExecutionCopy }) {
  return <section className="work-order-evidence-summary" aria-label={t.evidenceHeading}>
    <h3>{t.evidenceHeading}</h3>
    {value.readings.length > 0 && <ul>{value.readings.map(r => <li key={r.code}>{r.code}: {r.value}{r.unit ? ' ' + r.unit : ''}</li>)}</ul>}
    {value.spares.length > 0 && <ul>{value.spares.map((s, i) => <li key={i}>{s.spareId ? s.spareId.slice(0, 8) : s.description} × {s.quantity}</li>)}</ul>}
    {value.files.length > 0 && <ul>{value.files.map(f => <li key={f.file.id}>{f.kind === 'PHOTO' ? t.photo : t.document} <code>{f.file.id.slice(0, 8)} r{f.file.revision}</code></li>)}</ul>}
    {value.workDone && <p>{t.workDone}: {value.workDone}</p>}
    {value.findings && <p>{t.findings}: {value.findings}</p>}
    {!value.readings.length && !value.spares.length && !value.files.length && !value.workDone && !value.findings && <p>{t.noEvidence}</p>}
  </section>
}
