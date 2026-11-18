import { localizedLabel } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { EnterpriseChrome } from './EnterpriseChrome';
import { DailyOperationLaunch } from './DailyOperationLaunch';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination } from './EnterprisePatterns';
import { DraftConfirmation } from './DraftConfirmation';
import { reviewBlocker } from './daily-review-copy';
import { DailyOfflinePanel } from './daily-offline/DailyOfflinePanel';
import { DailyReviewPanel } from './DailyReviewPanel';
import { DailyReportFields } from './DailyReportFields';
import { isGuid } from './identifiers';
import { ApiError, session, vessels, type Session, type Vessel } from './api';
import { dailyApi, dailyAttempt, type DailyAttempt, type DailyCapabilities, type DailyCollection, type DailyPage, type DailyRecord } from './daily-operations-api';
import { dailyFields, dailyTitle, emptyDailyFields, type DailyFields } from './daily-report-fields';
import { dailyLocationPath, readDailyLocation, useDailyLocation, type DailyLocation } from './daily-location';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
import './daily-operations.css';
import { unsavedState, useUnsavedChangesSource } from './unsaved-changes';
type GuardChannel = 'ordinary' | 'review' | 'offline';
type View = {
    scope: string;
    user?: Session;
    caps?: DailyCapabilities;
    row?: DailyRecord;
    page?: DailyCollection;
    history?: DailyPage;
    vessels?: Vessel[];
    moreVessels?: boolean;
    failed?: number;
};
export function DailyReportWorkspace() {
    const [language, setLanguage] = useAppLanguage(), [pending, setPending] = useState<(() => void) | null>(null), [instance, setInstance] = useState(0), [savedNotice, setSavedNotice] = useState(false);
    const dirtyRef = useRef(false), savingRef = useRef(false);
    const setDirty = useCallback((value: boolean) => { dirtyRef.current = value; }, []);
    useUnsavedChangesSource(() => unsavedState(dirtyRef.current, savingRef.current));
    const setSaving = useCallback((value: boolean) => { savingRef.current = value; }, []);
    const guard = (action: () => void) => { if (savingRef.current)
        return; if (dirtyRef.current)
        setPending(() => action);
    else
        action(); };
    const clear = () => { setSavedNotice(false); setDirty(false); setInstance(n => n + 1); };
    const { route, move } = useDailyLocation(guard, clear);
    const navigate = (href: string, committed?: () => void) => {
        if (href !== '/app' && !href.startsWith('/app/') || href.startsWith('//') || /[\\\p{Cc}]/u.test(href))
            return;
        const next = readDailyLocation(href);
        guard(() => { committed?.(); clear(); if (!next.invalid)
            move(next);
        else
            window.location.assign(href); });
    };
    const startDraft = () => guard(() => { clear(); move({ ...route, id: null, draft: crypto.randomUUID(), offset: 0 }, true); });
    const recordSaved = (id: string) => { clear(); setSavedNotice(true); move({ ...route, id, draft: undefined, offset: 0 }, true); };
    useEffect(() => { document.title = `POSEIDON · ${dailyTitle(route.kind, language)}`; }, [language, route.kind]);
    useEffect(() => { const unload = (event: BeforeUnloadEvent) => { if (dirtyRef.current || savingRef.current)
        event.preventDefault(); }; window.addEventListener('beforeunload', unload); return () => window.removeEventListener('beforeunload', unload); }, []);
    return <><DailySurface key={`${route.kind}/${route.vessel}/${route.id}/${route.draft ?? ''}/${route.offset}/${instance}`} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate} onDirty={setDirty} onSaving={setSaving} onStartDraft={startDraft} onRecordSaved={recordSaved} savedNotice={savedNotice}/>{pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action(); }}/>}</>;
}
function DailySurface({ route, language, onLanguage, onNavigate, onDirty, onSaving, onStartDraft, onRecordSaved, savedNotice }: {
    route: DailyLocation;
    language: AppLocale;
    onLanguage: (language: AppLocale) => void;
    onNavigate: (href: string, committed?: () => void) => void;
    onDirty: (dirty: boolean) => void;
    onSaving: (saving: boolean) => void;
    onStartDraft: () => void;
    onRecordSaved: (id: string) => void;
    savedNotice: boolean;
}) {
    const en = language === 'en', epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [historyOffset, setHistoryOffset] = useState(0), [view, setView] = useState<View | null>(null);
    const [editor, setEditor] = useState<{
        owner: string;
        recordId: string;
        fields: DailyFields;
        record?: DailyRecord;
        dirty: boolean;
    } | null>(null), [frozen, setFrozen] = useState<{
        owner: string;
        command: DailyAttempt;
        uncertain: boolean;
    } | null>(null), [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [notice, setNotice] = useState(false);
    const dirtyChannels = useRef({ ordinary: false, review: false, offline: false }), savingChannels = useRef({ ordinary: false, review: false, offline: false });
    const [offlineCommitted, setOfflineCommitted] = useState(false);
    const [panelState, setPanelState] = useState({ reviewDirty: false, reviewSaving: false, offlineDirty: false, offlineSaving: false });
    const reportDirty = useCallback((channel: GuardChannel, value: boolean) => {
        dirtyChannels.current[channel] = value;
        if (channel !== 'ordinary')
            setPanelState(previous => previous[`${channel}Dirty`] === value ? previous : { ...previous, [`${channel}Dirty`]: value });
        onDirty(Object.values(dirtyChannels.current).some(Boolean));
    }, [onDirty]);
    const reportSaving = useCallback((channel: GuardChannel, value: boolean) => {
        savingChannels.current[channel] = value;
        if (channel !== 'ordinary')
            setPanelState(previous => previous[`${channel}Saving`] === value ? previous : { ...previous, [`${channel}Saving`]: value });
        onSaving(Object.values(savingChannels.current).some(Boolean));
    }, [onSaving]);
    const reviewDirty = useCallback((value: boolean) => reportDirty('review', value), [reportDirty]);
    const reviewSaving = useCallback((value: boolean) => reportSaving('review', value), [reportSaving]);
    const offlineDirty = useCallback((value: boolean) => reportDirty('offline', value), [reportDirty]);
    const offlineSaving = useCallback((value: boolean) => reportSaving('offline', value), [reportSaving]);
    const offlineCommit = useCallback(() => { setOfflineCommitted(true); retry(n => n + 1); }, []);
    const refreshCommitted = useCallback(() => retry(n => n + 1), []);
    const active = useRef(false), locked = useRef(false), writer = useRef<AbortController | null>(null);
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = `${epoch}/${attempt}/${historyOffset}`;
    useEffect(() => { active.current = true; return () => { active.current = false; writer.current?.abort(); }; }, []);
    useEffect(() => {
        if (!online || !visible || route.invalid)
            return;
        const c = new AbortController();
        let current = true;
        const timer = setTimeout(() => c.abort(), 10000);
        async function load() {
            const user = await session(c.signal);
            if (!user.authenticated || !user.canRead || !isGuid(user.actorId) || !isGuid(user.tenantId))
                throw new ApiError(403);
            if (!route.vessel) {
                const page = await vessels(route.offset, 'Ship', c.signal);
                if (current)
                    setView({ scope, user, vessels: page.items, moreVessels: page.hasMore });
                return;
            }
            const caps = await dailyApi.capabilities(route.kind, route.vessel, c.signal);
            if (!caps.canRead)
                throw new ApiError(403);
            if (current && caps.canWrite)
                setFailure(previous => previous && [401, 403, 404].includes(previous.status) ? null : previous);
            const draftId = route.draft;
            if (draftId) {
                if (current) {
                    setView({ scope, user, caps });
                    if (caps.canWrite)
                        setEditor(previous => previous ?? { owner: `${user.tenantId}/${user.actorId}`, recordId: draftId, fields: emptyDailyFields(route.kind), dirty: false });
                }
                return;
            }
            if (route.id) {
                const [row, history] = await Promise.all([dailyApi.read(route.kind, route.vessel, route.id, c.signal), dailyApi.history(route.kind, route.vessel, route.id, historyOffset, c.signal)]);
                if (current) {
                    setView({ scope, user, caps, row, history });
                    setEditor(previous => previous && !previous.dirty && previous.owner === `${user.tenantId}/${user.actorId}` && previous.record && previous.record.version === row.version && previous.record.revision === row.revision ? { ...previous, record: row } : previous ?? { owner: `${user.tenantId}/${user.actorId}`, recordId: row.id, fields: row.fields, record: row, dirty: false });
                }
            }
            else {
                const page = await dailyApi.list(route.kind, route.vessel, route.offset, c.signal);
                if (current)
                    setView({ scope, user, caps: { ...caps, canWrite: caps.canWrite && page.canWrite, canReview: caps.canReview && page.canReview }, page });
            }
        }
        load().catch(error => { if (current)
            setView({ scope, failed: error instanceof ApiError ? error.status : 0 }); }).finally(() => clearTimeout(timer));
        return () => { current = false; c.abort(); clearTimeout(timer); };
    }, [route, scope, online, visible, historyOffset]);
    const current = view?.scope === scope && online && visible ? view : null, caps = current?.caps, user = current?.user;
    const owner = user?.tenantId && user?.actorId ? `${user.tenantId}/${user.actorId}` : null;
    const identityBoundary = useRef<string | null>(null);
    useLayoutEffect(() => { identityBoundary.current = owner; }, [owner]);
    const ownerChanged = !!editor && editor.owner !== owner || !!frozen && frozen.owner !== owner;
    const denied = failure && [401, 403, 404].includes(failure.status), versionConflict = failure?.status === 409 && !frozen?.uncertain;
    const editable = !panelState.reviewDirty && !panelState.reviewSaving && !panelState.offlineDirty && !panelState.offlineSaving && !!caps?.canWrite && !ownerChanged && !busy && !frozen && !denied && !versionConflict;
    const title = dailyTitle(route.kind, language);
    const rootHref = dailyLocationPath({ ...route, id: null, draft: undefined, offset: 0 });
    async function save() {
        if (locked.current || savingChannels.current.review || savingChannels.current.offline || dirtyChannels.current.review || dirtyChannels.current.offline || !route.vessel || !user || !caps?.canWrite || denied || versionConflict || !editor || !owner || editor.owner !== owner || frozen && frozen.owner !== owner)
            return;
        const wasUnknown = frozen?.uncertain ?? false;
        const command = frozen?.command ?? dailyAttempt(route.kind, route.vessel, editor.fields, editor.record, editor.recordId);
        locked.current = true;
        setBusy(true);
        reportSaving('ordinary', true);
        setFrozen({ owner, command, uncertain: wasUnknown });
        setFailure(null);
        setNotice(false);
        const c = new AbortController();
        writer.current = c;
        const timer = setTimeout(() => c.abort(), 15000);
        let uncertain = false;
        let confirmedId: string | null = null;
        try {
            const row = await dailyApi.save(command, user.csrfToken, c.signal);
            if (active.current && identityBoundary.current !== owner) {
                uncertain = true;
                setFrozen({ owner, command, uncertain: true });
                setFailure(new ApiError(0, 'identity_boundary_changed'));
                return;
            }
            if (active.current) {
                setEditor({ owner, recordId: row.id, fields: row.fields, record: row, dirty: false });
                setFrozen(null);
                reportDirty('ordinary', false);
                setNotice(true);
                if (route.draft)
                    confirmedId = row.id;
            }
        }
        catch (error) {
            if (!active.current)
                return;
            const problem = error instanceof ApiError ? error : new ApiError(0);
            uncertain = wasUnknown || problem.status === 0 || problem.status >= 500;
            setFailure(problem);
            setFrozen(uncertain ? { owner, command, uncertain: true } : null);
            if ([401, 403, 404].includes(problem.status)) {
                setView(null);
                retry(n => n + 1);
            }
        }
        finally {
            clearTimeout(timer);
            writer.current = null;
            locked.current = false;
            if (active.current) {
                setBusy(false);
                reportSaving('ordinary', uncertain);
            }
        }
        if (confirmedId && active.current)
            onRecordSaved(confirmedId);
    }
    const link = (id: string) => dailyLocationPath({ ...route, id, draft: undefined, offset: 0 });
    return <div className="product-shell enterprise-shell daily-workspace"><EnterpriseChrome session={user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="daily-main"/><main id="daily-main" tabIndex={-1}><EnterprisePageHeader eyebrow={localizedText(en, "pairs.m_26797930a628b217")} title={title} description={localizedText(en, "pairs.m_1e12cc437c24c67e")} actionsLabel={localizedText(en, "pairs.m_18eedcbac7ec94d9")} actions={editor ? <Button variant="outline" disabled={busy || !!frozen || panelState.reviewSaving || panelState.offlineSaving} onClick={() => onNavigate(rootHref)}>{localizedText(en, "pairs.m_5fca6c50f2b1ccc6")}</Button> : caps?.canWrite ? <Button onClick={onStartDraft}>{localizedText(en, "pairs.m_ced646267c9def5d")}</Button> : undefined}/>
    {route.invalid ? <p role="alert">{localizedText(en, "pairs.m_03e90f107dc563d9")}</p> : !online || !visible ? <p role="status">{localizedText(en, "pairs.m_0e156d6bb5cf86f9")}</p> : !current ? <p role="status">{localizedText(en, "pairs.m_9031ce38ef38996d")}</p> : current.failed !== undefined ? <div role="alert"><p>{localizedText(en, "pairs.m_858b9c21c0889966")}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_efd357812eeb9551")}</Button></div> : !route.vessel ? <section aria-label={localizedText(en, "pairs.m_0b4807c1f18ad1eb")}><h2>{localizedText(en, "pairs.m_118d4b4dcafadb1e")}</h2>{current.vessels?.length ? <ul>{current.vessels.map(vessel => <li key={vessel.id}><Button variant="outline" onClick={() => onNavigate(dailyLocationPath({ ...route, vessel: vessel.id, offset: 0 }))}>{vessel.code} · {vessel.name}</Button></li>)}</ul> : <p>{localizedText(en, "pairs.m_91001af036515b5d")}</p>}<EnterprisePagination label={localizedText(en, "pairs.m_7b38dbce706d0227")} previous={localizedText(en, "pairs.m_7d3bb6f0b9a6734d")} next={localizedText(en, "pairs.m_a99bb90292c2965e")} previousDisabled={!route.offset} nextDisabled={!current.moreVessels} onPrevious={() => onNavigate(dailyLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(dailyLocationPath({ ...route, offset: route.offset + 25 }))}/></section> : caps && <>
      <aside className="daily-source-context" aria-label={localizedText(en, "pairs.m_1ab9187b92eafad0")}><Badge tone="warning">{localizedText(en, "pairs.m_8cd13ab22deeb528")}</Badge><details><summary>{localizedText(en, "pairs.m_2d6920533a6da322")}</summary><p>{localizedText(en, "pairs.m_96d4eca0cab041d2")}: {route.vessel}</p><code>{caps.sourceVersion}</code></details><p>{localizedText(en, "pairs.m_53adfca49bb92a5f")}</p></aside>
      {(notice || savedNotice) && !editor?.dirty && !ownerChanged && <p role="status">{localizedText(en, "pairs.m_80ad60f37d64adc3")}</p>}
      {failure && <div role="alert"><p>{frozen ? (localizedText(en, "pairs.m_75fe8b68bb469597")) : versionConflict ? (localizedText(en, "pairs.m_2aa2a09c13c9dc31")) : denied ? (localizedText(en, "pairs.m_4dfa357eae2f61f2")) : (localizedText(en, "pairs.m_1ef354de7cdbb32b"))}</p></div>}
      {ownerChanged ? <div role="alert"><p>{localizedText(en, "pairs.m_a4412b6f1eea1910")}</p>{!frozen && <Button variant="outline" onClick={() => onNavigate(rootHref)}>{localizedText(en, "pairs.m_838ff69f4b60d901")}</Button>}</div> : editor ? <section aria-label={localizedText(en, "pairs.m_a6d34adfe3ef38ac")}><h2>{editor.record ? (localizedText(en, "pairs.m_04164474fdce7d81")) : (localizedText(en, "pairs.m_8d9d59590fca291b"))}</h2>{!caps.canWrite && <p>{localizedText(en, "pairs.m_08a0c02829998cf4")}</p>}{(panelState.reviewDirty || panelState.reviewSaving || panelState.offlineDirty || panelState.offlineSaving) && <p role="status">{localizedText(en, "pairs.m_dd5531b32ae07c71")}</p>}{editor.record && <DailyRecordContext row={editor.record} language={language}/>}{current.row && editor.record && current.row.version !== editor.record.version && <p role="alert">{localizedText(en, "pairs.m_4f0887a5306364c9")}</p>}<form noValidate aria-label={localizedText(en, "pairs.m_4c1c8a82f1d3f7e7")} aria-busy={busy} onSubmit={event => { event.preventDefault(); void save(); }}><DailyReportFields kind={route.kind} fields={editor.fields} language={language} disabled={!editable} requiredKeys={caps.schema.filter(field => field.required && !field.readOnly).map(field => field.key)} unavailableKeys={caps.schema.filter(field => field.referenceUnavailable).map(field => field.key)} errors={failure?.errors ?? {}} narrativeKeys={caps.schema.filter(field => field.type === 'Narrative').map(field => field.key)} units={Object.fromEntries(caps.schema.map(field => [field.key, field.unit]))} maxLengths={Object.fromEntries(caps.schema.map(field => [field.key, field.type === 'Narrative' ? 4000 : field.type === 'Number' ? 48 : 512]))} onChange={(key, value) => { setEditor({ ...editor, fields: { ...editor.fields, [key]: value }, dirty: true }); reportDirty('ordinary', true); setNotice(false); setFailure(null); }}/><EnterpriseActions layout="form" aria-label={localizedText(en, "pairs.m_80be646fc7fa3045")}><Button variant="outline" disabled={busy || !!frozen || panelState.reviewSaving || panelState.offlineSaving} onClick={() => onNavigate(rootHref)}>{localizedText(en, "pairs.m_71be3ecd3d59de10")}</Button>{caps.canWrite && <Button type="submit" disabled={busy || ownerChanged || !!denied || versionConflict || panelState.reviewDirty || panelState.reviewSaving || panelState.offlineDirty || panelState.offlineSaving}>{frozen ? (localizedText(en, "pairs.m_d2f40a8f527fc75a")) : busy ? (localizedText(en, "pairs.m_5b598bccc6931d54")) : (localizedText(en, "pairs.m_7e0cdba2e467dbec"))}</Button>}</EnterpriseActions></form></section> : route.draft ? <p role="alert">{localizedText(en, "pairs.m_cd618b7f4d571f8b")}</p> : <section aria-label={localizedText(en, "pairs.m_7f9a19d86c5bafb4")}><h2>{localizedText(en, "pairs.m_e5fde405ed517a1b")}</h2>{current.page?.items.length ? <ul className="daily-record-list">{current.page.items.map(row => <li key={row.id}><Button variant="outline" onClick={() => onNavigate(link(row.id))}>{row.fields.report_date?.value ?? row.fields.log_date?.value ?? (localizedText(en, "pairs.m_7951e4b8a14341e7"))} · r{row.revision}</Button><span>{localizedText(en, "pairs.m_f276accd269c795a")}</span><time dateTime={row.savedAt}>{row.savedAt}</time></li>)}</ul> : <p>{localizedText(en, "pairs.m_ba5f5d5e55c7c386")}</p>}<EnterprisePagination label={localizedText(en, "pairs.m_758bb0e4bd06744e")} previous={localizedText(en, "pairs.m_390e962254c7c6d3")} next={localizedText(en, "pairs.m_84e6a63fd767db18")} previousDisabled={!route.offset} nextDisabled={!current.page?.hasMore || route.offset >= 100000} onPrevious={() => onNavigate(dailyLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(dailyLocationPath({ ...route, offset: route.offset + 25 }))}/></section>}
      {current.row && caps && !ownerChanged && <DailyOperationLaunch record={current.row} destinations={caps.destinations} language={language} onNavigate={onNavigate} disabled={busy || !!frozen || !!editor?.dirty || !!denied || versionConflict || panelState.reviewDirty || panelState.reviewSaving || panelState.offlineDirty || panelState.offlineSaving || editor?.record?.version !== current.row.version || editor?.record?.revision !== current.row.revision}/>}
      {route.id && current.history && <section aria-label={localizedText(en, "pairs.m_8334aeb3c6080103")}><h2>{localizedText(en, "pairs.m_edf58271348a6c9d")}</h2>{current.history.items.map(row => <details key={row.revision}><summary>{localizedText(en, "pairs.m_5cf7feeda047e695")} {row.revision} · {row.savedAt}</summary><DailyRecordContext row={row} language={language}/><dl className="daily-source-grid">{dailyFields(route.kind).map(field => <div key={field.key}><dt>{localizedLabel(field, language)}</dt><dd>{row.fields[field.key]?.state === 'Known' ? row.fields[field.key].value : row.fields[field.key]?.state === 'Unknown' ? (localizedText(en, "pairs.m_a5a7675bd7000dbd")) : (localizedText(en, "pairs.m_a5bd35d90de7c160"))}</dd></div>)}</dl></details>)}<EnterprisePagination label={localizedText(en, "pairs.m_195adeba8140e16a")} previous={localizedText(en, "pairs.m_79ebddaec7d89db0")} next={localizedText(en, "pairs.m_a41fcf745e74b43d")} previousDisabled={!historyOffset || !!editor?.dirty || !!frozen} nextDisabled={!current.history.hasMore || !!editor?.dirty || !!frozen} onPrevious={() => setHistoryOffset(n => Math.max(0, n - 25))} onNext={() => setHistoryOffset(n => n + 25)}/></section>}
    </>}{route.vessel && editor && <DailyOfflinePanel key={`${editor.owner}/${route.kind}/${route.vessel}/${editor.recordId}`} vessel={route.vessel} kind={route.kind} recordId={editor.recordId} record={editor.record ?? null} fields={editor.fields} enabled={!busy && !frozen && !denied && !versionConflict && visible && !panelState.reviewDirty && !panelState.reviewSaving && (online ? !!caps?.canWrite && owner === editor.owner : !!view?.caps?.canWrite && `${view.user?.tenantId}/${view.user?.actorId}` === editor.owner)} language={language} onDirty={offlineDirty} onSaving={offlineSaving} onCommitted={offlineCommit}/>}{offlineCommitted && editor && !ownerChanged && <p role="status">{localizedText(en, "pairs.m_5806d37a2d0a3652")}<Button variant="outline" onClick={() => onNavigate(link(editor.recordId))}>{localizedText(en, "pairs.m_2c645170152ddcda")}</Button></p>}{route.vessel && editor?.record && <DailyReviewPanel key={`${editor.owner}/${route.kind}/${route.vessel}/${editor.recordId}`} owner={editor.owner} session={user ?? null} record={editor.record} language={language} enabled={!busy && !frozen && !denied && !versionConflict && !editor.dirty && !panelState.offlineDirty && !panelState.offlineSaving && !ownerChanged} onDirty={reviewDirty} onSaving={reviewSaving} onCommitted={refreshCommitted} onReloadRecord={() => onNavigate(link(editor.recordId))}/>}{route.vessel && editor && !editor.record && <section aria-label={localizedText(en, "pairs.m_1bce2fee2ad58434")}><h3>{localizedText(en, "pairs.m_a575617cc0974a5d")}</h3><p>{localizedText(en, "pairs.m_edb383e7e6e499d6")}</p></section>}{!online && editor?.dirty && <p>{localizedText(en, "pairs.m_02b5d05e19db86ab")}</p>}{!current && online && <Button variant="outline" onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_efd357812eeb9551")}</Button>}<p className="daily-measurement-boundary">{localizedText(en, "pairs.m_a07eca3434958d9b")}</p></main></div>;
}
function DailyRecordContext({ row, language }: {
    row: DailyRecord;
    language: AppLocale;
}) {
    const en = language === 'en';
    return <div className="daily-record-context"><dl className="daily-source-grid"><div><dt>{localizedText(en, "pairs.m_5cf7feeda047e695")}</dt><dd>{row.revision}</dd></div><div><dt>{localizedText(en, "pairs.m_4f03e35eaf8245af")}</dt><dd><time dateTime={row.savedAt}>{row.savedAt}</time> · {localizedText(en, "pairs.m_3961ebf92dad0e81")}</dd></div><div><dt>{localizedText(en, "pairs.m_99ee874b1c896b5e")}</dt><dd>{row.reviewState === 'PENDING' ? (localizedText(en, "pairs.m_82decf38c307bd3c")) : (localizedText(en, "pairs.m_e444db98656d4518"))}</dd></div></dl>{row.reviewState === 'RECORDED_UNASSESSED' && <dl className="daily-source-grid"><div><dt>{localizedText(en, "pairs.m_6e1c8bb0cc6d76d6")}</dt><dd>{localizedText(en, "pairs.m_88e352cf3a6909ac")} {row.reviewedRevision}</dd></div><div><dt>{localizedText(en, "pairs.m_8781e7fa28418448")}</dt><dd>{row.reviewedAt && <time dateTime={row.reviewedAt}>{row.reviewedAt}</time>}</dd></div></dl>}<details><summary>{localizedText(en, "pairs.m_8243b3c791b322c1")}</summary><dl><div><dt>{localizedText(en, "pairs.m_3371cc7a3c622003")}</dt><dd>{row.id} / {row.version}</dd></div>{row.reviewedBy && <div><dt>{localizedText(en, "pairs.m_c831e4ec307c3323")}</dt><dd>{row.reviewedBy}</dd></div>}</dl><code>{row.hash}</code><ul>{row.blockers.map((code, i) => <li key={`${code}/${i}`}>{['HUMAN_REVIEW_AUTHORITY_UNAVAILABLE', 'HUMAN_REVIEW_NOT_RECORDED', 'REVIEW_RECORDING_IS_NOT_VERIFICATION'].includes(code) ? <>{reviewBlocker(code, en)} · <code>{code}</code></> : code}</li>)}</ul></details></div>;
}
