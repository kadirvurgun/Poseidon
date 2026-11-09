import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import { readCatalog, saveCatalog, type Catalog, type CatalogCommand } from './crew-catalog-api';
// Mounted with the actual session/context key by the owning workspace. No persistence of proposal text.
export function CrewCatalog({ vessel, language, csrf, onDirty, onChanged }: {
    vessel: string;
    language: AppLocale;
    csrf: string;
    onDirty: (dirty: boolean) => void;
    onChanged: () => void;
}) {
    const en = language === 'en', [data, setData] = useState<Catalog | null>(null), [error, setError] = useState(0);
    const [generation, reload] = useState(0), [reason, setReason] = useState(''), [proposal, setProposal] = useState('');
    const [state, setState] = useState<'idle' | 'saving' | 'uncertain' | 'conflict' | 'invalid'>('idle');
    const frozen = useRef<{
        key: string;
        command: CatalogCommand;
    } | null>(null), pending = useRef(false), live = useRef(true);
    const controller = useRef<AbortController | null>(null);
    const [attemptState, setAttemptState] = useState<CatalogCommand['state'] | null>(null);
    useEffect(() => { live.current = true; return () => { live.current = false; controller.current?.abort(); }; }, []);
    useEffect(() => { onDirty(reason.length > 0 || proposal.length > 0 || state === 'uncertain' || state === 'saving'); return () => onDirty(false); }, [reason, proposal, state, onDirty]);
    const resetScope1 = [vessel, generation] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], generation)) {
        setPreviousScope1(resetScope1);
        setData(null);
        setError(0);
    }
    useEffect(() => {
        const c = new AbortController();
        readCatalog(vessel, c.signal).then(v => { if (!c.signal.aborted)
            setData(v); }).catch(e => { if (!c.signal.aborted)
            setError(e instanceof ApiError ? e.status : 503); });
        return () => c.abort();
    }, [vessel, generation]);
    async function save(action: Catalog['actions'][number]) {
        if (pending.current || !data || !navigator.onLine)
            return;
        if (!frozen.current) {
            if (!reason.trim()) {
                setState('invalid');
                return;
            }
            const state = { draft: 'Draft', review: 'Reviewed', adopt: 'Adopted', withdraw: 'Withdrawn' }[action.action] as CatalogCommand['state'];
            let content;
            try {
                content = action.action === 'draft' ? JSON.parse(proposal) : undefined;
            }
            catch {
                setState('invalid');
                return;
            }
            frozen.current = { key: crypto.randomUUID(), command: { expectedVersion: data.version, authorityId: action.authorityId, state, reason, ...(content ? { content } : {}) } };
        }
        setAttemptState(frozen.current.command.state);
        pending.current = true;
        setState('saving');
        const c = new AbortController();
        controller.current = c;
        try {
            await saveCatalog(vessel, frozen.current.command, frozen.current.key, csrf, c.signal);
            if (!live.current || c.signal.aborted)
                return;
            frozen.current = null;
            setReason('');
            setProposal('');
            setState('idle');
            reload(v => v + 1);
            onChanged();
        }
        catch (e) {
            if (!live.current || c.signal.aborted)
                return;
            if (e instanceof ApiError && [400, 403, 404, 409, 401].includes(e.status)) {
                frozen.current = null;
                setState(e.status === 409 ? 'conflict' : 'invalid');
                setError(e.status);
                if ([401, 403, 404].includes(e.status)) {
                    setData(null);
                    setReason('');
                    setProposal('');
                }
            }
            else
                setState('uncertain');
        }
        finally {
            pending.current = false;
        }
    }
    return <section aria-label={localizedText(en, "pairs.m_2f23ca7b95b2cfde")}><h2>{localizedText(en, "pairs.m_2f23ca7b95b2cfde")}</h2>
    <p>{localizedText(en, "pairs.m_4f3e7b0d74078461")}</p>
    {!data && !error && <p role="status">{localizedText(en, "pairs.m_80835e539d338514")}</p>}
    {error > 0 && <p role="alert">{[401, 403, 404].includes(error) ? (localizedText(en, "pairs.m_2b0b32dbe0648f68")) : (localizedText(en, "pairs.m_82a27fa52a81253b"))}</p>}
    {data && <><p>{localizedText(en, "pairs.m_bf38873d310ce8df")}: {data.activeRevision ?? (localizedText(en, "pairs.m_48bc5d350746563d"))}</p>
      <ol>{data.items.map(e => <li key={e.revision}>{e.revision} · {e.state} · {e.policyVersion} · {e.savedAt}<p>{e.reason}</p></li>)}</ol>
      {data.actions.length > 0 && <><fieldset disabled={state === 'saving' || state === 'uncertain' || state === 'conflict'}>
        <label>{localizedText(en, "pairs.m_540d9fd15a75d9b6")}<textarea maxLength={1000} value={reason} onChange={e => setReason(e.target.value)}/></label>
        {data.actions.some(a => a.action === 'draft') && <label>{localizedText(en, "pairs.m_4f7cc98e3e5e4e47")}<textarea value={proposal} onChange={e => setProposal(e.target.value)}/></label>}
      </fieldset>{data.actions.map(a => <button key={a.action} disabled={state === 'saving' || state === 'conflict' || !navigator.onLine || state === 'uncertain' && attemptState !== ({ draft: 'Draft', review: 'Reviewed', adopt: 'Adopted', withdraw: 'Withdrawn' }[a.action])} onClick={() => void save(a)}>{state === 'uncertain' ? (localizedText(en, "pairs.m_cffad542755a2b2b")) : ({ draft: localizedText(en, "pairs.m_95a75b787befee0a"), review: localizedText(en, "pairs.m_8d9287a2cb7e153e"), adopt: localizedText(en, "pairs.m_d1df3f5ee15b1ce3"), withdraw: localizedText(en, "pairs.m_3e499a4460c13e6c") }[a.action])}</button>)}</>}
    </>}
    {state === 'uncertain' && <p role="alert">{localizedText(en, "pairs.m_0587138ba8832d4f")}</p>}
    {state === 'invalid' && <p role="alert">{localizedText(en, "pairs.m_2c28ac1de401e724")}</p>}
    <button disabled={state === 'saving' || state === 'uncertain'} onClick={() => { frozen.current = null; setReason(''); setProposal(''); setState('idle'); reload(v => v + 1); }}>{localizedText(en, "pairs.m_eeb7441647d948b6")}</button>
  </section>;
}
