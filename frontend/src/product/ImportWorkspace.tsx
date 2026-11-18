import { localizedText } from "../i18n/messages";
import { useAppLanguage, type Locale as AppLocale } from "../i18n";
import { EnterpriseChrome } from './EnterpriseChrome';
import { useCallback, useLayoutEffect, useEffect, useRef, useState } from 'react';
import { ApiError, session } from './api';
import type { Session } from './directory-contract';
import * as api from './import-api';
import { roster, type CrewPage } from './crew-api';
import { ImportMappingEditor, type ImportMappingBinding } from './ImportMappingEditor';
import { ImportValidationResults } from './ImportValidationResults';
import './import-workspace.css';
import { unsavedState, useUnsavedChangesSource } from './unsaved-changes';
export function ImportWorkspace({ vessel }: {
    vessel: string;
}) {
    const [language, setLanguage] = useAppLanguage(), [identity, setIdentity] = useState<Session | null>(null);
    const [href, setHref] = useState(window.location.href), [generation, setGeneration] = useState(0), [error, setError] = useState(0);
    const [dirty, setDirty] = useState(false), [pending, setPending] = useState<string | null>(null);
    const location = useRef(href), dirtyRef = useRef(false), savingRef = useRef(false);
    // DEV-021 AC-07: an in-flight write or uncertain commit outcome blocks a shell context switch.
    useUnsavedChangesSource(() => unsavedState(dirtyRef.current, savingRef.current));
    const reportDirty = useCallback((value: boolean, saving = false) => { savingRef.current = saving; setDirty(value); }, []);
    const navigationCommit = useRef<(() => void) | null>(null);
    useLayoutEffect(() => { dirtyRef.current = dirty; }, [dirty]);
    const en = language === 'en';
    const resetScope1 = [generation] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], generation)) {
        setPreviousScope1(resetScope1);
        setIdentity(null);
        setError(0);
    }
    useEffect(() => {
        const controller = new AbortController();
        session(controller.signal).then(s => { if (!controller.signal.aborted) {
            if (s.authenticated && s.canRead)
                setIdentity(s);
            else
                setError(401);
        } })
            .catch(e => { if (!controller.signal.aborted)
            setError(e instanceof ApiError ? e.status : 503); });
        return () => controller.abort();
    }, [generation]);
    useEffect(() => {
        const leave = (event: BeforeUnloadEvent) => { if (dirtyRef.current) {
            event.preventDefault();
            event.returnValue = '';
        } };
        const pop = () => {
            const next = window.location.href;
            if (dirtyRef.current) {
                history.pushState(null, '', location.current);
                setPending(next);
            }
            else {
                location.current = next;
                setHref(next);
            }
        };
        const privacy = () => { if (document.visibilityState === 'hidden')
            setIdentity(null);
        else
            setGeneration(g => g + 1); };
        window.addEventListener('beforeunload', leave);
        window.addEventListener('popstate', pop);
        document.addEventListener('visibilitychange', privacy);
        return () => { window.removeEventListener('beforeunload', leave); window.removeEventListener('popstate', pop); document.removeEventListener('visibilitychange', privacy); };
    }, []);
    useEffect(() => { document.title = `POSEIDON · ${localizedText(en, "pairs.m_f1779f35acec39d7")}`; }, [language, en]);
    function commitNavigation(next: string) {
        if (!next.startsWith(`/app/excel-migration/${vessel}`)) {
            window.location.assign(next);
            return;
        }
        history.pushState(null, '', next);
        location.current = window.location.href;
        setHref(window.location.href);
    }
    function navigate(next: string, committed?: () => void) {
        if (dirty) {
            navigationCommit.current = committed ?? null;
            setPending(next);
            return;
        }
        committed?.();
        commitNavigation(next);
    }
    return <div className="product-shell enterprise-shell import-workspace">
    <EnterpriseChrome session={identity} vessel={vessel} language={language} onLanguageChange={setLanguage} onNavigate={navigate} title={localizedText(en, "pairs.m_f1779f35acec39d7")} mainId="import-main"/>
    <main id="import-main" tabIndex={-1}><h1>{localizedText(en, "pairs.m_f1779f35acec39d7")}</h1>
      <p>{localizedText(en, "pairs.m_d8a853fd0ec0a8bf")}</p>
      {error ? <div role="alert"><p>{error === 401 ? (localizedText(en, "pairs.m_48c324493374e672")) : (localizedText(en, "pairs.m_08b20099663c7a89"))}</p>
        <button onClick={() => setGeneration(g => g + 1)}>{localizedText(en, "pairs.m_044bf27da41fda96")}</button>{error === 401 && <a href={`/api/v1/auth/login?returnUrl=${encodeURIComponent(window.location.pathname)}`}>{localizedText(en, "pairs.m_16505c0ee5e527ad")}</a>}</div>
            : !identity ? <p role="status">{localizedText(en, "pairs.m_a603212553b98958")}</p>
                : <Importer key={`${generation}:${identity.tenantId}:${identity.actorId}:${identity.csrfToken}:${vessel}:${href}`} vessel={vessel} language={language} user={identity} href={href} navigate={navigate} savedNavigate={next => { setDirty(false); dirtyRef.current = false; history.pushState(null, '', next); location.current = window.location.href; setHref(window.location.href); }} onDirty={reportDirty}/>}
    </main>
    {pending && <div role="dialog" aria-modal="true" aria-label={localizedText(en, "pairs.m_27ecae4f282f7b40")}>
      <p>{localizedText(en, "pairs.m_69685d1c6273381b")}</p>
      <button autoFocus onClick={() => { navigationCommit.current = null; setPending(null); }}>{localizedText(en, "pairs.m_9a036e9bba5f46c4")}</button>
      <button onClick={() => { setDirty(false); dirtyRef.current = false; const next = pending; setPending(null); navigationCommit.current?.(); navigationCommit.current = null; commitNavigation(next); }}>{localizedText(en, "pairs.m_86c18d487dfe252a")}</button>
    </div>}
  </div>;
}
function Importer({ vessel, language, user, href, navigate, savedNavigate, onDirty }: {
    vessel: string;
    language: AppLocale;
    user: Session;
    href: string;
    navigate: (href: string) => void;
    savedNavigate: (href: string) => void;
    onDirty: (dirty: boolean, saving?: boolean) => void;
}) {
    const en = language === 'en', query = new URL(href).searchParams;
    const [templates, setTemplates] = useState<api.ImportTemplate[]>([]), [category, setCategory] = useState('vessel');
    const [assignment, setAssignment] = useState(query.get('assignment') ?? ''), [crew, setCrew] = useState<CrewPage | null>(null), [crewOffset, setCrewOffset] = useState(0);
    const [batch, setBatch] = useState<api.ImportBatch | null>(null), [bindings, setBindings] = useState<readonly ImportMappingBinding[]>([]);
    const [preview, setPreview] = useState<api.Preview | null>(null), [outcome, setOutcome] = useState<api.Committed | null>(null);
    const [busy, setBusy] = useState(true), [error, setError] = useState(0), [failure, setFailure] = useState<'draft' | 'preview' | null>(null);
    const [file, setFile] = useState<File | null>(null), [dirty, setDirty] = useState(false), [uncertain, setUncertain] = useState(false), [writing, setWriting] = useState(false);
    const [historyRows, setHistoryRows] = useState<Awaited<ReturnType<typeof api.history>> | null>(null), [historyOffset, setHistoryOffset] = useState(0);
    const attempt = useRef<{
        key: string;
        preview: api.Preview;
    } | null>(null), operation = useRef<AbortController | null>(null), mounted = useRef(true);
    const requestedAssignment = query.get('assignment');
    const selectedTemplate = batch?.template ?? templates.find(t => t.category === category);
    const contextKey = `${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${batch?.id ?? 'new'}:${selectedTemplate?.version}:${selectedTemplate?.fingerprint}`;
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; operation.current?.abort(); onDirty(false); }; }, [onDirty]);
    useEffect(() => { onDirty(dirty || uncertain || busy && !!attempt.current, uncertain || writing); }, [dirty, uncertain, busy, writing, onDirty]);
    useEffect(() => {
        const c = new AbortController();
        operation.current = c;
        async function load() {
            const list = await api.templates(vessel, c.signal);
            if (c.signal.aborted)
                return;
            setTemplates(list);
            if (list[0])
                setCategory(query.has('assignment') && list.some(t => t.category === 'certificate') ? 'certificate' : list[0].category);
            const id = query.get('batch');
            if (id) {
                const b = await api.detail(vessel, id, c.signal);
                if (c.signal.aborted)
                    return;
                setBatch(b);
                setCategory(b.category);
                setBindings(b.bindings);
                if (query.get('preview')) {
                    const p = await api.page(vessel, b.id, { previewId: query.get('preview')!, revision: query.get('revision') ?? '', templateFingerprint: query.get('fingerprint') ?? '' } as api.Preview, Number(query.get('offset') ?? 0), c.signal);
                    if (!c.signal.aborted) {
                        api.results(p, b.template, contextKey);
                        setPreview(p);
                    }
                }
            }
        }
        load().catch(e => fail(e, c)).finally(() => { if (!c.signal.aborted)
            setBusy(false); });
        return () => c.abort();
        // A new href or actor/session mounts a fresh private workspace.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);
    const resetScope2 = [vessel, historyOffset, outcome] as const;
    const [previousScope2, setPreviousScope2] = useState(resetScope2);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope2[0], vessel) || !Object.is(previousScope2[1], historyOffset) || !Object.is(previousScope2[2], outcome)) {
        setPreviousScope2(resetScope2);
        setHistoryRows(null);
    }
    useEffect(() => {
        const c = new AbortController();
        api.history(vessel, historyOffset, c.signal).then(rows => { if (!c.signal.aborted)
            setHistoryRows(rows); }).catch(() => { });
        return () => c.abort();
    }, [vessel, historyOffset, outcome]);
    const resetScope3 = [category, vessel, crewOffset, batch, requestedAssignment] as const;
    const [previousScope3, setPreviousScope3] = useState(resetScope3);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope3[0], category) || !Object.is(previousScope3[1], vessel) || !Object.is(previousScope3[2], crewOffset) || !Object.is(previousScope3[3], batch)) {
        setPreviousScope3(resetScope3);
        setCrew(null);
        setAssignment('');
    }
    useEffect(() => {
        if (category !== 'certificate' || batch)
            return;
        const c = new AbortController();
        roster(vessel, crewOffset, c.signal).then(rows => { if (!c.signal.aborted) {
            setCrew(rows);
            if (rows.items.some(r => r.id === requestedAssignment))
                setAssignment(requestedAssignment!);
        } }).catch(() => { if (!c.signal.aborted)
            setError(403); });
        return () => c.abort();
    }, [category, vessel, crewOffset, batch, requestedAssignment]);
    function start() { operation.current?.abort(); const c = new AbortController(); operation.current = c; setBusy(true); setError(0); return c; }
    function fail(e: unknown, c: AbortController) {
        if (!c.signal.aborted && mounted.current) {
            const status = e instanceof ApiError ? e.status : 503;
            setError(status);
            if ([401, 403, 404].includes(status)) {
                setBatch(null);
                setPreview(null);
                setBindings([]);
                setHistoryRows(null);
                setTemplates([]);
                setFile(null);
                onDirty(false);
            }
        }
    }
    // A write (upload, draft, commit) in flight is reported as 'saving' to the shell context switch.
    function startWrite() { const c = start(); setWriting(true); return c; }
    function finish(c: AbortController) { if (operation.current === c && mounted.current)
        setWriting(false); if (!c.signal.aborted && mounted.current)
        setBusy(false); }
    function route(b: api.ImportBatch, p?: api.Preview, offset = 0) {
        const q = new URLSearchParams({ batch: b.id });
        if (p) {
            q.set('preview', p.previewId);
            q.set('revision', p.revision);
            q.set('fingerprint', p.templateFingerprint);
            q.set('offset', String(offset));
        }
        return `/app/excel-migration/${vessel}?${q}`;
    }
    async function upload() {
        if (!file || !selectedTemplate || busy || category === 'certificate' && !assignment)
            return;
        if (file.size > selectedTemplate.maxBytes || !file.name.toLowerCase().endsWith('.xlsx')) {
            setError(400);
            return;
        }
        const c = startWrite();
        try {
            const b = await api.upload(vessel, category, assignment, file, user.csrfToken, c.signal);
            if (!c.signal.aborted)
                navigate(route(b));
        }
        catch (e) {
            fail(e, c);
        }
        finally {
            finish(c);
        }
    }
    async function save(previewAfter: boolean) {
        if (!batch || busy || uncertain)
            return;
        const c = startWrite();
        setFailure(null);
        setPreview(null);
        try {
            const b = await api.draft(vessel, batch, bindings, user.csrfToken, c.signal);
            if (c.signal.aborted)
                return;
            setBatch(b);
            setDirty(false);
            onDirty(false, previewAfter);
            if (previewAfter) {
                const p = await api.preview(vessel, b.id, user.csrfToken, c.signal);
                if (!c.signal.aborted) {
                    setPreview(p);
                    savedNavigate(route(b, p));
                }
            }
        }
        catch (e) {
            if (!c.signal.aborted)
                setFailure(previewAfter ? 'preview' : 'draft');
            fail(e, c);
        }
        finally {
            finish(c);
        }
    }
    async function commit() {
        if (!batch || !preview || busy || dirty)
            return;
        if (!attempt.current)
            attempt.current = { key: crypto.randomUUID(), preview };
        const c = startWrite();
        try {
            const result = await api.commit(vessel, batch.id, attempt.current.preview, user.csrfToken, attempt.current.key, c.signal);
            if (!c.signal.aborted) {
                setOutcome(result);
                setBatch({ ...batch, state: result.state });
                setUncertain(false);
                attempt.current = null;
                onDirty(false);
            }
        }
        catch (e) {
            if (!c.signal.aborted) {
                // An interrupted response can follow an actual commit. Keep the exact attempt.
                const status = e instanceof ApiError ? e.status : 0;
                setUncertain(status === 0 || status >= 500);
                if (status > 0 && status < 500)
                    attempt.current = null;
            }
            fail(e, c);
        }
        finally {
            finish(c);
        }
    }
    async function download(reject: boolean) {
        if (!selectedTemplate || busy)
            return;
        const c = start();
        try {
            const blob = await api.download(vessel, category, reject ? batch?.id ?? null : null, reject ? preview : null, c.signal);
            if (!c.signal.aborted && mounted.current) {
                const url = URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = url;
                link.download = reject ? 'poseidon-rejects.csv' : `poseidon-${category}-declaration-v1.xlsx`;
                link.click();
                URL.revokeObjectURL(url);
            }
        }
        catch (e) {
            fail(e, c);
        }
        finally {
            finish(c);
        }
    }
    const state = busy && !batch ? 'loading' : error === 401 || error === 403 || error === 404 ? 'denied' : error ? 'unavailable' : 'ready';
    let resultPage: ReturnType<typeof api.results> | null = null;
    try {
        if (preview && batch)
            resultPage = api.results(preview, batch.template, contextKey);
    }
    catch { /* Invalid/stale contracts render the safe unavailable component state. */ }
    const categories: Record<string, string> = { vessel: localizedText(en, "pairs.m_e50c79dfa3d76f1e"), crew: localizedText(en, "pairs.m_9c7ff80bf1b60400"), certificate: localizedText(en, "pairs.m_02399e1be0969887"), equipment: localizedText(en, "pairs.m_77e9d3d4c2e4b44f") };
    const states: Record<string, string> = { Uploaded: localizedText(en, "pairs.m_6133f2807eb3bb7a"), MappingDraft: localizedText(en, "pairs.m_f85304d8cbfd1637"), Previewed: localizedText(en, "pairs.m_610b9a387569e88f"), Committed: localizedText(en, "pairs.m_7eec2adba8b1cdfb") };
    const unknown = localizedText(en, "pairs.m_49138f2831bf4262");
    return <>
    {error > 0 && <p role="alert">{error === 409 ? (localizedText(en, "pairs.m_0006f134ef1319bb")) : state === 'denied' ? (localizedText(en, "pairs.m_99402e4a002adc93")) : (localizedText(en, "pairs.m_6cf513cc79d26986"))}</p>}
    <button disabled={busy || uncertain} onClick={() => navigate(`/app/excel-migration/${vessel}`)}>{localizedText(en, "pairs.m_2a1f3f0b91db2b75")}</button>
    {!batch && <section aria-label={localizedText(en, "pairs.m_c68ef97480ced953")}>
      <label>{localizedText(en, "pairs.m_ef14a663489ac744")}<select value={category} disabled={busy} onChange={e => { setCategory(e.target.value); setFile(null); setBindings([]); setPreview(null); }}>{templates.map(t => <option key={t.category} value={t.category}>{({ vessel: localizedText(en, "pairs.m_e50c79dfa3d76f1e"), crew: localizedText(en, "pairs.m_9c7ff80bf1b60400"), certificate: localizedText(en, "pairs.m_02399e1be0969887"), equipment: localizedText(en, "pairs.m_77e9d3d4c2e4b44f") } as Record<string, string>)[t.category]}</option>)}</select></label>
      {category === 'certificate' && <><label>{localizedText(en, "pairs.m_0da4716937c74727")}<select value={assignment} onChange={e => setAssignment(e.target.value)} disabled={busy || !crew}><option value="">{localizedText(en, "pairs.m_cd30c51c32f90a2b")}</option>{crew?.items.map((r, i) => <option key={r.id} value={r.id}>{r.fullName ?? `${localizedText(en, "pairs.m_75390dbd3e2e07a2")} ${crewOffset + i + 1}`}</option>)}</select></label>
        <button disabled={!crew || busy || crewOffset === 0} onClick={() => setCrewOffset(x => x - 25)}>{localizedText(en, "pairs.m_f7a4cde03cfe3908")}</button><button disabled={!crew?.hasMore || busy} onClick={() => setCrewOffset(x => x + 25)}>{localizedText(en, "pairs.m_9964a367c5d94f52")}</button></>}
      {selectedTemplate && <><p>{localizedText(en, "pairs.m_52248251f25bd3d1")}: {selectedTemplate.version} · XLSX · {selectedTemplate.maxRows} {localizedText(en, "pairs.m_6bbf8e70349f0cec")}</p>
        <p>{localizedText(en, "pairs.m_02ca2dc6a3dc0494")}</p>
        <button disabled={busy} onClick={() => void download(false)}>{localizedText(en, "pairs.m_5f75802ae8fbf8a7")}</button>
        <details><summary>{localizedText(en, "pairs.m_b58d8f26ceb4e920")}</summary><ul>{selectedTemplate.fields.map(f => <li key={f.key}>{api.label(f.key, language)} · {f.userWritable ? (localizedText(en, "pairs.m_d6cf8a425e0e7675")) : (localizedText(en, "pairs.m_7cf6a5a76fd8b78b"))}</li>)}</ul></details></>}
      <label>{localizedText(en, "pairs.m_3e5622a2e4c76097")}<input type="file" accept=".xlsx" disabled={busy} onChange={e => setFile(e.target.files?.[0] ?? null)}/></label>
      <button disabled={busy || !file || !selectedTemplate || category === 'certificate' && !assignment} onClick={() => void upload()}>{localizedText(en, "pairs.m_847ad6e344cd531c")}</button>
      {!busy && templates.length === 0 && !error && <p>{localizedText(en, "pairs.m_68cdcda48c85e9e8")}</p>}
    </section>}
    {batch && state !== 'denied' && <>
      <p>{localizedText(en, "pairs.m_0daeb5839bf5190f")}: <bdi className="import-hash">{batch.sourceHash}</bdi></p><p>{localizedText(en, "pairs.m_ac8b056f75c10eff")}: {states[batch.state] ?? unknown}</p>
      {batch.state !== 'Committed' && !outcome && <ImportMappingEditor language={language} currentContextKey={contextKey} loadedContextKey={contextKey} state={state} template={api.mapping(batch.template, language)} headers={batch.headers} bindings={bindings} busy={busy || uncertain} failure={failure} onChange={b => { setBindings(b); setDirty(true); setPreview(null); attempt.current = null; }} onSaveDraft={() => void save(false)} onPreview={() => void save(true)}/>}
      {preview && !outcome && batch.state !== 'Committed' && <ImportValidationResults language={language} contextKey={contextKey} state={state === 'ready' ? 'ready' : 'unavailable'} page={resultPage} fieldLabels={batch.template.fields.map(f => api.label(f.key, language))} busy={busy || uncertain} canDownloadRejectReport={!dirty} onPage={offset => { if (!busy && !uncertain)
                navigate(route(batch, preview, offset)); }} onDownloadRejectReport={() => void download(true)}/>}
      {uncertain && <p role="alert">{localizedText(en, "pairs.m_8d3be7060bddc2fb")}</p>}
      {preview && !outcome && batch.state !== 'Committed' && <><p>{localizedText(en, "pairs.m_d60acf62e409d707")}</p>
        <button disabled={busy || dirty || !resultPage || preview.rejectedRows > 0 || preview.totalRows === 0} onClick={() => void commit()}>{uncertain ? (localizedText(en, "pairs.m_6747f6624c82009a")) : (localizedText(en, "pairs.m_1fc6421e74cd42ce"))}</button></>}
      <p>{localizedText(en, "pairs.m_5a238a8bb4947bf3")}</p>
      {!outcome && batch.state === 'Committed' && <p role="status">{localizedText(en, "pairs.m_1711996faa5ba3d5")}: {batch.rowCount} · {localizedText(en, "pairs.m_d866166a42c4849e")}</p>}
      {outcome && <p role="status">{localizedText(en, "pairs.m_1711996faa5ba3d5")}: {outcome.importedRows} · {localizedText(en, "pairs.m_d866166a42c4849e")}</p>}
    </>}
    <section aria-label={localizedText(en, "pairs.m_31fde46f68bd6c52")}><h2>{localizedText(en, "pairs.m_e015b30e062ea3ac")}</h2>
      {!historyRows ? <p>{localizedText(en, "pairs.m_fbed5be382131c38")}</p> : historyRows.items.length === 0 ? <p>{localizedText(en, "pairs.m_2e3c113f32294ec7")}</p> : <ul>{historyRows.items.map(r => <li key={r.id}><button disabled={busy || uncertain} onClick={() => navigate(`/app/excel-migration/${vessel}?batch=${r.id}`)}>{categories[r.category] ?? unknown} · {states[r.state] ?? unknown} · {r.rowCount}</button></li>)}</ul>}
      <button disabled={busy || historyOffset === 0} onClick={() => setHistoryOffset(x => x - 25)}>{localizedText(en, "pairs.m_4491d86d2190518e")}</button><button disabled={busy || !historyRows?.hasMore} onClick={() => setHistoryOffset(x => x + 25)}>{localizedText(en, "pairs.m_2ed0722a8f770d97")}</button>
    </section>
  </>;
}
