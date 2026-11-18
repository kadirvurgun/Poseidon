import { localizedLabel, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import { useEffect, useId, useState } from 'react';
import { createPortal } from 'react-dom';
import { ArrowLeftRight, Building2, ChevronDown, Compass, Wifi, WifiOff } from 'lucide-react';
import { ApiError } from './api';
import type { Session } from './directory-contract';
import { loadWorkspaceContext } from './workspace-context-api';
import type { WorkspaceContext } from './workspace-context-api';
import './workspace-context-bar.css';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import { WorkspaceVesselSwitcher } from './WorkspaceVesselSwitcher';
type Props = {
    session: Session | null;
    vessel?: string | null;
    language: AppLocale;
    onNavigate?: (href: string, committed?: () => void) => void;
};
export function WorkspaceContextBar(props: Props) {
    const identity = props.session;
    if (!identity?.authenticated || !identity.canRead || !identity.tenantId || !identity.actorId)
        return null;
    // Logout destroys the old cache, even if a later login uses the same IDs.
    return <ScopedWorkspaceContextBar key={`${identity.tenantId}:${identity.actorId}:${props.vessel ?? ''}`} {...props}/>;
}
function ScopedWorkspaceContextBar({ session, vessel = null, language, onNavigate }: Props) {
    const epoch = useWorkspaceRevalidation();
    const tenant = session?.tenantId, actor = session?.actorId;
    const allowed = session?.authenticated === true && session.canRead === true && !!tenant && !!actor;
    const scope = `${tenant}:${actor}:${vessel ?? ''}:${epoch}`;
    const [attempt, retry] = useState(0);
    const [result, setResult] = useState<{
        scope: string;
        owner: Session | null;
        data?: WorkspaceContext;
        error?: number;
    } | null>(null);
    const [online, setOnline] = useState(navigator.onLine);
    const [chooser, setChooser] = useState<{
        owner: Session;
        epoch: number;
    } | null>(null);
    const en = language === 'en';
    const detailsId = useId();
    const [expanded, setExpanded] = useState(false);
    useEffect(() => {
        const update = () => setOnline(navigator.onLine);
        window.addEventListener('online', update);
        window.addEventListener('offline', update);
        return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update); };
    }, []);
    useEffect(() => {
        if (!allowed || !tenant || !actor)
            return;
        if (!navigator.onLine || document.visibilityState === 'hidden')
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        loadWorkspaceContext(tenant, actor, vessel, c.signal).then(data => { if (active)
            setResult({ scope, owner: session, data }); })
            .catch(error => { if (active)
            setResult({ scope, owner: session, error: error instanceof ApiError ? error.status : 0 }); });
        return () => { active = false; clearTimeout(timer); c.abort(); };
    }, [allowed, tenant, actor, scope, vessel, attempt, session]);
    if (!allowed)
        return null;
    const current = result?.scope === scope && result.owner === session ? result : null;
    // 65.03 paragraph 65 step 6: a vessel the server no longer grants shows a reason and a
    // safe chooser; it never falls back to another vessel or shows the old record's name.
    const deniedScope = !!vessel && !current?.data && (current?.error === 403 || current?.error === 404);
    const roleText = current?.data ? (current.data.roleContext.state === 'Configured'
        ? current.data.roleContext.roles.map(role => localizedLabel(role, en)).join(' · ')
        : current.data.roleContext.state === 'Missing' ? localizedText(en, "pairs.m_2fd8f713bc6d358f") : localizedText(en, "pairs.m_49138f2831bf4262")) : '';
    const vesselText = current?.data ? (current.data.vessel ? `${current.data.vessel.code} · ${current.data.vessel.name}` : localizedText(en, "pairs.m_a46569aa0232494e")) : '';
    const showData = online && document.visibilityState !== 'hidden' && !!current?.data;
    // DEV-021: one dedicated polite live region holding only the verified context text
    // (vessel, mode, role), so a context arriving after a switch is perceivable without moving
    // focus. The section itself is not live, so the role=status/alert children below are never
    // nested inside another live region.
    const modeText = current?.data?.vessel ? `${localizedText(en, "pairs.m_ed4e66320421b039")}: ${current.data.vessel.kind === 'Ship' ? localizedText(en, "pairs.m_bb1dbfbac5d5c650") : localizedText(en, "pairs.m_306aaabcb4e04a9f")}` : '';
    const companyText = current?.data ? `${localizedText(en, "pairs.m_d9580bbb7b397961")}: ${current.data.company ? `${current.data.company.code} · ${current.data.company.name}` : localizedText(en, "pairs.m_49138f2831bf4262")}` : '';
    const connectionText = online ? localizedText(en, "pairs.m_0772a120920395b6") : localizedText(en, "pairs.m_a24f7431b69a1715");
    const contextLabel = localizedText(en, "pairs.m_8628de1435d643d5");
    // DEV-025 / 65.03 paragraph 48: on a narrow screen the context collapses into one summary
    // button whose accessible name carries the full context (workspace, company, vessel, mode,
    // role, connection). CSS shows it only at <=767px (65.03 paragraph 71); the verified values
    // stay in the polite live region, which is only visually clipped, never removed.
    const summaryName = showData ? [`${contextLabel}: ${session?.tenantName ?? ''}`, companyText, vesselText, modeText, `${localizedText(en, "pairs.m_fdbbf279aa546fe7")}: ${roleText}`, connectionText].filter(Boolean).join(' · ') : '';
    return <section className="workspace-global-context" aria-label={contextLabel} data-collapsible={showData || undefined} data-expanded={showData ? String(expanded) : undefined}>
    {showData && <button type="button" className="context-summary-toggle" aria-expanded={expanded} aria-controls={detailsId} aria-label={summaryName} onClick={() => setExpanded(value => !value)}>
      <span className="context-summary-text" aria-hidden="true">{current?.data?.vessel ? current.data.vessel.code : vesselText}</span><ChevronDown aria-hidden="true"/>
    </button>}
    <div id={detailsId} className="context-details">
    <span className="context-workspace">{current?.data && session?.tenantName || (localizedText(en, "pairs.m_e9d69ba1c5c8afd8"))}</span>
    {!online ? <span role="status">{localizedText(en, "pairs.m_cdfc48e331f96299")}</span> : document.visibilityState === 'hidden' ? <span role="status">{localizedText(en, "pairs.m_0713fa35fcf43a0b")}</span> : !current ? <span role="status">{localizedText(en, "pairs.m_93cbd8401551ea19")}</span> : !current.data ? <>
      <span role="alert">{current.error === 401 ? localizedText(en, "pairs.m_221de6ea6fc51bb7") : deniedScope ? localizedText(en, "pairs.m_9b1a1deb14aac372") : current.error === 403 || current.error === 404 ? (localizedText(en, "pairs.m_e738676f907d8b26")) : (localizedText(en, "pairs.m_e59c3d6f026fb1d7"))}</span>
      <Button variant="outline" size="sm" disabled={!online} onClick={() => { setResult(null); retry(n => n + 1); }}>{localizedText(en, "pairs.m_d6420ab086c48ebe")}</Button>
    </> : null}
    <span className="context-live" aria-live="polite" aria-atomic="true">{showData && current?.data && <>
      <span className="context-company"><Building2 aria-hidden="true"/>{localizedText(en, "pairs.m_d9580bbb7b397961")}: <strong>{current.data.company ? `${current.data.company.code} · ${current.data.company.name}` : localizedText(en, "pairs.m_49138f2831bf4262")}</strong></span>
      <span className="context-vessel">{vesselText}</span>
      {current.data.vessel && <span className="context-mode"><Compass aria-hidden="true"/>{modeText}</span>}
      <span className="context-role">{localizedText(en, "pairs.m_fdbbf279aa546fe7")}: {roleText}</span>
    </>}</span>
    </div>
    {onNavigate && (current?.data || deniedScope) && online && session && <Button variant="outline" size="sm" className="context-switch-trigger" onClick={() => setChooser({ owner: session, epoch })}><ArrowLeftRight aria-hidden="true"/>{deniedScope ? localizedText(en, "pairs.m_5db2b92c3bcc6931") : localizedText(en, "pairs.m_4f3d73f754a89edf")}</Button>}
    <span className="context-connection">{online ? <Wifi aria-hidden="true"/> : <WifiOff aria-hidden="true"/>}{online ? (localizedText(en, "pairs.m_0772a120920395b6")) : (localizedText(en, "pairs.m_a24f7431b69a1715"))}</span>
    {chooser?.owner === session && chooser.epoch === epoch && session && onNavigate && createPortal(<WorkspaceVesselSwitcher session={session} vessel={vessel} language={language} onClose={() => setChooser(null)} onNavigate={onNavigate}/>, document.querySelector('.product-shell') ?? document.body)}
  </section>;
}
