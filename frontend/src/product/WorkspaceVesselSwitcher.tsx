import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Anchor, ArrowUpRight, Search, Ship, WifiOff, X } from 'lucide-react';
import { Button } from '../components/ui/button';
import * as api from './api';
import type { Session } from './directory-contract';
import { isGuid } from './identifiers';
import { loadWorkspaceContext } from './workspace-context-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import { decodeTodayVessels } from './today-api';
import { currentUnsavedState } from './unsaved-changes';
import './workspace-vessel-switcher.css';
type Props = {
    session: Session;
    vessel: string | null;
    language: AppLocale;
    onClose: () => void;
    onNavigate: (href: string, committed?: () => void) => void;
};
// Safe, non-enumerating denial reasons (59.01 paragraph 858: 403/404 never reveal
// whether a foreign vessel exists). Every reason states that the current context is kept.
export type SwitchDenial = 'scope' | 'session' | 'unavailable';
export function switchDenial(error: unknown): SwitchDenial {
    const status = error instanceof api.ApiError ? error.status : 0;
    return status === 403 || status === 404 ? 'scope' : status === 401 ? 'session' : 'unavailable';
}
export const switchDenialKey: Record<SwitchDenial, string> = {
    scope: 'pairs.m_b8002f9e764b18ef',
    session: 'pairs.m_2b19d5fd50cd15bd',
    unavailable: 'pairs.m_3a8d042ee60783dc',
};
// A context switch carries no form/entity ID from the old vessel into the new one.
export function vesselContextHref(path: string, vessel: string, kind: 'Ship' | 'Tug') {
    if (!isGuid(vessel))
        throw new api.ApiError(400, 'invalid_context');
    const id = vessel.toLowerCase();
    if (path === '/app/system-health')
        return `/app/system-health?vessel=${id}`;
    if (path === '/app/notification-escalation')
        return `/app/notification-escalation?vessel=${id}`;
    if (path === '/app/offline-conflict-center')
        return `/app/offline-conflict-center?vessel=${id}`;
    if (/^\/app\/today(?:\/|$)/.test(path))
        return `/app/today/${id}`;
    if (/^\/app\/operational-downtime(?:\/|$)/.test(path))
        return kind === 'Ship' ? `/app/operational-downtime/${id}` : `/app/fleet/${id}`;
    const family = /^\/app\/(audit-ready|work-order-center|defects|crew-roster|equipment-hierarchy|maintenance-plan|excel-migration)(?:\/|$)/.exec(path);
    if (family)
        return `/app/${family[1]}/${id}`;
    const tug = /^\/app\/(tug-command|tug-job-board|tug-job-intake|tug-assignment|tug-readiness|tug-equipment-readiness|tug-crew-readiness)(?:\/|$)/.exec(path);
    if (tug && kind === 'Tug')
        return `/app/${tug[1]}?vessel=${id}`;
    return `/app/fleet/${id}`;
}
export function WorkspaceVesselSwitcher({ session, vessel, language, onClose, onNavigate }: Props) {
    const en = language === 'en';
    const epoch = useWorkspaceRevalidation();
    const dialog = useRef<HTMLDialogElement>(null);
    const queryInput = useRef<HTMLInputElement>(null);
    const [query, setQuery] = useState('');
    const [search, setSearch] = useState('');
    const [offset, setOffset] = useState(0);
    const [attempt, retry] = useState(0);
    const scope = `${epoch}:${offset}:${attempt}:${search}`;
    const [result, setResult] = useState<{
        owner: Session;
        epoch: number;
        offset: number;
        search: string;
        attempt: number;
        data?: api.Page<api.Vessel>;
        failed?: boolean;
    } | null>(null);
    const [selection, setSelection] = useState<{
        owner: Session;
        scope: string;
        id: string;
        state: 'checking' | 'denied' | 'saving';
        reason?: SwitchDenial;
    } | null>(null);
    // Read once at opening: the surface below is inert while this modal is open.
    const [unsavedAtOpen] = useState(() => currentUnsavedState());
    const check = useRef<AbortController | null>(null);
    const alive = useRef(true);
    const online = navigator.onLine;
    const visible = document.visibilityState !== 'hidden';
    const current = result?.owner === session && result.epoch === epoch && result.offset === offset && result.search === search && result.attempt === attempt ? result : null;
    const choosing = selection?.owner === session && selection.scope === scope ? selection : null;
    const activeScope = useRef<{
        session: Session;
        scope: string;
    } | null>(null);
    useLayoutEffect(() => {
        activeScope.current = { session, scope };
        return () => { activeScope.current = null; check.current?.abort(); };
    }, [session, scope]);
    useEffect(() => {
        alive.current = true;
        const previous = document.activeElement as HTMLElement | null;
        const node = dialog.current;
        node?.showModal();
        queryInput.current?.focus();
        return () => { alive.current = false; node?.close(); check.current?.abort(); if (previous?.isConnected)
            previous.focus(); };
    }, []);
    useEffect(() => {
        check.current?.abort();
        if (!online || !visible)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => { c.abort(); if (active)
            setResult({ owner: session, epoch, offset, search, attempt, failed: true }); }, 10000);
        api.vessels(offset, '', c.signal, search).then(data => decodeTodayVessels(data, offset)).then(data => {
            if (active && !c.signal.aborted)
                setResult({ owner: session, epoch, offset, search, attempt, data });
        }).catch(() => { if (active)
            setResult({ owner: session, epoch, offset, search, attempt, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; clearTimeout(timer); c.abort(); check.current?.abort(); };
    }, [session, epoch, offset, search, attempt, online, visible]);
    async function choose(id: string) {
        if (!online || !visible || choosing?.state === 'checking' || !current?.data?.items.some(row => row.id === id) || !session.tenantId || !session.actorId)
            return;
        const row = current.data.items.find(item => item.id === id)!;
        // 65.03 paragraph 65 step 2 / UX-038 (paragraph 620): an unsaved form is resolved BEFORE any switch
        // request. 'saving' blocks (outcome unknown); 'dirty' goes straight to the owning
        // workspace guard, so Stay sends no request and keeps the draft. After an explicit
        // discard the destination re-verifies the new context server-side (step 4).
        const unsaved = currentUnsavedState();
        if (unsaved === 'saving') {
            setSelection({ owner: session, scope, id, state: 'saving' });
            return;
        }
        if (unsaved === 'dirty') {
            check.current?.abort();
            setSelection(null);
            onNavigate(vesselContextHref(window.location.pathname, row.id, row.kind), onClose);
            return;
        }
        check.current?.abort();
        const c = new AbortController();
        check.current = c;
        const stillCurrent = () => alive.current && activeScope.current?.session === session && activeScope.current.scope === scope;
        const timer = setTimeout(() => { c.abort(); if (stillCurrent())
            setSelection({ owner: session, scope, id, state: 'denied', reason: 'unavailable' }); }, 10000);
        setSelection({ owner: session, scope, id, state: 'checking' });
        try {
            // A list result is not a switch grant. Recheck the target against the server
            // before handing it to the workspace's existing unsaved-change guard.
            const target = await loadWorkspaceContext(session.tenantId, session.actorId, id, c.signal);
            if (c.signal.aborted || !stillCurrent() || !navigator.onLine || document.visibilityState === 'hidden')
                return;
            if (!target.vessel)
                throw new api.ApiError(502, 'invalid_context');
            setSelection(null);
            onNavigate(vesselContextHref(window.location.pathname, target.vessel.id, target.vessel.kind), onClose);
        }
        catch (error) {
            if (stillCurrent() && !c.signal.aborted)
                setSelection({ owner: session, scope, id, state: 'denied', reason: switchDenial(error) });
        }
        finally {
            clearTimeout(timer);
        }
    }
    const title = localizedText(en, "pairs.m_4f3d73f754a89edf");
    return <dialog ref={dialog} className="workspace-vessel-switcher" aria-labelledby="vessel-switch-title" onCancel={event => { event.preventDefault(); onClose(); }}>
    <div className="vessel-switch-heading"><div><span className="vessel-switch-eyebrow">{localizedText(en, "pairs.m_47032199b4691c00")}</span><h2 id="vessel-switch-title">{title}</h2></div><Button variant="ghost" size="icon" aria-label={localizedText(en, "pairs.m_be2dff8608e83639")} onClick={onClose}><X aria-hidden="true"/></Button></div>
    <p id="vessel-switch-help">{localizedText(en, "pairs.m_462ff70249cacd1d")}</p>
    <form aria-label={localizedText(en, "pairs.m_aeaf477899049aaf")} onSubmit={event => { event.preventDefault(); if (online && visible) {
        setSearch(query.trim());
        setOffset(0);
        retry(n => n + 1);
    } }}>
      <label htmlFor="vessel-switch-query">{localizedText(en, "pairs.m_08ca52213e308b0d")}</label>
      <div className="vessel-switch-query"><input ref={queryInput} id="vessel-switch-query" type="search" maxLength={100} value={query} aria-describedby="vessel-switch-help" onChange={event => setQuery(event.target.value)}/><Button type="submit" variant="outline" disabled={!online || !visible}><Search aria-hidden="true"/>{localizedText(en, "pairs.m_cd496e59db1422e0")}</Button></div>
    </form>
    {!online ? <p role="status" className="vessel-switch-state"><WifiOff aria-hidden="true"/>{localizedText(en, "pairs.m_3387d8d4a9e59e4f")}</p> : !visible ? <p role="status">{localizedText(en, "pairs.m_a9a6ee6c050bd9f7")}</p> : current?.failed ? <div role="alert"><p>{localizedText(en, "pairs.m_21da236313e95714")}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_525dd72b26e12b26")}</Button></div> : !current?.data ? <p role="status">{localizedText(en, "pairs.m_a6455a7ec0514b45")}</p> : <>
      {unsavedAtOpen === 'dirty' && <p className="vessel-switch-unsaved" role="status">{localizedText(en, "pairs.m_e7bc23cb053dacc1")}</p>}
      {choosing?.state === 'saving' && <p role="alert">{localizedText(en, "pairs.m_2492dcd13a8d5ab3")}</p>}
      {choosing?.state === 'denied' && <p role="alert">{localizedText(en, "pairs.m_09c28a05eb60c775")} {localizedText(en, switchDenialKey[choosing.reason ?? 'unavailable'])}</p>}
      {choosing?.state === 'checking' && <p role="status">{localizedText(en, "pairs.m_62efeb5e3f768a57")}</p>}
      {!current.data.items.length && <p role="status">{localizedText(en, "pairs.m_ae517ef119c87e49")}</p>}
      <ul className="vessel-switch-results">{current.data.items.map(row => { const Icon = row.kind === 'Tug' ? Anchor : Ship; const selected = row.id.toLowerCase() === vessel?.toLowerCase(); return <li key={row.id}><button type="button" disabled={choosing?.state === 'checking' || selected} onClick={() => void choose(row.id)} aria-label={`${row.code} · ${row.name} · ${row.kind === 'Tug' ? (localizedText(en, "pairs.m_306aaabcb4e04a9f")) : (localizedText(en, "pairs.m_bb1dbfbac5d5c650"))}`}><Icon aria-hidden="true"/><span><strong>{row.code} · {row.name}</strong><small>{row.kind === 'Tug' ? (localizedText(en, "pairs.m_306aaabcb4e04a9f")) : (localizedText(en, "pairs.m_bb1dbfbac5d5c650"))}{selected && ` · ${localizedText(en, "pairs.m_566583a89f579f0f")}`}</small></span><ArrowUpRight aria-hidden="true"/></button></li>; })}</ul>
      <div className="vessel-switch-pagination"><Button variant="outline" disabled={!offset || choosing?.state === 'checking'} onClick={() => setOffset(n => Math.max(0, n - 25))}>{localizedText(en, "pairs.m_3ebc6aea5e3f139e")}</Button><Button variant="outline" disabled={!current.data.hasMore || offset >= 100000 || choosing?.state === 'checking'} onClick={() => setOffset(n => n + 25)}>{localizedText(en, "pairs.m_1062ab19d3959cdd")}</Button></div>
    </>}
  </dialog>;
}
