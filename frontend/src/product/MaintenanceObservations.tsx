import { localizedText } from "../i18n/messages";
import { useEffect, useRef, useState } from 'react';
import { ApiError, request } from './api';
type Source = {
    source: string;
    version: string;
    kind: string;
    unit: string;
    freshnessSeconds: number;
    maxScale: number;
};
type Observation = {
    id: string;
    source: string;
    policyVersion: string;
    sourceRevision: number;
    kind: string;
    unit: string;
    value: string | null;
    observedAt: string | null;
    provenance: string;
    revoked: boolean;
};
type Snapshot = {
    sources: Source[];
    items: Observation[];
    canWrite: boolean;
    state: string;
};
export function MaintenanceObservations({ vessel, asset, csrf, en, onDirty, onUncertain }: {
    vessel: string;
    asset: string;
    csrf: string;
    en: boolean;
    onDirty: (value: boolean) => void;
    onUncertain: (value: boolean) => void;
}) {
    const [snapshot, setSnapshot] = useState<Snapshot | null>(null), [error, setError] = useState(false), [refresh, setRefresh] = useState(0);
    const [source, setSource] = useState(''), [value, setValue] = useState(''), [time, setTime] = useState(''), [revision, setRevision] = useState('');
    const [state, setState] = useState<'idle' | 'saving' | 'uncertain' | 'denied'>('idle');
    const attempt = useRef<{
        id: string;
        body: string;
        path: string;
    } | null>(null), busy = useRef(false);
    const active = useRef<AbortController | null>(null);
    const path = `equipment-hierarchy/${encodeURIComponent(vessel)}/${encodeURIComponent(asset)}/observations`;
    const resetScope1 = [path, csrf, refresh, onDirty, onUncertain] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], path) || !Object.is(previousScope1[1], csrf) || !Object.is(previousScope1[2], refresh) || !Object.is(previousScope1[3], onDirty) || !Object.is(previousScope1[4], onUncertain)) {
        setPreviousScope1(resetScope1);
        setSnapshot(null);
        setError(false);
        setSource('');
        setValue('');
        setTime('');
        setRevision('');
        setState('idle');
    }
    useEffect(() => {
        const controller = new AbortController();
        active.current = controller;
        attempt.current = null;
        onDirty(false);
        onUncertain(false);
        request<Snapshot>(path, controller.signal).then(result => { if (!Array.isArray(result?.sources) || !Array.isArray(result.items) || typeof result.canWrite !== 'boolean')
            throw new ApiError(502); if (!controller.signal.aborted)
            setSnapshot(result); })
            .catch(() => { if (!controller.signal.aborted)
            setError(true); });
        return () => { controller.abort(); active.current?.abort(); };
    }, [path, csrf, refresh, onDirty, onUncertain]);
    const run = async (revokeId?: string) => {
        if (!snapshot || busy.current)
            return;
        if (!attempt.current) {
            const selected = snapshot.sources.find(s => s.source === source);
            if (!revokeId && (!selected || !/^[1-9][0-9]*$/.test(revision) || !Number.isSafeInteger(Number(revision))))
                return;
            const id = crypto.randomUUID();
            attempt.current = { id, path: revokeId ? `${path}/${encodeURIComponent(revokeId)}/revoke` : path,
                body: JSON.stringify(revokeId ? {} : { id, source, policyVersion: selected!.version, sourceRevision: Number(revision), kind: selected!.kind, unit: selected!.unit, value: value || null, observedAt: time || null }) };
        }
        const frozen = attempt.current, controller = new AbortController();
        active.current = controller;
        busy.current = true;
        setState('saving');
        onDirty(true);
        const timeout = setTimeout(() => controller.abort(), 15000);
        try {
            await request(frozen.path, controller.signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': frozen.id }, body: frozen.body });
            attempt.current = null;
            onDirty(false);
            onUncertain(false);
            setRefresh(r => r + 1);
        }
        catch (e) {
            if (e instanceof ApiError && [400, 401, 403, 404, 409].includes(e.status)) {
                attempt.current = null;
                onUncertain(false);
                setState('denied');
                if ([401, 403, 404].includes(e.status)) {
                    setSnapshot(null);
                    onDirty(false);
                }
            }
            else {
                setState('uncertain');
                onUncertain(true);
            }
        }
        finally {
            clearTimeout(timeout);
            busy.current = false;
        }
    };
    if (error)
        return <p role="alert">{localizedText(en, "pairs.m_598312c777f972b4")}</p>;
    if (!snapshot)
        return <p role="status">{localizedText(en, "pairs.m_2eb8858991de8dd9")}</p>;
    const selected = snapshot.sources.find(s => s.source === source), locked = state === 'saving' || state === 'uncertain';
    return <section aria-label={localizedText(en, "pairs.m_e08597ac7f99df73")}><h3>{localizedText(en, "pairs.m_e08597ac7f99df73")}</h3>
    <p>{localizedText(en, "pairs.m_be21a46099a0c3f2")}</p>
    {!snapshot.sources.length && <p>{localizedText(en, "pairs.m_0c6beadce286b699")}</p>}
    <ul>{snapshot.items.map(item => <li key={item.id}>{item.source} · {item.kind === 'Meter' ? (localizedText(en, "pairs.m_896af24280cadb66")) : (localizedText(en, "pairs.m_6867907ad1d51eb8"))} · {item.value ?? (localizedText(en, "pairs.m_a5bd35d90de7c160"))} {item.unit} · {item.observedAt ?? (localizedText(en, "pairs.m_691ebb5fec500494"))} · {item.revoked ? (localizedText(en, "pairs.m_ed83bf5770ddaa2a")) : (localizedText(en, "pairs.m_b980704b1fa89cd9"))}
      <code>{item.id}</code>{snapshot.canWrite && !item.revoked && <button type="button" disabled={locked} onClick={() => void run(item.id)}>{localizedText(en, "pairs.m_3c2e6369288f5c4c")}</button>}</li>)}</ul>
    {snapshot.canWrite && snapshot.sources.length > 0 && <form onSubmit={e => { e.preventDefault(); void run(); }}><fieldset disabled={locked}>
      <label>{localizedText(en, "pairs.m_aa06fe636f46b76e")}<select required value={source} onChange={e => { setSource(e.target.value); setValue(''); onDirty(true); }}><option value="">{localizedText(en, "pairs.m_01b23fcd15621f8f")}</option>{snapshot.sources.map(s => <option key={s.source} value={s.source}>{s.source} · {s.unit} · {s.version}</option>)}</select></label>
      <label>{localizedText(en, "pairs.m_b307fec7870b283f")}<input required inputMode="numeric" value={revision} onChange={e => { setRevision(e.target.value); onDirty(true); }}/></label>
      <label>{localizedText(en, "pairs.m_fb01fb6b8a066134")}{selected?.kind === 'Condition' ? <select value={value} onChange={e => { setValue(e.target.value); onDirty(true); }}><option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option><option value="True">{localizedText(en, "pairs.m_129a7406352aa95e")}</option><option value="False">{localizedText(en, "pairs.m_f2b1c53b82a3e806")}</option></select> : <input maxLength={1024} value={value} onChange={e => { setValue(e.target.value); onDirty(true); }}/>}</label>
      <label>{localizedText(en, "pairs.m_1ba7a501acd86ead")}<input value={time} onChange={e => { setTime(e.target.value); onDirty(true); }}/></label>
    </fieldset><button disabled={locked}>{localizedText(en, "pairs.m_c57f0b5c072259d7")}</button></form>}
    {state === 'uncertain' && <button type="button" onClick={() => void run()}>{localizedText(en, "pairs.m_7231b768ded13bd5")}</button>}
    {state === 'uncertain' && <p role="alert">{localizedText(en, "pairs.m_62930366e47b200e")}</p>}
    {state === 'denied' && <p role="alert">{localizedText(en, "pairs.m_c12f4ab7b5b4e04d")}</p>}
    <button disabled={locked} type="button" onClick={() => { onDirty(false); setRefresh(r => r + 1); }}>{localizedText(en, "pairs.m_301d18c5de7d235b")}</button>
  </section>;
}
