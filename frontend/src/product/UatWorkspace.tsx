import { useCallback, useEffect, useRef, useState } from 'react'
import { localizedCopy, localizedText } from '../i18n/messages'
import { useAppLanguage, type Locale as AppLocale } from '../i18n'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterprisePageHeader } from './EnterprisePatterns'
import { ApiError, session as readSession, type Session } from './api'
import { isGuid } from './identifiers'
import { send, type Command } from './tug-readiness-api'
import { uatApi, uatOutcomes, uatScopes, uatSeverities, type GateState, type UatCatalog, type UatFinding, type UatRunDetail, type UatScope, type UatSummary } from './pilot-gate-api'

// S11 WP-D DEV-074 (ship) / DEV-094 (tug) role UAT records. Facilitators record participant outcomes and findings;
// another human attests a run. The summary compares human evidence to the human-agreed threshold; it is never a UAT
// acceptance or a pilot GO, and no state is shown in a green "passed" tone.
export const pilotCopy = localizedCopy('UatPilotGate_tsx_copy')
export type PilotCopy = (typeof pilotCopy)[AppLocale]
export type PilotNotice = '' | 'done' | 'uncertain' | 'conflict' | 'denied' | 'refused' | 'failed'
export const pilotWord = (t: PilotCopy, key: string) => (t as Record<string, string>)[key] ?? key

export function GateBadge({ state, t }: { state: GateState; t: PilotCopy }) {
  return <Badge tone={state === 'FAIL' ? 'danger' : state === 'PASS' ? 'info' : 'warning'} data-gate-state={state}>{pilotWord(t, 'gate_' + state)}</Badge>
}

// One command at a time; an uncertain outcome keeps the exact key/body for an identical retry (never a fresh key).
export function usePilotCommand(user: Session | null, offline: boolean, onDone: () => void) {
  const [busy, setBusy] = useState(false), [notice, setNotice] = useState<PilotNotice>(''), [retained, setRetained] = useState<Command | null>(null)
  const [code, setCode] = useState('')
  const lock = useRef(false), mounted = useRef(true)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  const run = useCallback(async (command: Command) => {
    if (lock.current || offline || !navigator.onLine) return
    lock.current = true; setBusy(true); setNotice(''); setCode(''); setRetained(command)
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000)
    let sent = false
    try {
      const s = await readSession(c.signal)
      if (!s.authenticated || !s.csrfToken || user && `${s.tenantId}/${s.actorId}` !== `${user.tenantId}/${user.actorId}`) throw new ApiError(403, 'identity_boundary_changed')
      sent = true
      await send(command, s.csrfToken, c.signal)
      if (!mounted.current) return
      setRetained(null); setNotice('done'); onDone()
    } catch (e) {
      if (!mounted.current) return
      const definite = e instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(e.status)
      if (!sent || definite) setRetained(null)
      setCode(e instanceof ApiError ? e.code : '')
      setNotice(sent && !definite ? 'uncertain' : e instanceof ApiError && e.status === 409 ? 'conflict' : e instanceof ApiError && e.status === 422 ? 'refused'
        : e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : 'failed')
    } finally { clearTimeout(timeout); lock.current = false; if (mounted.current) setBusy(false) }
  }, [offline, user, onDone])
  return { run, busy, notice, code, retained, clear: () => { setNotice(''); setCode('') } }
}

export function useOnline() {
  const [offline, setOffline] = useState(!navigator.onLine)
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true)
    addEventListener('online', on); addEventListener('offline', off)
    return () => { removeEventListener('online', on); removeEventListener('offline', off) }
  }, [])
  return offline
}

export function CommandNotice({ t, notice, code, retained, busy, offline, run, reload }: { t: PilotCopy; notice: PilotNotice; code: string; retained: Command | null
  busy: boolean; offline: boolean; run: (c: Command) => Promise<void>; reload: () => void }) {
  if (!notice) return null
  return <div role={notice === 'done' ? 'status' : 'alert'} data-notice={notice}>
    <p>{pilotWord(t, 'notice_' + notice)}{code && notice !== 'done' ? <> · <code>{code}</code></> : null}</p>
    {notice === 'uncertain' && retained && <Button type="button" disabled={busy || offline} onClick={() => void run(retained)}>{t.retry}</Button>}
    {notice === 'conflict' && <Button type="button" variant="outline" onClick={reload}>{t.reload}</Button>}
  </div>
}

export type UatRoute = { scope: UatScope; run: string | null; invalid: boolean }
export function readUatRoute(href: string): UatRoute {
  const url = new URL(href, 'http://local'), p = url.searchParams
  const scope = p.get('scope') ?? 'SHIP', runId = p.get('run')
  const invalid = !/^\/app\/role-uat\/?$/.test(url.pathname) || [...p.keys()].some(k => !['scope', 'run'].includes(k) || p.getAll(k).length !== 1) ||
    !(uatScopes as readonly string[]).includes(scope) || runId !== null && !isGuid(runId)
  return { scope: (uatScopes as readonly string[]).includes(scope) ? scope as UatScope : 'SHIP', run: runId && isGuid(runId) ? runId.toLowerCase() : null, invalid }
}
export const uatHref = (scope: UatScope, run?: string) => `/app/role-uat?scope=${scope}${run ? `&run=${run}` : ''}`

export function UatWorkspace() {
  const [lang, setLanguage] = useAppLanguage(), t = pilotCopy[lang]
  const title = localizedText(lang, 'pairs.m_5b1e7c9d3a2f4e60')
  const [route, setRoute] = useState(() => readUatRoute(window.location.href))
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(true), [error, setError] = useState<ApiError | null>(null)
  const [catalog, setCatalog] = useState<UatCatalog | null>(null), [summary, setSummary] = useState<UatSummary | null>(null), [detail, setDetail] = useState<UatRunDetail | null>(null)
  const offline = useOnline()
  const refresh = useCallback(() => setReload(n => n + 1), [])
  const cmd = usePilotCommand(user, offline, refresh)
  useEffect(() => {
    const pop = () => { setRoute(readUatRoute(window.location.href)); refresh() }
    addEventListener('popstate', pop); return () => removeEventListener('popstate', pop)
  }, [refresh])
  const move = useCallback((href: string) => {
    if (cmd.retained || cmd.busy) return
    if (!/^\/app\/role-uat(?:\/|\?|$)/.test(href)) { window.location.assign(href); return }
    history.pushState(null, '', href); setRoute(readUatRoute(window.location.href)); cmd.clear(); refresh()
  }, [cmd, refresh])
  useEffect(() => {
    const c = new AbortController()
    ;(async () => {
      await Promise.resolve()
      if (c.signal.aborted) return
      setLoading(true); setError(null); setSummary(null); setDetail(null)
      if (route.invalid) throw new ApiError(400, 'invalid_route')
      setUser(await readSession(c.signal))
      const [cat, sum] = await Promise.all([uatApi.catalog(c.signal), uatApi.summary(route.scope, c.signal)])
      setCatalog(cat); setSummary(sum)
      if (route.run) setDetail(await uatApi.run(route.run, c.signal))
    })().then(() => { if (!c.signal.aborted) setLoading(false) })
      .catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e : new ApiError(0, 'network')); setLoading(false) })
    return () => c.abort()
  }, [route, reload])

  const disabled = cmd.busy || offline || !!cmd.retained && cmd.notice !== 'uncertain'
  const scenarios = catalog?.scenarios.filter(s => s.scope === route.scope) ?? []
  const roles = route.scope === 'SHIP' ? catalog?.shipRoles ?? [] : catalog?.tugRoles ?? []
  return <div className="product-shell enterprise-shell uat-workspace">
    <EnterpriseChrome session={user} language={lang} onLanguageChange={setLanguage} onNavigate={move} title={title} mainId="uat-main"/>
    <main id="uat-main" tabIndex={-1} aria-busy={loading || cmd.busy} data-testid="role-uat">
      <EnterprisePageHeader eyebrow={t.eyebrow} title={title} description={<span>{t.uatBoundary}</span>}
        actions={<Button variant="outline" disabled={offline || cmd.busy} onClick={refresh}>{t.refresh}</Button>}/>
      <nav aria-label={t.scope}><EnterpriseActions align="start">{uatScopes.map(s =>
        <Button key={s} variant={s === route.scope ? 'default' : 'outline'} aria-current={s === route.scope ? 'page' : undefined} onClick={() => move(uatHref(s))}>{pilotWord(t, 'scope_' + s)}</Button>)}
      </EnterpriseActions></nav>
      {offline && <p role="alert" data-state="offline">{t.offline}</p>}
      <CommandNotice t={t} {...cmd} offline={offline} reload={refresh}/>
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error && <div role="alert" data-state={error.status === 403 ? 'forbidden' : 'error'}>
        <p>{error.code === 'invalid_route' ? t.invalidRoute : error.status === 403 ? t.forbidden : error.status === 404 ? t.notFound : error.status === 0 ? t.offline : t.error}</p>
        {error.code !== 'invalid_route' && <Button type="button" variant="outline" onClick={refresh}>{t.retry}</Button>}</div>}
      {!loading && !error && summary && <>
        <section aria-label={t.uatGate}>
          <h2>{t.uatGate}</h2>
          <p><GateBadge state={summary.gate.gateState} t={t}/> <Badge tone="neutral" data-uat-state={summary.gate.state}>{pilotWord(t, 'uat_' + summary.gate.state)}</Badge> <small>{t.notAcceptance}</small></p>
          <p>{t.openBlockers}: <strong data-count="blockers">{summary.gate.openBlockers}</strong> · {t.openMajors}: {summary.gate.openMajors} · {t.attestedRuns}: {summary.gate.attestedRuns} · {t.pendingAttestation}: {summary.gate.pendingAttestation}</p>
          {summary.threshold ? <p data-state="threshold">{t.threshold}: {summary.threshold.minScenarioPassPercent}% · {t.maxMajors}: {summary.threshold.maxOpenMajorFindings} · {t.designConformance}: {summary.threshold.requireDesignConformance ? t.yes : t.no} · <code>{summary.threshold.agreementReference}</code></p>
            : <p role="status" data-state="threshold-unset">{t.thresholdUnset}</p>}
          {summary.gate.reasons.length > 0 && <ul aria-label={t.reasons}>{summary.gate.reasons.map(r => <li key={r}><code>{r}</code></li>)}</ul>}
          <table><thead><tr><th scope="col">{t.scenario}</th><th scope="col">{t.passProof}</th><th scope="col">{t.attestedRuns}</th><th scope="col">{t.passRate}</th></tr></thead>
            <tbody>{scenarios.map(s => { const o = summary.gate.scenarios.find(x => x.scenarioId === s.id)
              return <tr key={s.id} data-scenario={s.id}><td><code>{s.id}</code> {s.journey}</td><td>{s.passProof}</td><td>{o?.attestedRuns ?? 0}</td><td>{o?.passPercent == null ? t.notExecuted : `${o.passPercent}%`}</td></tr> })}</tbody></table>
        </section>
        {summary.canRecordThreshold && <ThresholdForm key={'t' + summary.thresholdSequence} t={t} summary={summary} disabled={disabled} run={cmd.run}/>}
        <section aria-label={t.runs}><h2>{t.runs}</h2>
          {summary.runs.length === 0 ? <p data-state="empty">{t.noRuns}</p> :
            <ul>{summary.runs.map(r => <li key={r.id} data-run-state={r.state}><Button variant="link" onClick={() => move(uatHref(route.scope, r.id))}>
              {r.role} · {r.environment} · <code>{r.buildCommit}</code> · {r.participantPseudonym}</Button> <Badge tone="neutral">{pilotWord(t, 'run_' + r.state)}</Badge>
              {r.provenance === 'SyntheticUnverified' && <Badge tone="warning">{t.synthetic}</Badge>}</li>)}</ul>}
          {summary.canRecord && <RunForm t={t} scope={route.scope} roles={roles} environments={catalog?.environments ?? []} disabled={disabled} run={cmd.run}/>}
        </section>
        {detail && <RunPanel key={detail.run.id + detail.run.version + detail.results.length + detail.findings.length} t={t} detail={detail}
          scenarios={scenarios.map(s => s.id)} actor={user?.actorId ?? ''} disabled={disabled} run={cmd.run}/>}
        <section aria-label={t.openFindings}><h2>{t.openFindings}</h2>
          {summary.openFindings.length === 0 ? <p data-state="empty">{t.noFindings}</p> :
            <ul>{summary.openFindings.map(f => <FindingItem key={f.id + f.version} t={t} f={f} canResolve={summary.canRecord && f.reportedBy !== user?.actorId} disabled={disabled} run={cmd.run}/>)}</ul>}
        </section>
      </>}
    </main>
  </div>
}

function ThresholdForm({ t, summary, disabled, run }: { t: PilotCopy; summary: UatSummary; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const [pass, setPass] = useState(''), [majors, setMajors] = useState('0'), [design, setDesign] = useState(true), [ref, setRef] = useState(''), [why, setWhy] = useState('')
  const p = Number(pass), m = Number(majors)
  const ok = Number.isInteger(p) && p >= 1 && p <= 100 && Number.isInteger(m) && m >= 0 && ref.trim().length >= 3 && why.trim().length >= 10
  return <form aria-label={t.recordThreshold} onSubmit={e => { e.preventDefault(); if (ok && !disabled) void run(uatApi.threshold(summary.scope, p, m, design, ref.trim(), why.trim(), summary.thresholdSequence)) }}>
    <h2>{t.recordThreshold}</h2><p>{t.thresholdHint}</p>
    <label htmlFor="uat-pass">{t.threshold} (%)</label><input id="uat-pass" type="number" min={1} max={100} required value={pass} onChange={e => setPass(e.target.value)}/>
    <label htmlFor="uat-majors">{t.maxMajors}</label><input id="uat-majors" type="number" min={0} required value={majors} onChange={e => setMajors(e.target.value)}/>
    <label><input type="checkbox" checked={design} onChange={e => setDesign(e.target.checked)}/>{t.designConformance}</label>
    <label htmlFor="uat-agreement">{t.agreementReference}</label><input id="uat-agreement" required maxLength={200} value={ref} onChange={e => setRef(e.target.value)}/>
    <label htmlFor="uat-rationale">{t.rationale}</label><textarea id="uat-rationale" required minLength={10} maxLength={500} value={why} onChange={e => setWhy(e.target.value)}/>
    <EnterpriseActions layout="form" aria-label={t.recordThreshold}><Button type="submit" disabled={disabled || !ok}>{t.record}</Button></EnterpriseActions>
  </form>
}

function RunForm({ t, scope, roles, environments, disabled, run }: { t: PilotCopy; scope: UatScope; roles: string[]; environments: string[]; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const [role, setRole] = useState(''), [env, setEnv] = useState(''), [commit, setCommit] = useState(''), [pseudonym, setPseudonym] = useState('')
  const ok = !!role && !!env && /^[0-9a-f]{7,40}$/.test(commit) && /^[A-Z0-9-]{3,32}$/.test(pseudonym)
  return <form aria-label={t.newRun} onSubmit={e => { e.preventDefault(); if (ok && !disabled) void run(uatApi.createRun(scope, role, env, commit, pseudonym)) }}>
    <h3>{t.newRun}</h3>
    <label htmlFor="uat-role">{t.role}</label><select id="uat-role" required value={role} onChange={e => setRole(e.target.value)}><option value="">—</option>{roles.map(r => <option key={r} value={r}>{r}</option>)}</select>
    <label htmlFor="uat-env">{t.environment}</label><select id="uat-env" required value={env} onChange={e => setEnv(e.target.value)}><option value="">—</option>{environments.map(x => <option key={x} value={x}>{x}</option>)}</select>
    <label htmlFor="uat-commit">{t.build}</label><input id="uat-commit" required pattern="[0-9a-f]{7,40}" value={commit} onChange={e => setCommit(e.target.value.trim())}/>
    <label htmlFor="uat-pseudonym">{t.pseudonym}</label><input id="uat-pseudonym" required aria-describedby="uat-pseudonym-hint" value={pseudonym} onChange={e => setPseudonym(e.target.value.trim().toUpperCase())}/>
    <p id="uat-pseudonym-hint">{t.pseudonymHint}</p>
    <EnterpriseActions layout="form" aria-label={t.newRun}><Button type="submit" disabled={disabled || !ok}>{t.record}</Button></EnterpriseActions>
  </form>
}

function RunPanel({ t, detail, scenarios, actor, disabled, run }: { t: PilotCopy; detail: UatRunDetail; scenarios: string[]; actor: string; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const r = detail.run
  const [scenario, setScenario] = useState(''), [outcome, setOutcome] = useState(''), [assisted, setAssisted] = useState(false), [evidence, setEvidence] = useState('')
  const [severity, setSeverity] = useState(''), [title, setTitle] = useState(''), [ux, setUx] = useState(false), [design, setDesign] = useState(false), [statement, setStatement] = useState('')
  const latest = (id: string) => detail.results.filter(x => x.scenarioId === id).sort((a, b) => b.attempt - a.attempt)[0]
  return <section aria-label={t.runDetail} data-run={r.id}>
    <h2>{t.runDetail}: {r.role} · {r.participantPseudonym} <Badge tone="neutral">{pilotWord(t, 'run_' + r.state)}</Badge></h2>
    <p>{t.facilitator}: <code>{r.facilitatorActorId === actor ? t.you : r.facilitatorActorId}</code> · {t.attestation}: {r.attestedBy ? <code>{r.attestedBy}</code> : <span data-state="attestation-empty">{t.attestationEmpty}</span>}</p>
    <ul aria-label={t.results}>{scenarios.map(id => { const x = latest(id)
      return <li key={id} data-scenario={id} data-outcome={x?.outcome ?? 'NOT_RUN'}><code>{id}</code> · {pilotWord(t, 'outcome_' + (x?.outcome ?? 'NOT_RUN'))}
        {x?.assisted ? <> · {t.assisted}</> : null}{x?.automatedEvidenceReference ? <> · {t.automatedEvidence}: <code>{x.automatedEvidenceReference}</code></> : null}</li> })}</ul>
    <p>{t.automatedNote}</p>
    {detail.canRecord && <form aria-label={t.recordResult} onSubmit={e => { e.preventDefault(); if (scenario && outcome && !disabled)
      void run(uatApi.result(r.id, scenario, outcome, null, assisted, evidence.trim() || null, null)) }}>
      <h3>{t.recordResult}</h3>
      <label htmlFor="uat-scenario">{t.scenario}</label><select id="uat-scenario" required value={scenario} onChange={e => setScenario(e.target.value)}><option value="">—</option>{scenarios.map(s => <option key={s} value={s}>{s}</option>)}</select>
      <label htmlFor="uat-outcome">{t.outcome}</label><select id="uat-outcome" required value={outcome} onChange={e => setOutcome(e.target.value)}><option value="">—</option>{uatOutcomes.map(o => <option key={o} value={o}>{pilotWord(t, 'outcome_' + o)}</option>)}</select>
      <label><input type="checkbox" checked={assisted} onChange={e => setAssisted(e.target.checked)}/>{t.assisted}</label>
      <label htmlFor="uat-evidence">{t.automatedEvidence}</label><input id="uat-evidence" maxLength={200} value={evidence} onChange={e => setEvidence(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.recordResult}><Button type="submit" disabled={disabled || !scenario || !outcome}>{t.record}</Button></EnterpriseActions>
    </form>}
    {detail.canRecord && <form aria-label={t.recordFinding} onSubmit={e => { e.preventDefault(); if (scenario && severity && title.trim().length >= 5 && !disabled)
      void run(uatApi.finding(r.id, scenario, severity, ux, design, title.trim())) }}>
      <h3>{t.recordFinding}</h3>
      <label htmlFor="uat-severity">{t.severity}</label><select id="uat-severity" required value={severity} onChange={e => setSeverity(e.target.value)}><option value="">—</option>{uatSeverities.map(s => <option key={s} value={s}>{pilotWord(t, 'severity_' + s)}</option>)}</select>
      <label htmlFor="uat-title">{t.findingTitle}</label><input id="uat-title" required minLength={5} maxLength={200} value={title} onChange={e => setTitle(e.target.value)}/>
      <label><input type="checkbox" checked={ux} onChange={e => setUx(e.target.checked)}/>{t.ux}</label>
      <label><input type="checkbox" checked={design} onChange={e => setDesign(e.target.checked)}/>{t.designConformance}</label>
      <EnterpriseActions layout="form" aria-label={t.recordFinding}><Button type="submit" disabled={disabled || !scenario || !severity || title.trim().length < 5}>{t.record}</Button></EnterpriseActions>
    </form>}
    {detail.canRecord && <EnterpriseActions aria-label={t.submitRun}><Button type="button" disabled={disabled} onClick={() => void run(uatApi.submit(r))}>{t.submitRun}</Button></EnterpriseActions>}
    {detail.canAttest && <form aria-label={t.attest} onSubmit={e => { e.preventDefault(); if (statement.trim().length >= 10 && !disabled) void run(uatApi.attest(r, statement.trim())) }}>
      <h3>{t.attest}</h3><p>{t.attestHint}</p>
      <label htmlFor="uat-statement">{t.statement}</label><textarea id="uat-statement" required minLength={10} maxLength={500} value={statement} onChange={e => setStatement(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.attest}><Button type="submit" disabled={disabled || statement.trim().length < 10}>{t.record}</Button></EnterpriseActions>
    </form>}
    {r.state === 'SUBMITTED' && !detail.canAttest && <p data-state="attestation-other">{t.attestOther}</p>}
  </section>
}

function FindingItem({ t, f, canResolve, disabled, run }: { t: PilotCopy; f: UatFinding; canResolve: boolean; disabled: boolean; run: (c: Command) => Promise<void> }) {
  const [state, setState] = useState(''), [resolution, setResolution] = useState('')
  const states = f.severity === 'BLOCKER' ? ['RESOLVED'] : ['RESOLVED', 'ACCEPTED_RISK']
  const ok = !!state && resolution.trim().length >= 10
  return <li data-severity={f.severity}><Badge tone={f.severity === 'BLOCKER' ? 'danger' : 'warning'}>{pilotWord(t, 'severity_' + f.severity)}</Badge> <code>{f.scenarioId}</code> · {f.title}
    {f.isUx ? <> · {t.ux}</> : null}{f.designConformance ? <> · {t.designConformance}</> : null}
    {canResolve && <form aria-label={t.resolve} onSubmit={e => { e.preventDefault(); if (ok && !disabled) void run(uatApi.resolve(f, state, resolution.trim(), null)) }}>
      <select aria-label={t.resolution} value={state} onChange={e => setState(e.target.value)}><option value="">—</option>{states.map(s => <option key={s} value={s}>{pilotWord(t, 'finding_' + s)}</option>)}</select>
      <input aria-label={t.resolutionNote} minLength={10} maxLength={500} value={resolution} onChange={e => setResolution(e.target.value)}/>
      <Button type="submit" disabled={disabled || !ok}>{t.resolve}</Button></form>}
  </li>
}
