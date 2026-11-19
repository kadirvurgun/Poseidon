import { useState } from 'react'
import { localizedText } from '../i18n/messages'
import { type Locale as AppLocale } from '../i18n'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import type { ApplicabilityReview, PassportSource } from './compliance-api'
import { ComplianceBlockers } from './ComplianceBlockers'
import * as service from './compliance-override-api'

export const CANDIDATE_TEXT = {
  generate: 'pairs.m_5a0e3c2f1b7d4e83', created: 'pairs.m_5a0e3c2f1b7d4e84', failed: 'pairs.m_5a0e3c2f1b7d4e71',
  sources: 'pairs.m_5a0e3c2f1b7d4e85', unknown: 'pairs.m_5a0e3c2f1b7d4e86', qualifiedAt: 'pairs.m_5a0e3c2f1b7d4e87', open: 'pairs.m_5a0e3c2f1b7d4e88',
  none: 'pairs.m_5a0e3c2f1b7d4e8c', rule: 'pairs.m_cc646e6a24f49b58',
} as const

// DEV-041: generate DRAFT control candidates for current, unlinked APPLICABLE requirements. Candidates are never
// reviewed, published, completed or verified here; their owner/frequency/evidence/verification blockers are shown.
export function ControlCandidates({ vessel, review, uncontrolled, language, csrf, offline, onCreated }: {
  vessel: string; review: ApplicabilityReview; uncontrolled: number | null; language: AppLocale; csrf?: string; offline: boolean; onCreated: () => void
}) {
  const t = (key: string) => localizedText(language, key)
  const [busy, setBusy] = useState(false), [failed, setFailed] = useState<string | null>(null), [result, setResult] = useState<service.CandidateOutcome | null>(null)
  const [frozen, setFrozen] = useState<{ body: string; key: string } | null>(null)
  if (!review.current || !review.evaluationId || !review.inputHash || !review.ruleSetHash || !uncontrolled) return result ? <CandidateResult result={result} language={language}/> : null
  const evaluation = review.evaluationId
  async function generate() {
    if (busy || offline || !csrf) return
    const body = JSON.stringify({ evaluationId: evaluation, inputHash: review.inputHash, ruleSetHash: review.ruleSetHash })
    const command = frozen?.body === body ? frozen : { body, key: crypto.randomUUID() }
    setFrozen(command); setBusy(true); setFailed(null)
    try { setResult(await service.generateCandidates(vessel, evaluation, command, csrf, new AbortController().signal)); setFrozen(null); onCreated() }
    catch (e) { setFailed(e && typeof e === 'object' && 'code' in e && typeof e.code === 'string' ? e.code : 'service_unavailable') }
    finally { setBusy(false) }
  }
  return <div className="control-candidates">
    <Button type="button" variant="outline" disabled={busy || offline || !csrf} onClick={() => void generate()}>{t(CANDIDATE_TEXT.generate)}</Button>
    {failed && <p role="alert" data-code={failed}>{t(CANDIDATE_TEXT.failed)} ({failed})</p>}
    {result && <CandidateResult result={result} language={language}/>}
  </div>
}
function CandidateResult({ result, language }: { result: service.CandidateOutcome; language: AppLocale }) {
  return <section role="status" aria-label={localizedText(language, CANDIDATE_TEXT.generate)}>
    <p>{localizedText(language, CANDIDATE_TEXT.created)}</p>
    <ul>{result.created.map(c => <li key={c.controlId} data-control={c.controlId}><Badge tone="warning">{c.controlState}</Badge> {c.title} · {c.requirementId} / {c.requirementRevision}<ComplianceBlockers codes={c.blockers} language={language}/></li>)}</ul>
  </section>
}

// DEV-042 source freshness drill-down. UNKNOWN freshness is shown as such; it is never rendered as fresh/current.
export function PassportSources({ sources, language, onNavigate }: { sources: PassportSource[]; language: AppLocale; onNavigate: (href: string) => void }) {
  const t = (key: string) => localizedText(language, key)
  return <section aria-label={t(CANDIDATE_TEXT.sources)}><h3>{t(CANDIDATE_TEXT.sources)}</h3>
    {!sources.length && <p>{t(CANDIDATE_TEXT.none)}</p>}
    {sources.map(s => <details key={`${s.sourceId}/${s.sourceRevision}`}><summary>{s.sourceId} / {s.sourceRevision} · <span data-freshness={s.freshness}>{t(CANDIDATE_TEXT.unknown)}</span></summary>
      <p>{t(CANDIDATE_TEXT.qualifiedAt)}: {s.qualifiedAt ?? localizedText(language, 'pairs.m_a5a7675bd7000dbd')}</p>
      <p>{t(CANDIDATE_TEXT.rule)}: {s.ruleIds.join(' · ')}</p>
      <ComplianceBlockers codes={s.blockers} language={language}/>
      <Button variant="outline" type="button" onClick={() => onNavigate(s.href)}>{t(CANDIDATE_TEXT.open)}</Button>
    </details>)}
  </section>
}
