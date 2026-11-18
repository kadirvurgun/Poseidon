import { localizedText } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination } from './EnterprisePatterns';
import { EnterpriseChrome } from './EnterpriseChrome';
import { DraftConfirmation } from './DraftConfirmation';
import { ApiError, session, vessel as readVessel, vessels, type Session, type Vessel } from './api';
import { isGuid } from './identifiers';
import { emptyDefectFields, type DefectFields } from './defect-fields';
import { defectApi, defectInputErrors, freezeDefectCommand, type DefectAttempt, type DefectAssetContext, type DefectAssetPin, type DefectCapabilities, type DefectHistory, type DefectPage, type DefectRecord } from './defect-api';
import { readDefectLocation, defectLocationPath, useDefectLocation, type DefectLocation } from './defect-location';
import { DefectFieldsForm } from './DefectFieldsForm';
import { DefectAssetAside, DefectRecordHistory, DefectRecordList, DefectSourceSummary, DefectTemporalTimeline } from './DefectRecordView';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import { DowntimeSourceLaunch } from './DowntimeSourceLaunch';
import { unsavedState, useUnsavedChangesSource } from './unsaved-changes';
type Editor = {
    owner: string;
    id: string;
    fields: DefectFields;
    record: DefectRecord | null;
    asset: DefectAssetPin | null;
    dirty: boolean;
};
type Source = {
    scope: string;
    user?: Session;
    vessel?: Vessel;
    vessels?: Vessel[];
    hasMore?: boolean;
    caps?: DefectCapabilities;
    page?: DefectPage;
    record?: DefectRecord;
    history?: DefectHistory;
    assetContext?: DefectAssetContext;
    assetIssue?: 'stale' | 'unavailable';
    failed?: boolean;
};
const identity = (user?: Session) => user?.authenticated && user.canRead && isGuid(user.actorId) && isGuid(user.tenantId) ? user.tenantId + '/' + user.actorId : null;
const mutable = (record: DefectRecord): DefectFields => structuredClone(record.fields);
export function DefectWorkspace() {
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
    const { route, move } = useDefectLocation(guard, clear);
    const navigate = (href: string, committed?: () => void) => {
        if (href !== '/app' && !href.startsWith('/app/') || /[\\\p{Cc}]/u.test(href))
            return;
        const next = readDefectLocation(href);
        guard(() => { committed?.(); clear(); if (next.invalid)
            window.location.assign(href);
        else
            move(next); });
    };
    const canonical = (id: string) => { dirty.current = false; saving.current = false; move({ vessel: route.vessel, id, offset: 0 }, true); };
    useEffect(() => { const unload = (event: BeforeUnloadEvent) => { if (dirty.current || saving.current)
        event.preventDefault(); }; window.addEventListener('beforeunload', unload); return () => window.removeEventListener('beforeunload', unload); }, []);
    return <><DefectSurface key={route.vessel + '/' + instance} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate} onCanonical={canonical} onDirty={onDirty} onSaving={onSaving}/>{pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action(); }}/>}</>;
}
function DefectSurface({ route, language, onLanguage, onNavigate, onCanonical, onDirty, onSaving }: {
    route: DefectLocation;
    language: AppLocale;
    onLanguage: (value: AppLocale) => void;
    onNavigate: (href: string, committed?: () => void) => void;
    onCanonical: (id: string) => void;
    onDirty: (value: boolean) => void;
    onSaving: (value: boolean) => void;
}) {
    const en = language === 'en', epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [historyOffset, setHistoryOffset] = useState(0);
    const [draftId] = useState(() => crypto.randomUUID());
    const [source, setSource] = useState<Source | null>(null), [editor, setEditor] = useState<Editor | null>(null), [frozen, setFrozen] = useState<DefectAttempt | null>(null);
    const [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({}), [receipt, setReceipt] = useState<{
        owner: string;
        value: DefectRecord;
    } | null>(null);
    const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef({ owner: null as string | null, scope: '' });
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = defectLocationPath(route) + '/' + epoch + '/' + attempt + '/' + historyOffset;
    const current = source?.scope === scope && online && visible ? source : null, user = current?.user, owner = identity(user);
    const title = (localizedText(en, "pairs.m_12ecbc7ff21b0959")), sameOwner = !editor || editor.owner === owner, sameAttempt = !frozen || frozen.owner === owner;
    const sourceChanged = !!editor?.record && !!current?.record && (editor.record.version !== current.record.version || editor.record.revision !== current.record.revision);
    const conflict = !frozen && (failure?.status === 409 || sourceChanged), denied = !!failure && [401, 403, 404].includes(failure.status);
    const assetBlocked = !!editor && !editor.record && !!route.asset && (!editor.asset || !current?.assetContext || current.assetContext.pin.version !== editor.asset.version || current.assetContext.pin.hash !== editor.asset.hash);
    const headEditable = !editor?.record || editor.record.currentRevision && editor.record.canEdit;
    const editable = !!owner && sameOwner && sameAttempt && !!current?.caps?.canWrite && headEditable && !busy && !frozen && !conflict && !denied;
    const base = defectLocationPath({ vessel: route.vessel, id: null, offset: 0 });
    useLayoutEffect(() => { committed.current = { owner, scope }; if (!online || !visible)
        writer.current?.abort(); }, [owner, scope, online, visible]);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort(); }; }, []);
    useEffect(() => { document.title = 'POSEIDON · ' + title; }, [language, title]);
    useEffect(() => {
        if (!online || !visible || route.invalid)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        async function load() {
            const user = await session(c.signal), original = identity(user);
            if (!original)
                throw new ApiError(403);
            const ensureOwner = async () => { if (identity(await session(c.signal)) !== original)
                throw new ApiError(403, 'identity_changed'); };
            if (!route.vessel) {
                const page = await vessels(route.offset, '', c.signal);
                await ensureOwner();
                if (active)
                    setSource({ scope, user, vessels: page.items, hasMore: page.hasMore });
                return;
            }
            const [caps, vessel] = await Promise.all([defectApi.capabilities(route.vessel, c.signal), readVessel(route.vessel, c.signal)]);
            if (!caps.canRead || vessel.id !== route.vessel)
                throw new ApiError(403);
            async function context(pin: {
                id: string;
                revision: number;
            }, expected?: DefectAssetPin): Promise<{
                assetContext?: DefectAssetContext;
                assetIssue?: 'stale' | 'unavailable';
            }> {
                try {
                    return { assetContext: await defectApi.assetContext(vessel.id, pin.id, pin.revision, c.signal, expected) };
                }
                catch (error) {
                    if (error instanceof ApiError && [401, 403].includes(error.status))
                        throw error;
                    return { assetIssue: error instanceof ApiError && error.status === 409 ? 'stale' : 'unavailable' };
                }
            }
            if (route.id) {
                const [record, history] = await Promise.all([defectApi.read(route.vessel, route.id, c.signal), defectApi.history(route.vessel, route.id, historyOffset, c.signal)]);
                const asset = record.asset ? await context(record.asset, record.asset) : {};
                await ensureOwner();
                if (active) {
                    setSource({ scope, user, vessel, caps, record, history, ...asset });
                    setEditor(previous => !previous ? { owner: original, id: record.id, fields: mutable(record), record, asset: record.asset, dirty: false } : previous.owner === original && previous.id === record.id && !previous.dirty && previous.record && !previous.record.currentRevision ? { ...previous, record, fields: mutable(record), asset: record.asset } : previous);
                }
            }
            else if (route.asset) {
                const asset = await context(route.asset);
                await ensureOwner();
                if (active) {
                    setSource({ scope, user, vessel, caps, ...asset });
                    if (caps.canWrite)
                        setEditor(previous => previous ?? { owner: original, id: draftId, fields: emptyDefectFields(), record: null, asset: asset.assetContext?.pin ?? null, dirty: false });
                }
            }
            else {
                const page = await defectApi.list(route.vessel, route.offset, c.signal);
                await ensureOwner();
                if (active)
                    setSource({ scope, user, vessel, caps, page });
            }
            if (active)
                setFailure(previous => previous && [401, 403, 404].includes(previous.status) ? null : previous);
        }
        load().catch(() => { if (active)
            setSource({ scope, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [route, scope, route.vessel, historyOffset, online, visible, draftId]);
    const change = (fields: DefectFields) => { if (!editable || !editor)
        return; setEditor({ ...editor, fields, dirty: true }); onDirty(true); setFieldErrors({}); setFailure(null); setReceipt(null); };
    async function save() {
        if (!editor || !route.vessel || !owner || !sameOwner || !sameAttempt || !current?.caps?.canWrite || running.current || busy || denied || conflict || !frozen && (!editable || assetBlocked))
            return;
        let command: DefectAttempt;
        try {
            if (frozen)
                command = frozen;
            else {
                const errors = defectInputErrors(editor.fields);
                if (Object.keys(errors).length) {
                    setFieldErrors(errors);
                    setFailure(new ApiError(400, 'invalid_draft_input', errors));
                    return;
                }
                command = freezeDefectCommand(route.vessel, editor.id, owner, editor.fields, editor.record, editor.asset);
            }
        }
        catch (error) {
            setFailure(error instanceof ApiError ? error : new ApiError(400));
            return;
        }
        const wasUnknown = !!frozen, originalScope = scope;
        running.current = true;
        setBusy(true);
        onSaving(true);
        setFrozen(command);
        setFailure(null);
        setReceipt(null);
        const c = new AbortController();
        writer.current = c;
        const timer = setTimeout(() => c.abort(), 20000);
        let sent = false, receiptReceived = false, confirmed = false, uncertain = wasUnknown;
        try {
            const fresh = await session(c.signal);
            if (identity(fresh) !== command.owner)
                throw new ApiError(403, 'identity_changed');
            sent = true;
            const outcome = await defectApi.save(command, fresh.csrfToken, c.signal);
            receiptReceived = true;
            if (identity(await session(c.signal)) !== command.owner)
                throw new ApiError(403, 'identity_changed');
            if (!mounted.current || committed.current.scope !== originalScope || committed.current.owner !== command.owner) {
                uncertain = true;
                return;
            }
            confirmed = true;
            uncertain = false;
            setFrozen(null);
            onDirty(false);
            setReceipt({ owner: command.owner, value: outcome });
            setEditor({ ...editor, record: outcome, fields: mutable(outcome), asset: outcome.asset, dirty: false });
            // Persist the confirmed resource identity before optional readback. A
            // failed current GET must never reopen as a different new draft UUID.
            // The canonical source loader alone establishes fresh current/edit rights.
            onCanonical(outcome.id);
            retry(n => n + 1);
        }
        catch (error) {
            if (!mounted.current)
                return;
            const problem = error instanceof ApiError ? error : new ApiError(0);
            uncertain = !confirmed && (wasUnknown || receiptReceived || sent && (problem.status === 0 || problem.status >= 500));
            setFrozen(uncertain ? command : null);
            setFailure(problem);
            if ([401, 403, 404].includes(problem.status)) {
                setSource({ scope, failed: true });
                retry(n => n + 1);
            }
        }
        finally {
            clearTimeout(timer);
            writer.current = null;
            running.current = false;
            if (mounted.current) {
                setBusy(false);
                if (uncertain) {
                    setFrozen(command);
                    setFailure(new ApiError(0, 'outcome_unknown'));
                }
                onSaving(uncertain);
            }
        }
    }
    const readCurrent = () => onNavigate(defectLocationPath({ vessel: route.vessel, id: editor?.record?.id ?? route.id, offset: 0 }));
    const savedRecord = editor?.record;
    const snapshot = savedRecord && (current?.record?.version === savedRecord.version ? current.record : current?.history?.items.find(record => record.version === savedRecord.version) ?? savedRecord);
    const assetContext = current?.assetContext && (!editor?.asset || current.assetContext.pin.id === editor.asset.id && current.assetContext.pin.revision === editor.asset.revision && current.assetContext.pin.version === editor.asset.version && current.assetContext.pin.hash === editor.asset.hash) ? current.assetContext : undefined;
    return <div className="product-shell enterprise-shell defect-workspace">
    <EnterpriseChrome session={user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="defect-main"/>
    <main id="defect-main" tabIndex={-1}>
      <EnterprisePageHeader eyebrow={localizedText(en, "pairs.m_b8558d2f2b0907ac")} title={title} description={current?.vessel ? current.vessel.code + ' · ' + current.vessel.name : (localizedText(en, "pairs.m_84ca8f84148764ed"))} actionsLabel={localizedText(en, "pairs.m_18eedcbac7ec94d9")} actions={editor ? <Button variant="outline" disabled={busy || !!frozen} onClick={() => onNavigate(base)}>{localizedText(en, "pairs.m_5202a626fdced394")}</Button> : route.vessel && current?.caps?.canWrite && owner ? <Button onClick={() => setEditor({ owner, id: draftId, fields: emptyDefectFields(), record: null, asset: null, dirty: false })}>{localizedText(en, "pairs.m_b7f461224708c286")}</Button> : undefined}/>
      {route.invalid ? <p role="alert">{localizedText(en, "pairs.m_19a491edb1a111a0")}</p>
            : !online || !visible ? <p role="status">{localizedText(en, "pairs.m_6a398444bbca90b9")}</p>
                : !current ? <p role="status">{localizedText(en, "pairs.m_d55645954836eb66")}</p>
                    : current.failed ? <div role="alert"><p>{localizedText(en, "pairs.m_9cb56bde8a79b479")}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_542619365a11df9d")}</Button></div>
                        : !route.vessel ? <section aria-label={localizedText(en, "pairs.m_9bb356adf2be749c")}><h2>{localizedText(en, "pairs.m_7d083f965def1942")}</h2>{!current.vessels?.length && <p>{localizedText(en, "pairs.m_f892a57da8a85d13")}</p>}<ul className="defect-records">{current.vessels?.map(vessel => <li key={vessel.id}><Button variant="outline" onClick={() => onNavigate(defectLocationPath({ vessel: vessel.id, id: null, offset: 0 }))}>{vessel.code} · {vessel.name}</Button></li>)}</ul><EnterprisePagination label={localizedText(en, "pairs.m_7d0b34e1cd9deed1")} previous={localizedText(en, "pairs.m_3ebc6aea5e3f139e")} next={localizedText(en, "pairs.m_e86c29e387920d03")} previousDisabled={!route.offset} nextDisabled={!current.hasMore} onPrevious={() => onNavigate(defectLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(defectLocationPath({ ...route, offset: route.offset + 25 }))}/></section>
                            : !sameOwner || !sameAttempt ? <p role="alert">{localizedText(en, "pairs.m_1110a2b47b9eb7dd")}</p>
                                : editor && current.caps ? <section aria-label={localizedText(en, "pairs.m_3edd5aa85a8ec320")}>
          {receipt && receipt.owner === owner && <p role="status">{localizedText(en, "pairs.m_f363e66e4c4a45e2")}{receipt.value.revision}{localizedText(en, "pairs.m_01575d702e2b5209")}</p>}
          {snapshot && <DefectSourceSummary value={sourceChanged ? { ...snapshot, currentRevision: false, canEdit: false } : snapshot} language={language}/>}
          {user && editor.record?.currentRevision && !editor.dirty && !sourceChanged && <DowntimeSourceLaunch session={user} vessel={route.vessel!} kind="Defect" sourceId={editor.record.id} revision={editor.record.revision} language={language} disabled={busy || !!frozen || !sameOwner || !sameAttempt} onNavigate={href => onNavigate(href)}/>}
          {(editor.asset || route.asset) && <DefectAssetAside value={assetContext} retained={editor.asset} issue={current.assetIssue ?? (editor.asset && current.assetContext && !assetContext ? 'stale' : undefined)} language={language} onNavigate={onNavigate}/>}
          {assetBlocked && !frozen && <div role="alert"><p>{localizedText(en, "pairs.m_fa066fdde72f3a3a")}</p>{current.assetContext && <Button variant="outline" onClick={() => { if (editable && current.assetContext) {
                                    setEditor({ ...editor, asset: current.assetContext.pin, dirty: true });
                                    onDirty(true);
                                } }}>{localizedText(en, "pairs.m_594b0e2650ebdc6b")}</Button>}<Button variant="outline" onClick={() => onNavigate(base)}>{localizedText(en, "pairs.m_7c544ca3804a76d8")}</Button></div>}
          {(failure || conflict) && <p role="alert">{frozen ? (localizedText(en, "pairs.m_edf32af32bcb5995")) : conflict ? (localizedText(en, "pairs.m_142c8c9f3d69e323")) : (localizedText(en, "pairs.m_b7d957e01de56922"))}</p>}
          {!editable && !frozen && <p>{localizedText(en, "pairs.m_4fe3ab054e101b0d")}</p>}
          <form noValidate aria-label={localizedText(en, "pairs.m_f64db53251a3d18c")} aria-busy={busy} onSubmit={event => { event.preventDefault(); void save(); }}>
            <DefectFieldsForm fields={editor.fields} schema={current.caps.schema} language={language} disabled={!editable} errors={{ ...failure?.errors, ...fieldErrors }} onChange={change}/>
            <EnterpriseActions layout="form" aria-label={localizedText(en, "pairs.m_e8d34212e8c5cddc")}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{localizedText(en, "pairs.m_71be3ecd3d59de10")}</Button>{current.caps.canWrite && <Button type="submit" disabled={busy || denied || conflict || !frozen && (!editable || assetBlocked)}>{frozen ? (localizedText(en, "pairs.m_f52ba103834d2858")) : busy ? (localizedText(en, "pairs.m_5b598bccc6931d54")) : (localizedText(en, "pairs.m_5cc8d52e02322212"))}</Button>}</EnterpriseActions>
          </form>
          {editor.record && <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_bdd59ff85a0d5483")}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{localizedText(en, "pairs.m_1a67815de6333b5c")}</Button></EnterpriseActions>}
          {snapshot ? <DefectTemporalTimeline value={snapshot} language={language} dirty={editor.dirty}/> : <p className="defect-hint">{localizedText(en, "pairs.m_b962307455b94e1f")}</p>}
          {current.history && <DefectRecordHistory value={current.history} offset={historyOffset} disabled={busy || !!frozen || editor.dirty} language={language} onOffset={setHistoryOffset}/>}
        </section>
                                    : current.page ? <DefectRecordList page={current.page} offset={route.offset} language={language} onOffset={offset => onNavigate(defectLocationPath({ ...route, offset }))} onOpen={id => onNavigate(defectLocationPath({ vessel: route.vessel, id, offset: 0 }))}/>
                                        : <p role="alert">{localizedText(en, "pairs.m_0f163a15832b452a")}</p>}
    </main>
  </div>;
}
