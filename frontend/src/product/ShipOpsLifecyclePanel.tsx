import { localizedCopy } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useId, useRef, useState } from 'react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { ApiError, session as readSession } from './api';
import { freezeDowntimeLifecycle, freezeSofFinalize, opsLifecycleApi, reviewActions, type DowntimeLifecycleView, type OpsLifecycleAttempt, type SofFinalization } from './ship-ops-lifecycle-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
const copy = localizedCopy('ShipOpsLifecyclePanel_tsx_copy');
type Props = { vessel: string; id: string; revision: number; language: AppLocale; disabled?: boolean; onCommitted: () => void; onSaving?: (value: boolean) => void };
// Shared command runner: one frozen attempt (exact key/body) per human act; an
// unknown outcome keeps the same attempt so a retry can only replay it once.
function useCommand(onCommitted: () => void, onSaving?: (value: boolean) => void) {
    const [frozen, setFrozen] = useState<OpsLifecycleAttempt | null>(null), [busy, setBusy] = useState(false), [failure, setFailure] = useState<string | null>(null), [done, setDone] = useState(false);
    const mounted = useRef(true);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
    async function run(attempt: OpsLifecycleAttempt) {
        if (busy) return;
        setBusy(true); setFrozen(attempt); setFailure(null); setDone(false); onSaving?.(true);
        const c = new AbortController(), timer = setTimeout(() => c.abort(), 20000);
        try {
            const user = await readSession(c.signal);
            await opsLifecycleApi.send(attempt, user.csrfToken, c.signal);
            if (!mounted.current) return;
            setFrozen(null); setDone(true); onSaving?.(false); onCommitted();
        } catch (error) {
            const problem = error instanceof ApiError ? error : new ApiError(0);
            const unknown = problem.status === 0 || problem.status >= 500;
            if (!mounted.current) return;
            if (!unknown) setFrozen(null);
            setFailure(unknown ? 'outcome_unknown' : problem.code ?? String(problem.status));
            onSaving?.(unknown);
        } finally { clearTimeout(timer); if (mounted.current) setBusy(false); }
    }
    return { frozen, busy, failure, done, run };
}
function useView<T>(load: (signal: AbortSignal) => Promise<T>, scope: string) {
    const epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [state, setState] = useState<{ scope: string; value?: T; failed?: boolean } | null>(null);
    const key = `${scope}/${epoch}/${attempt}`, online = navigator.onLine;
    useEffect(() => {
        if (!online) return;
        const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000);
        load(c.signal).then(value => { if (active) setState({ scope: key, value }); }, () => { if (active) setState({ scope: key, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [key, online]);
    return { current: online && state?.scope === key ? state : null, online, retry: () => retry(n => n + 1) };
}
function Status({ t, command }: { t: (typeof copy)['en']; command: ReturnType<typeof useCommand> }) {
    return <>{command.done && <p role="status">{t.done}</p>}{command.failure && <p role="alert">{command.failure === 'outcome_unknown' ? t.unknownOutcome : t.failed.replace('{{code}}', command.failure)}</p>}</>;
}

// DEV-101: SOF finalization by an appointed human; never a signature or external agreement.
export function SofFinalizationPanel({ vessel, id, revision, language, disabled, onCommitted, onSaving }: Props) {
    const t = copy[language], uid = useId(), command = useCommand(onCommitted, onSaving);
    const { current, online, retry } = useView<SofFinalization>(signal => opsLifecycleApi.sof(vessel, id, signal), `${vessel}/${id}/${revision}`);
    const [authority, setAuthority] = useState(''), [acknowledged, setAcknowledged] = useState<string[]>([]);
    const view = current?.value;
    const body = !online ? <p role="status">{t.offline}</p> : !current ? <p role="status">{t.loading}</p>
        : current.failed || !view ? <div role="alert"><p>{t.unavailable}</p><Button variant="outline" onClick={retry}>{t.retry}</Button></div>
            : <>
                <p><Badge tone={view.sofState === 'FINAL' ? 'info' : 'neutral'}>{view.sofState === 'FINAL' ? t.final : t.draft}</Badge> <Badge tone="warning">{t.signature}</Badge></p>
                {view.sofState === 'DRAFT' && view.finalRevision !== null && <p role="note">{t.amended}</p>}
                {view.sofState === 'DRAFT' && view.missingRequired.length > 0 && <p role="note">{t.missing.replace('{{fields}}', view.missingRequired.join(', '))}</p>}
                {view.sofState === 'DRAFT' && view.missingRequired.length === 0 && (view.authorities.length === 0 ? <p>{t.noAuthority}</p> : <fieldset disabled={disabled || command.busy || !!command.frozen}>
                    <legend>{t.unresolved}</legend>
                    {view.unresolvedReferences.map(key => <label key={key}><input type="checkbox" checked={acknowledged.includes(key)} onChange={event => setAcknowledged(previous => event.target.checked ? [...previous, key] : previous.filter(k => k !== key))}/> <code>{key}</code></label>)}
                    <label htmlFor={uid}>{t.authority}</label>
                    <select id={uid} value={authority} onChange={event => setAuthority(event.target.value)}><option value="">—</option>{view.authorities.map(a => <option key={a.id} value={a.id}>{a.duty}</option>)}</select>
                    <Button disabled={!view.canFinalize || !authority || acknowledged.length !== view.unresolvedReferences.length} onClick={() => { try { void command.run(freezeSofFinalize(view, authority, acknowledged)); } catch { /* invalid input stays local */ } }}>{t.finalize}</Button>
                </fieldset>)}
                {command.frozen && !command.busy && <Button variant="outline" onClick={() => void command.run(command.frozen!)}>{t.retry}</Button>}
                <Status t={t} command={command}/>
                {view.events.length > 0 && <details><summary>{t.history}</summary><ol>{view.events.map(e => <li key={e.id}>r{e.toRevision} · {e.duty} · <time dateTime={e.savedAt}>{e.savedAt}</time> · <code>{e.actorId}</code> · <code>{e.signatureState}</code></li>)}</ol></details>}
            </>;
    return <section className="ship-ops-lifecycle" aria-label={t.sofTitle}><h2>{t.sofTitle}</h2><p className="ship-operations-hint">{t.sofIntro}</p>{body}</section>;
}

// DEV-102: downtime review/closure with separation of duties; LoH is a candidate only.
export function DowntimeLifecyclePanel({ vessel, id, revision, language, disabled, onCommitted, onSaving }: Props) {
    const t = copy[language], uid = useId(), command = useCommand(onCommitted, onSaving);
    const { current, online, retry } = useView<DowntimeLifecycleView>(signal => opsLifecycleApi.downtime(vessel, id, signal), `${vessel}/${id}/${revision}`);
    const [authority, setAuthority] = useState(''), [evidence, setEvidence] = useState('');
    const view = current?.value, locked = disabled || command.busy || !!command.frozen;
    const act = (action: string, pin: { id: string; revision: number } | null = null) => {
        if (!view) return;
        try { void command.run(freezeDowntimeLifecycle(view, action, reviewActions.includes(action) ? authority || null : null, pin)); } catch { /* invalid input stays local */ }
    };
    const body = !online ? <p role="status">{t.offline}</p> : !current ? <p role="status">{t.loading}</p>
        : current.failed || !view ? <div role="alert"><p>{t.unavailable}</p><Button variant="outline" onClick={retry}>{t.retry}</Button></div>
            : <>
                <dl className="ship-operations-summary-grid">
                    <div><dt>{t.state}</dt><dd><Badge tone={view.state === 'IN_REVIEW' ? 'warning' : 'neutral'}>{t.states[view.state]}</Badge></dd></div>
                    <div><dt>{t.loh}</dt><dd><Badge tone={view.lossOfHireCandidate === 'REVIEW_REQUIRED' ? 'warning' : 'neutral'}>{view.lossOfHireCandidate === 'REVIEW_REQUIRED' ? t.lohReview : t.lohUnknown}</Badge></dd></div>
                </dl>
                {view.missingRequired.length > 0 && <p role="note">{t.missing.replace('{{fields}}', view.missingRequired.join(', '))}</p>}
                {view.actorIsRecorder && <p role="note">{t.recorderNote}</p>}
                <h3>{t.evidence}</h3>
                {view.claimEvidence.length === 0 ? <p>{t.noEvidence}</p> : <ul>{view.claimEvidence.map(pin => <li key={pin.id}><code>{pin.id}</code> · r{pin.revision}{view.allowedActions.includes('UNLINK_CLAIM_EVIDENCE') && <> <Button variant="outline" disabled={locked} onClick={() => act('UNLINK_CLAIM_EVIDENCE', pin)}>{t.unlink}</Button></>}</li>)}</ul>}
                {view.allowedActions.includes('LINK_CLAIM_EVIDENCE') && view.evidenceOptions.length > 0 && <p>
                    <label htmlFor={uid + 'e'}>{t.selectEvidence}</label>
                    <select id={uid + 'e'} value={evidence} disabled={locked} onChange={event => setEvidence(event.target.value)}><option value="">—</option>{view.evidenceOptions.map(o => <option key={o.id} value={o.id}>{o.label}</option>)}</select>
                    <Button variant="outline" disabled={locked || !evidence} onClick={() => { const o = view.evidenceOptions.find(x => x.id === evidence); if (o) act('LINK_CLAIM_EVIDENCE', { id: o.id, revision: o.revision }); }}>{t.link}</Button>
                </p>}
                {view.authorities.length > 0 && view.allowedActions.some(a => reviewActions.includes(a)) && <p><label htmlFor={uid}>{t.authority}</label>
                    <select id={uid} value={authority} disabled={locked} onChange={event => setAuthority(event.target.value)}><option value="">—</option>{view.authorities.map(a => <option key={a.id} value={a.id}>{a.duty}</option>)}</select></p>}
                <p>{(['SUBMIT_FOR_REVIEW', 'RETURN_FOR_CORRECTION', 'VERIFY', 'CLOSE'] as const).filter(a => view.allowedActions.includes(a)).map(a =>
                    <Button key={a} variant={a === 'SUBMIT_FOR_REVIEW' ? 'default' : 'outline'} disabled={locked || reviewActions.includes(a) && !authority || a !== 'RETURN_FOR_CORRECTION' && view.missingRequired.length > 0} onClick={() => act(a)}>{t.actions[a]}</Button>)}</p>
                {command.frozen && !command.busy && <Button variant="outline" onClick={() => void command.run(command.frozen!)}>{t.retry}</Button>}
                <Status t={t} command={command}/>
                {view.events.length > 0 && <details><summary>{t.history}</summary><ol>{view.events.map(e => <li key={e.id}>r{e.toRevision} · {t.actions[e.action as keyof typeof t.actions] ?? e.action}{e.duty ? ' · ' + e.duty : ''} · <time dateTime={e.savedAt}>{e.savedAt}</time> · <code>{e.actorId}</code></li>)}</ol></details>}
            </>;
    return <section className="ship-ops-lifecycle" aria-label={t.dtTitle}><h2>{t.dtTitle}</h2><p className="ship-operations-hint">{t.dtIntro}</p>{body}</section>;
}
