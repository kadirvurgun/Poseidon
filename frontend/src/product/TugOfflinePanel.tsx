import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterpriseFormGrid } from './EnterprisePatterns';
import { OfflineCaptureMetadata, OfflineQueueSummary } from './OfflineQueueSummary';
import { useEffect, useEffectEvent, useRef, useState } from 'react';
import { ApiError } from './api';
import { prepareOfflineShell } from './offline-application';
import { TugOfflineApplication, tugOfflineInventory, reportTugLostKey, type TugConflict, type TugPending } from './tug-offline';
export function TugOfflinePanel({ job, vessel, language, onDirty }: {
    job: string;
    vessel: string;
    language: AppLocale;
    onDirty?: (v: boolean) => void;
}) {
    const en = language === 'en';
    const [inventory, setInventory] = useState<{
        id: string;
        revokedAt: string | null;
    }[]>([]), [enrollment, setEnrollment] = useState('');
    const [app, setApp] = useState<TugOfflineApplication | null>(null), [pending, setPending] = useState<TugPending[]>([]);
    const [start, setStart] = useState(''), [end, setEnd] = useState(''), [busy, setBusy] = useState(false), [state, setState] = useState('locked');
    const [conflict, setConflict] = useState<TugConflict | null>(null);
    const [online, setOnline] = useState(navigator.onLine);
    const scope = useRef(new AbortController()), handle = useRef<TugOfflineApplication | null>(null);
    const generation = useRef(0);
    const closeScope = useEffectEvent((bootstrap: AbortController) => {
        generation.current++;
        bootstrap.abort();
        scope.current.abort();
        handle.current?.close();
        handle.current = null;
    });
    const [dirty, setDirty] = useState(false);
    useEffect(() => { onDirty?.(busy || dirty); return () => onDirty?.(false); }, [busy, dirty, onDirty]);
    const resetScope1 = [job, vessel] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], job) || !Object.is(previousScope1[1], vessel)) {
        setPreviousScope1(resetScope1);
        setApp(null);
        setPending([]);
        setConflict(null);
        setState('locked');
        setStart('');
        setEnd('');
        setInventory([]);
        setEnrollment('');
        setBusy(false);
        setDirty(false);
    }
    useEffect(() => {
        const c = new AbortController();
        scope.current = c;
        if (navigator.onLine)
            tugOfflineInventory(job, vessel, c.signal).then(v => { if (!c.signal.aborted)
                setInventory(v); }).catch(() => { });
        const withdraw = () => {
            generation.current++;
            scope.current.abort();
            handle.current?.close();
            handle.current = null;
            setApp(null);
            setPending([]);
            setConflict(null);
            setStart('');
            setEnd('');
            setBusy(false);
            setDirty(false);
            setState('locked');
        };
        const hidden = () => { if (document.hidden)
            withdraw(); };
        let observedOnline = navigator.onLine;
        const changed = () => { observedOnline = navigator.onLine; setOnline(observedOnline); if (observedOnline)
            withdraw(); };
        // Reuse the existing local lease observer; this does not fetch inventory or
        // authorize/decrypt any retained context when the native hint changes.
        const timer = window.setInterval(() => {
            const currentOnline = navigator.onLine;
            const reconnected = currentOnline && !observedOnline;
            observedOnline = currentOnline;
            setOnline(currentOnline);
            if (reconnected)
                withdraw();
            if (handle.current && !handle.current.active())
                withdraw();
        }, 1000);
        window.addEventListener('online', changed);
        window.addEventListener('offline', changed);
        window.addEventListener('pagehide', withdraw);
        document.addEventListener('visibilitychange', hidden);
        return () => { closeScope(c); window.clearInterval(timer); window.removeEventListener('online', changed); window.removeEventListener('offline', changed); window.removeEventListener('pagehide', withdraw); document.removeEventListener('visibilitychange', hidden); };
    }, [job, vessel]);
    async function work(action: (current: () => boolean) => Promise<void>) {
        // A new signal is created only by a fresh explicit user action.
        if (scope.current.signal.aborted)
            scope.current = new AbortController();
        const c = scope.current, operation = ++generation.current;
        const current = () => !c.signal.aborted && operation === generation.current;
        setBusy(true);
        try {
            await action(current);
        }
        catch (e) {
            if (!current())
                return;
            if (e instanceof ApiError && [401, 403, 404].includes(e.status)) {
                handle.current?.close();
                handle.current = null;
                setApp(null);
                setPending([]);
                setConflict(null);
                setStart('');
                setEnd('');
                setState('denied');
            }
            else
                setState(e instanceof Error && e.message === 'key_lost_data_retained' ? 'keyLost' : 'uncertain');
        }
        finally {
            if (current())
                setBusy(false);
        }
    }
    async function authorize(current: () => boolean) {
        const c = scope.current;
        handle.current?.close();
        handle.current = null;
        setApp(null);
        setPending([]);
        setConflict(null);
        await prepareOfflineShell(c.signal);
        if (!current())
            return;
        const next = await TugOfflineApplication.authorize(job, vessel, enrollment || null, c.signal);
        if (!current()) {
            next.close();
            return;
        }
        let rows: TugPending[];
        try {
            rows = await next.captures();
        }
        catch (error) {
            next.close();
            throw error;
        }
        if (!current()) {
            next.close();
            return;
        }
        handle.current = next;
        setApp(next);
        setEnrollment(next.context.id);
        setPending(rows);
        setStart(next.context.plannedStart?.slice(0, 19) ?? '');
        setEnd(next.context.plannedEnd?.slice(0, 19) ?? '');
        setDirty(false);
        setState('active');
    }
    const iso = (s: string) => s ? `${s.length === 16 ? s + ':00' : s}Z` : null;
    return <section className="offline-workspace" aria-label={localizedText(en, "pairs.m_8100a2d4355f0baa")}>
    <h4>{localizedText(en, "pairs.m_8100a2d4355f0baa")}</h4>
    <p>{localizedText(en, "pairs.m_089ef33386621e11")}</p>
    <p role="status">{state === 'active' ? (localizedText(en, "pairs.m_3966b8b7f23cdc8f")) : state === 'denied' ? (localizedText(en, "pairs.m_19f8e3d65b84d630")) : state === 'uncertain' ? (localizedText(en, "pairs.m_dfc256e5ef9b2f85")) : (localizedText(en, "pairs.m_7720acf50c3752ab"))}</p>
    {!app && <><label>{localizedText(en, "pairs.m_c84a75d3bede4faa")}<select value={enrollment} disabled={busy} onChange={e => setEnrollment(e.target.value)}><option value="">{localizedText(en, "pairs.m_bf4d6b0b74f4a618")}</option>{inventory.filter(e => !e.revokedAt).map(e => <option key={e.id} value={e.id}>{e.id}</option>)}</select></label>
      <Button variant="outline" disabled={busy || !online} onClick={() => void work(authorize)}>{localizedText(en, "pairs.m_7752e7a2d18db075")}</Button>
      {state === 'keyLost' && <><p role="alert">{localizedText(en, "pairs.m_09ab69bab1b56f71")}</p><Button variant="outline" disabled={busy || !online || !enrollment} onClick={() => void work(async (current) => { await reportTugLostKey(job, vessel, enrollment, scope.current.signal); if (current())
            setState('locked'); })}>{localizedText(en, "pairs.m_eefa3216e3fb6b04")}</Button></>}
    </>}
    {app && <><form onSubmit={e => { e.preventDefault(); void work(async (current) => { const rows = await app.capture({ plannedStart: iso(start), plannedEnd: iso(end) }); if (current()) {
            setPending(rows);
            setDirty(false);
            setState('active');
        } }); }}>
      <EnterpriseFormGrid disabled={busy}><label>{localizedText(en, "pairs.m_65839c1bf1d0da96")}<input type="datetime-local" step="1" value={start} onChange={e => { setStart(e.target.value); setDirty(true); }}/></label>
        <label>{localizedText(en, "pairs.m_b5896013c8b89991")}<input type="datetime-local" step="1" value={end} onChange={e => { setEnd(e.target.value); setDirty(true); }}/></label></EnterpriseFormGrid>
      <EnterpriseActions aria-label={localizedText(en, "pairs.m_04ba784116a2cf40")}><Button type="submit" disabled={busy}>{localizedText(en, "pairs.m_c06237d67b5bdf8e")}</Button></EnterpriseActions></form>
      {pending.length === 0 && <p>{localizedText(en, "pairs.m_b12762cacd67e426")}</p>}
      <OfflineQueueSummary captures={pending} language={language}/><ol className="offline-capture-list">{pending.map(p => <li key={p.commandId}>{p.commandId} · {p.outcome?.result === 'Committed' ? (localizedText(en, "pairs.m_7eec2adba8b1cdfb")) : p.outcome ? (localizedText(en, "pairs.m_7c7b4472a50a0056")) : (localizedText(en, "pairs.m_c85b0869794fddb8"))}
        <OfflineCaptureMetadata capture={p} language={language}/><EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_6554165b4d2b56b4")}><Button variant="outline" disabled={busy || !online} onClick={() => void work(async (current) => { await app.replay(p, scope.current.signal); if (!current())
                return; const rows = await app.captures(); if (current()) {
                setPending(rows);
                setState('active');
            } })}>{localizedText(en, "pairs.m_921504dd280991fe")}</Button>
        {p.outcome?.result === 'MaterialConflict' && <Button variant="outline" disabled={busy} onClick={() => void work(async (current) => { const v = await app.conflict(p.commandId, scope.current.signal); if (current())
                setConflict(v); })}>{localizedText(en, "pairs.m_72beb0ccb90927ac")}</Button>}</EnterpriseActions>
      </li>)}</ol>
      {conflict && <article aria-label={localizedText(en, "pairs.m_18a5e934d3ff952e")}><p>{localizedText(en, "pairs.m_79776c1186a28b3b")}</p><dl className="offline-window-comparison">{[conflict.base, conflict.captured, conflict.server].map((v, i) => <div key={i}><dt>{(en ? ['Before capture', 'Captured', 'Current server'] : ['Yakalama öncesi', 'Yakalanan', 'Güncel sunucu'])[i]}</dt><dd>{v.plannedStart ?? '—'} / {v.plannedEnd ?? '—'}</dd></div>)}</dl><EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_3f91f6fcc0796342")}>
        {(['keepServer', 'useCaptured'] as const).filter(c => conflict.choices.includes(c)).map(choice => <Button variant="outline" key={choice} disabled={busy} onClick={() => void work(async (current) => { const rows = await app.capture(choice === 'keepServer' ? conflict.server : conflict.captured, { conflict, choice }); if (current()) {
                setPending(rows);
                setConflict(null);
            } })}>{choice === 'keepServer' ? (localizedText(en, "pairs.m_68f1f2841d509032")) : (localizedText(en, "pairs.m_41a57bf29a9ce911"))}</Button>)}
      </EnterpriseActions></article>}
      <Button variant="outline" disabled={busy} onClick={() => void work(async (current) => { await app.lifecycle('revoke', scope.current.signal); if (current()) {
            handle.current = null;
            setApp(null);
            setPending([]);
            setConflict(null);
            setStart('');
            setEnd('');
            setState('locked');
        } })}>{localizedText(en, "pairs.m_7bb5d7a05efa7e56")}</Button>
      <Button variant="outline" disabled={busy} onClick={() => { generation.current++; scope.current.abort(); app.close(); handle.current = null; setApp(null); setPending([]); setConflict(null); setStart(''); setEnd(''); setState('locked'); }}>{localizedText(en, "pairs.m_19f2994eaae68c10")}</Button>
    </>}
  </section>;
}
