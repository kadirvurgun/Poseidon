import { localizedTuple } from "../i18n/messages";
import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import type { Session } from './directory-contract';
import { lensLabels, loadLenses, type Lenses, type LensItem } from './workspace-lens-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import { WorkspaceSearch } from './WorkspaceSearch';
import './workspace-role-navigation.css';
export type RoleLensProjection = {
    owner: Session | null;
    vessel: string | null;
    epoch: number;
    reference: string;
    items: LensItem[];
};
type Props = {
    session: Session | null;
    vessel?: string | null;
    language: AppLocale;
    onNavigate: (href: string, committed?: () => void) => void;
    onSearchOpen?: () => void;
    embedded?: boolean;
    primary?: boolean;
    scopeEpoch?: number;
    onProjection?: (value: RoleLensProjection | null) => void;
};
export function WorkspaceRoleNavigation(props: Props) {
    const s = props.session;
    return s?.authenticated && s.canRead && s.tenantId && s.actorId
        ? <RoleNavigation key={`${s.tenantId}:${s.actorId}:${props.vessel ?? ''}`} {...props}/> : null;
}
function RoleNavigation({ session, vessel = null, language, onNavigate, onSearchOpen, embedded = false, primary = false, scopeEpoch, onProjection }: Props) {
    const revalidationEpoch = useWorkspaceRevalidation();
    const epoch = scopeEpoch ?? revalidationEpoch;
    const [attempt, retry] = useState(0);
    const [result, setResult] = useState<{
        owner: Session | null;
        epoch: number;
        data: Lenses;
    } | null>(null);
    const [error, setError] = useState(false);
    const [selection, select] = useState<{
        owner: Session | null;
        reference: string;
    } | null>(null);
    const [search, showSearch] = useState<Session | null>(null);
    const en = language === 'en';
    const data = result?.owner === session && result.epoch === epoch ? result.data : null;
    const chosen = selection && selection.owner === session ? data?.lenses.find(x => x.reference === selection.reference) : primary ? data?.lenses[0] : null;
    useEffect(() => {
        onProjection?.(chosen ? { owner: session, vessel, epoch, reference: chosen.reference, items: chosen.items } : null);
    }, [chosen, session, vessel, epoch, onProjection]);
    const resetScope1 = [session, vessel, epoch, attempt] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], session) || !Object.is(previousScope1[1], vessel) || !Object.is(previousScope1[2], epoch) || !Object.is(previousScope1[3], attempt)) {
        setPreviousScope1(resetScope1);
        setResult(null);
        setError(false);
    }
    useEffect(() => {
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        if (navigator.onLine && document.visibilityState !== 'hidden') {
            loadLenses(session!.tenantId!, session!.actorId!, vessel, c.signal)
                .then(value => {
                if (active) {
                    setResult({ owner: session, epoch, data: value });
                    select(old => old && old.owner === session && !value.lenses.some(l => l.reference === old.reference) ? null : old);
                    if (!value.canSearch)
                        showSearch(null);
                }
            })
                .catch(() => {
                if (active)
                    setError(true);
            });
        }
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [session, vessel, epoch, attempt]);
    useEffect(() => {
        if (embedded)
            return;
        const key = (e: KeyboardEvent) => {
            if (data?.canSearch && (e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
                e.preventDefault();
                onSearchOpen?.();
                showSearch(session);
            }
        };
        window.addEventListener('keydown', key);
        return () => window.removeEventListener('keydown', key);
    }, [data, onSearchOpen, session, embedded]);
    return <section className="workspace-role-navigation" aria-label={localizedText(en, "pairs.m_9a1cacfc4f37cf8c")}>
    {!embedded && <p>{localizedText(en, "pairs.m_f5d4051a821a911b")}</p>}
    {!data ? <><span role={error ? 'alert' : 'status'}>{error ? (localizedText(en, "pairs.m_bf7db5d1ac826bde")) : (localizedText(en, "pairs.m_cdf92881bb2210f7"))}</span><Button variant="outline" size="sm" disabled={!navigator.onLine} onClick={() => { setResult(null); retry(x => x + 1); }}>{localizedText(en, "pairs.m_31836b857ab61d01")}</Button></>
            : <>
        <span>{data.mode === 'Fleet' ? (localizedText(en, "pairs.m_89983e56b8a35926")) : data.mode === 'Ship' ? (localizedText(en, "pairs.m_bb1dbfbac5d5c650")) : (localizedText(en, "pairs.m_306aaabcb4e04a9f"))}</span>
        {data.state === 'Configured' && data.lenses.length > 0 ? <>
          <label>{localizedText(en, "pairs.m_4c1e847b1abe43df")} <select value={chosen?.reference ?? ''} onChange={e => select({ owner: session, reference: e.target.value })}>
            {!primary && <option value="">{localizedText(en, "pairs.m_4b29a5ecd7257370")}</option>}
            {data.lenses.map(l => <option key={l.reference} value={l.reference}>{localizedLabel(l, en)}</option>)}
          </select></label>
          {chosen && !primary && <nav aria-label={localizedText(en, "pairs.m_55bf5f1c8238cf56")}>{(['daily', 'review'] as const).map(group => {
                            const items = chosen.items.filter(i => i.group === group);
                            return items.length > 0 && <div key={group}><strong>{group === 'daily' ? (localizedText(en, "pairs.m_4b3e2a3d9ef6a9da")) : (localizedText(en, "pairs.m_7c0e771f757915e8"))}</strong>{items.map(i => <Button key={i.key} variant="ghost" size="sm" aria-current={window.location.pathname + window.location.search === i.href ? 'page' : undefined} onClick={() => onNavigate(i.href)}>{localizedTuple(lensLabels[i.key], en, 1)}</Button>)}</div>;
                        })}{chosen.items.length === 0 && <p role="status">{localizedText(en, "pairs.m_aa0ed8645844937f")}</p>}</nav>}
        </> : <span role="status">{data.state === 'Missing' ? (localizedText(en, "pairs.m_e6df9163b2dcb831")) : data.state === 'Unavailable' ? (localizedText(en, "pairs.m_da69b840d4c5ecc6")) : (localizedText(en, "pairs.m_c5a9c6d91a6298b9"))}</span>}
        {!primary && <Button variant="outline" size="sm" onClick={() => onNavigate(vessel ? `/app/fleet/${vessel}` : '/app/fleet')}>{localizedText(en, "pairs.m_ba91f7fe5286366c")}</Button>}
        {data.canSearch && !embedded && <Button variant="outline" size="sm" onClick={() => { onSearchOpen?.(); showSearch(session); }}>{localizedText(en, "pairs.m_a959c1a640f4b9b6")}</Button>}
      </>}
    {search && search === session && createPortal(<WorkspaceSearch language={language} authorization={data ? (data.canSearch ? 'allowed' : 'denied') : error ? 'unavailable' : 'pending'} grant={data?.canSearch ? result : null} onRetryAuthorization={() => { setResult(null); retry(x => x + 1); }} onClose={() => showSearch(null)} onNavigate={href => onNavigate(href, () => showSearch(null))}/>, document.querySelector('.product-shell') ?? document.body)}
  </section>;
}
