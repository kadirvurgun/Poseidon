import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { localizedCopy } from '../i18n/messages';
import { useAppLanguage, type Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { EnterpriseActions } from './EnterprisePatterns';
import { ApiError, session, type Session } from './api';
import { isGuid } from './identifiers';
import { tugApi, type TugBoard } from './tug-api';
import { send, type Command } from './tug-readiness-api';
import { categories, commsStates, emptyDrydock, emptyLog, evidencePurposes, fieldOpsApi, fuelUnits, linkKinds, loadUnits, priorities, releaseResults, watches, winchStates,
  type Authority, type DrydockContent, type DrydockList, type DrydockView, type EventInput, type ExecutionView, type JobView, type LogContent, type LogList, type LogView,
  type ScopeItem } from './field-ops-api';
import './tug.css';

const copy = localizedCopy('FieldOps_tsx_copy');
type Copy = (typeof copy)[AppLocale];
export const fieldOpsScreens = ['towage-execution', 'tug-daily-log', 'drydock-planning'] as const;
type Screen = typeof fieldOpsScreens[number];
type Notice = '' | 'done' | 'uncertain' | 'conflict' | 'denied' | 'failed' | 'incomplete' | 'queued';
type Sync = 'SYNCED' | 'LOCAL_PENDING' | 'CONFLICT' | 'FAILED';
const label = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key;
const blank = (v: string) => v.trim() === '' ? null : v;

export function fieldOpsRoute(href: string) {
  const url = new URL(href, 'http://local'), params = url.searchParams;
  const match = /^\/app\/(towage-execution|tug-daily-log|drydock-planning)\/?$/.exec(url.pathname);
  const value = (k: string) => params.get(k) ?? '';
  const allowed = match?.[1] === 'towage-execution' ? ['vessel', 'job', 'id'] : ['vessel', 'id'];
  const invalid = !match || [...params.keys()].some(k => !allowed.includes(k) || params.getAll(k).length !== 1) || allowed.some(k => value(k) !== '' && !isGuid(value(k)));
  return { screen: (match?.[1] ?? 'towage-execution') as Screen, vessel: value('vessel'), job: value('job'), id: value('id'), invalid };
}
export const fieldOpsHref = (screen: Screen, vessel: string, extra: Record<string, string> = {}) =>
  `/app/${screen}?vessel=${vessel}${Object.entries(extra).filter(([, v]) => v).map(([k, v]) => `&${k}=${v}`).join('')}`;

function Field({ id, text, value, onChange, disabled, long, max = 200 }: { id: string; text: string; value: string | null; onChange: (v: string | null) => void; disabled: boolean; long?: boolean; max?: number }) {
  return <><label htmlFor={id}>{text}</label>{long
    ? <textarea id={id} maxLength={max} disabled={disabled} value={value ?? ''} onChange={e => onChange(blank(e.target.value))}/>
    : <input id={id} maxLength={max} disabled={disabled} value={value ?? ''} onChange={e => onChange(blank(e.target.value))}/>}</>;
}
function AuthorityPicker({ id, authorities, value, onChange, t, disabled }: { id: string; authorities: Authority[]; value: string; onChange: (v: string) => void; t: Copy; disabled: boolean }) {
  return <><label htmlFor={id}>{t.authority}</label>
    <select id={id} required value={value} disabled={disabled} onChange={e => onChange(e.target.value)}>
      <option value="">—</option>{authorities.map(a => <option key={a.id} value={a.id}>{label(t, 'duty_' + a.duty)}</option>)}</select></>;
}
// Codes stay visible verbatim; a code with a localized label (prefix + code in the copy) also shows that label.
function Codes({ items, empty, t, prefix }: { items: string[]; empty: string; t?: Copy; prefix?: string }) {
  const text = (b: string) => t && prefix ? (t as Record<string, string>)[prefix + b] : undefined;
  return items.length === 0 ? <p>{empty}</p> : <ul>{items.map(b => <li key={b} data-code={b}><code>{b}</code>{text(b) && <> · {text(b)}</>}</li>)}</ul>;
}
// Verified/reviewed states are informational, never a green "safe" tone; open or unknown states warn.
function StateBadge({ state, t }: { state: string; t: Copy }) {
  const tone = ['VERIFIED', 'REVIEWED', 'RECONCILED', 'LINKS_CURRENT', 'COMPLETED', 'CURRENT', 'LINKED'].includes(state) ? 'info'
    : ['ABORTED', 'REJECTED', 'CONFLICT', 'FAILED', 'STALE', 'UNAVAILABLE', 'OVERDUE', 'RETURNED'].includes(state) ? 'danger' : 'warning';
  return <Badge tone={tone} data-state={state}>{label(t, 'state_' + state)}</Badge>;
}
function ReasonForm({ id, text, t, disabled, min = 10, onSubmit, children }: { id: string; text: string; t: Copy; disabled: boolean; min?: number; onSubmit: (reason: string) => void; children?: ReactNode }) {
  const [reason, setReason] = useState('');
  return <form aria-label={text} onSubmit={e => { e.preventDefault(); if (reason.trim().length >= min) onSubmit(reason.trim()); }}>
    {children}
    <label htmlFor={id}>{t.reason}</label><textarea id={id} required minLength={min} maxLength={4000} value={reason} onChange={e => setReason(e.target.value)}/>
    <EnterpriseActions layout="form" aria-label={text}><Button type="submit" disabled={disabled || reason.trim().length < min}>{text}</Button></EnterpriseActions></form>;
}

export function FieldOpsWorkspace() {
  const [lang, language] = useAppLanguage(), t = copy[lang];
  const [route, setRoute] = useState(() => fieldOpsRoute(window.location.href));
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true), [error, setError] = useState(0), [offline, setOffline] = useState(!navigator.onLine);
  const [board, setBoard] = useState<TugBoard | null>(null), [job, setJob] = useState<JobView | null>(null), [execution, setExecution] = useState<ExecutionView | null>(null);
  const [logs, setLogs] = useState<LogList | null>(null), [log, setLog] = useState<LogView | null>(null);
  const [docks, setDocks] = useState<DrydockList | null>(null), [dock, setDock] = useState<DrydockView | null>(null);
  const [notice, setNotice] = useState<Notice>(''), [busy, setBusy] = useState(false), [retained, setRetained] = useState<Command | null>(null);
  // Daily log offline outbox: in memory only (durable tug capture is not registered); the exact key/body is kept.
  const [outbox, setOutbox] = useState<Command | null>(null), [sync, setSync] = useState<Sync>('SYNCED');
  const lock = useRef(false), mounted = useRef(true), offlineRef = useRef(!navigator.onLine), outboxRef = useRef<Command | null>(null);
  const runRef = useRef<Run | null>(null);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => { outboxRef.current = outbox; }, [outbox]);
  useEffect(() => {
    // Replays the queued daily-log command with its original key once the connection returns.
    const on = () => { offlineRef.current = false; setOffline(false); if (outboxRef.current) void runRef.current?.(outboxRef.current, undefined, true); };
    const off = () => { offlineRef.current = true; setOffline(true); }, pop = () => { setRoute(fieldOpsRoute(window.location.href)); setReload(n => n + 1); };
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop);
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop); };
  }, []);
  const move = (href: string) => { if (retained || busy || outbox) return; history.pushState(null, '', href); setRoute(fieldOpsRoute(window.location.href)); setNotice(''); setReload(n => n + 1); };
  useEffect(() => {
    const c = new AbortController();
    (async () => {
      await Promise.resolve();
      if (c.signal.aborted) return;
      setLoading(true); setError(0); setBoard(null); setJob(null); setExecution(null); setLogs(null); setLog(null); setDocks(null); setDock(null);
      if (route.invalid) throw new ApiError(400, 'invalid_route');
      setUser(await session(c.signal));
      if (!route.vessel) return;
      if (route.screen === 'towage-execution') {
        if (route.id) setExecution(await fieldOpsApi.execution(route.vessel, route.id, c.signal));
        else if (route.job) setJob(await fieldOpsApi.job(route.vessel, route.job, c.signal));
        else setBoard(await tugApi.board(route.vessel, '', '', 0, c.signal, 'Submitted'));
      } else if (route.screen === 'tug-daily-log') {
        if (route.id) setLog(await fieldOpsApi.log(route.vessel, route.id, c.signal)); else setLogs(await fieldOpsApi.logs(route.vessel, c.signal));
      } else if (route.id) setDock(await fieldOpsApi.drydock(route.vessel, route.id, c.signal)); else setDocks(await fieldOpsApi.drydocks(route.vessel, c.signal));
    })().then(() => { if (!c.signal.aborted) setLoading(false); }).catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e.status : 500); setLoading(false); });
    return () => c.abort();
  }, [route, reload]);

  // One command at a time; an uncertain outcome keeps the exact key/body for an identical retry. After a
  // confirmed receipt the canonical resource id moves into the URL before any optional re-read.
  const run = useCallback(async (command: Command, then?: { screen: Screen; extra: Record<string, string> }, queueOffline = false): Promise<boolean> => {
    if (lock.current) return false;
    if (offlineRef.current || !navigator.onLine) {
      if (queueOffline) { setOutbox(command); setSync('LOCAL_PENDING'); setNotice('queued'); }
      return false;
    }
    lock.current = true; setBusy(true); setNotice(''); setRetained(command);
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000);
    let sent = false;
    try {
      const s = await session(c.signal);
      if (!s.authenticated || !s.csrfToken || user && `${s.tenantId}/${s.actorId}` !== `${user.tenantId}/${user.actorId}`) throw new ApiError(403, 'identity_boundary_changed');
      sent = true;
      await send(command, s.csrfToken, c.signal);
      if (!mounted.current) return false;
      setRetained(null); setOutbox(null); setSync('SYNCED'); setNotice('done');
      if (then) { history.replaceState(null, '', fieldOpsHref(then.screen, route.vessel, then.extra)); setRoute(fieldOpsRoute(window.location.href)); }
      setReload(n => n + 1); return true;
    } catch (e) {
      if (!mounted.current) return false;
      const definite = e instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(e.status);
      if (!sent || definite) setRetained(null);
      if (definite) setOutbox(null);
      if (queueOffline) setSync(e instanceof ApiError && e.status === 409 ? 'CONFLICT' : definite ? 'FAILED' : 'LOCAL_PENDING');
      setNotice(sent && !definite ? 'uncertain' : e instanceof ApiError && ['towage_start_blocked', 'towage_resume_blocked', 'towage_record_incomplete', 'daily_log_incomplete', 'drydock_record_incomplete'].includes(e.code) ? 'incomplete'
        : e instanceof ApiError && e.status === 409 ? 'conflict' : e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : 'failed');
      return false;
    } finally { clearTimeout(timeout); lock.current = false; if (mounted.current) setBusy(false); }
  }, [user, route.vessel]);
  useEffect(() => { runRef.current = run; }, [run]);

  const disabled = busy || !!retained && notice !== 'uncertain';
  const titles: Record<Screen, string> = { 'towage-execution': t.screenExecution, 'tug-daily-log': t.screenDailyLog, 'drydock-planning': t.screenDrydock };
  const message = notice ? label(t, 'notice_' + notice) : '';
  const props = { vessel: route.vessel, t, disabled, offline, run };
  return <div className="product-shell enterprise-shell tug-workspace">
    <EnterpriseChrome session={user} vessel={route.vessel || null} language={lang} onLanguageChange={language} onNavigate={href => { if (!retained && !busy && !outbox) window.location.assign(href); }}
      title={titles[route.screen]} mainId="field-ops-main"/>
    <main id="field-ops-main" tabIndex={-1} aria-busy={loading || busy}>
      <div className="enterprise-page-heading"><div><p className="enterprise-page-eyebrow">{t.title}</p><h1>{titles[route.screen]}</h1></div></div>
      <p className="tug-planning-boundary">{label(t, 'boundary_' + route.screen)}</p>
      {offline && <p role="alert">{route.screen === 'tug-daily-log' ? t.offlineLog : t.offline}</p>}
      {route.screen === 'tug-daily-log' && <p>{t.syncState}: <StateBadge state={sync} t={t}/></p>}
      {message && <div role={notice === 'done' ? 'status' : 'alert'}><p>{message}</p>
        {notice === 'uncertain' && retained && <Button type="button" disabled={busy || offline} onClick={() => void run(retained, undefined, route.screen === 'tug-daily-log')}>{t.retry}</Button>}
        {notice === 'conflict' && <Button type="button" variant="outline" onClick={() => { setNotice(''); setOutbox(null); setSync('SYNCED'); setReload(n => n + 1); }}>{t.reload}</Button>}</div>}
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error > 0 && <div role="alert"><p>{[401, 403].includes(error) ? t.denied : error === 404 || error === 400 ? t.notFound : t.unavailable}</p>
        <Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>}
      {!loading && !error && !route.vessel && <p role="status">{t.selectVessel}</p>}
      {!loading && !error && board && <section aria-label={t.selectJob}><h2>{t.selectJob}</h2>{board.items.length === 0 ? <p>{t.noJobs}</p> :
        <ul>{board.items.map(row => <li key={row.request.id}><a href={fieldOpsHref('towage-execution', route.vessel, { job: row.request.id })}
          onClick={e => { e.preventDefault(); move(fieldOpsHref('towage-execution', route.vessel, { job: row.request.id })); }}>{row.request.jobNumber}</a></li>)}</ul>}</section>}
      {!loading && !error && job && <JobPanel data={job} {...props} open={id => move(fieldOpsHref('towage-execution', route.vessel, { id }))}/>}
      {!loading && !error && execution && <ExecutionPanel key={`${execution.execution.id}:${execution.execution.version}`} data={execution} {...props}/>}
      {!loading && !error && logs && <LogListPanel data={logs} {...props} open={id => move(fieldOpsHref('tug-daily-log', route.vessel, { id }))}/>}
      {!loading && !error && log && <LogPanel key={`${log.log.id}:${log.log.version}`} data={log} {...props}/>}
      {!loading && !error && docks && <DrydockListPanel data={docks} {...props} open={id => move(fieldOpsHref('drydock-planning', route.vessel, { id }))}/>}
      {!loading && !error && dock && <DrydockPanel key={`${dock.package.id}:${dock.package.version}`} data={dock} {...props}/>}
    </main></div>;
}

type Run = (c: Command, then?: { screen: Screen; extra: Record<string, string> }, queueOffline?: boolean) => Promise<boolean>;
type PanelProps = { vessel: string; t: Copy; disabled: boolean; offline: boolean; run: Run };

// ---- DEV-089 towage execution ----
function JobPanel({ data, vessel, t, disabled, offline, run, open }: PanelProps & { data: JobView; open: (id: string) => void }) {
  return <section aria-label={t.startGate}>
    <p>{t.job}: <strong>{data.jobNumber}</strong> · {data.jobStatus}</p>
    {data.tugs.length === 0 && <p role="status">{t.noAssignedTugs}</p>}
    {data.tugs.map(g => <StartGatePanel key={g.tugId} gate={g} job={data.jobId} vessel={vessel} t={t} disabled={disabled || offline} run={run} open={open}/>)}
  </section>;
}
function StartGatePanel({ gate, job, vessel, t, disabled, run, open }: { gate: JobView['tugs'][number]; job: string; vessel: string; t: Copy; disabled: boolean; run: Run; open: (id: string) => void }) {
  const [authority, setAuthority] = useState(gate.authorities.length === 1 ? gate.authorities[0].id : ''), [position, setPosition] = useState<string | null>(null);
  const [id] = useState(() => crypto.randomUUID());
  return <article aria-label={`${t.tug} ${gate.tugId.slice(0, 8)}`} data-tug={gate.tugId}>
    <h2>{t.tug} {gate.tugId.slice(0, 8)}</h2>
    <p>{t.readinessDecision}: {gate.decision ? label(t, 'decision_' + gate.decision) : t.unknownValue}</p>
    <h3>{t.blockers}</h3><Codes items={gate.blockers} empty={t.noBlockers} t={t} prefix="blocker_"/>
    {gate.executionId && <p><a href={fieldOpsHref('towage-execution', vessel, { id: gate.executionId })} onClick={e => { e.preventDefault(); open(gate.executionId!); }}>{t.openExecution}</a>
      {' '}{gate.executionState && <StateBadge state={gate.executionState} t={t}/>}</p>}
    {gate.canStart && !gate.executionId && <form aria-label={t.start} onSubmit={e => { e.preventDefault(); if (authority) void run(fieldOpsApi.start(vessel, id, job, gate, authority, position), { screen: 'towage-execution', extra: { id } }); }}>
      <p>{t.startConfirm}</p>
      <AuthorityPicker id={`start-authority-${gate.tugId}`} authorities={gate.authorities} value={authority} onChange={setAuthority} t={t} disabled={disabled}/>
      <Field id={`start-position-${gate.tugId}`} text={t.startPosition} value={position} onChange={setPosition} disabled={disabled} max={400}/>
      <EnterpriseActions layout="form" aria-label={t.start}><Button type="submit" disabled={disabled || !authority}>{t.start}</Button></EnterpriseActions></form>}
  </article>;
}

const emptyEvent = (kind: string): EventInput => ({ kind, occurredAt: '', value: null, unit: kind === 'LOAD_OBSERVED' ? 't' : null, result: null, text: null, reference: null,
  weather: kind === 'WEATHER_OBSERVED' ? { windSpeedKn: null, windDirection: null, seaState: null, visibility: null, current: null } : null });
function eventValid(e: EventInput) {
  switch (e.kind) {
    case 'LOAD_OBSERVED': return !!e.value && /^\d{1,7}(?:\.\d{1,3})?$/.test(e.value) && !!e.unit;
    case 'COMMS_CHECK': case 'WINCH_STATE': case 'EMERGENCY_RELEASE_TEST': return !!e.result;
    case 'WEATHER_OBSERVED': return !!e.weather && Object.values(e.weather).some(v => v !== null) && (e.weather.windSpeedKn === null || /^\d{1,7}(?:\.\d{1,3})?$/.test(e.weather.windSpeedKn));
    case 'POSITION': case 'NOTE': return !!e.text;
    case 'EVIDENCE': return !!e.result && !!e.reference;
    default: return true;
  }
}
function ExecutionPanel({ data, vessel, t, disabled, offline, run }: PanelProps & { data: ExecutionView }) {
  const x = data.execution, l = data.ledger, off = disabled || offline;
  const [event, setEvent] = useState<EventInput>(() => emptyEvent(data.recordableKinds[0] ?? 'NOTE'));
  const [action, setAction] = useState(data.allowedActions[0] ?? ''), [authority, setAuthority] = useState(data.authorities.length === 1 ? data.authorities[0].id : '');
  const [endPosition, setEndPosition] = useState<string | null>(null);
  const set = (patch: Partial<EventInput>) => setEvent(e => ({ ...e, ...patch }));
  const options = (key: string) => key === 'COMMS_CHECK' ? commsStates : key === 'WINCH_STATE' ? winchStates : key === 'EMERGENCY_RELEASE_TEST' ? releaseResults : evidencePurposes;
  return <section aria-label={t.screenExecution}>
    <p>{t.job}: {data.jobNumber ?? t.unknownValue} · {t.assistedVessel}: {data.assistedVessel ?? t.unknownValue}</p>
    <p>{t.state}: <StateBadge state={x.state} t={t}/> · {t.verification}: <StateBadge state={x.verificationState} t={t}/> · r{x.revision}</p>
    <p className="tug-planning-boundary">{t.authorityBoundary}</p>
    <dl className="field-ops-ledger">
      <dt>{t.startedAt}</dt><dd><time dateTime={x.startedAt}>{x.startedAt}</time></dd>
      <dt>{t.towlineConnected}</dt><dd>{l.towlineConnectedAt ? <time dateTime={l.towlineConnectedAt}>{l.towlineConnectedAt}</time> : t.notRecorded}</dd>
      <dt>{t.towlineReleased}</dt><dd>{l.towlineReleasedAt ? <time dateTime={l.towlineReleasedAt}>{l.towlineReleasedAt}</time> : l.towlineConnected ? t.stillConnected : t.notRecorded}</dd>
      <dt>{t.maxLoad}</dt><dd>{l.maxTowLoad.length === 0 ? t.notRecorded : l.maxTowLoad.map(m => `${m.value} ${m.unit} (${m.samples})`).join(' · ')}</dd>
      <dt>{t.comms}</dt><dd><StateBadge state={l.communicationState} t={t}/></dd>
      <dt>{t.winch}</dt><dd><StateBadge state={l.winchState} t={t}/></dd>
      <dt>{t.releaseTest}</dt><dd><StateBadge state={l.emergencyReleaseTested} t={t}/></dd>
      <dt>{t.activeMinutes}</dt><dd>{Math.round(l.activeSeconds / 60)}</dd>
      <dt>{t.safetyEvents}</dt><dd><Badge tone="warning">{t.unknownValue}</Badge></dd>
    </dl>
    {data.completeBlockers.length > 0 && <><h3>{t.completeBlockers}</h3><Codes items={data.completeBlockers} empty={t.noBlockers}/></>}
    {data.canRecord && <form aria-label={t.recordEvent} onSubmit={e => { e.preventDefault(); if (eventValid(event)) void run(fieldOpsApi.record(vessel, x, { ...event, occurredAt: new Date().toISOString() })); }}>
      <h2>{t.recordEvent}</h2>
      <label htmlFor="exe-kind">{t.eventKind}</label><select id="exe-kind" disabled={off} value={event.kind} onChange={e => setEvent(emptyEvent(e.target.value))}>
        {data.recordableKinds.map(k => <option key={k} value={k}>{label(t, 'kind_' + k)}</option>)}</select>
      {event.kind === 'LOAD_OBSERVED' && <><Field id="exe-value" text={t.value} value={event.value} onChange={v => set({ value: v })} disabled={off} max={12}/>
        <label htmlFor="exe-unit">{t.unit}</label><select id="exe-unit" disabled={off} value={event.unit ?? ''} onChange={e => set({ unit: e.target.value })}>{loadUnits.map(u => <option key={u} value={u}>{u}</option>)}</select></>}
      {['COMMS_CHECK', 'WINCH_STATE', 'EMERGENCY_RELEASE_TEST', 'EVIDENCE'].includes(event.kind) && <><label htmlFor="exe-result">{t.result}</label>
        <select id="exe-result" disabled={off} value={event.result ?? ''} onChange={e => set({ result: e.target.value || null })}><option value="">—</option>
          {options(event.kind).map(r => <option key={r} value={r}>{label(t, 'state_' + r)}</option>)}</select></>}
      {event.kind === 'WEATHER_OBSERVED' && event.weather && <fieldset><legend>{t.weather}</legend>
        <Field id="exe-wind" text={t.windSpeed} value={event.weather.windSpeedKn} onChange={v => set({ weather: { ...event.weather!, windSpeedKn: v } })} disabled={off} max={12}/>
        <Field id="exe-winddir" text={t.windDirection} value={event.weather.windDirection} onChange={v => set({ weather: { ...event.weather!, windDirection: v } })} disabled={off}/>
        <Field id="exe-sea" text={t.seaState} value={event.weather.seaState} onChange={v => set({ weather: { ...event.weather!, seaState: v } })} disabled={off}/>
        <Field id="exe-vis" text={t.visibility} value={event.weather.visibility} onChange={v => set({ weather: { ...event.weather!, visibility: v } })} disabled={off}/>
        <Field id="exe-current" text={t.current} value={event.weather.current} onChange={v => set({ weather: { ...event.weather!, current: v } })} disabled={off}/></fieldset>}
      {event.kind === 'EVIDENCE' && <Field id="exe-ref" text={t.evidenceRef} value={event.reference} onChange={v => set({ reference: v })} disabled={off} max={400}/>}
      <Field id="exe-text" text={t.note} long value={event.text} onChange={v => set({ text: v })} disabled={off} max={4000}/>
      <EnterpriseActions layout="form" aria-label={t.recordEvent}><Button type="submit" disabled={off || !eventValid(event)}>{t.recordEvent}</Button></EnterpriseActions></form>}
    {data.canTransition && data.allowedActions.length > 0 && <ReasonForm id="exe-transition-reason" text={label(t, 'action_' + action)} t={t} disabled={off || !authority || !action}
      min={action === 'ABORT' || action === 'COMPLETE' ? 20 : 10} onSubmit={r => void run(fieldOpsApi.transition(vessel, x, action, r, authority, endPosition))}>
      <h2>{t.masterActions}</h2>
      <label htmlFor="exe-action">{t.action}</label><select id="exe-action" disabled={off} value={action} onChange={e => setAction(e.target.value)}>
        {data.allowedActions.map(a => <option key={a} value={a}>{label(t, 'action_' + a)}</option>)}</select>
      <AuthorityPicker id="exe-authority" authorities={data.authorities} value={authority} onChange={setAuthority} t={t} disabled={off}/>
      {(action === 'COMPLETE' || action === 'ABORT') && <Field id="exe-end" text={t.endPosition} value={endPosition} onChange={setEndPosition} disabled={off} max={400}/>}
    </ReasonForm>}
    {data.canVerify && <ReasonForm id="exe-verify-reason" text={t.verify} t={t} disabled={off} onSubmit={r => void run(fieldOpsApi.verify(vessel, x, 'VERIFIED', r))}/>}
    {data.canVerify && <ReasonForm id="exe-reject-reason" text={t.rejectVerification} t={t} disabled={off} onSubmit={r => void run(fieldOpsApi.verify(vessel, x, 'REJECTED', r))}/>}
    <h2>{t.timeline}</h2>
    <ol className="field-ops-timeline">{data.timeline.map(e => <li key={e.sequence} data-kind={e.kind}>
      <time dateTime={e.occurredAt}>{e.occurredAt}</time> · {label(t, 'kind_' + e.kind)}
      {e.value && <> · {e.value} {e.unit}</>}{e.result && <> · {label(t, 'state_' + e.result)}</>}{e.reference && <> · <code>{e.reference}</code></>}
      {e.weather && <> · {[e.weather.windSpeedKn && `${e.weather.windSpeedKn} kn`, e.weather.windDirection, e.weather.seaState, e.weather.visibility, e.weather.current].filter(Boolean).join(', ')}</>}
      {e.position && <> · {e.position}</>}{e.reason && <> · {e.reason}</>}{e.text && <> · {e.text}</>}{e.duty && <> · {label(t, 'duty_' + e.duty)}</>}
    </li>)}</ol>
  </section>;
}

// ---- DEV-090 tug daily log ----
function LogListPanel({ data, vessel, t, disabled, offline, run, open }: PanelProps & { data: LogList; open: (id: string) => void }) {
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10)), [watch, setWatch] = useState<string>('OTHER');
  const [id] = useState(() => crypto.randomUUID());
  return <section aria-label={t.screenDailyLog}>
    {data.items.length === 0 ? <p role="status">{t.noLogs}</p> : <ul>{data.items.map(i => <li key={i.id}>
      <a href={fieldOpsHref('tug-daily-log', vessel, { id: i.id })} onClick={e => { e.preventDefault(); open(i.id); }}>{i.logDate} · {i.watch}</a> <StateBadge state={i.state} t={t}/> <StateBadge state={i.masterReview} t={t}/></li>)}</ul>}
    {data.canWrite && <form aria-label={t.createLog} onSubmit={e => { e.preventDefault(); void run(fieldOpsApi.createLog(vessel, id, date, watch, emptyLog()), { screen: 'tug-daily-log', extra: { id } }, true); }}>
      <label htmlFor="log-date">{t.logDate}</label><input id="log-date" type="date" required value={date} disabled={disabled} onChange={e => setDate(e.target.value)}/>
      <label htmlFor="log-watch">{t.watch}</label><select id="log-watch" value={watch} disabled={disabled} onChange={e => setWatch(e.target.value)}>
        {watches.map(w => <option key={w} value={w}>{label(t, 'watch_' + w)}</option>)}</select>
      <EnterpriseActions layout="form" aria-label={t.createLog}><Button type="submit" disabled={disabled || !date}>{offline ? t.queueOffline : t.createLog}</Button></EnterpriseActions></form>}
  </section>;
}
const numberOk = (v: string | null) => v === null || /^\d{1,9}(?:\.\d{1,2})?$/.test(v);
function LogPanel({ data, vessel, t, disabled, offline, run }: PanelProps & { data: LogView }) {
  const l = data.log, r = data.reconciliation;
  const [form, setForm] = useState<LogContent>(() => l.content), edit = !data.canEdit || disabled;
  const [authority, setAuthority] = useState(data.authorities.length === 1 ? data.authorities[0].id : ''), [defects, setDefects] = useState(l.content.defectIds.join('\n'));
  const set = (patch: Partial<LogContent>) => setForm(f => ({ ...f, ...patch }));
  const defectIds = defects.split(/\s+/).filter(Boolean);
  const valid = form.meters.every(m => m.meter.trim() !== '' && numberOk(m.opening) && numberOk(m.closing)) && numberOk(form.fuel.quantity) && defectIds.every(isGuid) &&
    new Set(form.meters.map(m => m.meter.trim())).size === form.meters.length;
  return <section aria-label={t.screenDailyLog}>
    <p>{l.logDate} · {label(t, 'watch_' + l.watch)} · {t.state}: <StateBadge state={l.state} t={t}/> · {t.masterReview}: <StateBadge state={data.masterReview} t={t}/> · r{l.revision}</p>
    <p>{t.capturedAt}: {l.capturedAt ? <time dateTime={l.capturedAt}>{l.capturedAt}</time> : t.notRecorded} · {t.syncedAt}: <time dateTime={l.syncedAt}>{l.syncedAt}</time></p>
    <h2>{t.reconciliation}</h2>
    <p><StateBadge state={r.state} t={t}/> · {t.jobsCompleted}: {r.jobsCompleted} · {t.operatingHours}: {r.operatingHours ?? t.unknownValue}{r.openInterval && ` (${t.openInterval})`}
      {' '}· {t.meterHours}: {r.meterHours ?? t.unknownValue} · {t.defectsReported}: {data.defectsReported} · {t.safetyEvents}: <Badge tone="warning">{t.unknownValue}</Badge></p>
    {r.reasons.length > 0 && <ul>{r.reasons.map((x, i) => <li key={i} data-reason={x.code}><code>{x.code}</code>{x.subject && <> · {x.subject}</>}</li>)}</ul>}
    {l.state === 'DRAFT' && <><h3>{t.blockers}</h3><Codes items={data.submitBlockers} empty={t.noBlockers}/></>}
    <form aria-label={t.screenDailyLog} onSubmit={e => { e.preventDefault(); if (valid) void run(fieldOpsApi.updateLog(vessel, l, { ...form, defectIds }), undefined, true); }}>
      <Field id="log-tz" text={t.timeZone} value={form.timeZone} onChange={v => set({ timeZone: v })} disabled={edit} max={64}/>
      <fieldset><legend>{t.meters}</legend>
        {form.meters.map((m, i) => { const up = (patch: Partial<typeof m>) => set({ meters: form.meters.map(y => y.id === m.id ? { ...y, ...patch } : y) });
          return <div key={m.id}><Field id={`log-meter-${i}`} text={t.meter} value={m.meter} onChange={v => up({ meter: v ?? '' })} disabled={edit} max={60}/>
            <Field id={`log-open-${i}`} text={t.opening} value={m.opening} onChange={v => up({ opening: v })} disabled={edit} max={14}/>
            <Field id={`log-close-${i}`} text={t.closing} value={m.closing} onChange={v => up({ closing: v })} disabled={edit} max={14}/>
            <Field id={`log-reset-${i}`} text={t.resetEvidence} value={m.resetEvidenceRef} onChange={v => up({ resetEvidenceRef: v })} disabled={edit} max={400}/>
            <Button type="button" variant="outline" disabled={edit} onClick={() => set({ meters: form.meters.filter(y => y.id !== m.id) })}>{t.remove}</Button></div>; })}
        <Button type="button" variant="outline" disabled={edit || form.meters.length >= 20} onClick={() => set({ meters: [...form.meters, { id: crypto.randomUUID(), meter: '', opening: null, closing: null, resetEvidenceRef: null }] })}>{t.add}</Button>
      </fieldset>
      <fieldset><legend>{t.fuel}</legend>
        <Field id="log-fuel" text={t.value} value={form.fuel.quantity} onChange={v => set({ fuel: { ...form.fuel, quantity: v } })} disabled={edit} max={14}/>
        <label htmlFor="log-fuel-unit">{t.unit}</label><select id="log-fuel-unit" disabled={edit} value={form.fuel.unit ?? ''} onChange={e => set({ fuel: { ...form.fuel, unit: e.target.value || null } })}>
          <option value="">{t.unknownOption}</option>{fuelUnits.map(u => <option key={u} value={u}>{u}</option>)}</select>
        <Field id="log-fuel-src" text={t.fuelSource} value={form.fuel.sourceRef} onChange={v => set({ fuel: { ...form.fuel, sourceRef: v } })} disabled={edit} max={400}/>
      </fieldset>
      <fieldset><legend>{t.executions}</legend>{data.executions.length === 0 ? <p>{t.noExecutions}</p> : data.executions.map(x => <label key={x.id}>
        <input type="checkbox" disabled={edit} checked={form.executionIds.includes(x.id)} onChange={e => set({ executionIds: e.target.checked ? [...form.executionIds, x.id] : form.executionIds.filter(y => y !== x.id) })}/>
        {x.startedAt.slice(0, 16)} · {label(t, 'state_' + x.state)}</label>)}</fieldset>
      <label htmlFor="log-defects">{t.defectIds}</label><textarea id="log-defects" disabled={edit} value={defects} onChange={e => setDefects(e.target.value)}/>
      <Field id="log-summary" text={t.dailySummary} long value={form.dailySummary} onChange={v => set({ dailySummary: v })} disabled={edit} max={8000}/>
      {data.canEdit && <EnterpriseActions layout="form" aria-label={t.save}><Button type="submit" disabled={edit || !valid}>{offline ? t.queueOffline : t.save}</Button></EnterpriseActions>}
    </form>
    {data.canSubmit && <EnterpriseActions layout="form" aria-label={t.submit}><Button type="button" disabled={disabled || offline} onClick={() => void run(fieldOpsApi.submitLog(vessel, l), undefined, true)}>{t.submit}</Button></EnterpriseActions>}
    {data.canReview && <ReasonForm id="log-review-reason" text={t.masterVerify} t={t} disabled={disabled || offline || !authority} onSubmit={x => void run(fieldOpsApi.reviewLog(vessel, l, 'VERIFY', x, authority))}>
      <AuthorityPicker id="log-review-authority" authorities={data.authorities} value={authority} onChange={setAuthority} t={t} disabled={disabled}/></ReasonForm>}
    {data.canReview && <ReasonForm id="log-return-reason" text={t.masterReturn} t={t} disabled={disabled || offline || !authority} onSubmit={x => void run(fieldOpsApi.reviewLog(vessel, l, 'RETURN', x, authority))}/>}
    <h2>{t.reviews}</h2>{data.reviews.length === 0 ? <p>{t.noReviews}</p> : <ul>{data.reviews.map(v => <li key={v.id} data-decision={v.decision}>
      {label(t, 'decision_' + v.decision)} · {label(t, 'duty_' + v.duty)} · <StateBadge state={v.reconciliation} t={t}/> · {v.reason} · <time dateTime={v.decidedAt}>{v.decidedAt}</time></li>)}</ul>}
    <h2>{t.revisions}</h2><ul>{data.revisions.map(v => <li key={v.revision}>r{v.revision} · {label(t, 'state_' + v.state)} · <code>{v.reasonCode}</code> · <time dateTime={v.syncedAt}>{v.syncedAt}</time></li>)}</ul>
  </section>;
}

// ---- DEV-111 drydock planning ----
function DrydockListPanel({ data, vessel, t, disabled, offline, run, open }: PanelProps & { data: DrydockList; open: (id: string) => void }) {
  const [id] = useState(() => crypto.randomUUID());
  return <section aria-label={t.screenDrydock}>
    {data.items.length === 0 ? <p role="status">{t.noPackages}</p> : <ul>{data.items.map(i => <li key={i.id}>
      <a href={fieldOpsHref('drydock-planning', vessel, { id: i.id })} onClick={e => { e.preventDefault(); open(i.id); }}>{i.projectReference ?? t.unnamed}</a>
      {' '}<StateBadge state={i.state} t={t}/> · {i.yard ?? t.unknownValue} · {i.plannedStart ?? '—'} → {i.plannedFinish ?? '—'} · {i.items}</li>)}</ul>}
    {data.canWrite && <EnterpriseActions layout="form" aria-label={t.createPackage}><Button type="button" disabled={disabled || offline}
      onClick={() => void run(fieldOpsApi.saveDrydock(vessel, id, null, emptyDrydock()), { screen: 'drydock-planning', extra: { id } })}>{t.createPackage}</Button></EnterpriseActions>}
  </section>;
}
function DrydockPanel({ data, vessel, t, disabled, offline, run }: PanelProps & { data: DrydockView }) {
  const p = data.package, r = data.readiness, off = disabled || offline;
  const [form, setForm] = useState<DrydockContent>(() => p.content), edit = !data.canEdit || off;
  const [action, setAction] = useState(data.allowedActions[0] ?? ''), [note, setNote] = useState<string | null>(null);
  const set = (patch: Partial<DrydockContent>) => setForm(f => ({ ...f, ...patch }));
  const status = (item: string) => r.items.find(i => i.itemId === item);
  const valid = form.items.every(i => i.title.trim() !== '' && i.links.every(l => isGuid(l.targetId) && l.pinnedRevision >= 1)) && form.completionEvidence.every(e => e.reference.trim() !== '') &&
    (form.external.currency === null || /^[A-Z]{3}$/.test(form.external.currency)) && (form.external.estimatedCost === null || /^\d{1,12}(?:\.\d{1,2})?$/.test(form.external.estimatedCost));
  const upItem = (id: string, patch: Partial<ScopeItem>) => set({ items: form.items.map(i => i.id === id ? { ...i, ...patch } : i) });
  return <section aria-label={t.screenDrydock}>
    <p>{t.state}: <StateBadge state={p.state} t={t}/> · r{p.revision} · {t.readiness}: <StateBadge state={r.state} t={t}/></p>
    <p className="tug-planning-boundary">{t.readinessMeaning}</p>
    <p>{t.unlinked}: {r.unlinked} · {t.overdue}: {r.overdue} · {t.stale}: {r.stale} · {t.unavailableLinks}: {r.unavailable}</p>
    {r.stale > 0 && <p role="alert">{t.staleNotice}</p>}
    {data.unreadableLinkKinds.length > 0 && <p role="alert">{t.unreadableLinks}: {data.unreadableLinkKinds.map(k => label(t, 'link_' + k)).join(', ')}</p>}
    {p.state === 'DRAFT' && <><h3>{t.planBlockers}</h3><Codes items={data.planBlockers} empty={t.noBlockers}/></>}
    {p.state === 'IN_PROGRESS' && <><h3>{t.completeBlockers}</h3><Codes items={data.completeBlockers} empty={t.noBlockers}/></>}
    <form aria-label={t.screenDrydock} onSubmit={e => { e.preventDefault(); if (valid) void run(fieldOpsApi.saveDrydock(vessel, p.id, p, form)); }}>
      <Field id="dd-project" text={t.projectReference} value={form.projectReference} onChange={v => set({ projectReference: v })} disabled={edit}/>
      <Field id="dd-yard" text={t.yard} value={form.yard} onChange={v => set({ yard: v })} disabled={edit}/>
      <label htmlFor="dd-start">{t.plannedStart}</label><input id="dd-start" type="date" disabled={edit} value={form.plannedStart ?? ''} onChange={e => set({ plannedStart: e.target.value || null })}/>
      <label htmlFor="dd-finish">{t.plannedFinish}</label><input id="dd-finish" type="date" disabled={edit} value={form.plannedFinish ?? ''} onChange={e => set({ plannedFinish: e.target.value || null })}/>
      <fieldset><legend>{t.external}</legend><p>{t.externalBoundary}</p>
        <Field id="dd-cost" text={t.estimatedCost} value={form.external.estimatedCost} onChange={v => set({ external: { ...form.external, estimatedCost: v } })} disabled={edit} max={15}/>
        <Field id="dd-currency" text={t.currency} value={form.external.currency} onChange={v => set({ external: { ...form.external, currency: v?.toUpperCase() ?? null } })} disabled={edit} max={3}/>
        <Field id="dd-vendor" text={t.vendor} value={form.external.vendor} onChange={v => set({ external: { ...form.external, vendor: v } })} disabled={edit}/>
        <Field id="dd-budget" text={t.budgetReference} value={form.external.budgetReference} onChange={v => set({ external: { ...form.external, budgetReference: v } })} disabled={edit}/>
      </fieldset>
      <fieldset><legend>{t.scopeItems}</legend>
        {form.items.map((i, n) => { const s = status(i.id);
          return <div key={i.id} data-item-state={s?.state ?? 'UNSAVED'} className="field-ops-scope-item">
            <p>{s ? <StateBadge state={s.state} t={t}/> : <Badge tone="warning">{t.unsaved}</Badge>}</p>
            <label htmlFor={`dd-cat-${n}`}>{t.category}</label><select id={`dd-cat-${n}`} disabled={edit} value={i.category} onChange={e => upItem(i.id, { category: e.target.value as ScopeItem['category'] })}>
              {categories.map(c => <option key={c} value={c}>{label(t, 'cat_' + c)}</option>)}</select>
            <Field id={`dd-title-${n}`} text={t.itemTitle} value={i.title} onChange={v => upItem(i.id, { title: v ?? '' })} disabled={edit}/>
            <Field id={`dd-wp-${n}`} text={t.workPackage} value={i.workPackage} onChange={v => upItem(i.id, { workPackage: v })} disabled={edit}/>
            <Field id={`dd-class-${n}`} text={t.classRequirement} value={i.classRequirement} onChange={v => upItem(i.id, { classRequirement: v })} disabled={edit} max={1000}/>
            <Field id={`dd-stat-${n}`} text={t.statutoryRequirement} value={i.statutoryRequirement} onChange={v => upItem(i.id, { statutoryRequirement: v })} disabled={edit} max={1000}/>
            <label htmlFor={`dd-prio-${n}`}>{t.priority}</label><select id={`dd-prio-${n}`} disabled={edit} value={i.priority ?? ''} onChange={e => upItem(i.id, { priority: (e.target.value || null) as ScopeItem['priority'] })}>
              <option value="">{t.unknownOption}</option>{priorities.map(c => <option key={c} value={c}>{label(t, 'prio_' + c)}</option>)}</select>
            <fieldset><legend>{t.links}</legend>
              {i.links.map((l, k) => { const ls = s?.links.find(x => x.kind === l.kind && x.targetId === l.targetId);
                const upLink = (patch: Partial<typeof l>) => upItem(i.id, { links: i.links.map((y, j) => j === k ? { ...y, ...patch } : y) });
                return <div key={k} data-link-state={ls?.state ?? 'UNSAVED'}>
                  <label htmlFor={`dd-lk-${n}-${k}`}>{t.linkKind}</label><select id={`dd-lk-${n}-${k}`} disabled={edit} value={l.kind} onChange={e => upLink({ kind: e.target.value as typeof l.kind })}>
                    {linkKinds.map(c => <option key={c} value={c}>{label(t, 'link_' + c)}</option>)}</select>
                  <Field id={`dd-lt-${n}-${k}`} text={t.targetId} value={l.targetId} onChange={v => upLink({ targetId: v ?? '' })} disabled={edit} max={36}/>
                  <label htmlFor={`dd-lr-${n}-${k}`}>{t.pinnedRevision}</label><input id={`dd-lr-${n}-${k}`} type="number" min={1} disabled={edit} value={l.pinnedRevision}
                    onChange={e => upLink({ pinnedRevision: Number(e.target.value) })}/>
                  {ls && <> <StateBadge state={ls.state} t={t}/> <code>{ls.reason}</code>{ls.state === 'STALE' && ls.currentRevision !== null && !edit &&
                    <Button type="button" variant="outline" onClick={() => upLink({ pinnedRevision: ls.currentRevision! })}>{t.repin} r{ls.currentRevision}</Button>}</>}
                  <Button type="button" variant="outline" disabled={edit} onClick={() => upItem(i.id, { links: i.links.filter((_, j) => j !== k) })}>{t.remove}</Button></div>; })}
              <Button type="button" variant="outline" disabled={edit || i.links.length >= 20} onClick={() => upItem(i.id, { links: [...i.links, { kind: 'WORK_ORDER', targetId: '', pinnedRevision: 1 }] })}>{t.addLink}</Button>
            </fieldset>
            <Button type="button" variant="outline" disabled={edit} onClick={() => set({ items: form.items.filter(y => y.id !== i.id) })}>{t.remove}</Button></div>; })}
        <Button type="button" variant="outline" disabled={edit || form.items.length >= 200} onClick={() => set({ items: [...form.items, { id: crypto.randomUUID(), category: 'OTHER', title: '', equipmentId: null,
          workPackage: null, classRequirement: null, statutoryRequirement: null, priority: null, links: [] }] })}>{t.addItem}</Button>
      </fieldset>
      <fieldset><legend>{t.completionEvidence}</legend>
        {form.completionEvidence.map((x, n) => <div key={x.id}><Field id={`dd-ev-${n}`} text={t.evidenceRef} value={x.reference} max={400} disabled={edit}
          onChange={v => set({ completionEvidence: form.completionEvidence.map(y => y.id === x.id ? { ...y, reference: v ?? '' } : y) })}/>
          <Button type="button" variant="outline" disabled={edit} onClick={() => set({ completionEvidence: form.completionEvidence.filter(y => y.id !== x.id) })}>{t.remove}</Button></div>)}
        <Button type="button" variant="outline" disabled={edit || form.completionEvidence.length >= 50} onClick={() => set({ completionEvidence: [...form.completionEvidence, { id: crypto.randomUUID(), reference: '', note: null }] })}>{t.add}</Button>
      </fieldset>
      {data.canEdit && <EnterpriseActions layout="form" aria-label={t.save}><Button type="submit" disabled={edit || !valid}>{t.save}</Button></EnterpriseActions>}
    </form>
    {data.allowedActions.length > 0 && <form aria-label={t.transition} onSubmit={e => { e.preventDefault(); if (action && (action !== 'CANCEL' || note)) void run(fieldOpsApi.transitionDrydock(vessel, p, action, note)); }}>
      <label htmlFor="dd-action">{t.action}</label><select id="dd-action" disabled={off} value={action} onChange={e => setAction(e.target.value)}>
        {data.allowedActions.map(a => <option key={a} value={a}>{label(t, 'ddaction_' + a)}</option>)}</select>
      <Field id="dd-note" text={t.note} long value={note} onChange={setNote} disabled={off} max={1000}/>
      <EnterpriseActions layout="form" aria-label={t.transition}><Button type="submit" disabled={off || !action || action === 'CANCEL' && !note}>{label(t, 'ddaction_' + action)}</Button></EnterpriseActions></form>}
    {data.canVerify && <ReasonForm id="dd-verify" text={t.verify} t={t} disabled={off} onSubmit={x => void run(fieldOpsApi.verifyDrydock(vessel, p, 'VERIFY', x))}/>}
    {data.canVerify && <ReasonForm id="dd-reject" text={t.rejectVerification} t={t} disabled={off} onSubmit={x => void run(fieldOpsApi.verifyDrydock(vessel, p, 'REJECT', x))}/>}
    <h2>{t.timeline}</h2>
    <ol className="field-ops-timeline">
      <li>{t.plannedWindow}: {p.content.plannedStart ?? '—'} → {p.content.plannedFinish ?? '—'}</li>
      {p.actualStart && <li>{t.actualStart}: <time dateTime={p.actualStart}>{p.actualStart}</time></li>}
      {p.actualFinish && <li>{t.actualFinish}: <time dateTime={p.actualFinish}>{p.actualFinish}</time></li>}
      {data.timeline.map(e => <li key={e.revision} data-reason={e.reasonCode}>r{e.revision} · <code>{e.reasonCode}</code> · {label(t, 'state_' + e.state)}{e.note && <> · {e.note}</>} · <time dateTime={e.savedAt}>{e.savedAt}</time></li>)}
    </ol>
  </section>;
}
