import { useEffect, useState } from 'react'
import { localizedText } from '../i18n/messages'
import { type Locale as AppLocale } from '../i18n'
import { Button } from '../components/ui/button'
import * as service from './control-evidence-api'

// DEV-028 lifecycle + DEV-029 inverse evidence view for one selected control. Loaded only on
// demand; approval/publication are never shown as verification, completion or effectiveness.
const K = {
  heading: 'pairs.m_1ef91045b475b974', open: 'pairs.m_5f54b6d1e4675ea5', state: 'pairs.m_998e36fd59de457a', note: 'pairs.m_18ee548afc344b02',
  reason: 'pairs.m_cae5678ec9131c59', history: 'pairs.m_8530283d0ed6947f', saved: 'pairs.m_36c7afe901875bc8', failed: 'pairs.m_358abceaaa3ee990',
  unavailable: 'pairs.m_027dc262b7fbd095', supporting: 'pairs.m_cc38ff2f59dfb8aa', none: 'pairs.m_9437f74f16d45312', pinned: 'pairs.m_3544d26cbfe22570'
} as const
const STATES: Record<service.ControlState, string> = { DRAFT: 'pairs.m_474c37ed25025393', IN_REVIEW: 'pairs.m_458e2b38d4c815f2', APPROVED: 'pairs.m_445ccc551acc5d3b', PUBLISHED: 'pairs.m_18307695a3cdd392', RETIRED: 'pairs.m_bc8a6e023d55eff5' }
const REASONS: Record<service.LifecycleEvent['reasonCode'], string> = { SUBMIT_FOR_REVIEW: 'pairs.m_e8bfa7293d7befa7', RETURN_FOR_CORRECTION: 'pairs.m_889f6e7487cd555e', APPROVE: 'pairs.m_3c5062665dcad1de', PUBLISH: 'pairs.m_bf8498e5cb2f6ce1', RETIRE: 'pairs.m_e7b1434304021219', CONTENT_REVISED: 'pairs.m_a43e9e79ef0e42cd' }
export const ORPHAN_REASONS: Record<service.OrphanReason, string> = { CRITICALITY_UNKNOWN: 'pairs.m_bd7660f8ea8e0811', REQUIREMENT_LINK_MISSING: 'pairs.m_69c8580208acc8c2', OWNER_MISSING: 'pairs.m_fe74d79deff90fe1' }
export const FINDINGS = { heading: 'pairs.m_848a93989244a54a', unavailable: 'pairs.m_965193f060525feb' } as const
// Offered actions follow the server state machine; the server still decides every gate.
function actions(state: service.ControlState, canWrite: boolean, canReview: boolean): service.LifecycleReason[] {
  if (state === 'DRAFT') return canWrite ? ['SUBMIT_FOR_REVIEW'] : []
  if (state === 'IN_REVIEW') return canReview ? ['RETURN_FOR_CORRECTION', 'APPROVE'] : []
  if (state === 'APPROVED') return canReview ? ['PUBLISH'] : []
  if (state === 'PUBLISHED') return canReview ? ['RETIRE'] : []
  return []
}
type Props = { vessel: string; control: service.Row; language: AppLocale; csrf: string; offline: boolean; locked: boolean; onChanged: () => void; timeLabel: (state: service.Row['timeState']) => string }
export function ControlLifecycle({ vessel, control, language, csrf, offline, locked, onChanged, timeLabel }: Props) {
  const [open, setOpen] = useState(false), [reload, setReload] = useState(0)
  const [data, setData] = useState<service.Lifecycle | null>(null), [support, setSupport] = useState<Awaited<ReturnType<typeof service.supportingEvidence>> | null>(null)
  const [error, setError] = useState(false), [failed, setFailed] = useState(false), [notice, setNotice] = useState(false), [busy, setBusy] = useState(false)
  const [rationale, setRationale] = useState(''), [frozen, setFrozen] = useState<{ body: string; key: string } | null>(null)
  useEffect(() => {
    if (!open || offline) return
    const c = new AbortController(); let active = true
    Promise.all([service.lifecycle(vessel, control.id, 0, c.signal), service.supportingEvidence(vessel, control.id, 0, c.signal)])
      .then(([value, evidence]) => { if (active) { setData(value); setSupport(evidence); setError(false) } })
      .catch(() => { if (active) { setData(null); setSupport(null); setError(true) } })
    return () => { active = false; c.abort() }
  }, [open, offline, vessel, control.id, control.revision, reload])
  async function run(reason: service.LifecycleReason) {
    if (!data || busy || locked || offline || !rationale.trim()) return
    const body = JSON.stringify({ expectedVersion: data.version, revision: data.revision, reasonCode: reason, rationale: rationale.trim() } satisfies service.LifecycleCommand)
    // Retain the original key for an identical unresolved command; a new body is a new command.
    const command = frozen?.body === body ? frozen : { body, key: crypto.randomUUID() }
    setFrozen(command); setBusy(true); setFailed(false); setNotice(false)
    try {
      await service.transition(vessel, control.id, command, csrf, new AbortController().signal)
      setFrozen(null); setRationale(''); setNotice(true); setReload(x => x + 1); onChanged()
    } catch { setFailed(true) } finally { setBusy(false) }
  }
  if (!open) return <Button variant="outline" type="button" disabled={offline} onClick={() => setOpen(true)}>{localizedText(language, K.open)}</Button>
  return <section aria-label={localizedText(language, K.heading)}>
    <h3>{localizedText(language, K.heading)}</h3>
    <p>{localizedText(language, K.note)}</p>
    {error && <p role="alert">{localizedText(language, K.unavailable)}</p>}
    {failed && <p role="alert">{localizedText(language, K.failed)}</p>}
    {notice && <p role="status">{localizedText(language, K.saved)}</p>}
    {data && <>
      <p>{localizedText(language, K.state)}: <strong data-state={data.state}>{localizedText(language, STATES[data.state])}</strong></p>
      {actions(data.state, data.canWrite, data.canReview).length > 0 && <>
        <label>{localizedText(language, K.reason)}<textarea value={rationale} maxLength={2000} disabled={busy || locked} onChange={e => setRationale(e.target.value)}/></label>
        <div>{actions(data.state, data.canWrite, data.canReview).map(reason => <Button key={reason} type="button" variant="outline" disabled={busy || locked || offline || !rationale.trim()} onClick={() => void run(reason)}>{localizedText(language, REASONS[reason])}</Button>)}</div>
      </>}
      <h4>{localizedText(language, K.history)}</h4>
      <ol>{data.events.map(e => <li key={e.sequence}>{e.savedAt} · {localizedText(language, REASONS[e.reasonCode])} · {localizedText(language, STATES[e.fromState])} → {localizedText(language, STATES[e.toState])} · r{e.controlRevision}{e.rationale ? ` · ${e.rationale}` : ''}</li>)}</ol>
    </>}
    {support && <section aria-label={localizedText(language, K.supporting)}>
      <h4>{localizedText(language, K.supporting)}</h4>
      {support.items.length === 0 ? <p>{localizedText(language, K.none)}</p> : <ul>{support.items.map(item => <li key={item.evidence.id}>
        {item.evidence.content.title ?? item.evidence.id} · r{item.evidence.revision} · <span data-time-state={item.evidence.timeState}>{timeLabel(item.evidence.timeState)}</span>
        {item.evidence.file ? ` · ${item.evidence.file.checksum}` : ''}{item.evidence.content.provenance ? ` · ${item.evidence.content.provenance}` : ''}
        {!item.pinCurrent && <> · {localizedText(language, K.pinned)}</>}
      </li>)}</ul>}
    </section>}
  </section>
}
