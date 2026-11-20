import { localizedCopy, localizedText } from "../i18n/messages";
import { useAppLanguage } from "../i18n";
import { EnterpriseChrome } from './EnterpriseChrome';
import { CrewEnterpriseRoster, crewPresenceLabel } from './CrewEnterpriseRoster';
import { CrewEnterpriseProfileFields } from './CrewEnterpriseProfile';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Plus, Settings2 } from 'lucide-react';
import './crew-enterprise.css';
import { CrewIdentityHistory } from './CrewIdentityHistory';
import { CrewAssignmentHistory } from './CrewAssignmentHistory';
import { CrewOwningInputs } from './CrewOwningInputs';
import { CrewCatalog } from './CrewCatalog';
import { OfflineApplicationPanel } from './OfflineApplicationPanel';
import { CrewPersonalFiles } from './CrewPersonalFiles';
import { CrewPersonalProfile } from './CrewPersonalProfile';
import { CertificatePanel } from './CertificatePanel';
import { CrewCompetencePanel } from './CrewComplianceWorkspace';
import { useEffect, useRef, useState } from 'react';
import { ApiError, session } from './api';
import type { Session } from './directory-contract';
import * as crew from './crew-api';
import { EnterpriseActions, EnterpriseFormGrid, EnterprisePageHeader } from './EnterprisePatterns';
import { unsavedState, useUnsavedChangesSource } from './unsaved-changes';
const words = localizedCopy("CrewWorkspace_tsx_words");
type Editor = {
    command: crew.CrewCommand;
    key: string;
};
export function CrewWorkspace({ vessel, assignment }: {
    vessel: string;
    assignment?: string;
}) {
    const [language, setLanguage] = useAppLanguage();
    const t = words[language];
    const [user, setUser] = useState<Session | null>(null);
    const [page, setPage] = useState<crew.CrewPage | null>(null);
    const [detail, setDetail] = useState<crew.CrewProfile | null>(null);
    const [current, setCurrent] = useState<{
        state: string;
        vesselId?: string;
        assignmentId?: string;
        policyVersion?: string;
        position?: {
            rank: string | null;
            department: string | null;
        } | null;
    } | null>(null);
    const [positions, setPositions] = useState<crew.CrewPosition[]>([]);
    const [offset, setOffset] = useState(0);
    const [rosterFilter, setRosterFilter] = useState<crew.CrewRosterFilter>({});
    const [generation, setGeneration] = useState(0);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(0);
    const [certificateDirty, setCertificateDirty] = useState(false);
    const [profileDirty, setProfileDirty] = useState(false);
    const [fileDirty, setFileDirty] = useState(false);
    const [offlineDirty, setOfflineDirty] = useState(false);
    const [catalogDirty, setCatalogDirty] = useState(false), [catalogOpen, setCatalogOpen] = useState(false);
    const childDirty = certificateDirty || profileDirty || fileDirty || offlineDirty || catalogDirty;
    const [personMode, setPersonMode] = useState(false);
    const [conversion, setConversion] = useState(false);
    const [positionVersion, setPositionVersion] = useState<string | null>(null);
    const [editor, setEditor] = useState<Editor | null>(null);
    const [saving, setSaving] = useState(false);
    const [result, setResult] = useState<'idle' | 'saved' | 'uncertain' | 'conflict' | 'invalid' | 'denied' | 'expired'>('idle');
    const [online, setOnline] = useState(navigator.onLine);
    const [destination, setDestination] = useState<string | null>(null);
    const inFlight = useRef(false);
    // DEV-021 AC-07: an uncertain outcome blocks a shell context switch like an in-flight save;
    // discarding it would lose the idempotency key/body needed to resolve the write.
    useUnsavedChangesSource(() => unsavedState(!!editor || childDirty, inFlight.current || result === 'uncertain'));
    const knownIdentity = useRef<Session | null>(null);
    const [paused, setPaused] = useState(false);
    const dialog = useRef<HTMLDialogElement>(null);
    const rosterHref = `/app/crew-roster/${encodeURIComponent(vessel)}`;
    const clear = () => { knownIdentity.current = null; setPage(null); setDetail(null); setPositions([]); setCurrent(null); setEditor(null); setUser(null); };
    const resetScope1 = [vessel, assignment, offset, generation] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], assignment) || !Object.is(previousScope1[2], offset) || !Object.is(previousScope1[3], generation)) {
        setPreviousScope1(resetScope1);
        setLoading(true);
        setError(0);
        setPage(null);
        setDetail(null);
        setPositions([]);
        setCurrent(null);
    }
    useEffect(() => {
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        session(c.signal).then(async (identity) => {
            if (!identity.authenticated)
                throw new ApiError(401);
            const value = await crew.roster(vessel, offset, c.signal, rosterFilter);
            const record = assignment ? await crew.profile(vessel, assignment, c.signal) : null;
            const currentAssignment = assignment ? await crew.currentAssignment(vessel, assignment, c.signal) : null;
            const refs = value.canWrite ? await crew.positionPolicy(vessel, c.signal) : { items: [], version: null, state: 'Unavailable' };
            if (active) {
                const old = knownIdentity.current;
                if (old && (old.actorId !== identity.actorId || old.tenantId !== identity.tenantId) || !value.canWrite || !value.canWriteIdentity) {
                    setEditor(previous => previous && (old?.actorId !== identity.actorId || old?.tenantId !== identity.tenantId || !value.canWrite || previous.command.fullName !== null || previous.command.employeeNo !== null) ? null : previous);
                }
                knownIdentity.current = identity;
                setUser(identity);
                setPage(value);
                setDetail(record);
                setCurrent(currentAssignment);
                setPositions(refs.items);
                setPositionVersion(refs.version);
                setPaused(false);
            }
        }).catch(e => { if (active) {
            clear();
            setPaused(false);
            setError(e instanceof ApiError ? e.status : 503);
        } })
            .finally(() => { if (active)
            setLoading(false); clearTimeout(timer); });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [vessel, assignment, offset, generation, rosterFilter]);
    useEffect(() => {
        const update = () => setOnline(navigator.onLine);
        const leave = (e: BeforeUnloadEvent) => { if (editor || childDirty || inFlight.current)
            e.preventDefault(); };
        // Nothing is stored in browser storage. Hide all personal projections when the session leaves view.
        const hide = () => {
            if (document.visibilityState === 'hidden') {
                setPaused(true);
                setPage(null);
                setDetail(null);
                setPositions([]);
                setCurrent(null);
            }
            else {
                setGeneration(v => v + 1);
            }
        };
        const restore = (e: PageTransitionEvent) => { if (e.persisted) {
            setPaused(true);
            setPage(null);
            setDetail(null);
            setGeneration(v => v + 1);
        } };
        window.addEventListener('pageshow', restore);
        window.addEventListener('online', update);
        window.addEventListener('offline', update);
        window.addEventListener('beforeunload', leave);
        document.addEventListener('visibilitychange', hide);
        return () => { window.removeEventListener('pageshow', restore); window.removeEventListener('online', update); window.removeEventListener('offline', update); window.removeEventListener('beforeunload', leave); document.removeEventListener('visibilitychange', hide); };
    }, [editor, childDirty]);
    useEffect(() => { document.title = `POSEIDON · ${assignment ? t.profile : t.title}`; }, [language, assignment, t]);
    useEffect(() => { if (destination && dialog.current && !dialog.current.open)
        dialog.current.showModal(); }, [destination]);
    const start = (row?: crew.CrewRow, personal = false, convert = false) => {
        setPersonMode(personal);
        setConversion(convert);
        setResult('idle');
        setEditor({ key: crypto.randomUUID(), command: {
                id: row?.id ?? crypto.randomUUID(), personId: row?.personId ?? null, baseVersion: personal ? detail?.personVersion ?? null : row?.version ?? null,
                fullName: personal ? row?.fullName ?? null : null, employeeNo: personal ? row?.employeeNo ?? null : null, positionId: row?.positionId ?? null, from: row?.from ?? null, to: row?.to ?? null,
                contractEndDate: row?.contractEndDate ?? null, watchGroup: row?.watchGroup ?? null, sourceObservedAt: row?.sourceObservedAt ?? null, declaredStatus: null, assignmentPolicyVersion: page?.declarationPolicy?.version ?? null,
                identityPolicyVersion: (detail?.identityPolicy ?? page?.identityPolicy)?.version ?? null, identityPurpose: (detail?.identityPolicy ?? page?.identityPolicy)?.purpose ?? null, positionPolicyVersion: positionVersion,
            } });
    };
    const change = (field: keyof crew.CrewCommand, value: string) => setEditor(old => old ? { ...old, command: { ...old.command, [field]: value || null } } : null);
    const save = async () => {
        if (!editor || !user || inFlight.current || !online)
            return;
        inFlight.current = true;
        setSaving(true);
        const c = new AbortController();
        const timer = setTimeout(() => c.abort(), 15000);
        try {
            await (conversion ? crew.convertPerson : personMode ? crew.savePerson : crew.saveCrew)(vessel, editor.command, user.csrfToken, editor.key, c.signal);
            setEditor(null);
            setResult('saved');
            setGeneration(v => v + 1);
        }
        catch (e) {
            if (e instanceof ApiError && (e.status === 401 || e.status === 403)) {
                clear();
                setResult(e.status === 401 ? 'expired' : 'denied');
            }
            else
                setResult(e instanceof ApiError && e.status === 409 ? 'conflict' : e instanceof ApiError && e.status === 400 ? 'invalid' : 'uncertain');
        }
        finally {
            clearTimeout(timer);
            inFlight.current = false;
            setSaving(false);
        }
    };
    const row = detail?.assignment;
    const nullable = (value: string | null | undefined) => value ?? t.unknown;
    const presence = (value: crew.CrewRow) => crewPresenceLabel(value.presenceState, language);
    const navigationCommit = useRef<(() => void) | null>(null);
    const navigate = (href: string, committed?: () => void) => { if (saving || result === 'uncertain')
        return; if (editor || childDirty) {
        navigationCommit.current = committed ?? null;
        setDestination(href);
    }
    else {
        committed?.();
        window.location.assign(href);
    } };
    return <div className="product-shell enterprise-shell crew-shell">
    <EnterpriseChrome session={user} vessel={vessel} language={language} onLanguageChange={setLanguage} onNavigate={navigate} title={assignment ? t.profile : t.title} mainId="crew-main"/>
    <main id="crew-main" tabIndex={-1}>
      <nav className="crew-enterprise-breadcrumbs" aria-label={localizedText(language, "pairs.m_a5d85ef31a6442bb")}><a href={`/app/fleet/${vessel}`} onClick={e => { e.preventDefault(); navigate(`/app/fleet/${vessel}`); }}>{t.fleet}</a>{assignment && <a href={rosterHref} onClick={e => { e.preventDefault(); navigate(rosterHref); }}>{t.roster}</a>}</nav>
      <EnterprisePageHeader eyebrow={localizedText(language, "pairs.m_21d7f247d100e66a")} title={assignment ? t.profile : t.title} description={<>{t.privacy}<span className="enterprise-heading-badge"><Badge tone="warning">{t.unverified}</Badge></span></>} actionsLabel={localizedText(language, "pairs.m_18eedcbac7ec94d9")} actions={page && online && !paused && !assignment && <Button disabled={!page.canWrite || !page.canWriteIdentity || !page.identityPolicy || !!editor || childDirty} onClick={() => start()}><Plus size={16} aria-hidden="true"/>{t.create}</Button>}/>
      {!assignment && page && user && !paused && !editor && <>
        <div className="crew-enterprise-tools"><Button variant="outline" disabled={catalogDirty} aria-expanded={catalogOpen} aria-controls="crew-position-governance" onClick={() => setCatalogOpen(v => !v)}><Settings2 size={15} aria-hidden="true"/>{localizedText(language, "pairs.m_2f23ca7b95b2cfde")}</Button></div>
        {catalogOpen && <div id="crew-position-governance"><CrewCatalog key={`catalog:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}`} vessel={vessel} language={language} csrf={user.csrfToken} onDirty={setCatalogDirty} onChanged={() => setGeneration(v => v + 1)}/></div>}
      </>}
      {assignment && <OfflineApplicationPanel key={`offline:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language} onDirty={setOfflineDirty}/>}
      {!online && <p role="alert">{t.offline}</p>}
      {loading && <p role="status">{t.loading}</p>}
      {error > 0 && <div role="alert"><p>{error === 401 ? t.expired : error === 403 || error === 404 ? t.denied : t.error}</p><button onClick={() => setGeneration(v => v + 1)}>{t.retry}</button>{error === 401 && <a href={`/api/v1/auth/login?returnUrl=${encodeURIComponent(window.location.pathname)}`}>{t.expired}</a>}</div>}
      {result !== 'idle' && <p role={result === 'saved' ? 'status' : 'alert'}>{t[result]}</p>}
      {page && online && !paused && !assignment && <CrewEnterpriseRoster key={`${vessel}:${user?.tenantId}:${user?.actorId}:${generation}:${offset}`} page={page} language={language} locked={!!editor || childDirty} rosterHref={rosterHref} onNavigate={navigate} onPage={setOffset} filter={rosterFilter} onFilter={value => { setOffset(0); setRosterFilter(value); setGeneration(v => v + 1); }}/>}
      {current && !paused && <section aria-label={localizedText(language, "pairs.m_0f4369dd1009441f")}><h2>{localizedText(language, "pairs.m_0f4369dd1009441f")}</h2><p>{current.state === 'ConfiguredEffectiveDeclaration' ? (localizedText(language, "pairs.m_66f7d6ac21cc4bba")) : current.state === 'PolicyUnavailable' ? (localizedText(language, "pairs.m_f2ecfae2aa6ca3d3")) : current.state === 'PolicyStale' ? (localizedText(language, "pairs.m_dd561645c38feea1")) : current.state === 'Ambiguous' ? (localizedText(language, "pairs.m_ae6748989fdd2ee8")) : (localizedText(language, "pairs.m_f16b2cd47e0621fc"))}</p>{current.assignmentId && <><p>{current.position?.rank ?? t.unknown} · {current.position?.department ?? t.unknown}</p><a href={`/app/crew-roster/${current.vesselId}/crew-profile/${current.assignmentId}`} onClick={e => { e.preventDefault(); navigate(`/app/crew-roster/${current.vesselId}/crew-profile/${current.assignmentId}`); }}>{localizedText(language, "pairs.m_0134cdcac5756c3c")}</a></>}</section>}
      {page && online && !paused && row && <section className="crew-enterprise-profile"><h2>{row.fullName ?? t.withheld}</h2>{detail?.statusSpecification && <p role="status">{localizedText(language, "pairs.m_3582031b40650352")}</p>}{row.privacyScope === 'LegacyUnavailable' && <p role="status">{localizedText(language, "pairs.m_6f26031196b3a14e")}</p>}{row.privacyScope === 'Unavailable' && <p role="alert">{localizedText(language, "pairs.m_a942d354c0beca36")}</p>}<p role="status">{presence(row)}</p><p>{row.positionState === 'Stale' ? (localizedText(language, "pairs.m_2f10accb7ba7a7ee")) : null}</p><CrewEnterpriseProfileFields row={row} language={language}/><EnterpriseActions align="start" className="crew-enterprise-profile-actions" aria-label={localizedText(language, "pairs.m_612ca5a34407a7b9")}><Button variant="outline" disabled={!page.canWrite || !!editor || childDirty || !online} onClick={() => start(row)}>{t.edit}</Button>{page.canWrite && page.canWriteIdentity && detail?.personVersion && <><Button variant="outline" disabled={!!editor || childDirty || !online || row.privacyScope !== 'IdentityPermitted'} onClick={() => start(row, true)}>{t.editPerson}</Button>{row.privacyScope === 'LegacyUnavailable' && detail?.canConvertIdentity && <Button variant="outline" disabled={!!editor || childDirty || !online} onClick={() => start(row, true, true)}>{localizedText(language, "pairs.m_197ca3348ce60847")}</Button>}</>}</EnterpriseActions></section>}
      {editor && !paused && <form className="crew-enterprise-editor" onSubmit={e => { e.preventDefault(); void save(); }}><h2>{t.dirty}</h2><EnterpriseFormGrid disabled={saving || result === 'uncertain' || result === 'conflict'}>
        {!conversion && (personMode || !editor.command.personId) && page?.canWriteIdentity && <><label>{t.name}<input maxLength={200} value={editor.command.fullName ?? ''} onChange={e => change('fullName', e.target.value)}/></label><label>{t.employee}<input disabled={personMode && detail?.identityPolicy?.employeeNumberMode === 'ImmutableAfterCapture' && row?.employeeNo != null} maxLength={64} value={editor.command.employeeNo ?? ''} onChange={e => change('employeeNo', e.target.value)}/></label></>}
        {conversion && <p>{localizedText(language, "pairs.m_f2fd334366b67429")}</p>}
        {!personMode && <>{!positionVersion && <p role="status">{localizedText(language, "pairs.m_c7a3e50e0921088f")}</p>}<label>{t.position}<select value={editor.command.positionId ?? ''} onChange={e => change('positionId', e.target.value)}><option value="">{t.unknown}</option>{positions.map(p => <option value={p.id} key={p.id}>{p.code} · {nullable(p.rank)} · {nullable(p.capacity)} · {nullable(p.department)}</option>)}</select></label>
        <label>{t.from}<input type="date" value={editor.command.from ?? ''} onChange={e => change('from', e.target.value)}/></label><label>{t.to}<input type="date" value={editor.command.to ?? ''} onChange={e => change('to', e.target.value)}/></label>
        <label>{t.contract}<input type="date" value={editor.command.contractEndDate ?? ''} onChange={e => change('contractEndDate', e.target.value)}/></label>{page?.declarationPolicy && <label>{localizedText(language, "pairs.m_0a0d99752eb81b8e")}<select value={editor.command.declaredStatus ?? ''} onChange={e => change('declaredStatus', e.target.value)}><option value="">{localizedText(language, "pairs.m_c61f94b717836eaf")}</option>{page.declarationPolicy.statuses.map(status => <option key={status}>{status}</option>)}</select></label>}
        <label>{localizedText(language, "pairs.m_bde6f213fcb482b3")}<input placeholder="2026-11-02T10:00:00+03:00" value={editor.command.sourceObservedAt ?? ''} onChange={e => change('sourceObservedAt', e.target.value)}/></label><label>{t.watch}<input maxLength={64} value={editor.command.watchGroup ?? ''} onChange={e => change('watchGroup', e.target.value)}/></label></>}
      </EnterpriseFormGrid><EnterpriseActions layout="form" aria-label={localizedText(language, "pairs.m_ae1f2ff34e96c6a8")}><Button variant="outline" disabled={saving || result === 'uncertain'} onClick={() => { setEditor(null); setResult('idle'); setGeneration(v => v + 1); }}>{result === 'conflict' ? t.reload : t.cancel}</Button><Button type="submit" disabled={saving || !online || result === 'conflict'}>{result === 'uncertain' ? t.retry : t.save}</Button></EnterpriseActions></form>}
      {destination && <dialog ref={dialog} aria-label={t.dirty} onCancel={() => { navigationCommit.current = null; setDestination(null); }}><p>{t.dirty}</p><button autoFocus disabled={saving || result === 'uncertain'} onClick={() => { navigationCommit.current?.(); navigationCommit.current = null; window.location.assign(destination); }}>{t.reload}</button><button onClick={() => { navigationCommit.current = null; setDestination(null); }}>{t.cancel}</button></dialog>}
      {assignment && detail && user && !editor && !paused && online && error === 0 && <details className="crew-enterprise-disclosure"><summary>{localizedText(language, "pairs.m_ff2239b7ea2f296a")}</summary><div><CrewIdentityHistory key={`identity:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language}/></div></details>}
      {assignment && detail && user && !editor && !paused && online && error === 0 && <details className="crew-enterprise-disclosure"><summary>{localizedText(language, "pairs.m_44f33c577ce6a69b")}</summary><div><CrewAssignmentHistory key={`assignment-history:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language}/></div></details>}
      {assignment && detail && user && !editor && !paused && online && error === 0 && <CrewCompetencePanel key={`competence:${user.actorId}:${user.tenantId}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language}/>}
      {assignment && detail && user && !editor && !paused && online && error === 0 && <CrewOwningInputs key={`owning-inputs:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language}/>}
      {assignment && detail && user && !editor && !paused && error === 0 && <CrewPersonalProfile key={`profile:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language} csrf={user.csrfToken} onDirty={setProfileDirty}/>}
      {assignment && row && detail && user && !editor && !paused && error === 0 && <CrewPersonalFiles key={`files:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} person={row.personId} language={language} csrf={user.csrfToken} onDirty={setFileDirty}/>}
      {assignment && detail && user && !editor && !paused && error === 0 && <CertificatePanel key={`${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} person={row?.personId} language={language} csrf={user.csrfToken} onDirty={setCertificateDirty}/>}
    </main></div>;
}
