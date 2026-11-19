import { localizedText, localizedTuple } from "../i18n/messages";
import { lensLabels } from './workspace-lens-api';
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import { searchWorkspace, searchMaxOffset } from './workspace-search-api';
import type { SearchPage } from './workspace-search-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
type Props = {
    language: AppLocale;
    onNavigate: (href: string) => void;
    onClose: () => void;
    authorization: 'allowed' | 'pending' | 'denied' | 'unavailable';
    grant: object | null;
    onRetryAuthorization: () => void;
};
export function WorkspaceSearch({ language, onNavigate, onClose, authorization, grant, onRetryAuthorization }: Props) {
    const en = language === 'en';
    const epoch = useWorkspaceRevalidation();
    const [loadedEpoch, setLoadedEpoch] = useState(-1);
    const [loadedGrant, setLoadedGrant] = useState<object | null>(null);
    const allowed = authorization === 'allowed' && typeof grant === 'object' && grant !== null && !Array.isArray(grant);
    // DEV-022: every authorized destination gets its localized menu label (never the raw route key).
    const destinationLabels: Record<string, string> = { ...Object.fromEntries(Object.entries(lensLabels).map(([key, label]) => [key, localizedTuple(label, en, 1)])), today: localizedText(en, "pairs.m_14d5741b2e6e2585"), fleet: localizedText(en, "pairs.m_624c9bbf6ccfa3dd"), companies: localizedText(en, "pairs.m_3ff4996c5348b429"), sources: localizedText(en, "pairs.m_d52797a6e362de20"), requirements: localizedText(en, "pairs.m_d6ad4bb19c60b2b2"), audit: localizedText(en, "pairs.m_9f8662ba28b5b4fc"), 'tug-command': localizedText(en, "pairs.m_8d10fb1a220cd756"), 'tug-job-board': localizedText(en, "pairs.m_326009e7c4fb72c6"), 'tug-job-intake': localizedText(en, "pairs.m_ba8de4826f405e02"), 'system-health': localizedText(en, "pairs.m_4e40e458d82d0a06"), 'notification-escalation': localizedText(en, "pairs.m_11fcfe7e5ea66ca5"), 'offline-conflict-center': localizedText(en, "pairs.m_494839deca172186") };
    const dialog = useRef<HTMLDialogElement>(null);
    const [query, setQuery] = useState('');
    const [search, setSearch] = useState('');
    const [offset, setOffset] = useState(0);
    const [attempt, setAttempt] = useState(0);
    const [state, setState] = useState<SearchPage | null>(null);
    const [failure, setFailure] = useState<number | null>(null);
    const [offline, setOffline] = useState(!navigator.onLine);
    const errorNode = useRef<HTMLDivElement>(null);
    useEffect(() => {
        const connection = () => {
            setState(null);
            setFailure(null);
            setOffline(!navigator.onLine);
            setAttempt(v => v + 1);
        };
        window.addEventListener('online', connection);
        window.addEventListener('offline', connection);
        return () => { window.removeEventListener('online', connection); window.removeEventListener('offline', connection); };
    }, []);
    useEffect(() => { if (failure !== null)
        errorNode.current?.focus(); }, [failure]);
    useEffect(() => {
        const previous = document.activeElement as HTMLElement | null;
        const node = dialog.current;
        node?.showModal();
        return () => { node?.close(); if (previous?.isConnected)
            previous.focus(); };
    }, []);
    useEffect(() => {
        if (!allowed || offline || document.visibilityState === 'hidden')
            return;
        const controller = new AbortController();
        let active = true;
        const timeout = setTimeout(() => controller.abort(), 10000);
        searchWorkspace(search, offset, controller.signal)
            .then(value => {
            if (active) {
                setFailure(null);
                setState(value);
                setLoadedEpoch(epoch);
                setLoadedGrant(grant);
            }
        }).catch(error => { if (active) {
            setState(null);
            setFailure(error instanceof ApiError ? error.status : 0);
            setLoadedGrant(grant);
        } });
        return () => { active = false; clearTimeout(timeout); controller.abort(); };
    }, [search, offset, attempt, offline, epoch, allowed, grant]);
    const reload = (next: number) => { setState(null); setFailure(null); setOffset(next); setAttempt(v => v + 1); };
    return <dialog className="workspace-search" ref={dialog} aria-labelledby="workspace-search-title" onCancel={e => { e.preventDefault(); onClose(); }}>
    <h2 id="workspace-search-title">{localizedText(en, "pairs.m_a959c1a640f4b9b6")}</h2>
    <button onClick={onClose}>{localizedText(en, "pairs.m_99627faa6a096fa8")}</button>
    <form onSubmit={e => { e.preventDefault(); if (!offline && allowed) {
        setSearch(query.trim());
        reload(0);
    } }}>
      <label htmlFor="workspace-query">{localizedText(en, "pairs.m_cb7d56f4882153fc")}</label>
      <input autoFocus id="workspace-query" type="search" maxLength={100} value={query} onChange={e => setQuery(e.target.value)}/>
      <button disabled={offline || !allowed}>{localizedText(en, "pairs.m_f9903f43e53b0bd3")}</button>
    </form>
    {offline ? <p role="status">{localizedText(en, "pairs.m_9c4236e357f97005")}</p> : !allowed ? authorization === 'pending' ? <p role="status">{localizedText(en, "pairs.m_58fafca251ddf05f")}</p> : <div role="alert"><p>{authorization === 'denied' ? (localizedText(en, "pairs.m_2fb4156a283ad6a0")) : (localizedText(en, "pairs.m_98e89734d7334824"))}</p><button onClick={onRetryAuthorization}>{localizedText(en, "pairs.m_6f48ae08910cd3c3")}</button></div> : failure !== null && loadedGrant === grant ? <div role="alert" tabIndex={-1} ref={errorNode}><p>{failure === 401 ? (localizedText(en, "pairs.m_a5811cfb5b422a85")) : failure === 403 ? (localizedText(en, "pairs.m_2fb4156a283ad6a0")) : (localizedText(en, "pairs.m_a8810a379bbd1ff4"))}</p><button onClick={() => reload(offset)}>{localizedText(en, "pairs.m_044bf27da41fda96")}</button></div> : !state || loadedEpoch !== epoch || loadedGrant !== grant ? <p role="status">{localizedText(en, "pairs.m_c9914bd9caad7a7f")}</p> : <div aria-live="polite">
      {!state.items.length && <p>{localizedText(en, "pairs.m_e42e02602849cfa4")}</p>}
      <ul>{state.items.map(item => <li key={item.key}><a href={item.href} onClick={e => { e.preventDefault(); onNavigate(item.href); }}>{item.kind === 'Destination' ? destinationLabels[item.key] ?? item.label : item.label} {item.kind === 'Ship' ? (localizedText(en, "pairs.m_649d7be11586d454")) : item.kind === 'Tug' ? (localizedText(en, "pairs.m_1f474f50f330d506")) : ''}</a></li>)}</ul>
      <div className="form-actions"><button disabled={!offset} onClick={() => reload(Math.max(0, offset - 25))}>{localizedText(en, "pairs.m_2f7d24c724255a10")}</button><button disabled={!state.hasMore || offset >= searchMaxOffset} onClick={() => reload(offset + 25)}>{localizedText(en, "pairs.m_93a493ffda9a1959")}</button></div>
    </div>}
  </dialog>;
}
