import { localizedCopy } from '../i18n/messages'
import type { Locale } from '../i18n'
import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { ApiError, session } from './api'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import { isGuid } from './identifiers'
import * as pms from './pms-api'
import './pms-panels.css'

// DEV-108 measurement points and DEV-109 postponement panels for the technical workspace.
// Every unknown/unconfigured/unvalidated state uses the neutral or open tone; nothing here is
// ever rendered as an approved or "ready" green state.
export const pmsCopy = localizedCopy('PmsWorkspace_tsx_copy')
type Copy = (typeof pmsCopy)[Locale]
type Issue = 'offline' | 'forbidden' | 'error' | 'conflict' | 'unknown' | null
const label = (t: Copy, prefix: string, value: string | null | undefined) => {
  const key = (prefix + (value ?? 'UNKNOWN')) as keyof Copy
  return typeof t[key] === 'string' ? t[key] as string : value ?? '—'
}
const refusal = (t: Copy, code: string | undefined) => {
  const key = ('e_' + (code ?? '')) as keyof Copy
  return typeof t[key] === 'string' ? t[key] as string : t.e_generic
}
export function PmsBadge({ state, text, tone }: { state: string; text: string; tone: 'open' | 'muted' | 'alert' | 'recorded' }) {
  return <span className={`pms-badge pms-badge-${tone}`} data-state={state}>{text}</span>
}
const bandTone = (band: string) => band === 'ALARM' || band === 'WARNING' ? 'alert' : band === 'NORMAL' ? 'recorded' : 'open'
function Banner({ issue, t, code, onReload }: { issue: Issue; t: Copy; code?: string; onReload: () => void }) {
  if (!issue) return null
  const text = issue === 'offline' ? t.offline : issue === 'forbidden' ? t.forbidden : issue === 'conflict' ? t.conflict
    : issue === 'unknown' ? t.unknownOutcome : code ? refusal(t, code) : t.error
  return <div role={issue === 'offline' ? 'status' : 'alert'} className={`pms-banner pms-banner-${issue}`} data-issue={issue}>
    <p>{text}</p>{issue !== 'offline' && <Button variant="outline" size="sm" onClick={onReload}>{t.reload}</Button>}</div>
}
function Field({ name, children }: { name: string; children: ReactNode }) {
  return <label className="pms-field"><span>{name}</span>{children}</label>
}

// Shared single-attempt command runner: the key and body of an unknown outcome are retained
// and resent unchanged on retry; a known refusal clears them.
function useAttempt(onDone: () => void) {
  const attempt = useRef<pms.PmsAttempt | null>(null)
  const [busy, setBusy] = useState(false), [issue, setIssue] = useState<Issue>(null), [code, setCode] = useState<string | undefined>()
  const [notice, setNotice] = useState<string | null>(null)
  const run = async <T,>(next: pms.PmsAttempt | null, onOk?: (value: T) => void) => {
    const command = attempt.current ?? next
    if (!command || busy) return
    if (!navigator.onLine) { setIssue('offline'); return }
    attempt.current = command; setBusy(true); setIssue(null); setCode(undefined); setNotice(null)
    const c = new AbortController(), timer = setTimeout(() => c.abort(), 15000)
    let sent = false
    try {
      const user = await session(c.signal)
      sent = true
      const value = await pms.sendPms<T>(command, user.csrfToken, c.signal)
      attempt.current = null; onOk?.(value); setNotice('saved'); onDone()
    } catch (problem) {
      const status = problem instanceof ApiError ? problem.status : 0
      if (sent && (status === 0 || status >= 500)) { setIssue('unknown'); return }
      attempt.current = null
      setCode(problem instanceof ApiError ? problem.code : undefined)
      const refusedCode = problem instanceof ApiError ? problem.code : undefined
      setIssue(status === 409 && refusedCode === 'version_conflict' ? 'conflict' : status === 401 || status === 403 && refusedCode !== 'separation_of_duties' ? 'forbidden' : 'error')
    } finally { clearTimeout(timer); setBusy(false) }
  }
  return { run, busy, issue, code, notice, pending: () => attempt.current != null, clear: () => { attempt.current = null; setIssue(null); setCode(undefined) } }
}

function useLoad<T>(load: (signal: AbortSignal) => Promise<T>, key: string) {
  const [state, setState] = useState<{ scope: string; value: T | null; issue: Issue }>({ scope: '', value: null, issue: null })
  const [tick, setTick] = useState(0)
  const scope = `${key}#${tick}`
  useEffect(() => {
    if (!navigator.onLine) return
    const c = new AbortController(); let active = true
    load(c.signal).then(v => { if (active) setState({ scope, value: v, issue: null }) }).catch(problem => {
      if (!active) return
      const status = problem instanceof ApiError ? problem.status : 0
      setState({ scope, value: null, issue: [401, 403, 404].includes(status) ? 'forbidden' : 'error' })
    })
    return () => { active = false; c.abort() }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scope])
  // A reload of the same resource keeps the previous projection visible; another resource never does.
  const sameResource = state.scope.startsWith(key + '#')
  const issue: Issue = !navigator.onLine ? 'offline' : state.scope === scope ? state.issue : null
  return { value: sameResource ? state.value : null, issue, reload: () => setTick(n => n + 1) }
}

export function MeasurementPointsPanel({ vessel, asset, language }: { vessel: string; asset: string; language: Locale }) {
  const t = pmsCopy[language]
  const loader = useLoad(useCallback((signal: AbortSignal) => pms.readPoints(vessel, signal), [vessel]), `${vessel}/${asset}`)
  const command = useAttempt(loader.reload)
  const [draft, setDraft] = useState({ code: '', kind: 'RUNNING_HOURS', unit: 'HOUR', source: 'MANUAL', precision: '1', rangeMin: '', rangeMax: '', parent: '' })
  const [reading, setReading] = useState({ point: '', value: '', measuredAt: '', sourceType: 'MANUAL', sourceReference: '' })
  const [derived, setDerived] = useState<{ propagated: number; skipped: number } | null>(null)
  const page = loader.value
  if (loader.issue && !page) return <section className="pms-panel" aria-label={t.titlePoints}><h3>{t.titlePoints}</h3><Banner issue={loader.issue} t={t} onReload={loader.reload}/></section>
  if (!page) return <section className="pms-panel" aria-label={t.titlePoints}><h3>{t.titlePoints}</h3><p role="status">{t.loading}</p></section>
  const items = page.items.filter(p => p.assetId === asset)
  const counters = page.items.filter(p => p.kind === 'RUNNING_HOURS')
  const units = page.units[draft.kind] ?? []
  const savePoint = () => {
    const id = crypto.randomUUID()
    void command.run(command.pending() ? null : { path: pms.pointsPath(vessel), key: crypto.randomUUID(), body: JSON.stringify({ id, expectedVersion: null, assetId: asset,
      code: draft.code.trim(), kind: draft.kind, unit: draft.unit, source: draft.source, precision: Number(draft.precision),
      rangeMin: draft.rangeMin || null, rangeMax: draft.rangeMax || null, parentPointId: isGuid(draft.parent) ? draft.parent : null }) })
  }
  const saveReading = () => {
    if (!isGuid(reading.point)) return
    const at = reading.measuredAt ? new Date(reading.measuredAt) : null
    if (!at || Number.isNaN(at.getTime())) return
    void command.run<{ propagated: unknown[]; skipped: unknown[] }>(command.pending() ? null : { path: `${pms.pointsPath(vessel)}/${encodeURIComponent(reading.point)}/readings`,
      key: crypto.randomUUID(), body: JSON.stringify({ id: crypto.randomUUID(), value: reading.value.trim(), measuredAt: at.toISOString(), sourceType: reading.sourceType,
        sourceReference: reading.sourceReference.trim() || null, evidenceFileId: null }) },
      value => setDerived({ propagated: value.propagated.length, skipped: value.skipped.length }))
  }
  const disabled = command.busy
  return <section className="pms-panel" aria-label={t.titlePoints} data-testid="measurement-points">
    <h3>{t.titlePoints}</h3><p className="pms-boundary">{t.introPoints}</p>
    <Banner issue={command.issue} t={t} code={command.code} onReload={() => { command.clear(); loader.reload() }}/>
    {command.notice && <p role="status">{t.saved}</p>}
    {derived && <p role="status">{t.propagated.replace('{{count}}', String(derived.propagated))}{derived.skipped > 0 && <> · {t.skipped.replace('{{count}}', String(derived.skipped))}</>}</p>}
    {!items.length ? <p>{t.emptyPoints}</p> : <table className="pms-table"><thead><tr><th>{t.code}</th><th>{t.kind}</th><th>{t.latest}</th><th>{t.band}</th><th>{t.policy}</th><th>{t.freshness}</th></tr></thead>
      <tbody>{items.map(p => <tr key={p.id}><td><code>{p.code}</code>{p.parentPointId && <small> ↳ {page.items.find(x => x.id === p.parentPointId)?.code ?? '—'}</small>}</td>
        <td>{label(t, 'k_', p.kind)} · {p.unit}</td>
        <td>{p.latest ? <>{p.latest.value} {p.unit} · {new Date(p.latest.measuredAt).toLocaleString(language)} · {label(t, 'src_', p.latest.sourceType)}</> : t.none}</td>
        <td><PmsBadge state={p.band} text={label(t, 'b_', p.band)} tone={bandTone(p.band)}/></td>
        <td>{label(t, 'pol_', p.policyState)}{p.policyVersion && <small> · {p.policyVersion}</small>}</td>
        <td>{label(t, 'fresh_', p.freshness)}</td></tr>)}</tbody></table>}
    {page.canWrite && <form className="pms-form" onSubmit={e => { e.preventDefault(); savePoint() }}><fieldset disabled={disabled}><legend>{t.definePoint}</legend>
      <Field name={t.code}><input required maxLength={64} pattern="[A-Za-z0-9._\-]+" value={draft.code} onChange={e => setDraft({ ...draft, code: e.target.value })}/></Field>
      <Field name={t.kind}><select value={draft.kind} onChange={e => setDraft({ ...draft, kind: e.target.value, unit: page.units[e.target.value]?.[0] ?? '', parent: '' })}>
        {page.kinds.map(k => <option key={k} value={k}>{label(t, 'k_', k)}</option>)}</select></Field>
      <Field name={t.unit}><select value={draft.unit} onChange={e => setDraft({ ...draft, unit: e.target.value })}>{units.map(u => <option key={u} value={u}>{u}</option>)}</select></Field>
      <Field name={t.source}><select value={draft.source} onChange={e => setDraft({ ...draft, source: e.target.value })}>
        {['MANUAL', 'DEVICE'].map(s => <option key={s} value={s}>{label(t, 'src_', s)}</option>)}</select></Field>
      <Field name={t.precision}><input type="number" min={0} max={6} required value={draft.precision} onChange={e => setDraft({ ...draft, precision: e.target.value })}/></Field>
      <Field name={t.rangeMin}><input inputMode="decimal" value={draft.rangeMin} onChange={e => setDraft({ ...draft, rangeMin: e.target.value })}/></Field>
      <Field name={t.rangeMax}><input inputMode="decimal" value={draft.rangeMax} onChange={e => setDraft({ ...draft, rangeMax: e.target.value })}/></Field>
      {draft.kind === 'RUNNING_HOURS' && <Field name={t.parent}><select value={draft.parent} onChange={e => setDraft({ ...draft, parent: e.target.value })}>
        <option value="">{t.none}</option>{counters.map(c => <option key={c.id} value={c.id}>{c.code}</option>)}</select></Field>}
      </fieldset><EnterpriseActions layout="form" aria-label={t.definePoint}><Button type="submit" disabled={command.busy || !draft.code.trim()}>{command.issue === 'unknown' ? t.retry : t.savePoint}</Button></EnterpriseActions></form>}
    {page.canWrite && items.length > 0 && <form className="pms-form" onSubmit={e => { e.preventDefault(); saveReading() }}><fieldset disabled={disabled}><legend>{t.recordReading}</legend>
      <Field name={t.titlePoints}><select required value={reading.point} onChange={e => setReading({ ...reading, point: e.target.value })}><option value="">—</option>
        {items.map(p => <option key={p.id} value={p.id}>{p.code} · {p.unit}</option>)}</select></Field>
      <Field name={t.value}><input required inputMode="decimal" maxLength={60} value={reading.value} onChange={e => setReading({ ...reading, value: e.target.value })}/></Field>
      <Field name={t.measuredAt}><input required type="datetime-local" value={reading.measuredAt} onChange={e => setReading({ ...reading, measuredAt: e.target.value })}/></Field>
      <Field name={t.sourceType}><select value={reading.sourceType} onChange={e => setReading({ ...reading, sourceType: e.target.value })}>
        {['MANUAL', 'DEVICE', 'IMPORT'].map(s => <option key={s} value={s}>{label(t, 'src_', s)}</option>)}</select></Field>
      <Field name={t.sourceReference}><input maxLength={200} value={reading.sourceReference} onChange={e => setReading({ ...reading, sourceReference: e.target.value })}/></Field>
      </fieldset><EnterpriseActions layout="form" aria-label={t.recordReading}><Button type="submit" disabled={command.busy}>{command.issue === 'unknown' ? t.retry : t.saveReading}</Button></EnterpriseActions></form>}
  </section>
}

export function PostponementPanel({ vessel, plan, language }: { vessel: string; plan: string; language: Locale }) {
  const t = pmsCopy[language]
  const loader = useLoad(useCallback((signal: AbortSignal) => pms.readPostponements(vessel, plan, signal), [vessel, plan]), `${vessel}/${plan}`)
  const command = useAttempt(loader.reload)
  const [form, setForm] = useState({ deadline: '', reason: '', risk: '', workOrder: '', defects: '' })
  const [decision, setDecision] = useState<Record<string, string>>({})
  const page = loader.value
  if (loader.issue && !page) return <section className="pms-panel" aria-label={t.titlePost}><h3>{t.titlePost}</h3><Banner issue={loader.issue} t={t} onReload={loader.reload}/></section>
  if (!page) return <section className="pms-panel" aria-label={t.titlePost}><h3>{t.titlePost}</h3><p role="status">{t.loading}</p></section>
  const request = () => {
    const defects = form.defects.split(',').map(x => x.trim()).filter(Boolean)
    if (defects.some(d => !isGuid(d)) || form.risk && !isGuid(form.risk) || form.workOrder && !isGuid(form.workOrder)) return
    void command.run(command.pending() ? null : { path: `${pms.postponementPath(vessel, plan)}/postpone`, key: crypto.randomUUID(), body: JSON.stringify({
      id: crypto.randomUUID(), requestedDeadline: form.deadline, reason: form.reason, riskAssessmentId: form.risk || null, workOrderId: form.workOrder || null,
      defectIds: defects, requirementRefs: [] }) })
  }
  const decide = (item: pms.Postponement, choice: 'APPROVE' | 'REJECT') => void command.run(command.pending() ? null : {
    path: `${pms.postponementPath(vessel, plan)}/postponements/${encodeURIComponent(item.id)}/decision`, key: crypto.randomUUID(),
    body: JSON.stringify({ expectedVersion: item.version, decision: choice, reason: decision[item.id] ?? '' }) })
  return <section className="pms-panel" aria-label={t.titlePost} data-testid="postponements">
    <h3>{t.titlePost}</h3><p className="pms-boundary">{t.introPost}</p>
    <Banner issue={command.issue} t={t} code={command.code} onReload={() => { command.clear(); loader.reload() }}/>
    {command.notice && <p role="status">{t.saved}</p>}
    <dl className="pms-facts"><div><dt>{t.originalDue}</dt><dd>{page.originalDue ?? t.e_original_due_unavailable}</dd></div>
      <div><dt>{t.criticality}</dt><dd><PmsBadge state={page.criticality} text={label(t, 'crit_', page.criticality)} tone={page.criticality === 'CRITICAL' || page.criticality === 'UNKNOWN' ? 'alert' : 'muted'}/></dd></div></dl>
    <p role="note" data-policy={page.policyState}>{page.policyState === 'CONFIGURED'
      ? t.policyConfigured.replace('{{ref}}', page.policyRef ?? '').replace('{{version}}', page.policyVersion ?? '').replace('{{days}}', String(page.maxExtensionDays ?? ''))
      : t.policyUnconfigured}</p>
    {!page.items.length ? <p>{t.emptyPost}</p> : <ul className="pms-list">{page.items.map(item => <li key={item.id} data-state={item.effectiveState}>
      <PmsBadge state={item.effectiveState} text={label(t, 'st_', item.effectiveState)} tone={item.effectiveState === 'APPROVED' ? 'recorded' : item.effectiveState === 'REQUESTED' ? 'open' : 'muted'}/>
      <span> {t.deadline}: {item.requestedDeadline} · {t.extension}: {item.extensionDays} · {t.impact}: </span>
      <PmsBadge state={item.readinessImpact} text={label(t, 'imp_', item.readinessImpact)} tone={item.readinessImpact === 'REVIEW_REQUIRED' ? 'alert' : 'open'}/>
      <p>{t.reason}: {item.reason}</p>
      <small>{t.requestedBy}: <code>{item.requestedBy}</code>{item.decidedBy && <> · {t.decidedBy}: <code>{item.decidedBy}</code> · {t.decisionReason}: {item.decisionReason}</>}</small>
      {item.decisionBlockers.length > 0 && <div role="note"><strong>{t.blockers}</strong><ul>{item.decisionBlockers.map(b => <li key={b}>{refusal(t, b)}</li>)}</ul></div>}
      {item.canDecide && item.state === 'REQUESTED' && <div className="pms-decision">
        <Field name={t.decisionReason}><textarea rows={2} minLength={10} maxLength={2000} value={decision[item.id] ?? ''} onChange={e => setDecision({ ...decision, [item.id]: e.target.value })}/></Field>
        <EnterpriseActions align="start" aria-label={t.titlePost}>
          <Button disabled={command.busy || item.decisionBlockers.length > 0} onClick={() => decide(item, 'APPROVE')}>{t.approve}</Button>
          <Button variant="outline" disabled={command.busy} onClick={() => decide(item, 'REJECT')}>{t.reject}</Button></EnterpriseActions></div>}
    </li>)}</ul>}
    {page.canRequest && <form className="pms-form" onSubmit={e => { e.preventDefault(); request() }}><fieldset disabled={command.busy}><legend>{t.request}</legend>
      <Field name={t.deadline}><input required type="date" value={form.deadline} onChange={e => setForm({ ...form, deadline: e.target.value })}/></Field>
      <Field name={t.reason}><textarea required rows={2} minLength={10} maxLength={2000} value={form.reason} onChange={e => setForm({ ...form, reason: e.target.value })}/></Field>
      <Field name={t.riskReview}><input value={form.risk} onChange={e => setForm({ ...form, risk: e.target.value })}/></Field>
      <Field name={t.workOrder}><input value={form.workOrder} onChange={e => setForm({ ...form, workOrder: e.target.value })}/></Field>
      <Field name={t.defects}><input value={form.defects} onChange={e => setForm({ ...form, defects: e.target.value })}/></Field>
      </fieldset><EnterpriseActions layout="form" aria-label={t.request}><Button type="submit" disabled={command.busy}>{command.issue === 'unknown' ? t.retry : t.request}</Button></EnterpriseActions></form>}
  </section>
}
