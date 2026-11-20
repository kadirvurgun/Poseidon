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
import { send, type Command } from './tug-readiness-api';
import { emptyBriefing, emptyPlan, lineSources, limitParameters, methods, participantRoles, positions, scenarios, towageApi,
  type Authority, type BriefingContent, type BriefingView, type Item, type PlanContent, type PlanView } from './towage-api';
import './tug.css';

const copy = localizedCopy('Towage_tsx_copy');
type Copy = (typeof copy)[AppLocale];
export const towageScreens = ['towage-plan', 'towage-risk-briefing'] as const;
type Screen = typeof towageScreens[number];
type Notice = '' | 'done' | 'uncertain' | 'conflict' | 'denied' | 'failed' | 'incomplete';
const label = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key;

export function towageRoute(href: string) {
  const url = new URL(href, 'http://local'), params = url.searchParams;
  const match = /^\/app\/(towage-plan|towage-risk-briefing)\/?$/.exec(url.pathname);
  const value = (k: string) => params.get(k) ?? '';
  const invalid = !match || [...params.keys()].some(k => !['vessel', 'job'].includes(k) || params.getAll(k).length !== 1) ||
    ['vessel', 'job'].some(k => value(k) !== '' && !isGuid(value(k)));
  return { screen: (match?.[1] ?? 'towage-plan') as Screen, vessel: value('vessel'), job: value('job'), invalid };
}
export const towageHref = (screen: Screen, vessel: string, job?: string) => `/app/${screen}?vessel=${vessel}${job ? `&job=${job}` : ''}`;

function StateBadge({ state, t }: { state: string; t: Copy }) {
  // APPROVED/ISSUED is informational (never a green GO tone); RETURNED/blocked states warn.
  return <Badge tone={state === 'APPROVED' || state === 'ISSUED' ? 'info' : state === 'RETURNED' ? 'danger' : 'warning'} data-state={state}>{label(t, 'state_' + state)}</Badge>;
}
function AuthorityPicker({ id, authorities, value, onChange, t, disabled }: { id: string; authorities: Authority[]; value: string; onChange: (v: string) => void; t: Copy; disabled: boolean }) {
  return <><label htmlFor={id}>{t.authority}</label>
    <select id={id} required value={value} disabled={disabled} onChange={e => onChange(e.target.value)}>
      <option value="">—</option>{authorities.map(a => <option key={a.id} value={a.id}>{label(t, 'duty_' + a.duty)}</option>)}</select></>;
}
const blank = (v: string) => v.trim() === '' ? null : v;

function Field({ id, text, value, onChange, disabled, long }: { id: string; text: string; value: string | null; onChange: (v: string | null) => void; disabled: boolean; long?: boolean }) {
  return <><label htmlFor={id}>{text}</label>{long
    ? <textarea id={id} maxLength={4000} disabled={disabled} value={value ?? ''} onChange={e => onChange(blank(e.target.value))}/>
    : <input id={id} maxLength={200} disabled={disabled} value={value ?? ''} onChange={e => onChange(blank(e.target.value))}/>}</>;
}
function ItemList({ id, title, items, onChange, t, disabled }: { id: string; title: string; items: Item[]; onChange: (v: Item[]) => void; t: Copy; disabled: boolean }) {
  return <fieldset><legend>{title}</legend>
    {items.map((x, i) => <div key={x.id}><label htmlFor={`${id}-${i}`}>{t.description}</label>
      <input id={`${id}-${i}`} maxLength={4000} disabled={disabled} value={x.description} onChange={e => onChange(items.map(y => y.id === x.id ? { ...y, description: e.target.value } : y))}/>
      <Button type="button" variant="outline" disabled={disabled} onClick={() => onChange(items.filter(y => y.id !== x.id))}>{t.remove}</Button></div>)}
    <Button type="button" variant="outline" disabled={disabled || items.length >= 30} onClick={() => onChange([...items, { id: crypto.randomUUID(), description: '' }])}>{t.add}</Button>
  </fieldset>;
}
const itemsValid = (xs: Item[]) => xs.every(x => x.description.trim() !== '');

export function TowageWorkspace() {
  const [lang, language] = useAppLanguage(), t = copy[lang];
  const [route, setRoute] = useState(() => towageRoute(window.location.href));
  const [user, setUser] = useState<Session | null>(null), [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true), [error, setError] = useState(0), [offline, setOffline] = useState(!navigator.onLine);
  const [board, setBoard] = useState<TugBoard | null>(null), [plan, setPlan] = useState<PlanView | null>(null), [briefing, setBriefing] = useState<BriefingView | null>(null);
  const [notice, setNotice] = useState<Notice>(''), [busy, setBusy] = useState(false), [retained, setRetained] = useState<Command | null>(null);
  const lock = useRef(false), mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true), pop = () => { setRoute(towageRoute(window.location.href)); setReload(n => n + 1); };
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop);
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop); };
  }, []);
  const move = (href: string) => { if (retained || busy) return; history.pushState(null, '', href); setRoute(towageRoute(window.location.href)); setNotice(''); setReload(n => n + 1); };
  useEffect(() => {
    const c = new AbortController();
    (async () => {
      await Promise.resolve();
      if (c.signal.aborted) return;
      setLoading(true); setError(0); setBoard(null); setPlan(null); setBriefing(null);
      if (route.invalid) throw new ApiError(400, 'invalid_route');
      setUser(await session(c.signal));
      if (!route.vessel) return;
      if (!route.job) setBoard(await tugApi.board(route.vessel, '', '', 0, c.signal, 'Submitted'));
      else if (route.screen === 'towage-plan') setPlan(await towageApi.plan(route.vessel, route.job, c.signal));
      else setBriefing(await towageApi.briefing(route.vessel, route.job, c.signal));
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
      setNotice(sent && !definite ? 'uncertain' : e instanceof ApiError && e.code === 'towage_record_incomplete' ? 'incomplete'
        : e instanceof ApiError && e.status === 409 ? 'conflict' : e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : 'failed');
      return false;
    } finally { clearTimeout(timeout); lock.current = false; if (mounted.current) setBusy(false); }
  }, [offline, user]);

  const disabled = busy || offline || !!retained && notice !== 'uncertain';
  const titles: Record<Screen, string> = { 'towage-plan': t.screenPlan, 'towage-risk-briefing': t.screenBriefing };
  const message = notice ? label(t, 'notice_' + notice) : '';
  return <div className="product-shell enterprise-shell tug-workspace">
    <EnterpriseChrome session={user} vessel={route.vessel || null} language={lang} onLanguageChange={language} onNavigate={href => { if (!retained && !busy) window.location.assign(href); }} title={titles[route.screen]} mainId="towage-main"/>
    <main id="towage-main" tabIndex={-1} aria-busy={loading || busy}>
      <div className="enterprise-page-heading"><div><p className="enterprise-page-eyebrow">{t.title}</p><h1>{titles[route.screen]}</h1></div></div>
      <p className="tug-planning-boundary">{t.boundary}</p>
      {offline && <p role="alert">{t.offline}</p>}
      {route.vessel && <nav aria-label={t.title}>{towageScreens.map(s => <a key={s} aria-current={route.screen === s ? 'page' : undefined} href={towageHref(s, route.vessel, route.job)}
        onClick={e => { e.preventDefault(); move(towageHref(s, route.vessel, route.job)); }}>{titles[s]} </a>)}</nav>}
      {message && <div role={notice === 'done' ? 'status' : 'alert'}><p>{message}</p>
        {notice === 'uncertain' && retained && <Button type="button" disabled={busy || offline} onClick={() => void run(retained)}>{t.retry}</Button>}
        {notice === 'conflict' && <Button type="button" variant="outline" onClick={() => { setNotice(''); setReload(n => n + 1); }}>{t.reload}</Button>}</div>}
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error > 0 && <div role="alert"><p>{[400, 401, 403, 404].includes(error) ? t.denied : t.unavailable}</p><Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>}
      {!loading && !error && !route.vessel && <p role="status">{t.selectVessel} <a href="/app/tug-command">{t.openTugCommand}</a></p>}
      {!loading && !error && board && <section aria-label={t.selectJob}><h2>{t.selectJob}</h2>{board.items.length === 0 ? <p>{t.noJobs}</p> :
        <ul>{board.items.map(row => <li key={row.request.id}><a href={towageHref(route.screen, route.vessel, row.request.id)}
          onClick={e => { e.preventDefault(); move(towageHref(route.screen, route.vessel, row.request.id)); }}>{row.request.jobNumber}</a></li>)}</ul>}</section>}
      {!loading && !error && plan && <PlanPanel key={`${plan.plan?.id ?? 'new'}:${plan.plan?.version ?? 0}`} data={plan} vessel={route.vessel} t={t} disabled={disabled} run={run}/>}
      {!loading && !error && briefing && <BriefingPanel key={`${briefing.briefing?.id ?? 'new'}:${briefing.briefing?.version ?? 0}`} data={briefing} vessel={route.vessel} t={t} disabled={disabled} run={run}/>}
    </main></div>;
}

type PanelProps = { vessel: string; t: Copy; disabled: boolean; run: (c: Command) => Promise<boolean> };

function ReasonAction({ id, text, t, disabled, onSubmit, variant }: { id: string; text: string; t: Copy; disabled: boolean; onSubmit: (reason: string) => void; variant?: 'outline' | 'destructive' }) {
  const [reason, setReason] = useState('');
  return <form aria-label={text} onSubmit={e => { e.preventDefault(); if (reason.trim().length >= 10) onSubmit(reason.trim()); }}>
    <label htmlFor={id}>{t.reason}</label><textarea id={id} required minLength={10} maxLength={1000} value={reason} onChange={e => setReason(e.target.value)}/>
    <EnterpriseActions layout="form" aria-label={text}><Button type="submit" variant={variant} disabled={disabled || reason.trim().length < 10}>{text}</Button></EnterpriseActions></form>;
}

function PlanPanel({ data, vessel, t, disabled, run }: PanelProps & { data: PlanView }) {
  const p = data.plan;
  const [form, setForm] = useState<PlanContent>(() => p?.content ?? emptyPlan());
  const [authority, setAuthority] = useState(data.authorities.length === 1 ? data.authorities[0].id : ''), [reason, setReason] = useState('');
  const edit = !data.canEdit || disabled;
  const set = (patch: Partial<PlanContent>) => setForm(f => ({ ...f, ...patch }));
  const valid = form.tugs.every(x => isGuid(x.tugId) && (x.towlineItemId === null || isGuid(x.towlineItemId)) && (x.emergencyReleaseItemId === null || isGuid(x.emergencyReleaseItemId))) &&
    form.limits.every(l => l.value === null || /^\d{1,6}(?:\.\d{1,2})?$/.test(l.value)) && new Set(form.limits.map(l => l.parameter)).size === form.limits.length &&
    form.contingencies.every(x => x.action.trim() !== '') && itemsValid(form.snapBack.controls);
  return <section aria-label={t.screenPlan}>
    <p>{t.state}: {p ? <StateBadge state={p.state} t={t}/> : <Badge tone="warning">{t.empty}</Badge>}{p && <> · {t.revision} {p.revision}</>}</p>
    {data.requestState === 'RequestChangedSincePlan' && <p role="alert">{t.requestChanged}</p>}
    <h2>{t.blockers}</h2>{data.blockers.length === 0 ? <p>{p ? t.noBlockers : t.empty}</p> : <ul>{data.blockers.map(b => <li key={b}><code>{b}</code></li>)}</ul>}
    <form aria-label={t.screenPlan} onSubmit={e => { e.preventDefault(); if (valid) void run(towageApi.savePlan(vessel, data.jobId, p, form)); }}>
      <fieldset><legend>{t.secTugs}</legend>
        {form.tugs.map((x, i) => { const up = (patch: Partial<typeof x>) => set({ tugs: form.tugs.map((y, j) => j === i ? { ...y, ...patch } : y) });
          return <div key={`${x.tugId}-${i}`} className="towage-tug-row">
            <label htmlFor={`tow-tug-${i}`}>{t.tug}</label><select id={`tow-tug-${i}`} disabled={edit} value={x.tugId} onChange={e => up({ tugId: e.target.value })}>
              <option value="">—</option>{data.assignedTugs.map(id => <option key={id} value={id}>{id.slice(0, 8)}</option>)}</select>
            <label htmlFor={`tow-pos-${i}`}>{t.position}</label><select id={`tow-pos-${i}`} disabled={edit} value={x.position ?? ''} onChange={e => up({ position: e.target.value || null })}>
              <option value="">{t.unknownOption}</option>{positions.map(v => <option key={v} value={v}>{label(t, 'pos_' + v)}</option>)}</select>
            <label htmlFor={`tow-method-${i}`}>{t.method}</label><select id={`tow-method-${i}`} disabled={edit} value={x.method ?? ''} onChange={e => up({ method: e.target.value || null })}>
              <option value="">{t.unknownOption}</option>{methods.map(v => <option key={v} value={v}>{label(t, 'method_' + v)}</option>)}</select>
            <label><input type="checkbox" disabled={edit} checked={x.coordinating} onChange={e => up({ coordinating: e.target.checked })}/>{t.coordinating}</label>
            <label htmlFor={`tow-line-${i}`}>{t.lineSource}</label><select id={`tow-line-${i}`} disabled={edit} value={x.lineSource ?? ''} onChange={e => up({ lineSource: e.target.value || null })}>
              <option value="">{t.unknownOption}</option>{lineSources.map(v => <option key={v} value={v}>{label(t, 'line_' + v)}</option>)}</select>
            <Field id={`tow-cp-${i}`} text={t.connectionPoint} value={x.connectionPoint} disabled={edit} onChange={v => up({ connectionPoint: v })}/>
            <Field id={`tow-towline-${i}`} text={t.towlineItem} value={x.towlineItemId} disabled={edit} onChange={v => up({ towlineItemId: v })}/>
            <Field id={`tow-release-${i}`} text={t.releaseItem} value={x.emergencyReleaseItemId} disabled={edit} onChange={v => up({ emergencyReleaseItemId: v })}/>
            <Button type="button" variant="outline" disabled={edit} onClick={() => set({ tugs: form.tugs.filter((_, j) => j !== i) })}>{t.remove}</Button></div>; })}
        <Button type="button" variant="outline" disabled={edit || form.tugs.length >= 12} onClick={() => set({ tugs: [...form.tugs, { tugId: data.assignedTugs[0] ?? '', position: null, method: null,
          coordinating: form.tugs.length === 0, lineSource: null, connectionPoint: null, towlineItemId: null, emergencyReleaseItemId: null }] })}>{t.add}</Button>
      </fieldset>
      <fieldset><legend>{t.secExchange}</legend><Field id="tow-exchange" text={t.secExchange} long value={form.informationExchange} disabled={edit} onChange={v => set({ informationExchange: v })}/></fieldset>
      <fieldset><legend>{t.secLimits}</legend>
        {form.limits.map((l, i) => { const up = (patch: Partial<typeof l>) => set({ limits: form.limits.map((y, j) => j === i ? { ...y, ...patch } : y) });
          return <div key={`${l.parameter}-${i}`}>
            <label htmlFor={`tow-limit-p-${i}`}>{t.parameter}</label><select id={`tow-limit-p-${i}`} disabled={edit} value={l.parameter} onChange={e => up({ parameter: e.target.value })}>
              {limitParameters.map(v => <option key={v} value={v}>{label(t, 'limit_' + v)}</option>)}</select>
            <label htmlFor={`tow-limit-v-${i}`}>{t.value} ({data.limitUnits[l.parameter] ?? '—'})</label><input id={`tow-limit-v-${i}`} inputMode="decimal" disabled={edit} value={l.value ?? ''} onChange={e => up({ value: blank(e.target.value) })}/>
            <Field id={`tow-limit-s-${i}`} text={t.limitSource} value={l.sourceRef} disabled={edit} onChange={v => up({ sourceRef: v })}/>
            <Button type="button" variant="outline" disabled={edit} onClick={() => set({ limits: form.limits.filter((_, j) => j !== i) })}>{t.remove}</Button></div>; })}
        {/* A new limit starts empty: no value is pre-filled (no invented default). */}
        <Button type="button" variant="outline" disabled={edit || form.limits.length >= limitParameters.length} onClick={() => {
          const free = limitParameters.find(x => !form.limits.some(l => l.parameter === x)); if (free) set({ limits: [...form.limits, { parameter: free, value: null, sourceRef: null }] }); }}>{t.add}</Button>
      </fieldset>
      <fieldset><legend>{t.secComms}</legend>
        <Field id="tow-ch1" text={t.primaryChannel} value={form.communication.primaryChannel} disabled={edit} onChange={v => set({ communication: { ...form.communication, primaryChannel: v } })}/>
        <Field id="tow-ch2" text={t.standbyChannel} value={form.communication.standbyChannel} disabled={edit} onChange={v => set({ communication: { ...form.communication, standbyChannel: v } })}/>
        <Field id="tow-lang" text={t.language} value={form.communication.workingLanguage} disabled={edit} onChange={v => set({ communication: { ...form.communication, workingLanguage: v } })}/>
        <Field id="tow-lost" text={t.lostComms} long value={form.communication.lostCommunicationProcedure} disabled={edit} onChange={v => set({ communication: { ...form.communication, lostCommunicationProcedure: v } })}/>
      </fieldset>
      <fieldset><legend>{t.secRelease}</legend>
        <Field id="tow-rel-proc" text={t.releaseProcedure} long value={form.emergencyRelease.procedure} disabled={edit} onChange={v => set({ emergencyRelease: { ...form.emergencyRelease, procedure: v } })}/>
        <Field id="tow-rel-by" text={t.releaseOrderedBy} value={form.emergencyRelease.orderedBy} disabled={edit} onChange={v => set({ emergencyRelease: { ...form.emergencyRelease, orderedBy: v } })}/>
      </fieldset>
      <fieldset><legend>{t.secContingency}</legend>
        {form.contingencies.map((x, i) => { const up = (patch: Partial<typeof x>) => set({ contingencies: form.contingencies.map(y => y.id === x.id ? { ...y, ...patch } : y) });
          return <div key={x.id}><label htmlFor={`tow-scen-${i}`}>{t.scenario}</label><select id={`tow-scen-${i}`} disabled={edit} value={x.scenario} onChange={e => up({ scenario: e.target.value })}>
            {scenarios.map(v => <option key={v} value={v}>{label(t, 'scen_' + v)}</option>)}</select>
            <label htmlFor={`tow-act-${i}`}>{t.action}</label><input id={`tow-act-${i}`} maxLength={4000} disabled={edit} value={x.action} onChange={e => up({ action: e.target.value })}/>
            <Button type="button" variant="outline" disabled={edit} onClick={() => set({ contingencies: form.contingencies.filter(y => y.id !== x.id) })}>{t.remove}</Button></div>; })}
        <Button type="button" variant="outline" disabled={edit || form.contingencies.length >= 30} onClick={() => set({ contingencies: [...form.contingencies, { id: crypto.randomUUID(), scenario: 'OTHER', action: '' }] })}>{t.add}</Button>
      </fieldset>
      <fieldset><legend>{t.secSnapBack}</legend>
        <label htmlFor="tow-zones">{t.zonesMarked}</label><select id="tow-zones" disabled={edit} value={form.snapBack.zonesMarked === null ? '' : String(form.snapBack.zonesMarked)}
          onChange={e => set({ snapBack: { ...form.snapBack, zonesMarked: e.target.value === '' ? null : e.target.value === 'true' } })}>
          <option value="">{t.unknownOption}</option><option value="true">{t.yes}</option><option value="false">{t.no}</option></select>
        <ItemList id="tow-snap" title={t.control} items={form.snapBack.controls} t={t} disabled={edit} onChange={controls => set({ snapBack: { ...form.snapBack, controls } })}/>
      </fieldset>
      {data.canEdit && <EnterpriseActions layout="form" aria-label={t.save}><Button type="submit" disabled={disabled || !valid}>{p ? t.save : t.create}</Button></EnterpriseActions>}
    </form>
    {p && data.canSubmit && <EnterpriseActions layout="form" aria-label={t.submit}><Button type="button" disabled={disabled} onClick={() => void run(towageApi.submitPlan(vessel, p))}>{t.submit}</Button></EnterpriseActions>}
    {p && data.canDecide && <form aria-label={t.approve} onSubmit={e => e.preventDefault()}>
      <AuthorityPicker id="tow-decision-authority" authorities={data.authorities} value={authority} onChange={setAuthority} t={t} disabled={disabled}/>
      <label htmlFor="tow-decision-reason">{t.reason}</label><textarea id="tow-decision-reason" required minLength={10} maxLength={1000} value={reason} onChange={e => setReason(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.approve}>
        <Button type="button" variant="outline" disabled={disabled || !authority || reason.trim().length < 10} onClick={() => void run(towageApi.decidePlan(vessel, p, 'RETURN', reason.trim(), authority))}>{t.returnPlan}</Button>
        <Button type="button" disabled={disabled || !authority || reason.trim().length < 10} onClick={() => void run(towageApi.decidePlan(vessel, p, 'APPROVE', reason.trim(), authority))}>{t.approve}</Button>
      </EnterpriseActions></form>}
    {p && data.canAmend && <ReasonAction id="tow-amend" text={t.amend} t={t} disabled={disabled} variant="outline" onSubmit={r => void run(towageApi.amendPlan(vessel, p, r))}/>}
    <h2>{t.decisions}</h2>{data.decisions.length === 0 ? <p>{t.noDecisions}</p> :
      <ul>{data.decisions.map(d => <li key={d.id} data-decision={d.decision}>{d.decision === 'APPROVE' ? t.approve : t.returnPlan} · {label(t, 'duty_' + d.duty)} · r{d.revision} · {d.reason} · <time dateTime={d.decidedAt}>{d.decidedAt}</time></li>)}</ul>}
    <h2>{t.revisions}</h2><ul>{data.revisions.map(r => <li key={r.revision}>r{r.revision} · {label(t, 'state_' + r.state)} · <code>{r.reasonCode}</code> · <time dateTime={r.savedAt}>{r.savedAt}</time></li>)}</ul>
    <p><a href={towageHref('towage-risk-briefing', vessel, data.jobId)}>{t.openBriefing}</a></p>
  </section>;
}

function BriefingPanel({ data, vessel, t, disabled, run }: PanelProps & { data: BriefingView }) {
  const b = data.briefing;
  const [form, setForm] = useState<BriefingContent>(() => b?.content ?? emptyBriefing());
  const [authority, setAuthority] = useState(data.authorities.length === 1 ? data.authorities[0].id : ''), [disputeReason, setDisputeReason] = useState('');
  const edit = !data.canEdit || disabled;
  const set = (patch: Partial<BriefingContent>) => setForm(f => ({ ...f, ...patch }));
  const valid = [form.hazards, form.preventiveBarriers, form.mitigatingBarriers, form.stopWorkTriggers].every(itemsValid) && (form.riskAssessmentId === null || isGuid(form.riskAssessmentId)) &&
    form.participants.every(p => isGuid(p.actorId)) && new Set(form.participants.map(p => p.actorId)).size === form.participants.length;
  return <section aria-label={t.screenBriefing}>
    <p>{t.state}: {b ? <StateBadge state={b.state} t={t}/> : <Badge tone="warning">{t.empty}</Badge>}{b && <> · {t.revision} {b.revision}</>}
      {' · '}{t.ackState}: <Badge tone={data.acknowledgementState === 'COMPLETE' ? 'info' : data.acknowledgementState === 'DISPUTED' ? 'danger' : 'warning'} data-ack={data.acknowledgementState}>{label(t, 'ack_' + data.acknowledgementState)}</Badge></p>
    <p>{t.riskState}: <Badge tone={data.risk.state === 'BLOCKED' ? 'danger' : data.risk.state === 'READY' ? 'info' : 'warning'} data-risk={data.risk.state}>{label(t, 'risk_' + data.risk.state)}</Badge> <code>{data.risk.reason}</code>
      {' · '}{t.planState}: {data.planState ? label(t, 'state_' + data.planState) : t.unknownValue}</p>
    <h2>{t.readinessEffect}</h2><ul>{data.readinessChecks.map((c, i) => <li key={`${c.reason}-${i}`} data-result={c.result}>{label(t, 'result_' + c.result)} · <code>{c.reason}</code></li>)}</ul>
    {b && b.state === 'DRAFT' && <><h2>{t.blockers}</h2>{data.blockers.length === 0 ? <p>{t.noBlockers}</p> : <ul>{data.blockers.map(x => <li key={x}><code>{x}</code></li>)}</ul>}</>}
    <form aria-label={t.screenBriefing} onSubmit={e => { e.preventDefault(); if (valid) void run(towageApi.saveBriefing(vessel, data.jobId, b, form)); }}>
      <fieldset><legend>{t.secConditions}</legend>
        <Field id="brf-weather" text={t.weather} long value={form.weather} disabled={edit} onChange={v => set({ weather: v })}/>
        <Field id="brf-current" text={t.current} long value={form.current} disabled={edit} onChange={v => set({ current: v })}/>
        <Field id="brf-visibility" text={t.visibility} long value={form.visibility} disabled={edit} onChange={v => set({ visibility: v })}/>
      </fieldset>
      <ItemList id="brf-hazard" title={t.secHazards} items={form.hazards} t={t} disabled={edit} onChange={hazards => set({ hazards })}/>
      <ItemList id="brf-prev" title={t.secPreventive} items={form.preventiveBarriers} t={t} disabled={edit} onChange={preventiveBarriers => set({ preventiveBarriers })}/>
      <ItemList id="brf-mit" title={t.secMitigating} items={form.mitigatingBarriers} t={t} disabled={edit} onChange={mitigatingBarriers => set({ mitigatingBarriers })}/>
      <fieldset><legend>{t.secStopWork}</legend>
        <Field id="brf-swa" text={t.stopWorkAuthority} long value={form.stopWorkAuthority} disabled={edit} onChange={v => set({ stopWorkAuthority: v })}/>
        <ItemList id="brf-swt" title={t.stopWorkTriggers} items={form.stopWorkTriggers} t={t} disabled={edit} onChange={stopWorkTriggers => set({ stopWorkTriggers })}/>
      </fieldset>
      <fieldset><legend>{t.secEmergency}</legend><Field id="brf-emergency" text={t.secEmergency} long value={form.emergencyArrangements} disabled={edit} onChange={v => set({ emergencyArrangements: v })}/></fieldset>
      <Field id="brf-risk" text={t.riskAssessment} value={form.riskAssessmentId} disabled={edit} onChange={v => set({ riskAssessmentId: v })}/>
      <fieldset><legend>{t.secParticipants}</legend>
        {form.participants.map((p, i) => { const up = (patch: Partial<typeof p>) => set({ participants: form.participants.map((y, j) => j === i ? { ...y, ...patch } : y) });
          return <div key={i}><Field id={`brf-actor-${i}`} text={t.actor} value={p.actorId} disabled={edit} onChange={v => up({ actorId: v ?? '' })}/>
            <label htmlFor={`brf-role-${i}`}>{t.role}</label><select id={`brf-role-${i}`} disabled={edit} value={p.role} onChange={e => up({ role: e.target.value })}>
              {participantRoles.map(r => <option key={r} value={r}>{label(t, 'role_' + r)}</option>)}</select>
            <Button type="button" variant="outline" disabled={edit} onClick={() => set({ participants: form.participants.filter((_, j) => j !== i) })}>{t.remove}</Button></div>; })}
        <Button type="button" variant="outline" disabled={edit || form.participants.length >= 50} onClick={() => set({ participants: [...form.participants, { actorId: '', role: 'DECK_CREW', tugId: vessel }] })}>{t.add}</Button>
      </fieldset>
      {data.canEdit && <EnterpriseActions layout="form" aria-label={t.save}><Button type="submit" disabled={disabled || !valid}>{b ? t.save : t.create}</Button></EnterpriseActions>}
    </form>
    {b && data.canIssue && <form aria-label={t.issue} onSubmit={e => { e.preventDefault(); if (authority) void run(towageApi.issueBriefing(vessel, b, authority)); }}>
      <AuthorityPicker id="brf-issue-authority" authorities={data.authorities} value={authority} onChange={setAuthority} t={t} disabled={disabled}/>
      <EnterpriseActions layout="form" aria-label={t.issue}><Button type="submit" disabled={disabled || !authority}>{t.issue}</Button></EnterpriseActions></form>}
    {b && data.canAcknowledge && <form aria-label={t.acknowledge} onSubmit={e => e.preventDefault()}>
      <label htmlFor="brf-dispute-reason">{t.reason}</label><textarea id="brf-dispute-reason" maxLength={1000} value={disputeReason} onChange={e => setDisputeReason(e.target.value)}/>
      <EnterpriseActions layout="form" aria-label={t.acknowledge}>
        <Button type="button" variant="outline" disabled={disabled || disputeReason.trim().length < 10} onClick={() => void run(towageApi.acknowledge(vessel, b, 'DISPUTE', disputeReason.trim()))}>{t.dispute}</Button>
        <Button type="button" disabled={disabled} onClick={() => void run(towageApi.acknowledge(vessel, b, 'ACK', null))}>{t.acknowledge}</Button>
      </EnterpriseActions></form>}
    {b && data.canAmend && <ReasonAction id="brf-amend" text={t.amend} t={t} disabled={disabled} variant="outline" onSubmit={r => void run(towageApi.amendBriefing(vessel, b, r))}/>}
    <h2>{t.acknowledgements}</h2>{data.acknowledgements.length === 0 ? <p>{t.empty}</p> :
      <ul>{data.acknowledgements.map(k => <li key={`${k.actorId}-${k.revision}`} data-choice={k.choice}>{k.choice === 'ACK' ? t.acknowledge : t.dispute} · r{k.revision} · {k.actorId.slice(0, 8)}{k.reason ? ` · ${k.reason}` : ''}</li>)}</ul>}
    <h2>{t.revisions}</h2><ul>{data.revisions.map(r => <li key={r.revision}>r{r.revision} · {label(t, 'state_' + r.state)} · <code>{r.reasonCode}</code></li>)}</ul>
    <p><a href={towageHref('towage-plan', vessel, data.jobId)}>{t.openPlan}</a></p>
  </section>;
}
