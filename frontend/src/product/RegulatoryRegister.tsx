import { presentationLabel } from '../i18n/labels';
import { sourceText, localizedTuple, localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import './regulatory-enterprise.css';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import { loginHref } from './workspace-location';
import { LifecycleApplicability } from './LifecycleApplicability';
import { ControlEvidence } from './ControlEvidence';
import { DraftConfirmation } from './DraftConfirmation';
import * as service from './register-api';
import { readRequirement, type Requirement, type RequirementLifecycle } from './requirement-api';
import type { Draft, FieldKey, RegisterKind, RegisterPage, Revision } from './register-api';
const labels: Record<FieldKey, [
    string,
    string
]> = { title: [sourceText("en", "pairs.m_fa3e5e76e1977a4f"), sourceText("tr", "pairs.m_fa3e5e76e1977a4f")], authority: [sourceText("en", "pairs.m_39db52349248b598"), sourceText("tr", "pairs.m_39db52349248b598")], locator: [sourceText("en", "pairs.m_be6034131c381637"), sourceText("tr", "pairs.m_be6034131c381637")], instrument: [sourceText("en", "pairs.m_9ac4381a3797c51f"), sourceText("tr", "pairs.m_9ac4381a3797c51f")], amendment: [sourceText("en", "pairs.m_4871770ea162d7eb"), sourceText("tr", "pairs.m_4871770ea162d7eb")], effectiveDate: [sourceText("en", "pairs.m_de8b1680f4d7cb9a"), sourceText("tr", "pairs.m_de8b1680f4d7cb9a")], declaration: [sourceText("en", "pairs.m_d2045a0b1f190866"), sourceText("tr", "pairs.m_d2045a0b1f190866")], observedAt: [sourceText("en", "pairs.m_9482f9ce103abf01"), sourceText("tr", "pairs.m_9482f9ce103abf01")] };
type Props = {
    owner?: string | null;
    kind: RegisterKind;
    id: string | null;
    language: AppLocale;
    csrf: string;
    canWrite: boolean;
    offline: boolean;
    onDirty: (v: boolean) => void;
    onSaving: (v: boolean) => void;
    onNavigate: (href: string) => void;
};
export function RegulatoryRegister(props: Props) {
    const [chain, setChain] = useState<'lifecycle' | 'controls' | null>(null), [chainDirty, setChainDirty] = useState(false), [confirm, setConfirm] = useState(false);
    const [chainSaving, setChainSaving] = useState(false);
    const [registerDirty, setRegisterDirty] = useState(false), [registerSaving, setRegisterSaving] = useState(false), [enterConfirm, setEnterConfirm] = useState(false);
    const [destination, setDestination] = useState<'lifecycle' | 'controls'>('lifecycle');
    const enter = () => { if (registerSaving) return; props.onDirty(false); setRegisterDirty(false); setEnterConfirm(false); setChain(destination); };
    const close = () => { if (chainSaving) return; props.onDirty(false); setChainDirty(false); setChain(null); setConfirm(false); };
    return chain ? <>
    <Button variant="outline" type="button" disabled={chainSaving} onClick={() => chainDirty ? setConfirm(true) : close()}>{presentationLabel(props.language, 'registerBack')}</Button>
    {confirm && <DraftConfirmation language={props.language} onStay={() => setConfirm(false)} onDiscard={close}/>}
    {chain === 'lifecycle' ? <LifecycleApplicability language={props.language} csrf={props.csrf} offline={props.offline} onDirty={v => { setChainDirty(v); props.onDirty(v); }} onSaving={v => { setChainSaving(v); props.onSaving(v); }}/> : <ControlEvidence owner={props.owner} language={props.language} csrf={props.csrf} offline={props.offline} onDirty={v => { setChainDirty(v); props.onDirty(v); }} onSaving={v => { setChainSaving(v); props.onSaving(v); }}/>}
  </> : <>
    {!props.id && <EnterpriseActions align="start" aria-label={presentationLabel(props.language, 'regulatoryWorkspaces')}><Button variant="outline" type="button" disabled={registerSaving} onClick={() => {
                setDestination('lifecycle');
                if (registerDirty)
                    setEnterConfirm(true);
                else {
                    props.onDirty(false);
                    setChain('lifecycle');
                }
            }}>{localizedText(props.language, 'pairs.m_0176b8e9e9e34d3c')}</Button>
    <Button variant="outline" type="button" disabled={registerSaving} onClick={() => {
                setDestination('controls');
                if (registerDirty)
                    setEnterConfirm(true);
                else {
                    props.onDirty(false);
                    setChain('controls');
                }
            }}>{localizedText(props.language, 'pairs.m_edd35c8e32c4c9c9')}</Button></EnterpriseActions>}
    {enterConfirm && <DraftConfirmation language={props.language} onStay={() => setEnterConfirm(false)} onDiscard={enter}/>}
    <RegisterContext key={`${props.kind}/${props.id ?? 'list'}`} {...props} onDirty={v => { setRegisterDirty(v); props.onDirty(v); }} onSaving={v => { setRegisterSaving(v); props.onSaving(v); }}/>
  </>;
}
function RegisterContext({ kind, id, language, csrf, canWrite, offline, onDirty, onSaving, onNavigate }: Props) {
    const [rows, setRows] = useState<RegisterPage<Draft> | null>(null);
    const [draft, setDraft] = useState<Draft | null>(null);
    const [content, setContent] = useState(service.blankContent);
    const [sourceId, setSourceId] = useState<string | null>(null);
    const [sourceRevision, setSourceRevision] = useState<number | null>(null);
    const [sources, setSources] = useState<RegisterPage<Draft> | null>(null);
    const [sourceVersions, setSourceVersions] = useState<RegisterPage<Revision> | null>(null);
    const [sourceOffset, setSourceOffset] = useState(0);
    const [sourceVersionOffset, setSourceVersionOffset] = useState(0);
    const [sourceQuery, setSourceQuery] = useState('');
    const [sourceSearch, setSourceSearch] = useState('');
    const [history, setHistory] = useState<RegisterPage<Revision> | null>(null);
    const [historyOffset, setHistoryOffset] = useState(0);
    const [frozen, setFrozen] = useState<{
        key: string;
        body: string;
    } | null>(null);
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [error, setError] = useState<number | null>(null);
    const [historyError, setHistoryError] = useState(false);
    const [sourceError, setSourceError] = useState(false);
    const [sourceAttempt, setSourceAttempt] = useState(0);
    const [historyAttempt, setHistoryAttempt] = useState(0);
    const [notice, setNotice] = useState(false);
    const [offset, setOffset] = useState(0);
    const [query, setQuery] = useState('');
    const [search, setSearch] = useState('');
    const [attempt, setAttempt] = useState(0);
    const [creating, setCreating] = useState(false);
    const [conflict, setConflict] = useState(false);
    // DEV-026: server-derived lifecycle; a failed read shows Unavailable, never an assumed Draft.
    const [requirement, setRequirement] = useState<Requirement | 'unavailable' | null>(null);
    const editor = !!id || creating;
    const retained = useRef(false);
    useEffect(() => { retained.current = dirty || !!frozen; }, [dirty, frozen]);
    const editable = canWrite && (!draft || draft.canEdit);
    useEffect(() => { onDirty(dirty || !!frozen); }, [dirty, frozen, onDirty]);
    useEffect(() => { onSaving(saving); }, [saving, onSaving]);
    useEffect(() => {
        if (offline || id && retained.current)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        const work = id ? service.readDraft(kind, id, c.signal).then(d => {
            if (!active)
                return;
            setDraft(d);
            setContent(d.current.content);
            setSourceId(d.current.sourceId);
            setSourceRevision(d.current.sourceRevision);
        }) : service.listDrafts(kind, search, offset, c.signal).then(d => {
            if (active)
                setRows(d);
        });
        work.catch(e => {
            if (active)
                setError(e instanceof ApiError ? e.status : 0);
        });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [id, kind, search, offset, attempt, offline]);
    useEffect(() => {
        if (!id || offline)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        service.historyDraft(kind, id, historyOffset, c.signal).then(d => {
            if (active)
                setHistory(d);
        }).catch(() => {
            if (active)
                setHistoryError(true);
        });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [id, kind, historyOffset, historyAttempt, offline]);
    useEffect(() => {
        if (!editor || kind !== 'requirements' || offline)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        service.listDrafts('sources', sourceSearch, sourceOffset, c.signal).then(d => {
            if (active)
                setSources(d);
        }).catch(() => {
            if (active)
                setSourceError(true);
        });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [editor, kind, sourceSearch, sourceOffset, sourceAttempt, offline]);
    useEffect(() => {
        if (!sourceId || offline)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        service.historyDraft('sources', sourceId, sourceVersionOffset, c.signal).then(d => {
            if (active)
                setSourceVersions(d);
        }).catch(() => {
            if (active)
                setSourceError(true);
        });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [sourceId, sourceVersionOffset, sourceAttempt, offline]);
    useEffect(() => {
        if (kind !== 'requirements' || !id || offline)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        readRequirement(id, c.signal).then(r => {
            if (active)
                setRequirement(r);
        }).catch(() => {
            if (active)
                setRequirement('unavailable');
        });
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [kind, id, historyAttempt, offline]);
    const lifecycleLabel = (value: RequirementLifecycle) => localizedText(language, ({ DRAFT: 'pairs.m_6f7b0af9992cd6bf', ADOPTED: 'pairs.m_273d3f4a33b570b2', FUTURE: 'pairs.m_3c0a0a26b887c8ad',
        IN_FORCE: 'pairs.m_a4f326ab525f2a2f', SUPERSEDED: 'pairs.m_61ecb1880ed2d354', UNAVAILABLE: 'pairs.m_49138f2831bf4262' } as const)[value]);
    // Reconnection refreshes list/history only; dirty editor values and frozen commands stay in memory.
    const denied = error === 401 || error === 403 || error === 404;
    const locked = saving || !!frozen || conflict || denied || !editable;
    const save = async () => {
        if (!editable || saving || offline || conflict || denied)
            return;
        if (!service.validContent(content) || (sourceId !== null && sourceRevision === null)) {
            setError(400);
            return;
        }
        const payload: service.Command = { expectedVersion: draft?.version ?? 0, content, sourceId, sourceRevision };
        const command = frozen ?? { key: crypto.randomUUID(), body: JSON.stringify(payload) };
        setFrozen(command);
        setSaving(true);
        setError(null);
        const c = new AbortController();
        const timer = setTimeout(() => c.abort(), 15000);
        try {
            const d = await service.saveDraft(kind, id ?? draft?.current.id ?? null, command.body, csrf, command.key, c.signal);
            setDraft(d);
            setContent(d.current.content);
            setDirty(false);
            setFrozen(null);
            onDirty(false);
            setNotice(true);
            setHistory(null);
            setHistoryAttempt(v => v + 1);
        }
        catch (e) {
            const status = e instanceof ApiError ? e.status : 0;
            setError(status);
            if ([400, 401, 403, 404, 409].includes(status))
                setFrozen(null);
            if (status === 409)
                setConflict(true);
        }
        finally {
            clearTimeout(timer);
            setSaving(false);
        }
    };
    const title = (d: Draft) => d.current.content[kind === 'sources' ? 'title' : 'instrument'].value ?? d.current.id;
    const pageButtons = (page: {
        hasMore: boolean;
    } | null, value: number, next: (n: number) => void) => <EnterprisePagination label={localizedText(language, "pairs.m_8b6433d45192da49")} previous={localizedText(language, "pairs.m_2f7d24c724255a10")} next={localizedText(language, "pairs.m_93a493ffda9a1959")} previousDisabled={!value || saving} nextDisabled={!page?.hasMore || saving} onPrevious={() => next(Math.max(0, value - 25))} onNext={() => next(value + 25)}/>;
    return <section className="regulatory-workspace" aria-label={presentationLabel(language, 'registerTitle')}>
    <p>{presentationLabel(language, 'registerDisclaimer')}</p>
    {error !== null && <div role="alert"><p>{error === 401 ? presentationLabel(language, 'registerSignInRetain') : error === 403 ? presentationLabel(language, 'registerDenied') : error === 409 ? presentationLabel(language, 'registerConflict') : error === 400 ? presentationLabel(language, 'registerCheckFields') : presentationLabel(language, 'registerServiceRetain')}</p>{error === 401 && <a href={loginHref()}>{localizedText(language, "pairs.m_16505c0ee5e527ad")}</a>}{!editor && <Button onClick={() => { setError(null); setAttempt(v => v + 1); }}>{localizedText(language, "pairs.m_044bf27da41fda96")}</Button>}</div>}
    {notice && <p role="status">{presentationLabel(language, 'registerSaved')}</p>}
    {error !== null && id && !draft && <Button onClick={() => { setError(null); setAttempt(v => v + 1); }}>{presentationLabel(language, 'registerRetryDraft')}</Button>}
    {draft && !id && <Button onClick={() => onNavigate(`/app/${kind}/${draft.current.id}`)}>{presentationLabel(language, 'registerOpenSaved')}</Button>}
    {conflict && id && <Button onClick={() => onNavigate(`/app/${kind}/${id}`)}>{presentationLabel(language, 'registerReloadSaved')}</Button>}
    {!editor ? <>
      {canWrite && <Button onClick={() => { setCreating(true); setDirty(false); setError(null); }}>{localizedText(language, "pairs.m_ced646267c9def5d")}</Button>}
      <form className="regulatory-search-form" onSubmit={e => { e.preventDefault(); setRows(null); setError(null); setSearch(query.trim()); setOffset(0); setAttempt(v => v + 1); }}><EnterpriseToolbar><label>{presentationLabel(language, 'registerSearchDrafts')}<input type="search" maxLength={100} value={query} onChange={e => setQuery(e.target.value)}/></label><Button type="submit" variant="outline">{localizedText(language, "pairs.m_f9903f43e53b0bd3")}</Button></EnterpriseToolbar></form>
      {!rows && error === null && <p role="status">{localizedText(language, "pairs.m_c9914bd9caad7a7f")}</p>}
      {rows && <><ul className="regulatory-register-list">{rows.items.map(d => <li key={d.current.id}><a href={`/app/${kind}/${d.current.id}`} onClick={e => { e.preventDefault(); onNavigate(`/app/${kind}/${d.current.id}`); }}>{title(d)} · {localizedText(language, "pairs.m_5cf7feeda047e695")} {d.current.revision} · DRAFT</a></li>)}</ul>{!rows.items.length && <p>{presentationLabel(language, 'registerNoDrafts')}</p>}{pageButtons(rows, offset, n => { setRows(null); setOffset(n); })}</>}
    </> : <>
      {id && !draft ? error === null && <p role="status">{localizedText(language, "pairs.m_c9914bd9caad7a7f")}</p> : <form onSubmit={e => { e.preventDefault(); void save(); }}>
        {draft && <p>{draft.current.id} · DRAFT · {localizedText(language, "pairs.m_5cf7feeda047e695")} {draft.current.revision} · {localizedText(language, "pairs.m_99bf579926929ccb")}: {draft.current.savedAt}</p>}
        {kind === 'requirements' && id && <section className="requirement-lifecycle" aria-label={localizedText(language, "pairs.m_d07a98f15fab786c")}>
          <p role="status">{localizedText(language, "pairs.m_d07a98f15fab786c")}: {requirement === null ? localizedText(language, "pairs.m_c9914bd9caad7a7f") : requirement === 'unavailable' ? lifecycleLabel('UNAVAILABLE') : <>{lifecycleLabel(requirement.lifecycle)} · {localizedText(language, "pairs.m_5cf7feeda047e695")} {requirement.revision}</>}</p>
          {requirement !== null && requirement !== 'unavailable' && requirement.qualifications.length > 0 && <ul>{requirement.qualifications.map(q => <li key={`${q.ruleId}/${q.qualificationRevision}`}>{lifecycleLabel(q.lifecycle)} · {localizedText(language, "pairs.m_5cf7feeda047e695")} {q.requirementRevision} · {q.ruleId} / {q.ruleRevision} · {q.savedAt}</li>)}</ul>}
        </section>}
        <fieldset disabled={locked}><legend>{presentationLabel(language, 'registerFields')}</legend>
          {(Object.keys(service.fieldBounds) as FieldKey[]).filter(key => kind === 'sources' ? key !== 'instrument' : !['title', 'authority', 'locator'].includes(key)).map(key => <div className="profile-field" key={key}>
            <label htmlFor={`reg-${key}-state`}>{localizedTuple(labels[key], language, 0)} · {localizedText(language, "pairs.m_ac8b056f75c10eff")}</label><select id={`reg-${key}-state`} value={content[key].state} onChange={e => { const state = e.target.value as 'Missing' | 'Unknown' | 'Known'; setContent(v => ({ ...v, [key]: { state, value: state === 'Known' ? '' : null } })); setDirty(true); }}>
              <option value="Missing">{localizedText(language, "pairs.m_a5bd35d90de7c160")}</option><option value="Unknown">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option><option value="Known">{presentationLabel(language, 'registerKnownDeclared')}</option>
            </select>{content[key].state === 'Known' && <><label htmlFor={`reg-${key}`}>{localizedTuple(labels[key], language, 0)}</label><input id={`reg-${key}`} type={key === 'effectiveDate' ? 'date' : 'text'} maxLength={service.fieldBounds[key]} value={content[key].value ?? ''} onChange={e => { setContent(v => ({ ...v, [key]: { state: 'Known', value: e.target.value } })); setDirty(true); }}/></>}
          </div>)}
          {kind === 'requirements' && <section aria-label={presentationLabel(language, 'registerSourceCitation')}>
            <p>{presentationLabel(language, 'registerSelectedSource')}: {sourceId ?? localizedText(language, "pairs.m_a5bd35d90de7c160")} / {sourceRevision ?? localizedText(language, "pairs.m_a5bd35d90de7c160")}</p>
            <label>{presentationLabel(language, 'registerFindSource')}<input maxLength={100} value={sourceQuery} onChange={e => setSourceQuery(e.target.value)}/></label><Button variant="outline" type="button" onClick={() => { setSources(null); setSourceSearch(sourceQuery.trim()); setSourceOffset(0); setSourceAttempt(v => v + 1); }}>{presentationLabel(language, 'registerSearchSources')}</Button>
            <label htmlFor="reg-source">{presentationLabel(language, 'registerSourceDraft')}</label><select id="reg-source" value={sourceId ?? ''} onChange={e => { setSourceId(e.target.value || null); setSourceRevision(null); setSourceVersions(null); setSourceVersionOffset(0); setDirty(true); }}><option value="">{localizedText(language, "pairs.m_a5bd35d90de7c160")}</option>{sourceId && !sources?.items.some(d => d.current.id === sourceId) && <option value={sourceId}>{sourceId}</option>}{sources?.items.map(d => <option key={d.current.id} value={d.current.id}>{d.current.content.title.value ?? d.current.id}</option>)}</select>
            {pageButtons(sources, sourceOffset, n => { setSources(null); setSourceOffset(n); })}
            {sourceId && <><label htmlFor="reg-source-revision">{presentationLabel(language, 'registerExactRevision')}</label><select id="reg-source-revision" value={sourceRevision ?? ''} onChange={e => { setSourceRevision(e.target.value ? Number(e.target.value) : null); setDirty(true); }}><option value="">{localizedText(language, "pairs.m_a5bd35d90de7c160")}</option>{sourceRevision && !sourceVersions?.items.some(r => r.revision === sourceRevision) && <option value={sourceRevision}>{sourceRevision}</option>}{sourceVersions?.items.map(r => <option key={r.revision} value={r.revision}>{r.revision} · {r.content.amendment.value ?? 'DRAFT'} · {r.hash.slice(0, 12)}</option>)}</select>{pageButtons(sourceVersions, sourceVersionOffset, n => { setSourceVersions(null); setSourceVersionOffset(n); })}</>}
            {sourceVersions?.items.filter(r => r.revision === sourceRevision).map(r => <details key={r.revision}><summary>{presentationLabel(language, 'registerSelectedSourceContent')}</summary><dl>{Object.entries(r.content).map(([key, f]) => <div key={key}><dt>{localizedTuple(labels[key as FieldKey], language, 0)}</dt><dd>{f.state === 'Known' ? f.value : localizedText(language, f.state === 'Unknown' ? 'pairs.m_a5a7675bd7000dbd' : 'pairs.m_a5bd35d90de7c160')}</dd></div>)}</dl></details>)}
          </section>}
        </fieldset>
        {sourceError && <div role="alert">{presentationLabel(language, 'registerSourceUnavailable')}<Button type="button" onClick={() => { setSourceError(false); setSourceAttempt(v => v + 1); }}>{presentationLabel(language, 'registerRetrySources')}</Button></div>}
        {(dirty || frozen || conflict) && <p role="status">{presentationLabel(language, 'registerRetainedDraft')}</p>}
        <EnterpriseActions aria-label={presentationLabel(language, 'registerActions')}><Button variant="outline" type="button" disabled={saving} onClick={() => onNavigate(`/app/${kind}`)}>{presentationLabel(language, 'registerBack')}</Button>
        {editable && <Button disabled={saving || offline || conflict || denied} type="submit">{saving ? localizedText(language, "pairs.m_5b598bccc6931d54") : frozen ? presentationLabel(language, 'registerRetryCommand') : localizedText(language, "pairs.m_7e0cdba2e467dbec")}</Button>}</EnterpriseActions>
      </form>}
      {id && <section aria-label={localizedText(language, "pairs.m_d21b2e2163abcf4b")}><h2>{localizedText(language, "pairs.m_d21b2e2163abcf4b")}</h2>{historyError ? <div role="alert">{presentationLabel(language, 'registerHistoryUnavailable')}<Button onClick={() => { setHistoryError(false); setHistoryAttempt(v => v + 1); }}>{presentationLabel(language, 'registerRetryHistory')}</Button></div> : !history ? <p role="status">{localizedText(language, "pairs.m_9e04b8b6eafff876")}</p> : <>{history.items.map(r => <details key={r.revision}><summary>{localizedText(language, "pairs.m_5cf7feeda047e695")} {r.revision} · {r.savedAt} · {r.provenance}</summary><dl>{Object.entries(r.content).map(([key, f]) => <div key={key}><dt>{localizedTuple(labels[key as FieldKey], language, 0)}</dt><dd>{f.state === 'Known' ? f.value : f.state === 'Unknown' ? localizedText(language, "pairs.m_a5a7675bd7000dbd") : localizedText(language, "pairs.m_a5bd35d90de7c160")}</dd></div>)}<dt>{localizedText(language, "pairs.m_291f25245b3342d1")}</dt><dd>{r.sourceId ?? '—'} / {r.sourceRevision ?? '—'}</dd><dt>{localizedText(language, "pairs.m_be0dd17f19ef1dd5")}</dt><dd className="register-hash">{r.hash}</dd></dl></details>)}{pageButtons(history, historyOffset, n => { setHistory(null); setHistoryOffset(n); })}</>}</section>}
    </>}
  </section>;
}
