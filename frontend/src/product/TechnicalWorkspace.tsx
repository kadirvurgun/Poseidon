import { localizedLabel } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { useAppLanguage } from "../i18n";
import { EnterpriseChrome } from './EnterpriseChrome';
import { MaintenanceObservations } from './MaintenanceObservations';
import { DefectEquipmentLaunch } from './DefectEquipmentLaunch';
import { WorkOrderPlanLaunch } from './WorkOrderPlanLaunch';
import { dueReason, dueState } from './maintenance-copy';
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
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
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import { isGuid } from './identifiers';
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
const identity = (value: Session | null) => value?.authenticated && isGuid(value.actorId) && isGuid(value.tenantId)
    ? `${value.tenantId.toLowerCase()}/${value.actorId.toLowerCase()}` : null;
type JobDraft = { id: string; code: string; title: string };
type DraftContext = { owner: string; vessel: string; plan: boolean };
type TechnicalAttempt = DraftContext & ({ kind: 'record'; key: string; body: api.EquipmentCommand | api.PlanCommand } | { kind: 'job'; body: JobDraft });
export function TechnicalWorkspace() {
    const epoch = useWorkspaceRevalidation();
    const [route, setRoute] = useState<Route | null>(read);
    const [language, setLanguage] = useAppLanguage(), en = language === 'en';
    const [user, setUser] = useState<Session | null>(null);
    const [loadedScope, setLoadedScope] = useState<string | null>(null);
    const [loadedOwner, setLoadedOwner] = useState<string | null>(null);
    const [assets, setAssets] = useState<api.Snapshot | null>(null), [plans, setPlans] = useState<api.Plans | null>(null);
    const [refs, setRefs] = useState<api.References | null>(null), [error, setError] = useState(0), [attempt, setAttempt] = useState(0);
    const [editor, setEditor] = useState<api.EquipmentCommand | api.PlanCommand | null>(null);
    const [owners, setOwners] = useState<string[]>([]);
    const [jobDraft, setJobDraft] = useState<JobDraft | null>(null);
    const [draftContext, setDraftContext] = useState<DraftContext | null>(null);
    const [confirmed, setConfirmed] = useState<{ owner: string; vessel: string; plan: boolean; id: string } | null>(null);
    const [observationDirty, setObservationDirty] = useState(false);
    const [observationUncertain, setObservationUncertain] = useState(false);
    const [observationBusy, setObservationBusy] = useState(false);
    const [observationContext, setObservationContext] = useState<{ owner: string; vessel: string; asset: string } | null>(null);
    const [conflict, setConflict] = useState(false);
    const [history, setHistory] = useState<unknown[] | null>(null), [historyError, setHistoryError] = useState(false);
    const [dirty, setDirty] = useState(false), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false);
    const [notice, setNotice] = useState(''), [offline, setOffline] = useState(!navigator.onLine), [filter, setFilter] = useState('');
    const [pending, setPending] = useState<(() => void) | null>(null);
    const historyGeneration = useRef(0);
    const savedAttempt = useRef<TechnicalAttempt | null>(null);
    const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null);
    const committed = useRef({ scope: '', location: '', owner: null as string | null });
    const current = useRef({ url: location.href, index: Number(window.history.state?.technicalIndex ?? 0) });
    const guards = useRef({ dirty: dirty || observationDirty, busy, uncertain: uncertain || observationUncertain, protected: false });
    const clear = () => { historyGeneration.current++; setEditor(null); setJobDraft(null); setDraftContext(null); setConfirmed(null); setObservationContext(null); setObservationDirty(false); setObservationUncertain(false); setObservationBusy(false); setDirty(false); setUncertain(false); setHistory(null); setHistoryError(false); setNotice(''); setConflict(false); savedAttempt.current = null; };
    const recheckObservationAccess = useCallback(() => setAttempt(value => value + 1), []);
    const guard = (action: () => void) => {
        const state = guards.current;
        if (state.busy || state.uncertain || state.protected || running.current)
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
                if (target && !guards.current.busy && !guards.current.uncertain && !guards.current.protected) {
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
    const scope = `${routeVessel}:${routePlan}:${epoch}:${attempt}`;
    const owner = identity(user), online = !offline && navigator.onLine, visible = document.visibilityState !== 'hidden';
    const ready = loadedScope === scope && loadedOwner === owner && !!owner && online && visible;
    const sameOwner = !draftContext || draftContext.owner === owner && draftContext.vessel === routeVessel && draftContext.plan === routePlan;
    const sameObservationOwner = !observationContext || !(observationDirty || observationUncertain || observationBusy) ||
        observationContext.owner === owner && observationContext.vessel === routeVessel && !routePlan && observationContext.asset === route?.id;
    const canShowPrivate = ready && sameOwner && sameObservationOwner;
    useLayoutEffect(() => {
        if (committed.current.scope !== scope || !online || !visible) writer.current?.abort();
        committed.current = { scope, location: route ? path(route) : '', owner };
        guards.current = { dirty: dirty || observationDirty, busy: busy || observationBusy, uncertain: uncertain || observationUncertain,
            protected: !!draftContext && !canShowPrivate || !!observationContext && (observationDirty || observationUncertain || observationBusy) && !canShowPrivate };
    }, [scope, route, owner, online, visible, ready, dirty, observationDirty, busy, uncertain, observationUncertain, observationBusy, draftContext, canShowPrivate, observationContext]);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort(); }; }, []);
    const resetScope1 = [scope, online, visible] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], scope) || !Object.is(previousScope1[1], online) || !Object.is(previousScope1[2], visible)) {
        setPreviousScope1(resetScope1);
        setLoadedScope(null);
        setLoadedOwner(null);
        setAssets(null);
        setPlans(null);
        setRefs(null);
        setOwners([]);
        setError(0);
        setFilter('');
        setHistory(null);
        setHistoryError(false);
        setPending(null);
    }
    useEffect(() => {
        if (!routeVessel || !online || !visible)
            return;
        const c = new AbortController();
        let active = true;
        const timeout = setTimeout(() => c.abort(), 15000);
        session(c.signal).then(async (u) => {
            if (!active) return;
            setUser(u);
            const original = identity(u);
            if (!original) throw new ApiError(401);
            const ensureOwner = async () => {
                const fresh = await session(c.signal);
                if (!active) throw new ApiError(0, 'obsolete_scope');
                setUser(fresh);
                if (identity(fresh) !== original) throw new ApiError(403, 'identity_changed');
            };
            // Equipment scope gates the complete forest; plan reads require an additional capability.
            const a = await api.snapshot(routeVessel, c.signal);
            await ensureOwner();
            const o = await api.owners(routeVessel, c.signal);
            await ensureOwner();
            let p: api.Plans | null = null, r: api.References | null = null;
            if (routePlan) {
                [p, r] = await Promise.all([api.plans(routeVessel, c.signal), api.references(routeVessel, c.signal)]);
                await ensureOwner();
            }
            if (active) {
                setAssets(a); setOwners(o); setPlans(p); setRefs(r);
                setLoadedOwner(original);
                setLoadedScope(scope);
            }
        }).catch(e => {
            if (active) {
                setAssets(null);
                setPlans(null);
                setRefs(null);
                setError(e instanceof ApiError ? e.status : 503);
                if (e instanceof ApiError && [401, 403].includes(e.status) && e.code !== 'identity_changed') setUser(null);
            }
        }).finally(() => clearTimeout(timeout));
        return () => { active = false; c.abort(); clearTimeout(timeout); };
    }, [routeVessel, routePlan, scope, online, visible]);
    const asset = assets?.items.find(a => a.id === route?.id), plan = plans?.items.find(p => p.id === route?.id);
    const canWrite = canShowPrivate && (route?.plan ? plans?.canWrite : assets?.canWrite);
    const canHistory = canShowPrivate && (route?.plan ? plans?.canReadHistory : assets?.canReadHistory);
    // Adjust clean derived scope before committing children, just like the
    // projection withdrawal above. A dirty/unknown/running owner is never rebound.
    if (canShowPrivate && user && owner && route && !route.plan && route.id && !editor &&
        assets?.items.some(item => item.id === route.id) && !(observationDirty || observationUncertain || observationBusy) &&
        (observationContext?.owner !== owner || observationContext.vessel !== route.vessel || observationContext.asset !== route.id)) {
        setObservationContext({ owner, vessel: route.vessel, asset: route.id });
    }
    const start = (create: boolean) => guard(() => {
        if (!route || !owner || !canWrite) return;
        clear();
        setDraftContext({ owner, vessel: route.vessel, plan: route.plan });
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
        if (!editor || !canShowPrivate || busy || uncertain || conflict)
            return;
        setEditor(inputs && 'inputs' in editor ? { ...editor, inputs: { ...editor.inputs, [name]: value || null } } : { ...editor, [name]: value || null });
        setDirty(true);
        savedAttempt.current = null;
    };
    // The envelope stays in this mounted page only. Refreshing access never
    // transfers it to a replacement session or silently resends it.
    const send = async (command: TechnicalAttempt) => {
        if (!route || !canWrite || !sameOwner || owner !== command.owner || running.current || busy ||
            command.vessel !== route.vessel || command.plan !== route.plan || conflict && !uncertain)
            return;
        const priorUnknown = uncertain, originalScope = scope, originalLocation = path(route);
        const active = () => mounted.current && committed.current.scope === originalScope && committed.current.location === originalLocation;
        const c = new AbortController(), timeout = setTimeout(() => c.abort(), 15000);
        running.current = true; writer.current = c; savedAttempt.current = command;
        setBusy(true); setNotice('');
        let sent = false, received = false, accepted = false;
        const ensureOwner = async () => {
            const fresh = await session(c.signal);
            if (!active()) throw new ApiError(0, 'obsolete_scope');
            setUser(fresh);
            if (identity(fresh) !== command.owner) throw new ApiError(403, 'identity_changed');
            return fresh;
        };
        try {
            await ensureOwner();
            // Reauthorize the actual vessel/module before both first submission
            // and receipt recovery. Cached menu capabilities are insufficient.
            const access = command.plan ? await api.plans(command.vessel, c.signal) : await api.snapshot(command.vessel, c.signal);
            const fresh = await ensureOwner();
            if (!access.canWrite) throw new ApiError(403, 'permission_denied');
            sent = true;
            const result = command.kind === 'job'
                ? await api.addJob(command.vessel, command.body, fresh.csrfToken, c.signal)
                : await api.save(command.vessel, command.plan, command.body, fresh.csrfToken, command.key, c.signal);
            received = true;
            if (result.id.toLowerCase() !== command.body.id.toLowerCase())
                throw new ApiError(502, 'invalid_contract');
            await ensureOwner();
            if (!active() || committed.current.owner !== command.owner) throw new ApiError(0, 'obsolete_scope');
            accepted = true;
            clear();
            if (command.kind === 'record') {
                // Canonical identity is durable navigation state BEFORE optional
                // readback. A failed refresh cannot recreate the command UUID.
                const next = { vessel: command.vessel, plan: command.plan, id: result.id };
                const index = current.current.index + 1;
                window.history.pushState({ technicalIndex: index }, '', path(next));
                current.current = { index, url: location.href };
                setRoute(next);
                setConfirmed({ owner: command.owner, vessel: command.vessel, plan: command.plan, id: result.id });
            }
            setAttempt(x => x + 1);
            setNotice(command.kind === 'job' ? localizedText(en, 'pairs.m_79054af08f971230') : '');
        }
        catch (problem) {
            if (!mounted.current) return;
            const status = problem instanceof ApiError ? problem.status : 0;
            // A denial of THIS request cannot establish that an EARLIER unknown
            // attempt had no effect. Keep its original key/body/base and owner.
            const unknown = !accepted && (priorUnknown || received || sent && (status === 0 || status >= 500));
            savedAttempt.current = unknown ? command : null;
            setUncertain(unknown);
            if (unknown) setNotice(localizedText(en, 'pairs.m_9ff9e5844dc2e66a'));
            if (active()) {
                setConflict(!unknown && status === 409);
                setNotice(unknown ? localizedText(en, 'pairs.m_9ff9e5844dc2e66a')
                    : status === 409 ? localizedText(en, 'pairs.m_a240687aa75a4858')
                        : status === 401 || status === 403 ? localizedText(en, 'pairs.m_09990dfbb814a254') : localizedText(en, 'pairs.m_fb43667d523613b2'));
                if ([401, 403, 404].includes(status)) {
                    setLoadedScope(null); setAssets(null); setPlans(null); setRefs(null); setHistory(null); setOwners([]); setError(status);
                    if ([401, 403].includes(status) && (!(problem instanceof ApiError) || problem.code !== 'identity_changed')) setUser(null);
                }
            }
        }
        finally {
            clearTimeout(timeout); running.current = false; writer.current = null;
            if (mounted.current) setBusy(false);
        }
    };
    const save = () => {
        if (!editor || !draftContext || !canShowPrivate || !canWrite) return;
        const retained = savedAttempt.current;
        if (retained && retained.kind !== 'record') return;
        void send(retained ?? { ...draftContext, kind: 'record', key: crypto.randomUUID(), body: structuredClone(editor) });
    };
    const saveJob = () => {
        if (!jobDraft || !draftContext || !canShowPrivate || !canWrite) return;
        const retained = savedAttempt.current;
        if (retained && retained.kind !== 'job') return;
        void send(retained ?? { ...draftContext, kind: 'job', body: structuredClone(jobDraft) });
    };
    const loadHistory = async () => {
        if (!route?.id || !owner || !canShowPrivate)
            return;
        setHistory(null);
        setHistoryError(false);
        const generation = ++historyGeneration.current;
        const originalScope = scope, originalOwner = owner, originalLocation = path(route);
        const active = () => mounted.current && generation === historyGeneration.current && committed.current.scope === originalScope && committed.current.location === originalLocation;
        const c = new AbortController(), timeout = setTimeout(() => c.abort(), 10000);
        try {
            const result = await api.history(route.vessel, route.id, route.plan, c.signal);
            const fresh = await session(c.signal);
            if (!active()) return;
            setUser(fresh);
            if (identity(fresh) !== originalOwner) { setLoadedScope(null); setError(403); return; }
            setHistory(result);
        }
        catch {
            if (active())
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
            if (a && (dirty || observationDirty || busy || observationBusy || uncertain || observationUncertain)) {
                e.preventDefault();
                e.stopPropagation();
                guard(() => { window.location.assign(a.href); });
            }
        }}>

    <EnterpriseChrome session={user} vessel={route.vessel} language={language} onLanguageChange={setLanguage} onNavigate={(href, committed) => guard(() => { committed?.(); window.location.assign(href); })} title={route.plan ? (localizedText(en, "pairs.m_2546e6e66d60b12f")) : (localizedText(en, "pairs.m_d0517aed8e15637a"))} mainId="technical-main"/>
    <main id="technical-main" tabIndex={-1}><EnterprisePageHeader eyebrow={localizedText(en, "pairs.m_3f733356f26f9757")} title={route.plan ? (localizedText(en, "pairs.m_2546e6e66d60b12f")) : (localizedText(en, "pairs.m_d0517aed8e15637a"))} description={localizedText(en, "pairs.m_c75e40d5e0f8d2bc")} actionsLabel={localizedText(en, "pairs.m_18eedcbac7ec94d9")} actions={canWrite && !editor && !jobDraft && !error && <Button disabled={busy || uncertain || observationBusy || observationUncertain || offline} onClick={() => start(true)}><Plus aria-hidden="true"/>{localizedText(en, "pairs.m_9b0263faba2b6d6f")}</Button>}/>
      <nav className="enterprise-section-tabs" aria-label={localizedText(en, "pairs.m_3c3c7be095bfceaa")}><Button variant={!route.plan ? 'secondary' : 'ghost'} aria-current={!route.plan ? 'page' : undefined} disabled={busy || uncertain || observationBusy || observationUncertain || (!!draftContext || observationDirty) && !canShowPrivate} onClick={() => navigate({ ...route, plan: false, id: null })}>{localizedText(en, "pairs.m_77e9d3d4c2e4b44f")}</Button><Button variant={route.plan ? 'secondary' : 'ghost'} aria-current={route.plan ? 'page' : undefined} disabled={busy || uncertain || observationBusy || observationUncertain || (!!draftContext || observationDirty) && !canShowPrivate} onClick={() => navigate({ ...route, plan: true, id: null })}>{localizedText(en, "pairs.m_aa23756ffbae97e7")}</Button></nav>
      {offline && <p role="alert">{localizedText(en, "pairs.m_20305c523753febe")}</p>}
      {owner && (!sameOwner || !sameObservationOwner) && <p role="alert">{localizedText(en, 'pairs.m_1110a2b47b9eb7dd')}</p>}
      {(error || uncertain || draftContext && !canShowPrivate || (observationDirty || observationUncertain) && !canShowPrivate) && <EnterpriseActions align="start" aria-label={localizedText(en, 'pairs.m_79b34eda155292bd')}><Button variant="outline" disabled={busy || observationBusy || offline || !visible} onClick={() => setAttempt(x => x + 1)}>{localizedText(en, 'pairs.m_542619365a11df9d')}</Button></EnterpriseActions>}
      {confirmed && confirmed.owner === owner && confirmed.vessel === route.vessel && confirmed.plan === route.plan && route.id === confirmed.id && <div role="status"><p>{localizedText(en, 'pairs.m_79054af08f971230')}</p><code>{confirmed.id}</code></div>}
      {error ? <div role="alert"><p>{error === 401 ? (localizedText(en, "pairs.m_a5811cfb5b422a85")) : error === 403 || error === 404 ? (localizedText(en, "pairs.m_37decc04e315e7ef")) : (localizedText(en, "pairs.m_45f069187f5778f4"))}</p></div> : !canShowPrivate || !assets || route.plan && !plans ? <p role="status">{localizedText(en, "pairs.m_c9914bd9caad7a7f")}</p> : <>
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
          {!editor && asset && !route.plan && user && <DefectEquipmentLaunch session={user} vessel={route.vessel} assetId={asset.id} revision={asset.revision} language={language} disabled={dirty || observationDirty || busy || observationBusy || uncertain || observationUncertain || offline} onNavigate={href => guard(() => window.location.assign(href))}/>}
          {!editor && plan && route.plan && <MaintenancePlanSummary plan={plan} asset={assets.items.find(asset => asset.id === plan.assetId)} references={refs} en={en}/>}
          {!editor && plan && route.plan && user && <WorkOrderPlanLaunch session={user} vessel={route.vessel} planId={plan.id} revision={plan.revision} language={language} disabled={dirty || observationDirty || busy || observationBusy || uncertain || observationUncertain || offline} onNavigate={href => guard(() => window.location.assign(href))}/>}
          {(canWrite && !editor && (asset || plan) || canHistory && route.id) && <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_2ce1bd8b350fd2a6")}>{canWrite && !editor && (asset || plan) && <Button variant="outline" disabled={busy || uncertain || observationBusy || observationUncertain || offline} onClick={() => start(false)}>{localizedText(en, "pairs.m_2a8233db25e97857")}</Button>}{canHistory && route.id && <Button variant="outline" disabled={observationBusy || observationUncertain} onClick={loadHistory}>{localizedText(en, "pairs.m_247303d64af5e1f0")}</Button>}</EnterpriseActions>}
          {historyError && <p role="alert">{localizedText(en, "pairs.m_8918abcb6db89258")}</p>}
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
      {/* Suspension removes private DOM without discarding the separately owned
          observation component's in-memory attempt during access revalidation. */}
      {observationContext && !editor && <MaintenanceObservations key={`${observationContext.owner}:${observationContext.vessel}:${observationContext.asset}`} vessel={observationContext.vessel} asset={observationContext.asset} owner={observationContext.owner} en={en} onDirty={setObservationDirty} onUncertain={setObservationUncertain} onBusy={setObservationBusy} onAccessLost={recheckObservationAccess}
          suspended={!canShowPrivate || observationContext.owner !== owner || route.plan || observationContext.vessel !== route.vessel || observationContext.asset !== route.id}/>}
      {route.plan && canWrite && !editor && !jobDraft && <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_b5993fcb908c17b7")}><Button variant="outline" disabled={busy || uncertain || observationBusy || observationUncertain || offline} onClick={() => guard(() => { if (!owner) return; setDraftContext({ owner, vessel: route.vessel, plan: true }); setJobDraft({ id: crypto.randomUUID(), code: '', title: '' }); })}>{localizedText(en, "pairs.m_dcc7486f86fd92d9")}</Button></EnterpriseActions>}
      {canShowPrivate && jobDraft && route.plan && <form className="enterprise-editor-panel" onSubmit={e => { e.preventDefault(); saveJob(); }}><EnterpriseFormGrid disabled={busy || uncertain || conflict || offline || !canWrite}><legend>{localizedText(en, "pairs.m_519c4012eb685675")}</legend>
        <label>{localizedText(en, "pairs.m_060d3419b817d68d")}<input required maxLength={2000} value={jobDraft.code} onChange={e => { setJobDraft({ ...jobDraft, code: e.target.value }); setDirty(true); }}/></label>
        <label>{localizedText(en, "pairs.m_1581566562f17d98")}<input required maxLength={2000} value={jobDraft.title} onChange={e => { setJobDraft({ ...jobDraft, title: e.target.value }); setDirty(true); }}/></label>
        </EnterpriseFormGrid><EnterpriseActions layout="form" aria-label={localizedText(en, "pairs.m_a3d7221596f12c68")}><Button variant="outline" disabled={busy || uncertain} onClick={() => guard(clear)}>{localizedText(en, "pairs.m_b8c70a3fdcf207b3")}</Button><Button type="submit" disabled={busy || offline || conflict || !canWrite}>{uncertain ? localizedText(en, 'pairs.m_9f97920d4c817430') : localizedText(en, "pairs.m_fa8a706b6220e82c")}</Button></EnterpriseActions></form>}
      {canShowPrivate && editor && <form className="enterprise-editor-panel" onSubmit={e => { e.preventDefault(); save(); }}><EnterpriseFormGrid disabled={busy || uncertain || conflict || offline || !canWrite}><legend>{localizedText(en, "pairs.m_9714969a2eddcfe7")}</legend>
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
      </EnterpriseFormGrid><EnterpriseActions layout="form" aria-label={localizedText(en, "pairs.m_ae1f2ff34e96c6a8")}><Button variant="outline" disabled={busy || uncertain} onClick={() => guard(() => { clear(); setAttempt(x => x + 1); })}>{localizedText(en, "pairs.m_b8c70a3fdcf207b3")}</Button><Button type="submit" disabled={busy || offline || conflict || !canWrite}>{uncertain ? (localizedText(en, "pairs.m_9f97920d4c817430")) : (localizedText(en, "pairs.m_7e0cdba2e467dbec"))}</Button></EnterpriseActions></form>}
      {notice && <p role="status">{notice}</p>}
      {pending && canShowPrivate && <TechnicalDiscard en={en} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action(); }}/>}
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
