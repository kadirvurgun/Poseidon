import { localizedCopy } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useId, useRef, useState } from 'react';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { ApiError, session as readSession, type Session } from './api';
import { isGuid } from './identifiers';
import type { DailyRecord } from './daily-operations-api';
import { engineReferenceApi, engineReferenceAttempt, engineReferenceKinds, type EngineReferenceAttempt, type EngineReferenceCandidatePage, type EngineReferenceKind, type EngineReferences } from './daily-engine-reference-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import './daily-operations.css';
const copy = localizedCopy('DailyEngineReferencePanel_tsx_copy');
const boundary = (user: Session | null | undefined) => user?.authenticated && user.canRead && isGuid(user.tenantId) && isGuid(user.actorId) ? `${user.tenantId}/${user.actorId}` : null;
export type DailyEngineReferencePanelProps = { owner: string; session: Session | null; record: DailyRecord; language: AppLocale; enabled: boolean; onSaving?: (value: boolean) => void };
// DEV-099: WO/defect links of one engine log entry. Online-only; UNKNOWN stays UNKNOWN and
// every link is REVIEW_REQUIRED lineage, never a verified, closed or approved state.
export function DailyEngineReferencePanel({ owner, session, record, language, enabled, onSaving }: DailyEngineReferencePanelProps) {
    const t = copy[language], id = useId(), epoch = useWorkspaceRevalidation();
    const [attempt, retry] = useState(0), [source, setSource] = useState<{ scope: string; data?: EngineReferences; user?: Session; failed?: boolean; other?: boolean } | null>(null);
    const [kind, setKind] = useState<EngineReferenceKind | ''>(''), [candidates, setCandidates] = useState<{ kind: EngineReferenceKind; page?: EngineReferenceCandidatePage; failed?: boolean } | null>(null), [choice, setChoice] = useState('');
    const [frozen, setFrozen] = useState<{ attempt: EngineReferenceAttempt; uncertain: boolean } | null>(null), [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [notice, setNotice] = useState(false);
    const mounted = useRef(false), running = useRef(false);
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', identity = boundary(session);
    const scope = `${record.id}/${record.revision}/${record.version}/${identity}/${epoch}/${attempt}`;
    const mayRead = online && visible && identity === owner && record.kind === 'Engine';
    const current = mayRead && source?.scope === scope ? source : null, data = current?.data;
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
    useEffect(() => {
        if (!mayRead) return;
        const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000);
        (async () => {
            const user = await readSession(c.signal);
            if (boundary(user) !== owner) { if (active) setSource({ scope, other: true }); return; }
            const value = await engineReferenceApi.read(record.vesselId, record.id, c.signal);
            if (active) setSource({ scope, user, data: value });
        })().catch(() => { if (active) setSource({ scope, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [record.id, record.vesselId, owner, scope, mayRead]);
    useEffect(() => {
        if (!mayRead || !kind || !data?.canLink) return;
        const c = new AbortController(); let active = true;
        engineReferenceApi.candidates(record.vesselId, kind, 0, c.signal).then(page => { if (active) setCandidates({ kind, page }); }).catch(() => { if (active) setCandidates({ kind, failed: true }); });
        return () => { active = false; c.abort(); };
    }, [kind, mayRead, data?.canLink, record.vesselId]);
    const stale = !!data && (data.recordRevision !== record.revision || data.recordVersion !== record.version);
    const canWrite = enabled && !!data?.canLink && !stale && !busy && !!current?.user;
    async function send(next: EngineReferenceAttempt) {
        if (running.current || !current?.user) return;
        running.current = true; setBusy(true); onSaving?.(true); setFrozen({ attempt: next, uncertain: frozen?.uncertain ?? false }); setFailure(null); setNotice(false);
        const c = new AbortController(), timer = setTimeout(() => c.abort(), 15000); let uncertain = false;
        try {
            const user = await readSession(c.signal);
            if (boundary(user) !== owner) throw new ApiError(403, 'identity_boundary_changed');
            await engineReferenceApi.record(next, user.csrfToken, c.signal);
            if (!mounted.current) return;
            setFrozen(null); setChoice(''); setNotice(true); retry(n => n + 1);
        } catch (error) {
            if (!mounted.current) return;
            const problem = error instanceof ApiError ? error : new ApiError(0);
            uncertain = problem.status === 0 || problem.status >= 500;
            setFrozen(uncertain ? { attempt: next, uncertain: true } : null); setFailure(problem);
            if (!uncertain) retry(n => n + 1);
        } finally { clearTimeout(timer); running.current = false; if (mounted.current) { setBusy(false); onSaving?.(uncertain); } }
    }
    const link = () => {
        const chosen = candidates?.page?.items.find(row => row.pin.id === choice);
        if (data && chosen) void send(engineReferenceAttempt(data, 'LINK', chosen.pin));
    };
    const stateLabel = (state: string) => state === 'LINKED_CURRENT' ? t.current : state === 'LINKED_SUPERSEDED' ? t.superseded : t.unknown;
    const kindLabel = (k: EngineReferenceKind) => k === 'WorkOrder' ? t.workOrder : t.defect;
    return <section className="daily-review-panel" aria-label={t.title}><h3>{t.title}</h3><p>{t.boundary}</p>
      {!online ? <p role="status">{t.offline}</p> : !mayRead || current?.other ? <p role="status">{t.hidden}</p> : !current ? <p role="status">{t.loading}</p> : current.failed || !data ? <p role="alert">{t.loadError}</p> : <>
        {notice && <p role="status">{t.saved}</p>}
        {stale && <p role="alert">{t.stale}</p>}
        {failure && <p role="alert">{frozen?.uncertain ? t.unknownOutcome : failure.status === 409 ? t.conflict : failure.code === 'linked_resource_unavailable' ? t.unavailableTarget : t.failed}</p>}
        <ul>{data.sources.map(s => <li key={s.kind}>{kindLabel(s.kind)}: {s.state === 'AVAILABLE' ? t.lookupAvailable : t.lookupUnavailable}</li>)}</ul>
        {data.items.length === 0 ? <p>{t.empty}</p> : <ul aria-label={t.linked}>{data.items.map(item => <li key={`${item.kind}/${item.sequence}`}>
          <Badge tone="warning">{stateLabel(item.state)}</Badge> {kindLabel(item.kind)} · {item.pin ? <a href={item.href ?? undefined}>{item.label ?? t.noNumber}</a> : t.unknown} {item.pin && <small>r{item.pin.revision}</small>} · {t.reviewRequired}
          {item.reason === 'REFERENCE_CHANGED_SINCE_LINK' && <span> · {t.changed}</span>}
          {item.pin && canWrite && !frozen && <Button variant="outline" size="sm" onClick={() => void send(engineReferenceAttempt(data, 'UNLINK', item.pin!))}>{t.unlink}</Button>}
        </li>)}</ul>}
        {!data.canLink ? <p>{t.noLink}</p> : <form noValidate aria-label={t.linkForm} onSubmit={event => { event.preventDefault(); link(); }}>
          <label htmlFor={`${id}-kind`}>{t.kind}</label><select id={`${id}-kind`} value={kind} disabled={!canWrite || !!frozen} onChange={e => { setKind(e.target.value as EngineReferenceKind | ''); setChoice(''); setCandidates(null); }}>
            <option value="">{t.choose}</option>{engineReferenceKinds.filter(k => data.sources.some(s => s.kind === k && s.state === 'AVAILABLE')).map(k => <option key={k} value={k}>{kindLabel(k)}</option>)}</select>
          {kind && (candidates?.kind !== kind ? <p role="status">{t.loading}</p> : candidates.failed ? <p role="alert">{t.lookupUnavailable}</p> : candidates.page?.items.length === 0 ? <p>{t.noCandidates}</p> : <>
            <label htmlFor={`${id}-target`}>{t.target}</label><select id={`${id}-target`} value={choice} disabled={!canWrite || !!frozen} onChange={e => setChoice(e.target.value)}>
              <option value="">{t.choose}</option>{candidates.page?.items.map(row => <option key={row.pin.id} value={row.pin.id}>{row.label ?? t.noNumber} · r{row.pin.revision}{row.detail ? ` · ${row.detail}` : ''}</option>)}</select></>)}
          {frozen?.uncertain ? <Button type="button" disabled={busy} onClick={() => void send(frozen.attempt)}>{t.retrySame}</Button> : <Button type="submit" disabled={!canWrite || !choice}>{busy ? t.saving : t.link}</Button>}
        </form>}
      </>}
      <Button variant="outline" size="sm" disabled={!online || !visible || busy} onClick={() => retry(n => n + 1)}>{t.refresh}</Button>
    </section>;
}
