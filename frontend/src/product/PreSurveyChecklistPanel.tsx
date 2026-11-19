import { localizedCopy } from '../i18n/messages'
import { type Locale as AppLocale } from '../i18n'
import { useEffect, useId, useRef, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns'
import { ApiError, session, type Session } from './api'
import { isGuid } from './identifiers'
import { CertificateDocumentPicker } from './CertificateDocumentPicker'
import { answers, rejectReasons, surveyApi, VerificationRejected, toInput, type Answer, type ItemInput, type PortCallOption, type PreSurveyCapabilities, type PreSurveyChecklist, type PreSurveyPlan, type RejectReason } from './survey-api'

// DEV-104 pre-survey checklist editor and human verification. The server decides every gate
// (owner eligibility, NOT_APPLICABLE authority, evidence, verifier != answerer, Human-only,
// version fence). Readiness is advisory and never rendered as a green "ready" state. An
// uncertain outcome keeps its original idempotency key and body until resolved.
export const surveyCopy = localizedCopy('SurveyWorkspace_tsx_copy')
export type SurveyCopy = (typeof surveyCopy)['en']
const identity = (user?: Session | null) => user?.authenticated && user.canRead && isGuid(user.tenantId) && isGuid(user.actorId) ? user.tenantId + '/' + user.actorId : null
type Attempt = { owner: string; key: string; kind: 'update' | 'verify'; body: string }
type Decision = { decision: 'VERIFIED' | 'REJECTED' | ''; reason: RejectReason | '' }
type Props = { vessel: string; record: PreSurveyChecklist; capabilities: PreSurveyCapabilities; language: AppLocale; onDirty: (value: boolean) => void; onApplied: (record: PreSurveyChecklist) => void; onReload: () => void }

export function ReadinessBadge({ state, t }: { state: PreSurveyChecklist['readiness']['state']; t: SurveyCopy }) {
  // Never a success tone: the best state is still "pending human confirmation".
  return <Badge tone={state === 'BLOCKED' ? 'danger' : 'warning'} data-readiness={state}>{t[`readiness${state}`]}</Badge>
}
export function FitBadge({ state, t }: { state: PreSurveyChecklist['voyageFit']['state']; t: SurveyCopy }) {
  return <Badge tone={state === 'NOT_FIT' ? 'danger' : 'warning'} data-fit={state}>{t[`fit${state}`]}</Badge>
}
export function WindowBadge({ state, t }: { state: PreSurveyChecklist['window']['state']; t: SurveyCopy }) {
  return <Badge tone={state === 'OVERDUE' ? 'danger' : state === 'OPEN' ? 'info' : 'warning'} data-window={state}>{t[`window${state}`]}</Badge>
}
export const Codes = ({ codes }: { codes: string[] }) => codes.length ? <ul className="survey-codes">{codes.map(code => <li key={code}><code>{code}</code></li>)}</ul> : null

function useOnline() {
  const [online, setOnline] = useState(() => navigator.onLine)
  useEffect(() => {
    const on = () => setOnline(true), off = () => setOnline(false)
    window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) }
  }, [])
  return online
}

export function PreSurveyChecklistPanel({ vessel, record, capabilities, language, onDirty, onApplied, onReload }: Props) {
  const t = surveyCopy[language], id = useId(), online = useOnline()
  const [owner, setOwner] = useState<string | null>(null)
  const [plan, setPlan] = useState<PreSurveyPlan>(record.plan), [items, setItems] = useState<ItemInput[]>(() => record.items.map(toInput))
  const [decisions, setDecisions] = useState<Record<string, Decision>>({}), [picking, setPicking] = useState<string | null>(null)
  const [ports, setPorts] = useState<PortCallOption[] | null>(null)
  const [pending, setPending] = useState<Attempt | null>(null), [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [recorded, setRecorded] = useState(false)
  const [itemErrors, setItemErrors] = useState<{ itemId: string; code: string }[]>([])
  const mounted = useRef(false), writer = useRef<AbortController | null>(null)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => { if (!online) writer.current?.abort() }, [online])
  useEffect(() => {
    const c = new AbortController()
    session(c.signal).then(user => { if (mounted.current) setOwner(identity(user)) }).catch(() => { if (mounted.current) setOwner(null) })
    return () => c.abort()
  }, [record.id, record.version])
  useEffect(() => {
    if (!capabilities.canReadPortCalls || !online) return
    const c = new AbortController()
    surveyApi.portCalls(vessel, c.signal).then(page => { if (mounted.current) setPorts(page.items) }).catch(() => { if (mounted.current) setPorts([]) })
    return () => c.abort()
  }, [vessel, capabilities.canReadPortCalls, online])
  const original = JSON.stringify({ plan: record.plan, items: record.items.map(toInput) })
  const dirty = JSON.stringify({ plan, items }) !== original
  const chosen = Object.values(decisions).some(d => d.decision)
  useEffect(() => { onDirty(dirty || chosen || !!pending) }, [dirty, chosen, pending, onDirty])
  const stale = !record.currentRevision, conflict = failure?.status === 409 && !pending
  const may = online && !!owner && !stale && !busy && !pending && !conflict
  const canEdit = record.canEdit && capabilities.canWrite, canVerify = record.canVerify && capabilities.canVerify
  const patch = (itemId: string, value: Partial<ItemInput>) => { if (may && canEdit) setItems(previous => previous.map(i => i.itemId === itemId ? { ...i, ...value } : i)) }

  async function send(kind: 'update' | 'verify') {
    if (!owner || busy || !online) return
    const c = new AbortController(); writer.current = c
    const timer = setTimeout(() => c.abort(), 20000)
    setBusy(true); setFailure(null); setItemErrors([])
    let attempt = pending, sent = false
    try {
      if (!attempt) {
        const body = kind === 'update'
          ? { id: record.id, expectedVersion: record.version, expectedRevision: record.revision, certificateRevision: record.certificateRevision, certificateVersion: record.certificateVersion, plan, items }
          : { id: record.id, expectedVersion: record.version, expectedRevision: record.revision, decisions: record.items.filter(i => decisions[i.itemId]?.decision).map(i => ({ itemId: i.itemId, expectedItemHash: i.itemHash, decision: decisions[i.itemId].decision, reasonCode: decisions[i.itemId].decision === 'REJECTED' ? decisions[i.itemId].reason || null : null })) }
        attempt = { owner, key: crypto.randomUUID(), kind, body: JSON.stringify(body) }
      }
      const fresh = await session(c.signal)
      if (identity(fresh) !== attempt.owner) throw new ApiError(403, 'identity_changed')
      setPending(attempt); sent = true
      const command = { key: attempt.key, id: record.id, vessel, body: attempt.body }
      const receipt = attempt.kind === 'update' ? await surveyApi.update(command, fresh.csrfToken, c.signal) : await surveyApi.verify(command, fresh.csrfToken, c.signal)
      if (!mounted.current) return
      setPending(null); setDecisions({}); setRecorded(true)
      onApplied(receipt)
    } catch (error) {
      if (!mounted.current) return
      const problem = error instanceof ApiError ? error : new ApiError(0)
      setFailure(problem)
      if (problem instanceof VerificationRejected) setItemErrors(problem.items)
      // A definite 4xx releases the attempt; network/5xx keep the exact key and body for retry.
      if (!sent || problem.status > 0 && problem.status < 500) setPending(null)
    } finally { clearTimeout(timer); writer.current = null; if (mounted.current) setBusy(false) }
  }

  const ownerLabel = (roleId: string) => roleId.slice(0, 8)
  return <section className="survey-checklist" aria-label={t.checklistTitle} aria-busy={busy}>
    <p className="certificate-field-hint">{t.qualification}</p>
    <dl className="survey-summary">
      <div><dt>{t.readiness}</dt><dd><ReadinessBadge state={record.readiness.state} t={t}/> · {record.readiness.verified}/{record.readiness.total}</dd></div>
      <div><dt>{t.window}</dt><dd><WindowBadge state={record.window.state} t={t}/> {record.window.start ?? '—'} → {record.window.end ?? '—'}</dd></div>
      <div><dt>{t.fit}</dt><dd><FitBadge state={record.voyageFit.state} t={t}/><Codes codes={record.voyageFit.reasons}/></dd></div>
      <div><dt>{t.template}</dt><dd><code>{record.templateCode}</code> r{record.templateRevision} · {t.templateStatus}</dd></div>
    </dl>
    {record.readiness.blockers.length > 0 && <details><summary>{t.blockers}</summary><Codes codes={record.readiness.blockers}/></details>}
    {!online && <p role="status" data-state="offline">{t.offline}</p>}
    {stale && <p role="alert">{t.stale}</p>}
    {!canEdit && !canVerify && <p role="note">{t.readOnly}</p>}
    {recorded && !failure && <p role="status">{t.saved}</p>}
    {failure && <p role="alert">{pending ? t.unknownOutcome : conflict ? t.conflict : t.failed}{failure.code && <> · <code>{failure.code}</code></>}</p>}
    {itemErrors.length > 0 && <div role="alert"><p>{t.itemErrors}</p><ul>{itemErrors.map(e => <li key={e.itemId}><code>{record.items.find(i => i.itemId === e.itemId)?.itemCode}</code> · <code>{e.code}</code></li>)}</ul></div>}
    <form noValidate aria-label={t.plan} onSubmit={event => { event.preventDefault(); void send('update') }}>
      <EnterpriseFormGrid disabled={!may || !canEdit}><legend>{t.plan}</legend>
        <EnterpriseField htmlFor={`${id}-start`} label={t.plannedStart}><input id={`${id}-start`} type="date" value={plan.plannedStart ?? ''} onChange={e => may && canEdit && setPlan(p => ({ ...p, plannedStart: e.target.value || null }))}/></EnterpriseField>
        <EnterpriseField htmlFor={`${id}-end`} label={t.plannedEnd}><input id={`${id}-end`} type="date" value={plan.plannedEnd ?? ''} onChange={e => may && canEdit && setPlan(p => ({ ...p, plannedEnd: e.target.value || null }))}/></EnterpriseField>
        <EnterpriseField htmlFor={`${id}-location`} label={t.location}><input id={`${id}-location`} maxLength={200} value={plan.locationText ?? ''} onChange={e => may && canEdit && setPlan(p => ({ ...p, locationText: e.target.value || null }))}/></EnterpriseField>
        <EnterpriseField htmlFor={`${id}-port`} label={t.portCall}>
          <select id={`${id}-port`} value={plan.portCallId ? `${plan.portCallId}@${plan.portCallRevision}` : ''} disabled={!capabilities.canReadPortCalls}
            onChange={e => { if (!may || !canEdit) return; const [pid, rev] = e.target.value.split('@'); setPlan(p => ({ ...p, portCallId: pid || null, portCallRevision: pid ? Number(rev) : null })) }}>
            <option value="">{t.noPortCall}</option>
            {plan.portCallId && !ports?.some(o => o.id === plan.portCallId && o.revision === plan.portCallRevision) && <option value={`${plan.portCallId}@${plan.portCallRevision}`}>{plan.portCallId.slice(0, 8)} r{plan.portCallRevision}</option>}
            {ports?.map(o => <option key={`${o.id}@${o.revision}`} value={`${o.id}@${o.revision}`}>{o.label} · {o.from ?? '—'} → {o.to ?? '—'}</option>)}
          </select>
        </EnterpriseField>
      </EnterpriseFormGrid>
      <p className="certificate-field-hint">{t.voyage}{record.portCall && <> · <code>{record.portCall.reference}</code></>}</p>
      <table className="survey-items"><caption>{t.items}</caption>
        <thead><tr><th scope="col">{t.item}</th><th scope="col">{t.owner}</th><th scope="col">{t.due}</th><th scope="col">{t.answer}</th><th scope="col">{t.evidence}</th><th scope="col">{t.finding}</th><th scope="col">{t.verification}</th></tr></thead>
        <tbody>{record.items.map(item => {
          const input = items.find(i => i.itemId === item.itemId)!, decision = decisions[item.itemId] ?? { decision: '', reason: '' }
          const options = answers.filter(a => a !== 'NOT_APPLICABLE' || item.allowNotApplicable)
          return <tr key={item.itemId} data-item={item.itemCode}>
            <th scope="row"><code>{item.itemCode}</code><br/>{item.critical ? <Badge tone="danger">{t.critical}</Badge> : item.required ? <Badge tone="warning">{t.required}</Badge> : <Badge>{t.optional}</Badge>}<Codes codes={item.blockers}/></th>
            <td><select aria-label={`${t.owner} ${item.itemCode}`} value={input.ownerRoleId ?? ''} disabled={!may || !canEdit} onChange={e => patch(item.itemId, { ownerRoleId: e.target.value || null })}>
              <option value="">{t.unassigned}</option>
              {input.ownerRoleId && !capabilities.ownerOptions.some(o => o.roleId === input.ownerRoleId) && <option value={input.ownerRoleId}>{ownerLabel(input.ownerRoleId)}</option>}
              {capabilities.ownerOptions.map(o => <option key={o.roleId} value={o.roleId}>{ownerLabel(o.roleId)}</option>)}
            </select></td>
            <td><input aria-label={`${t.due} ${item.itemCode}`} type="date" value={input.dueDate ?? ''} disabled={!may || !canEdit} onChange={e => patch(item.itemId, { dueDate: e.target.value || null })}/></td>
            <td><select aria-label={`${t.answer} ${item.itemCode}`} value={input.answer} disabled={!may || !canEdit} onChange={e => patch(item.itemId, { answer: e.target.value as Answer })}>
              {options.map(a => <option key={a} value={a}>{t[`answer${a}`]}</option>)}
            </select></td>
            <td>{input.evidence.length ? <ul>{input.evidence.map(p => <li key={p.id}><code>{p.id.slice(0, 8)} r{p.revision}</code> <Button type="button" variant="outline" disabled={!may || !canEdit} onClick={() => patch(item.itemId, { evidence: input.evidence.filter(x => x.id !== p.id) })}>{t.removeEvidence}</Button></li>)}</ul> : '—'}
              {canEdit && input.evidence.length < 10 && <Button type="button" variant="outline" disabled={!may} onClick={() => setPicking(picking === item.itemId ? null : item.itemId)}>{t.addEvidence}</Button>}</td>
            <td><textarea aria-label={`${t.finding} ${item.itemCode}`} maxLength={4000} value={input.finding ?? ''} disabled={!may || !canEdit} onChange={e => patch(item.itemId, { finding: e.target.value || null })}/>
              <textarea aria-label={`${t.comment} ${item.itemCode}`} maxLength={4000} value={input.comment ?? ''} disabled={!may || !canEdit} onChange={e => patch(item.itemId, { comment: e.target.value || null })}/></td>
            <td><Badge tone={item.verificationState === 'REJECTED' ? 'danger' : 'warning'} data-verification={item.verificationState}>{t[`ver${item.verificationState}`]}</Badge>
              {item.answeredByCurrentActor && <p className="certificate-field-hint">{t.answeredByYou}</p>}
              {canVerify && item.answer !== 'OPEN' && !item.answeredByCurrentActor && <>
                <select aria-label={`${t.decision} ${item.itemCode}`} value={decision.decision} disabled={!may || dirty} onChange={e => setDecisions(d => ({ ...d, [item.itemId]: { decision: e.target.value as Decision['decision'], reason: '' } }))}>
                  <option value="">{t.noDecision}</option><option value="VERIFIED">{t.decisionVERIFIED}</option><option value="REJECTED">{t.decisionREJECTED}</option>
                </select>
                {decision.decision === 'REJECTED' && <select aria-label={`${t.reason} ${item.itemCode}`} value={decision.reason} disabled={!may} onChange={e => setDecisions(d => ({ ...d, [item.itemId]: { decision: 'REJECTED', reason: e.target.value as RejectReason } }))}>
                  <option value="">{t.noDecision}</option>{rejectReasons.map(r => <option key={r} value={r}>{r}</option>)}
                </select>}
              </>}</td>
          </tr>
        })}</tbody>
      </table>
      {picking && <CertificateDocumentPicker vessel={vessel} owner={owner ?? ''} value={null} language={language} disabled={!may}
        onChange={pin => { if (!pin) return; const target = items.find(i => i.itemId === picking); if (target && !target.evidence.some(e => e.id === pin.id)) patch(picking, { evidence: [...target.evidence, pin] }); setPicking(null) }}/>}
      <EnterpriseActions aria-label={t.save}>
        {conflict && <Button variant="outline" type="button" disabled={busy} onClick={() => { setFailure(null); onReload() }}>{t.reload}</Button>}
        {canEdit && <Button type="submit" disabled={busy || !online || !owner || stale || conflict || (!pending && !dirty) || pending?.kind === 'verify'}>{pending?.kind === 'update' ? t.retrySame : t.save}</Button>}
        {canVerify && <Button type="button" variant="outline" disabled={busy || !online || !owner || stale || conflict || dirty || (!pending && !chosen) || pending?.kind === 'update' ||
          Object.values(decisions).some(d => d.decision === 'REJECTED' && !d.reason)} onClick={() => void send('verify')}>{pending?.kind === 'verify' ? t.retrySame : t.verify}</Button>}
      </EnterpriseActions>
      {canVerify && <p className="certificate-field-hint">{t.verifyIntro}</p>}
    </form>
  </section>
}
