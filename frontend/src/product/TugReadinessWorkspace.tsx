import { useCallback, useEffect, useRef, useState } from 'react';
import { localizedCopy } from '../i18n/messages';
import { useAppLanguage, type Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { EnterpriseActions } from './EnterprisePatterns';
import { ApiError, session, type Session } from './api';
import { isGuid } from './identifiers';
import { tugApi, type TugBoard } from './tug-api';
import { assignmentRoles, send, towConditions, towKinds, tugReadinessApi, type AssignmentList, type Authority, type CheckResult, type Command, type CrewReadiness,
  type ReadinessCheck, type ReadinessState, type Readiness, type TowEquipment, type TowInputs, type TowRow } from './tug-readiness-api';
import './tug.css';

const copy = localizedCopy('TugReadiness_tsx_copy');
type Copy = (typeof copy)[AppLocale];
export const tugReadinessScreens = ['tug-assignment', 'tug-readiness', 'tug-equipment-readiness', 'tug-crew-readiness'] as const;
type Screen = typeof tugReadinessScreens[number];
type Notice = '' | 'done' | 'uncertain' | 'conflict' | 'stale' | 'goBlocked' | 'denied' | 'failed';

export function tugReadinessRoute(href: string) {
  const url = new URL(href, 'http://local'), params = url.searchParams;
  const match = /^\/app\/(tug-assignment|tug-readiness|tug-equipment-readiness|tug-crew-readiness)\/?$/.exec(url.pathname);
  const value = (k: string) => params.get(k) ?? '';
  const invalid = !match || [...params.keys()].some(k => !['vessel', 'job', 'assignment'].includes(k) || params.getAll(k).length !== 1) ||
    ['vessel', 'job', 'assignment'].some(k => value(k) !== '' && !isGuid(value(k)));
  return { screen: (match?.[1] ?? 'tug-assignment') as Screen, vessel: value('vessel'), job: value('job'), assignment: value('assignment'), invalid };
}
export const tugReadinessHref = (screen: Screen, vessel: string, extra: { job?: string; assignment?: string } = {}) =>
  `/app/${screen}?vessel=${vessel}${extra.job ? `&job=${extra.job}` : ''}${extra.assignment ? `&assignment=${extra.assignment}` : ''}`;

// UNKNOWN/REVIEW_REQUIRED is never shown as ready; READY is informational only (no green "go" tone).
export function StateBadge({ state, t }: { state: ReadinessState; t: Copy }) {
  return <Badge tone={state === 'NOT_READY' ? 'danger' : state === 'READY' ? 'info' : 'warning'} data-readiness={state}>{t[state]}</Badge>;
}
function ResultBadge({ result, t }: { result: CheckResult; t: Copy }) {
  return <Badge tone={result === 'FAIL' ? 'danger' : result === 'PASS' ? 'neutral' : 'warning'} data-result={result}>{t[result]}</Badge>;
}
const categoryLabel = (t: Copy, c: string) => (t as Record<string, string>)['cat_' + c] ?? c;

function Checks({ checks, t }: { checks: ReadinessCheck[]; t: Copy }) {
  if (checks.length === 0) return <p>{t.unknownValue}</p>;
  return <table className="tug-readiness-checks"><thead><tr><th scope="col">{t.category}</th><th scope="col">{t.result}</th><th scope="col">{t.reasonCode}</th></tr></thead>
    <tbody>{checks.map((c, i) => <tr key={`${c.category}-${c.reason}-${i}`}><td>{categoryLabel(t, c.category)}</td><td><ResultBadge result={c.result} t={t}/></td><td><code>{c.reason}</code></td></tr>)}</tbody></table>;
}
function AuthorityPicker({ id, authorities, value, onChange, t, disabled }: { id: string; authorities: Authority[]; value: string; onChange: (v: string) => void; t: Copy; disabled: boolean }) {
  return <><label htmlFor={id}>{t.authority}</label>
    <select id={id} required value={value} disabled={disabled} onChange={e => onChange(e.target.value)}>
      <option value="">—</option>{authorities.map(a => <option key={a.id} value={a.id}>{(t as Record<string, string>)['duty_' + a.duty] ?? a.duty}</option>)}</select></>;
}

export function TugReadinessWorkspace() {
  const [lang, language] = useAppLanguage(), t = copy[lang];
  const [route, setRoute] = useState(() => tugReadinessRoute(window.location.href));
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true), [error, setError] = useState(0), [offline, setOffline] = useState(!navigator.onLine);
  const [board, setBoard] = useState<TugBoard | null>(null), [assignments, setAssignments] = useState<AssignmentList | null>(null);
  const [readiness, setReadiness] = useState<Readiness | null>(null), [crew, setCrew] = useState<CrewReadiness | null>(null), [equipment, setEquipment] = useState<TowEquipment | null>(null);
  const [notice, setNotice] = useState<Notice>(''), [busy, setBusy] = useState(false), [retained, setRetained] = useState<Command | null>(null);
  const lock = useRef(false), mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true), pop = () => { setRoute(tugReadinessRoute(window.location.href)); setReload(n => n + 1); };
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop);
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop); };
  }, []);
  const move = (href: string) => { if (retained || busy) return; history.pushState(null, '', href); setRoute(tugReadinessRoute(window.location.href)); setNotice(''); setReload(n => n + 1); };
  useEffect(() => {
    const c = new AbortController();
    (async () => {
      await Promise.resolve();
      if (c.signal.aborted) return;
      setLoading(true); setError(0); setBoard(null); setAssignments(null); setReadiness(null); setCrew(null); setEquipment(null);
      if (route.invalid) throw new ApiError(400, 'invalid_route');
      const s = await session(c.signal); setUser(s);
      if (!route.vessel) return;
      if (route.screen === 'tug-assignment') {
        if (route.job) setAssignments(await tugReadinessApi.assignments(route.vessel, route.job, c.signal));
        else setBoard(await tugApi.board(route.vessel, '', '', 0, c.signal, 'Submitted'));
      } else if (route.screen === 'tug-readiness' && route.assignment) setReadiness(await tugReadinessApi.readiness(route.vessel, route.assignment, c.signal));
      else if (route.screen === 'tug-crew-readiness' && route.assignment) setCrew(await tugReadinessApi.crew(route.vessel, route.assignment, c.signal));
      else if (route.screen === 'tug-equipment-readiness') setEquipment(await tugReadinessApi.equipment(route.vessel, c.signal));
    })().then(() => { if (!c.signal.aborted) setLoading(false); }).catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e.status : 500); setLoading(false); });
    return () => c.abort();
  }, [route, reload]);

  // One command at a time; an uncertain outcome keeps the exact key/body for an identical retry.
  const run = useCallback(async (command: Command): Promise<boolean> => {
    if (lock.current || offline || !navigator.onLine) return false;
    lock.current = true; setBusy(true); setNotice(''); setRetained(command);
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000);
    let sent = false;
    try {
      const s = await session(c.signal);
      if (!s.authenticated || !s.csrfToken || user && `${s.tenantId}/${s.actorId}` !== `${user.tenantId}/${user.actorId}`) throw new ApiError(403, 'identity_boundary_changed');
      sent = true;
      await send(command, s.csrfToken, c.signal);
      if (!mounted.current) return false;
      setRetained(null); setNotice('done'); setReload(n => n + 1); return true;
    } catch (e) {
      if (!mounted.current) return false;
      const definite = e instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(e.status);
      if (!sent || definite) setRetained(null);
      setNotice(sent && !definite ? 'uncertain' : e instanceof ApiError && e.code === 'assessment_stale' ? 'stale' : e instanceof ApiError && e.code === 'tug_go_not_permitted' ? 'goBlocked'
        : e instanceof ApiError && e.status === 409 ? 'conflict' : e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : 'failed');
      return false;
    } finally { clearTimeout(timeout); lock.current = false; if (mounted.current) setBusy(false); }
  }, [offline, user]);

  const disabled = busy || offline || !!retained && notice !== 'uncertain';
  const titles: Record<Screen, string> = { 'tug-assignment': t.screenAssignment, 'tug-readiness': t.screenReadiness, 'tug-equipment-readiness': t.screenEquipment, 'tug-crew-readiness': t.screenCrew };
  const message = notice ? (t as Record<string, string>)['notice_' + notice] : '';
  return <div className="product-shell enterprise-shell tug-workspace">
    <EnterpriseChrome session={user} vessel={route.vessel || null} language={lang} onLanguageChange={language} onNavigate={href => { if (!retained && !busy) window.location.assign(href); }} title={titles[route.screen]} mainId="tug-readiness-main"/>
    <main id="tug-readiness-main" tabIndex={-1} aria-busy={loading || busy}>
      <div className="enterprise-page-heading"><div><p className="enterprise-page-eyebrow">{t.title}</p><h1>{titles[route.screen]}</h1></div></div>
      <p className="tug-planning-boundary">{t.boundary}</p>
      {offline && <p role="alert">{t.offline}</p>}
      {route.vessel && <nav aria-label={t.title}>{tugReadinessScreens.map(s => <a key={s} aria-current={route.screen === s ? 'page' : undefined}
        href={tugReadinessHref(s, route.vessel, { job: s === 'tug-assignment' ? route.job : undefined, assignment: s === 'tug-readiness' || s === 'tug-crew-readiness' ? route.assignment : undefined })}
        onClick={e => { e.preventDefault(); move(tugReadinessHref(s, route.vessel, { job: s === 'tug-assignment' ? route.job : undefined, assignment: s === 'tug-readiness' || s === 'tug-crew-readiness' ? route.assignment : undefined })); }}>{titles[s]} </a>)}</nav>}
      {message && <div role={notice === 'done' ? 'status' : 'alert'}><p>{message}</p>
        {notice === 'uncertain' && retained && <Button type="button" disabled={busy || offline} onClick={() => void run(retained)}>{t.retry}</Button>}
        {['conflict', 'stale'].includes(notice) && <Button type="button" variant="outline" onClick={() => { setNotice(''); setReload(n => n + 1); }}>{t.reload}</Button>}</div>}
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error > 0 && <div role="alert"><p>{[400, 401, 403, 404].includes(error) ? t.denied : t.unavailable}</p><Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>}
      {!loading && !error && !route.vessel && <p role="status">{t.selectVessel} <a href="/app/tug-command">{t.openTugCommand}</a></p>}
      {!loading && !error && route.vessel && (route.screen === 'tug-readiness' || route.screen === 'tug-crew-readiness') && !route.assignment &&
        <p role="status">{t.selectAssignment} <a href={tugReadinessHref('tug-assignment', route.vessel)} onClick={e => { e.preventDefault(); move(tugReadinessHref('tug-assignment', route.vessel)); }}>{t.screenAssignment}</a></p>}
      {!loading && !error && board && <section aria-label={t.selectJob}><h2>{t.selectJob}</h2>{board.items.length === 0 ? <p>{t.noJobs}</p> :
        <ul>{board.items.map(row => <li key={row.request.id}><a href={tugReadinessHref('tug-assignment', route.vessel, { job: row.request.id })}
          onClick={e => { e.preventDefault(); move(tugReadinessHref('tug-assignment', route.vessel, { job: row.request.id })); }}>{row.request.jobNumber}</a></li>)}</ul>}</section>}
      {!loading && !error && assignments && <AssignmentPanel data={assignments} vessel={route.vessel} t={t} disabled={disabled} run={run} move={move}/>}
      {!loading && !error && readiness && <ReadinessPanel data={readiness} vessel={route.vessel} t={t} disabled={disabled} run={run}/>}
      {!loading && !error && crew && <section aria-label={t.screenCrew}><p>{t.state}: <StateBadge state={crew.state} t={t}/></p>
        <p>{t.crewEvaluator}: {crew.evaluatorState === 'Observed' ? crew.evaluatorSource : t.evaluatorUnavailable}</p><Checks checks={crew.checks} t={t}/></section>}
      {!loading && !error && equipment && <EquipmentPanel data={equipment} tug={route.vessel} t={t} disabled={disabled} run={run}/>}
    </main></div>;
}

type PanelProps = { vessel: string; t: Copy; disabled: boolean; run: (c: Command) => Promise<boolean> };

function AssignmentPanel({ data, vessel, t, disabled, run, move }: PanelProps & { data: AssignmentList; move: (href: string) => void }) {
  const [tug, setTug] = useState(''), [start, setStart] = useState(''), [end, setEnd] = useState(''), [role, setRole] = useState('');
  const iso = (local: string) => { const d = new Date(local); return Number.isNaN(d.getTime()) ? '' : d.toISOString().replace(/\.\d{3}Z$/, 'Z'); };
  return <section aria-label={t.screenAssignment}>
    {data.items.length === 0 && <p>{t.empty}</p>}
    {data.items.map(item => <AssignmentItemView key={item.assignment.id} item={item} vessel={vessel} t={t} disabled={disabled} run={run} move={move}/>)}
    {data.canPropose && <form className="tug-assignment-propose" onSubmit={e => { e.preventDefault();
      const s = iso(start), f = iso(end); if (!tug || !role || !s || !f) return;
      void run(tugReadinessApi.propose(vessel, { clientId: crypto.randomUUID(), jobId: data.jobId, expectedJobVersion: data.jobVersion, tugId: tug, plannedStart: s, plannedEnd: f, role })); }}>
      <h2>{t.propose}</h2>
      <label htmlFor="tug-assignment-tug">{t.tug}</label><select id="tug-assignment-tug" required value={tug} onChange={e => setTug(e.target.value)}><option value="">—</option>{data.tugs.map(x => <option key={x.id} value={x.id}>{x.code}</option>)}</select>
      <label htmlFor="tug-assignment-start">{t.start}</label><input id="tug-assignment-start" type="datetime-local" required value={start} onChange={e => setStart(e.target.value)}/>
      <label htmlFor="tug-assignment-end">{t.end}</label><input id="tug-assignment-end" type="datetime-local" required value={end} onChange={e => setEnd(e.target.value)}/>
      <label htmlFor="tug-assignment-role">{t.role}</label><select id="tug-assignment-role" required value={role} onChange={e => setRole(e.target.value)}><option value="">—</option>{assignmentRoles.map(r => <option key={r} value={r}>{t['role_' + r as keyof Copy]}</option>)}</select>
      <EnterpriseActions layout="form" aria-label={t.propose}><Button type="submit" disabled={disabled || !tug || !role || !start || !end}>{t.propose}</Button></EnterpriseActions>
    </form>}
  </section>;
}

function AssignmentItemView({ item, vessel, t, disabled, run, move }: PanelProps & { item: AssignmentList['items'][number]; move: (href: string) => void }) {
  const a = item.assignment;
  const [authority, setAuthority] = useState(item.authorities.length === 1 ? item.authorities[0].id : ''), [reason, setReason] = useState(''), [withdrawReason, setWithdrawReason] = useState('');
  return <article className="tug-assignment-item" aria-label={`${t.tug} ${a.tugId}`}>
    <p>{t.state}: <Badge tone={a.state === 'Cancelled' ? 'warning' : 'neutral'}>{t['state_' + a.state as keyof Copy]}</Badge> · {t.masterAck}: <Badge tone={a.masterAck === 'Rejected' ? 'danger' : a.masterAck === 'Accepted' ? 'info' : 'warning'} data-ack={a.masterAck}>{t['ack_' + a.masterAck as keyof Copy]}</Badge></p>
    <p>{t.role}: {t['role_' + a.role as keyof Copy]} · {t.start}: <time dateTime={a.plannedStart}>{a.plannedStart}</time> · {t.end}: <time dateTime={a.plannedEnd}>{a.plannedEnd}</time></p>
    {item.requestChanged && <p role="alert">{t.requestChanged}</p>}
    <h3>{t.conflicts}</h3>
    {item.conflicts.length === 0 ? <p>{t.noConflicts}</p> : <ul>{item.conflicts.map((c, i) => <li key={`${c.reason}-${i}`}>{c.kind}: <ResultBadge result={c.result} t={t}/> <code>{c.reason}</code></li>)}</ul>}
    {item.response && <p>{t.masterAck}: {t['ack_' + (item.response.decision === 'ACCEPT' ? 'Accepted' : 'Rejected') as keyof Copy]} · {item.response.reason ?? '—'} · <time dateTime={item.response.recordedAt}>{item.response.recordedAt}</time></p>}
    <p><a href={tugReadinessHref('tug-readiness', vessel, { assignment: a.id })} onClick={e => { e.preventDefault(); move(tugReadinessHref('tug-readiness', vessel, { assignment: a.id })); }}>{t.screenReadiness}</a>{' · '}
      <a href={tugReadinessHref('tug-crew-readiness', vessel, { assignment: a.id })} onClick={e => { e.preventDefault(); move(tugReadinessHref('tug-crew-readiness', vessel, { assignment: a.id })); }}>{t.screenCrew}</a></p>
    {item.canRespond && <form onSubmit={e => e.preventDefault()} aria-label={t.masterAck}>
      <AuthorityPicker id={`ack-authority-${a.id}`} authorities={item.authorities} value={authority} onChange={setAuthority} t={t} disabled={disabled}/>
      <label htmlFor={`ack-reason-${a.id}`}>{t.reason}</label><textarea id={`ack-reason-${a.id}`} maxLength={2000} value={reason} onChange={e => setReason(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.masterAck}>
        <Button type="button" variant="outline" disabled={disabled || !authority || !reason.trim()} onClick={() => void run(tugReadinessApi.respond(vessel, a, 'REJECT', authority, reason.trim()))}>{t.reject}</Button>
        <Button type="button" disabled={disabled || !authority} onClick={() => void run(tugReadinessApi.respond(vessel, a, 'ACCEPT', authority, reason.trim() || null))}>{t.accept}</Button>
      </EnterpriseActions></form>}
    {item.canWithdraw && <form onSubmit={e => { e.preventDefault(); if (withdrawReason.trim()) void run(tugReadinessApi.withdraw(vessel, a, withdrawReason.trim())); }} aria-label={t.withdraw}>
      <label htmlFor={`withdraw-reason-${a.id}`}>{t.reason}</label><textarea id={`withdraw-reason-${a.id}`} required maxLength={2000} value={withdrawReason} onChange={e => setWithdrawReason(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.withdraw}><Button type="submit" variant="destructive" disabled={disabled || !withdrawReason.trim()}>{t.withdraw}</Button></EnterpriseActions></form>}
  </article>;
}

function ReadinessPanel({ data, vessel, t, disabled, run }: PanelProps & { data: Readiness }) {
  const [decision, setDecision] = useState<'GO' | 'NO_GO' | ''>(''), [reason, setReason] = useState('');
  const [authority, setAuthority] = useState(data.authorities.length === 1 ? data.authorities[0].id : '');
  const goAllowed = data.state === 'READY' || data.state === 'CONDITIONAL';
  return <section aria-label={t.screenReadiness}>
    <p>{t.assessment}: <StateBadge state={data.state} t={t}/> <small>{t.advisory}</small></p>
    <dl className="tug-readiness-bp"><dt>{t.bpRequired}</dt><dd>{data.requiredBollardPull ?? t.unknownValue}</dd><dt>{t.bpAvailable}</dt><dd>{data.availableBollardPull ?? t.unknownValue}</dd>
      <dt>{t.bpMargin}</dt><dd>{data.bollardPullMargin ?? t.unknownValue}</dd></dl>
    {data.criticalBlockers.length > 0 && <div role="alert"><h2>{t.criticalBlockers}</h2><ul>{data.criticalBlockers.map(x => <li key={x}><code>{x}</code></li>)}</ul></div>}
    {data.reviewReasons.length > 0 && <><h2>{t.reviewReasons}</h2><ul>{data.reviewReasons.map(x => <li key={x}><code>{x}</code></li>)}</ul></>}
    {data.conditions.length > 0 && <><h2>{t.conditions}</h2><ul>{data.conditions.map(x => <li key={x}><code>{x}</code></li>)}</ul></>}
    <table className="tug-readiness-categories"><thead><tr><th scope="col">{t.category}</th><th scope="col">{t.result}</th></tr></thead>
      <tbody>{data.categories.map(c => <tr key={c.category}><td>{categoryLabel(t, c.category)}</td><td><ResultBadge result={c.result} t={t}/></td></tr>)}</tbody></table>
    <h2>{t.decisions}</h2>
    {data.decisions.length === 0 ? <p>{t.noDecisions}</p> : <ul>{data.decisions.map(d => <li key={d.id} data-decision={d.decision}>{d.decision === 'GO' ? t.go : t.noGo} · {t[d.assessedState]} · {d.reason} · <time dateTime={d.recordedAt}>{d.recordedAt}</time></li>)}</ul>}
    {data.canDecide && <form onSubmit={e => { e.preventDefault(); if (decision && authority && reason.trim()) void run(tugReadinessApi.decide(vessel, data, decision, authority, reason.trim())); }} aria-label={t.decide}>
      <h2>{t.decide}</h2>
      <fieldset><legend>{t.decide}</legend>
        <label><input type="radio" name="tug-decision" value="NO_GO" checked={decision === 'NO_GO'} onChange={() => setDecision('NO_GO')}/>{t.noGo}</label>
        <label><input type="radio" name="tug-decision" value="GO" checked={decision === 'GO'} disabled={!goAllowed} aria-describedby="tug-go-hint" onChange={() => setDecision('GO')}/>{t.go}</label>
      </fieldset>
      {!goAllowed && <p id="tug-go-hint">{t.notice_goBlocked}</p>}
      <AuthorityPicker id="decision-authority" authorities={data.authorities} value={authority} onChange={setAuthority} t={t} disabled={disabled}/>
      <label htmlFor="decision-reason">{t.reason}</label><textarea id="decision-reason" required maxLength={2000} value={reason} onChange={e => setReason(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.decide}><Button type="submit" disabled={disabled || !decision || !authority || !reason.trim() || decision === 'GO' && !goAllowed}>{t.record}</Button></EnterpriseActions>
    </form>}
    <h2>{t.checks}</h2><Checks checks={data.checks} t={t}/>
  </section>;
}

const emptyTow = (): TowInputs => ({ kind: '', label: null, swlTonnes: null, lastTestDate: null, nextTestDue: null, condition: 'Unknown', critical: null, assetId: null, certificateId: null, notes: null });
function EquipmentPanel({ data, tug, t, disabled, run }: Omit<PanelProps, 'vessel'> & { data: TowEquipment; tug: string }) {
  const [editing, setEditing] = useState<TowRow | 'new' | null>(null), [form, setForm] = useState<TowInputs>(emptyTow);
  const [authority, setAuthority] = useState(data.authorities.length === 1 ? data.authorities[0].id : ''), [verifyReason, setVerifyReason] = useState('');
  const set = (patch: Partial<TowInputs>) => setForm(f => ({ ...f, ...patch }));
  const blank = (v: string) => v.trim() === '' ? null : v.trim();
  const valid = form.kind !== '' && (form.assetId === null || isGuid(form.assetId)) && (form.certificateId === null || isGuid(form.certificateId)) && (form.swlTonnes === null || /^\d{1,6}(?:\.\d{1,2})?$/.test(form.swlTonnes));
  return <section aria-label={t.screenEquipment}>
    <p>{t.assessment}: <StateBadge state={data.assessment.state} t={t}/> <small>{t.advisory}</small></p>
    {data.assessment.items.length === 0 && <p>{t.empty}</p>}
    {data.assessment.items.map(row => <article key={row.id} className="tug-tow-item" aria-label={`${t['kind_' + row.inputs.kind as keyof Copy]} ${row.inputs.label ?? ''}`}>
      <h3>{t['kind_' + row.inputs.kind as keyof Copy]} {row.inputs.label}</h3>
      <p><ResultBadge result={row.check.result} t={t}/> <code>{row.check.reason}</code></p>
      <dl><dt>{t.swl}</dt><dd>{row.inputs.swlTonnes ?? t.unknownValue}</dd><dt>{t.nextTest}</dt><dd>{row.inputs.nextTestDue ?? t.unknownValue}</dd>
        <dt>{t.condition}</dt><dd>{t['cond_' + row.inputs.condition as keyof Copy]}</dd><dt>{t.critical}</dt><dd>{row.inputs.critical === null ? t.criticalUnknown : row.inputs.critical ? t.yes : t.no}</dd></dl>
      <h4>{t.verifications}</h4>{row.verifications.length === 0 ? <p>{t.noVerification}</p> :
        <ul>{row.verifications.map(v => <li key={v.id}>{v.outcome === 'VERIFIED' ? t.verified : t.notVerified} · r{v.itemRevision} · <time dateTime={v.recordedAt}>{v.recordedAt}</time>{v.itemRevision !== row.revision && ` · ${t.superseded}`}</li>)}</ul>}
      {data.canWrite && <Button type="button" variant="outline" disabled={disabled} onClick={() => { setEditing(row); setForm(row.inputs); }}>{t.edit}</Button>}
      {data.canVerify && <form onSubmit={e => e.preventDefault()} aria-label={t.verify}>
        <AuthorityPicker id={`verify-authority-${row.id}`} authorities={data.authorities} value={authority} onChange={setAuthority} t={t} disabled={disabled}/>
        <label htmlFor={`verify-reason-${row.id}`}>{t.reason}</label><textarea id={`verify-reason-${row.id}`} maxLength={2000} value={verifyReason} onChange={e => setVerifyReason(e.target.value)}/>
        <EnterpriseActions layout="form" aria-label={t.verify}>
          <Button type="button" variant="outline" disabled={disabled || !authority || !verifyReason.trim()} onClick={() => void run(tugReadinessApi.verifyTow(tug, row, 'NOT_VERIFIED', authority, verifyReason.trim()))}>{t.notVerified}</Button>
          <Button type="button" disabled={disabled || !authority} onClick={() => void run(tugReadinessApi.verifyTow(tug, row, 'VERIFIED', authority, verifyReason.trim() || null))}>{t.verified}</Button>
        </EnterpriseActions></form>}
    </article>)}
    {data.canWrite && !editing && <Button type="button" disabled={disabled} onClick={() => { setEditing('new'); setForm(emptyTow()); }}>{t.addItem}</Button>}
    {data.canWrite && editing && <form className="tug-tow-form" aria-label={t.save} onSubmit={e => { e.preventDefault(); if (!valid) return;
      const id = editing === 'new' ? crypto.randomUUID() : editing.id;
      void run(tugReadinessApi.saveTow(tug, id, editing === 'new' ? null : editing.version, form)).then(ok => { if (ok) setEditing(null); }); }}>
      <label htmlFor="tow-kind">{t.kind}</label><select id="tow-kind" required disabled={editing !== 'new'} value={form.kind} onChange={e => set({ kind: e.target.value })}><option value="">—</option>{towKinds.map(k => <option key={k} value={k}>{t['kind_' + k as keyof Copy]}</option>)}</select>
      <label htmlFor="tow-label">{t.label}</label><input id="tow-label" maxLength={200} value={form.label ?? ''} onChange={e => set({ label: blank(e.target.value) })}/>
      <label htmlFor="tow-swl">{t.swl}</label><input id="tow-swl" inputMode="decimal" value={form.swlTonnes ?? ''} onChange={e => set({ swlTonnes: blank(e.target.value) })}/>
      <label htmlFor="tow-last">{t.lastTest}</label><input id="tow-last" type="date" value={form.lastTestDate ?? ''} onChange={e => set({ lastTestDate: blank(e.target.value) })}/>
      <label htmlFor="tow-next">{t.nextTest}</label><input id="tow-next" type="date" value={form.nextTestDue ?? ''} onChange={e => set({ nextTestDue: blank(e.target.value) })}/>
      <label htmlFor="tow-condition">{t.condition}</label><select id="tow-condition" value={form.condition} onChange={e => set({ condition: e.target.value })}>{towConditions.map(c => <option key={c} value={c}>{t['cond_' + c as keyof Copy]}</option>)}</select>
      <label htmlFor="tow-critical">{t.critical}</label><select id="tow-critical" value={form.critical === null ? '' : String(form.critical)} onChange={e => set({ critical: e.target.value === '' ? null : e.target.value === 'true' })}>
        <option value="">{t.criticalUnknown}</option><option value="true">{t.yes}</option><option value="false">{t.no}</option></select>
      <label htmlFor="tow-asset">{t.assetId}</label><input id="tow-asset" value={form.assetId ?? ''} onChange={e => set({ assetId: blank(e.target.value) })}/>
      <label htmlFor="tow-certificate">{t.certificateId}</label><input id="tow-certificate" value={form.certificateId ?? ''} onChange={e => set({ certificateId: blank(e.target.value) })}/>
      <label htmlFor="tow-notes">{t.notes}</label><textarea id="tow-notes" maxLength={2000} value={form.notes ?? ''} onChange={e => set({ notes: blank(e.target.value) })}/>
      <p>{t.declarationHint}</p>
      <EnterpriseActions layout="form" aria-label={t.save}><Button type="button" variant="outline" disabled={disabled} onClick={() => setEditing(null)}>{t.cancel}</Button><Button type="submit" disabled={disabled || !valid}>{t.save}</Button></EnterpriseActions>
    </form>}
    <h2>{t.checks}</h2><Checks checks={data.assessment.checks} t={t}/>
  </section>;
}
