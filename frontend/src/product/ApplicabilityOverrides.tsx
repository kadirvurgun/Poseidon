import { useEffect, useState } from 'react'
import { localizedText } from '../i18n/messages'
import { type Locale as AppLocale } from '../i18n'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import type { ApplicabilityReview } from './compliance-api'
import * as service from './compliance-override-api'

// DEV-040 manual override review (60.03 §10). Honest by construction: the authority is UNCONFIGURED, so the
// UI says an override requires a configured authority and never shows an approved or active state. Proposals
// keep their original idempotency key while unresolved; the server decides every gate (maker-checker, grant,
// binding, overlap). The recorded evaluation result is shown unchanged.
export const OVERRIDE_TEXT = {
  heading: 'pairs.m_5a0e3c2f1b7d4e61', authority: 'pairs.m_5a0e3c2f1b7d4e62', unchanged: 'pairs.m_5a0e3c2f1b7d4e63', none: 'pairs.m_5a0e3c2f1b7d4e64',
  propose: 'pairs.m_5a0e3c2f1b7d4e65', action: 'pairs.m_5a0e3c2f1b7d4e66', reason: 'pairs.m_5a0e3c2f1b7d4e67', rationale: 'pairs.m_5a0e3c2f1b7d4e68',
  validFrom: 'pairs.m_5a0e3c2f1b7d4e69', expiresAt: 'pairs.m_5a0e3c2f1b7d4e6a', reviewAt: 'pairs.m_5a0e3c2f1b7d4e6b', submit: 'pairs.m_5a0e3c2f1b7d4e6c',
  withdraw: 'pairs.m_5a0e3c2f1b7d4e6d', approve: 'pairs.m_5a0e3c2f1b7d4e6e', reject: 'pairs.m_5a0e3c2f1b7d4e6f', decisionReason: 'pairs.m_5a0e3c2f1b7d4e70',
  failed: 'pairs.m_5a0e3c2f1b7d4e71', saved: 'pairs.m_5a0e3c2f1b7d4e72', validity: 'pairs.m_5a0e3c2f1b7d4e73', baseResult: 'pairs.m_5a0e3c2f1b7d4e74',
  unavailable: 'pairs.m_5a0e3c2f1b7d4e75', rule: 'pairs.m_cc646e6a24f49b58', state: 'pairs.m_ac8b056f75c10eff', evidence: 'pairs.m_29983c02e9b2276d',
  notCurrent: 'pairs.m_5a0e3c2f1b7d4e76', notConfigured: 'pairs.m_5a0e3c2f1b7d4e82',
} as const
export const OVERRIDE_ACTIONS: Record<service.OverrideAction, string> = { ACCEPT_INTERPRETATION: 'pairs.m_5a0e3c2f1b7d4e77', REQUIRE_ADDITIONAL_CONTROL: 'pairs.m_5a0e3c2f1b7d4e78' }
export const OVERRIDE_REASONS: Record<string, string> = {
  VERIFIED_LEGAL_INTERPRETATION: 'pairs.m_5a0e3c2f1b7d4e79', FLAG_STATE_EXEMPTION_REFERENCE: 'pairs.m_5a0e3c2f1b7d4e7a', CLASS_EXEMPTION_REFERENCE: 'pairs.m_5a0e3c2f1b7d4e7b',
  ADDITIONAL_PRECAUTION: 'pairs.m_5a0e3c2f1b7d4e7c', SOURCE_CONFLICT_PRECAUTION: 'pairs.m_5a0e3c2f1b7d4e7d',
}
export const OVERRIDE_STATES: Record<service.Override['state'], string> = {
  DRAFT: 'pairs.m_474c37ed25025393', IN_REVIEW: 'pairs.m_458e2b38d4c815f2', ACTIVE: 'pairs.m_5a0e3c2f1b7d4e7e', REVOKED: 'pairs.m_e4b5dd2deec128b7',
  EXPIRED: 'pairs.m_4ddfd606ba8bd458', SUPERSEDED: 'pairs.m_61ecb1880ed2d354',
}
export const OVERRIDE_VALIDITY: Record<service.Override['currentValidity'], string> = {
  NOT_ACTIVE: 'pairs.m_5a0e3c2f1b7d4e7f', REQUIRES_REVIEW: 'pairs.m_5a0e3c2f1b7d4e80', EXPIRED: 'pairs.m_4ddfd606ba8bd458', NOT_YET_VALID: 'pairs.m_5a0e3c2f1b7d4e81',
  CURRENT: 'pairs.m_3d20d517375ac03d', REVOKED: 'pairs.m_e4b5dd2deec128b7', SUPERSEDED: 'pairs.m_61ecb1880ed2d354',
}
type Props = { vessel: string; review: ApplicabilityReview; evidence: { id: string; title: string }[]; language: AppLocale; csrf?: string; offline: boolean }
const local = (value: string) => value ? new Date(value).toISOString() : ''
export function ApplicabilityOverrides({ vessel, review, evidence, language, csrf, offline }: Props) {
  const t = (key: string) => localizedText(language, key)
  const [data, setData] = useState<service.OverrideList | null>(null), [error, setError] = useState(false), [reload, setReload] = useState(0)
  const [failed, setFailed] = useState<string | null>(null), [notice, setNotice] = useState(false), [busy, setBusy] = useState(false)
  const [frozen, setFrozen] = useState<{ body: string; key: string } | null>(null)
  const rules = review.current ? review.results : []
  const [rule, setRule] = useState(''), [action, setAction] = useState<service.OverrideAction>('REQUIRE_ADDITIONAL_CONTROL')
  const [reason, setReason] = useState<string>(service.overrideReasons.REQUIRE_ADDITIONAL_CONTROL[0]), [rationale, setRationale] = useState('')
  const [validFrom, setValidFrom] = useState(''), [expiresAt, setExpiresAt] = useState(''), [reviewAt, setReviewAt] = useState(''), [refs, setRefs] = useState<string[]>([])
  const [decisionReason, setDecisionReason] = useState('')
  useEffect(() => {
    if (offline) return
    const c = new AbortController(); let active = true
    service.loadOverrides(vessel, 0, c.signal).then(v => { if (active) { setData(v); setError(false) } }).catch(() => { if (active) { setData(null); setError(true) } })
    return () => { active = false; c.abort() }
  }, [vessel, offline, reload])
  async function run(send: (frozenCommand: { body: string; key: string }) => Promise<unknown>, body: string) {
    if (busy || offline || !csrf) return
    const command = frozen?.body === body ? frozen : { body, key: crypto.randomUUID() }
    setFrozen(command); setBusy(true); setFailed(null); setNotice(false)
    try { await send(command); setFrozen(null); setNotice(true); setRationale(''); setDecisionReason(''); setReload(x => x + 1) }
    catch (e) { setFailed(e && typeof e === 'object' && 'code' in e && typeof e.code === 'string' ? e.code : 'service_unavailable') }
    finally { setBusy(false) }
  }
  const selected = rules.find(r => `${r.ruleId}/${r.revision}` === rule)
  const ready = !!selected && rationale.trim().length >= 20 && !!validFrom && !!expiresAt && !!reviewAt && (action !== 'ACCEPT_INTERPRETATION' || refs.length > 0)
  function propose() {
    if (!selected || !review.evaluationId || !review.inputHash || !review.ruleSetHash) return
    const body: service.OverrideCommand = { evaluationId: review.evaluationId, inputHash: review.inputHash, ruleSetHash: review.ruleSetHash, ruleId: selected.ruleId, ruleRevision: selected.revision,
      action, reasonCode: reason, purpose: 'COMPLIANCE_REVIEW', rationale: rationale.trim(), evidenceIds: refs, validFrom: local(validFrom), expiresAt: local(expiresAt), reviewAt: local(reviewAt) }
    void run(f => service.proposeOverride(vessel, f, csrf!, new AbortController().signal), JSON.stringify(body))
  }
  function decide(item: service.Override, decision: service.OverrideDecision) {
    if (!decisionReason.trim()) return
    const body = JSON.stringify({ expectedVersion: item.version, expectedSequence: item.sequence, decision, rationale: decisionReason.trim() })
    void run(f => service.decideOverride(vessel, item.id, f, csrf!, new AbortController().signal), body)
  }
  return <section aria-label={t(OVERRIDE_TEXT.heading)} className="applicability-overrides">
    <h4>{t(OVERRIDE_TEXT.heading)}</h4>
    <p data-authority="UNCONFIGURED"><Badge tone="warning">{t(OVERRIDE_TEXT.notConfigured)}</Badge> {t(OVERRIDE_TEXT.authority)}</p>
    <p>{t(OVERRIDE_TEXT.unchanged)}</p>
    {error && <p role="alert">{t(OVERRIDE_TEXT.unavailable)}</p>}
    {failed && <p role="alert" data-code={failed}>{t(OVERRIDE_TEXT.failed)} ({failed})</p>}
    {notice && <p role="status">{t(OVERRIDE_TEXT.saved)}</p>}
    {data && <>
      {data.items.length === 0 ? <p>{t(OVERRIDE_TEXT.none)}</p> : <ul>{data.items.map(item => <li key={item.id} data-override={item.id}>
        <p>{t(OVERRIDE_TEXT.rule)}: {item.ruleId} / {item.ruleRevision} · {t(OVERRIDE_TEXT.baseResult)}: {item.baseResult}</p>
        <p>{t(OVERRIDE_ACTIONS[item.action])} · {t(OVERRIDE_REASONS[item.reasonCode] ?? OVERRIDE_TEXT.reason)}</p>
        <p>{t(OVERRIDE_TEXT.state)}: <strong data-state={item.state}>{t(OVERRIDE_STATES[item.state])}</strong> · {t(OVERRIDE_TEXT.validity)}: <span data-validity={item.currentValidity}>{t(OVERRIDE_VALIDITY[item.currentValidity])}</span></p>
        <p>{t(OVERRIDE_TEXT.validFrom)}: {item.validFrom} · {t(OVERRIDE_TEXT.reviewAt)}: {item.reviewAt} · {t(OVERRIDE_TEXT.expiresAt)}: {item.expiresAt}</p>
        {item.rationale && <p>{item.rationale}</p>}
        {item.state === 'IN_REVIEW' && <div>
          <label>{t(OVERRIDE_TEXT.decisionReason)}<textarea value={decisionReason} maxLength={2000} disabled={busy || offline} onChange={e => setDecisionReason(e.target.value)}/></label>
          <Button type="button" variant="outline" disabled={busy || offline || !csrf || !decisionReason.trim()} onClick={() => decide(item, 'WITHDRAW')}>{t(OVERRIDE_TEXT.withdraw)}</Button>
          {data.canDecide && <>
            <Button type="button" variant="outline" disabled={busy || offline || !csrf || !decisionReason.trim()} onClick={() => decide(item, 'APPROVE')}>{t(OVERRIDE_TEXT.approve)}</Button>
            <Button type="button" variant="outline" disabled={busy || offline || !csrf || !decisionReason.trim()} onClick={() => decide(item, 'REJECT')}>{t(OVERRIDE_TEXT.reject)}</Button>
          </>}
        </div>}
      </li>)}</ul>}
      {data.canPropose && (!review.current ? <p>{t(OVERRIDE_TEXT.notCurrent)}</p> : <fieldset disabled={busy || offline || !csrf}>
        <legend>{t(OVERRIDE_TEXT.propose)}</legend>
        <label>{t(OVERRIDE_TEXT.rule)}<select value={rule} onChange={e => setRule(e.target.value)}><option value=""></option>{rules.map(r => <option key={`${r.ruleId}/${r.revision}`} value={`${r.ruleId}/${r.revision}`}>{r.ruleId} / {r.revision} · {r.prediction.result}</option>)}</select></label>
        <label>{t(OVERRIDE_TEXT.action)}<select value={action} onChange={e => { const next = e.target.value as service.OverrideAction; setAction(next); setReason(service.overrideReasons[next][0]) }}>{service.overrideActions.map(a => <option key={a} value={a}>{t(OVERRIDE_ACTIONS[a])}</option>)}</select></label>
        <label>{t(OVERRIDE_TEXT.reason)}<select value={reason} onChange={e => setReason(e.target.value)}>{service.overrideReasons[action].map(r => <option key={r} value={r}>{t(OVERRIDE_REASONS[r])}</option>)}</select></label>
        <label>{t(OVERRIDE_TEXT.rationale)}<textarea value={rationale} minLength={20} maxLength={4000} onChange={e => setRationale(e.target.value)}/></label>
        <label>{t(OVERRIDE_TEXT.validFrom)}<input type="datetime-local" value={validFrom} onChange={e => setValidFrom(e.target.value)}/></label>
        <label>{t(OVERRIDE_TEXT.reviewAt)}<input type="datetime-local" value={reviewAt} onChange={e => setReviewAt(e.target.value)}/></label>
        <label>{t(OVERRIDE_TEXT.expiresAt)}<input type="datetime-local" value={expiresAt} onChange={e => setExpiresAt(e.target.value)}/></label>
        {evidence.length > 0 && <label>{t(OVERRIDE_TEXT.evidence)}<select multiple value={refs} onChange={e => setRefs(Array.from(e.target.selectedOptions, o => o.value).slice(0, 10))}>{evidence.map(x => <option key={x.id} value={x.id}>{x.title}</option>)}</select></label>}
        <Button type="button" disabled={!ready || busy || offline || !csrf} onClick={propose}>{t(OVERRIDE_TEXT.submit)}</Button>
      </fieldset>)}
    </>}
  </section>
}
