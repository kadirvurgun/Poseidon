import { localizedLabel } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { useAppLanguage } from "../i18n";
import { EnterpriseChrome } from './EnterpriseChrome';
import { MaintenanceObservations } from './MaintenanceObservations';
import { DefectEquipmentLaunch } from './DefectEquipmentLaunch';
import { WorkOrderPlanLaunch } from './WorkOrderPlanLaunch';
import { dueReason, dueState } from './maintenance-copy';
import { useEffect, useRef, useState } from 'react';
import { ApiError, session } from './api';
import type { Session } from './directory-contract';
import { EquipmentHierarchyTree } from './EquipmentHierarchyTree';
import { assetFields, planFields } from './technical-fields';
import { TechnicalOverview, TechnicalPlanList, TechnicalEmptyDetail, EquipmentSummary, MaintenancePlanSummary } from './TechnicalReadViews';
import { Button } from '../components/ui/button';
import { Plus, Search } from 'lucide-react';
import * as api from './technical-api';
import './technical-workspace.css';
import { EnterpriseActions, EnterpriseFormGrid, EnterprisePageHeader } from './EnterprisePatterns';
type Route = {
    vessel: string;
    plan: boolean;
    id: string | null;
};
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function read(): Route | null {
    const match = /^\/app\/(equipment-hierarchy|maintenance-plan)\/([0-9a-f-]{36})\/?$/i.exec(location.pathname);
    const params = new URLSearchParams(location.search), id = params.get('id');
    return match && guid.test(match[2]) && (!id || guid.test(id)) && [...params.keys()].every(x => x === 'id') && params.getAll('id').length <= 1
        ? { vessel: match[2], plan: match[1] === 'maintenance-plan', id } : null;
}
const path = (r: Route) => `/app/${r.plan ? 'maintenance-plan' : 'equipment-hierarchy'}/${r.vessel}${r.id ? `?id=${r.id}` : ''}`;
export function TechnicalWorkspace() {
    const [route, setRoute] = useState<Route | null>(read);
    const [language, setLanguage] = useAppLanguage(), en = language === 'en';
    const [user, setUser] = useState<Session | null>(null);
    const [loadedScope, setLoadedScope] = useState<string | null>(null);
    const [assets, setAssets] = useState<api.Snapshot | null>(null), [plans, setPlans] = useState<api.Plans | null>(null);
    const [refs, setRefs] = useState<api.References | null>(null), [error, setError] = useState(0), [attempt, setAttempt] = useState(0);
    const [editor, setEditor] = useState<api.EquipmentCommand | api.PlanCommand | null>(null);
    const [owners, setOwners] = useState<string[]>([]);
    const [jobDraft, setJobDraft] = useState<{
        id: string;
        code: string;
        title: string;
    } | null>(null);
    const [observationDirty, setObservationDirty] = useState(false);
    const [observationUncertain, setObservationUncertain] = useState(false);
    const [conflict, setConflict] = useState(false);
    const [history, setHistory] = useState<unknown[] | null>(null), [historyError, setHistoryError] = useState(false);
    const [dirty, setDirty] = useState(false), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false);
    const [notice, setNotice] = useState(''), [offline, setOffline] = useState(!navigator.onLine), [filter, setFilter] = useState('');
    const [pending, setPending] = useState<(() => void) | null>(null);
    const historyGeneration = useRef(0);
    const savedAttempt = useRef<{
        key: string;
        body: api.EquipmentCommand | api.PlanCommand;
        plan: boolean;
        vessel: string;
    } | null>(null);
    const current = useRef({ url: location.href, index: Number(window.history.state?.technicalIndex ?? 0) });
    const guards = useRef({ dirty: dirty || observationDirty, busy, uncertain: uncertain || observationUncertain });
    useEffect(() => { guards.current = { dirty: dirty || observationDirty, busy, uncertain: uncertain || observationUncertain }; }, [dirty, busy, uncertain, observationDirty, observationUncertain]);
    const clear = () => { historyGeneration.current++; setEditor(null); setJobDraft(null); setObservationDirty(false); setObservationUncertain(false); setDirty(false); setUncertain(false); setHistory(null); setHistoryError(false); setNotice(''); setConflict(false); savedAttempt.current = null; };
    const guard = (action: () => void) => {
        const state = guards.current;
        if (state.busy || state.uncertain)
            return;
        if (state.dirty)
            setPending(() => action);
        else
            action();
    };
    const navigate = (r: Route) => guard(() => {
        clear();
        const index = current.current.index + 1;
        window.history.pushState({ technicalIndex: index }, '', path(r));
        current.current = { index, url: location.href };
        setRoute(r);
    });
    useEffect(() => {
        document.title = `POSEIDON · ${localizedText(en, "pairs.m_6c4fb9c1511200f9")}`;
    }, [language, en]);
    useEffect(() => {
        window.history.replaceState({ technicalIndex: current.current.index }, '', location.href);
        let restoring = false, committed = false, target: {
            delta: number;
        } | null = null;
        const pop = () => {
            if (restoring) {
                if (location.href !== current.current.url)
                    return;
                restoring = false;
                if (target && !guards.current.busy && !guards.current.uncertain) {
                    const delta = target.delta;
                    const go = () => { committed = true; window.history.go(delta); };
                    if (guards.current.dirty)
                        setPending(() => go);
                    else
                        go();
                }
                target = null;
                return;
            }
            const index = Number(window.history.state?.technicalIndex ?? current.current.index - 1), delta = index - current.current.index;
            if (committed) {
                committed = false;
                current.current = { index, url: location.href };
                clear();
                setRoute(read());
                return;
            }
            if (!delta)
                return;
            target = { delta };
            restoring = true;
            window.history.go(-delta);
        };
        const unload = (e: BeforeUnloadEvent) => {
            if (guards.current.dirty || guards.current.busy || guards.current.uncertain)
                e.preventDefault();
        };
        const network = () => setOffline(!navigator.onLine);
        window.addEventListener('popstate', pop);
        window.addEventListener('beforeunload', unload);
        window.addEventListener('online', network);
        window.addEventListener('offline', network);
        return () => { window.removeEventListener('popstate', pop); window.removeEventListener('beforeunload', unload); window.removeEventListener('online', network); window.removeEventListener('offline', network); };
    }, []);
    const routeVessel = route?.vessel, routePlan = route?.plan;
    const resetScope1 = [routeVessel, routePlan, attempt] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], routeVessel) || !Object.is(previousScope1[1], routePlan) || !Object.is(previousScope1[2], attempt)) {
        setPreviousScope1(resetScope1);
        setLoadedScope(null);
        setAssets(null);
        setPlans(null);
        setRefs(null);
        setOwners([]);
        setError(0);
        setFilter('');
    }
    useEffect(() => {
        if (!routeVessel)
            return;
        const c = new AbortController();
        let active = true;
        const timeout = setTimeout(() => c.abort(), 15000);
        session(c.signal).then(async (u) => {
            if (!active)
                return;
            setUser(u);
            if (!u.authenticated)
                throw new ApiError(401);
            // Equipment scope gates the complete forest; plan reads require an additional capability.
            const a = await api.snapshot(routeVessel, c.signal);
            if (active)
                setAssets(a);
            const o = await api.owners(routeVessel, c.signal);
            if (active)
                setOwners(o);
            if (routePlan) {
                const [p, r] = await Promise.all([api.plans(routeVessel, c.signal), api.references(routeVessel, c.signal)]);
                if (active) {
                    setPlans(p);
                    setRefs(r);
                }
            }
            if (active)
                setLoadedScope(`${routeVessel}:${routePlan}`);
        }).catch(e => {
            if (active) {
                setAssets(null);
                setPlans(null);
                setRefs(null);
                setError(e instanceof ApiError ? e.status : 503);
            }
        });
        return () => { active = false; c.abort(); clearTimeout(timeout); };
    }, [routeVessel, routePlan, attempt]);
    const asset = assets?.items.find(a => a.id === route?.id), plan = plans?.items.find(p => p.id === route?.id);
    const canWrite = loadedScope !== `${route?.vessel}:${route?.plan}` ? false : route?.plan ? plans?.canWrite : assets?.canWrite;
    const canHistory = route?.plan ? plans?.canReadHistory : assets?.canReadHistory;
    const start = (create: boolean) => guard(() => {
        clear();
        if (route?.plan)
            setEditor(create ? { id: crypto.randomUUID(), baseVersion: null, assetId: assets?.items[0]?.id ?? '', jobId: null, ownerRoleId: null,
                inputs: {}, referenceAt: new Date().toISOString() } : plan ? { id: plan.id, baseVersion: plan.version, assetId: plan.assetId, jobId: plan.jobId, ownerRoleId: plan.ownerRoleId, inputs: plan.inputs, referenceAt: plan.referenceAt } : null);
        else
            setEditor(create ? { id: crypto.randomUUID(), baseVersion: null, parentId: null, code: null, name: null, level: 'Equipment', systemFamily: null,
                equipmentType: null, criticality: null, maker: null, model: null, serialNumber: null, location: null, commissionDate: null, responsibleRoleId: null, notes: null }
                : asset ? { id: asset.id, baseVersion: asset.version, parentId: asset.parentId, code: asset.code, name: asset.name, level: asset.level, systemFamily: asset.systemFamily,
                    equipmentType: asset.equipmentType, criticality: asset.criticality, maker: asset.maker, model: asset.model, serialNumber: asset.serialNumber, location: asset.location,
                    commissionDate: asset.commissionDate, responsibleRoleId: asset.responsibleRoleId, notes: asset.notes } : null);
    });
    const change = (name: string, value: string, inputs = false) => {
        if (!editor || busy || uncertain || conflict)
            return;
        setEditor(inputs && 'inputs' in editor ? { ...editor, inputs: { ...editor.inputs, [name]: value || null } } : { ...editor, [name]: value || null });
        setDirty(true);
        savedAttempt.current = null;
    };
    const save = async () => {
        if (!route || !editor || !user || offline || busy)
            return;
        const request = savedAttempt.current ?? { key: crypto.randomUUID(), body: editor, plan: route.plan, vessel: route.vessel };
        savedAttempt.current = request;
        setBusy(true);
        setNotice('');
        const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000);
        try {
            const result = await api.save(request.vessel, request.plan, request.body, user.csrfToken, request.key, c.signal);
            clear();
            const next = { ...route, id: result.id };
            const index = current.current.index + 1;
            window.history.pushState({ technicalIndex: index }, '', path(next));
            current.current = { index, url: location.href };
            setRoute(next);
            setAttempt(x => x + 1);
            setNotice(localizedText(en, "pairs.m_79054af08f971230"));
        }
        catch (e) {
            const status = e instanceof ApiError ? e.status : 0;
            const ambiguous = status === 0 || status >= 500;
            setUncertain(ambiguous);
            setConflict(status === 409 || status === 401 || status === 403);
            setNotice(ambiguous ? (localizedText(en, "pairs.m_9ff9e5844dc2e66a"))
                : status === 409 ? (localizedText(en, "pairs.m_a240687aa75a4858"))
                    : status === 403 || status === 401 ? (localizedText(en, "pairs.m_09990dfbb814a254")) : (localizedText(en, "pairs.m_fb43667d523613b2")));
            if (!ambiguous)
                savedAttempt.current = null;
        }
        finally {
            clearTimeout(timeout);
            setBusy(false);
        }
    };
    const loadHistory = async () => {
        if (!route?.id)
            return;
        setHistory(null);
        setHistoryError(false);
        const generation = ++historyGeneration.current;
        const c = new AbortController(), timeout = setTimeout(() => c.abort(), 10000);
        try {
            const result = await api.history(route.vessel, route.id, route.plan, c.signal);
            if (generation === historyGeneration.current)
                setHistory(result);
        }
        catch {
            if (generation === historyGeneration.current)
                setHistoryError(true);
        }
        finally {
            clearTimeout(timeout);
        }
    };
    if (!route)
        return <main><p role="alert">{localizedText(en, "pairs.m_6a8ef4f6b2013466")}</p><a href="/app/fleet">{localizedText(en, "pairs.m_624c9bbf6ccfa3dd")}</a></main>;
    return <div className="product-shell enterprise-shell technical-workspace" onClickCapture={e => {
            const a = (e.target as HTMLElement).closest('a');
            // Chrome closes its mobile drawer before invoking this workspace's guard.
            // Intercepting its anchors here would leave the dirty dialog inside inert main.
            if (a?.closest('.workspace-search, .enterprise-sidebar, .enterprise-topbar') || a?.getAttribute('href')?.startsWith('#'))
                return;
            if (a && (dirty || observationDirty || busy || uncertain || observationUncertain)) {
                e.preventDefault();
                e.stopPropagation();
                guard(() => { window.location.assign(a.href); });
            }
        }}>

    <EnterpriseChrome session={user} vessel={route.vessel} language={language} onLanguageChange={setLanguage} onNavigate={(href, committed) => guard(() => { committed?.(); window.location.assign(href); })} title={route.plan ? (localizedText(en, "pairs.m_2546e6e66d60b12f")) : (localizedText(en, "pairs.m_d0517aed8e15637a"))} mainId="technical-main"/>
    <main id="technical-main" tabIndex={-1}><EnterprisePageHeader eyebrow={localizedText(en, "pairs.m_3f733356f26f9757")} title={route.plan ? (localizedText(en, "pairs.m_2546e6e66d60b12f")) : (localizedText(en, "pairs.m_d0517aed8e15637a"))} description={localizedText(en, "pairs.m_c75e40d5e0f8d2bc")} actionsLabel={localizedText(en, "pairs.m_18eedcbac7ec94d9")} actions={canWrite && !editor && !jobDraft && !error && <Button disabled={busy || uncertain || offline} onClick={() => start(true)}><Plus aria-hidden="true"/>{localizedText(en, "pairs.m_9b0263faba2b6d6f")}</Button>}/>
      <nav className="enterprise-section-tabs" aria-label={localizedText(en, "pairs.m_3c3c7be095bfceaa")}><Button variant={!route.plan ? 'secondary' : 'ghost'} aria-current={!route.plan ? 'page' : undefined} disabled={busy || uncertain} onClick={() => navigate({ ...route, plan: false, id: null })}>{localizedText(en, "pairs.m_77e9d3d4c2e4b44f")}</Button><Button variant={route.plan ? 'secondary' : 'ghost'} aria-current={route.plan ? 'page' : undefined} disabled={busy || uncertain} onClick={() => navigate({ ...route, plan: true, id: null })}>{localizedText(en, "pairs.m_aa23756ffbae97e7")}</Button></nav>
      {offline && <p role="alert">{localizedText(en, "pairs.m_20305c523753febe")}</p>}
      {error ? <div role="alert"><p>{error === 401 ? (localizedText(en, "pairs.m_a5811cfb5b422a85")) : error === 403 || error === 404 ? (localizedText(en, "pairs.m_37decc04e315e7ef")) : (localizedText(en, "pairs.m_45f069187f5778f4"))}</p><button onClick={() => guard(() => setAttempt(x => x + 1))}>{localizedText(en, "pairs.m_044bf27da41fda96")}</button></div> : !assets || loadedScope !== `${route.vessel}:${route.plan}` || route.plan && !plans ? <p role="status">{localizedText(en, "pairs.m_c9914bd9caad7a7f")}</p> : <>
        <p className="technical-boundary">{localizedText(en, "pairs.m_4bef44fe990058ed")}</p>
        <TechnicalOverview assets={assets} plans={route.plan ? plans : null} en={en}/>
        <label className="technical-filter"><span>{localizedText(en, "pairs.m_53b2085feaa0cc74")}</span><div><Search aria-hidden="true"/><input type="search" value={filter} onChange={e => setFilter(e.target.value)}/></div></label>
        <div className="technical-columns"><section className="technical-record-panel" aria-label={localizedText(en, "pairs.m_37307219f1c6d3ae")}><h2>{route.plan ? (localizedText(en, "pairs.m_fc89847cf2eb3c14")) : (localizedText(en, "pairs.m_61ce25c54dbc0a2f"))}</h2>
          {route.plan ? <TechnicalPlanList plans={plans!.items} assets={assets.items} references={refs} filter={filter} selectedId={route.id} en={en} onSelect={id => navigate({ ...route, id })}/>
                : <><EquipmentHierarchyTree scopeKey={`${user?.tenantId}:${user?.actorId}:${route.vessel}`} language={language} nodes={assets!.items.map(a => ({ id: a.id, parentId: a.parentId, label: a.name || a.code }))} selectedId={route.id} onSelect={id => navigate({ ...route, id })}/>
              {filter && <ul aria-label={localizedText(en, "pairs.m_56337645601df143")}>{assets!.items.filter(a => `${a.name ?? ''} ${a.code ?? ''}`.toLocaleLowerCase(language).includes(filter.toLocaleLowerCase(language))).map(a => <li key={a.id}><button onClick={() => navigate({ ...route, id: a.id })}>{a.name || a.code || a.id}</button></li>)}</ul>}</>}
        </section><section aria-label={localizedText(en, "pairs.m_bf14f1c0eb80dc29")}>
          {route.id && !(route.plan ? plan : asset) && <p role="alert">{localizedText(en, "pairs.m_d3956072850197af")}</p>}
          {!route.id && !editor && <TechnicalEmptyDetail plan={route.plan} en={en}/>}
          {!editor && asset && !route.plan && <EquipmentSummary asset={asset} en={en}/>}
          {!editor && asset && !route.plan && user && <DefectEquipmentLaunch session={user} vessel={route.vessel} assetId={asset.id} revision={asset.revision} language={language} disabled={dirty || observationDirty || busy || uncertain || observationUncertain || offline} onNavigate={href => guard(() => window.location.assign(href))}/>}
          {!editor && plan && route.plan && <MaintenancePlanSummary plan={plan} asset={assets.items.find(asset => asset.id === plan.assetId)} references={refs} en={en}/>}
          {!editor && plan && route.plan && user && <WorkOrderPlanLaunch session={user} vessel={route.vessel} planId={plan.id} revision={plan.revision} language={language} disabled={dirty || observationDirty || busy || uncertain || observationUncertain || offline} onNavigate={href => guard(() => window.location.assign(href))}/>}
          {(canWrite && !editor && (asset || plan) || canHistory && route.id) && <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_2ce1bd8b350fd2a6")}>{canWrite && !editor && (asset || plan) && <Button variant="outline" disabled={busy || uncertain || offline} onClick={() => start(false)}>{localizedText(en, "pairs.m_2a8233db25e97857")}</Button>}{canHistory && route.id && <Button variant="outline" onClick={loadHistory}>{localizedText(en, "pairs.m_247303d64af5e1f0")}</Button>}</EnterpriseActions>}
          {historyError && <p role="alert">{localizedText(en, "pairs.m_8918abcb6db89258")}</p>}
          {!route.plan && route.id && user && !editor && loadedScope === `${route.vessel}:${route.plan}` && <MaintenanceObservations key={`${user.actorId}:${user.tenantId}:${user.csrfToken}:${route.vessel}:${route.id}`} vessel={route.vessel} asset={route.id} csrf={user.csrfToken} en={en} onDirty={setObservationDirty} onUncertain={setObservationUncertain}/>}
          {history && <ol>{history.map((r, i) => {
                    const record = r as {
                        revision?: number;
                        savedAt?: string;
                        referenceAt?: string;
                        inputs?: api.Inputs;
                        due?: api.Due;
                        asset?: api.Asset;
                    };
                    return <li key={i}><details><summary>{localizedText(en, "pairs.m_9063738166a73fc0")} {record.revision ?? '—'} · {record.savedAt ?? '—'}</summary>{record.due && <p>{dueState(record.due.state, en)} · {dueReason(record.due.reason, en)} · {record.referenceAt}</p>}<dl>{record.inputs && planFields.map(([key, english, turkish]) => <div key={key}><dt>{localizedLabel({ en: english, tr: turkish }, en)}</dt><dd>{record.inputs![key] ?? (localizedText(en, "pairs.m_a5a7675bd7000dbd"))}</dd></div>)}{record.asset && assetFields.map(([key, english, turkish]) => <div key={key}><dt>{localizedLabel({ en: english, tr: turkish }, en)}</dt><dd>{record.asset![key] ?? (localizedText(en, "pairs.m_a5a7675bd7000dbd"))}</dd></div>)}</dl></details></li>;
                })}</ol>}
        </section></div>
      </>}
      {route.plan && canWrite && !editor && <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_b5993fcb908c17b7")}><Button variant="outline" disabled={busy || uncertain || offline} onClick={() => setJobDraft({ id: crypto.randomUUID(), code: '', title: '' })}>{localizedText(en, "pairs.m_dcc7486f86fd92d9")}</Button></EnterpriseActions>}
      {jobDraft && route.plan && <form className="enterprise-editor-panel" onSubmit={async (e) => {
                e.preventDefault();
                if (!user || offline || busy)
                    return;
                setBusy(true);
                const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000);
                try {
                    await api.addJob(route.vessel, jobDraft, user.csrfToken, c.signal);
                    setJobDraft(null);
                    setDirty(false);
                    setUncertain(false);
                    setAttempt(x => x + 1);
                }
                catch (error) {
                    const ambiguous = !(error instanceof ApiError) || error.status >= 500;
                    setUncertain(ambiguous);
                    setNotice(localizedText(en, "pairs.m_da9b8df0a88cdc20"));
                }
                finally {
                    clearTimeout(timeout);
                    setBusy(false);
                }
            }}><EnterpriseFormGrid disabled={busy || uncertain || offline}><legend>{localizedText(en, "pairs.m_519c4012eb685675")}</legend>
        <label>{localizedText(en, "pairs.m_060d3419b817d68d")}<input required maxLength={2000} value={jobDraft.code} onChange={e => { setJobDraft({ ...jobDraft, code: e.target.value }); setDirty(true); }}/></label>
        <label>{localizedText(en, "pairs.m_1581566562f17d98")}<input required maxLength={2000} value={jobDraft.title} onChange={e => { setJobDraft({ ...jobDraft, title: e.target.value }); setDirty(true); }}/></label>
        </EnterpriseFormGrid><EnterpriseActions layout="form" aria-label={localizedText(en, "pairs.m_a3d7221596f12c68")}><Button variant="outline" disabled={busy || uncertain} onClick={() => guard(clear)}>{localizedText(en, "pairs.m_b8c70a3fdcf207b3")}</Button><Button type="submit" disabled={busy || offline}>{localizedText(en, "pairs.m_fa8a706b6220e82c")}</Button></EnterpriseActions></form>}
      {editor && <form className="enterprise-editor-panel" onSubmit={e => { e.preventDefault(); void save(); }}><EnterpriseFormGrid disabled={busy || uncertain || conflict || offline}><legend>{localizedText(en, "pairs.m_9714969a2eddcfe7")}</legend>
        {'inputs' in editor ? <>
          <label>{localizedText(en, "pairs.m_77e9d3d4c2e4b44f")}<select required value={editor.assetId} onChange={e => change('assetId', e.target.value)}><option value="">{localizedText(en, "pairs.m_01b23fcd15621f8f")}</option>{assets?.items.map(a => <option key={a.id} value={a.id}>{a.name || a.code || a.id}</option>)}</select></label>
          <label>{localizedText(en, "pairs.m_519c4012eb685675")}<select value={editor.jobId ?? ''} onChange={e => change('jobId', e.target.value)}><option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option>{refs?.jobs.map(j => <option key={j.id} value={j.id}>{j.code} · {j.title}</option>)}</select></label>
          <label>{localizedText(en, "pairs.m_aad8c1474d4ec372")}<select value={editor.ownerRoleId ?? ''} onChange={e => change('ownerRoleId', e.target.value)}><option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option>{refs?.owners.map(o => <option key={o} value={o}>{o}</option>)}</select></label>
          <label>{localizedText(en, "pairs.m_3a79b494843d5477")}<select value={editor.inputs.triggerType ?? ''} onChange={e => change('triggerType', e.target.value, true)}><option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option><option value="Calendar">{localizedText(en, "pairs.m_74edb9597529c78a")}</option><option value="Meter">{localizedText(en, "pairs.m_896af24280cadb66")}</option><option value="Condition">{localizedText(en, "pairs.m_6867907ad1d51eb8")}</option></select></label>
          <label>{localizedText(en, "pairs.m_9b7af1a254187cf1")}<select value={editor.inputs.condition ?? ''} onChange={e => change('condition', e.target.value, true)}><option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option><option value="True">{localizedText(en, "pairs.m_129a7406352aa95e")}</option><option value="False">{localizedText(en, "pairs.m_f2b1c53b82a3e806")}</option></select></label>
          {planFields.map(([key, english, turkish]) => <label key={key}>{localizedLabel({ en: english, tr: turkish }, en)}<input maxLength={2000} type={key === 'lastDoneDate' ? 'date' : 'text'} value={editor.inputs[key] ?? ''} onChange={e => change(key, e.target.value, true)}/></label>)}
          <label>{localizedText(en, "pairs.m_6bdf5c708d6282c1")}<input required value={editor.referenceAt} onChange={e => change('referenceAt', e.target.value)}/></label>
        </> : <>
          <label>{localizedText(en, "pairs.m_95b612a214a9ed88")}<select value={editor.parentId ?? ''} onChange={e => change('parentId', e.target.value)}><option value="">{localizedText(en, "pairs.m_fff1cf175aa1a66a")}</option>{assets?.items.filter(a => a.id !== editor.id).map(a => <option key={a.id} value={a.id}>{a.name || a.code || a.id}</option>)}</select></label>
          <label>{localizedText(en, "pairs.m_496c84d8d0354fa1")}<select value={editor.level} onChange={e => change('level', e.target.value)}><option value="System">{localizedText(en, "pairs.m_0e986037a7ebffd5")}</option><option value="Equipment">{localizedText(en, "pairs.m_77e9d3d4c2e4b44f")}</option><option value="Component">{localizedText(en, "pairs.m_1015c6735bd0c910")}</option></select></label>
          <label>{localizedText(en, "pairs.m_4932250e7b636bb1")}<select value={editor.responsibleRoleId ?? ''} onChange={e => change('responsibleRoleId', e.target.value)}><option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option>{owners.map(o => <option key={o} value={o}>{o}</option>)}</select></label>
          {assetFields.map(([key, english, turkish]) => <label key={key}>{localizedLabel({ en: english, tr: turkish }, en)}<input maxLength={2000} type={key === 'commissionDate' ? 'date' : 'text'} value={editor[key] ?? ''} onChange={e => change(key, e.target.value)}/></label>)}
        </>}
      </EnterpriseFormGrid><EnterpriseActions layout="form" aria-label={localizedText(en, "pairs.m_ae1f2ff34e96c6a8")}><Button variant="outline" disabled={busy || uncertain} onClick={() => guard(() => { clear(); setAttempt(x => x + 1); })}>{localizedText(en, "pairs.m_b8c70a3fdcf207b3")}</Button><Button type="submit" disabled={busy || offline || conflict}>{uncertain ? (localizedText(en, "pairs.m_9f97920d4c817430")) : (localizedText(en, "pairs.m_7e0cdba2e467dbec"))}</Button></EnterpriseActions></form>}
      {notice && <p role="status">{notice}</p>}
      {pending && <TechnicalDiscard en={en} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action(); }}/>}
    </main>
  </div>;
}
function TechnicalDiscard({ en, onStay, onDiscard }: {
    en: boolean;
    onStay: () => void;
    onDiscard: () => void;
}) {
    const dialog = useRef<HTMLDialogElement>(null);
    useEffect(() => {
        const previous = document.activeElement as HTMLElement | null, node = dialog.current;
        node?.showModal();
        return () => { node?.close(); previous?.focus(); };
    }, []);
    return <dialog ref={dialog} aria-label={localizedText(en, "pairs.m_810226301c8f51c2")} onCancel={e => { e.preventDefault(); onStay(); }}>
    <p>{localizedText(en, "pairs.m_879689123cd412fb")}</p>
    <EnterpriseActions aria-label={localizedText(en, "pairs.m_0b547af70a64c52f")}><Button variant="outline" autoFocus onClick={onStay}>{localizedText(en, "pairs.m_b027049e6f119010")}</Button><Button variant="destructive" onClick={onDiscard}>{localizedText(en, "pairs.m_651c688640fa5059")}</Button></EnterpriseActions>
  </dialog>;
}
