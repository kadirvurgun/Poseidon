import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { ApplicabilityReasonView, CapturedEvaluationInputs } from './ApplicabilityReasonView';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePagination } from './EnterprisePatterns';
import './regulatory-enterprise.css';
import './compliance-enterprise.css';
import { ComplianceEvaluationSelector } from './ComplianceInputEditor';
import type { OwningReferences } from './compliance-input-contract';
import { RegulatoryQualification } from './RegulatoryQualification';
import { OwningEvaluationSelector } from './OperationEquipmentEditor';
import * as qualification from './qualification-api';
import { useLayoutEffect, useEffect, useRef, useState } from 'react';
import * as service from './applicability-api';
import * as directory from './api';
import { isGuid } from './identifiers';
import { timestamp } from './date-validation';
import type { Capabilities, Evaluation, FrozenCommand, Page, RuleDraft, HistoryItem } from './applicability-api';
type Props = {
    language: AppLocale;
    csrf: string;
    offline: boolean;
    onDirty: (v: boolean) => void;
    onSaving: (v: boolean) => void;
};
export function LifecycleApplicability({ language, csrf, offline, onDirty, onSaving }: Props) {
    const [owningInputs, setOwningInputs] = useState<OwningReferences | null>(null);
    const [qualificationDirty, setQualificationDirty] = useState(false), [qualificationSaving, setQualificationSaving] = useState(false);
    const [currentState, setCurrentState] = useState<{
        id: string;
        current: boolean;
        causes: string[];
    } | null>(null);
    const [caps, setCaps] = useState<Capabilities | null>(null), [rows, setRows] = useState<Page<RuleDraft> | null>(null);
    const [vessels, setVessels] = useState<directory.Vessel[]>([]), [vesselPage, setVesselPage] = useState(0), [moreVessels, setMoreVessels] = useState(false);
    const [offset, setOffset] = useState(0), [attempt, setAttempt] = useState(0), [error, setError] = useState<number | null>(null);
    const [selected, setSelected] = useState<RuleDraft | null>(null), [ruleJson, setRuleJson] = useState('');
    const [source, setSource] = useState(''), [sourceRevision, setSourceRevision] = useState(''), [requirement, setRequirement] = useState(''), [requirementRevision, setRequirementRevision] = useState('');
    const [dirty, setDirty] = useState(false), [saving, setSaving] = useState(false), [frozen, setFrozen] = useState<FrozenCommand | null>(null), [conflict, setConflict] = useState(false);
    const [requested, setRequested] = useState('ADOPTED'), [ruleHistory, setRuleHistory] = useState<Page<RuleDraft> | null>(null), [ruleHistoryOffset, setRuleHistoryOffset] = useState(0);
    const [vessel, setVessel] = useState(''), [checked, setChecked] = useState<string[]>([]), [evaluation, setEvaluation] = useState<Evaluation | null>(null);
    const resetScope1 = [vessel, offline] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], offline)) {
        setPreviousScope1(resetScope1);
        setOwningInputs(null);
    }
    useEffect(() => { }, [vessel, offline]);
    const [asOf, setAsOf] = useState('');
    const [history, setHistory] = useState<Page<HistoryItem> | null>(null), [historyOffset, setHistoryOffset] = useState(0), [notice, setNotice] = useState('');
    const liveVessel = useRef(vessel);
    useLayoutEffect(() => { liveVessel.current = vessel; }, [vessel]);
    useEffect(() => { onDirty(dirty || !!frozen || conflict || qualificationDirty); }, [dirty, frozen, conflict, qualificationDirty, onDirty]);
    useEffect(() => { onSaving(saving || qualificationSaving); }, [saving, qualificationSaving, onSaving]);
    useEffect(() => {
        if (offline)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 15000);
        service.capabilities(c.signal).then(async (p) => {
            if (!active)
                return;
            setCaps(p);
            if (!p.canRead)
                return;
            const page = await service.rules(offset, c.signal);
            if (active)
                setRows(page);
            const v = await directory.vessels(vesselPage, '', c.signal);
            if (active) {
                setVessels(v.items);
                setMoreVessels(v.hasMore);
            }
        }).catch(e => {
            if (active)
                setError(e instanceof directory.ApiError ? e.status : 0);
        });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [attempt, offset, vesselPage, offline]);
    const resetScope2 = [vessel, historyOffset, attempt, offline, caps?.canRead] as const;
    const [previousScope2, setPreviousScope2] = useState(resetScope2);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope2[0], vessel) || !Object.is(previousScope2[1], historyOffset) || !Object.is(previousScope2[2], attempt) || !Object.is(previousScope2[3], offline) || !Object.is(previousScope2[4], caps?.canRead)) {
        setPreviousScope2(resetScope2);
        setHistory(null);
    }
    useEffect(() => {
        if (!vessel || offline || !caps?.canRead)
            return;
        const c = new AbortController();
        let active = true;
        service.history(vessel, historyOffset, c.signal).then(p => {
            if (active)
                setHistory(p);
        }).catch(e => {
            if (active)
                setError(e instanceof directory.ApiError ? e.status : 0);
        });
        return () => { active = false; c.abort(); };
    }, [vessel, historyOffset, attempt, offline, caps?.canRead]);
    const resetScope3 = [selected, ruleHistoryOffset, offline] as const;
    const [previousScope3, setPreviousScope3] = useState(resetScope3);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope3[0], selected) || !Object.is(previousScope3[1], ruleHistoryOffset) || !Object.is(previousScope3[2], offline)) {
        setPreviousScope3(resetScope3);
        setRuleHistory(null);
    }
    useEffect(() => {
        if (!selected || offline)
            return;
        const c = new AbortController();
        let active = true;
        service.versions(selected.current.id, ruleHistoryOffset, c.signal).then(p => {
            if (active)
                setRuleHistory(p);
        }).catch(e => {
            if (active)
                setError(e instanceof directory.ApiError ? e.status : 0);
        });
        return () => { active = false; c.abort(); };
    }, [selected, ruleHistoryOffset, offline]);
    const locked = qualificationDirty || qualificationSaving || saving || !!frozen || conflict || error === 401 || error === 403;
    function choose(row: RuleDraft | null) {
        if (dirty || frozen)
            return;
        setSelected(row);
        setRuleJson(row?.current.ruleJson ?? '');
        setRuleHistoryOffset(0);
        setSource(row?.current.sourceId ?? '');
        setSourceRevision(row?.current.sourceRevision?.toString() ?? '');
        setRequirement(row?.current.requirementId ?? '');
        setRequirementRevision(row?.current.requirementRevision?.toString() ?? '');
        setConflict(false);
    }
    async function send(command: FrozenCommand) {
        if (offline || saving || conflict)
            return;
        setFrozen(command);
        setSaving(true);
        setError(null);
        setNotice('');
        const c = new AbortController();
        const timer = setTimeout(() => c.abort(), 15000);
        try {
            const result = await service.send(command, csrf, c.signal);
            if ('current' in result) {
                setSelected(result);
                setRuleJson(result.current.ruleJson ?? '');
                setDirty(false);
            }
            else {
                setEvaluation(result);
                setCurrentState(null);
                setHistoryOffset(0);
            }
            setFrozen(null);
            setAttempt(v => v + 1);
            setNotice(command.kind === 'review' ? localizedText(language, "pairs.m_62015f9285db6f9f") : command.kind === 'evaluate' ? localizedText(language, "pairs.m_7f0ed35bcb4bb8b6") : localizedText(language, "pairs.m_9be8904cf97e02ce"));
        }
        catch (e) {
            const status = e instanceof directory.ApiError ? e.status : 0;
            setError(status);
            if ([400, 401, 403, 404, 409].includes(status))
                setFrozen(null);
            if (status === 409)
                setConflict(true);
        }
        finally {
            clearTimeout(timer);
            setSaving(false);
        }
    }
    const freeze = (kind: FrozenCommand['kind'], path: string, body: unknown, method: 'POST' | 'PUT' = 'POST') => send({ kind, path, body: JSON.stringify(body), method, key: crypto.randomUUID() });
    function save() {
        if (!caps?.canWrite || locked || offline)
            return;
        const validLink = (id: string, revision: string) => !id && !revision || isGuid(id) && /^[1-9][0-9]*$/.test(revision) && Number.isSafeInteger(Number(revision));
        if (!validLink(source, sourceRevision) || !validLink(requirement, requirementRevision)) {
            setError(400);
            return;
        }
        try {
            if (ruleJson.trim()) {
                const rule: unknown = JSON.parse(ruleJson);
                if (!rule || typeof rule !== 'object' || Array.isArray(rule))
                    throw new Error();
            }
        }
        catch {
            setError(400);
            return;
        }
        const metadata = {
            expectedVersion: selected?.version ?? 0, contract: service.ruleContract, sourceId: source || null, sourceRevision: source ? Number(sourceRevision) : null,
            requirementId: requirement || null, requirementRevision: requirement ? Number(requirementRevision) : null, reasonCode: selected ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING',
        };
        // Parse above checks JSON shape only. Preserve the original numeric lexemes;
        // never serialize the JS-parsed rule back through IEEE754.
        void send({ kind: 'author', path: selected ? `rules/${selected.current.id}` : 'rules', method: selected ? 'PUT' : 'POST', key: crypto.randomUUID(),
            body: `${JSON.stringify(metadata).slice(0, -1)},"rule":${ruleJson.trim() ? ruleJson : 'null'}}` });
    }
    const paging = (page: {
        hasMore: boolean;
    } | null, value: number, change: (n: number) => void) => <EnterprisePagination label={localizedText(language, "pairs.m_49ad2a93d3189a59")} previous={localizedText(language, "pairs.m_2f7d24c724255a10")} next={localizedText(language, "pairs.m_93a493ffda9a1959")} previousDisabled={saving || value === 0} nextDisabled={saving || !page?.hasMore} onPrevious={() => change(Math.max(0, value - 25))} onNext={() => change(value + 25)}/>;
    return <section className="lifecycle-applicability regulatory-workspace" aria-label={localizedText(language, "pairs.m_84039f2a1dc8b139")}>
    <h2>{localizedText(language, "pairs.m_0176b8e9e9e34d3c")}</h2>
    <p>{localizedText(language, "pairs.m_b42364a3347beeb0")}</p>
    {offline && <p role="status">{localizedText(language, "pairs.m_a887e9b8b553fe9d")}</p>}
    {error !== null && <p role="alert">{error === 409 ? localizedText(language, "pairs.m_a5abe3f2a4905636") : error === 403 || error === 401 ? localizedText(language, "pairs.m_d27c50f5d3c547c7") : error === 400 ? localizedText(language, "pairs.m_5cbc42513ee4d399") : localizedText(language, "pairs.m_edfe9b0c22e3dac4")}</p>}
    {notice && <p role="status">{notice}</p>}
    {!caps ? <p role="status">{localizedText(language, "pairs.m_c75450e56eb27196")}</p> : !caps.canRead ? <p role="status">{localizedText(language, "pairs.m_90f0a059747eab7d")}</p> : <>
      <h3>{localizedText(language, "pairs.m_bacce44def4579b4")}</h3>
      {!rows ? <p role="status">{localizedText(language, "pairs.m_8392c78584cbed80")}</p> : rows.items.length === 0 ? <p>{localizedText(language, "pairs.m_d68392d4d9039b84")}</p> : <ul className="regulatory-register-list">{rows.items.map(row => <li key={row.current.id}><Button type="button" disabled={locked || dirty} onClick={() => choose(row)}>{row.current.id} · {row.current.revision} · DRAFT</Button></li>)}</ul>}
      {paging(rows, offset, setOffset)}
      {caps.canWrite && <Button variant="outline" type="button" disabled={locked || dirty} onClick={() => choose(null)}>{localizedText(language, "pairs.m_fa81e243a72edcbb")}</Button>}
      <form onSubmit={e => { e.preventDefault(); save(); }}><fieldset disabled={locked || !caps.canWrite}>
        <legend>{localizedText(language, "pairs.m_17a89412037d339f")} {selected?.current.revision ?? 0}</legend>
        <p>{localizedText(language, "pairs.m_6ba73832107cfa26")}</p>
        <label className="enterprise-form-full">{localizedText(language, "pairs.m_ff91e6ef8776abe0")}<textarea rows={12} maxLength={32768} value={ruleJson} onChange={e => { setRuleJson(e.target.value); setDirty(true); }}/></label>
        <label>{localizedText(language, "pairs.m_fef3cc9576d6ae72")}<input value={source} onChange={e => { setSource(e.target.value); setDirty(true); }} maxLength={36}/></label>
        <label>{localizedText(language, "pairs.m_343f158cbe6cccbc")}<input inputMode="numeric" value={sourceRevision} onChange={e => { setSourceRevision(e.target.value); setDirty(true); }}/></label>
        <label>{localizedText(language, "pairs.m_ba1bb8203171853f")}<input value={requirement} onChange={e => { setRequirement(e.target.value); setDirty(true); }} maxLength={36}/></label>
        <label>{localizedText(language, "pairs.m_30bf8038aa5382bd")}<input inputMode="numeric" value={requirementRevision} onChange={e => { setRequirementRevision(e.target.value); setDirty(true); }}/></label>
        <EnterpriseActions className="enterprise-form-full" aria-label={localizedText(language, "pairs.m_e209ad2b365f29a4")}><Button type="submit" disabled={offline}>{localizedText(language, "pairs.m_8ad9a1fb7872b659")}</Button></EnterpriseActions>
      </fieldset></form>
      {selected && <>
        <p>{localizedText(language, "pairs.m_03e794b6f58c14af")}: DRAFT · {selected.current.reviewStatus}</p>
        <fieldset disabled={locked || dirty || offline || !caps.canReview}><legend>{localizedText(language, "pairs.m_372b44fea9e23270")}</legend>
          <label>{localizedText(language, "pairs.m_09218968fb04ea05")}<select value={requested} onChange={e => setRequested(e.target.value)}>{service.lifecycleStates.map(s => <option key={s}>{s}</option>)}</select></label>
          <Button type="button" onClick={() => void freeze('review', `rules/${selected.current.id}/review`, { expectedVersion: selected.version, requestedLifecycle: requested, reasonCode: 'REQUEST_REVIEW' })}>{localizedText(language, "pairs.m_c255b69eae61088f")}</Button>
        </fieldset>
        <h3>{localizedText(language, "pairs.m_4f4e6d5860f1e82a")}</h3>
        {ruleHistory?.items.map(row => <details key={row.current.revision}><summary>{row.current.revision} · {row.current.lifecycle} · {row.current.requestedLifecycle} · {row.current.reviewStatus}</summary><p>{row.current.savedAt}</p><pre className="register-hash">{row.current.hash}</pre><pre>{row.current.ruleJson ?? '—'}</pre></details>)}
        {paging(ruleHistory, ruleHistoryOffset, setRuleHistoryOffset)}
      </>}
      <fieldset disabled={locked || dirty || offline || !caps.canEvaluate}><legend>{localizedText(language, "pairs.m_8c2a0dc2d55145a6")}</legend>
        <label>{localizedText(language, "pairs.m_ba91f7fe5286366c")}<select value={vessel} onChange={e => { setVessel(e.target.value); setEvaluation(null); setHistoryOffset(0); }}><option value="">{localizedText(language, "pairs.m_2bf32faa5cd6ab8a")}</option>{vessel && !vessels.some(v => v.id === vessel) && <option value={vessel}>{vessel}</option>}{vessels.map(v => <option key={v.id} value={v.id}>{v.name} · {v.kind}</option>)}</select></label>
        {paging({ hasMore: moreVessels }, vesselPage, setVesselPage)}
        {rows?.items.map(row => <label key={row.current.id}><input type="checkbox" disabled={!row.current.rule} checked={checked.includes(row.current.id)} onChange={e => setChecked(v => e.target.checked ? [...v, row.current.id] : v.filter(id => id !== row.current.id))}/>{row.current.id}</label>)}
        <label>{localizedText(language, "pairs.m_36def52bd0e188bc")}<input value={asOf} maxLength={40} onChange={e => setAsOf(e.target.value)}/></label>
        <Button type="button" disabled={!vessel || checked.length === 0} onClick={() => {
                if (asOf && !timestamp(asOf)) {
                    setError(400);
                    return;
                }
                void freeze('evaluate', 'applicability/simulate', { vesselId: vessel, ruleIds: checked, asOf: asOf || null });
            }}>{localizedText(language, "pairs.m_22b4a2e9f8819c2e")}</Button>
      </fieldset>
      {selected && <RegulatoryQualification key={selected.current.id} rule={selected.current.id} ruleRevision={selected.current.revision} vessel={vessel} language={language} csrf={csrf} offline={offline} onDirty={setQualificationDirty} onSaving={setQualificationSaving}/>}
      <ComplianceEvaluationSelector value={owningInputs} onChange={setOwningInputs} offline={offline || locked || dirty} language={language}/>
      <OwningEvaluationSelector key={vessel} vessel={vessel} language={language} offline={offline} disabled={locked} onChange={v => setOwningInputs(previous => v ? { ...previous, ...v } : previous ? { ...previous, operationId: null, cargoPlanId: null, winchFitmentId: null } : null)}/>
      <Button disabled={locked || dirty || offline || !vessel || checked.length === 0 || !caps?.canEvaluate} onClick={() => void freeze('evaluate', 'applicability/evaluate', { vesselId: vessel, ruleIds: checked, asOf: null, owningInputs })}>{localizedText(language, "pairs.m_8c47453afe8d875c")}</Button>
      {evaluation?.authoritative && <><Button disabled={offline || saving} onClick={() => { const c = new AbortController(); qualification.current(evaluation.id, c.signal).then(v => setCurrentState({ ...v, id: evaluation.id })).catch(() => setCurrentState({ id: evaluation.id, current: false, causes: ['EVALUATION_UNAVAILABLE'] })); }}>{localizedText(language, "pairs.m_e654f741ae6ef5bc")}</Button><p role="status">{currentState?.id === evaluation.id ? currentState.current ? localizedText(language, "pairs.m_46402d63c780a37b") : `${localizedText(language, "pairs.m_6edb7d9a4aca3fd7")}: ${currentState.causes.join(' · ')}` : localizedText(language, "pairs.m_8e993843da88051b")}</p></>}
      {evaluation && <section aria-label={localizedText(language, "pairs.m_3d258805ad3da5d0")}><h3>{evaluation.authoritative ? localizedText(language, "pairs.m_5c0384d5869105f9") : localizedText(language, "pairs.m_d67ccbef4e42cd66")}</h3><p>{evaluation.asOf} · {evaluation.snapshotHash}</p>{evaluation.conflicts.map(c => <p role="alert" key={`${c.code}:${c.target}`}>{localizedText(language, "pairs.m_4f994eae441a6ee3")} {c.target} · {c.ruleReferences.join(' · ')}</p>)}<ApplicabilityReasonView results={evaluation.results} language={language}/><CapturedEvaluationInputs inputs={evaluation.snapshot} inputHash={evaluation.snapshotHash} ruleSetHash={evaluation.ruleSetHash} language={language}/></section>}
      {vessel && <section aria-label={localizedText(language, "pairs.m_1b73e15dd8a526c3")}><h3>{localizedText(language, "pairs.m_1b73e15dd8a526c3")}</h3>{!history ? <p role="status">{localizedText(language, "pairs.m_9e04b8b6eafff876")}</p> : history.items.length === 0 ? <p>{localizedText(language, "pairs.m_420e5835187133da")}</p> : history.items.map(row => <Button type="button" disabled={saving || offline} key={row.id} onClick={() => {
                        const c = new AbortController();
                        const timer = setTimeout(() => c.abort(), 15000);
                        service.evaluation(row.id, c.signal).then(v => {
                            if (v.vesselId === liveVessel.current)
                                setEvaluation(v);
                        }).catch(e => {
                            if (row.vesselId === liveVessel.current)
                                setError(e instanceof directory.ApiError ? e.status : 0);
                        }).finally(() => clearTimeout(timer));
                    }}>{row.asOf} · {localizedText(language, "pairs.m_9c88fc0eef9f2aa9")}</Button>)}{paging(history, historyOffset, setHistoryOffset)}</section>}
    </>}
    {frozen && <Button type="button" disabled={offline || saving || conflict} onClick={() => void send(frozen)}>{localizedText(language, "pairs.m_71c9516fa1dbb80c")}</Button>}
    {(dirty || frozen || conflict) && <><p role="status">{localizedText(language, "pairs.m_1a4ea6bacbf91b32")}</p><Button type="button" disabled={saving} onClick={() => { setDirty(false); setFrozen(null); setConflict(false); setSelected(null); setRuleJson(''); setSource(''); setSourceRevision(''); setRequirement(''); setRequirementRevision(''); setAttempt(v => v + 1); setError(null); }}>{localizedText(language, "pairs.m_10e1dc6328309ecd")}</Button></>}
    {!frozen && <Button type="button" disabled={offline || saving} onClick={() => { setError(null); setAttempt(v => v + 1); }}>{localizedText(language, "pairs.m_cb356a7a6c65821e")}</Button>}
  </section>;
}
