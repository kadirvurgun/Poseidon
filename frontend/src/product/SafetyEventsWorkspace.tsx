import { useEffect, useRef, useState } from 'react';
import { localizedCopy } from '../i18n/messages';
import { useAppLanguage, type Locale as AppLocale } from '../i18n';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { ApiError, session, vessels as listVessels, type Session, type Vessel } from './api';
import { isGuid } from './identifiers';
import type { Command } from './tug-readiness-api';
import {
  barrierFunctions, barrierPerformances, categories, contexts, decodeEvent, emptyDraft, emptyInvestigation, factorCategories, factorKinds, identityPurposes, missingFields,
  precisions, safetyApi, severities, verifications, yesNoUnknown, type DraftFields, type FollowUp, type Identity, type InvestigationContent, type Page, type SafetyAction,
  type SafetyEvent,
} from './safety-events-api';
import { projectSafetyQueue, SafetyEventOfflineApplication, type SafetyPending } from './safety-event-offline';
import './tug.css';

const copy = localizedCopy('SafetyEvents_tsx_copy');
type Copy = (typeof copy)[AppLocale];
const label = (t: Copy, key: string) => (t as Record<string, string>)[key] ?? key;

// /app/safety-events?vessel=<id>[&event=<id> | &new=1]. Unknown or malformed parameters are refused.
export function safetyRoute(href: string) {
  const url = new URL(href, 'http://local'), p = url.searchParams, value = (k: string) => p.get(k) ?? '';
  const invalid = !/^\/app\/safety-events\/?$/.test(url.pathname) || [...p.keys()].some(k => !['vessel', 'event', 'new'].includes(k) || p.getAll(k).length !== 1) ||
    ['vessel', 'event'].some(k => value(k) !== '' && !isGuid(value(k))) || value('new') !== '' && value('new') !== '1' || value('event') !== '' && value('new') !== '';
  return { vessel: value('vessel'), event: value('event'), creating: value('new') === '1', invalid };
}
const routeHref = (r: { vessel: string; event?: string; creating?: boolean }) =>
  `/app/safety-events?vessel=${r.vessel}${r.event ? `&event=${r.event}` : ''}${r.creating ? '&new=1' : ''}`;

// Exact retained command: an uncertain outcome is retried with the SAME key and body.
type Failure = { status: number; code: string; missing: string[] };
async function sendCommand(command: Command, csrf: string, signal: AbortSignal): Promise<unknown> {
  const response = await fetch(`/api/v1/${command.path}`, { credentials: 'same-origin', signal, method: command.method,
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: JSON.stringify(command.body) });
  let body: unknown = null;
  try { body = await response.json(); } catch { /* Proxy errors need not be JSON. */ }
  if (!response.ok) {
    const f = body && typeof body === 'object' ? body as Record<string, unknown> : {};
    throw Object.assign(new ApiError(response.status, typeof f.code === 'string' ? f.code : 'request_failed'), { missing: Array.isArray(f.missing) ? f.missing.filter(x => typeof x === 'string') : [] });
  }
  return body;
}

function Tone({ value, t, prefix }: { value: string; t: Copy; prefix: string }) {
  const tone = ['CRITICAL', 'HIGH', 'NOT_EFFECTIVE', 'BLOCKERS_PRESENT'].includes(value) ? 'danger'
    : ['UNASSESSED', 'UNKNOWN', 'PENDING', 'INCONCLUSIVE', 'REVIEW_REQUIRED', 'SUBMITTED', 'RETURNED'].includes(value) ? 'warning'
      : ['EFFECTIVE'].includes(value) ? 'info' : 'neutral';
  return <Badge tone={tone} data-state={value}>{label(t, prefix + value)}</Badge>;
}

function Select({ id, value, options, onChange, t, prefix, disabled, empty = true }: { id: string; value: string | null; options: readonly string[]; onChange: (v: string | null) => void
  t: Copy; prefix: string; disabled?: boolean; empty?: boolean }) {
  return <select id={id} value={value ?? ''} disabled={disabled} onChange={e => onChange(e.target.value || null)}>
    {empty && <option value="">{t.notSelected}</option>}{options.map(o => <option key={o} value={o}>{label(t, prefix + o)}</option>)}</select>;
}

export function SafetyEventsWorkspace() {
  const [lang, language] = useAppLanguage(), t = copy[lang];
  const [route, setRoute] = useState(() => safetyRoute(window.location.href));
  const [user, setUser] = useState<Session | null>(null), [vesselOptions, setVesselOptions] = useState<Vessel[]>([]);
  const [page, setPage] = useState<Page | null>(null), [event, setEvent] = useState<SafetyEvent | null>(null), [mine, setMine] = useState(false);
  const [loading, setLoading] = useState(true), [error, setError] = useState(0), [offline, setOffline] = useState(!navigator.onLine), [reload, setReload] = useState(0);
  const [busy, setBusy] = useState(false), [failure, setFailure] = useState<Failure | null>(null), [pending, setPending] = useState<Command | null>(null);
  const [secret, setSecret] = useState<string | null>(null), [loadedAt, setLoadedAt] = useState(() => Date.now()), [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const on = () => setOffline(false), off = () => setOffline(true), pop = () => setRoute(safetyRoute(window.location.href));
    addEventListener('online', on); addEventListener('offline', off); addEventListener('popstate', pop);
    const timer = setInterval(() => setNow(Date.now()), 60000);
    return () => { removeEventListener('online', on); removeEventListener('offline', off); removeEventListener('popstate', pop); clearInterval(timer); };
  }, []);
  useEffect(() => {
    const c = new AbortController();
    (async () => {
      await Promise.resolve(); if (c.signal.aborted) return;
      setLoading(true); setError(0);
      if (route.invalid) throw new ApiError(400, 'invalid_route');
      setUser(await session(c.signal));
      if (!route.vessel) { setVesselOptions((await listVessels(0, '', c.signal)).items); setPage(null); setEvent(null); return; }
      setPage(await safetyApi.list(route.vessel, { mine }, c.signal));
      setEvent(route.event ? await safetyApi.detail(route.vessel, route.event, c.signal) : null);
      setLoadedAt(Date.now());
    })().then(() => { if (!c.signal.aborted) setLoading(false); }).catch(e => { if (c.signal.aborted) return; setError(e instanceof ApiError ? e.status : 500); setLoading(false); });
    return () => c.abort();
  }, [route, reload, mine]);
  const go = (next: { vessel: string; event?: string; creating?: boolean }) => { history.pushState(null, '', routeHref(next)); setRoute(safetyRoute(window.location.href)); setFailure(null); setSecret(null); };
  const stale = now - loadedAt > 15 * 60000;

  // Runs one command; on success the canonical event id is put in the URL before any further read.
  const run = async (command: Command, after?: (value: unknown) => void) => {
    if (!user?.csrfToken || !route.vessel) return;
    setBusy(true); setFailure(null); setPending(command);
    try {
      const value = await sendCommand(command, user.csrfToken, new AbortController().signal);
      setPending(null);
      if (after) { after(value); return; }
      const saved = decodeEvent(value, route.vessel);
      if (saved.followUpSecret) setSecret(saved.followUpSecret);
      setEvent(saved);
      if (route.event !== saved.id) { history.pushState(null, '', routeHref({ vessel: route.vessel, event: saved.id })); setRoute(safetyRoute(window.location.href)); }
    } catch (e) {
      // A definite server answer releases the command; a network failure keeps it for an exact retry.
      if (e instanceof ApiError) { setPending(null); setFailure({ status: e.status, code: e.code, missing: (e as ApiError & { missing?: string[] }).missing ?? [] }); }
      else setFailure({ status: 0, code: 'network', missing: [] });
    } finally { setBusy(false); }
  };

  const can = page?.capabilities;
  return <div className="product-shell enterprise-shell tug-workspace safety-events">
    <EnterpriseChrome session={user} vessel={route.vessel || null} language={lang} onLanguageChange={language} onNavigate={h => window.location.assign(h)} title={t.title} mainId="safety-main"/>
    <main id="safety-main" tabIndex={-1} aria-busy={loading || busy}>
      <div className="enterprise-page-heading"><div><h1>{t.title}</h1></div></div>
      <p className="tug-planning-boundary">{t.boundary}</p>
      {offline && <p role="alert">{t.offline}</p>}
      {stale && !loading && <p role="status">{t.stale} <Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.reload}</Button></p>}
      {loading && <p role="status">{t.loading}</p>}
      {!loading && error > 0 && <div role="alert"><p>{[401, 403].includes(error) ? t.denied : error === 404 ? t.notFound : error === 400 ? t.invalidRoute : t.unavailable}</p>
        <Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>}
      {failure && <div role="alert" data-code={failure.code}>
        <p>{failure.status === 409 ? t.conflict : [401, 403].includes(failure.status) ? t.forbidden : failure.status === 422 ? t.blocked : failure.status === 0 ? t.uncertain : t.failed}
          {' '}<code>{failure.code}</code></p>
        {failure.missing.length > 0 && <ul aria-label={t.missingHeading}>{failure.missing.map(m => <li key={m}>{label(t, 'f_' + m.split(':')[0])}{m.includes(':') ? ` (${m.split(':')[1].slice(0, 8)})` : ''}</li>)}</ul>}
        {failure.status === 409 && <Button type="button" variant="outline" onClick={() => setReload(n => n + 1)}>{t.reload}</Button>}
        {failure.status === 0 && pending && <Button type="button" variant="outline" disabled={busy || offline} onClick={() => void run(pending)}>{t.retryExact}</Button>}
      </div>}
      {!loading && !error && !route.vessel && <section aria-label={t.chooseVessel}>
        <h2>{t.chooseVessel}</h2>
        {vesselOptions.length === 0 ? <p role="status">{t.noVessels}</p> :
          <ul>{vesselOptions.map(v => <li key={v.id}><a href={routeHref({ vessel: v.id })} onClick={e => { e.preventDefault(); go({ vessel: v.id }); }}>{v.code} · {v.name}</a></li>)}</ul>}
      </section>}
      {!loading && !error && page && can && <>
        <BlockerSummary page={page} t={t}/>
        {secret && <section role="alert" aria-label={t.secretHeading} className="safety-secret"><h2>{t.secretHeading}</h2><p>{t.secretBody}</p><code data-testid="follow-up-secret">{secret}</code>
          <Button type="button" variant="outline" onClick={() => setSecret(null)}>{t.secretSaved}</Button></section>}
        {route.creating ? <CaptureForm t={t} busy={busy || offline} onCreate={(mode, fields, contact) => void run(safetyApi.create(route.vessel, crypto.randomUUID(), mode, fields, contact))}/>
          : event ? <EventDetail key={`${event.id}:${event.version}:${event.investigation?.version ?? 0}:${event.actions.map(a => a.revision).join('.')}`} t={t} vessel={route.vessel} event={event} busy={busy || offline} run={run} back={() => go({ vessel: route.vessel })}/>
            : <EventList t={t} page={page} mine={mine} setMine={setMine} open={id => go({ vessel: route.vessel, event: id })}
              create={() => go({ vessel: route.vessel, creating: true })}/>}
        {!event && !route.creating && can.canReport && <FollowUpPanel t={t} csrf={user?.csrfToken ?? null} busy={busy || offline}/>}
        {!event && can.canReport && <OfflinePanel t={t} vessel={route.vessel} available={can.offline.available} online={!offline} onSynced={() => setReload(n => n + 1)}/>}
      </>}
    </main></div>;
}

function BlockerSummary({ page, t }: { page: Page; t: Copy }) {
  const b = page.blockers;
  return <section aria-label={t.blockersHeading} className="safety-blockers">
    <h2>{t.blockersHeading} <Tone value={b.state} t={t} prefix="b_"/></h2>
    {b.items.length > 0 && <ul>{b.items.map(i => <li key={i.eventId} data-blocking={i.blocking}>{i.eventNo} · {label(t, 'r_' + i.reason)}{i.severity && <> · <Tone value={i.severity} t={t} prefix="sev_"/></>}</li>)}</ul>}
    <p><small>{t.coverage}: {b.coverage.map(c => <code key={c}>{c} </code>)}</small></p>
  </section>;
}

function EventList({ t, page, mine, setMine, open, create }: { t: Copy; page: Page; mine: boolean; setMine: (v: boolean) => void; open: (id: string) => void; create: () => void }) {
  return <section aria-label={t.listHeading}>
    <h2>{t.listHeading}</h2>
    <div className="safety-toolbar">
      {page.capabilities.canReport && <Button type="button" onClick={create}>{t.report}</Button>}
      {page.capabilities.canRead && <label><input type="checkbox" checked={mine} onChange={e => setMine(e.target.checked)}/> {t.mineOnly}</label>}
    </div>
    {page.items.length === 0 ? <p role="status">{t.empty}</p> :
      <table><thead><tr>{[t.colNo, t.colCategory, t.colSummary, t.colOccurred, t.colSeverity, t.colState].map(h => <th key={h} scope="col">{h}</th>)}</tr></thead>
        <tbody>{page.items.map(i => <tr key={i.id} data-mine={i.mine}>
          <td><a href={`?event=${i.id}`} onClick={e => { e.preventDefault(); open(i.id); }}>{i.eventNo}</a>{i.reportingMode === 'CONFIDENTIAL' && <> <Badge tone="neutral">{t.m_CONFIDENTIAL}</Badge></>}
            {i.origin === 'OFFLINE' && <> <Badge tone="neutral">{t.originOffline}</Badge></>}</td>
          <td>{i.category ? label(t, 'cat_' + i.category) : '—'}</td><td>{i.summary ?? '—'}</td>
          <td>{i.occurredAt ? <time dateTime={i.occurredAt}>{i.occurredAt}</time> : i.occurrenceDate ?? (i.occurrencePrecision ? label(t, 'p_' + i.occurrencePrecision) : '—')}</td>
          <td>{i.severity ? <Tone value={i.severity} t={t} prefix="sev_"/> : t.severityHidden}</td><td><Tone value={i.state} t={t} prefix="s_"/></td></tr>)}</tbody></table>}
  </section>;
}

function DraftEditor({ t, value, onChange, disabled }: { t: Copy; value: DraftFields; onChange: (v: DraftFields) => void; disabled: boolean }) {
  const set = (patch: Partial<DraftFields>) => onChange({ ...value, ...patch });
  const exactLocal = value.occurredAt ? new Date(value.occurredAt).toISOString().slice(0, 16) : '';
  return <fieldset disabled={disabled} className="safety-fields">
    <label htmlFor="se-category">{t.f_category}</label><Select id="se-category" value={value.category} options={categories} onChange={v => set({ category: v })} t={t} prefix="cat_"/>
    <label htmlFor="se-precision">{t.f_occurrence_precision}</label>
    <Select id="se-precision" value={value.occurrencePrecision} options={precisions} t={t} prefix="p_"
      onChange={v => set({ occurrencePrecision: v, occurredAt: v === 'EXACT' ? value.occurredAt : null, occurrenceDate: v === 'DATE_ONLY' ? value.occurrenceDate : null, occurrenceUnknownReason: v === 'UNKNOWN' ? value.occurrenceUnknownReason : null })}/>
    {value.occurrencePrecision === 'EXACT' && <><label htmlFor="se-at">{t.f_occurred_at}</label>
      <input id="se-at" type="datetime-local" value={exactLocal} onChange={e => set({ occurredAt: e.target.value ? new Date(e.target.value + 'Z').toISOString() : null })}/><small>{t.utcHint}</small></>}
    {value.occurrencePrecision === 'DATE_ONLY' && <><label htmlFor="se-date">{t.f_occurrence_date}</label>
      <input id="se-date" type="date" value={value.occurrenceDate ?? ''} onChange={e => set({ occurrenceDate: e.target.value || null })}/></>}
    {value.occurrencePrecision === 'UNKNOWN' && <><label htmlFor="se-unknown">{t.f_occurrence_unknown_reason}</label>
      <textarea id="se-unknown" value={value.occurrenceUnknownReason ?? ''} maxLength={1000} onChange={e => set({ occurrenceUnknownReason: e.target.value || null })}/></>}
    <label htmlFor="se-context">{t.f_operation_context}</label><Select id="se-context" value={value.operationContext} options={contexts} onChange={v => set({ operationContext: v })} t={t} prefix="ctx_"/>
    <label htmlFor="se-location">{t.f_location}</label><input id="se-location" value={value.locationText ?? ''} maxLength={500} onChange={e => set({ locationText: e.target.value || null })}/>
    <label htmlFor="se-summary">{t.f_summary}</label><input id="se-summary" value={value.summary ?? ''} maxLength={500} onChange={e => set({ summary: e.target.value || null })}/>
    <label htmlFor="se-narrative">{t.f_narrative}</label><textarea id="se-narrative" value={value.narrative ?? ''} maxLength={20000} onChange={e => set({ narrative: e.target.value || null })}/>
    <label htmlFor="se-ia">{t.f_immediate_action_taken}</label>
    <Select id="se-ia" value={value.immediateActionTaken} options={yesNoUnknown} t={t} prefix="yn_" onChange={v => set({ immediateActionTaken: v, immediateActionText: v === 'NO' ? null : value.immediateActionText })}/>
    {value.immediateActionTaken !== 'NO' && <><label htmlFor="se-ia-text">{t.f_immediate_action_text}</label>
      <textarea id="se-ia-text" value={value.immediateActionText ?? ''} maxLength={4000} onChange={e => set({ immediateActionText: e.target.value || null })}/></>}
    <label htmlFor="se-claim">{t.f_severity_claim}</label><Select id="se-claim" value={value.severityClaim} options={severities} onChange={v => set({ severityClaim: v })} t={t} prefix="sev_"/>
    <small>{t.claimHint}</small>
  </fieldset>;
}

function CaptureForm({ t, busy, onCreate }: { t: Copy; busy: boolean; onCreate: (mode: string, fields: DraftFields, contact: string | null) => void }) {
  const [fields, setFields] = useState(emptyDraft()), [mode, setMode] = useState<'NAMED' | 'CONFIDENTIAL'>('NAMED'), [contact, setContact] = useState('IN_APP_FOLLOW_UP');
  const missing = missingFields(fields);
  return <form aria-label={t.captureHeading} onSubmit={e => { e.preventDefault(); onCreate(mode, fields, mode === 'CONFIDENTIAL' ? contact : null); }}>
    <h2>{t.captureHeading}</h2>
    <fieldset disabled={busy}><legend>{t.modeLegend}</legend>
      <label><input type="radio" name="se-mode" checked={mode === 'NAMED'} onChange={() => setMode('NAMED')}/> {t.m_NAMED}</label>
      <label><input type="radio" name="se-mode" checked={mode === 'CONFIDENTIAL'} onChange={() => setMode('CONFIDENTIAL')}/> {t.m_CONFIDENTIAL}</label>
      <p><small>{mode === 'CONFIDENTIAL' ? t.confidentialHint : t.namedHint} {t.anonymousUnavailable}</small></p>
      {mode === 'CONFIDENTIAL' && <><label htmlFor="se-contact">{t.contactLegend}</label>
        <select id="se-contact" value={contact} onChange={e => setContact(e.target.value)}><option value="IN_APP_FOLLOW_UP">{t.contact_IN_APP_FOLLOW_UP}</option><option value="NONE">{t.contact_NONE}</option></select></>}
    </fieldset>
    <DraftEditor t={t} value={fields} onChange={setFields} disabled={busy}/>
    {missing.length > 0 && <p role="note">{t.draftIncomplete}: {missing.map(m => label(t, 'f_' + m)).join(', ')}</p>}
    <Button type="submit" disabled={busy}>{t.saveDraft}</Button>
  </form>;
}

type Run = (command: Command, after?: (value: unknown) => void) => Promise<void>;
function Reason({ id, labelText, value, onChange }: { id: string; labelText: string; value: string; onChange: (v: string) => void }) {
  return <><label htmlFor={id}>{labelText}</label><textarea id={id} value={value} maxLength={2000} onChange={e => onChange(e.target.value)}/></>;
}

function EventDetail({ t, vessel, event, busy, run, back }: { t: Copy; vessel: string; event: SafetyEvent; busy: boolean; run: Run; back: () => void }) {
  const [fields, setFields] = useState(event.fields), [reason, setReason] = useState('');
  const ok = (r: string) => r.trim().length >= 10;
  return <article aria-label={event.eventNo} data-state={event.state}>
    <p><Button type="button" variant="ghost" onClick={back}>{t.back}</Button></p>
    <h2>{event.eventNo} <Tone value={event.state} t={t} prefix="s_"/></h2>
    <dl className="safety-facts">
      <dt>{t.reporter}</dt><dd data-testid="reporter">{event.reportingMode === 'CONFIDENTIAL' ? (event.reporter.isCurrentActor ? t.reporterYouProtected : t.reporterProtected)
        : event.reporter.isCurrentActor ? t.reporterYou : <code>{event.reporter.actorId?.slice(0, 8)}</code>}</dd>
      <dt>{t.colSeverity}</dt><dd>{event.severity ? <Tone value={event.severity} t={t} prefix="sev_"/> : t.notTriaged} <small>{t.severityPolicy}: {label(t, 'pol_' + event.severityPolicy)}</small></dd>
      <dt>{t.claim}</dt><dd>{event.severityClaim ? label(t, 'sev_' + event.severityClaim) : '—'}</dd>
      <dt>{t.investigationRequired}</dt><dd>{event.investigationRequired ? label(t, 'yn_' + event.investigationRequired) : '—'}</dd>
      <dt>{t.effectiveness}</dt><dd><Tone value={event.effectiveness} t={t} prefix="e_"/></dd>
      <dt>{t.closure}</dt><dd>{event.state === 'CLOSED' ? <><Tone value="CLOSED" t={t} prefix="s_"/> {event.closedAt && <time dateTime={event.closedAt}>{event.closedAt}</time>}</> : t.notClosed}</dd>
    </dl>
    <p><small>{t.closedNotEffective}</small></p>
    {event.can.edit ? <form aria-label={t.editHeading} onSubmit={e => { e.preventDefault(); void run(safetyApi.update(vessel, event, fields)); }}>
      <DraftEditor t={t} value={fields} onChange={setFields} disabled={busy}/>
      {event.missing.length > 0 && <p role="note">{t.draftIncomplete}: {event.missing.map(m => label(t, 'f_' + m)).join(', ')}</p>}
      <Button type="submit" disabled={busy}>{t.saveDraft}</Button>{' '}
      <Button type="button" disabled={busy || !event.can.submit || JSON.stringify(fields) !== JSON.stringify(event.fields)} onClick={() => void run(safetyApi.submit(vessel, event))}>{t.submit}</Button>
      {event.can.withdraw && <><Reason id="se-withdraw" labelText={t.withdrawReason} value={reason} onChange={setReason}/>
        <Button type="button" variant="destructive" disabled={busy || !ok(reason)} onClick={() => void run(safetyApi.withdraw(vessel, event, reason))}>{t.withdraw}</Button></>}
    </form> : <dl className="safety-facts">
      <dt>{t.f_category}</dt><dd>{event.fields.category ? label(t, 'cat_' + event.fields.category) : '—'}</dd>
      <dt>{t.f_occurrence_precision}</dt><dd>{event.fields.occurrencePrecision ? label(t, 'p_' + event.fields.occurrencePrecision) : '—'} {event.fields.occurredAt ?? event.fields.occurrenceDate ?? event.fields.occurrenceUnknownReason ?? ''}</dd>
      <dt>{t.f_operation_context}</dt><dd>{event.fields.operationContext ? label(t, 'ctx_' + event.fields.operationContext) : '—'}</dd>
      <dt>{t.f_location}</dt><dd>{event.fields.locationText ?? '—'}</dd>
      <dt>{t.f_summary}</dt><dd>{event.fields.summary ?? '—'}</dd>
      <dt>{t.f_narrative}</dt><dd className="safety-narrative">{event.fields.narrative ?? '—'}</dd>
      <dt>{t.f_immediate_action_taken}</dt><dd>{event.fields.immediateActionTaken ? label(t, 'yn_' + event.fields.immediateActionTaken) : '—'} {event.fields.immediateActionText ?? ''}</dd>
    </dl>}
    {event.can.triage && <TriagePanel t={t} vessel={vessel} event={event} busy={busy} run={run}/>}
    {event.triages.length > 0 && <section aria-label={t.triageHistory}><h3>{t.triageHistory}</h3><ul>{event.triages.map(x => <li key={x.id}>{label(t, 'td_' + x.decision)}
      {x.severity && <> · <Tone value={x.severity} t={t} prefix="sev_"/></>} · {x.reason} · <time dateTime={x.decidedAt}>{x.decidedAt}</time></li>)}</ul></section>}
    {event.can.readIdentity && <IdentityPanel t={t} vessel={vessel} event={event} busy={busy} run={run}/>}
    {(event.investigation || event.can.investigate) && <InvestigationPanel t={t} vessel={vessel} event={event} busy={busy} run={run}/>}
    {(event.actions.length > 0 || event.can.createAction) && <ActionsPanel t={t} vessel={vessel} event={event} busy={busy} run={run}/>}
    {['TRIAGED', 'ACTION_TRACKING'].includes(event.state) && <ClosurePanel t={t} vessel={vessel} event={event} busy={busy} run={run}/>}
    <MessagesPanel t={t} vessel={vessel} event={event} busy={busy} run={run}/>
  </article>;
}

function TriagePanel({ t, vessel, event, busy, run }: { t: Copy; vessel: string; event: SafetyEvent; busy: boolean; run: Run }) {
  const [severity, setSeverity] = useState<string | null>(event.severity), [need, setNeed] = useState<string | null>(event.investigationRequired), [reason, setReason] = useState('');
  return <section aria-label={t.triageHeading}><h3>{t.triageHeading}</h3><p><small>{t.triageHint}</small></p>
    <label htmlFor="se-sev">{t.colSeverity}</label><Select id="se-sev" value={severity} options={severities} onChange={setSeverity} t={t} prefix="sev_" disabled={busy}/>
    <label htmlFor="se-need">{t.investigationRequired}</label><Select id="se-need" value={need} options={yesNoUnknown} onChange={setNeed} t={t} prefix="yn_" disabled={busy}/>
    <Reason id="se-triage-reason" labelText={t.reason} value={reason} onChange={setReason}/>
    <Button type="button" disabled={busy || !severity || !need || reason.trim().length < 10} onClick={() => void run(safetyApi.triage(vessel, event, 'TRIAGE', severity, need, reason))}>{t.triage}</Button>{' '}
    {event.state === 'SUBMITTED' && <Button type="button" variant="outline" disabled={busy || reason.trim().length < 10} onClick={() => void run(safetyApi.triage(vessel, event, 'RETURN', null, null, reason))}>{t.returnToReporter}</Button>}
  </section>;
}

function IdentityPanel({ t, vessel, event, busy, run }: { t: Copy; vessel: string; event: SafetyEvent; busy: boolean; run: Run }) {
  const [purpose, setPurpose] = useState<string | null>(null), [reason, setReason] = useState(''), [identity, setIdentity] = useState<Identity | null>(null);
  return <section aria-label={t.identityHeading}><h3>{t.identityHeading}</h3><p><small>{t.identityHint}</small></p>
    <label htmlFor="se-purpose">{t.purpose}</label><Select id="se-purpose" value={purpose} options={identityPurposes} onChange={setPurpose} t={t} prefix="pu_" disabled={busy}/>
    <Reason id="se-identity-reason" labelText={t.reason} value={reason} onChange={setReason}/>
    <Button type="button" disabled={busy || !purpose || reason.trim().length < 10} onClick={() => void run(safetyApi.identity(vessel, event, purpose!, reason), v => setIdentity(v as Identity))}>{t.revealIdentity}</Button>
    {identity && <p role="status" data-testid="identity">{t.reporter}: <code>{identity.reporterActorId}</code> · {label(t, 'contact_' + identity.contactPreference)}</p>}
  </section>;
}

const newId = () => crypto.randomUUID();
function InvestigationPanel({ t, vessel, event, busy, run }: { t: Copy; vessel: string; event: SafetyEvent; busy: boolean; run: Run }) {
  const inv = event.investigation, editable = inv ? inv.canEdit : event.can.investigate;
  const [content, setContent] = useState<InvestigationContent>(inv?.content ?? emptyInvestigation()), [reason, setReason] = useState('');
  const set = (patch: Partial<InvestigationContent>) => setContent({ ...content, ...patch });
  const dirty = JSON.stringify(content) !== JSON.stringify(inv?.content ?? emptyInvestigation());
  return <section aria-label={t.investigationHeading}><h3>{t.investigationHeading} {inv && <Badge tone={inv.state === 'APPROVED' ? 'info' : 'warning'}>{label(t, 'is_' + inv.state)}</Badge>}</h3>
    <p><small>{t.rootCauseHint}</small></p>
    <fieldset disabled={busy || !editable}>
      <label htmlFor="si-scope">{t.scope}</label><textarea id="si-scope" value={content.scope ?? ''} maxLength={4000} onChange={e => set({ scope: e.target.value || null })}/>
      <h4>{t.factors}</h4>
      {content.factors.map((f, i) => <div key={f.id} className="safety-row">
        <Select id={`si-fk-${i}`} value={f.kind} options={factorKinds} t={t} prefix="fk_" empty={false} onChange={v => set({ factors: content.factors.map(x => x.id === f.id ? { ...x, kind: v ?? 'FACT' } : x) })}/>
        <Select id={`si-fc-${i}`} value={f.category} options={factorCategories} t={t} prefix="fc_" empty={false} onChange={v => set({ factors: content.factors.map(x => x.id === f.id ? { ...x, category: v ?? 'UNKNOWN' } : x) })}/>
        <input aria-label={`${t.statement} ${i + 1}`} value={f.statement} maxLength={4000} onChange={e => set({ factors: content.factors.map(x => x.id === f.id ? { ...x, statement: e.target.value } : x) })}/>
        <Button type="button" variant="ghost" onClick={() => set({ factors: content.factors.filter(x => x.id !== f.id), rootCauses: content.rootCauses.map(r => ({ ...r, factorIds: r.factorIds.filter(id => id !== f.id) })) })}>{t.remove}</Button></div>)}
      <Button type="button" variant="outline" onClick={() => set({ factors: [...content.factors, { id: newId(), kind: 'FACT', category: 'UNKNOWN', statement: '' }] })}>{t.addFactor}</Button>
      <h4>{t.rootCauses}</h4>
      {content.rootCauses.map((r, i) => <div key={r.id} className="safety-row">
        <input aria-label={`${t.rootCause} ${i + 1}`} value={r.statement} maxLength={4000} onChange={e => set({ rootCauses: content.rootCauses.map(x => x.id === r.id ? { ...x, statement: e.target.value } : x) })}/>
        <Select id={`si-rv-${i}`} value={r.verification} options={verifications} t={t} prefix="v_" empty={false} onChange={v => set({ rootCauses: content.rootCauses.map(x => x.id === r.id ? { ...x, verification: v ?? 'UNVERIFIED' } : x) })}/>
        <fieldset><legend>{t.linkedFactors}</legend>{content.factors.map(f => <label key={f.id}><input type="checkbox" checked={r.factorIds.includes(f.id)}
          onChange={e => set({ rootCauses: content.rootCauses.map(x => x.id === r.id ? { ...x, factorIds: e.target.checked ? [...x.factorIds, f.id] : x.factorIds.filter(id => id !== f.id) } : x) })}/> {f.statement.slice(0, 40) || '—'}</label>)}</fieldset>
        <Button type="button" variant="ghost" onClick={() => set({ rootCauses: content.rootCauses.filter(x => x.id !== r.id) })}>{t.remove}</Button></div>)}
      <Button type="button" variant="outline" onClick={() => set({ rootCauses: [...content.rootCauses, { id: newId(), statement: '', factorIds: [], verification: 'UNVERIFIED' }] })}>{t.addRootCause}</Button>
      <h4>{t.barriers}</h4>
      {content.barriers.map((b, i) => <div key={b.id} className="safety-row">
        <input aria-label={`${t.barrier} ${i + 1}`} value={b.name} maxLength={200} onChange={e => set({ barriers: content.barriers.map(x => x.id === b.id ? { ...x, name: e.target.value } : x) })}/>
        <Select id={`si-bf-${i}`} value={b.function} options={barrierFunctions} t={t} prefix="bf_" empty={false} onChange={v => set({ barriers: content.barriers.map(x => x.id === b.id ? { ...x, function: v ?? 'UNKNOWN' } : x) })}/>
        <Select id={`si-bp-${i}`} value={b.performance} options={barrierPerformances} t={t} prefix="bp_" empty={false} onChange={v => set({ barriers: content.barriers.map(x => x.id === b.id ? { ...x, performance: v ?? 'UNKNOWN' } : x) })}/>
        <Button type="button" variant="ghost" onClick={() => set({ barriers: content.barriers.filter(x => x.id !== b.id) })}>{t.remove}</Button></div>)}
      <Button type="button" variant="outline" onClick={() => set({ barriers: [...content.barriers, { id: newId(), name: '', function: 'UNKNOWN', performance: 'UNKNOWN', riskBarrierRef: null }] })}>{t.addBarrier}</Button>
      <h4>{t.uncertainties}</h4>
      {content.uncertainties.map((u, i) => <div key={i} className="safety-row"><input aria-label={`${t.uncertainty} ${i + 1}`} value={u} maxLength={4000}
        onChange={e => set({ uncertainties: content.uncertainties.map((x, j) => j === i ? e.target.value : x) })}/>
        <Button type="button" variant="ghost" onClick={() => set({ uncertainties: content.uncertainties.filter((_, j) => j !== i) })}>{t.remove}</Button></div>)}
      <Button type="button" variant="outline" onClick={() => set({ uncertainties: [...content.uncertainties, ''] })}>{t.addUncertainty}</Button>
    </fieldset>
    {inv && inv.missing.length > 0 && <p role="note">{t.draftIncomplete}: {inv.missing.map(m => label(t, 'f_' + m)).join(', ')}</p>}
    {editable && <p><Button type="button" disabled={busy || !dirty} onClick={() => void run(safetyApi.saveInvestigation(vessel, event, content))}>{inv ? t.saveInvestigation : t.openInvestigation}</Button>{' '}
      {inv && <Button type="button" variant="outline" disabled={busy || dirty || !inv.canSubmit} onClick={() => void run(safetyApi.submitInvestigation(vessel, event))}>{t.submitInvestigation}</Button>}</p>}
    {inv?.canReview && <div aria-label={t.reviewHeading}><h4>{t.reviewHeading}</h4><Reason id="si-review" labelText={t.reason} value={reason} onChange={setReason}/>
      {(['APPROVE', 'RETURN', 'REJECT'] as const).map(d => <Button key={d} type="button" variant={d === 'APPROVE' ? 'default' : 'outline'} disabled={busy || reason.trim().length < 10}
        onClick={() => void run(safetyApi.reviewInvestigation(vessel, event, d, reason))}>{label(t, 'rd_' + d)}</Button>)}</div>}
  </section>;
}

function ActionsPanel({ t, vessel, event, busy, run }: { t: Copy; vessel: string; event: SafetyEvent; busy: boolean; run: Run }) {
  const approved = event.investigation?.state === 'APPROVED' ? event.investigation.content : null;
  const [draft, setDraft] = useState({ kind: 'CORRECTIVE', title: '', description: '', ownerId: '', dueDate: '', rootCauseIds: [] as string[], barrierIds: [] as string[], followUpOf: null as string | null });
  const valid = draft.title.trim().length >= 3 && draft.description.trim().length >= 10 && isGuid(draft.ownerId) && !!draft.dueDate && draft.rootCauseIds.length + draft.barrierIds.length > 0;
  return <section aria-label={t.actionsHeading}><h3>{t.actionsHeading}</h3><p><small>{t.actionsHint}</small></p>
    {event.actions.length === 0 ? <p role="status">{t.noActions}</p> : <ul>{event.actions.map(a => <ActionRow key={a.id} t={t} vessel={vessel} event={event} action={a} busy={busy} run={run}/>)}</ul>}
    {event.can.createAction && approved && <form aria-label={t.addAction} onSubmit={e => { e.preventDefault(); void run(safetyApi.createAction(vessel, event, draft)); }}>
      <h4>{t.addAction}</h4>
      <fieldset disabled={busy}>
        <label htmlFor="sa-kind">{t.actionKind}</label><select id="sa-kind" value={draft.kind} onChange={e => setDraft({ ...draft, kind: e.target.value })}>
          <option value="CORRECTIVE">{t.ak_CORRECTIVE}</option><option value="PREVENTIVE">{t.ak_PREVENTIVE}</option></select>
        <label htmlFor="sa-title">{t.actionTitle}</label><input id="sa-title" value={draft.title} maxLength={200} onChange={e => setDraft({ ...draft, title: e.target.value })}/>
        <label htmlFor="sa-desc">{t.actionDescription}</label><textarea id="sa-desc" value={draft.description} maxLength={4000} onChange={e => setDraft({ ...draft, description: e.target.value })}/>
        <label htmlFor="sa-owner">{t.owner}</label><input id="sa-owner" value={draft.ownerId} onChange={e => setDraft({ ...draft, ownerId: e.target.value.trim() })}/><small>{t.ownerHint}</small>
        <label htmlFor="sa-due">{t.due}</label><input id="sa-due" type="date" value={draft.dueDate} onChange={e => setDraft({ ...draft, dueDate: e.target.value })}/>
        <fieldset><legend>{t.linkedTo}</legend>
          {approved.rootCauses.map(r => <label key={r.id}><input type="checkbox" checked={draft.rootCauseIds.includes(r.id)} onChange={e => setDraft({ ...draft,
            rootCauseIds: e.target.checked ? [...draft.rootCauseIds, r.id] : draft.rootCauseIds.filter(x => x !== r.id) })}/> {t.rootCause}: {r.statement.slice(0, 60)}</label>)}
          {approved.barriers.map(b => <label key={b.id}><input type="checkbox" checked={draft.barrierIds.includes(b.id)} onChange={e => setDraft({ ...draft,
            barrierIds: e.target.checked ? [...draft.barrierIds, b.id] : draft.barrierIds.filter(x => x !== b.id) })}/> {t.barrier}: {b.name} ({label(t, 'bp_' + b.performance)})</label>)}
        </fieldset>
        {event.actions.some(a => a.effectiveness === 'NOT_EFFECTIVE') && <><label htmlFor="sa-follow">{t.followUpOf}</label>
          <select id="sa-follow" value={draft.followUpOf ?? ''} onChange={e => setDraft({ ...draft, followUpOf: e.target.value || null })}><option value="">{t.notSelected}</option>
            {event.actions.filter(a => a.effectiveness === 'NOT_EFFECTIVE').map(a => <option key={a.id} value={a.id}>{a.title}</option>)}</select></>}
      </fieldset>
      <Button type="submit" disabled={busy || !valid}>{t.addAction}</Button>
    </form>}
  </section>;
}

function ActionRow({ t, vessel, event, action, busy, run }: { t: Copy; vessel: string; event: SafetyEvent; action: SafetyAction; busy: boolean; run: Run }) {
  const [text, setText] = useState(''), [outcome, setOutcome] = useState<string>('');
  const kind = action.allowedSteps[0] ?? null;
  const outcomes = kind === 'VERIFY' ? ['VERIFIED', 'NOT_VERIFIED'] : kind === 'EFFECTIVENESS' ? ['EFFECTIVE', 'NOT_EFFECTIVE', 'INCONCLUSIVE'] : [];
  return <li data-action-state={action.state} data-effectiveness={action.effectiveness}>
    <strong>{action.title}</strong> · {label(t, 'ak_' + action.kind)} · {t.owner}: <code>{action.ownerId.slice(0, 8)}</code> · {t.due}: <time dateTime={action.dueDate}>{action.dueDate}</time>
    {action.overdue && <> <Badge tone="danger">{t.overdue}</Badge></>} · <Badge tone="neutral">{label(t, 'as_' + action.state)}</Badge> <Tone value={action.effectiveness} t={t} prefix="e_"/>
    {action.steps.length > 0 && <ol>{action.steps.map(s => <li key={s.id}>{label(t, 'sk_' + s.kind)}{s.outcome && <> · {label(t, 'o_' + s.outcome)}</>} · {s.text}</li>)}</ol>}
    {kind && event.state === 'ACTION_TRACKING' && <div className="safety-row">
      <Reason id={`sa-step-${action.id}`} labelText={label(t, 'sk_' + kind)} value={text} onChange={setText}/>
      {outcomes.length > 0 && <select aria-label={t.outcome} value={outcome} onChange={e => setOutcome(e.target.value)}><option value="">{t.notSelected}</option>
        {outcomes.map(o => <option key={o} value={o}>{label(t, 'o_' + o)}</option>)}</select>}
      <Button type="button" disabled={busy || text.trim().length < 10 || outcomes.length > 0 && !outcome} onClick={() => void run(safetyApi.step(vessel, event, action, kind, outcomes.length ? outcome : null, text))}>{label(t, 'sk_' + kind)}</Button>
    </div>}
  </li>;
}

function ClosurePanel({ t, vessel, event, busy, run }: { t: Copy; vessel: string; event: SafetyEvent; busy: boolean; run: Run }) {
  const [reason, setReason] = useState('');
  return <section aria-label={t.closureHeading}><h3>{t.closureHeading}</h3>
    {event.closureBlockers.length > 0 ? <ul aria-label={t.closureBlockers}>{event.closureBlockers.map(b => <li key={b}>{label(t, 'f_' + b.split(':')[0])}</li>)}</ul> : <p role="status">{t.noClosureBlockers}</p>}
    {event.can.close && <><Reason id="se-close" labelText={t.reason} value={reason} onChange={setReason}/>
      <Button type="button" disabled={busy || reason.trim().length < 10} onClick={() => void run(safetyApi.close(vessel, event, reason))}>{t.close}</Button></>}
  </section>;
}

function MessagesPanel({ t, vessel, event, busy, run }: { t: Copy; vessel: string; event: SafetyEvent; busy: boolean; run: Run }) {
  const [body, setBody] = useState(''), [visibility, setVisibility] = useState('TEAM_ONLY');
  return <section aria-label={t.messagesHeading}><h3>{t.messagesHeading}</h3>
    {event.messages.length === 0 ? <p role="status">{t.noMessages}</p> : <ul>{event.messages.map(m => <li key={m.id}><Badge tone="neutral">{label(t, 'mv_' + m.visibility)}</Badge>{' '}
      {m.authorKind === 'REPORTER' ? t.fromReporter : t.fromTeam}: {m.body}</li>)}</ul>}
    {event.can.message && <div className="safety-row">
      <label htmlFor="se-msg">{t.message}</label><textarea id="se-msg" value={body} maxLength={4000} onChange={e => setBody(e.target.value)}/>
      <select aria-label={t.visibility} value={visibility} onChange={e => setVisibility(e.target.value)}><option value="TEAM_ONLY">{t.mv_TEAM_ONLY}</option><option value="REPORTER_SHARED">{t.mv_REPORTER_SHARED}</option></select>
      <Button type="button" disabled={busy || !body.trim()} onClick={() => void run(safetyApi.message(vessel, event, visibility, body))}>{t.send}</Button></div>}
  </section>;
}

// The reporter keeps the token themselves; it is held only in component memory here, never stored or put in the URL.
function FollowUpPanel({ t, csrf, busy }: { t: Copy; csrf: string | null; busy: boolean }) {
  const [token, setToken] = useState(''), [view, setView] = useState<FollowUp | null>(null), [body, setBody] = useState(''), [state, setState] = useState<'idle' | 'error' | 'loading'>('idle');
  const act = async (f: () => Promise<FollowUp>) => { setState('loading'); try { setView(await f()); setState('idle'); } catch { setView(null); setState('error'); } };
  return <section aria-label={t.followUpHeading}><h3>{t.followUpHeading}</h3><p><small>{t.followUpHint}</small></p>
    <label htmlFor="se-token">{t.followUpToken}</label><input id="se-token" type="password" autoComplete="off" value={token} onChange={e => setToken(e.target.value.trim())}/>
    <Button type="button" variant="outline" disabled={busy || token.length < 24 || state === 'loading'} onClick={() => void act(() => safetyApi.followUp(token, new AbortController().signal))}>{t.followUpOpen}</Button>
    {state === 'error' && <p role="alert">{t.followUpUnavailable}</p>}
    {view && <div><p>{view.eventNo} · {label(t, 's_' + view.state)}</p>
      {view.messages.length === 0 ? <p role="status">{t.noMessages}</p> : <ul>{view.messages.map(m => <li key={m.id}>{m.authorKind === 'REPORTER' ? t.fromYou : t.fromTeam}: {m.body}</li>)}</ul>}
      <label htmlFor="se-fu-msg">{t.message}</label><textarea id="se-fu-msg" value={body} maxLength={4000} onChange={e => setBody(e.target.value)}/>
      <Button type="button" disabled={busy || !csrf || !body.trim() || state === 'loading'} onClick={() => void act(async () => { const v = await safetyApi.followUpMessage(token, body, csrf!, new AbortController().signal); setBody(''); return v; })}>{t.send}</Button></div>}
  </section>;
}

function OfflinePanel({ t, vessel, available, online, onSynced }: { t: Copy; vessel: string; available: boolean; online: boolean; onSynced: () => void }) {
  const [app, setApp] = useState<SafetyEventOfflineApplication | null>(null), [rows, setRows] = useState<SafetyPending[]>([]), [draft, setDraft] = useState(emptyDraft());
  const [status, setStatus] = useState<string | null>(null), [busy, setBusy] = useState(false); const handle = useRef<SafetyEventOfflineApplication | null>(null);
  useEffect(() => () => handle.current?.close(), []);
  const guard = async (action: () => Promise<void>) => {
    setBusy(true); setStatus(null);
    try { await action(); } catch (e) {
      const m = e instanceof Error ? e.message : '';
      setStatus(m === 'reauthorization_required_data_retained' ? t.offlineReauthorize : m === 'key_lost_data_retained' ? t.offlineKeyLost : t.offlineFailed);
    } finally { setBusy(false); }
  };
  const refresh = async (value: SafetyEventOfflineApplication) => setRows(await value.captures());
  const queue = projectSafetyQueue(rows);
  return <section aria-label={t.offlineHeading} aria-busy={busy}><h3>{t.offlineHeading}</h3><p><small>{t.offlineIntro}</small></p>
    {status && <p role="status">{status}</p>}
    {!available ? <p role="note" data-testid="offline-unavailable">{t.offlineUnavailable}</p> : !app ?
      <Button type="button" variant="outline" disabled={busy || !online} onClick={() => void guard(async () => {
        const value = await SafetyEventOfflineApplication.authorize(vessel, null, new AbortController().signal); handle.current?.close(); handle.current = value; setApp(value); await refresh(value); setStatus(t.offlinePrepared); })}>{t.offlinePrepare}</Button> : <>
        <p><Badge tone={queue.pending ? 'warning' : 'neutral'}>{t.offlinePending}: {queue.pending}</Badge></p>
        <DraftEditor t={t} value={draft} onChange={setDraft} disabled={busy}/>
        <Button type="button" disabled={busy} onClick={() => void guard(async () => { await app.capture(draft); setDraft(emptyDraft()); await refresh(app); setStatus(t.offlineQueued); })}>{t.offlineCapture}</Button>{' '}
        <Button type="button" variant="outline" disabled={busy || !online || queue.pending === 0} onClick={() => void guard(async () => {
          for (const row of (await app.captures()).filter(r => !r.outcome)) await app.replay(row, new AbortController().signal);
          await refresh(app); setStatus(t.offlineSynced); onSynced(); })}>{t.offlineSync}</Button>{' '}
        <Button type="button" variant="ghost" disabled={busy || !online} onClick={() => void guard(async () => { await app.revoke(false, new AbortController().signal); setApp(null); handle.current = null; setStatus(t.offlineRevoked); })}>{t.offlineRevoke}</Button>
        {queue.committed.length > 0 && <ul aria-label={t.offlineSynced}>{queue.committed.map(o => <li key={o.commandId}>{o.eventNo}</li>)}</ul>}
      </>}
  </section>;
}
