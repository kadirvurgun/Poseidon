import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffectEvent, useEffect, useState } from 'react';
import type { Content, Row } from './control-evidence-api';
import * as graph from './control-evidence-api';
import { tugApi, type TugBoard } from './tug-api';
import * as technical from './technical-api';
import type { OwningContent } from './operation-equipment-contract';
export function OperationEquipmentEditor({ vessel, language, offline, isTug, value, onChange }: {
    vessel: string;
    language: AppLocale;
    offline: boolean;
    isTug: boolean;
    value: Content;
    onChange: (v: Content) => void;
}) {
    const [jobs, setJobs] = useState<TugBoard | null>(null), [offset, setOffset] = useState(0), [assets, setAssets] = useState<technical.Asset[]>([]);
    const [evidence, setEvidence] = useState<graph.Page | null>(null), [evidenceOffset, setEvidenceOffset] = useState(0), [error, setError] = useState(false);
    const key = (['operation', 'serviceAuthorization', 'cargoPlan', 'winchFitment'] as const).find(k => value[k] != null);
    const resetScope1 = [vessel, offline, isTug, offset, evidenceOffset] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], offline) || !Object.is(previousScope1[2], isTug) || !Object.is(previousScope1[3], offset) || !Object.is(previousScope1[4], evidenceOffset)) {
        setPreviousScope1(resetScope1);
        setJobs(null);
        setAssets([]);
        setEvidence(null);
        setError(false);
    }
    useEffect(() => {
        if (offline)
            return;
        const c = new AbortController();
        let active = true;
        void Promise.allSettled([isTug ? tugApi.board(vessel, '', '', offset, c.signal) : Promise.resolve(null), technical.snapshot(vessel, c.signal), graph.page(vessel, 'Evidence', evidenceOffset, c.signal)]).then(([j, a, e]) => {
            if (!active)
                return;
            if (j.status === 'fulfilled')
                setJobs(j.value);
            if (a.status === 'fulfilled')
                setAssets(a.value.items);
            if (e.status === 'fulfilled')
                setEvidence(e.value);
            setError([j, a, e].some(r => r.status === 'rejected'));
        });
        return () => { active = false; c.abort(); };
    }, [vessel, offline, isTug, offset, evidenceOffset]);
    const update = (v: OwningContent) => onChange({ ...value, ...v });
    const choose = (k: string) => onChange(Object.assign({}, value, { tugCapability: null, classPack: null, msmd: null, crewRoleAssessment: null, operation: null, serviceAuthorization: null, cargoPlan: null, winchFitment: null }, (k === 'operation' ? { operation: { jobId: null, jobRevision: null, jobVersion: null, planRevision: null, planVersion: null, profile: null, serviceAuthorizationId: null, serviceAuthorizationRevision: null } } :
        k === 'serviceAuthorization' ? { serviceAuthorization: { jobId: null, jobRevision: null, jobVersion: null, planRevision: null, planVersion: null, serviceContext: null, issuerReference: null } } :
            k === 'cargoPlan' ? { cargoPlan: { planReference: null, packagedDangerousGoods: null } } : k === 'winchFitment' ? { winchFitment: { assetId: null, assetRevision: null, assetVersion: null, fitted: null, registerReference: null } } : {})));
    const bool = (label: string, v: boolean | null, change: (v: boolean | null) => void) => <label>{label}<select value={v == null ? '' : String(v)} onChange={e => change(e.target.value === '' ? null : e.target.value === 'true')}><option value="">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option><option value="true">{localizedText(language, "pairs.m_b0df697bd7b8694e")}</option><option value="false">{localizedText(language, "pairs.m_63083b11c5b758e1")}</option></select></label>;
    const jobRef = value.operation ?? value.serviceAuthorization;
    return <fieldset disabled={offline}><legend>{localizedText(language, "pairs.m_e3b19efa3fd132ef")}</legend>
    <p>{localizedText(language, "pairs.m_d48de8d3e6d11f7d")}</p>
    {error && <p role="alert">{localizedText(language, "pairs.m_cbcaf75384faa109")}</p>}
    <label>{localizedText(language, "pairs.m_55207931e5f13456")}<select value={key ?? ''} onChange={e => choose(e.target.value)}><option value="">{localizedText(language, "pairs.m_57eb4d8604eec437")}</option>{isTug && <><option value="operation">{localizedText(language, "pairs.m_4e0b6a413be53cbb")}</option><option value="serviceAuthorization">{localizedText(language, "pairs.m_de403118049d1ea0")}</option></>}<option value="cargoPlan">{localizedText(language, "pairs.m_4e665581e60e08fd")}</option><option value="winchFitment">{localizedText(language, "pairs.m_5451033666ad3a69")}</option></select></label>
    {jobRef && <><label>{localizedText(language, "pairs.m_24c868317bc2bdc8")}<select value={jobRef.jobId ?? ''} onChange={e => {
                const row = jobs?.items.find(j => j.request.id === e.target.value), plan = row?.draftPlan;
                const ref = { jobId: row?.request.id ?? null, jobRevision: row?.request.revision ?? null, jobVersion: row?.request.version ?? null, planRevision: plan?.revision ?? null, planVersion: plan?.version ?? null };
                if (value.operation)
                    update({ operation: { ...value.operation, ...ref, serviceAuthorizationId: null, serviceAuthorizationRevision: null } });
                else if (value.serviceAuthorization)
                    update({ serviceAuthorization: { ...value.serviceAuthorization, ...ref } });
            }}><option value="">{localizedText(language, "pairs.m_01b23fcd15621f8f")}</option>{jobRef.jobId && !jobs?.items.some(j => j.request.id === jobRef.jobId) && <option value={jobRef.jobId}>{localizedText(language, "pairs.m_336b3eb535630752")}</option>}{jobs?.items.map(j => <option key={j.request.id} value={j.request.id}>{j.request.jobNumber} · {j.request.inputs.jobType} · {j.request.inputs.operationArea}</option>)}</select></label><button type="button" disabled={offset === 0} onClick={() => setOffset(v => v - 25)}>{localizedText(language, "pairs.m_f5adbdc8e0b58292")}</button><button type="button" disabled={!jobs?.hasMore} onClick={() => setOffset(v => v + 25)}>{localizedText(language, "pairs.m_dfb705f559bc7e4b")}</button></>}
    {value.operation && <><label>{localizedText(language, "pairs.m_46dadc84862646c8")}<input value={value.operation.profile ?? ''} maxLength={2000} onChange={e => update({ operation: { ...value.operation!, profile: e.target.value || null } })}/></label><label>{localizedText(language, "pairs.m_88b16bd251e02024")}<select value={value.operation.serviceAuthorizationId ?? ''} onChange={e => { const r = evidence?.items.find(r => r.id === e.target.value); update({ operation: { ...value.operation!, serviceAuthorizationId: r?.id ?? null, serviceAuthorizationRevision: r?.revision ?? null } }); }}><option value="">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option>{evidence?.items.filter(r => r.content.serviceAuthorization).map(r => <option key={r.id} value={r.id}>{r.content.title} · {r.verification}</option>)}</select></label></>}
    {value.serviceAuthorization && <>{(['serviceContext', 'issuerReference'] as const).map(k => <label key={k}>{k === 'serviceContext' ? localizedText(language, "pairs.m_fa4692bc2dff9cf4") : localizedText(language, "pairs.m_c1b857de03bca315")}<input maxLength={2000} value={value.serviceAuthorization?.[k] ?? ''} onChange={e => update({ serviceAuthorization: { ...value.serviceAuthorization!, [k]: e.target.value || null } })}/></label>)}</>}
    {value.cargoPlan && <><label>{localizedText(language, "pairs.m_bc3ab257a8472057")}<input maxLength={2000} value={value.cargoPlan.planReference ?? ''} onChange={e => update({ cargoPlan: { ...value.cargoPlan!, planReference: e.target.value || null } })}/></label>{bool(localizedText(language, "pairs.m_2d74ae176beaf8f6"), value.cargoPlan.packagedDangerousGoods, v => update({ cargoPlan: { ...value.cargoPlan!, packagedDangerousGoods: v } }))}</>}
    {value.winchFitment && <><label>{localizedText(language, "pairs.m_de575f06f6279b2b")}<select value={value.winchFitment.assetId ?? ''} onChange={e => { const a = assets.find(a => a.id === e.target.value); update({ winchFitment: { ...value.winchFitment!, assetId: a?.id ?? null, assetRevision: a?.revision ?? null, assetVersion: a?.version ?? null } }); }}><option value="">{localizedText(language, "pairs.m_01b23fcd15621f8f")}</option>{assets.map(a => <option key={a.id} value={a.id}>{a.code} · {a.name}</option>)}</select></label><label>{localizedText(language, "pairs.m_86a841a5e6f80dfd")}<input maxLength={2000} value={value.winchFitment.registerReference ?? ''} onChange={e => update({ winchFitment: { ...value.winchFitment!, registerReference: e.target.value || null } })}/></label>{bool(localizedText(language, "pairs.m_3e54ddc9b803d15f"), value.winchFitment.fitted, v => update({ winchFitment: { ...value.winchFitment!, fitted: v } }))}</>}
    {key === 'operation' && <><button type="button" disabled={evidenceOffset === 0} onClick={() => setEvidenceOffset(v => v - 25)}>{localizedText(language, "pairs.m_8d066aeac53779f9")}</button><button type="button" disabled={!evidence?.hasMore} onClick={() => setEvidenceOffset(v => v + 25)}>{localizedText(language, "pairs.m_7f797e33ffaf4a3b")}</button></>}
  </fieldset>;
}
export function OwningEvaluationSelector({ vessel, language, offline, disabled = false, onChange }: {
    vessel: string;
    language: AppLocale;
    offline: boolean;
    disabled?: boolean;
    onChange: (refs: {
        operationId: string | null;
        cargoPlanId: string | null;
        winchFitmentId: string | null;
    } | null) => void;
}) {
    const [rows, setRows] = useState<Row[]>([]), [offset, setOffset] = useState(0), [more, setMore] = useState(false), [error, setError] = useState(false);
    const [refs, setRefs] = useState({ operationId: null as string | null, cargoPlanId: null as string | null, winchFitmentId: null as string | null });
    const notify = useEffectEvent(onChange);
    const resetScope2 = [vessel, offline] as const;
    const [previousScope2, setPreviousScope2] = useState(resetScope2);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope2[0], vessel) || !Object.is(previousScope2[1], offline)) {
        setPreviousScope2(resetScope2);
        setRefs({ operationId: null, cargoPlanId: null, winchFitmentId: null });
    }
    useEffect(() => { notify(null); }, [vessel, offline]);
    const resetScope3 = [vessel, offline, offset] as const;
    const [previousScope3, setPreviousScope3] = useState(resetScope3);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope3[0], vessel) || !Object.is(previousScope3[1], offline) || !Object.is(previousScope3[2], offset)) {
        setPreviousScope3(resetScope3);
        setRows([]);
    }
    useEffect(() => {
        if (!vessel || offline) {
            notify(null);
            return;
        }
        const c = new AbortController();
        let active = true;
        graph.page(vessel, 'Evidence', offset, c.signal).then(p => {
            if (active) {
                setRows(p.items);
                setMore(p.hasMore);
                setError(false);
            }
        }).catch(() => {
            if (active) {
                setError(true);
                setRefs({ operationId: null, cargoPlanId: null, winchFitmentId: null });
                notify(null);
            }
        });
        return () => { active = false; c.abort(); };
    }, [vessel, offline, offset]);
    return <fieldset disabled={offline || error || disabled}><legend>{localizedText(language, "pairs.m_160f2d373d3a2f13")}</legend>{error && <p role="alert">{localizedText(language, "pairs.m_7c5c286ce4d602b2")}</p>}{(['operationId', 'cargoPlanId', 'winchFitmentId'] as const).map((k, i) => <label key={k}>{[localizedText(language, "pairs.m_2b1dafb091233a54"), localizedText(language, "pairs.m_4e665581e60e08fd"), localizedText(language, "pairs.m_490bebfae4e6309f")][i]}<select value={refs[k] ?? ''} onChange={e => { const next = { ...refs, [k]: e.target.value || null }; setRefs(next); onChange(Object.values(next).some(Boolean) ? next : null); }}><option value="">{localizedText(language, "pairs.m_27cb039f3215c501")}</option>{refs[k] && !rows.some(r => r.id === refs[k]) && <option value={refs[k]!}>{localizedText(language, "pairs.m_e7c98fe77333e2dc")}</option>}{rows.filter(r => [r.content.operation, r.content.cargoPlan, r.content.winchFitment][i]).map(r => <option key={r.id} value={r.id}>{r.content.title} · {r.verification}</option>)}</select></label>)}<button type="button" disabled={offset === 0} onClick={() => setOffset(v => v - 25)}>{localizedText(language, "pairs.m_dca76185a8f0fd3d")}</button><button type="button" disabled={!more} onClick={() => setOffset(v => v + 25)}>{localizedText(language, "pairs.m_af2352e8cffef848")}</button></fieldset>;
}
