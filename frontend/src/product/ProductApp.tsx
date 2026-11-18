import { localizedCopy, localizedText } from "../i18n/messages";
import { useAppLanguage } from "../i18n";
import { EnterpriseChrome } from './EnterpriseChrome';
import { Button } from '../components/ui/button';
import { Plus } from 'lucide-react';
import { DirectoryOverview } from './DirectoryOverview';
import { VesselAttachments } from './VesselAttachments';
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import * as api from './api';
import './product.css';
import { DirectoryEditor } from './DirectoryEditor';
import type { EditorTarget } from './DirectoryEditor';
import { DraftConfirmation } from './DraftConfirmation';
import { CompliancePassport } from './CompliancePassport';
import { RegulatoryProfile, type ProfileDiscardHandle } from './RegulatoryProfile';
import { editorCopy } from './editor-copy';
import { loginHref, readLocation, useWorkspaceLocation } from './workspace-location';
import { TodayWorkspace } from './TodayWorkspace';
import { isSeparateWorkspaceDestination } from './workspace-search-api';
import { AuditWorkspace } from './AuditWorkspace';
import { RegulatoryRegister } from './RegulatoryRegister';
import { enterpriseFocusTargets } from './enterprise-focus';
import { EnterprisePageHeader, EnterprisePagination, EnterpriseSectionHeader, EnterpriseToolbar } from './EnterprisePatterns';
const copy = localizedCopy("ProductApp_tsx_copy");
type State<T> = {
    state: 'loading';
} | {
    state: 'ok';
    data: T;
} | {
    state: 'error';
    status: number;
};
const errorStatus = (error: unknown) => error instanceof api.ApiError ? error.status : 0;
export function ProductApp() {
    const [language, setLanguage] = useAppLanguage();
    const [identity, setIdentity] = useState<State<api.Session>>({ state: 'loading' });
    const [attempt, setAttempt] = useState(0);
    const [identityAttempt, setIdentityAttempt] = useState(0);
    const [auditAllowed, setAuditAllowed] = useState(false);
    const [regulatory, setRegulatory] = useState(false);
    const [regulatoryWrite, setRegulatoryWrite] = useState(false);
    const [offset, setOffset] = useState(0);
    const [kind, setKind] = useState('');
    const [searchInput, setSearchInput] = useState('');
    const [search, setSearch] = useState('');
    const [records, setRecords] = useState<State<api.Page<api.Company | api.Vessel>>>({ state: 'loading' });
    const [detail, setDetail] = useState<State<api.Company | api.Vessel>>({ state: 'loading' });
    const [logoutError, setLogoutError] = useState(false);
    const [editor, setEditor] = useState<EditorTarget | null>(null);
    const [drawer, setDrawer] = useState(false);
    const [offline, setOffline] = useState(!navigator.onLine);
    const [dirty, setDirty] = useState(false);
    const [saving, setSaving] = useState(false);
    const [pendingNavigation, setPendingNavigation] = useState<(() => void) | null>(null);
    const profileDiscard = useRef<ProfileDiscardHandle | null>(null);
    const discardRunning = useRef(false);
    const navigationLive = useRef<{ saving: boolean; pending: (() => void) | null; url: string }>({ saving: false, pending: null, url: '' });
    const registerProfileDiscard = useCallback((handle: ProfileDiscardHandle) => {
        profileDiscard.current = handle;
        return () => { if (profileDiscard.current === handle) profileDiscard.current = null; };
    }, []);
    const profileSaving = useCallback((value: boolean) => { navigationLive.current.saving = value; setSaving(value); }, []);
    const [notice, setNotice] = useState('');
    const [profileInstance, setProfileInstance] = useState(0);
    const t = copy[language];
    const et = editorCopy[language];
    const guard = (action: () => void) => {
        if (saving || navigationLive.current.saving)
            return;
        if (dirty) {
            setDrawer(false);
            navigationLive.current.pending = action;
            setPendingNavigation(() => action);
        }
        else
            action();
    };
    const closeEditor = () => { setEditor(null); setDirty(false); setProfileInstance(v => v + 1); };
    const { route, move } = useWorkspaceLocation(guard, () => { closeEditor(); setDrawer(false); setDetail({ state: 'loading' }); setRecords({ state: 'loading' }); setOffset(0); setSearch(''); setSearchInput(''); setAttempt(v => v + 1); document.getElementById('product-main')?.focus(); });
    const tab = route.tab;
    const selected = route.selected;
    const onboardingOpen = route.onboarding === true;
    const passportOpen = route.passport === true;
    const profileOpen = route.profile || onboardingOpen;
    useLayoutEffect(() => { navigationLive.current = { saving, pending: pendingNavigation, url: window.location.pathname+window.location.search }; });
    async function discardPendingNavigation() {
        if (navigationLive.current.saving || discardRunning.current) return;
        const action = navigationLive.current.pending, original = profileDiscard.current;
        if (!action) return;
        const url = navigationLive.current.url, context = original?.context;
        discardRunning.current = true;
        try {
            if (profileOpen) {
                const canRelease = () => profileDiscard.current === original && original?.context === context &&
                    navigationLive.current.pending === action && navigationLive.current.url === url && window.location.pathname+window.location.search === url;
                if (!original || !await original.discard(canRelease)) return;
                const current = navigationLive.current;
                if (profileDiscard.current !== original || original.context !== context || current.pending !== action || current.saving ||
                    current.url !== url || window.location.pathname+window.location.search !== url || !original.canLeave()) return;
            } else if (navigationLive.current.saving || navigationLive.current.pending !== action) return;
            setPendingNavigation(null); closeEditor(); action();
        } finally { discardRunning.current = false; }
    }
    const formMode = editor !== null || (profileOpen || passportOpen) && selected !== null && tab === 'fleet';
    const registerTab = tab === 'sources' || tab === 'requirements';
    const setSelected = (id: string | null) => move({ tab, selected: id, profile: false });
    const setTab = (value: typeof tab) => move({ tab: value, selected: null, profile: false });
    const user = identity.state === 'ok' ? identity.data : null;
    const canRead = user?.authenticated === true && user.canRead;
    const heading = registerTab ? localizedText(language, tab === 'sources' ? 'pairs.m_d52797a6e362de20' : 'pairs.m_d6ad4bb19c60b2b2') : canRead && !route.invalid && (tab !== 'companies' || user?.canReadCompanies) ? (onboardingOpen ? (localizedText(language, "pairs.m_372d69a18a20b71d")) : passportOpen ? (localizedText(language, "pairs.m_79a9c76a3213e157")) : t[tab]) : t.tenant;
    const canCreateRecord = !formMode && canRead && !route.invalid && !registerTab && tab !== 'today' && tab !== 'audit' && user?.canCreate && (tab !== 'companies' || user.canReadCompanies);
    useEffect(() => {
        if (!canRead || route.invalid)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        api.request<{
            destinations: {
                key: string;
                href: string;
            }[];
            canWriteRegulatory: boolean;
        }>('workspace', c.signal).then(value => {
            if (active) {
                setRegulatory(Array.isArray(value?.destinations) && value.destinations.some(d => d.key === 'sources' && d.href === '/app/sources'));
                setRegulatoryWrite(value.canWriteRegulatory === true);
                setAuditAllowed(Array.isArray(value?.destinations) && value.destinations.some(d => d.key === 'audit' && d.href === '/app/audit'));
            }
        }).catch(() => { if (active) {
            setRegulatory(false);
            setRegulatoryWrite(false);
            setAuditAllowed(false);
        } });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [canRead, route.invalid, identityAttempt]);
    useEffect(() => {
        const update = () => setOffline(!navigator.onLine);
        window.addEventListener('online', update);
        window.addEventListener('offline', update);
        const unload = (event: BeforeUnloadEvent) => { if (dirty || saving)
            event.preventDefault(); };
        window.addEventListener('beforeunload', unload);
        return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update); window.removeEventListener('beforeunload', unload); };
    }, [dirty, saving]);
    useEffect(() => {
        const key = (event: KeyboardEvent) => {
            if (event.key === 'Escape')
                setDrawer(false);
        };
        window.addEventListener('keydown', key);
        return () => window.removeEventListener('keydown', key);
    }, [user?.canSearch]);
    useEffect(() => {
        if (!drawer)
            return;
        // Native inert can withdraw focus from the header before this effect runs.
        const previous = document.querySelector<HTMLElement>('button[aria-controls="workspace-navigation"]') ?? document.activeElement as HTMLElement | null;
        const nav = document.getElementById('workspace-navigation')!;
        const elements = () => enterpriseFocusTargets(nav);
        elements()[0]?.focus();
        const trap = (event: KeyboardEvent) => {
            if (event.key !== 'Tab')
                return;
            const items = elements();
            const first = items[0];
            const last = items.at(-1);
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last?.focus();
            }
            if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first?.focus();
            }
        };
        nav.addEventListener('keydown', trap);
        return () => { nav.removeEventListener('keydown', trap); if (previous?.isConnected)
            previous.focus(); };
    }, [drawer]);
    useEffect(() => { document.title = `POSEIDON · ${heading}`; }, [language, heading]);
    useEffect(() => {
        const controller = new AbortController();
        let active = true;
        const timeout = setTimeout(() => { controller.abort(); }, 10000);
        api.session(controller.signal).then(data => { if (active) {
            setIdentity({ state: 'ok', data });
            if (window.location.pathname === '/app' && data.canRead && !data.canReadCompanies)
                move({ tab: 'fleet', selected: null, profile: false }, true);
        } }).catch(error => { if (active)
            setIdentity({ state: 'error', status: errorStatus(error) }); });
        return () => { active = false; clearTimeout(timeout); controller.abort(); };
    }, [identityAttempt, move]);
    useEffect(() => {
        if (tab === 'audit' || tab === 'today' || registerTab || !canRead || route.invalid || (tab === 'companies' && !user?.canReadCompanies))
            return;
        const controller = new AbortController();
        let active = true;
        const timeout = setTimeout(() => { controller.abort(); }, 10000);
        const request = tab === 'companies' ? api.companies(offset, controller.signal, search) : api.vessels(offset, kind, controller.signal, search);
        request.then(data => { if (active)
            setRecords({ state: 'ok', data }); }).catch(error => { if (active)
            setRecords({ state: 'error', status: errorStatus(error) }); });
        return () => { active = false; clearTimeout(timeout); controller.abort(); };
    }, [canRead, user?.canReadCompanies, route.invalid, tab, registerTab, offset, kind, search, attempt]);
    useEffect(() => {
        if (tab === 'audit' || tab === 'today' || registerTab || !selected || !canRead || (tab === 'companies' && !user?.canReadCompanies))
            return;
        const controller = new AbortController();
        let active = true;
        const timeout = setTimeout(() => controller.abort(), 10000);
        const request = tab === 'companies' ? api.company(selected, controller.signal) : api.vessel(selected, controller.signal);
        request.then(data => { if (active)
            setDetail({ state: 'ok', data }); }).catch(error => { if (active)
            setDetail({ state: 'error', status: errorStatus(error) }); });
        return () => { active = false; clearTimeout(timeout); controller.abort(); };
    }, [selected, tab, registerTab, canRead, user?.canReadCompanies, attempt]);
    const message = (status: number) => status === 401 ? t.expired : status === 403 ? t.forbidden : status === 404 ? t.empty : t.unavailable;
    const retry = () => { setIdentity({ state: 'loading' }); setIdentityAttempt(v => v + 1); setRecords({ state: 'loading' }); setDetail({ state: 'loading' }); setAttempt(v => v + 1); };
    const navigate = (value: typeof tab) => { if (value === tab && !selected) {
        setDrawer(false);
        return;
    } ; guard(() => { closeEditor(); setDrawer(false); setNotice(''); setTab(value); setSearch(''); setSearchInput(''); setOffset(0); setRecords({ state: 'loading' }); setAttempt(v => v + 1); document.getElementById('product-main')?.focus(); }); };
    const follow = (href: string, committed?: () => void) => {
        if (/^\/app\/tug-job-detail\/[0-9a-f-]{36}\?vessel=[0-9a-f-]{36}$/.test(href) || /^\/app\/maintenance-plan\/[0-9a-f-]{36}\?id=[0-9a-f-]{36}$/.test(href) || isSeparateWorkspaceDestination(href.split('?')[0]) || /^\/app\/(crew-roster|equipment-hierarchy|maintenance-plan|excel-migration)\/[0-9a-f-]{36}$/.test(href)) {
            guard(() => { committed?.(); closeEditor(); window.location.assign(href); });
            return;
        }
        const next = href === '/app' && !user?.canReadCompanies ? { tab: 'fleet' as const, selected: null, profile: false } : readLocation(href);
        if (next.invalid || (next.tab === 'companies' && !user?.canReadCompanies))
            return;
        guard(() => { committed?.(); closeEditor(); move(next); setDrawer(false); setDetail({ state: 'loading' }); setRecords({ state: 'loading' }); setOffset(0); setSearch(''); setSearchInput(''); setAttempt(v => v + 1); document.getElementById('product-main')?.focus(); });
    };
    const signout = async () => {
        if (!user)
            return;
        // A normal form navigation follows the IdP logout redirect; the CSRF token stays in memory/DOM.
        const form = document.createElement('form');
        form.method = 'POST';
        form.action = '/api/v1/auth/logout';
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = '__RequestVerificationToken';
        input.value = user.csrfToken;
        form.append(input);
        document.body.append(form);
        try {
            form.submit();
        }
        catch {
            form.remove();
            setLogoutError(true);
        }
    };
    return <div className="product-shell enterprise-shell" onClickCapture={event => {
            const anchor = (event.target as HTMLElement).closest('a');
            if (anchor?.closest('.workspace-search') || anchor?.closest('.enterprise-brand'))
                return;
            if (anchor && !anchor.getAttribute('href')?.startsWith('#') && (dirty || saving)) {
                event.preventDefault();
                event.stopPropagation();
                const url = new URL(anchor.href);
                if (url.origin === window.location.origin && !readLocation(url.pathname).invalid)
                    follow(url.pathname + url.search);
                else
                    guard(() => { window.location.assign(anchor.href); });
            }
        }}>
    <EnterpriseChrome session={route.invalid ? null : user} vessel={tab === 'fleet' || tab === 'today' ? selected : null} language={language} onLanguageChange={setLanguage} onNavigate={follow} title={heading} mainId="product-main" drawer={drawer} onDrawerChange={setDrawer} parentManagesFocus/>
    <main id="product-main" tabIndex={-1} inert={drawer}>

      <EnterprisePageHeader eyebrow={localizedText(language, "pairs.m_a1f7d5bcd8d64885")} title={heading} description={passportOpen ? (localizedText(language, "pairs.m_28f9d1d3c71cf16c")) : formMode ? (localizedText(language, "pairs.m_39eb1567220082da")) : t.intro} actionsLabel={localizedText(language, "pairs.m_18eedcbac7ec94d9")} actions={canCreateRecord ? <Button disabled={saving} onClick={() => guard(() => { closeEditor(); move({ tab, selected: null, profile: false }); setNotice(''); setEditor({ instanceKey: crypto.randomUUID(), resource: tab === 'companies' ? 'companies' : 'vessels' }); })}><Plus aria-hidden="true"/>{et.create}</Button> : (profileOpen || passportOpen) && selected && canRead ? <Button variant="outline" disabled={saving} onClick={() => guard(() => { closeEditor(); move({ tab, selected, profile: false }); })}>{localizedText(language, "pairs.m_09d51047be4de855")}</Button> : undefined}/>
      {offline && <p role="alert">{localizedText(language, "pairs.m_e1c3d24f45c01d66")}</p>}

      {canRead && (route.invalid || (tab === 'companies' && !user?.canReadCompanies)) && <p role="alert">{t.forbidden}</p>}
      {canRead && tab === 'fleet' && selected && detail.state === 'ok' && 'kind' in detail.data && <div className={formMode ? 'enterprise-form-context' : 'workspace-context'}><span>{localizedText(language, "pairs.m_cef97d4feef8c441")}: <strong>{detail.data.kind === 'Ship' ? t.ship : t.tug} · {detail.data.name}</strong></span><Button variant="ghost" disabled={saving} onClick={() => navigate('fleet')}>{localizedText(language, "pairs.m_152af425824e88bf")}</Button></div>}
      {identity.state === 'loading' && <p role="status">{t.loading}</p>}
      {identity.state === 'error' && <div role="alert"><p>{message(identity.status)}</p><Button variant="outline" onClick={() => guard(() => { closeEditor(); retry(); })}>{t.retry}</Button></div>}
      {user && <section className={formMode ? 'enterprise-session-compact' : 'product-session'} aria-label={t.identity}>
        {user.setupRequired ? <p role="status">{t.setup}</p> : !user.authenticated ? <><p>{t.expired}</p><a className="primary-button" href={loginHref()}>{t.signin}</a></> : <><p>{t.tenant}: <strong>{user.tenantName ?? user.tenantId ?? '—'}</strong></p><Button variant="ghost" onClick={() => guard(() => void signout())}>{t.signout}</Button></>}
        {user.authenticated && !user.canRead && <p role="alert">{t.forbidden}</p>}
        {logoutError && <p role="alert">{t.unavailable}</p>}
        {new URLSearchParams(window.location.search).has('auth') && <p role="alert">{t.failed}</p>}
      </section>}
      {pendingNavigation && <DraftConfirmation language={language} onStay={() => { navigationLive.current.pending = null; setPendingNavigation(null); }} onDiscard={() => void discardPendingNavigation()}/>}
      {canRead && !route.invalid && tab === 'audit' && <AuditWorkspace key={`${user?.tenantId}:${user?.actorId}:${selected}:${route.query ?? ''}`} language={language} selected={selected} query={route.query} allowed={auditAllowed} onNavigate={follow}/>}
      {canRead && !route.invalid && tab === 'today' && <TodayWorkspace key={selected ?? 'select'} id={selected} language={language} offline={offline} onNavigate={follow}/>}
      {canRead && regulatory && tab === 'today' && <p><a href="/app/sources" onClick={e => { e.preventDefault(); follow('/app/sources'); }}>{localizedText(language, "pairs.m_feecca63cf4fe395")}</a></p>}
      {canRead && !route.invalid && registerTab && <RegulatoryRegister owner={user?.tenantId && user.actorId ? `${user.tenantId.toLowerCase()}/${user.actorId.toLowerCase()}` : null} key={`${tab}-${selected}-${profileInstance}`} kind={tab === 'sources' ? 'sources' : 'requirements'} id={selected} language={language} csrf={user!.csrfToken} canWrite={regulatoryWrite} offline={offline} onDirty={setDirty} onSaving={setSaving} onNavigate={follow}/>}
      {canRead && !registerTab && tab !== 'audit' && tab !== 'today' && !route.invalid && (tab !== 'companies' || user?.canReadCompanies) && <>
        {notice && <p role="status" className="directory-notice">{notice}</p>}
        {!formMode && records.state === 'ok' && <DirectoryOverview records={records.data.items} fleet={tab === 'fleet'} language={language} offset={offset} hasMore={records.data.hasMore}/>}
        {editor && user && <DirectoryEditor key={editor.instanceKey} target={editor} language={language} csrf={user.csrfToken} onDirty={setDirty} onSaving={setSaving} onCancel={() => guard(closeEditor)} onReload={() => guard(() => { closeEditor(); setDetail({ state: 'loading' }); setAttempt(v => v + 1); })} onSaved={record => { closeEditor(); setSelected(record.id); setDetail({ state: 'loading' }); setRecords({ state: 'loading' }); setOffset(0); setNotice(et.saved); setAttempt(v => v + 1); }}/>}

        {!formMode && <form onSubmit={event => { event.preventDefault(); guard(() => { closeEditor(); setSearch(searchInput.trim()); setOffset(0); setSelected(null); setRecords({ state: 'loading' }); setAttempt(v => v + 1); }); }}>
          <EnterpriseToolbar><label className="enterprise-search-field" htmlFor="directory-search">{localizedText(language, "pairs.m_19d06bf0e7a86d28")}<input id="directory-search" type="search" maxLength={100} value={searchInput} onChange={event => setSearchInput(event.target.value)}/></label>
          {tab === 'fleet' && <label>{t.filter}<select value={kind} onChange={e => { const value = e.target.value; guard(() => { closeEditor(); setKind(value); setOffset(0); setSelected(null); setRecords({ state: 'loading' }); }); }}><option value="">{t.all}</option><option value="Ship">{t.ship}</option><option value="Tug">{t.tug}</option></select></label>}
          <Button type="submit" variant="outline" disabled={saving}>{localizedText(language, "pairs.m_f9903f43e53b0bd3")}</Button></EnterpriseToolbar>
        </form>}
        {!formMode && <section aria-label={t[tab]} aria-live="polite">
          {records.state === 'loading' && <p role="status">{t.loading}</p>}
          {records.state === 'error' && <div role="alert"><p>{message(records.status)}</p>{records.status === 401 ? <a href={loginHref()}>{t.signin}</a> : <Button variant="outline" onClick={() => guard(() => { closeEditor(); retry(); })}>{t.retry}</Button>}</div>}
          {records.state === 'ok' && (records.data.items.length === 0 ? <p>{t.empty}</p> : <div className="product-table"><table><thead><tr><th>{t.code}</th><th>{t.name}</th>{tab === 'fleet' && <th>{t.kind}</th>}</tr></thead><tbody>{records.data.items.map(row => <tr key={row.id}><td>{row.code}</td><td><Button variant="link" size="sm" onClick={() => { if (selected !== row.id)
                guard(() => { closeEditor(); setSelected(row.id); setDetail({ state: 'loading' }); }); }}>{row.name}</Button></td>{'kind' in row && <td>{row.kind === 'Tug' ? t.tug : t.ship}</td>}</tr>)}</tbody></table></div>)}
          {records.state === 'ok' && <EnterprisePagination label={localizedText(language, "pairs.m_93e6768be40ac541")} previous={t.previous} next={t.next} previousDisabled={offset === 0} nextDisabled={!records.data.hasMore} onPrevious={() => guard(() => { closeEditor(); setOffset(v => Math.max(0, v - 25)); setRecords({ state: 'loading' }); setSelected(null); })} onNext={() => guard(() => { closeEditor(); setOffset(v => v + 25); setRecords({ state: 'loading' }); setSelected(null); })}>{t.page} {offset / 25 + 1}</EnterprisePagination>}
        </section>}
        {selected && (!formMode || (profileOpen || passportOpen) && detail.state !== 'ok') && <section className="product-detail" aria-label={t.details}><EnterpriseSectionHeader title={t.details} actions={<Button variant="ghost" onClick={() => guard(() => { closeEditor(); setSelected(null); })}>{t.close}</Button>}/>{detail.state === 'loading' ? <p role="status">{t.loading}</p> : detail.state === 'error' ? <div role="alert"><p>{message(detail.status)}</p><Button variant="outline" onClick={() => guard(() => { closeEditor(); retry(); })}>{t.retry}</Button></div> : <><div className="enterprise-record-actions">{'kind' in detail.data && <Button variant="outline" disabled={saving} onClick={() => guard(() => { closeEditor(); move({ tab, selected, profile: true }); })}>{localizedText(language, "pairs.m_e2ecd6fd41f42570")}</Button>}{'kind' in detail.data && <><Button variant="outline" disabled={saving} onClick={() => guard(() => { closeEditor(); move({ tab, selected, profile: false, onboarding: true }); })}>{localizedText(language, "pairs.m_372d69a18a20b71d")}</Button><Button variant="outline" disabled={saving} onClick={() => guard(() => { closeEditor(); move({ tab, selected, profile: false, passport: true }); })}>{localizedText(language, "pairs.m_79a9c76a3213e157")}</Button></>}{detail.data.canEdit && <Button variant="outline" disabled={saving} onClick={() => guard(() => { closeEditor(); move({ tab, selected, profile: false }); setEditor({ instanceKey: crypto.randomUUID(), resource: tab === 'companies' ? 'companies' : 'vessels', record: detail.data }); })}>{et.edit}</Button>}</div><dl className="enterprise-record-summary"><div><dt>{t.code}</dt><dd>{detail.data.code}</dd></div><div><dt>{t.name}</dt><dd>{detail.data.name}</dd></div>{'kind' in detail.data && <div><dt>{t.kind}</dt><dd>{detail.data.kind === 'Tug' ? t.tug : t.ship}</dd></div>}</dl></>}</section>}
        {!formMode && selected && tab === 'fleet' && user && detail.state === 'ok' && <nav className="enterprise-record-links" aria-label={localizedText(language, "pairs.m_bd26c39cdffd99c5")}><Button variant="outline" asChild><a href={`/app/excel-migration/${selected}`}>{localizedText(language, "pairs.m_f1779f35acec39d7")}</a></Button><Button variant="outline" asChild><a href={`/app/equipment-hierarchy/${selected}`}>{localizedText(language, "pairs.m_6c4fb9c1511200f9")}</a></Button><Button variant="outline" asChild><a href={`/app/crew-roster/${selected}`}>{localizedText(language, "pairs.m_055ca5840cae3a74")}</a></Button></nav>}
        {selected && tab === 'fleet' && user && detail.state === 'ok' && !profileOpen && !passportOpen && !editor && <VesselAttachments key={`${user.tenantId}:${user.actorId}:${selected}`} vessel={selected} language={language} csrf={user.csrfToken} onDirty={setDirty} onSaving={setSaving}/>}
        {profileOpen && selected && tab === 'fleet' && user && detail.state === 'ok' && <RegulatoryProfile onRegisterDiscard={registerProfileDiscard} owner={user.tenantId && user.actorId ? `${user.tenantId.toLowerCase()}/${user.actorId.toLowerCase()}` : null} offline={offline} key={`${selected}-${profileInstance}`} id={selected} language={language} journey={onboardingOpen ? 'onboarding' : 'profile'} csrf={user.csrfToken} onDirty={setDirty} onSaving={profileSaving}/>}
        {passportOpen && selected && tab === 'fleet' && user && detail.state === 'ok' && <CompliancePassport key={`${user.tenantId}:${user.actorId}:${selected}`} vessel={selected} language={language} onNavigate={follow}/>}
      </>}
    </main>
  </div>;
}
