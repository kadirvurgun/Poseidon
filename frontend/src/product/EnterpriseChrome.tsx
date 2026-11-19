import { LanguageSelector } from "../components/LanguageSelector";
import { presentationLabel } from '../i18n/labels';
import { localizedLabel, localizedText, sourceText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Anchor, ArrowLeft, ArrowUpRight, Building2, ChevronRight, ClipboardList, FileCheck2, FolderCheck, Gauge, LayoutDashboard, Menu, Search, ShieldCheck, Ship, Users, Wrench, Upload, Waves, X } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { Button } from '../components/ui/button';
import type { Session } from './directory-contract';
import { WorkspaceContextBar } from './WorkspaceContextBar';
import { WorkspaceRoleNavigation } from './WorkspaceRoleNavigation';
import type { RoleLensProjection } from './WorkspaceRoleNavigation';
import { WorkspaceSearch } from './WorkspaceSearch';
import { useEnterpriseNavigation } from './enterprise-navigation';
import { enterpriseFocusTargets } from './enterprise-focus';
import { EnterpriseSidebarNav } from './EnterpriseSidebarNav';
import { AppearanceControls } from '../theme/AppearanceControls';
import './enterprise.css';
const labels: Record<string, {
    tr: string;
    en: string;
    icon: LucideIcon;
    group: string;
}> = {
    'operational-downtime': { tr: sourceText("tr", "pairs.m_0208af14c2c3282e"), en: sourceText("en", "pairs.m_0208af14c2c3282e"), icon: Gauge, group: 'operations' },
    'work-order-center': { tr: sourceText("tr", "pairs.m_8b20599d8e3aae73"), en: sourceText("en", "pairs.m_8b20599d8e3aae73"), icon: ClipboardList, group: 'operations' },
    defects: { tr: sourceText("tr", "pairs.m_e3d6ff22743649dc"), en: sourceText("en", "pairs.m_e3d6ff22743649dc"), icon: Wrench, group: 'operations' },
    'fuel-bunker-log': { tr: sourceText("tr", "pairs.m_4409cb4e233ec268"), en: sourceText("en", "pairs.m_4409cb4e233ec268"), icon: Gauge, group: 'operations' },
    'port-activity-sof': { tr: sourceText("tr", "pairs.m_13875df296e09add"), en: sourceText("en", "pairs.m_13875df296e09add"), icon: Anchor, group: 'operations' },
    'certificate-command-center': { tr: sourceText("tr", "pairs.m_49dd4df69d1c0e57"), en: sourceText("en", "pairs.m_49dd4df69d1c0e57"), icon: FileCheck2, group: 'assurance' },
    'renewal-workflow': { tr: sourceText("tr", "pairs.m_0e4b1a01b223bd75"), en: sourceText("en", "pairs.m_0e4b1a01b223bd75"), icon: ClipboardList, group: 'assurance' },
    'noon-report': { tr: sourceText("tr", "pairs.m_ff485806c57476ca"), en: sourceText("en", "pairs.m_ff485806c57476ca"), icon: ClipboardList, group: 'operations' },
    'deck-daily-log': { tr: sourceText("tr", "pairs.m_75d4c27ced71efda"), en: sourceText("en", "pairs.m_75d4c27ced71efda"), icon: Ship, group: 'operations' },
    'engine-daily-log': { tr: sourceText("tr", "pairs.m_21eb6ede9f2b37f9"), en: sourceText("en", "pairs.m_21eb6ede9f2b37f9"), icon: Wrench, group: 'operations' },
    today: { tr: sourceText("tr", "pairs.m_14d5741b2e6e2585"), en: sourceText("en", "pairs.m_14d5741b2e6e2585"), icon: LayoutDashboard, group: 'operations' },
    fleet: { tr: sourceText("tr", "pairs.m_624c9bbf6ccfa3dd"), en: sourceText("en", "pairs.m_624c9bbf6ccfa3dd"), icon: Ship, group: 'operations' },
    'tug-command': { tr: sourceText("tr", "pairs.m_e0ddb8ababbc1e0a"), en: sourceText("en", "pairs.m_e0ddb8ababbc1e0a"), icon: Anchor, group: 'operations' },
    'tug-job-board': { tr: sourceText("tr", "pairs.m_6127b9cab2d01064"), en: sourceText("en", "pairs.m_6127b9cab2d01064"), icon: ClipboardList, group: 'operations' },
    'tug-job-intake': { tr: sourceText("tr", "pairs.m_763373faffa2b6f9"), en: sourceText("en", "pairs.m_763373faffa2b6f9"), icon: FileCheck2, group: 'operations' },
    'tug-assignment': { tr: sourceText("tr", "pairs.m_e933a2eb7a19d5c1"), en: sourceText("en", "pairs.m_e933a2eb7a19d5c1"), icon: ClipboardList, group: 'operations' },
    'tug-readiness': { tr: sourceText("tr", "pairs.m_ea1a685b360fc1b6"), en: sourceText("en", "pairs.m_ea1a685b360fc1b6"), icon: ShieldCheck, group: 'operations' },
    'tug-equipment-readiness': { tr: sourceText("tr", "pairs.m_5151864934be8fa1"), en: sourceText("en", "pairs.m_5151864934be8fa1"), icon: Wrench, group: 'operations' },
    'tug-crew-readiness': { tr: sourceText("tr", "pairs.m_13bb4091a158976a"), en: sourceText("en", "pairs.m_13bb4091a158976a"), icon: Users, group: 'operations' },
    sources: { tr: sourceText("tr", "pairs.m_dfd9b78b64686784"), en: sourceText("en", "pairs.m_dfd9b78b64686784"), icon: FolderCheck, group: 'assurance' },
    requirements: { tr: sourceText("tr", "pairs.m_397eb5dca109db14"), en: sourceText("en", "pairs.m_397eb5dca109db14"), icon: ShieldCheck, group: 'assurance' },
    audit: { tr: sourceText("tr", "pairs.m_9f8662ba28b5b4fc"), en: sourceText("en", "pairs.m_9f8662ba28b5b4fc"), icon: Gauge, group: 'assurance' },
    'audit-ready': { tr: sourceText("tr", "pairs.m_372c48ba7e84c124"), en: sourceText("en", "pairs.m_372c48ba7e84c124"), icon: ShieldCheck, group: 'assurance' },
    companies: { tr: sourceText("tr", "pairs.m_3ff4996c5348b429"), en: sourceText("en", "pairs.m_3ff4996c5348b429"), icon: Building2, group: 'workspace' },
    'system-health': { tr: sourceText("tr", "pairs.m_4e40e458d82d0a06"), en: sourceText("en", "pairs.m_4e40e458d82d0a06"), icon: Gauge, group: 'workspace' },
    'notification-escalation': { tr: sourceText("tr", "pairs.m_11fcfe7e5ea66ca5"), en: sourceText("en", "pairs.m_11fcfe7e5ea66ca5"), icon: ClipboardList, group: 'workspace' },
    'offline-conflict-center': { tr: sourceText("tr", "pairs.m_494839deca172186"), en: sourceText("en", "pairs.m_494839deca172186"), icon: ClipboardList, group: 'workspace' },
    crew: { tr: sourceText("tr", "pairs.m_9c7ff80bf1b60400"), en: sourceText("en", "pairs.m_9c7ff80bf1b60400"), icon: Users, group: 'operations' },
    equipment: { tr: sourceText("tr", "pairs.m_77e9d3d4c2e4b44f"), en: sourceText("en", "pairs.m_77e9d3d4c2e4b44f"), icon: Wrench, group: 'operations' },
    maintenance: { tr: sourceText("tr", "pairs.m_21f68c2e5c640595"), en: sourceText("en", "pairs.m_21f68c2e5c640595"), icon: ClipboardList, group: 'operations' },
    import: { tr: sourceText("tr", "pairs.m_f1779f35acec39d7"), en: sourceText("en", "pairs.m_f1779f35acec39d7"), icon: Upload, group: 'assurance' }
};
type Props = {
    session: Session | null;
    vessel?: string | null;
    language: AppLocale;
    onLanguageChange: (value: AppLocale) => void;
    onNavigate: (href: string, committed?: () => void) => void;
    title: string;
    mainId: string;
    drawer?: boolean;
    onDrawerChange?: (open: boolean) => void;
    parentManagesFocus?: boolean;
};
export function EnterpriseChrome({ session, vessel = null, language, onLanguageChange, onNavigate, title, mainId, drawer: controlledDrawer, onDrawerChange, parentManagesFocus = false }: Props) {
    const en = language === 'en';
    const navigation = useEnterpriseNavigation(session);
    const [localDrawer, setLocalDrawer] = useState(false);
    const [searchOwner, setSearchOwner] = useState<{
        owner: Session;
    } | null>(null);
    const [roleLens, setRoleLens] = useState<RoleLensProjection | null>(null);
    const projectLens = useCallback((value: RoleLensProjection | null) => setRoleLens(value), []);
    const currentLens = roleLens?.owner === session && roleLens.vessel === vessel && roleLens.epoch === navigation.epoch ? roleLens : null;
    // A service-observation utility remains separately granted by the server;
    // selecting a role lens never grants it or hides it from an assigned operator.
    const menuItems = currentLens ? [...currentLens.items, ...navigation.destinations.filter(item => ['system-health', 'notification-escalation', 'offline-conflict-center'].includes(item.key) && !currentLens.items.some(lens => lens.key === item.key))] : navigation.destinations;
    const menuButton = useRef<HTMLButtonElement>(null);
    const header = useRef<HTMLElement>(null);
    // DEV-025 / 65.03 paragraph 90 + UX-013: scroll padding equals the real sticky bar height, so a
    // focused field is never hidden under it; a non-sticky bar (short viewport / zoom) needs none.
    useLayoutEffect(() => {
        const node = header.current, root = document.documentElement;
        if (!node)
            return;
        const update = () => {
            const sticky = getComputedStyle(node).position === 'sticky';
            root.style.setProperty('--shell-sticky-offset', sticky ? `${Math.ceil(node.getBoundingClientRect().height)}px` : '0px');
        };
        update();
        const observer = typeof ResizeObserver === 'function' ? new ResizeObserver(update) : null;
        observer?.observe(node);
        window.addEventListener('resize', update);
        return () => { observer?.disconnect(); window.removeEventListener('resize', update); root.style.removeProperty('--shell-sticky-offset'); };
    }, []);
    const drawer = controlledDrawer ?? localDrawer;
    const setDrawer = (open: boolean) => { setLocalDrawer(open); onDrawerChange?.(open); };
    const searchOpen = searchOwner !== null && searchOwner.owner === session && !!session?.authenticated && session.canRead;
    const move = (href: string, committed?: () => void) => { setDrawer(false); onNavigate(href, () => { setSearchOwner(null); committed?.(); }); };
    useEffect(() => {
        if (!drawer)
            return;
        const previous = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        return () => { document.body.style.overflow = previous; };
    }, [drawer]);
    useEffect(() => {
        if (typeof window.matchMedia !== 'function')
            return;
        const desktop = window.matchMedia('(min-width: 901px)');
        const closeOnDesktop = () => { if (desktop.matches) {
            setLocalDrawer(false);
            onDrawerChange?.(false);
        } };
        desktop.addEventListener('change', closeOnDesktop);
        return () => desktop.removeEventListener('change', closeOnDesktop);
    }, [onDrawerChange]);
    useEffect(() => {
        if (!drawer || parentManagesFocus)
            return;
        const trigger = menuButton.current;
        const nav = document.getElementById('workspace-navigation');
        const main = document.getElementById(mainId);
        if (main)
            main.inert = true;
        const targets = () => enterpriseFocusTargets(nav);
        targets()[0]?.focus();
        const key = (event: KeyboardEvent) => {
            if (event.key === 'Escape') {
                event.preventDefault();
                setLocalDrawer(false);
                onDrawerChange?.(false);
            }
            if (event.key === 'Tab') {
                const items = targets();
                const first = items[0];
                const last = items.at(-1);
                if (event.shiftKey && document.activeElement === first) {
                    event.preventDefault();
                    last?.focus();
                }
                else if (!event.shiftKey && document.activeElement === last) {
                    event.preventDefault();
                    first?.focus();
                }
            }
        };
        document.addEventListener('keydown', key);
        return () => { if (main)
            main.inert = false; document.removeEventListener('keydown', key); trigger?.focus(); };
    }, [drawer, mainId, onDrawerChange, parentManagesFocus]);
    useEffect(() => {
        const key = (event: KeyboardEvent) => {
            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k' && navigation.canSearch && session) {
                event.preventDefault();
                setLocalDrawer(false);
                onDrawerChange?.(false);
                setSearchOwner({ owner: session });
            }
        };
        window.addEventListener('keydown', key);
        return () => window.removeEventListener('keydown', key);
    }, [navigation.canSearch, navigation.epoch, session, onDrawerChange]);
    const path = window.location.pathname;
    const currentKey = path === '/app' || path === '/app/' ? (session?.canReadCompanies ? 'companies' : 'fleet') : path.startsWith('/app/tug-job-detail') ? 'tug-job-board' : menuItems
        .find(item => { const target = item.href.split('?')[0]; return path === target || path.startsWith(target + '/'); })?.key;
    return <>
    <a className="skip-link" href={`#${mainId}`}>{localizedText(en, "pairs.m_c5380d7bc08b74cb")}</a>
    <header ref={header} className="product-header enterprise-topbar" inert={drawer}>
      <Button ref={menuButton} variant="ghost" size="icon" className="workspace-menu" aria-expanded={drawer} aria-controls="workspace-navigation" aria-label={localizedText(en, "pairs.m_489a9d5d52c1f266")} onClick={() => setDrawer(!drawer)}><Menu /></Button>
      <div className="enterprise-breadcrumb">{vessel && <Button variant="ghost" size="icon-sm" asChild><a href={`/app/fleet/${vessel}`} aria-label={localizedText(en, "pairs.m_09d51047be4de855")} onClick={event => { event.preventDefault(); move(`/app/fleet/${vessel}`); }}><ArrowLeft aria-hidden="true"/></a></Button>}<span>POSEIDON</span><ChevronRight aria-hidden="true"/><strong>{title}</strong></div>
      <div className="enterprise-topbar-actions">
        <AppearanceControls language={language} />
        {navigation.canSearch && <Button variant="outline" className="enterprise-search-trigger" aria-keyshortcuts="Control+K Meta+K" aria-label={localizedText(en, "pairs.m_a959c1a640f4b9b6")} onClick={() => { if (session)
        setSearchOwner({ owner: session }); }}><Search /><span>{localizedText(en, "pairs.m_a959c1a640f4b9b6")}</span><kbd aria-hidden="true" data-shortcut="⌘ / Ctrl K" /></Button>}
        <LanguageSelector locale={language} onChange={onLanguageChange} />
      </div>
      <WorkspaceContextBar session={session} vessel={vessel} language={language} onNavigate={move}/>
    </header>
    {drawer && <button type="button" className="enterprise-nav-overlay" aria-label={localizedText(en, "pairs.m_7cf3ab76311a388d")} onClick={() => setDrawer(false)}/>}
    <nav id="workspace-navigation" className={`product-nav enterprise-sidebar ${drawer ? 'is-open' : ''}`} aria-label="POSEIDON" role={drawer ? 'dialog' : undefined} aria-modal={drawer ? true : undefined}>
      <div className="enterprise-brand-row"><a className="enterprise-brand" href="/app" onClick={event => { event.preventDefault(); move('/app'); }}><span className="enterprise-brand-mark"><Waves aria-hidden="true"/></span><span>POSEIDON{' '}<small>{presentationLabel(language, 'maritimeOperations')}</small></span></a>{drawer && <Button variant="ghost" size="icon" aria-label={localizedText(en, "pairs.m_99627faa6a096fa8")} onClick={() => setDrawer(false)}><X /></Button>}</div>
      <div className="enterprise-nav-scroll">
        <details className="enterprise-role-settings"><summary>{localizedText(en, "pairs.m_10b48db6980a8159")}</summary><WorkspaceRoleNavigation session={session} vessel={vessel} language={language} onNavigate={move} embedded primary scopeEpoch={navigation.epoch} onProjection={projectLens}/></details>
        <EnterpriseSidebarNav key={`${session?.tenantId}:${session?.actorId}:${vessel}`} items={menuItems.flatMap(item => {
            const label = labels[item.key];
            return label ? [{ ...item, label: localizedLabel(label, en), icon: label.icon }] : [];
        })} currentKey={currentKey} language={language} onNavigate={href => move(href)}/>
        {navigation.loading && <div className="enterprise-nav-loading" role="status">{localizedText(en, "pairs.m_e6da24e466eb33e9")}</div>}
        {navigation.failed && <div className="enterprise-nav-loading" role="status"><p>{localizedText(en, "pairs.m_5f4cc449dea3b70e")}</p><Button variant="ghost" onClick={navigation.retry}>{localizedText(en, "pairs.m_80a0813ed59604de")}</Button></div>}
        {currentLens && menuItems.length === 0 && <p className="enterprise-nav-loading" role="status">{localizedText(en, "pairs.m_aa0ed8645844937f")}</p>}
      </div>
      <div className="enterprise-sidebar-footer"><span className="enterprise-workspace-avatar">{session?.tenantName?.slice(0, 2).toLocaleUpperCase(language) ?? 'PO'}</span><div><strong>{session?.tenantName ?? (localizedText(en, "pairs.m_bdd972f1b143ec8b"))}</strong><span>{session?.authenticated ? (localizedText(en, "pairs.m_a8beb04804867ae3")) : (localizedText(en, "pairs.m_3a6354178aac6256"))}</span></div><ArrowUpRight aria-hidden="true"/></div>
    </nav>
    {searchOpen && createPortal(<WorkspaceSearch language={language} authorization={navigation.loading ? 'pending' : navigation.failed ? 'unavailable' : navigation.canSearch ? 'allowed' : 'denied'} grant={navigation.searchGrant} onRetryAuthorization={navigation.retry} onClose={() => setSearchOwner(null)} onNavigate={href => move(href, () => setSearchOwner(null))}/>, document.querySelector('.product-shell') ?? document.body)}
  </>;
}
