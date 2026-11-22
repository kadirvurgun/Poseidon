import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { EnterpriseActions, EnterprisePagination } from './EnterprisePatterns';
import { ApiError, session as readSession, type Session } from './api';
import { isGuid } from './identifiers';
import type { DailyRecord } from './daily-operations-api';
import { dailyReviewApi, freezeDailyReview, reviewRationale, type DailyReview, type DailyReviewAttempt, type DailyReviewCapabilities, type DailyReviewChoice, type DailyReviewPage } from './daily-review-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import { dailyReviewDuty, reviewBlocker, dailyReviewChoiceLabel as reviewChoice } from './daily-review-copy';
import './daily-operations.css';
export type DailyReviewPanelProps = {
    owner: string;
    session: Session | null;
    record: DailyRecord;
    language: AppLocale;
    enabled: boolean;
    onDirty: (value: boolean) => void;
    onSaving: (value: boolean) => void;
    onCommitted: () => void;
    onReloadRecord: () => void;
};
type Source = {
    scope: string;
    caps?: DailyReviewCapabilities;
    history?: DailyReviewPage;
    user?: Session;
    failed?: boolean;
    differentIdentity?: boolean;
};
const boundary = (user: Session | null | undefined) => user?.authenticated && user.canRead && isGuid(user.tenantId) && isGuid(user.actorId) ? `${user.tenantId}/${user.actorId}` : null;
export function DailyReviewPanel({ owner, session, record, language, enabled, onDirty, onSaving, onCommitted, onReloadRecord }: DailyReviewPanelProps) {
    const en = language === 'en', epoch = useWorkspaceRevalidation(), id = useId(), [offset, setOffset] = useState(0), [attempt, retry] = useState(0), [source, setSource] = useState<Source | null>(null);
    const [authorityId, setAuthority] = useState(''), [decision, setDecision] = useState<DailyReviewChoice | ''>(''), [rationale, setRationale] = useState(''), [validation, setValidation] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [notice, setNotice] = useState(false);
    const [frozen, setFrozen] = useState<{
        command: DailyReviewAttempt;
        uncertain: boolean;
    } | null>(null), [busy, setBusy] = useState(false);
    const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), liveIdentity = useRef<string | null>(null);
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', identity = boundary(session);
    const [identityEpoch, setIdentityEpoch] = useState({ identity, epoch: 0 });
    if (identityEpoch.identity !== identity)
        setIdentityEpoch({ identity, epoch: identityEpoch.epoch + 1 });
    const scope = `${record.id}/${record.revision}/${record.version}/${identityEpoch.epoch}/${epoch}/${attempt}/${offset}`;
    const mayRead = online && visible && identity === owner;
    const current = mayRead && source?.scope === scope ? source : null, caps = current?.caps;
    useLayoutEffect(() => { liveIdentity.current = current?.user ? boundary(current.user) : null; }, [current]);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort(); }; }, []);
    useEffect(() => {
        if (!mayRead)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        async function load() {
            const user = await readSession(c.signal);
            if (boundary(user) !== owner) {
                if (active)
                    setSource({ scope, differentIdentity: true });
                return;
            }
            const [caps, history] = await Promise.all([dailyReviewApi.capabilities(record.vesselId, record.kind, record.id, c.signal), dailyReviewApi.history(record.vesselId, record.kind, record.id, offset, c.signal)]);
            if (active) {
                setSource({ scope, user, caps, history });
                if (caps.canRecord)
                    setFailure(previous => previous && [401, 403, 404].includes(previous.status) ? null : previous);
            }
        }
        load().catch(() => { if (active)
            setSource({ scope, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [record.id, record.vesselId, record.kind, owner, scope, offset, mayRead]);
    const sameRecord = !!caps && caps.recordRevision === record.revision && caps.recordVersion === record.version;
    const chosen = caps?.authorities.find(a => a.id === (frozen?.command.authority.id ?? authorityId));
    const originalAuthority = !frozen || !!chosen && chosen.roleId === frozen.command.authority.roleId && chosen.duty === frozen.command.authority.duty;
    const canSend = enabled && mayRead && !!current?.user && !!caps?.canRecord && !!chosen && originalAuthority && (frozen ? frozen.command.owner === owner : sameRecord) && !busy && (failure?.status !== 409 || !!frozen);
    const editing = enabled && mayRead && !!current?.user && !!caps?.canRecord && sameRecord && !busy && !frozen && failure?.status !== 409;
    const change = (nextAuthority = authorityId, nextDecision: DailyReviewChoice | '' = decision, nextRationale = rationale) => { onDirty(nextAuthority !== '' || nextDecision !== '' || nextRationale !== ''); setNotice(false); setFailure(null); };
    const dirty = authorityId !== '' || decision !== '' || rationale !== '';
    async function save() {
        if (running.current || !canSend || !caps || !current?.user)
            return;
        let command: DailyReviewAttempt;
        try {
            if (frozen)
                command = frozen.command;
            else {
                const actorId = current.user.actorId, tenantId = current.user.tenantId;
                if (!isGuid(actorId) || !isGuid(tenantId) || decision !== 'REVIEWED' && decision !== 'RETURNED') {
                    setValidation(true);
                    return;
                }
                command = freezeDailyReview({ record, caps, actorId, tenantId, authorityId, decision, rationale });
            }
        }
        catch {
            setValidation(true);
            return;
        }
        const wasUnknown = frozen?.uncertain ?? false;
        running.current = true;
        setBusy(true);
        onSaving(true);
        setFrozen({ command, uncertain: wasUnknown });
        setFailure(null);
        setValidation(false);
        setNotice(false);
        const c = new AbortController();
        writer.current = c;
        const timer = setTimeout(() => c.abort(), 15000);
        let uncertain = false;
        try {
            const user = await readSession(c.signal);
            if (boundary(user) !== command.owner) {
                setSource({ scope, differentIdentity: true });
                throw new ApiError(403, 'identity_boundary_changed');
            }
            await dailyReviewApi.record(command, user.csrfToken, c.signal);
            if (!mounted.current)
                return;
            if (liveIdentity.current !== command.owner) {
                uncertain = true;
                setFrozen({ command, uncertain: true });
                setFailure(new ApiError(0, 'identity_boundary_changed'));
                return;
            }
            setFrozen(null);
            setAuthority('');
            setDecision('');
            setRationale('');
            onDirty(false);
            setNotice(true);
            setOffset(0);
            retry(n => n + 1);
            onCommitted();
        }
        catch (error) {
            if (!mounted.current)
                return;
            const problem = error instanceof ApiError ? error : new ApiError(0);
            uncertain = wasUnknown || problem.status === 0 || problem.status >= 500;
            setFrozen(uncertain ? { command, uncertain: true } : null);
            setFailure(problem);
            if ([401, 403, 404].includes(problem.status))
                retry(n => n + 1);
        }
        finally {
            clearTimeout(timer);
            writer.current = null;
            running.current = false;
            if (mounted.current) {
                setBusy(false);
                onSaving(uncertain);
            }
        }
    }
    const hidden = !mayRead || current?.differentIdentity;
    return <section className="daily-review-panel" aria-label={localizedText(en, "pairs.m_1bce2fee2ad58434")}><h3>{localizedText(en, "pairs.m_a575617cc0974a5d")}</h3><p>{localizedText(en, "pairs.m_f1b1f1c5e4efb992")}</p>
    {hidden ? <p role="status">{localizedText(en, "pairs.m_a10400e2625ebde7")}</p> : !current ? <p role="status">{localizedText(en, "pairs.m_013a5d19df10dc43")}</p> : current.failed || !caps ? <p role="alert">{localizedText(en, "pairs.m_f79a924339a7cbf0")}</p> : <>
      {notice && <p role="status">{localizedText(en, "pairs.m_c61cc3d7a48ee006")}</p>}
      <p>{localizedText(en, "pairs.m_6685390770bf4cab")}: <strong>{record.revision}</strong> · <time dateTime={record.savedAt}>{record.savedAt}</time></p><details><summary>{localizedText(en, "pairs.m_dcfba0c4a6d76b18")}</summary><code>{record.hash}</code></details>
      {!caps.canRecord && !caps.blockers.includes('REVIEWER_IS_RECORD_AUTHOR') && <p>{localizedText(en, "pairs.m_a3430440ebf71ff6")}</p>}
      {caps.blockers.includes('REVIEWER_IS_RECORD_AUTHOR') && <p role="note" data-state="reviewer-is-author">{reviewBlocker('REVIEWER_IS_RECORD_AUTHOR', en)}</p>}
      {!sameRecord && <div role="alert"><p>{localizedText(en, "pairs.m_db34f7f32fb56882")}</p><Button variant="outline" disabled={busy || !!frozen} onClick={onReloadRecord}>{localizedText(en, "pairs.m_2c645170152ddcda")}</Button></div>}
      {!enabled && <p>{localizedText(en, "pairs.m_2c726df11ea5a3c0")}</p>}
      {failure && <p role="alert">{frozen ? (localizedText(en, "pairs.m_5dc4689e941f999b")) : failure.status === 409 ? (localizedText(en, "pairs.m_927989d0657a99df")) : (localizedText(en, "pairs.m_b1180e10b124a6e4"))}</p>}
      <details><summary>{localizedText(en, "pairs.m_25835830dbd79beb")}</summary><p>{localizedText(en, "pairs.m_bc50bc3631241dfd")}: {caps.recordRevision} / {caps.reviewVersion}</p><code>{caps.sourceVersion}</code><ul>{caps.blockers.map((code, i) => <li key={`${code}/${i}`}>{reviewBlocker(code, en)} <code>{code}</code></li>)}</ul>{caps.authorities.map(a => <p key={a.id}>{dailyReviewDuty(a.duty, en)} · {localizedText(en, "pairs.m_3976bfb220e021a4")}: {a.id} / {a.roleId}</p>)}</details>
      {(caps.canRecord || frozen || dirty) && <form noValidate aria-label={localizedText(en, "pairs.m_521b11cdba16154f")} aria-busy={busy} onSubmit={event => { event.preventDefault(); void save(); }}><div className="enterprise-form-grid"><div className="enterprise-form-field"><label htmlFor={`${id}-authority`}>{localizedText(en, "pairs.m_10481f29f0ea76fc")}</label><select id={`${id}-authority`} disabled={!editing} aria-invalid={validation && !authorityId} aria-describedby={validation ? `${id}-errors` : undefined} value={authorityId} onChange={event => { setAuthority(event.target.value); setDecision(''); change(event.target.value, '', rationale); }}><option value="">{localizedText(en, "pairs.m_ee1c14a99fa5369d")}</option>{caps.authorities.map(a => <option key={a.id} value={a.id}>{dailyReviewDuty(a.duty, en)} · {a.id.slice(-8)}</option>)}</select></div><div className="enterprise-form-field"><label htmlFor={`${id}-decision`}>{localizedText(en, "pairs.m_7593f0f6afe5c192")}</label><select id={`${id}-decision`} disabled={!editing || !chosen} aria-invalid={validation && !decision} aria-describedby={validation ? `${id}-errors` : undefined} value={decision} onChange={event => { setDecision(event.target.value as DailyReviewChoice | ''); change(authorityId, event.target.value as DailyReviewChoice | '', rationale); }}><option value="">{localizedText(en, "pairs.m_fa8ff4d04a3ba251")}</option>{chosen?.decisions.map(value => <option key={value} value={value}>{reviewChoice(value, en)}</option>)}</select></div></div><label htmlFor={`${id}-rationale`}>{localizedText(en, "pairs.m_eb3ddf9860d4cb5f")}</label><textarea id={`${id}-rationale`} value={rationale} disabled={!editing} maxLength={2000} rows={4} aria-invalid={validation && !reviewRationale(rationale)} aria-describedby={validation ? `${id}-errors` : undefined} onChange={event => { setRationale(event.target.value); change(authorityId, decision, event.target.value); }}/>{validation && <p id={`${id}-errors`} role="alert">{localizedText(en, "pairs.m_feeb5e6bb5e61e29")}</p>}<EnterpriseActions aria-label={localizedText(en, "pairs.m_86efebdd4b1c4ff4")}><Button variant="outline" disabled={busy || !!frozen} onClick={onReloadRecord}>{localizedText(en, "pairs.m_cbad2741a06caa7e")}</Button><Button type="submit" disabled={!canSend}>{frozen ? (localizedText(en, "pairs.m_3adc27cb6acaef8f")) : busy ? (localizedText(en, "pairs.m_d842639a115f4f96")) : (localizedText(en, "pairs.m_a3bd20fcbe56ed1c"))}</Button></EnterpriseActions></form>}
      <section aria-label={localizedText(en, "pairs.m_fbe875578bfd8237")}><h4>{localizedText(en, "pairs.m_648794475dad05bb")}</h4>{current.history?.items.length ? current.history.items.map(entry => <ReviewEntry key={entry.id} entry={entry} currentRevision={caps.recordRevision} en={en}/>) : <p>{localizedText(en, "pairs.m_1c189c07c2a2b748")}</p>}<EnterprisePagination label={localizedText(en, "pairs.m_9f38457ba37837a8")} previous={localizedText(en, "pairs.m_0011ad0c6e568388")} next={localizedText(en, "pairs.m_ddcef2a4e3c99a19")} previousDisabled={!offset || busy || dirty || !!frozen} nextDisabled={!current.history?.hasMore || offset >= 100000 || busy || dirty || !!frozen} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)}/></section>
    </>}
    <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_c8a0c4f723b0a3db")}><Button variant="outline" disabled={!online || !visible || busy} onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_5e3b9a339e3191e5")}</Button></EnterpriseActions>
  </section>;
}
function ReviewEntry({ entry, currentRevision, en }: {
    entry: DailyReview;
    currentRevision: number;
    en: boolean;
}) { return <details className="daily-review-entry"><summary>{reviewChoice(entry.decision, en)} · {localizedText(en, "pairs.m_5cf7feeda047e695")} {entry.recordRevision} · {entry.savedAt}</summary><Badge tone="warning">{localizedText(en, "pairs.m_e744ae8365f26c4f")}</Badge><p>{entry.recordRevision === currentRevision ? (localizedText(en, "pairs.m_832d726fde833508")) : (localizedText(en, "pairs.m_6701273afff842be"))}</p><p>{dailyReviewDuty(entry.duty, en)}</p><p className="daily-review-rationale">{entry.rationale}</p><time dateTime={entry.savedAt}>{entry.savedAt}</time><details><summary>{localizedText(en, "pairs.m_8d1aea74f0acc536")}</summary><dl className="daily-source-grid"><div><dt>{localizedText(en, "pairs.m_038e0e9d5edec9df")}</dt><dd>{entry.actorId} / {entry.roleId} / {entry.authorityId}</dd></div><div><dt>{localizedText(en, "pairs.m_269ea8fa978b398f")}</dt><dd>{entry.sequence} / {entry.id} / {entry.version}</dd></div><div><dt>{localizedText(en, "pairs.m_d18a3288c1c046b2")}</dt><dd><code>{entry.authorityHash}</code></dd></div><div><dt>{localizedText(en, "pairs.m_78e99af4860b7080")}</dt><dd><code>{entry.recordHash}</code></dd></div><div><dt>{localizedText(en, "pairs.m_5674d277e580b4b7")}</dt><dd><code>{entry.sourceVersion}</code> / <code>{entry.reasonCode}</code></dd></div></dl></details></details>; }
