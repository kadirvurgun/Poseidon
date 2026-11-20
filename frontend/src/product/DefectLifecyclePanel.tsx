import { localizedCopy } from '../i18n/messages'
import type { Locale as AppLocale } from '../i18n'
import { useEffect, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { ApiError } from './api'
import { criticalities, decodeLifecycle, freezeTechnicalCommand, lifecyclePath, sendTechnicalCommand, technicalApi, technicalFailure, type DefectLifecycle,
  type DefectLifecycleCommand, type TechnicalAttempt, type TechnicalIssue } from './technical-records-api'
import { TechnicalBanner, TechnicalField } from './TechnicalRecordParts'
import './technical-records.css'

export const technicalCopy = localizedCopy('TechnicalRecords_tsx_copy')
export type TechnicalCopy = (typeof technicalCopy)[AppLocale]
export const label = (t: TechnicalCopy, prefix: string, value: string) => {
  const key = (prefix + value) as keyof TechnicalCopy
  return typeof t[key] === 'string' ? t[key] as string : value
}

type Kind = 'ASSESS' | 'SAFEGUARD' | 'LIFT' | 'ACTION' | 'VERIFY' | 'EFFECTIVENESS' | 'CLOSE'
type Form = { kind: Kind; criticality: string; measureKind: string; text: string; temporary: boolean; expiresAt: string; outcome: string; measure: string }
const route: Record<Kind, string> = { ASSESS: '/assess', SAFEGUARD: '/safeguards', LIFT: '/lift', ACTION: '/actions', VERIFY: '/verify', EFFECTIVENESS: '/effectiveness', CLOSE: '/close' }
const minimum: Partial<Record<Kind, number>> = { ASSESS: 10, LIFT: 10, VERIFY: 10, EFFECTIVENESS: 10, CLOSE: 10 }

// DEV-055 / T-S8-004: lifecycle records for one owning defect declaration. Every badge comes from the
// owning API; a CLOSED stage is shown as a recorded human decision, never as a green "fixed" state.
export function command(form: Form, revision: number): DefectLifecycleCommand {
  const base: DefectLifecycleCommand = { expectedRevision: revision, criticality: null, measureKind: null, text: form.text, temporary: null, expiresAt: null, outcome: null, defectRevision: null }
  if (form.kind === 'ASSESS') return { ...base, criticality: form.criticality }
  if (form.kind === 'SAFEGUARD') return { ...base, measureKind: form.measureKind, temporary: form.temporary,
    expiresAt: form.temporary && form.expiresAt ? new Date(form.expiresAt + 'Z').toISOString().replace(/\.\d{3}Z$/, 'Z') : null }
  if (form.kind === 'VERIFY' || form.kind === 'EFFECTIVENESS') return { ...base, outcome: form.outcome }
  return base
}

export function DefectLifecyclePanel({ vessel, defect, defectRevision, csrf, language }: { vessel: string; defect: string; defectRevision: number; csrf: string; language: AppLocale }) {
  const t = technicalCopy[language]
  const [data, setData] = useState<DefectLifecycle | null>(null), [failed, setFailed] = useState<ApiError | null>(null), [reload, setReload] = useState(0)
  const [form, setForm] = useState<Form | null>(null), [issue, setIssue] = useState<TechnicalIssue>(null), [busy, setBusy] = useState(false)
  const [pending, setPending] = useState<TechnicalAttempt | null>(null)
  useEffect(() => {
    const controller = new AbortController()
    technicalApi.lifecycle(vessel, defect, controller.signal).then(value => { if (!controller.signal.aborted) { setFailed(null); setData(value) } })
      .catch(error => { if (!controller.signal.aborted) setFailed(error instanceof ApiError ? error : new ApiError(0, 'network')) })
    return () => controller.abort()
  }, [vessel, defect, defectRevision, reload])

  async function run(attempt: TechnicalAttempt) {
    setBusy(true); setIssue(null); setPending(attempt)
    try {
      const outcome = await sendTechnicalCommand<unknown>(attempt, csrf, new AbortController().signal)
      setPending(null); setForm(null); setData(decodeLifecycle(outcome, vessel, defect))
    } catch (error) {
      const kind = technicalFailure(error); setIssue(kind)
      // An unresolved outcome keeps its key and body for an exact retry; a decided failure releases it.
      if (kind !== 'offline' && !(error instanceof ApiError && error.status >= 500)) setPending(null)
    } finally { setBusy(false) }
  }
  function start() {
    void run(freezeTechnicalCommand('POST', lifecyclePath(vessel, defect, '/start'), { expectedRevision: 0, criticality: null, measureKind: null, text: null, temporary: null,
      expiresAt: null, outcome: null, defectRevision }))
  }
  function submit() {
    if (!form || !data) return
    const suffix = form.kind === 'LIFT' ? `/safeguards/${form.measure}/lift` : route[form.kind]
    void run(freezeTechnicalCommand('POST', lifecyclePath(vessel, defect, suffix), command(form, data.revision)))
  }
  const open = (kind: Kind, measure = '') => { setIssue(null); setForm({ kind, criticality: '', measureKind: 'SAFEGUARD', text: '', temporary: false, expiresAt: '', outcome: '', measure }) }
  const valid = !!form && form.text.trim().length >= (minimum[form.kind] ?? 1) && (form.kind !== 'ASSESS' || criticalities.includes(form.criticality as typeof criticalities[number])) &&
    (form.kind !== 'SAFEGUARD' || !form.temporary || !!form.expiresAt) && (form.kind !== 'VERIFY' && form.kind !== 'EFFECTIVENESS' || !!form.outcome)

  if (failed) return <section className="technical-panel" aria-label={t.lcTitle}><h2>{t.lcTitle}</h2>
    <div role="alert"><p>{failed.status === 403 ? t.forbidden : t.error}</p><Button variant="outline" onClick={() => setReload(n => n + 1)}>{t.reload}</Button></div></section>
  if (!data) return <section className="technical-panel" aria-label={t.lcTitle}><h2>{t.lcTitle}</h2><p role="status">{t.loading}</p></section>
  const can = data.can
  return <section className="technical-panel technical-workspace" aria-label={t.lcTitle} data-testid="defect-lifecycle">
    <h2>{t.lcTitle}</h2>
    <p className="technical-note">{t.lcNotice}</p>
    <TechnicalBanner issue={issue} t={t} onReload={() => { setIssue(null); setPending(null); setReload(n => n + 1) }}/>
    <div className="technical-status">
      <Badge tone={data.stage === 'CLOSED' ? 'neutral' : 'warning'} data-stage={data.stage}>{t.stage}: {label(t, 'st_', data.stage)}</Badge>
      {data.critical && <Badge tone="danger">{t.critical}</Badge>}
      <span>{t.assessedCriticality}: {data.assessedCriticality ? label(t, 'cr_', data.assessedCriticality) : t.unknown}</span>
      <span>{t.declaredCriticality}: {data.declaredCriticality ? label(t, 'cr_', data.declaredCriticality) : t.unknown}</span>
    </div>
    {!data.started && <p>{t.lcNotStarted}</p>}
    {data.alerts.length > 0 && <div role="alert" className="technical-alerts"><h3>{t.alerts}</h3><ul>{data.alerts.map((a, i) =>
      <li key={a.code + i} data-blocking={a.blocking}>{label(t, 'al_', a.code)}{a.at && <> · <time dateTime={a.at}>{a.at}</time></>}</li>)}</ul></div>}
    <h3>{t.measures}</h3>
    {data.measures.length === 0 ? <p>{t.noMeasures}</p> : <ul className="technical-list">{data.measures.map(m => <li key={m.id}>
      <Badge tone={m.state === 'EXPIRED' ? 'danger' : m.state === 'ACTIVE' ? 'warning' : 'neutral'} data-state={m.state}>{label(t, 'ms_', m.state)}</Badge> {label(t, 'mk_', m.kind)}
      {m.temporary && m.expiresAt && <> · {t.expiresAt}: <time dateTime={m.expiresAt}>{m.expiresAt}</time></>}
      {can.lift && (m.state === 'ACTIVE' || m.state === 'EXPIRED') && <Button variant="outline" size="sm" disabled={busy || !!pending} onClick={() => open('LIFT', m.id)}>{t.lift}</Button>}
    </li>)}</ul>}
    {data.stage !== 'CLOSED' && data.closeBlockers.length > 0 && <><h3>{t.blockers}</h3><ul>{data.closeBlockers.map(code => <li key={code}>{label(t, 'bl_', code)} · <code>{code}</code></li>)}</ul></>}
    <div className="technical-actions" aria-label={t.actions}>
      {can.start && <Button disabled={busy || !!pending} onClick={start}>{t.start}</Button>}
      {can.assess && <Button variant="outline" disabled={busy || !!pending} onClick={() => open('ASSESS')}>{t.assess}</Button>}
      {can.safeguard && <Button variant="outline" disabled={busy || !!pending} onClick={() => open('SAFEGUARD')}>{t.safeguard}</Button>}
      {can.action && <Button variant="outline" disabled={busy || !!pending} onClick={() => open('ACTION')}>{t.action}</Button>}
      {can.verify && <Button variant="outline" disabled={busy || !!pending} onClick={() => open('VERIFY')}>{t.verify}</Button>}
      {can.effectiveness && <Button variant="outline" disabled={busy || !!pending} onClick={() => open('EFFECTIVENESS')}>{t.effectiveness}</Button>}
      {can.close && <Button disabled={busy || !!pending} onClick={() => open('CLOSE')}>{t.close}</Button>}
      {pending && <Button variant="outline" disabled={busy} onClick={() => void run(pending)}>{t.retry}</Button>}
    </div>
    {form && <form className="technical-form" aria-label={label(t, 'k_', form.kind)} onSubmit={event => { event.preventDefault(); if (valid) submit() }}>
      <h3>{label(t, 'k_', form.kind)}</h3>
      {form.kind === 'ASSESS' && <TechnicalField label={t.criticality}><select value={form.criticality} onChange={e => setForm({ ...form, criticality: e.target.value })}>
        <option value="">{t.unknown}</option>{criticalities.map(c => <option key={c} value={c}>{label(t, 'cr_', c)}</option>)}</select></TechnicalField>}
      {form.kind === 'SAFEGUARD' && <>
        <TechnicalField label={t.measureKind}><select value={form.measureKind} onChange={e => setForm({ ...form, measureKind: e.target.value })}>
          <option value="SAFEGUARD">{t.mk_SAFEGUARD}</option><option value="RESTRICTION">{t.mk_RESTRICTION}</option></select></TechnicalField>
        <label className="technical-check"><input type="checkbox" checked={form.temporary} onChange={e => setForm({ ...form, temporary: e.target.checked })}/>{t.temporary}</label>
        {form.temporary && <TechnicalField label={t.expiresAt}><input type="datetime-local" value={form.expiresAt} onChange={e => setForm({ ...form, expiresAt: e.target.value })}/></TechnicalField>}
      </>}
      {(form.kind === 'VERIFY' || form.kind === 'EFFECTIVENESS') && <TechnicalField label={t.outcome}><select value={form.outcome} onChange={e => setForm({ ...form, outcome: e.target.value })}>
        <option value="">{t.unknown}</option>{(form.kind === 'VERIFY' ? ['VERIFIED', 'NOT_VERIFIED'] : ['EFFECTIVE', 'NOT_EFFECTIVE']).map(o => <option key={o} value={o}>{label(t, 'oc_', o)}</option>)}</select></TechnicalField>}
      <TechnicalField label={t.statement} hint={minimum[form.kind] ? t.minimumTen : undefined}><textarea rows={3} maxLength={4000} value={form.text} onChange={e => setForm({ ...form, text: e.target.value })}/></TechnicalField>
      <div className="technical-actions"><Button type="submit" disabled={busy || !valid}>{t.record}</Button><Button variant="outline" disabled={busy} onClick={() => setForm(null)}>{t.cancel}</Button></div>
    </form>}
    <h3>{t.records}</h3>
    {data.records.length === 0 ? <p>{t.noRecords}</p> : <ol className="technical-records">{data.records.map(r => <li key={r.id}>
      <strong>{label(t, 'k_', r.kind)}</strong> → {label(t, 'st_', r.stageAfter)}{r.outcome && <> · {label(t, 'oc_', r.outcome)}</>}{r.criticality && <> · {label(t, 'cr_', r.criticality)}</>}
      <p className="technical-text">{r.text}</p><small><time dateTime={r.recordedAt}>{r.recordedAt}</time> · <code>{r.actorId}</code> · r{r.defectRevision}</small>
    </li>)}</ol>}
  </section>
}
