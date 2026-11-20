import { localizedText } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination } from './EnterprisePatterns';
import { EnterpriseChrome } from './EnterpriseChrome';
import { DraftConfirmation } from './DraftConfirmation';
import { ApiError, session, vessel as readVessel, vessels, type Session, type Vessel } from './api';
import { isGuid } from './identifiers';
import { emptyDowntimeFields, downtimeMutableSchema, type DowntimeFields } from './downtime-fields';
import { downtimeApi, downtimeInputErrors, freezeDowntimeCommand, type DowntimeAttempt, type DowntimeOriginContext, type DowntimeOriginPin, type DowntimeCapabilities, type DowntimeHistory, type DowntimePage, type DowntimeRecord } from './downtime-api';
import { readDowntimeLocation, downtimeLocationPath, useDowntimeLocation, type DowntimeLocation, type DowntimeOriginSelector } from './downtime-location';
import { DowntimeFieldsForm } from './DowntimeFieldsForm';
import { DowntimeLifecyclePanel } from './ShipOpsLifecyclePanel';
import { DowntimeOriginAside, DowntimeRecordHistory, DowntimeRecordList, DowntimeSourceSummary } from './DowntimeRecordView';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import { unsavedState, useUnsavedChangesSource } from './unsaved-changes';
type Editor = {
    owner: string;
    id: string;
    fields: DowntimeFields;
    record: DowntimeRecord | null;
    origin: DowntimeOriginPin | null;
    dirty: boolean;
};
type Source = {
    scope: string;
    user?: Session;
    vessel?: Vessel;
    vessels?: Vessel[];
    hasMore?: boolean;
    caps?: DowntimeCapabilities;
    page?: DowntimePage;
    record?: DowntimeRecord;
    history?: DowntimeHistory;
    originContext?: DowntimeOriginContext;
    originIssue?: 'stale' | 'unavailable';
    failed?: boolean;
};
const identity = (user?: Session) => user?.authenticated && user.canRead && isGuid(user.actorId) && isGuid(user.tenantId) ? user.tenantId + '/' + user.actorId : null;
const mutable = (record: DowntimeRecord): DowntimeFields => Object.fromEntries(downtimeMutableSchema().map(field => [field.key, structuredClone(record.fields[field.key])])) as DowntimeFields;
export function DowntimeWorkspace() {
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
    const { route, move } = useDowntimeLocation(guard, clear);
    const navigate = (href: string, committed?: () => void) => {
        if (href !== '/app' && !href.startsWith('/app/') || /[\\\p{Cc}]/u.test(href))
            return;
        const next = readDowntimeLocation(href);
        guard(() => { committed?.(); clear(); if (next.invalid)
            window.location.assign(href);
        else
            move(next); });
    };
    const canonical = (id: string) => { dirty.current = false; saving.current = false; move({ vessel: route.vessel, id, offset: 0 }, true); };
    useEffect(() => { const unload = (event: BeforeUnloadEvent) => { if (dirty.current || saving.current)
        event.preventDefault(); }; window.addEventListener('beforeunload', unload); return () => window.removeEventListener('beforeunload', unload); }, []);
    return <><DowntimeSurface key={route.vessel + '/' + instance} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate} onCanonical={canonical} onDirty={onDirty} onSaving={onSaving}/>{pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action(); }}/>}</>;
}
function DowntimeSurface({ route, language, onLanguage, onNavigate, onCanonical, onDirty, onSaving }: {
    route: DowntimeLocation;
    language: AppLocale;
    onLanguage: (value: AppLocale) => void;
    onNavigate: (href: string, committed?: () => void) => void;
    onCanonical: (id: string) => void;
    onDirty: (value: boolean) => void;
    onSaving: (value: boolean) => void;
}) {
    const en = language === 'en', epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [historyOffset, setHistoryOffset] = useState(0);
    const [draftId] = useState(() => crypto.randomUUID());
    const [source, setSource] = useState<Source | null>(null), [editor, setEditor] = useState<Editor | null>(null), [frozen, setFrozen] = useState<DowntimeAttempt | null>(null);
    const [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({}), [receipt, setReceipt] = useState<{
        owner: string;
        value: DowntimeRecord;
    } | null>(null);
    const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef({ owner: null as string | null, scope: '' });
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = downtimeLocationPath(route) + '/' + epoch + '/' + attempt + '/' + historyOffset;
    const current = source?.scope === scope && online && visible ? source : null, user = current?.user, owner = identity(user);
    const title = (localizedText(en, "pairs.m_ceee93655e45d98d")), sameOwner = !editor || editor.owner === owner, sameAttempt = !frozen || frozen.owner === owner;
    const sourceChanged = !!editor?.record && !!current?.record && (editor.record.version !== current.record.version || editor.record.revision !== current.record.revision);
    const conflict = !frozen && (failure?.status === 409 || sourceChanged), denied = !!failure && [401, 403, 404].includes(failure.status);
    const originBlocked = !!editor && !editor.record && !!route.origin && (!editor.origin || !current?.originContext || !current.originContext.current || !current.caps?.originKinds.includes(current.originContext.pin.kind) || current.originContext.pin.version !== editor.origin.version || current.originContext.pin.hash !== editor.origin.hash);
    const headEditable = !editor?.record || editor.record.currentRevision && editor.record.canEdit;
    const editable = !!owner && sameOwner && sameAttempt && !!current?.caps?.canWrite && headEditable && !busy && !frozen && !conflict && !denied;
    const base = downtimeLocationPath({ vessel: route.vessel, id: null, offset: 0 });
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
                const page = await vessels(route.offset, 'Ship', c.signal);
                await ensureOwner();
                if (active)
                    setSource({ scope, user, vessels: page.items.filter(item => item.kind === 'Ship'), hasMore: page.hasMore });
                return;
            }
            const [caps, vessel] = await Promise.all([downtimeApi.capabilities(route.vessel, c.signal), readVessel(route.vessel, c.signal)]);
            if (!caps.canRead || vessel.id !== route.vessel || vessel.kind !== 'Ship')
                throw new ApiError(403);
            async function context(pin: DowntimeOriginSelector, expected?: DowntimeOriginPin): Promise<{
                originContext?: DowntimeOriginContext;
                originIssue?: 'stale' | 'unavailable';
            }> {
                try {
                    return { originContext: await downtimeApi.originContext(vessel.id, pin, c.signal, expected) };
                }
                catch (error) {
                    if (error instanceof ApiError && [401, 403].includes(error.status))
                        throw error;
                    return { originIssue: error instanceof ApiError && error.status === 409 ? 'stale' : 'unavailable' };
                }
            }
            if (route.id) {
                const [record, history] = await Promise.all([downtimeApi.read(route.vessel, route.id, c.signal), downtimeApi.history(route.vessel, route.id, historyOffset, c.signal)]);
                const origin = record.origin ? await context(record.origin, record.origin) : {};
                await ensureOwner();
                if (active) {
                    setSource({ scope, user, vessel, caps, record, history, ...origin });
                    setEditor(previous => !previous ? { owner: original, id: record.id, fields: mutable(record), record, origin: record.origin, dirty: false } : previous.owner === original && previous.id === record.id && !previous.dirty && previous.record && !previous.record.currentRevision ? { ...previous, record, fields: mutable(record), origin: record.origin } : previous);
                }
            }
            else if (route.origin) {
                const origin = await context(route.origin);
                await ensureOwner();
                if (active) {
                    setSource({ scope, user, vessel, caps, ...origin });
                    if (caps.canWrite)
                        setEditor(previous => previous ?? { owner: original, id: draftId, fields: emptyDowntimeFields(), record: null, origin: origin.originContext?.pin ?? null, dirty: false });
                }
            }
            else {
                const page = await downtimeApi.list(route.vessel, route.offset, c.signal);
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
    const change = (fields: DowntimeFields) => { if (!editable || !editor)
        return; setEditor({ ...editor, fields, dirty: true }); onDirty(true); setFieldErrors({}); setFailure(null); setReceipt(null); };
    async function save() {
        if (!editor || !route.vessel || !owner || !sameOwner || !sameAttempt || !current?.caps?.canWrite || running.current || busy || denied || conflict || !frozen && (!editable || originBlocked))
            return;
        let command: DowntimeAttempt;
        try {
            if (frozen)
                command = frozen;
            else {
                const errors = downtimeInputErrors(editor.fields);
                if (Object.keys(errors).length) {
                    setFieldErrors(errors);
                    setFailure(new ApiError(400, 'invalid_draft_input', errors));
                    return;
                }
                command = freezeDowntimeCommand(route.vessel, editor.id, owner, editor.fields, editor.record, editor.origin);
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
            const outcome = await downtimeApi.save(command, fresh.csrfToken, c.signal);
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
            setEditor({ ...editor, record: outcome, fields: mutable(outcome), origin: outcome.origin, dirty: false });
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
    const readCurrent = () => onNavigate(downtimeLocationPath({ vessel: route.vessel, id: editor?.record?.id ?? route.id, offset: 0 }));
    const savedRecord = editor?.record;
    const snapshot = savedRecord && (current?.record?.version === savedRecord.version ? current.record : current?.history?.items.find(record => record.version === savedRecord.version) ?? savedRecord);
    const originContext = current?.originContext && (!editor?.origin || current.originContext.pin.kind === editor.origin.kind && current.originContext.pin.id === editor.origin.id && current.originContext.pin.revision === editor.origin.revision && current.originContext.pin.version === editor.origin.version && current.originContext.pin.hash === editor.origin.hash) ? current.originContext : undefined;
    return <div className="product-shell enterprise-shell downtime-workspace">
    <EnterpriseChrome session={user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="downtime-main"/>
    <main id="downtime-main" tabIndex={-1}>
      <EnterprisePageHeader eyebrow={localizedText(en, "pairs.m_42b4db07862c5e4f")} title={title} description={current?.vessel ? current.vessel.code + ' · ' + current.vessel.name : (localizedText(en, "pairs.m_7148f6e4efeaf12d"))} actionsLabel={localizedText(en, "pairs.m_18eedcbac7ec94d9")} actions={editor ? <Button variant="outline" disabled={busy || !!frozen} onClick={() => onNavigate(base)}>{localizedText(en, "pairs.m_3cf58b3de5b11563")}</Button> : route.vessel && current?.caps?.canWrite && owner ? <Button onClick={() => setEditor({ owner, id: draftId, fields: emptyDowntimeFields(), record: null, origin: null, dirty: false })}>{localizedText(en, "pairs.m_2ee95a0caeb7dfe0")}</Button> : undefined}/>
      {route.invalid ? <p role="alert">{localizedText(en, "pairs.m_8310a26c8eb7e342")}</p>
            : !online || !visible ? <p role="status">{localizedText(en, "pairs.m_c52a568c4fb9aae8")}</p>
                : !current ? <p role="status">{localizedText(en, "pairs.m_7cee4f04e4e3c965")}</p>
                    : current.failed ? <div role="alert"><p>{localizedText(en, "pairs.m_afde407f2e81b8a8")}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_542619365a11df9d")}</Button></div>
                        : !route.vessel ? <section aria-label={localizedText(en, "pairs.m_c7e4afd9396ad19b")}><h2>{localizedText(en, "pairs.m_118d4b4dcafadb1e")}</h2>{!current.vessels?.length && <p>{localizedText(en, "pairs.m_f892a57da8a85d13")}</p>}<ul className="downtime-records">{current.vessels?.map(vessel => <li key={vessel.id}><Button variant="outline" onClick={() => onNavigate(downtimeLocationPath({ vessel: vessel.id, id: null, offset: 0 }))}>{vessel.code} · {vessel.name}</Button></li>)}</ul><EnterprisePagination label={localizedText(en, "pairs.m_4827ea3ea54e5612")} previous={localizedText(en, "pairs.m_3ebc6aea5e3f139e")} next={localizedText(en, "pairs.m_e86c29e387920d03")} previousDisabled={!route.offset} nextDisabled={!current.hasMore} onPrevious={() => onNavigate(downtimeLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(downtimeLocationPath({ ...route, offset: route.offset + 25 }))}/></section>
                            : !sameOwner || !sameAttempt ? <p role="alert">{localizedText(en, "pairs.m_1110a2b47b9eb7dd")}</p>
                                : editor && current.caps ? <section aria-label={localizedText(en, "pairs.m_4411bff2af5d82f2")}>
          {receipt && receipt.owner === owner && <p role="status">{localizedText(en, "pairs.m_5870ae9570c04068")}{receipt.value.revision}{localizedText(en, "pairs.m_01575d702e2b5209")}</p>}
          {snapshot && <DowntimeSourceSummary value={sourceChanged ? { ...snapshot, currentRevision: false, canEdit: false } : snapshot} language={language}/>}
          {(editor.origin || route.origin) && <DowntimeOriginAside value={originContext} retained={editor.origin} issue={current.originIssue ?? (editor.origin && current.originContext && !originContext ? 'stale' : undefined)} language={language} onNavigate={onNavigate}/>}
          {originBlocked && !frozen && <div role="alert"><p>{localizedText(en, "pairs.m_3f78f5f6e419ada2")}</p>{current.originContext?.current && <Button variant="outline" onClick={() => { if (editable && current.originContext?.current && current.caps?.originKinds.includes(current.originContext.pin.kind)) {
                                    setEditor({ ...editor, origin: current.originContext.pin, dirty: true });
                                    onDirty(true);
                                } }}>{localizedText(en, "pairs.m_1124c9a86841b6d4")}</Button>}<Button variant="outline" onClick={() => onNavigate(base)}>{localizedText(en, "pairs.m_67c172e532286ab0")}</Button></div>}
          {(failure || conflict) && <p role="alert">{frozen ? (localizedText(en, "pairs.m_edf32af32bcb5995")) : conflict ? (localizedText(en, "pairs.m_92325c4c9196412d")) : (localizedText(en, "pairs.m_62db2e30d654f3a5"))}</p>}
          {!editable && !frozen && <p>{localizedText(en, "pairs.m_4fe3ab054e101b0d")}</p>}
          <form noValidate aria-label={localizedText(en, "pairs.m_d820e3ef4ed015be")} aria-busy={busy} onSubmit={event => { event.preventDefault(); void save(); }}>
            <EnterpriseActions layout="form" aria-label={localizedText(en, "pairs.m_0df778d42a2dbb08")}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{localizedText(en, "pairs.m_71be3ecd3d59de10")}</Button>{current.caps.canWrite && <Button type="submit" disabled={busy || denied || conflict || !frozen && (!editable || originBlocked)}>{frozen ? (localizedText(en, "pairs.m_f2a18780a5713d10")) : busy ? (localizedText(en, "pairs.m_5b598bccc6931d54")) : (localizedText(en, "pairs.m_8ca7b135ec822147"))}</Button>}</EnterpriseActions>
            <DowntimeFieldsForm fields={editor.fields} schema={current.caps.schema} language={language} disabled={!editable} errors={{ ...failure?.errors, ...fieldErrors }} onChange={change}/>
          </form>
          {editor.record && route.vessel && <DowntimeLifecyclePanel vessel={route.vessel} id={editor.record.id} revision={editor.record.revision} language={language} disabled={busy || !!frozen || editor.dirty} onCommitted={readCurrent} onSaving={onSaving}/>}
          {editor.record && <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_f7ac2028e377b6fd")}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{localizedText(en, "pairs.m_83229ffef92bbe4e")}</Button></EnterpriseActions>}
          {current.history && <DowntimeRecordHistory value={current.history} offset={historyOffset} disabled={busy || !!frozen || editor.dirty} language={language} onOffset={setHistoryOffset}/>}
        </section>
                                    : current.page ? <DowntimeRecordList page={current.page} offset={route.offset} language={language} onOffset={offset => onNavigate(downtimeLocationPath({ ...route, offset }))} onOpen={id => onNavigate(downtimeLocationPath({ vessel: route.vessel, id, offset: 0 }))}/>
                                        : <p role="alert">{localizedText(en, "pairs.m_feaefbb628471660")}</p>}
    </main>
  </div>;
}
