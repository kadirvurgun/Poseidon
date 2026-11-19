import { localizedLabel } from "../../i18n/messages";
import { localizedText } from "../../i18n/messages";
import { type Locale as AppLocale } from "../../i18n";
import { useCallback, useEffect, useId, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { EnterpriseActions } from '../EnterprisePatterns';
import { ApiError } from '../api';
import { dailyFields, type DailyFields, type DailyKind, type DailyValue } from '../daily-report-fields';
import type { DailyRecord } from '../daily-operations-api';
import { DailyOfflineApplication, dailyOfflineInventory, dailyOfflineLifecycle, discardDailyLostCaptures } from './daily-offline-application';
import { replayGate, type SyncState } from '../sync-operations';
import { SyncQueueNotice } from '../SyncQueueNotice';
import { projectDailyQueue, type DailyPending, type DailyConflict, type DailyInventoryItem, type DailyOfflineContext } from './daily-offline-contract';
import './daily-offline.css';
type Props = {
    vessel: string;
    kind: DailyKind;
    recordId: string;
    record: DailyRecord | null;
    fields: DailyFields;
    enabled: boolean;
    language: AppLocale;
    onDirty: (value: boolean) => void;
    onSaving: (value: boolean) => void;
    onCommitted: () => void;
};
export function DailyOfflinePanel({ vessel, kind, recordId, record, fields, enabled, language, onDirty, onSaving, onCommitted }: Props) {
    const fieldId = useId();
    const en = language === 'en', app = useRef<DailyOfflineApplication | null>(null), epoch = useRef(0), running = useRef(false), request = useRef<AbortController | null>(null);
    const [context, setContext] = useState<DailyOfflineContext | null>(null), [active, setActive] = useState(false), [online, setOnline] = useState(navigator.onLine), [busy, setBusy] = useState(false);
    const [contexts, setContexts] = useState<DailyInventoryItem[]>([]), [selected, setSelected] = useState(''), [captures, setCaptures] = useState<DailyPending[]>([]);
    const [local, setLocal] = useState<DailyFields>({}), [dirty, setDirty] = useState(false), [retained, setRetained] = useState(false), [conflict, setConflict] = useState<DailyConflict | null>(null);
    const [status, setStatus] = useState<'locked' | 'ready' | 'unavailable' | 'retained' | 'unknown' | 'keyLost'>('locked'), [confirmPurge, setConfirmPurge] = useState(false);
    const callbacks = useRef({ onDirty, onSaving, onCommitted });
    const retainedDraft = useRef<{
        context: DailyOfflineContext;
        fields: DailyFields;
    } | null>(null);
    const privateState = useRef({ local, dirty, pending: false });
    const queue = useMemo(() => projectDailyQueue(captures, context), [captures, context]);
    // DEV-034 reconnect reconciliation states; never persisted, never shown as saved.
    const [syncStates, setSyncStates] = useState<ReadonlyMap<string, SyncState>>(new Map());
    // DEV-033 ordered per resource (this daily record): only the oldest unresolved capture may be replayed.
    const replayOrder = useMemo(() => replayGate(captures, recordId, syncStates), [captures, recordId, syncStates]);
    const [memoryHeld, setMemoryHeld] = useState(false);
    useLayoutEffect(() => { privateState.current = { local, dirty, pending: queue.pending > 0 }; }, [local, dirty, queue.pending]);
    useLayoutEffect(() => { callbacks.current = { onDirty, onSaving, onCommitted }; }, [onDirty, onSaving, onCommitted]);
    const withdraw = useCallback(() => {
        const previous = app.current;
        if (previous && privateState.current.dirty)
            retainedDraft.current = { context: previous.context, fields: structuredClone(privateState.current.local) };
        const hasRetained = !!retainedDraft.current || privateState.current.pending || !!previous && running.current;
        if (hasRetained) {
            setRetained(true);
            callbacks.current.onDirty(true);
        }
        setMemoryHeld(!!retainedDraft.current);
        epoch.current++;
        request.current?.abort();
        previous?.close();
        app.current = null;
        setActive(false);
        setContext(null);
        setLocal({});
        setCaptures([]);
        setSyncStates(new Map());
        setConflict(null);
        setBusy(false);
        setDirty(false);
        setStatus('retained');
        running.current = false;
        callbacks.current.onSaving(false);
    }, []);
    useEffect(() => { callbacks.current.onDirty(dirty || retained || queue.pending > 0); return () => callbacks.current.onDirty(false); }, [dirty, retained, queue.pending]);
    useEffect(() => { callbacks.current.onSaving(busy); return () => callbacks.current.onSaving(false); }, [busy]);
    useEffect(() => {
        const lifetime = epoch;
        const network = () => {
            setOnline(navigator.onLine);
            if (navigator.onLine)
                withdraw();
        };
        const hidden = () => {
            if (document.hidden)
                withdraw();
        };
        window.addEventListener('online', network);
        window.addEventListener('offline', network);
        window.addEventListener('pagehide', withdraw);
        document.addEventListener('visibilitychange', hidden);
        return () => { lifetime.current++; request.current?.abort(); app.current?.close(); window.removeEventListener('online', network); window.removeEventListener('offline', network); window.removeEventListener('pagehide', withdraw); document.removeEventListener('visibilitychange', hidden); callbacks.current.onDirty(false); callbacks.current.onSaving(false); };
    }, [withdraw, vessel, kind, recordId]);
    useEffect(() => {
        if (!enabled)
            withdraw();
    }, [enabled, withdraw]);
    useEffect(() => {
        if (!active || !app.current)
            return;
        const timer = setTimeout(withdraw, app.current.remaining());
        return () => clearTimeout(timer);
    }, [active, withdraw]);
    async function run(work: (signal: AbortSignal, current: number) => Promise<void>, replay = false) {
        if (running.current || !enabled)
            return;
        running.current = true;
        setBusy(true);
        callbacks.current.onSaving(true);
        const current = ++epoch.current, controller = new AbortController();
        request.current?.abort();
        request.current = controller;
        const timer = setTimeout(() => controller.abort(), 20000);
        try {
            await work(controller.signal, current);
        }
        catch (error) {
            if (current !== epoch.current)
                return;
            if (error instanceof ApiError && [401, 403, 404].includes(error.status)) {
                setRetained(true);
                withdraw();
                setStatus('unavailable');
            }
            else {
                if (replay)
                    setRetained(true);
                setStatus(error instanceof Error && error.message === 'key_lost_data_retained' ? 'keyLost' : replay ? 'unknown' : 'unavailable');
            }
        }
        finally {
            clearTimeout(timer);
            if (current === epoch.current) {
                running.current = false;
                setBusy(false);
                callbacks.current.onSaving(false);
            }
        }
    }
    const fresh = (current: number, signal: AbortSignal) => current === epoch.current && !signal.aborted && enabled;
    const inventory = () => run(async (signal, current) => {
        const rows = await dailyOfflineInventory(kind, vessel, recordId, signal);
        if (fresh(current, signal))
            setContexts(rows);
    });
    const authorize = () => run(async (signal, current) => {
        const previous = app.current;
        if (previous && privateState.current.dirty) {
            retainedDraft.current = { context: previous.context, fields: structuredClone(privateState.current.local) };
            setMemoryHeld(true);
        }
        previous?.close();
        app.current = null;
        setActive(false);
        setContext(null);
        setLocal({});
        setCaptures([]);
        setSyncStates(new Map());
        setConflict(null);
        const value = await DailyOfflineApplication.authorize(kind, vessel, recordId, selected || null, signal);
        try {
            let reconciled: ReadonlyMap<string, SyncState> = new Map();
            if (navigator.onLine) {
                // Lost replay responses are recovered from server receipts without re-sending.
                // Failure is non-fatal: unproven captures simply remain pending.
                try { reconciled = await value.reconcile(signal); }
                catch (error) { if (signal.aborted) throw error; }
            }
            const rows = await value.captures();
            if (!fresh(current, signal)) {
                value.close();
                return;
            }
            const held = retainedDraft.current;
            const same = held && held.context.id === value.context.id && held.context.actorId === value.context.actorId && held.context.tenantId === value.context.tenantId && held.context.recordId === value.context.recordId && held.context.kind === value.context.kind && held.context.policyDigest === value.context.policyDigest && held.context.authorityDigest === value.context.authorityDigest && held.context.schemaHash === value.context.schemaHash;
            if (held && !same) {
                value.close();
                throw new Error('original_unsaved_context_required');
            }
            value.onWithdraw(withdraw);
            app.current = value;
            setSelected(value.context.id);
            setCaptures(rows);
            setSyncStates(reconciled);
            setContext(value.context);
            setActive(true);
            setDirty(!!same);
            setRetained(false);
            setMemoryHeld(false);
            setStatus('ready');
            setLocal(same && held ? held.fields : Object.fromEntries(value.context.allowedFields.map(key => [key, fields[key] ?? value.context.baseFields[key]])));
            retainedDraft.current = null;
        }
        catch (error) {
            value.close();
            throw error;
        }
    });
    const capture = () => run(async (_signal, current) => {
        const value = app.current;
        if (!value?.active())
            throw new Error('reauthorization_required_data_retained');
        const rows = await value.capture(local);
        if (current !== epoch.current)
            return;
        setCaptures(rows);
        setDirty(false);
        setRetained(false);
        setStatus('ready');
    });
    const replay = (row: DailyPending) => run(async (signal, current) => {
        const value = app.current;
        if (!value?.active())
            throw new Error('reauthorization_required_data_retained');
        const outcome = await value.replay(row, signal);
        const rows = await value.captures();
        if (!fresh(current, signal))
            return;
        setCaptures(rows);
        setSyncStates(previous => { const next = new Map(previous); next.delete(row.commandId); return next; });
        setRetained(false);
        setStatus('ready');
        if (outcome.result === 'MaterialConflict' && !projectDailyQueue(rows, value.context).resolved.has(row.commandId))
            setConflict(await value.conflict(row.commandId, signal));
        else {
            setConflict(null);
            if (outcome.result !== 'MaterialConflict')
                callbacks.current.onCommitted();
        }
    }, true);
    const resolve = (choice: 'keepServer' | 'useCaptured') => run(async (signal, current) => {
        const value = app.current;
        if (!value?.active() || !conflict)
            return;
        // This creates a retained child command. Sending it remains a separate,
        // explicit action, and the server rechecks the exact current version.
        const rows = await value.capture({}, { conflict, choice });
        if (fresh(current, signal)) {
            setCaptures(rows);
            setConflict(null);
            setStatus('ready');
        }
    });
    const label = (key: string) => { const field = dailyFields(kind).find(f => f.key === key); return field ? localizedLabel(field, language) : key; };
    const viewValue = (value: DailyValue) => value.state === 'Known' ? value.value : value.state === 'Unknown' ? (localizedText(en, "pairs.m_a5a7675bd7000dbd")) : (localizedText(en, "pairs.m_a5bd35d90de7c160"));
    const locked = !enabled || busy, current = enabled && document.visibilityState !== 'hidden' && active ? context : null;
    return <section className="daily-offline-panel" aria-label={localizedText(en, "pairs.m_5950600927ffd3a3")}>
    <header><h3>{localizedText(en, "pairs.m_993da089144c390b")}</h3><Badge tone={current ? 'info' : 'warning'}>{current ? (localizedText(en, "pairs.m_35c9eb665eb94b88")) : (localizedText(en, "pairs.m_bb4db3811f56c9db"))}</Badge></header>
    <p>{localizedText(en, "pairs.m_fad2badd34c2b29c")}</p>
    {status === 'unavailable' && <p role="alert">{localizedText(en, "pairs.m_0a84480f39751cdf")}</p>}
    {status === 'keyLost' && <p role="alert">{localizedText(en, "pairs.m_2a3d2d437bee53b0")}</p>}
    {status === 'unknown' && <p role="alert">{localizedText(en, "pairs.m_4243c4ab8bfb53e2")}</p>}
    {memoryHeld && <p role="status">{localizedText(en, "pairs.m_9e3b4b576af68c9a")}</p>}
    {!online && <p role="status">{localizedText(en, "pairs.m_0818b3f7139034c8")}</p>}
    <label>{localizedText(en, "pairs.m_15d4c36888498f0d")}<select value={selected} disabled={locked || !!current || memoryHeld} onChange={event => setSelected(event.target.value)}><option value="">{localizedText(en, "pairs.m_f01f704ae6e29dc7")}</option>{selected && !contexts.some(c => c.id === selected) && <option value={selected}>{localizedText(en, "pairs.m_954cfca6632f69a9")}</option>}{contexts.map((c, i) => <option key={c.id} value={c.id}>{localizedText(en, "pairs.m_20c3e8f064febfde")} {i + 1} · {c.revokedAt ? (localizedText(en, "pairs.m_e4b5dd2deec128b7")) : c.expiresAt}</option>)}</select></label>
    <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_f99f58bfa9e82d0d")}>
      <Button variant="outline" disabled={locked || !online} onClick={inventory}>{localizedText(en, "pairs.m_9b91db9e2565d151")}</Button>
      <Button disabled={locked || !online} onClick={authorize}>{selected ? (localizedText(en, "pairs.m_deb349842f3f1dcf")) : (localizedText(en, "pairs.m_d2bde0cd4f6dc5ef"))}</Button>
      {current && <Button variant="outline" disabled={busy} onClick={withdraw}>{localizedText(en, "pairs.m_04bad7ddb44ee8fe")}</Button>}
    </EnterpriseActions>
    {current && <>
      <p>{localizedText(en, "pairs.m_50cb61556eba162c")}: {current.baseRevision}. {record && record.version !== current.baseVersion ? (localizedText(en, "pairs.m_c7f7c24de4fc0ad8")) : ''}</p>
      <p>{localizedText(en, "pairs.m_7161128b835b028e")}</p>
      <div className="daily-offline-fields">{current.allowedFields.map(key => {
                const value = local[key] ?? { state: 'Missing' as const, value: null }, metadata = dailyFields(kind).find(f => f.key === key);
                return <div className="daily-offline-field" key={key}><label htmlFor={`${fieldId}-${key}`}>{label(key)}</label><select aria-label={`${label(key)} — ${localizedText(en, "pairs.m_8e65114b82841417")}`} value={value.state} disabled={locked} onChange={event => { setLocal(v => ({ ...v, [key]: { state: event.target.value as DailyValue['state'], value: event.target.value === 'Known' ? '' : null } })); setDirty(true); }}><option value="Missing">{localizedText(en, "pairs.m_a5bd35d90de7c160")}</option><option value="Unknown">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option><option value="Known" disabled={metadata?.kind === 'reference'}>{localizedText(en, "pairs.m_23f42eba6cf47e2c")}</option></select>{metadata?.kind === 'enum' ? <select id={`${fieldId}-${key}`} aria-label={label(key)} value={value.value ?? ''} disabled={locked || value.state !== 'Known'} onChange={event => { setLocal(v => ({ ...v, [key]: { state: 'Known', value: event.target.value } })); setDirty(true); }}><option value="">{localizedText(en, "pairs.m_d00c0e351f67fbbb")}</option>{metadata.options?.map(option => <option key={option}>{option}</option>)}</select> : <textarea id={`${fieldId}-${key}`} aria-label={label(key)} value={value.value ?? ''} disabled={locked || value.state !== 'Known' || metadata?.kind === 'reference'} maxLength={metadata?.kind === 'narrative' ? 4000 : 512} rows={metadata?.kind === 'narrative' ? 3 : 1} onChange={event => { setLocal(v => ({ ...v, [key]: { state: 'Known', value: event.target.value } })); setDirty(true); }}/>}</div>;
            })}</div>
      <EnterpriseActions align="start"><Button disabled={locked} onClick={capture}>{localizedText(en, "pairs.m_c81cedc503a88202")}</Button></EnterpriseActions>
      <p role="status">{localizedText(en, "pairs.m_11b6f5fa3d06cbe1", { value0: queue.pending })}</p>
      <ul className="daily-offline-queue">{captures.map((row, i) => <li key={row.commandId}><strong>{localizedText(en, "pairs.m_78fd146a51f0c965")} {i + 1}</strong><span>{queue.resolved.has(row.commandId) ? (localizedText(en, "pairs.m_f5f55139c8e905b3")) : row.outcome?.result === 'Committed' ? (localizedText(en, "pairs.m_cc0248a386633984")) : row.outcome?.result === 'ServerRetained' ? (localizedText(en, "pairs.m_b532c1458119b91d")) : row.outcome ? (localizedText(en, "pairs.m_555548196da154f7")) : (localizedText(en, "pairs.m_dfbae211988617ec"))}</span><SyncQueueNotice state={replayOrder.get(row.commandId)?.state} en={en}/><EnterpriseActions align="start"><Button variant="outline" disabled={locked || !online || replayOrder.get(row.commandId)?.state === 'blocked' || replayOrder.get(row.commandId)?.state === 'rejected'} onClick={() => replay(row)}>{row.outcome ? (localizedText(en, "pairs.m_ce40179e4a307296")) : (localizedText(en, "pairs.m_7d7cc3db0f6a6ed5"))}</Button>{row.outcome?.result === 'MaterialConflict' && !queue.resolved.has(row.commandId) && <Button variant="outline" disabled={locked || !online} onClick={() => void run(async (signal, n) => {
                        const value = app.current;
                        if (!value?.active())
                            return;
                        const detail = await value.conflict(row.commandId, signal);
                        if (fresh(n, signal))
                            setConflict(detail);
                    })}>{localizedText(en, "pairs.m_f451d02b41d61b8f")}</Button>}</EnterpriseActions></li>)}</ul>
      {conflict && <section aria-label={localizedText(en, "pairs.m_305dcf4d7e5b57c9")}><h4>{localizedText(en, "pairs.m_ae78a6cb7ba00dd9")}</h4><p>{localizedText(en, "pairs.m_e00760d470f8ea89")}</p><table><thead><tr><th>{localizedText(en, "pairs.m_f06cb94166ed86f7")}</th><th>{localizedText(en, "pairs.m_45d6f687dabba870")}</th><th>{localizedText(en, "pairs.m_b9441f91545d6179")}</th><th>{localizedText(en, "pairs.m_20d3c06469870bdc")}</th></tr></thead><tbody>{conflict.fields.map(f => <tr key={f.key}><th>{label(f.key)}</th><td>{viewValue(f.base)}</td><td>{viewValue(f.captured)}</td><td>{viewValue(f.server)}</td></tr>)}</tbody></table>{!conflict.resolved && !conflict.choices.length && <p role="status">{localizedText(en, "pairs.m_baae5942fc9c89b5")}</p>}<EnterpriseActions align="start">{conflict.choices.map(choice => <Button key={choice} disabled={locked || !online} onClick={() => resolve(choice)}>{choice === 'keepServer' ? (localizedText(en, "pairs.m_28197c892d9b4de0")) : (localizedText(en, "pairs.m_1e932ae6f2b92d27"))}</Button>)}</EnterpriseActions></section>}
    </>}
    {selected && <details><summary>{localizedText(en, "pairs.m_34493e5b412400e5")}</summary><EnterpriseActions align="start">{(['revoke', 'lost'] as const).map(action => <Button key={action} variant="outline" disabled={locked || !online} onClick={() => void run(async (signal, n) => {
                    await dailyOfflineLifecycle(kind, vessel, recordId, selected, action, signal);
                    if (fresh(n, signal)) {
                        setRetained(true);
                        withdraw();
                    }
                })}>{action === 'lost' ? (localizedText(en, "pairs.m_2c093773087b92d5")) : (localizedText(en, "pairs.m_784a8efd04e53901"))}</Button>)}</EnterpriseActions><label><input type="checkbox" checked={confirmPurge} disabled={locked} onChange={event => setConfirmPurge(event.target.checked)}/>{localizedText(en, "pairs.m_554a8daad379b75d")}</label><Button variant="outline" disabled={locked || !online || !confirmPurge} onClick={() => void run(async (signal, n) => {
                await discardDailyLostCaptures(kind, vessel, recordId, selected, true, signal);
                if (fresh(n, signal)) {
                    retainedDraft.current = null;
                    withdraw();
                    setMemoryHeld(false);
                    setRetained(false);
                    setConfirmPurge(false);
                }
            })}>{localizedText(en, "pairs.m_741d52582cc0e908")}</Button></details>}
  </section>;
}
