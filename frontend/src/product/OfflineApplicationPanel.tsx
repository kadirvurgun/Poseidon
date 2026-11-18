import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import { OfflineCaptureMetadata, OfflineQueueSummary } from './OfflineQueueSummary';
import { useEffectEvent, useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import { OfflineConflictReview, type OfflineConflictResolution, type OfflineConflictValue } from './OfflineConflictReview';
import { OfflineApplication, inventory, discardLostCaptures, prepareOfflineShell, type OfflineInventory, type PendingCapture, type OfflineConflictDetail, type OfflineOutcome } from './offline-application';
export function OfflineApplicationPanel({ vessel, assignment, language, onDirty }: {
    vessel: string;
    assignment: string;
    language: AppLocale;
    onDirty: (value: boolean) => void;
}) {
    const en = language === 'en';
    const app = useRef<OfflineApplication | null>(null), generation = useRef(0), request = useRef<AbortController | null>(null);
    const [contexts, setContexts] = useState<OfflineInventory['items']>([]), [selected, setSelected] = useState('');
    const [active, setActive] = useState(false), [online, setOnline] = useState(navigator.onLine), [busy, setBusy] = useState(false);
    const [status, setStatus] = useState<'locked' | 'ready' | 'retained' | 'failed' | 'denied' | 'keyLost'>('locked');
    const [captures, setCaptures] = useState<PendingCapture[]>([]), [watch, setWatch] = useState(''), [dirty, setDirty] = useState(false);
    const [snapshot, setSnapshot] = useState<{
        watchGroup: string | null;
        baseVersion: number;
        revision: number;
    } | null>(null);
    const [conflict, setConflict] = useState<OfflineConflictDetail | null>(null), [history, setHistory] = useState<OfflineOutcome[]>([]);
    const [historyOffset, setHistoryOffset] = useState(0), [historyMore, setHistoryMore] = useState(false);
    const [confirmDiscard, setConfirmDiscard] = useState(false), [uncertain, setUncertain] = useState(false);
    const [lease, setLease] = useState(0);
    const [authorizedContext, setAuthorizedContext] = useState<OfflineApplication['context'] | null>(null);
    const withdraw = useCallback(() => {
        generation.current++;
        request.current?.abort();
        app.current?.close();
        app.current = null;
        setActive(false);
        setAuthorizedContext(null);
        setBusy(false);
        setCaptures([]);
        setConflict(null);
        setHistory([]);
        setHistoryOffset(0);
        setHistoryMore(false);
        setSnapshot(null);
        setStatus('retained');
        // Unsaved in-memory editor is retained while hidden, but never exposed after scope remount.
    }, []);
    useEffect(() => { onDirty(dirty || busy); return () => onDirty(false); }, [dirty, busy, onDirty]);
    const closeScope = useEffectEvent(() => { generation.current++; request.current?.abort(); app.current?.close(); });
    useEffect(() => {
        const hidden = () => { if (document.hidden)
            withdraw(); };
        let observedOnline = navigator.onLine;
        const changed = () => {
            observedOnline = navigator.onLine;
            setOnline(observedOnline);
            // A real online event remains a conservative withdrawal signal.
            if (observedOnline)
                withdraw();
        };
        const synchronizeHint = () => {
            const currentOnline = navigator.onLine;
            const reconnected = currentOnline && !observedOnline;
            observedOnline = currentOnline;
            setOnline(currentOnline);
            // Some browsers update the native hint after offline bootstrap without
            // emitting online. This local check grants no authority or network read.
            if (reconnected)
                withdraw();
        };
        const pagehide = () => withdraw();
        window.addEventListener('online', changed);
        window.addEventListener('offline', changed);
        window.addEventListener('pagehide', pagehide);
        document.addEventListener('visibilitychange', hidden);
        const networkHintTimer = window.setInterval(synchronizeHint, 1000);
        return () => {
            closeScope();
            window.clearInterval(networkHintTimer);
            window.removeEventListener('online', changed);
            window.removeEventListener('offline', changed);
            window.removeEventListener('pagehide', pagehide);
            document.removeEventListener('visibilitychange', hidden);
        };
    }, [vessel, assignment, withdraw]);
    useEffect(() => {
        const authorized = app.current;
        if (!active || !authorized)
            return;
        // This timer WITHDRAWS expired access. It never contacts the server or renews authorization.
        const timer = setTimeout(() => { if (app.current === authorized)
            withdraw(); }, authorized.remaining());
        return () => clearTimeout(timer);
    }, [active, lease, withdraw]);
    async function run(work: (signal: AbortSignal, current: number) => Promise<void>) {
        if (busy)
            return;
        const current = ++generation.current;
        request.current?.abort();
        const controller = new AbortController();
        request.current = controller;
        setBusy(true);
        try {
            await work(controller.signal, current);
        }
        catch (error) {
            if (current !== generation.current || controller.signal.aborted)
                return;
            if (error instanceof ApiError && [401, 403, 404].includes(error.status)) {
                withdraw();
                setWatch('');
                setDirty(false);
                setStatus('denied');
            }
            else {
                setStatus(error instanceof Error && error.message === 'key_lost_data_retained' ? 'keyLost' : 'failed');
                setUncertain(true);
            }
        }
        finally {
            if (current === generation.current)
                setBusy(false);
        }
    }
    const fresh = (current: number, signal: AbortSignal) => current === generation.current && !signal.aborted;
    const authorize = () => run(async (signal, current) => {
        const previous = app.current;
        app.current = null;
        previous?.close();
        setActive(false);
        setAuthorizedContext(null);
        setCaptures([]);
        setSnapshot(null);
        setConflict(null);
        setHistory([]);
        const value = await OfflineApplication.authorize(vessel, assignment, selected || null, signal);
        if (!fresh(current, signal)) {
            value.close();
            return;
        }
        let rows: PendingCapture[], base: {
            watchGroup: string | null;
            baseVersion: number;
            revision: number;
        };
        try {
            await prepareOfflineShell(signal);
            rows = await value.captures();
            base = await value.snapshot();
        }
        catch (error) {
            value.close();
            throw error;
        }
        if (!fresh(current, signal)) {
            value.close();
            return;
        }
        app.current = value;
        setAuthorizedContext(value.context);
        setSelected(value.context.id);
        setCaptures(rows);
        setSnapshot(base);
        value.onWithdraw(() => { if (app.current === value)
            withdraw(); });
        setWatch(previous => dirty ? previous : base.watchGroup ?? '');
        setActive(true);
        setLease(n => n + 1);
        setStatus('ready');
        setUncertain(false);
        setConfirmDiscard(false);
    });
    const refresh = () => run(async (signal, current) => {
        const result = await inventory(vessel, assignment, signal);
        if (fresh(current, signal)) {
            setContexts(result.items);
            setStatus('retained');
        }
    });
    const capture = () => run(async (signal, current) => {
        const value = app.current;
        if (!value)
            return;
        await value.capture(watch || null);
        const rows = await value.captures();
        if (fresh(current, signal)) {
            setCaptures(rows);
            setDirty(false);
            setStatus('ready');
        }
    });
    const replay = (row: PendingCapture) => run(async (signal, current) => {
        const value = app.current;
        if (!value)
            return;
        await value.replay(row, signal);
        const rows = await value.captures();
        if (fresh(current, signal)) {
            setCaptures(rows);
            setStatus('ready');
            setUncertain(false);
        }
    });
    const review = (id: string) => run(async (signal, current) => {
        const value = app.current;
        if (!value)
            return;
        const result = await value.conflict(id, signal);
        if (fresh(current, signal)) {
            setConflict(result);
            setUncertain(false);
        }
    });
    const resolve = (resolution: OfflineConflictResolution) => run(async (signal, current) => {
        const value = app.current;
        if (!value)
            return;
        // Persist resolution as a new frozen command BEFORE network submission.
        const id = await value.capture(null, resolution), rows = await value.captures();
        if (!fresh(current, signal))
            return;
        setCaptures(rows);
        setConflict(null);
        await value.replay(rows.find(x => x.commandId === id)!, signal);
        const updated = await value.captures();
        if (fresh(current, signal)) {
            setCaptures(updated);
            setStatus('ready');
        }
    });
    const loadHistory = (offset: number) => run(async (signal, current) => {
        const value = app.current;
        if (!value)
            return;
        const page = await value.history(signal, offset);
        if (fresh(current, signal)) {
            setHistory(page.items);
            setHistoryOffset(page.offset);
            setHistoryMore(page.hasMore);
        }
    });
    const live = active && authorizedContext !== null;
    const before: OfflineConflictValue = conflict?.base.kind === 'known' && typeof conflict.base.text === 'string' ? { kind: 'known', text: conflict.base.text }
        : conflict?.base.kind === 'missing' ? { kind: 'missing' } : conflict?.base.kind === 'withheld' ? { kind: 'withheld' } : { kind: 'unknown' };
    const displayedConflict = conflict ? { ...conflict, fields: conflict.fields.map(field => field.key === 'watch_group' ? { ...field, before } : field) } : null;
    return <section className="offline-workspace" aria-label={localizedText(en, "pairs.m_bd180a669347ef89")}>
    <h2>{localizedText(en, "pairs.m_bd180a669347ef89")}</h2>
    <p>{localizedText(en, "pairs.m_9470d5c16338e443")}</p>
    <p role="status">{status === 'ready' && live ? (localizedText(en, "pairs.m_837552dcbf5b818c"))
            : status === 'denied' ? (localizedText(en, "pairs.m_a868e311619c3e44"))
                : status === 'keyLost' ? (localizedText(en, "pairs.m_628a5203868d75fb"))
                    : status === 'failed' ? (localizedText(en, "pairs.m_1bdf5c0cb043fd87"))
                        : (localizedText(en, "pairs.m_88faf19f28e48ece"))}</p>
    <a href={`/api/v1/auth/login?returnUrl=${encodeURIComponent(`/app/crew-roster/${vessel}/crew-profile/${assignment}`)}`}>{localizedText(en, "pairs.m_e3f876bc9c438b22")}</a>
    <Button variant="outline" disabled={!online || busy || dirty} onClick={() => void refresh()}>{localizedText(en, "pairs.m_9b91db9e2565d151")}</Button>
    <EnterpriseToolbar><label>{localizedText(en, "pairs.m_06191e7d9dfa7125")}<select value={selected} disabled={busy || dirty} onChange={e => { withdraw(); setSelected(e.target.value); }}>
      <option value="">{localizedText(en, "pairs.m_778e6d0b21b4dfe7")}</option>
      {selected && !contexts.some(x => x.id === selected) && <option value={selected}>{localizedText(en, "pairs.m_91be958e3489634c")}</option>}
      {contexts.map((c, index) => <option key={c.id} value={c.id}>{localizedText(en, "pairs.m_b4d0c5c2563a5f08")} {index + 1}{c.revokedAt ? (localizedText(en, "pairs.m_7de5a121e073774c")) : ''}</option>)}
    </select></label>
    <Button variant="outline" disabled={!online || busy} onClick={() => void authorize()}>{localizedText(en, "pairs.m_64dd9d6f454e185e")}</Button></EnterpriseToolbar>
    {live && snapshot && <>
      <p>{localizedText(en, "pairs.m_181c3766a3589496")} · {snapshot.revision} · {snapshot.watchGroup ?? (localizedText(en, "pairs.m_a5a7675bd7000dbd"))}</p>
      <label>{localizedText(en, "pairs.m_ea6ab47f16ab30fc")}<select value={watch} disabled={busy} onChange={e => { setWatch(e.target.value); setDirty(true); }}>
        <option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option>{authorizedContext?.watchGroups.map(v => <option key={v}>{v}</option>)}
      </select></label>
      <EnterpriseActions aria-label={localizedText(en, "pairs.m_04ba784116a2cf40")}><Button variant="outline" disabled={busy} onClick={() => { setWatch(snapshot.watchGroup ?? ''); setDirty(false); }}>{localizedText(en, "pairs.m_391bd1f8046032b2")}</Button><Button disabled={busy} onClick={() => void capture()}>{localizedText(en, "pairs.m_6935dec4c224fffd")}</Button></EnterpriseActions>
      <OfflineQueueSummary captures={captures} language={language}/><ul className="offline-capture-list" aria-label={localizedText(en, "pairs.m_dccad8ac57ececd7")}>{captures.map((row, index) => <li key={row.commandId}>
        {localizedText(en, "pairs.m_78fd146a51f0c965")} {index + 1} · {row.outcome?.result ?? (localizedText(en, "pairs.m_e2f96a34d108961d"))}
        <OfflineCaptureMetadata capture={row} language={language}/><EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_6554165b4d2b56b4")}>
        <Button variant="outline" disabled={busy || !online} onClick={() => void replay(row)}>{localizedText(en, "pairs.m_e02b437f8d432d05")}</Button>
        {row.outcome?.result === 'MaterialConflict' && <Button variant="outline" disabled={busy || !online} onClick={() => void review(row.commandId)}>{localizedText(en, "pairs.m_81941c7b2d89edfc")}</Button>}
        <Button variant="outline" disabled={busy || !confirmDiscard} onClick={() => void run(async (signal, current) => {
                    const value = app.current;
                    if (!value)
                        return;
                    await value.discard(row, confirmDiscard);
                    const rows = await value.captures();
                    if (fresh(current, signal)) {
                        setCaptures(rows);
                        setConfirmDiscard(false);
                    }
                })}>{localizedText(en, "pairs.m_63ed17a59e454a9a")}</Button></EnterpriseActions>
      </li>)}</ul>
      <Button variant="outline" disabled={busy || !online} onClick={() => void loadHistory(0)}>{localizedText(en, "pairs.m_85c3e973e82bad0a")}</Button>
      <ul aria-label={localizedText(en, "pairs.m_67a98367864a0460")}>{history.map((o, i) => <li key={o.commandId}>{i + 1} · {o.result} · {o.revision} · {o.receivedAt}{o.parentCommandId ? (localizedText(en, "pairs.m_29690a213331d2e9")) : ''}</li>)}</ul>
      <EnterprisePagination label={localizedText(en, "pairs.m_2464b5f3c1b53ac1")} previous={localizedText(en, "pairs.m_9a38c6be24d2da85")} next={localizedText(en, "pairs.m_98acb984cf8d64d0")} previousDisabled={busy || !online || historyOffset === 0} nextDisabled={busy || !online || !historyMore} onPrevious={() => void loadHistory(Math.max(0, historyOffset - 25))} onNext={() => void loadHistory(historyOffset + 25)}/>
      <Button variant="outline" disabled={busy || !online || !confirmDiscard} onClick={() => void run(async (signal) => { await app.current?.revoke(false, signal); withdraw(); })}>{localizedText(en, "pairs.m_36d39021843fe7bd")}</Button>
    </>}
    <label><input type="checkbox" checked={confirmDiscard} onChange={e => setConfirmDiscard(e.target.checked)}/>{localizedText(en, "pairs.m_6e18f21d6756c44b")}</label>
    {selected && !live && <Button variant="outline" disabled={!online || busy || !confirmDiscard} onClick={() => void run(async (signal, current) => {
                await discardLostCaptures(vessel, assignment, selected, confirmDiscard, signal);
                if (fresh(current, signal)) {
                    setSelected('');
                    setStatus('retained');
                    setConfirmDiscard(false);
                }
            })}>{localizedText(en, "pairs.m_7046d49567735bf2")}</Button>}
    {live && conflict && <>
      {conflict.resolved && <p>{localizedText(en, "pairs.m_74c07b8efe11459c")} · {conflict.resolvedValue?.kind === 'known' ? conflict.resolvedValue.text : (localizedText(en, "pairs.m_bc6c16844e81b737"))}
        {conflict.resolvedBy === authorizedContext?.actorId ? (localizedText(en, "pairs.m_fd8a3ad7f27369ff")) : ''} · {conflict.resolutionReason}</p>}
      <OfflineConflictReview language={language} contextKey={authorizedContext!.id} state="ready" conflict={displayedConflict} canResolve={conflict.resolved === null && conflict.currentVersion === Number(conflict.revision)} online={online} operation={busy ? 'submitting' : uncertain ? 'outcomeUnknown' : 'idle'} reasonLimit={500} onResolve={r => void resolve(r)} onCheckOutcome={() => void review(conflict.id)} onRetry={() => void review(conflict.id)}/>
    </>}
  </section>;
}
