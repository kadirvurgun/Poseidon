import { useCallback, useEffect, useRef, useState } from 'react';
import { localizedCopy } from '../i18n/messages';
import { useAppLanguage, type Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { EnterpriseActions } from './EnterprisePatterns';
import { ApiError, session, type Session } from './api';
import { isGuid } from './identifiers';
import { historyOutcomes, loadCapability, loadHistory, queuedState, TugOperationsOfflineQueue, type CapturePayload, type HistoryFilter, type HistoryItem,
  type HistoryOutcome, type OfflineCapability, type QueuedCapture, type TugSnapshot } from './tug-operations-api';
import './tug.css';

// S10 WP-B screens: DEV-091 tug-operation-history (13.02 screen 185), DEV-092 tug-operations-offline (Tug Ops offline
// queue) and DEV-119 tug-demo-journey. Server authority only: nothing here starts, releases, verifies or resolves on
// its own; "queued" is never "sent"; unknown/unverified is never shown as a green ready state.
const copy = localizedCopy('TugOperations_tsx_copy');
type Copy = (typeof copy)[AppLocale];
export const tugOperationScreens = ['tug-operation-history', 'tug-operations-offline', 'tug-demo-journey'] as const;
type Screen = typeof tugOperationScreens[number];
const label = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key;

export function tugOperationsRoute(href: string) {
  const url = new URL(href, 'http://local'), params = url.searchParams;
  const match = /^\/app\/(tug-operation-history|tug-operations-offline|tug-demo-journey)\/?$/.exec(url.pathname);
  const value = (k: string) => params.get(k) ?? '';
  const invalid = !match || [...params.keys()].some(k => !['vessel', 'job'].includes(k) || params.getAll(k).length !== 1) || ['vessel', 'job'].some(k => value(k) !== '' && !isGuid(value(k)));
  return { screen: (match?.[1] ?? 'tug-operation-history') as Screen, vessel: value('vessel'), job: value('job'), invalid };
}
export const tugOperationsHref = (screen: Screen, vessel: string, job = '') => `/app/${screen}?vessel=${vessel}${job ? `&job=${job}` : ''}`;

// Recorded/verified states are informational (never a green "safe" tone); open, unknown or refused states warn.
function Tone({ state, t }: { state: string; t: Copy }) {
  const tone = ['COMPLETED', 'VERIFIED_WITH_EVIDENCE', 'Committed', 'ServerRetained'].includes(state) ? 'info'
    : ['ABORTED', 'CANCELLED', 'VERIFICATION_REJECTED', 'MaterialConflict', 'Rejected'].includes(state) ? 'danger' : 'warning';
  return <Badge tone={tone} data-state={state}>{label(t, 'state_' + state)}</Badge>;
}

export function TugOperationsWorkspace() {
  const [lang, language] = useAppLanguage(), t = copy[lang];
  const [route] = useState(() => tugOperationsRoute(window.location.href));
  const [user, setUser] = useState<Session | null>(null), [offline, setOffline] = useState(!navigator.onLine);
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true);
    addEventListener('online', on); addEventListener('offline', off);
    return () => { removeEventListener('online', on); removeEventListener('offline', off); };
  }, []);
  useEffect(() => { const c = new AbortController(); session(c.signal).then(setUser).catch(() => setUser(null)); return () => c.abort(); }, []);
  const titles: Record<Screen, string> = { 'tug-operation-history': t.screenHistory, 'tug-operations-offline': t.screenOffline, 'tug-demo-journey': t.screenDemo };
  return <div className="product-shell enterprise-shell tug-workspace">
    <EnterpriseChrome session={user} vessel={route.vessel || null} language={lang} onLanguageChange={language} onNavigate={href => window.location.assign(href)}
      title={titles[route.screen]} mainId="tug-operations-main"/>
    <main id="tug-operations-main" tabIndex={-1}>
      <div className="enterprise-page-heading"><div><p className="enterprise-page-eyebrow">{t.title}</p><h1>{titles[route.screen]}</h1></div></div>
      <p className="tug-planning-boundary">{label(t, 'boundary_' + route.screen)}</p>
      {offline && <p role="alert">{route.screen === 'tug-operations-offline' ? t.offlineQueue : t.offline}</p>}
      {route.invalid ? <p role="alert">{t.notFound}</p> : !route.vessel ? <p role="status">{t.selectVessel}</p>
        : route.screen === 'tug-operation-history' ? <HistoryPanel vessel={route.vessel} job={route.job} t={t} offline={offline}/>
        : route.screen === 'tug-operations-offline' ? <OfflinePanel vessel={route.vessel} job={route.job} t={t} offline={offline}/>
        : <DemoPanel vessel={route.vessel} job={route.job} t={t}/>}
    </main></div>;
}

// ---- DEV-091 history ----
function HistoryPanel({ vessel, job, t, offline }: { vessel: string; job: string; t: Copy; offline: boolean }) {
  const [filter, setFilter] = useState<HistoryFilter>({ vesselId: vessel, jobId: job || undefined });
  const [draft, setDraft] = useState({ tugId: '', from: '', to: '', outcome: '' as HistoryOutcome | '' });
  const [items, setItems] = useState<HistoryItem[]>([]), [next, setNext] = useState<string | null>(null), [readAt, setReadAt] = useState('');
  const [state, setState] = useState<'loading' | 'ready' | 'error' | 'denied' | 'invalid'>('loading'), [more, setMore] = useState(0);
  const cursor = useRef<string | undefined>(undefined);
  useEffect(() => {
    if (offline) return;
    const c = new AbortController();
    (async () => { await Promise.resolve(); if (c.signal.aborted) return; setState('loading'); return loadHistory({ ...filter, cursor: cursor.current }, c.signal); })().then(page => {
      if (!page) return;
      setItems(previous => cursor.current ? [...previous, ...page.items] : page.items); setNext(page.nextCursor); setReadAt(page.readAt); setState('ready');
    }).catch(e => { if (c.signal.aborted) return; setState(e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : e instanceof ApiError && e.status === 400 ? 'invalid' : 'error'); });
    return () => c.abort();
  }, [filter, more, offline]);
  const apply = () => { cursor.current = undefined; setFilter({ vesselId: vessel, jobId: job || undefined, tugId: draft.tugId || undefined, from: draft.from || undefined, to: draft.to || undefined,
    outcome: draft.outcome }); };
  const validTug = draft.tugId === '' || isGuid(draft.tugId);
  return <section aria-label={t.screenHistory}>
    <form aria-label={t.filters} onSubmit={e => { e.preventDefault(); if (validTug) apply(); }}>
      <label htmlFor="toh-tug">{t.tugId}</label><input id="toh-tug" value={draft.tugId} maxLength={36} aria-invalid={!validTug} onChange={e => setDraft({ ...draft, tugId: e.target.value.trim() })}/>
      <label htmlFor="toh-from">{t.from}</label><input id="toh-from" type="date" value={draft.from} onChange={e => setDraft({ ...draft, from: e.target.value })}/>
      <label htmlFor="toh-to">{t.to}</label><input id="toh-to" type="date" value={draft.to} onChange={e => setDraft({ ...draft, to: e.target.value })}/>
      <label htmlFor="toh-outcome">{t.outcome}</label><select id="toh-outcome" value={draft.outcome} onChange={e => setDraft({ ...draft, outcome: e.target.value as HistoryOutcome | '' })}>
        <option value="">{t.allOutcomes}</option>{historyOutcomes.map(o => <option key={o} value={o}>{label(t, 'state_' + o)}</option>)}</select>
      <EnterpriseActions layout="form" aria-label={t.filters}><Button type="submit" disabled={!validTug || offline}>{t.applyFilters}</Button></EnterpriseActions>
    </form>
    <p>{t.noSilentDeletion}</p>
    {offline && <p role="status">{t.historyOffline}</p>}
    {state === 'loading' && <p role="status">{t.loading}</p>}
    {state === 'denied' && <p role="alert">{t.denied}</p>}
    {state === 'invalid' && <p role="alert">{t.invalidFilter}</p>}
    {state === 'error' && <div role="alert"><p>{t.unavailable}</p><Button type="button" variant="outline" onClick={() => setMore(n => n + 1)}>{t.retry}</Button></div>}
    {state !== 'loading' && state !== 'error' && items.length === 0 && state === 'ready' && <p role="status">{t.empty}</p>}
    {items.length > 0 && <>
      {readAt && <p>{t.readAt}: <time dateTime={readAt}>{readAt}</time></p>}
      <table className="tug-history-table"><caption>{t.screenHistory}</caption>
        <thead><tr><th scope="col">{t.job}</th><th scope="col">{t.when}</th><th scope="col">{t.outcome}</th><th scope="col">{t.evidence}</th><th scope="col">{t.executions}</th><th scope="col">{t.dailyLogs}</th></tr></thead>
        <tbody>{items.map(i => <tr key={i.jobId} data-outcome={i.outcome}>
          <td><strong>{i.jobNumber}</strong>{i.assistedVessel && <> · {i.assistedVessel}</>}{i.jobType && <> · <code>{i.jobType}</code></>}
            {i.cancelledAt && <><br/>{t.cancelledAt}: <time dateTime={i.cancelledAt}>{i.cancelledAt}</time></>}
            <br/>{t.plan}: <code>{i.planState ?? t.none}</code> · {t.briefing}: <code>{i.briefingState ?? t.none}</code></td>
          <td><time dateTime={i.at}>{i.at}</time></td>
          <td><Tone state={i.outcome} t={t}/></td>
          <td><Tone state={i.evidence} t={t}/><br/><small>{t.safetyEvents}: <code>{i.safetyEvents}</code></small></td>
          <td>{i.executions.length === 0 ? t.none : <ul>{i.executions.map(x => <li key={x.id}><code>{x.state}</code> · {label(t, 'verification_' + x.verificationState)}
            {x.durationMinutes !== null && <> · {x.durationMinutes} {t.minutes}</>} · {t.master}: <code>{x.endedBy ?? x.startedBy}</code> · {t.evidenceEvents}: {x.evidenceEvents}
            <br/><a href={`/app/towage-execution?vessel=${i.vesselId}&id=${x.id}`}>{t.openExecution}</a></li>)}</ul>}</td>
          <td>{i.dailyLogs.length === 0 ? t.none : <ul>{i.dailyLogs.map(l => <li key={l.id}><a href={`/app/tug-daily-log?vessel=${l.tugId}&id=${l.id}`}>{l.logDate} {l.watch}</a> · <code>{l.state}</code>
            {!l.linkedToExecution && <> · {t.notLinked}</>}</li>)}</ul>}</td>
        </tr>)}</tbody></table>
      {next && <Button type="button" variant="outline" disabled={offline || state === 'loading'} onClick={() => { cursor.current = next; setMore(n => n + 1); }}>{t.loadMore}</Button>}
    </>}
  </section>;
}

// ---- DEV-092 offline queue ----
type QueueState = { queue?: TugOperationsOfflineQueue; snapshot?: TugSnapshot | null; captures: QueuedCapture[]; capability?: OfflineCapability; failure?: string };
function OfflinePanel({ vessel, job, t, offline }: { vessel: string; job: string; t: Copy; offline: boolean }) {
  const [s, setS] = useState<QueueState>({ captures: [] }), [busy, setBusy] = useState(false), [notice, setNotice] = useState('');
  const queue = useRef<TugOperationsOfflineQueue | null>(null), syncing = useRef(false);
  const refresh = useCallback(async () => {
    const q = queue.current; if (!q) return;
    const [snapshot, captures] = await Promise.all([q.latestSnapshot(), q.captures()]);
    setS(v => ({ ...v, snapshot, captures }));
  }, []);
  useEffect(() => {
    if (!job) return;
    const c = new AbortController(); let active = true;
    (async () => {
      const capability = navigator.onLine ? await loadCapability(vessel, c.signal) : undefined;
      const q = await TugOperationsOfflineQueue.open(vessel, job, c.signal);
      if (!active) { q.close(); return; }
      queue.current = q; setS(v => ({ ...v, capability, queue: q })); await refresh();
    })().catch(e => { if (active) setS(v => ({ ...v, failure: e instanceof ApiError ? String(e.status) : e instanceof Error ? e.message : 'failed' })); });
    return () => { active = false; c.abort(); queue.current?.close(); queue.current = null; };
  }, [vessel, job, refresh]);
  // Reconnect: replay every queued capture in order with its original command id; outcomes come only from the server.
  const sync = useCallback(async () => {
    const q = queue.current; if (!q || syncing.current || !navigator.onLine) return;
    syncing.current = true; setBusy(true); setNotice('');
    try {
      for (const capture of (await q.captures()).filter(c => !c.outcome && !c.failure)) {
        const c = new AbortController(), timer = setTimeout(() => c.abort(), 15000);
        try { await q.replay(capture, c.signal); }
        catch (e) { if (!(e instanceof ApiError) || ![400, 403, 404, 409, 422].includes(e.status)) { setNotice('uncertain'); break; } }
        finally { clearTimeout(timer); }
      }
    } finally { syncing.current = false; setBusy(false); await refresh(); }
  }, [refresh]);
  useEffect(() => { if (!offline) void sync(); }, [offline, sync]);
  const prefetch = async () => {
    const q = queue.current; if (!q || offline) return;
    setBusy(true); setNotice('');
    const c = new AbortController();
    try { await q.prefetch(c.signal); await refresh(); setNotice('prefetched'); }
    catch (e) { setNotice(e instanceof ApiError && e.code === 'offline_policy_denied' ? 'policy' : 'failed'); }
    finally { setBusy(false); }
  };
  const capture = async (payload: CapturePayload, resolution?: { parentCommandId: string; choice: 'keepServer' | 'useCaptured'; reason: string }) => {
    const q = queue.current; if (!q || !s.snapshot) return;
    await q.capture(s.snapshot, payload, resolution); setNotice('queued'); await refresh();
    if (navigator.onLine) await sync();
  };
  if (!job) return <p role="status">{t.selectJob}</p>;
  const policy = s.capability?.state;
  const snap = s.snapshot;
  return <section aria-label={t.screenOffline}>
    <p>{t.policy}: <code>{policy ?? t.unknownOffline}</code></p>
    {policy === 'UNAVAILABLE_POLICY' && <p role="alert">{t.policyUnavailable}</p>}
    <p>{t.onlineOnly}</p>
    {s.failure && <p role="alert">{s.failure === 'key_lost_data_retained' ? t.keyLost : t.queueUnavailable}</p>}
    <EnterpriseActions layout="form" aria-label={t.prefetch}>
      <Button type="button" disabled={busy || offline || !s.queue || policy !== 'CONFIGURED_UNVERIFIED'} onClick={() => void prefetch()}>{t.prefetch}</Button>
      <Button type="button" variant="outline" disabled={busy || offline || !s.queue} onClick={() => void sync()}>{t.syncNow}</Button></EnterpriseActions>
    {notice && <p role={notice === 'prefetched' || notice === 'queued' ? 'status' : 'alert'}>{label(t, 'notice_' + notice)}</p>}
    {snap === null && <p role="status">{t.noSnapshot}</p>}
    {snap && <SnapshotView snapshot={snap} t={t} disabled={busy} onCapture={p => void capture(p)}/>}
    <h2>{t.queue}</h2>
    {s.captures.length === 0 ? <p>{t.queueEmpty}</p> : <ul className="tug-offline-queue">{s.captures.map(c => <QueueRow key={c.commandId} capture={c} t={t} disabled={busy || !snap}
      onResolve={(choice, reason) => { const body = JSON.parse(c.body) as Record<string, unknown>;
        const payload = (body.briefingAck ? { briefingAck: body.briefingAck } : body.executionEvent ? { executionEvent: body.executionEvent } : { dailyEntry: body.dailyEntry }) as CapturePayload;
        void capture(payload, { parentCommandId: c.commandId, choice, reason }); }}/>)}</ul>}
  </section>;
}

function SnapshotView({ snapshot, t, disabled, onCapture }: { snapshot: TugSnapshot; t: Copy; disabled: boolean; onCapture: (p: CapturePayload) => void }) {
  const [ack, setAck] = useState<'ACK' | 'DISPUTE'>('ACK'), [reason, setReason] = useState('');
  const [execution, setExecution] = useState(snapshot.executions[0]?.id ?? ''), [kind, setKind] = useState('NOTE'), [note, setNote] = useState('');
  const [log, setLog] = useState(''), [summary, setSummary] = useState('');
  const x = snapshot.executions.find(e => e.id === execution);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 30000); return () => clearInterval(timer); }, []);
  const expired = Date.parse(snapshot.expiresAt) <= now;
  return <section aria-label={t.snapshot}>
    <h2>{t.snapshot}</h2>
    <p>{t.job}: <strong>{snapshot.job.jobNumber}</strong> · <code>{snapshot.job.status}</code> · {t.leaseUntil}: <time dateTime={snapshot.expiresAt}>{snapshot.expiresAt}</time></p>
    {expired && <p role="alert">{t.leaseExpired}</p>}
    <p><small>{t.snapshotHash}: <code>{snapshot.snapshotHash.slice(0, 16)}</code> · {snapshot.classification}</small></p>
    {snapshot.briefing && snapshot.briefing.participant && !snapshot.briefing.acknowledged && snapshot.briefing.state === 'ISSUED' &&
      <form aria-label={t.ackBriefing} onSubmit={e => { e.preventDefault(); if (ack === 'ACK' || reason.trim().length >= 10)
        onCapture({ briefingAck: { briefingId: snapshot.briefing!.id, revision: snapshot.briefing!.revision, contentHash: snapshot.briefing!.contentHash, choice: ack, reason: ack === 'DISPUTE' ? reason.trim() : null } }); }}>
        <p>{t.briefing} r{snapshot.briefing.revision} · <code>{snapshot.briefing.acknowledgementState}</code></p>
        <label htmlFor="tof-ack">{t.ackChoice}</label><select id="tof-ack" value={ack} onChange={e => setAck(e.target.value as 'ACK' | 'DISPUTE')}>
          <option value="ACK">{t.ack}</option><option value="DISPUTE">{t.dispute}</option></select>
        {ack === 'DISPUTE' && <><label htmlFor="tof-ack-reason">{t.reason}</label><textarea id="tof-ack-reason" minLength={10} maxLength={2000} value={reason} onChange={e => setReason(e.target.value)}/></>}
        <EnterpriseActions layout="form" aria-label={t.ackBriefing}><Button type="submit" disabled={disabled || expired}>{t.queueCapture}</Button></EnterpriseActions></form>}
    {snapshot.executions.length > 0 && <form aria-label={t.recordEvent} onSubmit={e => { e.preventDefault(); if (!x || !note.trim()) return;
      onCapture({ executionEvent: { executionId: x.id, baseState: x.state, baseSequence: x.sequence,
        event: { kind, occurredAt: new Date().toISOString(), value: null, unit: null, result: null, text: note.trim(), reference: null, weather: null } } }); setNote(''); }}>
      <label htmlFor="tof-exe">{t.execution}</label><select id="tof-exe" value={execution} onChange={e => setExecution(e.target.value)}>
        {snapshot.executions.map(e => <option key={e.id} value={e.id}>{e.state} · {e.tugId.slice(0, 8)}</option>)}</select>
      <label htmlFor="tof-kind">{t.eventKind}</label><select id="tof-kind" value={kind} onChange={e => setKind(e.target.value)}>
        {(x?.recordableKinds ?? []).filter(k => ['NOTE', 'TOWLINE_CONNECTED', 'TOWLINE_RELEASED'].includes(k)).map(k => <option key={k} value={k}>{label(t, 'kind_' + k)}</option>)}</select>
      <label htmlFor="tof-note">{t.note}</label><textarea id="tof-note" maxLength={4000} value={note} onChange={e => setNote(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.recordEvent}><Button type="submit" disabled={disabled || expired || !x?.canRecord || !note.trim()}>{t.queueCapture}</Button></EnterpriseActions></form>}
    {snapshot.writableTugs.length > 0 && <form aria-label={t.dailyEntry} onSubmit={e => { e.preventDefault(); const l = snapshot.dailyLogs.find(d => d.id === log); if (!l || !summary.trim()) return;
      onCapture({ dailyEntry: { logId: l.id, tugId: l.tugId, baseVersion: l.version, logDate: l.logDate, watch: l.watch, content: { ...l.content, dailySummary: summary.trim() } } }); setSummary(''); }}>
      <label htmlFor="tof-log">{t.dailyLog}</label><select id="tof-log" value={log} onChange={e => setLog(e.target.value)}>
        <option value="">—</option>{snapshot.dailyLogs.filter(d => d.state === 'DRAFT' || d.state === 'RETURNED').map(d => <option key={d.id} value={d.id}>{d.logDate} {d.watch} · {d.state}</option>)}</select>
      <label htmlFor="tof-summary">{t.dailySummary}</label><textarea id="tof-summary" maxLength={4000} value={summary} onChange={e => setSummary(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.dailyEntry}><Button type="submit" disabled={disabled || expired || !log || !summary.trim()}>{t.queueCapture}</Button></EnterpriseActions></form>}
  </section>;
}

function QueueRow({ capture, t, disabled, onResolve }: { capture: QueuedCapture; t: Copy; disabled: boolean; onResolve: (choice: 'keepServer' | 'useCaptured', reason: string) => void }) {
  const [choice, setChoice] = useState(''), state = queuedState(capture);
  return <li data-state={state}><code>{label(t, 'op_' + capture.operation)}</code> · <Tone state={state} t={t}/>
    {capture.failure && <> · <code>{capture.failure}</code></>}
    {capture.outcome?.result === 'MaterialConflict' && <form aria-label={t.resolve} onSubmit={e => { e.preventDefault();
      if (choice === 'keepServer' || choice === 'useCaptured') onResolve(choice, choice === 'keepServer' ? 'CURRENT_SERVER_RETAINED' : 'INPUT_CORRECTED'); }}>
      <p>{t.conflictFields}: {capture.outcome.conflictFields.map(f => <code key={f}>{label(t, 'field_' + f)} </code>)}</p>
      <fieldset><legend>{t.resolve}</legend>{capture.outcome.choices.map(c => <label key={c}><input type="radio" name={`res-${capture.commandId}`} value={c} checked={choice === c}
        onChange={() => setChoice(c)}/>{label(t, 'choice_' + c)}</label>)}</fieldset>
      <EnterpriseActions layout="form" aria-label={t.resolve}><Button type="submit" disabled={disabled || !choice}>{t.resolve}</Button></EnterpriseActions></form>}
  </li>;
}

// ---- DEV-119 demo journey ----
const demoSteps = [
  ['intake', 'tug-job-intake'], ['assignment', 'tug-assignment'], ['readiness', 'tug-readiness'], ['plan', 'towage-plan'], ['briefing', 'towage-risk-briefing'],
  ['release', 'tug-readiness'], ['execution', 'towage-execution'], ['evidence', 'towage-execution'], ['history', 'tug-operation-history'], ['offline', 'tug-operations-offline'],
] as const;
export function demoHref(screen: string, vessel: string, job: string) {
  const withJob = job && ['tug-assignment', 'tug-readiness', 'towage-plan', 'towage-risk-briefing', 'towage-execution', 'tug-operation-history', 'tug-operations-offline'].includes(screen);
  return `/app/${screen}?vessel=${vessel}${withJob ? `&job=${job}` : ''}`;
}
function DemoPanel({ vessel, job, t }: { vessel: string; job: string; t: Copy }) {
  return <section aria-label={t.screenDemo}>
    <p>{t.demoIntro}</p>
    <ol className="tug-demo-steps">{demoSteps.map(([step, screen], n) => <li key={step} data-step={step}>
      <h2>{n + 1}. {label(t, 'step_' + step)}</h2>
      <p><a href={demoHref(screen, vessel, job)}>{t.openStep}</a></p>
      <details><summary>{t.why}</summary><p>{label(t, 'why_' + step)}</p></details>
    </li>)}</ol>
    <p role="note">{t.demoBoundary}</p>
  </section>;
}
