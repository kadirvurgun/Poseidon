import { useCallback, useEffect, useRef, useState } from 'react';
import { localizedCopy } from '../i18n/messages';
import { useAppLanguage, type Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { EnterpriseActions } from './EnterprisePatterns';
import { ApiError, session, type Session } from './api';
import { isGuid } from './identifiers';
import { send, type Command } from './tug-readiness-api';
import { decisions, exceptionKinds, reconciliationApi, type ListView, type ReconException, type ReportView } from './data-reconciliation-api';

const copy = localizedCopy('DataReconciliation_tsx_copy');
type Copy = (typeof copy)[AppLocale];
type Notice = '' | 'done' | 'uncertain' | 'conflict' | 'denied' | 'failed' | 'incomplete';
const label = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key;

// /app/data-reconciliation/{vessel}[?report={id}]
export function reconciliationRoute(href: string) {
  const url = new URL(href, 'http://local');
  const match = /^\/app\/data-reconciliation\/([0-9a-fA-F-]{36})\/?$/.exec(url.pathname);
  const report = url.searchParams.get('report') ?? '';
  const invalid = !match || !isGuid(match[1]) || [...url.searchParams.keys()].some(k => k !== 'report' || url.searchParams.getAll(k).length !== 1) || report !== '' && !isGuid(report);
  return { vessel: match?.[1]?.toLowerCase() ?? '', report: report.toLowerCase(), invalid };
}

// An unsigned report waits for the data owner; a signature is informational and a superseded one is invalidated.
// Nothing here ever renders as a green "accepted/ready" state.
function StateBadge({ state, t }: { state: string; t: Copy }) {
  const tone = state === 'SIGNED_OFF' ? 'info' : state === 'SUPERSEDED' ? 'danger' : 'warning';
  return <Badge tone={tone} data-state={state}>{label(t, 'state_' + state)}</Badge>;
}

function CountsTable({ counts, t }: { counts: ReportView['report']['counts']; t: Copy }) {
  const rows: [string, number][] = [['source', counts.source], ['accepted', counts.accepted], ['rejected', counts.rejected], ['duplicate', counts.duplicate],
    ['notCommitted', counts.notCommitted], ['missingCritical', counts.missingCritical]];
  return <table aria-label={t.counts}><tbody>{rows.map(([k, v]) => <tr key={k}><th scope="row">{label(t, 'count_' + k)}</th><td data-count={k}>{v}</td></tr>)}</tbody></table>;
}

export function DataReconciliationWorkspace() {
  const [lang, language] = useAppLanguage(), t = copy[lang];
  const [route, setRoute] = useState(() => reconciliationRoute(window.location.href));
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true), [error, setError] = useState(0), [offline, setOffline] = useState(!navigator.onLine);
  const [list, setList] = useState<ListView | null>(null), [view, setView] = useState<ReportView | null>(null);
  const [notice, setNotice] = useState<Notice>(''), [busy, setBusy] = useState(false), [retained, setRetained] = useState<Command | null>(null);
  const [selected, setSelected] = useState<string[]>([]), [sampleSize, setSampleSize] = useState('10');
  const [decision, setDecision] = useState<typeof decisions[number] | ''>(''), [reason, setReason] = useState(''), [authority, setAuthority] = useState('');
  const [verified, setVerified] = useState<string[]>([]), [exceptions, setExceptions] = useState<ReconException[]>([]);
  const lock = useRef(false), mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true), pop = () => { setRoute(reconciliationRoute(window.location.href)); setReload(n => n + 1); };
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop);
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop); };
  }, []);
  const move = (href: string) => { if (retained || busy) return; history.pushState(null, '', href); setRoute(reconciliationRoute(window.location.href)); setNotice(''); setReload(n => n + 1); };
  useEffect(() => {
    const c = new AbortController();
    (async () => {
      await Promise.resolve();
      if (c.signal.aborted) return;
      setLoading(true); setError(0); setList(null); setView(null);
      if (route.invalid) throw new ApiError(400, 'invalid_route');
      setUser(await session(c.signal));
      if (route.report) {
        const v = await reconciliationApi.report(route.vessel, route.report, c.signal);
        if (c.signal.aborted) return;
        setView(v); setVerified([]); setExceptions([]); setDecision(''); setReason(''); setAuthority(v.authorities.length === 1 ? v.authorities[0].id : '');
      } else setList(await reconciliationApi.list(route.vessel, c.signal));
    })().then(() => { if (!c.signal.aborted) setLoading(false); }).catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e.status : 500); setLoading(false); });
    return () => c.abort();
  }, [route, reload]);

  // One command at a time; an uncertain outcome keeps the exact key/body for an identical retry. After a confirmed
  // receipt the canonical report id moves into the URL before the optional re-read.
  const run = useCallback(async (command: Command, report?: string): Promise<boolean> => {
    if (lock.current || !navigator.onLine) return false;
    lock.current = true; setBusy(true); setNotice(''); setRetained(command);
    const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000);
    let sent = false;
    try {
      const s = await session(c.signal);
      if (!s.authenticated || !s.csrfToken || user && `${s.tenantId}/${s.actorId}` !== `${user.tenantId}/${user.actorId}`) throw new ApiError(403, 'identity_boundary_changed');
      sent = true;
      await send(command, s.csrfToken, c.signal);
      if (!mounted.current) return false;
      setRetained(null); setNotice('done');
      if (report) { history.replaceState(null, '', `/app/data-reconciliation/${route.vessel}?report=${report}`); setRoute(reconciliationRoute(window.location.href)); }
      setReload(n => n + 1); return true;
    } catch (e) {
      if (!mounted.current) return false;
      const definite = e instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(e.status);
      if (!sent || definite) setRetained(null);
      setNotice(sent && !definite ? 'uncertain' : e instanceof ApiError && e.status === 422 ? 'incomplete'
        : e instanceof ApiError && e.status === 409 ? 'conflict' : e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : 'failed');
      return false;
    } finally { clearTimeout(timeout); lock.current = false; if (mounted.current) setBusy(false); }
  }, [user, route.vessel]);
  const disabled = busy || offline || !!retained && notice !== 'uncertain';

  return <>
    <EnterpriseChrome session={user} vessel={route.vessel || null} language={lang} onLanguageChange={language} onNavigate={href => { if (!retained && !busy) window.location.assign(href); }}
      title={t.title} mainId="data-reconciliation-main"/>
    <main id="data-reconciliation-main" tabIndex={-1} aria-busy={loading || busy}>
      <div className="enterprise-page-heading"><div><p className="enterprise-page-eyebrow">{t.eyebrow}</p><h1>{t.title}</h1></div></div>
      <p className="tug-planning-boundary">{t.boundary}</p>
      {offline && <p role="alert">{t.offline}</p>}
      {notice && <p role={notice === 'done' ? 'status' : 'alert'} data-notice={notice}>{label(t, 'notice_' + notice)}</p>}
      {retained && notice === 'uncertain' && <EnterpriseActions layout="wrap" aria-label={t.retry}><Button onClick={() => void run(retained)} disabled={busy || offline}>{t.retry}</Button></EnterpriseActions>}
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error > 0 && <div role="alert"><p>{[401, 403].includes(error) ? t.denied : error === 404 || error === 400 ? t.notFound : t.unavailable}</p>
        <Button variant="outline" onClick={() => setReload(n => n + 1)}>{t.reload}</Button></div>}
      {!loading && !error && list && <section aria-label={t.reports}>
        <h2>{t.templateVersions}</h2>
        <ul>{Object.entries(list.templateVersions).map(([k, v]) => <li key={k}><span>{label(t, 'category_' + k)}</span> <code>{v}</code></li>)}</ul>
        <h2>{t.reports}</h2>
        {list.items.length === 0 ? <p role="status">{t.noReports}</p> : <ul>{list.items.map(i => <li key={i.id}>
          <button type="button" disabled={disabled} onClick={() => move(`/app/data-reconciliation/${route.vessel}?report=${i.id}`)}>{new Date(i.createdAt).toLocaleString(lang)}</button>{' '}
          <StateBadge state={i.state} t={t}/>{i.decision && <> <span>{label(t, 'decision_' + i.decision)}</span></>}{' '}
          {i.signOffValidity === 'INVALIDATED_SUPERSEDED' && <span data-validity="INVALIDATED_SUPERSEDED">{t.validity_INVALIDATED_SUPERSEDED}</span>}{' '}
          <span>{t.count_source}: {i.counts.source}</span></li>)}</ul>}
        {list.canCreate && <form aria-label={t.create} onSubmit={e => {
          e.preventDefault();
          const n = Number(sampleSize);
          if (selected.length === 0 || !Number.isInteger(n) || n < 1 || n > list.maxSample) return;
          const id = crypto.randomUUID(); void run(reconciliationApi.create(route.vessel, id, selected, n), id);
        }}>
          <h2>{t.create}</h2>
          {list.batches.length === 0 ? <p role="status">{t.noBatches}</p> : <fieldset><legend>{t.batches}</legend>{list.batches.map(b => <div key={b.id}>
            <input id={'batch-' + b.id} type="checkbox" disabled={disabled || !selected.includes(b.id) && selected.length >= list.maxBatches} checked={selected.includes(b.id)}
              onChange={e => setSelected(s => e.target.checked ? [...s, b.id] : s.filter(x => x !== b.id))}/>
            <label htmlFor={'batch-' + b.id}>{label(t, 'category_' + b.category)} · {label(t, 'batch_' + b.state)} · {b.rowCount} · <code>{b.templateVersion}</code></label></div>)}</fieldset>}
          <label htmlFor="reconciliation-sample">{t.sampleSize}</label>
          <input id="reconciliation-sample" type="number" min={1} max={list.maxSample} required value={sampleSize} disabled={disabled} onChange={e => setSampleSize(e.target.value)}/>
          <EnterpriseActions layout="form" aria-label={t.create}><Button type="submit" disabled={disabled || selected.length === 0}>{t.create}</Button></EnterpriseActions>
        </form>}
      </section>}
      {!loading && !error && view && <ReportPanel view={view} t={t} lang={lang} disabled={disabled} decision={decision} setDecision={setDecision} reason={reason} setReason={setReason}
        authority={authority} setAuthority={setAuthority} verified={verified} setVerified={setVerified} exceptions={exceptions} setExceptions={setExceptions}
        back={() => move(`/app/data-reconciliation/${route.vessel}`)}
        submit={() => { if (decision && authority) void run(reconciliationApi.signOff(route.vessel, view.report, decision, reason.trim(), exceptions, verified, authority), view.report.id); }}/>}
    </main>
  </>;
}

type PanelProps = { view: ReportView; t: Copy; lang: string; disabled: boolean; decision: typeof decisions[number] | ''; setDecision: (d: typeof decisions[number] | '') => void
  reason: string; setReason: (v: string) => void; authority: string; setAuthority: (v: string) => void; verified: string[]; setVerified: (f: (v: string[]) => string[]) => void
  exceptions: ReconException[]; setExceptions: (f: (v: ReconException[]) => ReconException[]) => void; back: () => void; submit: () => void };

function ReportPanel({ view, t, lang, disabled, decision, setDecision, reason, setReason, authority, setAuthority, verified, setVerified, exceptions, setExceptions, back, submit }: PanelProps) {
  const r = view.report, s = view.signOff;
  const valid = decision !== '' && authority !== '' && reason.trim().length >= 10 &&
    exceptions.every(x => x.reference.trim() !== '' && x.disposition.trim().length >= 3) && (decision !== 'ACCEPTED_WITH_EXCEPTIONS' || exceptions.length > 0);
  return <section aria-label={t.report}>
    <EnterpriseActions layout="wrap" aria-label={t.back}><Button variant="outline" onClick={back} disabled={disabled}>{t.back}</Button></EnterpriseActions>
    <h2>{t.report} <StateBadge state={r.state} t={t}/></h2>
    {r.provenance === 'SyntheticUnverified' && <p role="note">{t.synthetic}</p>}
    {view.stale && <p role="alert" data-stale="true">{t.stale}</p>}
    {r.state === 'SUPERSEDED' && <p role="alert">{t.superseded}</p>}
    <p><span>{t.hash}</span> <code>{r.hash}</code> · <span>{t.policy}</span> <code>{r.policyVersion}</code> · <code>{r.criticalFieldVersion}</code></p>
    <CountsTable counts={r.counts} t={t}/>
    <h3>{t.batches}</h3>
    <table aria-label={t.batches}><thead><tr><th>{t.category}</th><th>{t.batchState}</th><th>{t.count_source}</th><th>{t.count_accepted}</th><th>{t.count_rejected}</th><th>{t.count_duplicate}</th></tr></thead>
      <tbody>{r.batches.map(b => <tr key={b.batchId}><td>{label(t, 'category_' + b.category)}</td><td>{label(t, 'batch_' + b.state)}</td><td>{b.sourceRows}</td><td>{b.acceptedRows}</td>
        <td>{b.rejectedRows}</td><td>{b.duplicateRows}</td></tr>)}</tbody></table>
    <h3>{t.missingCritical}</h3>
    {r.missingCritical.length === 0 ? <p>{t.noMissing}</p> : <ul>{r.missingCritical.map(m => <li key={`${m.targetId}/${m.field}`} data-reason={m.reason}>
      <code>{m.targetKind}</code> · {t.row} {m.rowNumber} · <code>{m.field}</code> · {label(t, 'reason_' + m.reason)}</li>)}</ul>}
    {r.missingCriticalTruncated && <p role="note">{t.truncated}</p>}
    <h3>{t.sample}</h3>
    <p>{t.sampleSeed} <code>{r.sampleSeed}</code></p>
    {r.sample.length === 0 ? <p>{t.noSample}</p> : <ul>{r.sample.map(x => <li key={x.targetId}>
      <input id={'sample-' + x.targetId} type="checkbox" disabled={disabled || !view.canSignOff} checked={verified.includes(x.targetId)}
        onChange={e => setVerified(v => e.target.checked ? [...v, x.targetId] : v.filter(y => y !== x.targetId))}/>
      <label htmlFor={'sample-' + x.targetId}><code>{x.targetKind}</code> · {t.row} {x.rowNumber} · <code>{x.targetId}</code> — {t.sampleVerified}</label></li>)}</ul>}
    <h3>{t.openItems}</h3>
    {view.openItems.length === 0 ? <p>{t.noOpenItems}</p> : <ul>{view.openItems.map(o => <li key={o}>{label(t, 'kind_' + o)}</li>)}</ul>}
    {s && <section aria-label={t.signOff}>
      <h3>{t.signOff}</h3>
      <p><span>{label(t, 'decision_' + s.decision)}</span> · <span data-validity={s.validity}>{label(t, 'validity_' + s.validity)}</span> · {new Date(s.signedAt).toLocaleString(lang)}</p>
      <p>{s.reason}</p>
      {s.exceptions.length > 0 && <ul>{s.exceptions.map((x, i) => <li key={i}>{label(t, 'kind_' + x.kind)} · {x.reference} · {x.disposition}</li>)}</ul>}
    </section>}
    {!s && !view.canSignOff && <section aria-label={t.blockers}><h3>{t.blockers}</h3><ul>{view.signOffBlockers.map(b => <li key={b}>{label(t, 'blocker_' + b)}</li>)}</ul></section>}
    {!s && view.canSignOff && <form aria-label={t.signOff} onSubmit={e => { e.preventDefault(); if (valid) submit(); }}>
      <h3>{t.signOff}</h3>
      <p role="note">{t.ownerOnly}</p>
      <label htmlFor="reconciliation-authority">{t.authority}</label>
      <select id="reconciliation-authority" required value={authority} disabled={disabled} onChange={e => setAuthority(e.target.value)}>
        <option value="">—</option>{view.authorities.map(a => <option key={a.id} value={a.id}>{t.duty_DataOwner}</option>)}</select>
      <label htmlFor="reconciliation-decision">{t.decision}</label>
      <select id="reconciliation-decision" required value={decision} disabled={disabled} onChange={e => setDecision(e.target.value as typeof decisions[number] | '')}>
        <option value="">—</option>{decisions.map(d => <option key={d} value={d}>{label(t, 'decision_' + d)}</option>)}</select>
      <fieldset><legend>{t.exceptions}</legend>
        {exceptions.map((x, i) => <div key={i}>
          <label htmlFor={`exception-kind-${i}`}>{t.exceptionKind}</label>
          <select id={`exception-kind-${i}`} value={x.kind} disabled={disabled} onChange={e => setExceptions(v => v.map((y, j) => j === i ? { ...y, kind: e.target.value as ReconException['kind'] } : y))}>
            {exceptionKinds.map(k => <option key={k} value={k}>{label(t, 'kind_' + k)}</option>)}</select>
          <label htmlFor={`exception-reference-${i}`}>{t.exceptionReference}</label>
          <input id={`exception-reference-${i}`} maxLength={200} value={x.reference} disabled={disabled} onChange={e => setExceptions(v => v.map((y, j) => j === i ? { ...y, reference: e.target.value } : y))}/>
          <label htmlFor={`exception-disposition-${i}`}>{t.exceptionDisposition}</label>
          <textarea id={`exception-disposition-${i}`} maxLength={1000} value={x.disposition} disabled={disabled} onChange={e => setExceptions(v => v.map((y, j) => j === i ? { ...y, disposition: e.target.value } : y))}/>
          <Button type="button" variant="outline" disabled={disabled} onClick={() => setExceptions(v => v.filter((_, j) => j !== i))}>{t.removeException}</Button>
        </div>)}
        <Button type="button" variant="outline" disabled={disabled || exceptions.length >= 100}
          onClick={() => setExceptions(v => [...v, { kind: (view.openItems[v.length] as ReconException['kind'] | undefined) ?? 'OTHER', reference: '', disposition: '' }])}>{t.addException}</Button>
      </fieldset>
      <label htmlFor="reconciliation-reason">{t.reason}</label>
      <textarea id="reconciliation-reason" required minLength={10} maxLength={2000} value={reason} disabled={disabled} onChange={e => setReason(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.signOff}><Button type="submit" disabled={disabled || !valid}>{t.signOff}</Button></EnterpriseActions>
    </form>}
  </section>;
}
