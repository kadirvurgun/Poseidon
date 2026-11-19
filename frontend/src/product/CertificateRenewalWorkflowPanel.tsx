import { localizedText } from '../i18n/messages'
import { type Locale as AppLocale } from '../i18n'
import { useEffect, useId, useRef, useState } from 'react'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import { EnterpriseActions, EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns'
import { ApiError, session, type Session } from './api'
import { isGuid } from './identifiers'
import { certificatesApi, emptyRenewalDecision, type RenewalDecision, type RenewalDto, type RenewalMatch, type RenewalRequirementStatus, type RenewalState, type RenewalTransition } from './certificates-api'
import { CertificateReplacementPicker, type CertificateReplacementPin } from './CertificateReplacementPicker'
import { CertificateDocumentPicker } from './CertificateDocumentPicker'

// DEV-044 renewal workflow v1 (60.04 VC-012). The server decides every gate (state, owner,
// UNKNOWN requirements, effective boundary, Human-only verify/complete and separation of
// duties); this panel only offers the server's allowed steps for the current revision.
// An uncertain outcome keeps its original idempotency key and body until resolved.
export const RENEWAL_TEXT = {
  heading: 'pairs.m_7c1e0a5b3d9f2400', intro: 'pairs.m_7c1e0a5b3d9f2401', state: 'pairs.m_7c1e0a5b3d9f2402', next: 'pairs.m_7c1e0a5b3d9f2403',
  noSteps: 'pairs.m_7c1e0a5b3d9f2404', stale: 'pairs.m_7c1e0a5b3d9f2405', offline: 'pairs.m_7c1e0a5b3d9f2406', conflict: 'pairs.m_7c1e0a5b3d9f2407',
  failed: 'pairs.m_7c1e0a5b3d9f2408', unknown: 'pairs.m_7c1e0a5b3d9f2409', recorded: 'pairs.m_7c1e0a5b3d9f240a', submit: 'pairs.m_7c1e0a5b3d9f240b',
  retrySame: 'pairs.m_7c1e0a5b3d9f240c', reload: 'pairs.m_7c1e0a5b3d9f240d', survey: 'pairs.m_7c1e0a5b3d9f240e', documents: 'pairs.m_7c1e0a5b3d9f240f',
  actions: 'pairs.m_7c1e0a5b3d9f2410', applicationDate: 'pairs.m_7c1e0a5b3d9f2411', authorityReference: 'pairs.m_7c1e0a5b3d9f2412', evidence: 'pairs.m_7c1e0a5b3d9f2413',
  sod: 'pairs.m_7c1e0a5b3d9f2414', choose: 'pairs.m_7c1e0a5b3d9f2415', step: 'pairs.m_7c1e0a5b3d9f2416', unavailable: 'pairs.m_7c1e0a5b3d9f2417',
  notLegal: 'pairs.m_7c1e0a5b3d9f2418', replacementMissing: 'pairs.m_7c1e0a5b3d9f2419', loading: 'pairs.m_7c1e0a5b3d9f241a',
} as const
export const RENEWAL_STATES: Record<RenewalState, string> = {
  DRAFT: 'pairs.m_7c1e0a5b3d9f2420', PREPARING: 'pairs.m_7c1e0a5b3d9f2421', READY_TO_APPLY: 'pairs.m_7c1e0a5b3d9f2422', APPLICATION_RECORDED: 'pairs.m_7c1e0a5b3d9f2423',
  AWAITING_AUTHORITY: 'pairs.m_7c1e0a5b3d9f2424', ISSUED_UNVERIFIED: 'pairs.m_7c1e0a5b3d9f2425', VERIFIED: 'pairs.m_7c1e0a5b3d9f2426', COMPLETED: 'pairs.m_7c1e0a5b3d9f2427',
  CANCELLED: 'pairs.m_7c1e0a5b3d9f2428', REJECTED: 'pairs.m_7c1e0a5b3d9f2429',
}
export const RENEWAL_ACTIONS: Record<RenewalTransition, string> = {
  OPEN: 'pairs.m_7c1e0a5b3d9f2430', VALIDATE: 'pairs.m_7c1e0a5b3d9f2431', RECORD_APPLICATION: 'pairs.m_7c1e0a5b3d9f2432', AWAIT_AUTHORITY: 'pairs.m_7c1e0a5b3d9f2433',
  RECORD_ISSUED_CANDIDATE: 'pairs.m_7c1e0a5b3d9f2434', VERIFY: 'pairs.m_7c1e0a5b3d9f2435', COMPLETE: 'pairs.m_7c1e0a5b3d9f2436', REJECT: 'pairs.m_7c1e0a5b3d9f2437',
  CANCEL: 'pairs.m_7c1e0a5b3d9f2438',
}
const STATUS: Record<RenewalRequirementStatus, string> = { COMPLETE: 'pairs.m_7c1e0a5b3d9f2440', NOT_REQUIRED: 'pairs.m_7c1e0a5b3d9f2441', UNKNOWN: 'pairs.m_7c1e0a5b3d9f2442' }
const MATCH: Record<RenewalMatch, string> = { MATCH: 'pairs.m_7c1e0a5b3d9f2443', MISMATCH: 'pairs.m_7c1e0a5b3d9f2444', UNKNOWN: 'pairs.m_7c1e0a5b3d9f2442' }
const MATCH_FIELDS = [['issuerMatch', 'pairs.m_7c1e0a5b3d9f2450'], ['numberMatch', 'pairs.m_7c1e0a5b3d9f2451'], ['subjectMatch', 'pairs.m_7c1e0a5b3d9f2452'],
  ['dateMatch', 'pairs.m_7c1e0a5b3d9f2453'], ['scopeMatch', 'pairs.m_7c1e0a5b3d9f2454']] as const
const REQUIREMENT_FIELDS = [['surveyStatus', RENEWAL_TEXT.survey], ['documentsStatus', RENEWAL_TEXT.documents], ['actionsStatus', RENEWAL_TEXT.actions]] as const

type Attempt = { owner: string; key: string; action: RenewalTransition; body: string }
type Props = { vessel: string; record: RenewalDto; enabled: boolean; language: AppLocale; onDirty: (value: boolean) => void; onApplied: (receipt: RenewalDto) => void }
const identity = (user?: Session | null) => user?.authenticated && user.canRead && isGuid(user.tenantId) && isGuid(user.actorId) ? user.tenantId + '/' + user.actorId : null

export function RenewalStateBadge({ state, language }: { state: RenewalState; language: AppLocale }) {
  // Never a green "ready" signal: verified/completed remain internal human records, not legal validity.
  return <Badge tone={state === 'REJECTED' || state === 'CANCELLED' ? 'danger' : state === 'COMPLETED' ? 'neutral' : 'warning'}>{localizedText(language, RENEWAL_STATES[state])}</Badge>
}

export function CertificateRenewalWorkflowPanel({ vessel, record, enabled, language, onDirty, onApplied }: Props) {
  const t = (key: string) => localizedText(language, key), id = useId()
  const [online, setOnline] = useState(() => navigator.onLine)
  const [owner, setOwner] = useState<string | null>(null), [action, setAction] = useState<RenewalTransition | ''>('')
  const [decision, setDecision] = useState<RenewalDecision>(emptyRenewalDecision), [replacement, setReplacement] = useState<CertificateReplacementPin | null>(null)
  const [pending, setPending] = useState<Attempt | null>(null), [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [recorded, setRecorded] = useState(false)
  const mounted = useRef(false), writer = useRef<AbortController | null>(null)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => {
    const on = () => setOnline(true), off = () => { setOnline(false); writer.current?.abort() }
    window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) }
  }, [])
  useEffect(() => {
    const c = new AbortController()
    session(c.signal).then(user => { if (mounted.current) setOwner(identity(user)) }).catch(() => { if (mounted.current) setOwner(null) })
    return () => c.abort()
  }, [record.id, record.version])
  useEffect(() => { onDirty(!!action || !!pending) }, [action, pending, onDirty])
  const stale = !record.currentRevision, conflict = failure?.status === 409 && !pending
  const allowed = record.allowedTransitions, may = enabled && online && !!owner && !stale && !busy && !pending && !conflict
  const choose = (next: RenewalTransition | '') => { if (!may) return; setAction(next); setDecision(emptyRenewalDecision()); setReplacement(null); setFailure(null); setRecorded(false) }
  const patch = (value: Partial<RenewalDecision>) => { if (may) setDecision(previous => ({ ...previous, ...value })) }

  async function submit() {
    if (!owner || busy || !online || !enabled) return
    const c = new AbortController(); writer.current = c
    const timer = setTimeout(() => c.abort(), 20000)
    setBusy(true); setFailure(null)
    let attempt = pending, sent = false
    try {
      if (!attempt) {
        if (!action || !allowed.includes(action)) throw new ApiError(400, 'validation_failed')
        let body: RenewalDecision = decision
        if (action === 'RECORD_ISSUED_CANDIDATE') {
          if (!replacement) throw new ApiError(400, 'validation_failed')
          // Evidence is the exact document pinned on the replacement's own revision.
          const actual = await certificatesApi.read(vessel, replacement.id, c.signal)
          if (actual.revision !== replacement.revision || actual.version !== replacement.version) throw new ApiError(409, 'linked_version_conflict')
          body = { ...decision, newCertificateId: replacement.id, newCertificateRevision: replacement.revision, newCertificateVersion: replacement.version, newDocument: actual.content.document }
        }
        attempt = { owner, key: crypto.randomUUID(), action, body: JSON.stringify({ id: record.id, expectedVersion: record.version, expectedRevision: record.revision, action, decision: body }) }
      }
      const fresh = await session(c.signal)
      if (identity(fresh) !== attempt.owner) throw new ApiError(403, 'identity_changed')
      setPending(attempt); sent = true
      const receipt = await certificatesApi.transitionRenewal({ key: attempt.key, id: record.id, vessel, expectedVersion: record.version, body: attempt.body }, fresh.csrfToken, c.signal)
      if (!mounted.current) return
      setPending(null); setAction(''); setDecision(emptyRenewalDecision()); setReplacement(null); setRecorded(true)
      onApplied(receipt)
    } catch (error) {
      if (!mounted.current) return
      const problem = error instanceof ApiError ? error : new ApiError(0)
      setFailure(problem)
      // A definite 4xx releases the attempt; network/5xx keep the exact key and body for retry.
      if (!sent || problem.status > 0 && problem.status < 500) setPending(null)
    } finally { clearTimeout(timer); writer.current = null; if (mounted.current) setBusy(false) }
  }

  const select = (key: string, label: string, value: string | null, options: Record<string, string>, onChange: (value: string | null) => void) =>
    <EnterpriseField key={key} htmlFor={`${id}-${key}`} label={t(label)}>
      <select id={`${id}-${key}`} value={value ?? ''} disabled={!may} onChange={event => onChange(event.target.value || null)}>
        <option value="">{t(RENEWAL_TEXT.choose)}</option>
        {Object.entries(options).map(([option, text]) => <option key={option} value={option}>{t(text)}</option>)}
      </select>
    </EnterpriseField>

  return <section className="certificate-metadata-form" aria-label={t(RENEWAL_TEXT.heading)} aria-busy={busy}>
    <h3>{t(RENEWAL_TEXT.heading)}</h3>
    <p>{t(RENEWAL_TEXT.intro)}</p>
    <p>{t(RENEWAL_TEXT.state)}: <RenewalStateBadge state={record.state} language={language}/>{record.transition && <> · <code>{record.transition}</code></>}</p>
    <p className="certificate-field-hint">{t(RENEWAL_TEXT.notLegal)}</p>
    {!online && <p role="status">{t(RENEWAL_TEXT.offline)}</p>}
    {!owner && online && <p role="status">{t(RENEWAL_TEXT.loading)}</p>}
    {stale && <p role="alert">{t(RENEWAL_TEXT.stale)}</p>}
    {recorded && !failure && <p role="status">{t(RENEWAL_TEXT.recorded)}</p>}
    {failure && <p role="alert">{pending ? t(RENEWAL_TEXT.unknown) : conflict ? t(RENEWAL_TEXT.conflict) : t(RENEWAL_TEXT.failed)}{failure.code && <> · <code>{failure.code}</code></>}</p>}
    {!allowed.length ? <p>{t(RENEWAL_TEXT.noSteps)}</p> : <form noValidate aria-label={t(RENEWAL_TEXT.next)} onSubmit={event => { event.preventDefault(); void submit() }}>
      <EnterpriseFormGrid disabled={!may}><legend>{t(RENEWAL_TEXT.next)}</legend>
        {select('action', RENEWAL_TEXT.step, action || null, Object.fromEntries(allowed.map(a => [a, RENEWAL_ACTIONS[a]])), value => choose((value ?? '') as RenewalTransition | ''))}
        {action === 'VALIDATE' && REQUIREMENT_FIELDS.map(([key, label]) => select(key, label, decision[key], STATUS, value => patch({ [key]: value as RenewalRequirementStatus | null })))}
        {action === 'RECORD_APPLICATION' && <>
          <EnterpriseField htmlFor={`${id}-applicationDate`} label={t(RENEWAL_TEXT.applicationDate)}><input id={`${id}-applicationDate`} type="date" disabled={!may} value={decision.applicationDate ?? ''} onChange={event => patch({ applicationDate: event.target.value || null })}/></EnterpriseField>
          <EnterpriseField htmlFor={`${id}-authorityReference`} label={t(RENEWAL_TEXT.authorityReference)}><input id={`${id}-authorityReference`} maxLength={4096} disabled={!may} value={decision.authorityReference ?? ''} onChange={event => patch({ authorityReference: event.target.value || null })}/></EnterpriseField>
        </>}
        {action === 'VERIFY' && <>{MATCH_FIELDS.map(([key, label]) => select(key, label, decision[key], MATCH, value => patch({ [key]: value as RenewalMatch | null })))}<p className="certificate-field-hint">{t(RENEWAL_TEXT.sod)}</p></>}
        {action === 'COMPLETE' && <p className="certificate-field-hint">{t(RENEWAL_TEXT.sod)}</p>}
      </EnterpriseFormGrid>
      {action === 'RECORD_ISSUED_CANDIDATE' && <><CertificateReplacementPicker vessel={vessel} owner={owner ?? ''} originalId={record.certificateId} value={replacement} language={language} disabled={!may} onChange={setReplacement}/>
        {!replacement && <p className="certificate-field-hint">{t(RENEWAL_TEXT.replacementMissing)}</p>}</>}
      {action === 'REJECT' && <><p>{t(RENEWAL_TEXT.evidence)}</p><CertificateDocumentPicker vessel={vessel} owner={owner ?? ''} value={decision.evidence} language={language} disabled={!may} onChange={evidence => patch({ evidence })}/></>}
      <EnterpriseActions aria-label={t(RENEWAL_TEXT.next)}>
        {conflict && <Button variant="outline" type="button" disabled={busy} onClick={() => { setFailure(null); setAction(''); onApplied(record) }}>{t(RENEWAL_TEXT.reload)}</Button>}
        <Button type="submit" disabled={busy || !online || !enabled || !owner || stale || conflict || !pending && !action}>{pending ? t(RENEWAL_TEXT.retrySame) : t(RENEWAL_TEXT.submit)}</Button>
      </EnterpriseActions>
    </form>}
  </section>
}
