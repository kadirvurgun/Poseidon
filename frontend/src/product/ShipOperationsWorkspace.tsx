import { localizedText } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination } from './EnterprisePatterns';
import { EnterpriseChrome } from './EnterpriseChrome';
import { DraftConfirmation } from './DraftConfirmation';
import { ApiError, session, vessel as readVessel, vessels, type Session, type Vessel } from './api';
import { isGuid } from './identifiers';
import { emptyShipOpsFields, mutableShipOpsFields, shipOpsTitle, type ShipOpsFields } from './ship-operation-fields';
import { freezeShipOpsCommand, shipOperationsApi, shipOpsFieldErrors, type ShipOpsAttempt, type ShipOpsCapabilities, type ShipOpsHistory, type ShipOpsPage, type ShipOpsRecord, type ShipOperationOriginPin } from './ship-operations-api';
import { readShipOpsOrigin, type ShipOpsOriginContext } from './ship-operation-origin';
import { readShipOpsLocation, shipOpsLocationPath, useShipOpsLocation, type ShipOpsLocation } from './ship-operation-location';
import { ShipOperationsFields } from './ShipOperationsFields';
import { ShipOperationOriginView } from './ShipOperationOriginView';
import { ShipOpsRecordHistory, ShipOpsRecordList, ShipOpsRecordSummary } from './ShipOperationsRecordView';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
type Editor = {
    owner: string;
    id: string;
    fields: ShipOpsFields;
    record: ShipOpsRecord | null;
    origin: ShipOperationOriginPin | null;
    dirty: boolean;
};
type Source = {
    scope: string;
    user?: Session;
    vessel?: Vessel;
    vessels?: Vessel[];
    hasMore?: boolean;
    caps?: ShipOpsCapabilities;
    page?: ShipOpsPage;
    record?: ShipOpsRecord;
    history?: ShipOpsHistory;
    origin?: ShipOpsOriginContext;
    failed?: boolean;
};
const identity = (user?: Session) => user?.authenticated && user.canRead && isGuid(user.actorId) && isGuid(user.tenantId) ? user.tenantId + '/' + user.actorId : null;
const mutable = (record: ShipOpsRecord): ShipOpsFields => Object.fromEntries(mutableShipOpsFields(record.kind).map(field => [field.key, structuredClone(record.fields[field.key])]));
export function ShipOperationsWorkspace() {
    const [language, setLanguage] = useAppLanguage(), [pending, setPending] = useState<(() => void) | null>(null), [instance, setInstance] = useState(0);
    const dirty = useRef(false), saving = useRef(false);
    const onDirty = useCallback((value: boolean) => { dirty.current = value; }, []), onSaving = useCallback((value: boolean) => { saving.current = value; }, []);
    const clear = () => { dirty.current = false; setInstance(n => n + 1); };
    const guard = (action: () => void) => { if (saving.current)
        return; if (dirty.current)
        setPending(() => action);
    else
        action(); };
    const { route, move } = useShipOpsLocation(guard, clear);
    const navigate = (href: string, committed?: () => void) => {
        if (href !== '/app' && !href.startsWith('/app/') || /[\\\p{Cc}]/u.test(href))
            return;
        const next = readShipOpsLocation(href);
        guard(() => { committed?.(); clear(); if (next.invalid)
            window.location.assign(href);
        else
            move(next); });
    };
    const canonical = (id: string) => { dirty.current = false; saving.current = false; move({ kind: route.kind, vessel: route.vessel, id, offset: 0 }, true); };
    useEffect(() => { const unload = (event: BeforeUnloadEvent) => { if (dirty.current || saving.current)
        event.preventDefault(); }; window.addEventListener('beforeunload', unload); return () => window.removeEventListener('beforeunload', unload); }, []);
    return <><ShipOpsSurface key={route.kind + '/' + route.vessel + '/' + instance} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate} onCanonical={canonical} onDirty={onDirty} onSaving={onSaving}/>{pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action(); }}/>}</>;
}
function ShipOpsSurface({ route, language, onLanguage, onNavigate, onCanonical, onDirty, onSaving }: {
    route: ShipOpsLocation;
    language: AppLocale;
    onLanguage: (value: AppLocale) => void;
    onNavigate: (href: string, committed?: () => void) => void;
    onCanonical: (id: string) => void;
    onDirty: (value: boolean) => void;
    onSaving: (value: boolean) => void;
}) {
    const en = language === 'en', epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [historyOffset, setHistoryOffset] = useState(0);
    const [draftId] = useState(() => crypto.randomUUID());
    const [source, setSource] = useState<Source | null>(null), [editor, setEditor] = useState<Editor | null>(null), [frozen, setFrozen] = useState<ShipOpsAttempt | null>(null);
    const [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({}), [receipt, setReceipt] = useState<{
        owner: string;
        value: ShipOpsRecord;
    } | null>(null);
    const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef({ owner: null as string | null, scope: '' });
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = shipOpsLocationPath(route) + '/' + epoch + '/' + attempt + '/' + historyOffset;
    const current = source?.scope === scope && online && visible ? source : null, user = current?.user, owner = identity(user);
    const title = shipOpsTitle(route.kind, language), sameOwner = !editor || editor.owner === owner, sameAttempt = !frozen || frozen.owner === owner;
    const sourceChanged = !!editor?.record && !!current?.record && (editor.record.version !== current.record.version || editor.record.revision !== current.record.revision);
    const conflict = !frozen && (failure?.status === 409 || sourceChanged), denied = !!failure && [401, 403, 404].includes(failure.status);
    const originBlocked = !!editor && !editor.record && !!route.origin && (!current?.origin || !current.origin.current || current.origin.pin.version !== editor.origin?.version || current.origin.pin.hash !== editor.origin?.hash);
    const headEditable = !editor?.record || editor.record.currentRevision && editor.record.canEdit;
    const editable = !!owner && sameOwner && sameAttempt && !!current?.caps?.canWrite && headEditable && !busy && !frozen && !conflict && !denied;
    const base = shipOpsLocationPath({ kind: route.kind, vessel: route.vessel, id: null, offset: 0 });
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
                    setSource({ scope, user, vessels: page.items, hasMore: page.hasMore });
                return;
            }
            const [caps, vessel] = await Promise.all([shipOperationsApi.capabilities(route.kind, route.vessel, c.signal), readVessel(route.vessel, c.signal)]);
            if (!caps.canRead || vessel.id !== route.vessel || vessel.kind !== 'Ship')
                throw new ApiError(403);
            if (route.id) {
                const [record, history] = await Promise.all([shipOperationsApi.read(route.kind, route.vessel, route.id, c.signal), shipOperationsApi.history(route.kind, route.vessel, route.id, historyOffset, c.signal)]);
                const origin = record.origin ? await readShipOpsOrigin(route.vessel, route.kind, { kind: record.origin.kind, id: record.origin.id, revision: record.origin.revision }, c.signal, record.origin) : undefined;
                await ensureOwner();
                if (active) {
                    setSource({ scope, user, vessel, caps, record, history, origin });
                    setEditor(previous => !previous ? { owner: original, id: record.id, fields: mutable(record), record, origin: record.origin, dirty: false } : previous.owner === original && previous.id === record.id && !previous.dirty && previous.record && !previous.record.currentRevision ? { ...previous, record, fields: mutable(record), origin: record.origin } : previous);
                }
            }
            else if (route.origin) {
                const origin = await readShipOpsOrigin(route.vessel, route.kind, route.origin, c.signal);
                await ensureOwner();
                if (active) {
                    setSource({ scope, user, vessel, caps, origin });
                    if (caps.canWrite)
                        setEditor(previous => previous ?? { owner: original, id: draftId, fields: emptyShipOpsFields(route.kind), record: null, origin: origin.pin, dirty: false });
                }
            }
            else {
                const page = await shipOperationsApi.list(route.kind, route.vessel, route.offset, c.signal);
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
    const change = (fields: ShipOpsFields) => { if (!editable || !editor)
        return; setEditor({ ...editor, fields, dirty: true }); onDirty(true); setFieldErrors({}); setFailure(null); setReceipt(null); };
    async function save() {
        if (!editor || !route.vessel || !owner || !sameOwner || !sameAttempt || !current?.caps?.canWrite || running.current || busy || denied || conflict || !frozen && (!editable || originBlocked))
            return;
        let command: ShipOpsAttempt;
        try {
            if (frozen)
                command = frozen;
            else {
                const errors = shipOpsFieldErrors(editor.fields, route.kind);
                if (Object.keys(errors).length) {
                    setFieldErrors(errors);
                    setFailure(new ApiError(400, 'invalid_draft_input', errors));
                    return;
                }
                command = freezeShipOpsCommand(route.kind, route.vessel, editor.id, owner, editor.fields, editor.record, editor.origin);
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
            const outcome = await shipOperationsApi.save(command, fresh.csrfToken, c.signal);
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
    const readCurrent = () => onNavigate(shipOpsLocationPath({ kind: route.kind, vessel: route.vessel, id: editor?.record?.id ?? route.id, offset: 0 }));
    return <div className="product-shell enterprise-shell ship-operations-workspace">
    <EnterpriseChrome session={user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="ship-operations-main"/>
    <main id="ship-operations-main" tabIndex={-1}>
      <EnterprisePageHeader eyebrow={localizedText(en, "pairs.m_42b4db07862c5e4f")} title={title} description={current?.vessel ? current.vessel.code + ' · ' + current.vessel.name : (localizedText(en, "pairs.m_7d504248eecee6fd"))} actionsLabel={localizedText(en, "pairs.m_18eedcbac7ec94d9")} actions={editor ? <Button variant="outline" disabled={busy || !!frozen} onClick={() => onNavigate(base)}>{localizedText(en, "pairs.m_acb48ca5df9828f0")}</Button> : route.vessel && current?.caps?.canWrite && owner ? <Button onClick={() => { setEditor({ owner, id: draftId, fields: emptyShipOpsFields(route.kind), record: null, origin: null, dirty: false }); }}>{localizedText(en, "pairs.m_c269b2ae53e08382")}</Button> : undefined}/>
      {route.invalid ? <p role="alert">{localizedText(en, "pairs.m_04b499ffcaabe049")}</p>
            : !online || !visible ? <p role="status">{localizedText(en, "pairs.m_a59fbca1d04d1951")}</p>
                : !current ? <p role="status">{localizedText(en, "pairs.m_ea18acc2e8bd7625")}</p>
                    : current.failed ? <div role="alert"><p>{localizedText(en, "pairs.m_44309d40a7300d33")}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_542619365a11df9d")}</Button></div>
                        : !route.vessel ? <section aria-label={localizedText(en, "pairs.m_ae50efbad460b933")}><h2>{localizedText(en, "pairs.m_118d4b4dcafadb1e")}</h2><ul className="ship-operations-vessels">{current.vessels?.map(vessel => <li key={vessel.id}><Button variant="outline" onClick={() => onNavigate(shipOpsLocationPath({ ...route, vessel: vessel.id, offset: 0 }))}>{vessel.code} · {vessel.name}</Button></li>)}</ul>{!current.vessels?.length && <p>{localizedText(en, "pairs.m_4696394d8c6c1ea1")}</p>}<EnterprisePagination label={localizedText(en, "pairs.m_fb707f05567c4e90")} previous={localizedText(en, "pairs.m_7d3bb6f0b9a6734d")} next={localizedText(en, "pairs.m_a99bb90292c2965e")} previousDisabled={!route.offset} nextDisabled={!current.hasMore} onPrevious={() => onNavigate(shipOpsLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(shipOpsLocationPath({ ...route, offset: route.offset + 25 }))}/></section>
                            : !sameOwner || !sameAttempt ? <p role="alert">{localizedText(en, "pairs.m_6de5fe11e964f00e")}</p>
                                : editor && current.caps ? <section aria-label={localizedText(en, "pairs.m_6a1cebef9586a70a")}>
          {receipt && receipt.owner === owner && <p role="status">{localizedText(en, "pairs.m_9d807cf15c56824d")}{receipt.value.revision}{localizedText(en, "pairs.m_2091bbb2817a96b9")}</p>}
          {editor.record && <ShipOpsRecordSummary value={sourceChanged ? { ...editor.record, currentRevision: false, canEdit: false } : editor.record} language={language} contextOnly/>}
          {(editor.origin || route.origin) && <ShipOperationOriginView value={current.origin} unavailable={!current.origin} language={language} onNavigate={onNavigate}/>}
          {originBlocked && !frozen && <p role="alert">{localizedText(en, "pairs.m_4a421d82377c8fa8")}</p>}
          {originBlocked && !frozen && <Button variant="outline" onClick={() => onNavigate(base)}>{localizedText(en, "pairs.m_7b4bced7fecd20ad")}</Button>}
          {(failure || conflict) && <p role="alert">{frozen ? (localizedText(en, "pairs.m_f331d9a7efce29ac")) : conflict ? (localizedText(en, "pairs.m_673804b47f3c71bb")) : (localizedText(en, "pairs.m_95c2226079e983bd"))}</p>}
          {!editable && !frozen && <p>{localizedText(en, "pairs.m_4fe3ab054e101b0d")}</p>}
          <form noValidate aria-label={route.kind === 'Fuel' ? (localizedText(en, "pairs.m_630e765614476f41")) : (localizedText(en, "pairs.m_405093a8a4f26293"))} aria-busy={busy} onSubmit={event => { event.preventDefault(); void save(); }}>
            <ShipOperationsFields kind={route.kind} fields={editor.fields} schema={current.caps.schema} language={language} disabled={!editable} errors={{ ...failure?.errors, ...fieldErrors }} onChange={change}/>
            <EnterpriseActions layout="form" aria-label={localizedText(en, "pairs.m_ffd4d19cd64aed61")}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{localizedText(en, "pairs.m_71be3ecd3d59de10")}</Button>{current.caps.canWrite && <Button type="submit" disabled={busy || denied || conflict || !frozen && (!editable || originBlocked)}>{frozen ? (localizedText(en, "pairs.m_42c1d428a22ca926")) : busy ? (localizedText(en, "pairs.m_5b598bccc6931d54")) : (localizedText(en, "pairs.m_1057f80f39b6b640"))}</Button>}</EnterpriseActions>
          </form>
          {editor.record && <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_6abad8c133e1acac")}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{localizedText(en, "pairs.m_391249d8f314090a")}</Button></EnterpriseActions>}
          {current.history && <ShipOpsRecordHistory value={current.history} language={language} offset={historyOffset} disabled={busy || !!frozen || editor.dirty} onOffset={setHistoryOffset}/>}
        </section>
                                    : current.page ? <ShipOpsRecordList kind={route.kind} page={current.page} language={language} offset={route.offset} onOffset={offset => onNavigate(shipOpsLocationPath({ ...route, offset }))} onOpen={id => onNavigate(shipOpsLocationPath({ kind: route.kind, vessel: route.vessel, id, offset: 0 }))}/>
                                        : <p role="alert">{localizedText(en, "pairs.m_900492ac3734bc78")}</p>}
      <p className="ship-operations-hint">{localizedText(en, "pairs.m_c3cb745115573974")}</p>
    </main>
  </div>;
}
