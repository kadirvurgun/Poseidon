import { useCallback, useEffect, useState } from 'react'
import { localizedCopy, localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterprisePageHeader } from './EnterprisePatterns'
import { ApiError, session as readSession, type Session } from './api'
import { isGuid } from './identifiers'
import type { Command } from './tug-readiness-api'
import { CommandNotice, GateBadge, pilotCopy, pilotWord, useOnline, usePilotCommand, type PilotCopy } from './UatWorkspace'
import { areas, feedbackCategories, feedbackRoles, issueCategories, issueSeverities, measurementPhases, pilotProgramApi, sentiments, weeks,
  type CloseOutcome, type Issue, type Link, type ProgramDecision, type ProgramList, type ProgramView, type WeekView } from './pilot-program-api'

// Pilot program kit (DEV-076/077/078/079 weeks, DEV-095 dual-mode proof, DEV-096/121 value readout). The page records
// human pilot evidence only: an item turns "linked and verified" solely from a server-verified link to a real record,
// UNKNOWN values stay UNKNOWN (never 0, never green), and only the appointed Product Owner closes weeks or decides.
export const programCopy = localizedCopy('PilotProgram_tsx_copy')
type ProgramCopy = (typeof programCopy)[AppLocale]
const word = (t: ProgramCopy, key: string) => (t as Record<string, string>)[key] ?? key
type Run = (c: Command) => Promise<void>
type Ctx = { t: ProgramCopy; n: PilotCopy; disabled: boolean; run: Run }

export function readProgramRoute(href: string): { id: string | null; invalid: boolean } {
  const url = new URL(href, 'http://local'), m = /^\/app\/pilot-program(?:\/([^/]+))?\/?$/.exec(url.pathname)
  if (!m || url.search) return { id: null, invalid: true }
  return m[1] === undefined ? { id: null, invalid: false } : { id: m[1], invalid: !isGuid(m[1]) }
}

export function PilotProgramWorkspace() {
  const [lang, setLanguage] = useAppLanguage(), t = programCopy[lang], n = pilotCopy[lang]
  const title = localizedText(lang, 'pairs.m_5b1e7c9d3a2f4e62')
  const [route, setRoute] = useState(() => readProgramRoute(window.location.href))
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(true), [error, setError] = useState<ApiError | null>(null)
  const [list, setList] = useState<ProgramList | null>(null), [view, setView] = useState<ProgramView | null>(null)
  const offline = useOnline()
  const refresh = useCallback(() => setReload(x => x + 1), [])
  const cmd = usePilotCommand(user, offline, refresh)
  useEffect(() => {
    const pop = () => { setRoute(readProgramRoute(window.location.href)); refresh() }
    addEventListener('popstate', pop); return () => removeEventListener('popstate', pop)
  }, [refresh])
  const move = useCallback((href: string) => {
    if (cmd.retained || cmd.busy) return
    if (!/^\/app\/pilot-program(?:\/|$)/.test(href)) { window.location.assign(href); return }
    history.pushState(null, '', href); setRoute(readProgramRoute(window.location.href)); cmd.clear(); refresh()
  }, [cmd, refresh])
  useEffect(() => {
    const c = new AbortController()
    ;(async () => {
      await Promise.resolve()
      if (c.signal.aborted) return
      setLoading(true); setError(null); setList(null); setView(null)
      if (route.invalid) throw new ApiError(400, 'invalid_route')
      setUser(await readSession(c.signal))
      if (route.id) setView(await pilotProgramApi.read(route.id, c.signal))
      else setList(await pilotProgramApi.list(c.signal))
    })().then(() => { if (!c.signal.aborted) setLoading(false) })
      .catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e : new ApiError(0, 'network')); setLoading(false) })
    return () => c.abort()
  }, [route, reload])
  const disabled = cmd.busy || offline || !!cmd.retained && cmd.notice !== 'uncertain'
  const ctx: Ctx = { t, n, disabled, run: cmd.run }
  return <div className="product-shell enterprise-shell pilot-program-workspace">
    <EnterpriseChrome session={user} language={lang} onLanguageChange={setLanguage} onNavigate={move} title={title} mainId="pilot-program-main"/>
    <main id="pilot-program-main" tabIndex={-1} aria-busy={loading || cmd.busy} data-testid="pilot-program">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span>{t.boundary}</span>}
        actions={<EnterpriseActions>{route.id && <Button variant="outline" disabled={cmd.busy} onClick={() => move('/app/pilot-program')}>{t.back}</Button>}
          <Button variant="outline" disabled={offline || cmd.busy} onClick={refresh}>{n.refresh}</Button></EnterpriseActions>}/>
      {offline && <p role="alert" data-state="offline">{n.offline}</p>}
      <CommandNotice t={n} {...cmd} offline={offline} reload={refresh}/>
      {loading && <p role="status">{n.loading}</p>}
      {!loading && error && <div role="alert" data-state={error.status === 403 ? 'forbidden' : 'error'}>
        <p>{error.code === 'invalid_route' ? n.invalidRoute : error.status === 403 ? n.forbidden : error.status === 404 ? n.notFound : error.status === 0 ? n.offline : n.error}</p>
        {error.code !== 'invalid_route' && <Button type="button" variant="outline" onClick={refresh}>{n.retry}</Button>}</div>}
      {!loading && !error && list && <Programs ctx={ctx} list={list} open={id => move(`/app/pilot-program/${id}`)}/>}
      {!loading && !error && view && <Program ctx={ctx} view={view}/>}
    </main>
  </div>
}

function Programs({ ctx, list, open }: { ctx: Ctx; list: ProgramList; open: (id: string) => void }) {
  const { t, n, disabled, run } = ctx
  const [name, setName] = useState(''), [ship, setShip] = useState(''), [tug, setTug] = useState(''), [start, setStart] = useState(''), [zone, setZone] = useState('UTC')
  const ok = name.trim().length >= 3 && !!ship && /^\d{4}-\d{2}-\d{2}$/.test(start) && zone.trim().length > 0
  return <>
    <section aria-label={t.programs}>
      <h2>{t.programs}</h2>
      {list.programs.length === 0 ? <p data-state="empty">{t.noPrograms}</p> :
        <ul>{list.programs.map(p => <li key={p.id} data-program={p.id}>
          <Button type="button" variant="link" onClick={() => open(p.id)}>{p.name}</Button> · {p.startDate} · <code>{p.timeZone}</code>
          {p.provenance === 'SyntheticUnverified' && <> · <Badge tone="warning">{n.synthetic}</Badge></>}</li>)}</ul>}
    </section>
    {list.canCreate && <form aria-label={t.create} onSubmit={e => { e.preventDefault(); if (ok && !disabled) void run(pilotProgramApi.create(name.trim(), ship, tug || null, start, zone.trim())) }}>
      <h2>{t.create}</h2>
      <label htmlFor="pp-name">{t.name}</label><input id="pp-name" required minLength={3} maxLength={120} value={name} onChange={e => setName(e.target.value)}/>
      <label htmlFor="pp-ship">{t.shipVessel}</label>
      <select id="pp-ship" required value={ship} onChange={e => setShip(e.target.value)}><option value="">—</option>
        {list.vessels.filter(v => v.kind === 'SHIP').map(v => <option key={v.id} value={v.id}>{v.code} · {v.name}</option>)}</select>
      <label htmlFor="pp-tug">{t.tugVessel}</label>
      <select id="pp-tug" value={tug} onChange={e => setTug(e.target.value)}><option value="">—</option>
        {list.vessels.filter(v => v.kind === 'TUG').map(v => <option key={v.id} value={v.id}>{v.code} · {v.name}</option>)}</select>
      <label htmlFor="pp-start">{t.startDate}</label><input id="pp-start" type="date" required value={start} onChange={e => setStart(e.target.value)}/>
      <label htmlFor="pp-zone">{t.timeZone}</label><input id="pp-zone" required maxLength={64} value={zone} onChange={e => setZone(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.create}><Button type="submit" disabled={disabled || !ok}>{n.record}</Button></EnterpriseActions>
    </form>}
  </>
}

function Program({ ctx, view }: { ctx: Ctx; view: ProgramView }) {
  const { t, n } = ctx
  const synthetic = view.program.provenance === 'SyntheticUnverified'
  return <>
    <section aria-label={view.program.name}>
      <h2>{view.program.name}</h2>
      <p>{t.startDate}: {view.program.startDate} · <code>{view.program.timeZone}</code> · <time dateTime={view.assessedAt}>{view.assessedAt}</time></p>
      {synthetic && <p role="note" data-state="synthetic"><Badge tone="warning">{n.synthetic}</Badge> {t.syntheticProgram}</p>}
      {!view.canDecide && !synthetic && <p data-state="not-appointed">{t.notAppointedProgram}</p>}
    </section>
    {view.weeks.map(w => <Week key={w.definition.week + w.outcome.hash} ctx={ctx} view={view} week={w}/>)}
    <Issues ctx={ctx} view={view}/>
    <FeedbackSection ctx={ctx} view={view}/>
    <ReadoutSection ctx={ctx} view={view}/>
    <DecisionSection ctx={ctx} view={view}/>
  </>
}

function Week({ ctx, view, week }: { ctx: Ctx; view: ProgramView; week: WeekView }) {
  const { t, n } = ctx, o = week.outcome
  return <section aria-label={t.week + ' ' + week.definition.week} data-week={week.definition.week} data-phase={o.phase}>
    <h2>{t.week} {week.definition.number} · {week.startDate} → {week.endDateExclusive}</h2>
    <p><Badge tone="neutral">{word(t, 'phase_' + o.phase)}</Badge> <Badge tone={o.recommendation === 'READY_FOR_HUMAN_CLOSE' ? 'info' : 'warning'} data-recommendation={o.recommendation}>
      {word(t, 'rec_' + o.recommendation)}</Badge> · {week.definition.requirements.join(', ')} · <small><code>{week.definition.locator}</code></small></p>
    {o.overdueP0 > 0 && <p role="alert" data-state="p0-overdue">{word(t, 'timing_OVERDUE')}: {o.overdueP0}</p>}
    <table><thead><tr><th scope="col">{t.item}</th><th scope="col">{t.criterion}</th><th scope="col">{t.area}</th><th scope="col">{n.state}</th><th scope="col">{t.links}</th></tr></thead>
      <tbody>{week.definition.items.map(item => {
        const state = o.items.find(i => i.code === item.code)!
        const links = week.links.filter(l => l.itemCode === item.code)
        return <tr key={item.code} data-item={item.code} data-item-state={state.state}>
          <td><code>{item.code}</code></td><td><q>{item.criterion}</q> <small><code>{item.locator}</code></small></td><td>{word(t, 'area_' + item.area)}</td>
          <td><Badge tone={state.state === 'SATISFIED' ? 'info' : state.state === 'LINK_INVALID' ? 'danger' : 'warning'}>{word(t, 'item_' + state.state)}</Badge>
            {state.reasons.length > 0 && <ul>{state.reasons.map(r => <li key={r}><code>{r}</code></li>)}</ul>}</td>
          <td>{links.length === 0 ? <span data-state="empty">{t.noLinks}</span> :
            <ul>{links.map(l => <LinkRow key={l.id} ctx={ctx} programId={view.program.id} link={l} canWithdraw={view.canRecord}/>)}</ul>}</td></tr>
      })}</tbody></table>
    {view.canRecord && <LinkForm ctx={ctx} programId={view.program.id} week={week}/>}
    {week.closes.length > 0 && <><h3>{t.closes}</h3><ul>{week.closes.map(c => <li key={c.id} data-close={c.outcome} data-validity={c.validity}>
      #{c.sequence} · {word(t, 'outcome_' + c.outcome)} · <Badge tone={c.validity === 'STALE' ? 'warning' : 'neutral'}>{pilotWord(n, 'validity_' + c.validity)}</Badge> · {c.rationale}</li>)}</ul></>}
    {view.canDecide && o.phase !== 'NOT_STARTED' && <CloseForm key={o.hash + week.closeSequence} ctx={ctx} view={view} week={week}/>}
  </section>
}

function LinkRow({ ctx, programId, link, canWithdraw }: { ctx: Ctx; programId: string; link: Link; canWithdraw: boolean }) {
  const { t, disabled, run } = ctx
  const [reason, setReason] = useState('')
  return <li data-link={link.id} data-validity={link.validity}>
    <code>{link.recordType}</code> <code>{link.recordId}</code> · <Badge tone={link.validity === 'VALID' ? 'info' : 'danger'}>{word(t, 'link_' + link.validity)}</Badge>
    {link.refusal && <> · <code>{link.refusal}</code></>}{link.auditTracedAtLink && <> · {t.auditTraced}</>}
    {canWithdraw && <form aria-label={t.withdraw + ' ' + link.id} onSubmit={e => { e.preventDefault(); if (reason.trim().length >= 10 && !disabled) void run(pilotProgramApi.withdraw(programId, link, reason.trim())) }}>
      <input aria-label={t.withdrawReason} minLength={10} maxLength={500} value={reason} onChange={e => setReason(e.target.value)}/>
      <Button type="submit" variant="outline" disabled={disabled || reason.trim().length < 10}>{t.withdraw}</Button></form>}
  </li>
}

function LinkForm({ ctx, programId, week }: { ctx: Ctx; programId: string; week: WeekView }) {
  const { t, n, disabled, run } = ctx
  const [item, setItem] = useState(''), [type, setType] = useState(''), [record, setRecord] = useState(''), [note, setNote] = useState('')
  const types = week.definition.items.find(i => i.code === item)?.recordTypes ?? []
  const ok = !!item && types.includes(type) && isGuid(record.trim())
  return <form aria-label={t.linkRecord + ' ' + week.definition.week} onSubmit={e => { e.preventDefault(); if (ok && !disabled)
    void run(pilotProgramApi.link(programId, item, type, record.trim(), note.trim() || null)) }}>
    <h3>{t.linkRecord}</h3>
    <select aria-label={t.item} value={item} onChange={e => { setItem(e.target.value); setType('') }}><option value="">—</option>
      {week.definition.items.map(i => <option key={i.code} value={i.code}>{i.code}</option>)}</select>
    <select aria-label={t.recordType} value={type} onChange={e => setType(e.target.value)}><option value="">—</option>{types.map(x => <option key={x} value={x}>{x}</option>)}</select>
    <input aria-label={t.recordId} value={record} onChange={e => setRecord(e.target.value)}/>
    <input aria-label={n.note} maxLength={500} value={note} onChange={e => setNote(e.target.value)}/>
    <Button type="submit" disabled={disabled || !ok}>{t.linkRecord}</Button>
  </form>
}

function CloseForm({ ctx, view, week }: { ctx: Ctx; view: ProgramView; week: WeekView }) {
  const { t, n, disabled, run } = ctx
  const [outcome, setOutcome] = useState<CloseOutcome | ''>(''), [rationale, setRationale] = useState('')
  const [authority, setAuthority] = useState(view.authorities.length === 1 ? view.authorities[0].id : '')
  const ok = !!outcome && !!authority && rationale.trim().length >= 10 && week.allowedOutcomes.includes(outcome)
  const id = 'pp-close-' + week.definition.week
  return <form aria-label={t.closeWeek + ' ' + week.definition.week} onSubmit={e => { e.preventDefault(); if (ok && outcome && !disabled)
    void run(pilotProgramApi.close(view.program.id, week, outcome, rationale.trim(), authority)) }}>
    <h3>{t.closeWeek}</h3>
    <fieldset><legend>{t.closeWeek}</legend>
      {(['NOT_ACCEPTED', 'ACCEPTED'] as const).map(o => <label key={o}><input type="radio" name={id} value={o} checked={outcome === o}
        disabled={!week.allowedOutcomes.includes(o)} onChange={() => setOutcome(o)}/>{word(t, 'outcome_' + o)}</label>)}</fieldset>
    {!week.allowedOutcomes.includes('ACCEPTED') && <p>{t.closeHint}</p>}
    <label htmlFor={id + '-authority'}>{n.authority}</label>
    <select id={id + '-authority'} required value={authority} onChange={e => setAuthority(e.target.value)}><option value="">—</option>
      {view.authorities.map(a => <option key={a.id} value={a.id}>{n.productOwner}</option>)}</select>
    <label htmlFor={id + '-rationale'}>{n.rationale}</label>
    <textarea id={id + '-rationale'} required minLength={10} maxLength={2000} value={rationale} onChange={e => setRationale(e.target.value)}/>
    <EnterpriseActions layout="form" aria-label={t.closeWeek}><Button type="submit" disabled={disabled || !ok}>{n.record}</Button></EnterpriseActions>
  </form>
}

function Issues({ ctx, view }: { ctx: Ctx; view: ProgramView }) {
  const { t, n, disabled, run } = ctx
  const [w, setW] = useState('W1'), [area, setArea] = useState('SHIP'), [category, setCategory] = useState(''), [severity, setSeverity] = useState(''), [title, setTitle] = useState('')
  const ok = !!category && !!severity && title.trim().length >= 5
  return <section aria-label={t.issues}>
    <h2>{t.issues}</h2>
    {view.issues.length === 0 ? <p data-state="empty">{t.noIssues}</p> :
      <ul>{view.issues.map(i => <IssueRow key={i.id + i.version} ctx={ctx} programId={view.program.id} issue={i} canTriage={view.canTriage}/>)}</ul>}
    {view.canRecord && <form aria-label={t.reportIssue} onSubmit={e => { e.preventDefault(); if (ok && !disabled) void run(pilotProgramApi.issue(view.program.id, w, area, category, severity, title.trim())) }}>
      <h3>{t.reportIssue}</h3>
      <select aria-label={t.week} value={w} onChange={e => setW(e.target.value)}>{weeks.map(x => <option key={x} value={x}>{x}</option>)}</select>
      <select aria-label={t.area} value={area} onChange={e => setArea(e.target.value)}>{areas.map(x => <option key={x} value={x}>{word(t, 'area_' + x)}</option>)}</select>
      <select aria-label={t.category} value={category} onChange={e => setCategory(e.target.value)}><option value="">—</option>{issueCategories.map(x => <option key={x} value={x}>{x}</option>)}</select>
      <select aria-label={n.severity} value={severity} onChange={e => setSeverity(e.target.value)}><option value="">—</option>{issueSeverities.map(x => <option key={x} value={x}>{x}</option>)}</select>
      <input aria-label={t.issueTitle} minLength={5} maxLength={200} value={title} onChange={e => setTitle(e.target.value)}/>
      <p><small>{t.piiWarning}</small></p>
      <Button type="submit" disabled={disabled || !ok}>{t.reportIssue}</Button>
    </form>}
  </section>
}

function IssueRow({ ctx, programId, issue, canTriage }: { ctx: Ctx; programId: string; issue: Issue; canTriage: boolean }) {
  const { t, n, disabled, run } = ctx
  const [note, setNote] = useState('')
  const late = issue.timing === 'OVERDUE' || issue.timing === 'TRIAGED_LATE'
  return <li data-issue={issue.id} data-timing={issue.timing} data-issue-state={issue.state}>
    <Badge tone={issue.severity === 'P0' ? 'danger' : 'neutral'}>{issue.severity}</Badge> {issue.week} · <code>{issue.category}</code> · {issue.title} ·{' '}
    <Badge tone="neutral">{word(t, 'issue_' + issue.state)}</Badge> · <Badge tone={late ? 'danger' : 'neutral'}>{word(t, 'timing_' + issue.timing)}</Badge>
    {' '}· <time dateTime={issue.reportedAt}>{issue.reportedAt}</time>{issue.triagedAt && <> → <time dateTime={issue.triagedAt}>{issue.triagedAt}</time></>}
    {canTriage && issue.state !== 'RESOLVED' && <form aria-label={(issue.state === 'OPEN' ? t.triage : n.resolve) + ' ' + issue.id} onSubmit={e => { e.preventDefault()
      if (note.trim().length >= 3 && !disabled) void run(issue.state === 'OPEN' ? pilotProgramApi.triage(programId, issue, note.trim()) : pilotProgramApi.resolve(programId, issue, note.trim(), null)) }}>
      <input aria-label={issue.state === 'OPEN' ? t.triageNote : n.resolution} minLength={3} maxLength={500} value={note} onChange={e => setNote(e.target.value)}/>
      <Button type="submit" variant="outline" disabled={disabled || note.trim().length < 3}>{issue.state === 'OPEN' ? t.triage : n.resolve}</Button></form>}
  </li>
}

function FeedbackSection({ ctx, view }: { ctx: Ctx; view: ProgramView }) {
  const { t, n, disabled, run } = ctx
  const [w, setW] = useState('W1'), [area, setArea] = useState('SHIP'), [category, setCategory] = useState(''), [sentiment, setSentiment] = useState(''), [role, setRole] = useState('')
  const [summary, setSummary] = useState(''), [pseudonym, setPseudonym] = useState('')
  const ok = !!category && !!sentiment && !!role && summary.trim().length >= 3 && /^[A-Z0-9-]{3,32}$/.test(pseudonym)
  return <section aria-label={t.feedback}>
    <h2>{t.feedback}</h2>
    <p role="note">{t.piiWarning}</p>
    {view.feedbackRetention === 'UNCONFIGURED' && <p role="alert" data-state="retention-unconfigured">{t.retentionUnconfigured}</p>}
    {view.feedback.length === 0 ? <p data-state="empty">{t.noFeedback}</p> :
      <ul>{view.feedback.map(f => <li key={f.id} data-feedback={f.id}>{f.week} · {word(t, 'area_' + f.area)} · <code>{f.category}</code> · <code>{f.role}</code> ·
        {' '}<code>{f.participantPseudonym}</code> · {f.summary} · {t.expires} <time dateTime={f.expiresAt}>{f.expiresAt}</time></li>)}</ul>}
    {view.canRecord && view.feedbackRetention === 'CONFIGURED' && <form aria-label={t.recordFeedback} onSubmit={e => { e.preventDefault(); if (ok && !disabled)
      void run(pilotProgramApi.feedback(view.program.id, w, area, category, sentiment, role, summary.trim(), pseudonym)) }}>
      <h3>{t.recordFeedback}</h3>
      <select aria-label={t.week} value={w} onChange={e => setW(e.target.value)}>{weeks.map(x => <option key={x} value={x}>{x}</option>)}</select>
      <select aria-label={t.area} value={area} onChange={e => setArea(e.target.value)}>{areas.map(x => <option key={x} value={x}>{word(t, 'area_' + x)}</option>)}</select>
      <select aria-label={t.category} value={category} onChange={e => setCategory(e.target.value)}><option value="">—</option>{feedbackCategories.map(x => <option key={x} value={x}>{x}</option>)}</select>
      <select aria-label={t.sentiment} value={sentiment} onChange={e => setSentiment(e.target.value)}><option value="">—</option>{sentiments.map(x => <option key={x} value={x}>{x}</option>)}</select>
      <select aria-label={n.role} value={role} onChange={e => setRole(e.target.value)}><option value="">—</option>{feedbackRoles.map(x => <option key={x} value={x}>{x}</option>)}</select>
      <input aria-label={n.pseudonym} maxLength={32} value={pseudonym} onChange={e => setPseudonym(e.target.value.toUpperCase())}/>
      <textarea aria-label={t.summary} minLength={3} maxLength={500} value={summary} onChange={e => setSummary(e.target.value)}/>
      <Button type="submit" disabled={disabled || !ok}>{t.recordFeedback}</Button>
    </form>}
  </section>
}

const shown = (v: number | null, unknown: string) => v === null ? unknown : String(v)

function ReadoutSection({ ctx, view }: { ctx: Ctx; view: ProgramView }) {
  const { t, n, disabled, run } = ctx, r = view.readout
  const [metric, setMetric] = useState(''), [phase, setPhase] = useState(''), [value, setValue] = useState(''), [sample, setSample] = useState('')
  const [from, setFrom] = useState(''), [to, setTo] = useState(''), [evidence, setEvidence] = useState(''), [method, setMethod] = useState('')
  const sequence = view.measurements.find(m => m.metricId === metric && m.phase === phase)?.sequence ?? 0
  const ok = !!metric && !!phase && value.trim() !== '' && Number.isFinite(Number(value)) && Number.isSafeInteger(Number(sample)) && Number(sample) >= 1 &&
    /^\d{4}-\d{2}-\d{2}$/.test(from) && /^\d{4}-\d{2}-\d{2}$/.test(to) && evidence.trim().length > 0 && method.trim().length >= 10
  return <section aria-label={t.readout}>
    <h2>{t.readout}</h2>
    <table><thead><tr><th scope="col">{t.metric}</th><th scope="col">{t.mode}</th><th scope="col">{t.baseline}</th><th scope="col">{t.current}</th><th scope="col">{t.delta}</th>
      <th scope="col">{t.percentChange}</th><th scope="col">{n.state}</th><th scope="col">{t.telemetry}</th></tr></thead>
      <tbody>{r.metrics.map(m => <tr key={m.metricId} data-metric={m.metricId} data-quality={m.quality}>
        <td><code>{m.metricId}</code> <small>{m.unit}</small></td><td>{pilotWord(n, 'scope_' + m.mode)}</td>
        <td>{shown(m.baseline, n.unknown)}</td><td>{shown(m.current, n.unknown)}</td><td>{shown(m.delta, n.unknown)}</td><td>{shown(m.percentChange, n.unknown)}</td>
        <td><Badge tone={m.quality === 'COMPLETE' ? 'neutral' : 'warning'}>{word(t, 'quality_' + m.quality)}</Badge> {m.quality === 'COMPLETE' && word(t, 'comparison_' + m.comparison)}
          {m.reasons.length > 0 && <ul>{m.reasons.map(x => <li key={x}><code>{x}</code></li>)}</ul>}</td>
        <td>{m.telemetry === null ? '—' : m.telemetry.state === 'NOT_AUTHORIZED' ? t.telemetryHidden : <><code>{m.telemetry.state}</code>{m.telemetry.value !== null && <> {m.telemetry.value} {m.telemetry.unitCode}</>}</>}</td>
      </tr>)}</tbody></table>
    <h3>{t.uatSignoff}</h3>
    <p data-uat="SHIP">{pilotWord(n, 'scope_SHIP')}: <GateBadge state={r.uatShip.state} t={n}/></p>
    <p data-uat="TUG">{pilotWord(n, 'scope_TUG')}: <GateBadge state={r.uatTug.state} t={n}/></p>
    <h3>{t.defects}</h3>
    <ul>{r.defects.map(d => <li key={d.mode} data-defects={d.mode} data-quality={d.state}>{pilotWord(n, 'scope_' + d.mode)}: {d.state === 'UNKNOWN' ? n.unknown :
      `${t.critical} ${d.critical} · ${t.high} ${d.high} · ${t.medium} ${d.medium} · ${t.low} ${d.low} · ${t.unassessed} ${d.unassessed}`}</li>)}</ul>
    {view.canRecord && <form aria-label={t.recordMeasurement} onSubmit={e => { e.preventDefault(); if (ok && !disabled)
      void run(pilotProgramApi.measure(view.program.id, metric, phase, Number(value), Number(sample), from, to, evidence.trim(), method.trim(), sequence)) }}>
      <h3>{t.recordMeasurement}</h3>
      <select aria-label={t.metric} value={metric} onChange={e => setMetric(e.target.value)}><option value="">—</option>
        {view.metricCatalog.map(m => <option key={m.id} value={m.id}>{m.id} ({m.unit})</option>)}</select>
      <select aria-label={t.phase} value={phase} onChange={e => setPhase(e.target.value)}><option value="">—</option>
        {measurementPhases.map(x => <option key={x} value={x}>{word(t, 'measurement_' + x)}</option>)}</select>
      <input aria-label={t.value} inputMode="decimal" value={value} onChange={e => setValue(e.target.value)}/>
      <input aria-label={t.sampleSize} inputMode="numeric" value={sample} onChange={e => setSample(e.target.value)}/>
      <input aria-label={t.periodFrom} type="date" value={from} onChange={e => setFrom(e.target.value)}/>
      <input aria-label={t.periodTo} type="date" value={to} onChange={e => setTo(e.target.value)}/>
      <input aria-label={n.evidenceReference} maxLength={200} value={evidence} onChange={e => setEvidence(e.target.value)}/>
      <textarea aria-label={t.method} minLength={10} maxLength={500} value={method} onChange={e => setMethod(e.target.value)}/>
      <Button type="submit" disabled={disabled || !ok}>{t.recordMeasurement}</Button>
    </form>}
  </section>
}

function DecisionSection({ ctx, view }: { ctx: Ctx; view: ProgramView }) {
  const { t, n, disabled, run } = ctx
  const [decision, setDecision] = useState<ProgramDecision | ''>(''), [rationale, setRationale] = useState('')
  const [authority, setAuthority] = useState(view.authorities.length === 1 ? view.authorities[0].id : '')
  const ok = !!decision && !!authority && rationale.trim().length >= 10 && view.allowedDecisions.includes(decision)
  return <section aria-label={t.programDecision}>
    <h2>{t.programDecision}</h2>
    <p><Badge tone={view.review.recommendation === 'FIX_RECOMMENDED' ? 'warning' : 'info'} data-recommendation={view.review.recommendation}>
      {word(t, 'recP_' + view.review.recommendation)}</Badge> <small>{n.advisory}</small></p>
    {view.review.reasons.length > 0 && <ul>{view.review.reasons.map(x => <li key={x}><code>{x}</code></li>)}</ul>}
    {view.decisions.length === 0 ? <p data-state="empty">{n.noDecisions}</p> :
      <ul>{view.decisions.map(d => <li key={d.id} data-decision={d.decision} data-validity={d.validity}>#{d.sequence} · {word(t, 'decision_' + d.decision)} ·
        {' '}<Badge tone={d.validity === 'STALE' ? 'warning' : 'neutral'}>{pilotWord(n, 'validity_' + d.validity)}</Badge> · {d.rationale}</li>)}</ul>}
    {view.canDecide && <form key={view.review.hash + view.decisionSequence} aria-label={t.decide} onSubmit={e => { e.preventDefault(); if (ok && decision && !disabled)
      void run(pilotProgramApi.decide(view, decision, rationale.trim(), authority)) }}>
      <fieldset><legend>{t.decide}</legend>
        {(['CONTINUE', 'FIX', 'STOP', 'EXPAND'] as const).map(d => <label key={d}><input type="radio" name="pp-decision" value={d} checked={decision === d}
          disabled={!view.allowedDecisions.includes(d)} onChange={() => setDecision(d)}/>{word(t, 'decision_' + d)}</label>)}</fieldset>
      {!view.allowedDecisions.includes('EXPAND') && <p>{t.expandBlocked}</p>}
      <label htmlFor="pp-authority">{n.authority}</label>
      <select id="pp-authority" required value={authority} onChange={e => setAuthority(e.target.value)}><option value="">—</option>
        {view.authorities.map(a => <option key={a.id} value={a.id}>{n.productOwner}</option>)}</select>
      <label htmlFor="pp-rationale">{n.rationale}</label>
      <textarea id="pp-rationale" required minLength={10} maxLength={2000} value={rationale} onChange={e => setRationale(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.decide}><Button type="submit" disabled={disabled || !ok}>{n.record}</Button></EnterpriseActions>
    </form>}
  </section>
}
