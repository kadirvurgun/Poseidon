import { localizedCopy } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { useEffect, useId, useState, type ReactNode } from 'react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterpriseChrome } from './EnterpriseChrome';
import { EnterprisePageHeader, EnterpriseToolbar } from './EnterprisePatterns';
import * as api from './api';
import { decodeTodayVessels } from './today-api';
import { auditReadyHref, auditReadyTrace, chains as loadChains, readAuditReadyLocation, type AuditReadyChain, type AuditReadyChains, type AuditReadyRoute } from './audit-ready-api';
import type { Anchor, AuditTraceEvent, Review, Segment, Trace } from './human-review-api';
import { AuditPackPanel } from './AuditPackPanel';
import { pilotEvidenceSearch, pilotForVessel } from './pilot-telemetry';
import './offline-enterprise.css';
import './product.css';

// DEV-032 / T-S3-007 (18.01 Backlog row:33): requirement -> control -> evidence -> verification ->
// decision states and timestamps with a source-grounded drill-down. Presentation only: the server
// authorizes every read. A recorded chain is a human record; nothing here renders a green/ready
// state, and UNKNOWN/unavailable/unverified inputs are always shown as review-required.
const copy = localizedCopy("AuditReadyWorkspace_tsx_copy");
type Copy = (typeof copy)['en'];
type Tone = 'neutral' | 'warning';
const reviewStates = new Set(['UNKNOWN', 'Unavailable', 'REVIEW_REQUIRED', 'PENDING', 'DRAFT', 'REJECTED', 'EXPIRED', 'SUPERSEDED', 'SUPERSEDED_REVIEW_REQUIRED', 'EXPIRED_REVIEW_REQUIRED', 'NOT_RECORDED', 'Missing']);
export const stateTone = (state: string): Tone => reviewStates.has(state) || !state ? 'warning' : 'neutral';
type TraceEvent = { id: string; revision: number; kind: string; state: string; currentState: string; savedAt: string; validUntil: string | null; reasonCode: string | null; authorityRole: string | null; contextHash: string };
export function traceEvents(trace: Trace, kind: 'Verification' | 'Decision'): TraceEvent[] {
    const rows = new Map<string, TraceEvent>();
    const add = (e: Review | AuditTraceEvent, reason: string | null, role: string | null) => {
        if (e.kind === kind) rows.set(`${e.id}/${e.revision}`, { id: e.id, revision: e.revision, kind: e.kind, state: e.state, currentState: e.currentState, savedAt: e.savedAt, validUntil: e.validUntil, reasonCode: reason, authorityRole: role, contextHash: e.contextHash });
    };
    for (const e of trace.auditItems ?? []) add(e, e.reasonCode ?? null, e.authorityRole ?? null);
    for (const e of trace.items) add(e, e.reasonCode, e.recording?.policy.authorityRole ?? null);
    return [...rows.values()].sort((x, y) => Date.parse(y.savedAt) - Date.parse(x.savedAt) || y.revision - x.revision);
}
export function isStale(trace: Trace) {
    return ['SUPERSEDED', 'DECLARED_EXPIRED', 'DECLARED_STALE'].includes(trace.evidenceContext.timeState) ||
        [...trace.items, ...(trace.auditItems ?? [])].some(e => e.currentState.endsWith('_REVIEW_REQUIRED'));
}
function useOnline() {
    const [online, setOnline] = useState(() => navigator.onLine);
    useEffect(() => {
        const update = () => setOnline(navigator.onLine);
        window.addEventListener('online', update); window.addEventListener('offline', update);
        return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update); };
    }, []);
    return online;
}
const Time = ({ value, t }: { value: string | null; t: Copy }) => value ? <time dateTime={value}>{value}</time> : <span>{t.notRecorded}</span>;
export function AuditReadyWorkspace() {
    const [language, setLanguage] = useAppLanguage(), t = copy[language];
    const [route, setRoute] = useState<AuditReadyRoute>(() => readAuditReadyLocation(window.location.pathname, window.location.search));
    const [attempt, retry] = useState(0);
    const online = useOnline();
    const [identity, setIdentity] = useState<{ attempt: number; user?: api.Session; error?: boolean } | null>(null);
    useEffect(() => {
        if (route.invalid || !online) return;
        const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000);
        api.session(c.signal).then(user => { if (active) setIdentity({ attempt, user }); })
            .catch(() => { if (active) setIdentity({ attempt, error: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [route.invalid, attempt, online]);
    useEffect(() => { document.title = `POSEIDON · ${t.title}`; }, [t.title]);
    const go = (next: string) => { history.pushState(null, '', next); const [p, q = ''] = next.split('?'); setRoute(readAuditReadyLocation(p, q ? '?' + q : '')); };
    useEffect(() => {
        const pop = () => setRoute(readAuditReadyLocation(window.location.pathname, window.location.search));
        window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop);
    }, []);
    const user = identity?.user ?? null;
    const allowed = !!user?.authenticated && !!user.canRead;
    return <div className="product-shell enterprise-shell"><EnterpriseChrome session={user} vessel={route.vessel || null} language={language} onLanguageChange={setLanguage} onNavigate={(href, committed) => { committed?.(); window.location.assign(href); }} title={t.title} mainId="audit-ready-main"/>
    <main id="audit-ready-main" tabIndex={-1} data-testid="audit-ready"><EnterprisePageHeader title={t.title} description={t.description} actions={<Button variant="outline" disabled={!online} onClick={() => retry(n => n + 1)}>{t.retry}</Button>}/>
      {!online && <p role="status" data-state="offline">{t.offline}</p>}
      {route.invalid ? <p role="alert">{t.invalidRoute}</p> : !identity ? (online ? <p role="status">{t.loading}</p> : null) : identity.error ? <p role="alert">{t.sessionError}</p> : !allowed ? <p role="alert">{t.denied}</p> : <>
        <VesselPicker key={`${user?.tenantId}:${user?.actorId}:${attempt}`} vessel={route.vessel} online={online} t={t} onSelect={id => go(auditReadyHref(id))}/>
        {route.vessel ? <ChainList key={`${user?.tenantId}:${user?.actorId}:${route.vessel}:${attempt}`} vessel={route.vessel} anchor={route.anchor} online={online} t={t} language={language} csrf={user?.csrfToken ?? null} onOpen={chain => go(auditReadyHref(route.vessel, chain))}/> : <p role="status">{t.noVessel}</p>}
      </>}
    </main></div>;
}
function VesselPicker({ vessel, online, t, onSelect }: { vessel: string; online: boolean; t: Copy; onSelect: (id: string) => void }) {
    const [rows, setRows] = useState<api.Vessel[] | null>(null);
    useEffect(() => {
        if (!online) return;
        const c = new AbortController(); let active = true;
        api.vessels(0, '', c.signal).then(v => decodeTodayVessels(v, 0)).then(v => { if (active) setRows(v.items); }).catch(() => { if (active) setRows([]); });
        return () => { active = false; c.abort(); };
    }, [online]);
    const id = useId();
    return <EnterpriseToolbar><label htmlFor={id}>{t.vessel}</label><select id={id} value={vessel} disabled={!online} onChange={e => { if (e.target.value) onSelect(e.target.value); }}>
        <option value="">{t.chooseVessel}</option>
        {vessel && !rows?.some(r => r.id.toLowerCase() === vessel) && <option value={vessel}>{vessel}</option>}
        {rows?.map(r => <option key={r.id} value={r.id.toLowerCase()}>{r.code} · {r.name}</option>)}
    </select></EnterpriseToolbar>;
}
type Loaded<T> = { data?: T; error?: number; loadedAt?: string };
function ChainList({ vessel, anchor, online, t, language, csrf, onOpen }: { vessel: string; anchor: Anchor | null; online: boolean; t: Copy; language: AppLocale; csrf: string | null; onOpen: (chain: AuditReadyChain) => void }) {
    const [result, setResult] = useState<Loaded<AuditReadyChains> | null>(null);
    useEffect(() => {
        if (!online) return;
        const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000);
        // S11 gate B1 (DEV-072, 19.01 p150-152): evidence search time = request start to the readable chain
        // list. Only the duration and the vessel's SHIP/TUG mode are reported.
        const started = performance.now();
        loadChains(vessel, c.signal).then(data => { if (active) { setResult({ data, loadedAt: new Date().toISOString() }); const elapsed = performance.now() - started; pilotForVessel(vessel, area => pilotEvidenceSearch(area, elapsed)); } })
            .catch(e => { if (active) setResult({ error: e instanceof api.ApiError ? e.status : 0 }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [vessel, online]);
    if (!result) return online ? <p role="status">{t.loading}</p> : null;
    if (!result.data) return <p role="alert">{result.error === 403 ? t.denied : result.error === 404 ? t.notFound : t.loadError}</p>;
    const data = result.data;
    const selected = anchor && data.items.find(c => c.evidenceId.toLowerCase() === anchor.evidenceId && c.controlId.toLowerCase() === anchor.controlId);
    return <>
      <section aria-labelledby="audit-ready-chains"><h2 id="audit-ready-chains">{t.chainsTitle}</h2>
        {data.items.length === 0 ? <p role="status" data-state="empty">{t.chainsEmpty}</p> : <ul className="audit-ready-chains">{data.items.map(chain => {
            const current = selected === chain;
            return <li key={`${chain.evidenceId}/${chain.controlId}`}><Button variant={current ? 'default' : 'outline'} aria-current={current ? 'true' : undefined} onClick={() => onOpen(chain)}>
              {t.evidence} <bdi dir="ltr">{chain.evidenceId}</bdi> r{chain.evidenceRevision} → {t.control} <bdi dir="ltr">{chain.controlId}</bdi> r{chain.controlRevision}</Button></li>;
        })}</ul>}
        {data.unavailable > 0 && <p role="note">{t.chainsUnavailable.replace('{{count}}', String(data.unavailable))}</p>}
        {data.hasMore && <p role="note">{t.chainsMore}</p>}
      </section>
      {anchor && (selected
        ? <ChainTrace key={`${anchor.evidenceId}/${anchor.evidenceRevision}/${anchor.controlId}/${anchor.controlRevision}`} anchor={anchor} chain={selected} online={online} t={t} language={language}/>
        : <p role="alert">{t.notFound}</p>)}
      {/* DEV-064: packs are offered only when the reader has readable appointed chains. */}
      {data.items.length > 0 && <AuditPackPanel vessel={vessel} csrf={csrf} online={online} language={language}/>}
    </>;
}
type Stage = { key: 'requirement' | 'control' | 'evidence' | 'verification' | 'decision'; label: string; state: string; at: string | null; body: ReactNode };
function ChainTrace({ anchor, chain, online, t }: { anchor: Anchor; chain: AuditReadyChain; online: boolean; t: Copy; language: AppLocale }) {
    const [result, setResult] = useState<Loaded<Trace> | null>(null);
    useEffect(() => {
        if (!online) return;
        const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000);
        auditReadyTrace(anchor, c.signal).then(data => { if (active) setResult({ data, loadedAt: new Date().toISOString() }); })
            .catch(e => { if (active) setResult(prior => prior?.data ? { ...prior, error: e instanceof api.ApiError ? e.status : 0 } : { error: e instanceof api.ApiError ? e.status : 0 }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [anchor, online]);
    if (!result) return online ? <p role="status">{t.loading}</p> : null;
    if (!result.data) return <p role="alert">{result.error === 403 ? t.denied : result.error === 404 ? t.notFound : t.loadError}</p>;
    const trace = result.data, stale = isStale(trace);
    const seg = (kind: string) => trace.segments.filter(s => s.kind === kind);
    const worst = (segments: Segment[]) => segments.length === 0 ? 'Unavailable' : segments.find(s => s.availability !== 'Available')?.availability ?? segments.find(s => stateTone(s.state) === 'warning')?.state ?? segments[0].state;
    const latest = (values: (string | null)[]) => values.filter((v): v is string => !!v).sort().at(-1) ?? null;
    const requirementSegments = [...seg('Source'), ...seg('Requirement'), ...seg('Rule'), ...seg('Applicability'), ...seg('Evaluation')];
    const verifications = traceEvents(trace, 'Verification'), decisions = traceEvents(trace, 'Decision');
    const stages: Stage[] = [
        { key: 'requirement', label: t.requirement, state: worst(requirementSegments), at: latest(requirementSegments.map(s => s.savedAt)), body: <>
            <SegmentList segments={requirementSegments} t={t}/>
            <ul aria-label={t.source}>{chain.sources.map(s => <li key={`${s.sourceId}/${s.sourceRevision}/${s.requirementId}`} data-testid="audit-ready-source">
              <dl><div><dt>{t.source}</dt><dd><bdi dir="ltr">{s.sourceId}</bdi> · {t.revision} {s.sourceRevision}</dd></div>
                <div><dt>{t.requirement}</dt><dd><bdi dir="ltr">{s.requirementId}</bdi> · {t.revision} {s.requirementRevision}</dd></div>
                <div><dt>{t.locator}</dt><dd>{s.availability !== 'Available' ? <Badge tone="warning">{t.unavailable}</Badge> : s.locatorState === 'Known' ? <cite>{s.locator}</cite> : <Badge tone="warning">{s.locatorState === 'Missing' ? t.locatorMissing : s.locatorState}</Badge>}</dd></div>
                {s.titleState === 'Known' && <div><dt>{t.sourceTitle}</dt><dd>{s.title}</dd></div>}
                <div><dt>{t.savedAt}</dt><dd><Time value={s.sourceSavedAt} t={t}/></dd></div></dl></li>)}</ul></> },
        { key: 'control', label: t.control, state: worst(seg('Control')), at: latest(seg('Control').map(s => s.savedAt)), body: <SegmentList segments={seg('Control')} t={t}/> },
        { key: 'evidence', label: t.evidence, state: worst(seg('Evidence')), at: latest(seg('Evidence').map(s => s.savedAt)), body: <>
            <SegmentList segments={seg('Evidence')} t={t}/>
            <dl><div><dt>{t.validUntil}</dt><dd><Time value={trace.evidenceContext.validUntil} t={t}/></dd></div><div><dt>{t.state}</dt><dd><Badge tone={stateTone(trace.evidenceContext.timeState)}>{trace.evidenceContext.timeState}</Badge></dd></div></dl></> },
        { key: 'verification', label: t.verification, state: verifications[0]?.currentState ?? 'NOT_RECORDED', at: verifications[0]?.savedAt ?? null, body: <EventList events={verifications} t={t}/> },
        { key: 'decision', label: t.decision, state: decisions[0]?.currentState ?? 'NOT_RECORDED', at: decisions[0]?.savedAt ?? null, body: <EventList events={decisions} t={t}/> },
    ];
    return <section aria-labelledby="audit-ready-trace" data-testid="audit-ready-trace" data-disposition={trace.disposition}>
      <h2 id="audit-ready-trace">{t.stagesLabel}</h2>
      <p><Badge tone={trace.complete ? 'neutral' : 'warning'}>{trace.complete ? t.dispositionRecorded : t.dispositionReview}</Badge> {t.loadedAt}: <time dateTime={result.loadedAt}>{result.loadedAt}</time></p>
      {(stale || result.error !== undefined) && <p role="alert" data-state="stale">{t.stale}</p>}
      {!online && <p role="status" data-state="offline-stale">{t.offline}</p>}
      {trace.blockers.length > 0 && <div><h3>{t.blockers}</h3><ul aria-label={t.blockers}>{trace.blockers.map(b => <li key={b}><code>{b}</code></li>)}</ul></div>}
      <ol className="audit-ready-stages" aria-label={t.stagesLabel}>{stages.map(stage => <StageItem key={stage.key} stage={stage} t={t}/>)}</ol>
    </section>;
}
function StageItem({ stage, t }: { stage: Stage; t: Copy }) {
    const [open, setOpen] = useState(false), id = useId();
    const shown = stage.state === 'NOT_RECORDED' ? t.notRecorded : stage.state === 'Unavailable' ? t.unavailable : stage.state;
    return <li data-stage={stage.key}><h3><Button variant="ghost" aria-expanded={open} aria-controls={id} onClick={() => setOpen(v => !v)}>
        {stage.label} <Badge tone={stateTone(stage.state)} data-tone-state={stage.state}>{shown}</Badge> <span>{t.savedAt}: <Time value={stage.at} t={t}/></span></Button></h3>
      <div id={id} role="region" aria-label={`${stage.label} · ${t.details}`} hidden={!open}>{stage.body}</div></li>;
}
function SegmentList({ segments, t }: { segments: Segment[]; t: Copy }) {
    if (segments.length === 0) return <p><Badge tone="warning">{t.unavailable}</Badge></p>;
    return <ul>{segments.map((s, i) => <li key={`${s.kind}/${s.id ?? i}/${s.revision ?? ''}`}><dl>
        <div><dt>{s.kind}</dt><dd>{s.availability === 'Available' ? <><bdi dir="ltr">{s.id}</bdi>{s.revision !== null && <> · {t.revision} {s.revision}</>}</> : <Badge tone="warning">{t.unavailable}</Badge>}</dd></div>
        <div><dt>{t.state}</dt><dd><Badge tone={s.availability === 'Available' ? stateTone(s.state) : 'warning'}>{s.availability === 'Available' ? s.state : 'UNKNOWN'}</Badge> <code>{s.why}</code></dd></div>
        <div><dt>{t.savedAt}</dt><dd><Time value={s.savedAt} t={t}/></dd></div>
        {s.hash && <div><dt>{t.hash}</dt><dd><code>{s.hash}</code></dd></div>}</dl></li>)}</ul>;
}
function EventList({ events, t }: { events: TraceEvent[]; t: Copy }) {
    if (events.length === 0) return <p><Badge tone="warning">{t.notRecorded}</Badge></p>;
    return <ul>{events.map(e => <li key={`${e.id}/${e.revision}`}><dl>
        <div><dt>{t.state}</dt><dd><Badge tone={stateTone(e.currentState)}>{e.currentState}</Badge>{e.currentState !== e.state && <> (<code>{e.state}</code>)</>}</dd></div>
        <div><dt>{t.savedAt}</dt><dd><Time value={e.savedAt} t={t}/></dd></div>
        {e.validUntil && <div><dt>{t.validUntil}</dt><dd><Time value={e.validUntil} t={t}/></dd></div>}
        {e.reasonCode && <div><dt>{t.reason}</dt><dd><code>{e.reasonCode}</code></dd></div>}
        {e.authorityRole && <div><dt>{t.role}</dt><dd>{e.authorityRole}</dd></div>}
        <div><dt>{t.contextHash}</dt><dd><code>{e.contextHash}</code></dd></div></dl></li>)}</ul>;
}
