import { localizedText } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { EnterpriseChrome } from './EnterpriseChrome';
import { Button } from '../components/ui/button';
import { EnterprisePageHeader, EnterprisePagination } from './EnterprisePatterns';
import { DraftConfirmation } from './DraftConfirmation';
import { ApiError, session, vessels, type Session } from './api';
import { isGuid } from './identifiers';
import { certificatesApi, type CertificateCommand, type CertificateDto } from './certificates-api';
import { emptyCertificateRenewal, emptyVesselCertificate, type CertificateRenewalContent, type VesselCertificateContent } from './certificate-content';
import { certificateLocationPath, readCertificateLocation, useCertificateLocation, type CertificateLocation } from './certificate-location';
import { CertificateCenterView } from './CertificateCenterView';
import { CertificateAssertionPanel } from './CertificateAssertionPanel';
import type { CertificateVerification } from './certificate-assertion-contract';
import { CertificateExtractionReview } from './CertificateExtractionReview';
import { CertificateEditorView } from './CertificateEditorView';
import { CertificateWorkflowHistory } from './CertificateWorkflowHistory';
import type { CertificateWorkflowEditor, CertificateWorkspaceSource } from './certificate-workspace-model';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import './certificates-enterprise.css';
import { unsavedState, useUnsavedChangesSource } from './unsaved-changes';
const ownerOf = (user?: Session | null) => user?.authenticated && user.canRead && isGuid(user.tenantId) && isGuid(user.actorId) ? user.tenantId + '/' + user.actorId : null;
export function CertificateWorkspace() {
    const [language, setLanguage] = useAppLanguage(), [pending, setPending] = useState<(() => void) | null>(null), [instance, setInstance] = useState(0);
    const dirty = useRef(false), saving = useRef(false);
    useUnsavedChangesSource(() => unsavedState(dirty.current, saving.current));
    const onDirty = useCallback((value: boolean) => { dirty.current = value; }, []), onSaving = useCallback((value: boolean) => { saving.current = value; }, []);
    const clear = () => { dirty.current = false; setInstance(n => n + 1); };
    const guard = (action: () => void) => { if (saving.current)
        return; if (dirty.current)
        setPending(() => action);
    else
        action(); };
    const { route, move } = useCertificateLocation(guard, clear);
    const navigate = (href: string, committed?: () => void) => {
        if (href !== '/app' && !href.startsWith('/app/') || /[\\\p{Cc}]/u.test(href))
            return;
        const next = readCertificateLocation(href);
        guard(() => { committed?.(); clear(); if (next.invalid)
            window.location.assign(href);
        else
            move(next); });
    };
    useEffect(() => {
        const unload = (event: BeforeUnloadEvent) => { if (dirty.current || saving.current)
            event.preventDefault(); };
        window.addEventListener('beforeunload', unload);
        return () => window.removeEventListener('beforeunload', unload);
    }, []);
    return <><CertificateSurface key={certificateLocationPath(route) + '/' + instance} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate} onDirty={onDirty} onSaving={onSaving}/>{pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action(); }}/>}</>;
}
function CertificateSurface({ route, language, onLanguage, onNavigate, onDirty, onSaving }: {
    route: CertificateLocation;
    language: AppLocale;
    onLanguage: (language: AppLocale) => void;
    onNavigate: (href: string, committed?: () => void) => void;
    onDirty: (value: boolean) => void;
    onSaving: (value: boolean) => void;
}) {
    const en = language === 'en', epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [historyOffset, setHistoryOffset] = useState(0);
    const [source, setSource] = useState<CertificateWorkspaceSource | null>(null), [editor, setEditor] = useState<CertificateWorkflowEditor | null>(null);
    const [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [saved, setSaved] = useState(false);
    const [frozen, setFrozen] = useState<{
        owner: string;
        command: CertificateCommand;
        uncertain: boolean;
    } | null>(null);
    const [extractionDirty, setExtractionDirty] = useState(false), [extractionSaving, setExtractionSaving] = useState(false);
    const [assertionDirty, setAssertionDirty] = useState(false), [assertionSaving, setAssertionSaving] = useState(false);
    const dirtyChannels = useRef({ form: false, extraction: false, assertion: false }), savingChannels = useRef({ form: false, extraction: false, assertion: false });
    const formDirty = useCallback((value: boolean) => { dirtyChannels.current.form = value; onDirty(value || dirtyChannels.current.extraction || dirtyChannels.current.assertion); }, [onDirty]);
    const formSaving = useCallback((value: boolean) => { savingChannels.current.form = value; onSaving(value || savingChannels.current.extraction || savingChannels.current.assertion); }, [onSaving]);
    const documentDirty = useCallback((value: boolean) => { dirtyChannels.current.extraction = value; setExtractionDirty(value); onDirty(value || dirtyChannels.current.form || dirtyChannels.current.assertion); }, [onDirty]);
    const documentSaving = useCallback((value: boolean) => { savingChannels.current.extraction = value; setExtractionSaving(value); onSaving(value || savingChannels.current.form || savingChannels.current.assertion); }, [onSaving]);
    const humanDirty = useCallback((value: boolean) => { dirtyChannels.current.assertion = value; setAssertionDirty(value); onDirty(value || dirtyChannels.current.form || dirtyChannels.current.extraction); }, [onDirty]);
    const humanSaving = useCallback((value: boolean) => { savingChannels.current.assertion = value; setAssertionSaving(value); onSaving(value || savingChannels.current.form || savingChannels.current.extraction); }, [onSaving]);
    const running = useRef(false), mounted = useRef(false), writer = useRef<AbortController | null>(null), liveOwner = useRef<string | null>(null);
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = epoch + '/' + attempt + '/' + historyOffset;
    const current = source?.scope === scope && online && visible ? source : null, user = current?.user, owner = ownerOf(user);
    const ownerChanged = !!editor && editor.owner !== owner || !!frozen && frozen.owner !== owner;
    const sourceChanged = editor?.kind === 'certificate' && editor.record && current?.certificate
        ? editor.record.version !== current.certificate.version || editor.record.revision !== current.certificate.revision
        : editor?.kind === 'renewal' && current?.certificate
            ? (editor.record?.certificateVersion ?? editor.certificate.version) !== current.certificate.version || (editor.record?.certificateRevision ?? editor.certificate.revision) !== current.certificate.revision || !!editor.record && !!current.renewal && editor.record.version !== current.renewal.version
            : false;
    const conflict = (failure?.status === 409 || sourceChanged) && !frozen?.uncertain, denied = !!failure && [401, 403, 404].includes(failure.status);
    const title = route.page === 'center' ? (localizedText(en, "pairs.m_5ff973ce17c13943")) : (localizedText(en, "pairs.m_0e4b1a01b223bd75"));
    const base = certificateLocationPath({ ...route, id: null, certificateId: undefined, offset: 0 });
    useLayoutEffect(() => { liveOwner.current = owner; }, [owner]);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort(); }; }, []);
    useEffect(() => { document.title = 'POSEIDON · ' + title; }, [language, title]);
    useEffect(() => {
        if (!online || !visible || route.invalid)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        async function load() {
            const user = await session(c.signal), identity = ownerOf(user);
            if (!identity)
                throw new ApiError(403);
            const ensureOwner = async () => { if (ownerOf(await session(c.signal)) !== identity)
                throw new ApiError(403, 'identity_changed'); };
            if (!route.vessel) {
                const page = await vessels(route.offset, '', c.signal);
                await ensureOwner();
                if (active)
                    setSource({ scope, user, vessels: page.items, hasMore: page.hasMore });
                return;
            }
            if (route.page === 'center') {
                const caps = await certificatesApi.capabilities(route.vessel, c.signal);
                if (route.id) {
                    const [certificate, certificateHistory, renewalCaps] = await Promise.all([
                        certificatesApi.read(route.vessel, route.id, c.signal), certificatesApi.history(route.vessel, route.id, historyOffset, c.signal),
                        certificatesApi.renewalCapabilities(route.vessel, c.signal).catch(() => null),
                    ]);
                    if (certificate.scope !== 'Vessel')
                        throw new ApiError(403);
                    await ensureOwner();
                    if (active) {
                        setSource({ scope, user, caps, certificate, certificateHistory, renewalCaps });
                        setEditor(previous => previous ?? { kind: 'certificate', owner: identity, id: certificate.id, record: certificate, content: structuredClone(certificate.content), dirty: false });
                    }
                }
                else {
                    const selectedScope = route.scope ?? caps.scopes[0];
                    if (!selectedScope || !caps.scopes.includes(selectedScope))
                        throw new ApiError(403);
                    const center = await certificatesApi.center(route.vessel, selectedScope, route.offset, route.temporal ?? null, caps.crewPurpose, c.signal);
                    await ensureOwner();
                    if (active)
                        setSource({ scope, user, caps, center });
                }
            }
            else {
                const renewalCaps = await certificatesApi.renewalCapabilities(route.vessel, c.signal);
                if (route.id) {
                    const [renewal, renewalHistory] = await Promise.all([certificatesApi.renewal(route.vessel, route.id, c.signal), certificatesApi.renewalHistory(route.vessel, route.id, historyOffset, c.signal)]);
                    const certificate = await certificatesApi.read(route.vessel, renewal.certificateId, c.signal);
                    await ensureOwner();
                    if (active) {
                        setSource({ scope, user, renewalCaps, renewal, renewalHistory, certificate });
                        setEditor(previous => previous ?? { kind: 'renewal', owner: identity, id: renewal.id, record: renewal, certificate, content: structuredClone(renewal.content), dirty: false });
                    }
                }
                else if (route.certificateId) {
                    const certificate = await certificatesApi.read(route.vessel, route.certificateId, c.signal);
                    await ensureOwner();
                    if (active) {
                        setSource({ scope, user, renewalCaps, certificate });
                        if (renewalCaps.canWrite && certificate.currentRevision && certificate.state !== 'SUPERSEDED')
                            setEditor(previous => previous ?? { kind: 'renewal', owner: identity, id: crypto.randomUUID(), certificate, content: emptyCertificateRenewal(), dirty: false });
                    }
                }
                else {
                    const renewalPage = await certificatesApi.renewals(route.vessel, route.offset, c.signal);
                    await ensureOwner();
                    if (active)
                        setSource({ scope, user, renewalCaps, renewalPage });
                }
            }
            if (active)
                setFailure(previous => previous && [401, 403, 404].includes(previous.status) ? null : previous);
        }
        load().catch(() => { if (active)
            setSource({ scope, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [route, scope, historyOffset, online, visible]);
    const permitted = editor?.kind === 'certificate' ? !!current?.caps?.canWriteVessel : !!current?.renewalCaps?.canWrite;
    const headEditable = !!editor && (!editor.record || editor.record.canEdit && editor.record.currentRevision && editor.record.state === 'DRAFT');
    const editable = !!owner && !ownerChanged && permitted && headEditable && !busy && !frozen && !conflict && !denied && !extractionDirty && !extractionSaving && !assertionDirty && !assertionSaving;
    const changeCertificate = (content: VesselCertificateContent) => { if (editor?.kind !== 'certificate' || !editable)
        return; setEditor({ ...editor, content, dirty: true }); formDirty(true); setSaved(false); setFailure(null); };
    const changeRenewal = (content: CertificateRenewalContent) => { if (editor?.kind !== 'renewal' || !editable)
        return; setEditor({ ...editor, content, dirty: true }); formDirty(true); setSaved(false); setFailure(null); };
    async function save() {
        if (!editor || !route.vessel || !owner || editor.owner !== owner || frozen && frozen.owner !== owner || running.current || denied || conflict || !permitted || !frozen && !editable)
            return;
        const expectedVersion = editor.record?.version ?? 0, content = structuredClone(editor.content);
        const body = editor.kind === 'certificate'
            ? { id: editor.id, expectedVersion, content, reasonCode: editor.record ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING' }
            : { id: editor.id, expectedVersion, certificateId: editor.record?.certificateId ?? editor.certificate.id, certificateRevision: editor.record?.certificateRevision ?? editor.certificate.revision, certificateVersion: editor.record?.certificateVersion ?? editor.certificate.version, content, reasonCode: editor.record ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING' };
        const command = frozen?.command ?? { key: crypto.randomUUID(), id: editor.id, vessel: route.vessel, expectedVersion, body: JSON.stringify(body) }, wasUnknown = frozen?.uncertain ?? false;
        running.current = true;
        setBusy(true);
        formSaving(true);
        setFrozen({ owner, command, uncertain: wasUnknown });
        setFailure(null);
        setSaved(false);
        const c = new AbortController();
        writer.current = c;
        const timer = setTimeout(() => c.abort(), 15000);
        let uncertain = false, confirmed = false, receiptReceived = false;
        try {
            const fresh = await session(c.signal);
            if (ownerOf(fresh) !== owner) {
                setSource({ scope, failed: true });
                throw new ApiError(403, 'identity_changed');
            }
            if (editor.kind === 'certificate') {
                const receipt = await certificatesApi.save(command, fresh.csrfToken, c.signal);
                receiptReceived = true;
                if (ownerOf(await session(c.signal)) !== owner)
                    throw new ApiError(403, 'identity_changed');
                if (!mounted.current || liveOwner.current !== owner) {
                    uncertain = true;
                    return;
                }
                confirmed = true;
                setFrozen(null);
                formDirty(false);
                setSaved(true);
                setEditor({ ...editor, record: receipt, content: receipt.content, dirty: false });
                const actual = await certificatesApi.read(route.vessel, editor.id, c.signal);
                if (ownerOf(await session(c.signal)) !== owner)
                    throw new ApiError(403, 'identity_changed');
                if (mounted.current && liveOwner.current === owner)
                    setEditor({ ...editor, record: actual, content: actual.content, dirty: false });
            }
            else {
                const receipt = await certificatesApi.saveRenewal(command, fresh.csrfToken, c.signal);
                receiptReceived = true;
                if (ownerOf(await session(c.signal)) !== owner)
                    throw new ApiError(403, 'identity_changed');
                if (!mounted.current || liveOwner.current !== owner) {
                    uncertain = true;
                    return;
                }
                confirmed = true;
                setFrozen(null);
                formDirty(false);
                setSaved(true);
                setEditor({ ...editor, record: receipt, content: receipt.content, dirty: false });
                const actual = await certificatesApi.renewal(route.vessel, editor.id, c.signal);
                if (ownerOf(await session(c.signal)) !== owner)
                    throw new ApiError(403, 'identity_changed');
                if (mounted.current && liveOwner.current === owner)
                    setEditor({ ...editor, record: actual, content: actual.content, dirty: false });
            }
            if (mounted.current && liveOwner.current === owner)
                retry(n => n + 1);
        }
        catch (error) {
            if (!mounted.current)
                return;
            const problem = error instanceof ApiError ? error : new ApiError(0);
            uncertain = !confirmed && (wasUnknown || receiptReceived || problem.status === 0 || problem.status >= 500);
            setFrozen(uncertain ? { owner, command, uncertain: true } : null);
            setFailure(problem);
            if ([401, 403, 404].includes(problem.status)) {
                setSource({ scope, failed: true });
                retry(n => n + 1);
            }
        }
        finally {
            clearTimeout(timer);
            running.current = false;
            writer.current = null;
            if (mounted.current) {
                setBusy(false);
                if (uncertain) {
                    setFrozen({ owner, command, uncertain: true });
                    setFailure(new ApiError(0, 'outcome_unknown'));
                }
                formSaving(uncertain);
            }
        }
    }
    async function documentApplied(receipt: CertificateDto, draftOutcome = true) {
        if (!route.vessel || !editor || editor.kind !== 'certificate' || editor.id !== receipt.id || editor.owner !== owner || !owner)
            return;
        const originalOwner = owner, c = new AbortController();
        // Confirmed child outcomes never clear another form or change its old base.
        setSaved(draftOutcome);
        setFailure(null);
        setEditor(previous => previous?.kind === 'certificate' && previous.id === receipt.id && previous.owner === originalOwner && !previous.dirty
            ? { ...previous, record: receipt, content: structuredClone(receipt.content) } : previous);
        try {
            const fresh = await session(c.signal);
            if (ownerOf(fresh) !== originalOwner)
                throw new ApiError(403, 'identity_changed');
            const actual = await certificatesApi.read(route.vessel, receipt.id, c.signal);
            const after = await session(c.signal);
            if (!mounted.current || liveOwner.current !== originalOwner || ownerOf(after) !== originalOwner)
                return;
            setEditor(previous => previous?.kind === 'certificate' && previous.id === receipt.id && previous.owner === originalOwner && !previous.dirty
                ? { ...previous, record: actual, content: structuredClone(actual.content) } : previous);
            retry(n => n + 1);
        }
        catch {
            if (mounted.current && liveOwner.current === originalOwner) {
                setSource({ scope, failed: true });
                setFailure(new ApiError(403, 'current_source_unavailable'));
            }
        }
    }
    function humanRecorded(value: CertificateVerification) {
        if (editor?.kind !== 'certificate' || !editor.record || editor.id !== value.certificateId || editor.record.revision !== value.certificateRevision || editor.record.version !== value.certificateVersion)
            return;
        void documentApplied({ ...editor.record, verification: value, currentRevision: false, canEdit: false }, false);
    }
    const readCurrent = () => onNavigate(certificateLocationPath({ page: route.page, vessel: route.vessel, id: editor?.record?.id ?? route.id, offset: 0 }));
    return <div className="product-shell enterprise-shell certificate-workspace">
    <EnterpriseChrome session={user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="certificate-main"/>
    <main id="certificate-main" tabIndex={-1}>
      <EnterprisePageHeader eyebrow={localizedText(en, "pairs.m_d04ea02972e2ed89")} title={title} description={localizedText(en, "pairs.m_cf719c40d8d272c2")} actionsLabel={localizedText(en, "pairs.m_18eedcbac7ec94d9")} actions={editor
            ? <Button variant="outline" disabled={busy || !!frozen || extractionSaving || assertionSaving} onClick={() => onNavigate(base)}>{localizedText(en, "pairs.m_acb48ca5df9828f0")}</Button>
            : current?.caps?.canWriteVessel && owner ? <Button onClick={() => { if (owner)
                setEditor({ kind: 'certificate', owner, id: crypto.randomUUID(), content: emptyVesselCertificate(), dirty: false }); }}>{localizedText(en, "pairs.m_cdb347fbf23ff713")}</Button> : undefined}/>
      {route.invalid ? <p role="alert">{localizedText(en, "pairs.m_1c90ff6a4a39b22b")}</p>
            : !online || !visible ? <p role="status">{localizedText(en, "pairs.m_beb50fad76dae884")}</p>
                : !current ? <p role="status">{localizedText(en, "pairs.m_e47de63e4d267b95")}</p>
                    : current.failed ? <div role="alert"><p>{localizedText(en, "pairs.m_8ee0cc6507207fb3")}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_542619365a11df9d")}</Button></div>
                        : !route.vessel ? <section aria-label={localizedText(en, "pairs.m_e1a7436b335e4d17")}><h2>{localizedText(en, "pairs.m_e6921871374e576c")}</h2>
          {current.vessels?.length ? <ul>{current.vessels.map(vessel => <li key={vessel.id}><Button variant="outline" onClick={() => onNavigate(certificateLocationPath({ ...route, vessel: vessel.id, offset: 0 }))}>{vessel.code} · {vessel.name}</Button></li>)}</ul> : <p>{localizedText(en, "pairs.m_f892a57da8a85d13")}</p>}
          <EnterprisePagination label={localizedText(en, "pairs.m_e868bec6dcf66a8e")} previous={localizedText(en, "pairs.m_3ebc6aea5e3f139e")} next={localizedText(en, "pairs.m_e86c29e387920d03")} previousDisabled={!route.offset} nextDisabled={!current.hasMore} onPrevious={() => onNavigate(certificateLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(certificateLocationPath({ ...route, offset: route.offset + 25 }))}/>
        </section>
                            : ownerChanged ? <p role="alert">{localizedText(en, "pairs.m_cc118a6321875778")}</p>
                                : editor ? <>
          <CertificateEditorView editor={editor} source={current} vessel={route.vessel} language={language} editable={editable} busy={busy} frozen={!!frozen} failure={failure} conflict={conflict} denied={denied} saved={saved} onCertificate={changeCertificate} onRenewal={changeRenewal} onSave={() => void save()} onCancel={readCurrent} onNavigate={onNavigate}/>
          {route.id && <CertificateWorkflowHistory source={current} language={language} offset={historyOffset} disabled={busy || !!frozen || editor.dirty || extractionDirty || extractionSaving || assertionDirty || assertionSaving} onOffset={setHistoryOffset}/>}
        </>
                                    : route.page === 'center' && current.center && current.caps ? <CertificateCenterView page={current.center} caps={current.caps} route={route} language={language} onNavigate={onNavigate}/>
                                        : route.page === 'renewal' && current.renewalPage ? <section aria-label={localizedText(en, "pairs.m_b8be8680bac67dd1")}>
          <h2>{localizedText(en, "pairs.m_0504bfb88f7c8672")}</h2>
          <Button variant="outline" onClick={() => onNavigate(certificateLocationPath({ page: 'center', vessel: route.vessel, id: null, offset: 0 }))}>{localizedText(en, "pairs.m_0d0f4d8267cdcbac")}</Button>
          {!current.renewalPage.items.length && <p>{localizedText(en, "pairs.m_a9d17cd187701742")}</p>}
          <ul className="certificate-record-list">{current.renewalPage.items.map(row => <li key={row.id}><Button variant="outline" onClick={() => onNavigate(certificateLocationPath({ page: 'renewal', vessel: route.vessel, id: row.id, offset: 0 }))}>{localizedText(en, "pairs.m_89e95c66ebe0bd3e")} · r{row.revision} · {row.savedAt}</Button><p>{localizedText(en, "pairs.m_d49b61dd1a345a10")}</p></li>)}</ul>
          <EnterprisePagination label={localizedText(en, "pairs.m_9947cd4f2282bcd8")} previous={localizedText(en, "pairs.m_56491fda6fb662a0")} next={localizedText(en, "pairs.m_c3bc0bb00641ca48")} previousDisabled={!route.offset} nextDisabled={!current.renewalPage.hasMore} onPrevious={() => onNavigate(certificateLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(certificateLocationPath({ ...route, offset: route.offset + 25 }))}/>
        </section> : <p role="alert">{localizedText(en, "pairs.m_87f379f88cd920b2")}</p>}
      {editor?.kind === 'certificate' && editor.record && route.vessel && <CertificateExtractionReview key={editor.owner + '/' + route.vessel + '/' + editor.id} vessel={route.vessel} certificateId={editor.id} expectedVersion={editor.record.version} enabled={!!owner && !ownerChanged && !!current?.caps?.canWriteVessel && !!current?.certificate?.canEdit && (current.certificate.version === editor.record.version || extractionSaving) && headEditable && !editor.dirty && !busy && !frozen && (!conflict || extractionSaving) && !denied && !assertionDirty && !assertionSaving} language={language} onDirty={documentDirty} onSaving={documentSaving} onApplied={receipt => void documentApplied(receipt)}/>}
      {editor?.kind === 'certificate' && editor.record && route.vessel && <CertificateAssertionPanel key={editor.owner + '/' + route.vessel + '/' + editor.id} vessel={route.vessel} record={editor.record} enabled={!!owner && !ownerChanged && !!current?.caps?.canReadVessel && (editor.record.currentRevision || assertionSaving) && !editor.dirty && !busy && !frozen && (!conflict || assertionSaving) && !denied && !extractionDirty && !extractionSaving} language={language} onDirty={humanDirty} onSaving={humanSaving} onRecorded={humanRecorded}/>}
      {!current && online && !route.invalid && <Button variant="outline" onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_542619365a11df9d")}</Button>}
      <p className="certificate-field-hint">{localizedText(en, "pairs.m_ee1f80c431bbe464")}</p>
    </main>
  </div>;
}
