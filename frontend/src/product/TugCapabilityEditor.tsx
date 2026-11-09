import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useState } from 'react';
import { ApiError } from './api';
import { tugApi } from './tug-api';
import * as equipment from './technical-api';
import * as evidence from './control-evidence-api';
import { blankCapability, type CapabilityStatement } from './tug-capability-contract';
export function TugCapabilityEditor({ vessel, language, offline, value, onChange }: {
    vessel: string;
    language: AppLocale;
    offline: boolean;
    value: CapabilityStatement | null;
    onChange: (v: CapabilityStatement | null) => void;
}) {
    const [assets, setAssets] = useState<equipment.Asset[]>([]), [conditions, setConditions] = useState<evidence.Row[]>([]), [offset, setOffset] = useState(0), [more, setMore] = useState(false);
    const [profileVersion, setProfileVersion] = useState<number | null>(null), [state, setState] = useState('idle');
    const hasValue = value !== null;
    const resetScope1 = [vessel, hasValue, offline, offset] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], hasValue) || !Object.is(previousScope1[2], offline) || !Object.is(previousScope1[3], offset)) {
        setPreviousScope1(resetScope1);
        setState(hasValue && !offline ? 'loading' : 'idle');
        setAssets([]);
        setConditions([]);
        setProfileVersion(null);
    }
    useEffect(() => {
        if (!hasValue || offline)
            return;
        const c = new AbortController();
        Promise.all([equipment.snapshot(vessel, c.signal), tugApi.context(vessel, c.signal), evidence.page(vessel, 'Evidence', offset, c.signal)])
            .then(([a, p, e]) => {
            if (c.signal.aborted)
                return;
            setAssets(a.items);
            setProfileVersion(p.bollardPull.version);
            setConditions(e.items.filter(r => r.content.tugCapability?.kind === 'Condition'));
            setMore(e.hasMore);
            setState('loaded');
        })
            .catch(e => {
            if (!c.signal.aborted) {
                setAssets([]);
                setConditions([]);
                setProfileVersion(null);
                setState(e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : 'unavailable');
            }
        });
        return () => c.abort();
    }, [vessel, hasValue, offline, offset]);
    const patch = (p: Partial<CapabilityStatement>) => {
        if (value)
            onChange({ ...value, ...p });
    };
    return <fieldset aria-label={localizedText(language, "pairs.m_7286ccd5a4c33b97")}>
    <legend>{localizedText(language, "pairs.m_7286ccd5a4c33b97")}</legend>
    <label>{localizedText(language, "pairs.m_61654c53dbc3338d")}<select disabled={offline} value={value?.kind ?? ''} onChange={e => onChange(e.target.value ? blankCapability(e.target.value as CapabilityStatement['kind']) : null)}><option value="">{localizedText(language, "pairs.m_370abdb8ce28c3ea")}</option><option value="BollardPullTest">{localizedText(language, "pairs.m_220a18a4c889b328")}</option><option value="Condition">{localizedText(language, "pairs.m_52369e9735b0580c")}</option></select></label>
    {value && <>
      <p>{localizedText(language, "pairs.m_15fb3adbbd309612")}</p>
      {state === 'loading' && <p role="status">{localizedText(language, "pairs.m_231d68e55da99670")}</p>}
      {['denied', 'unavailable'].includes(state) && <p role="alert">{localizedText(language, "pairs.m_3f14e025bef2641a")}</p>}
      {offline && <p role="status">{localizedText(language, "pairs.m_dfe202a2bf8a0cfa")}</p>}
      <fieldset disabled={offline || state !== 'loaded'}>
        <label>{localizedText(language, "pairs.m_622a7121cef4b2d1")}<select value={value.assetId ?? ''} onChange={e => { const a = assets.find(a => a.id === e.target.value); patch({ assetId: a?.id ?? null, assetRevision: a?.revision ?? null, assetVersion: a?.version ?? null, profileVersion }); }}><option value="">{localizedText(language, "pairs.m_8de8ddde51dfdee4")}</option>{value.assetId && !assets.some(a => a.id === value.assetId) && <option value={value.assetId}>{localizedText(language, "pairs.m_00a0cb4d2f2a14d9")}</option>}{assets.map(a => <option key={a.id} value={a.id}>{a.code ?? a.id} / r{a.revision}</option>)}</select></label>
        <button type="button" disabled={!value.assetId || !assets.some(a => a.id === value.assetId)} onClick={() => { const a = assets.find(a => a.id === value.assetId)!; patch({ assetRevision: a.revision, assetVersion: a.version, profileVersion }); }}>{localizedText(language, "pairs.m_7ea89178f124de89")}</button>
        <p>{localizedText(language, "pairs.m_3834a996b8e71c7b")}: {value.profileVersion ?? localizedText(language, "pairs.m_a5a7675bd7000dbd")} · {localizedText(language, "pairs.m_40eb112200fbd22c")}: {value.assetVersion ?? '—'}</p>
        <label>{localizedText(language, "pairs.m_b2b01ab4c25656ed")}<input maxLength={2000} value={value.sourceLocator ?? ''} onChange={e => patch({ sourceLocator: e.target.value || null })}/></label>
        {value.kind === 'Condition' ? <label>{localizedText(language, "pairs.m_d7e453587ba125b9")}<input inputMode="decimal" value={value.conditionFactor ?? ''} onChange={e => patch({ conditionFactor: e.target.value || null })}/></label> : <>
          {(['certifiedTonnes', 'measuredTonnes', 'testAt', 'reviewAt'] as const).map((key, i) => <label key={key}>{[localizedText(language, "pairs.m_9d9e552936977cde"), localizedText(language, "pairs.m_d6a78f74daa8127b"), localizedText(language, "pairs.m_b66ccd5f18da07d8"), localizedText(language, "pairs.m_68201a9edae37830")][i]}<input value={value[key] ?? ''} onChange={e => patch({ [key]: e.target.value || null })}/></label>)}
          <label>{localizedText(language, "pairs.m_079c778181c05429")}<select value={value.conditionEvidenceId ?? ''} onChange={e => { const r = conditions.find(r => r.id === e.target.value); patch({ conditionEvidenceId: r?.id ?? null, conditionEvidenceRevision: r?.revision ?? null, conditionEvidenceVersion: r?.version ?? null }); }}><option value="">{localizedText(language, "pairs.m_8de8ddde51dfdee4")}</option>{value.conditionEvidenceId && !conditions.some(r => r.id === value.conditionEvidenceId) && <option value={value.conditionEvidenceId}>{value.conditionEvidenceId}</option>}{conditions.map(r => <option key={r.id} value={r.id}>{r.content.title ?? r.id} / r{r.revision} · {r.verification}</option>)}</select></label>
          {conditions.length === 0 && <p>{localizedText(language, "pairs.m_34293aaa425071f0")}</p>}
          <button type="button" disabled={offset === 0} onClick={() => setOffset(x => x - 25)}>{localizedText(language, "pairs.m_20aa33c8c915a456")}</button><button type="button" disabled={!more} onClick={() => setOffset(x => x + 25)}>{localizedText(language, "pairs.m_7d9fffe1d70207df")}</button>
        </>}
      </fieldset>
    </>}
  </fieldset>;
}
