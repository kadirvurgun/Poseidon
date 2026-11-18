import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useMemo, useRef, useState } from 'react';
import { documentRevision, documents, saveDocument, type DocumentDraft, type DocumentPage } from './document-api';
import { VesselFiles } from './VesselFiles';
export function VesselDocuments({ vessel, language, csrf, onDirty, onSaving }: {
    vessel: string;
    language: AppLocale;
    csrf: string;
    onDirty: (v: boolean) => void;
    onSaving: (v: boolean) => void;
}) {
    const en = language === 'en';
    const [offset, setOffset] = useState(0);
    const [reload, setReload] = useState(0);
    const [loaded, setLoaded] = useState<{
        key: string;
        page: DocumentPage;
    } | null>(null);
    const [selection, setSelection] = useState<{
        key: string;
        row: DocumentDraft;
    } | null>(null);
    const [title, setTitle] = useState('');
    const [error, setError] = useState(false);
    const [busy, setBusy] = useState(false);
    const [editing, setEditing] = useState(false);
    const [attachmentDirty, setAttachmentDirty] = useState(false);
    const [attachmentBusy, setAttachmentBusy] = useState(false);
    const [attempt, setAttempt] = useState<{
        key: string;
        title: string;
        current?: DocumentDraft;
    } | null>(null);
    const c = useRef<AbortController | null>(null);
    const key = `${vessel}:${offset}:${reload}`;
    const page = loaded?.key === key ? loaded.page : null;
    const selected = selection?.key === key ? selection.row : null;
    const owner = useMemo(() => selected ? { id: selected.id, revision: selected.revision } : undefined, [selected]);
    const locked = busy || attachmentBusy || attachmentDirty || editing || !!attempt;
    useEffect(() => {
        const request = new AbortController();
        let active = true;
        documents(vessel, offset, request.signal).then(value => { if (active) {
            setLoaded({ key, page: value });
            setSelection(null);
            setError(false);
        } }).catch(() => { if (active && !request.signal.aborted)
            setError(true); });
        return () => { active = false; request.abort(); };
    }, [vessel, offset, reload, key]);
    useEffect(() => () => c.current?.abort(), []);
    const save = async () => {
        if (busy || !page?.canWrite)
            return;
        const frozen = attempt ?? { key: crypto.randomUUID(), title, current: selected ?? undefined };
        setAttempt(frozen);
        setBusy(true);
        onSaving(true);
        setError(false);
        const request = new AbortController();
        c.current = request;
        try {
            const saved = await saveDocument(vessel, frozen.title, frozen.key, csrf, request.signal, frozen.current);
            setSelection({ key, row: saved });
            setAttempt(null);
            setEditing(false);
            onDirty(false);
            setLoaded(previous => previous?.key === key ? { key, page: { ...previous.page, items: [saved, ...previous.page.items.filter(x => x.id !== saved.id)].slice(0, 25) } } : previous);
        }
        catch {
            setError(true);
        }
        finally {
            setBusy(false);
            onSaving(false);
        }
    };
    return <section aria-label={localizedText(en, "pairs.m_e9c1e56ebf003d9d")}>
    <h2>{localizedText(en, "pairs.m_e9c1e56ebf003d9d")}</h2>
    <p>{localizedText(en, "pairs.m_c7251d0a2b18c3d7")}</p>
    {error && <p role="alert">{localizedText(en, "pairs.m_13c843094c7b7dad")}</p>}
    {!page && !error && <p role="status">{localizedText(en, "pairs.m_ddb2e3e267bdd045")}</p>}
    <button disabled={locked} onClick={() => setReload(x => x + 1)}>{localizedText(en, "pairs.m_92f5248c32a49c54")}</button>
    {page && <>
      {page.items.length === 0 && <p>{localizedText(en, "pairs.m_e31bc72f9eba0bcf")}</p>}
      <ul>{page.items.map(row => <li key={row.id}><button disabled={locked} onClick={() => { setSelection({ key, row }); setTitle(row.title ?? ''); }}>{row.title || (localizedText(en, "pairs.m_fc0743be647c8ad7"))}</button> · {localizedText(en, "pairs.m_2b696768fd48ba86")} · {row.revision}</li>)}</ul>
      <button disabled={locked || offset === 0} onClick={() => setOffset(x => Math.max(0, x - 25))}>{localizedText(en, "pairs.m_dced3f4e90cde0ce")}</button>
      <button disabled={locked || !page.hasMore} onClick={() => setOffset(x => x + 25)}>{localizedText(en, "pairs.m_382d25d9f068a66f")}</button>
      {page.canWrite && <>
        <button disabled={locked} onClick={() => { setSelection(null); setTitle(''); setEditing(true); onDirty(true); }}>{localizedText(en, "pairs.m_2b5534c53533dedd")}</button>
        {selected && <button disabled={locked} onClick={() => { setTitle(selected.title ?? ''); setEditing(true); onDirty(true); }}>{localizedText(en, "pairs.m_f60f2e5fb8b9965d")}</button>}
      </>}
      {(editing || attempt) && <form onSubmit={e => { e.preventDefault(); void save(); }}>
        <label>{localizedText(en, "pairs.m_4886ca26929a09e6")}<input maxLength={200} value={title} disabled={!!attempt || busy} onChange={e => setTitle(e.target.value)}/></label>
        <button disabled={busy}>{attempt ? (localizedText(en, "pairs.m_b226a56a95b28790")) : (localizedText(en, "pairs.m_1057f80f39b6b640"))}</button>
        <button type="button" disabled={busy} onClick={() => { setAttempt(null); setEditing(false); onDirty(false); }}>{localizedText(en, "pairs.m_3ad2ed104154a7a5")}</button>
      </form>}
      {selected && !editing && !attempt && <article aria-label={localizedText(en, "pairs.m_1ba81bb163b24c4d")}>
        <h3>{selected.title || (localizedText(en, "pairs.m_fc0743be647c8ad7"))}</h3><p>{localizedText(en, "pairs.m_9063738166a73fc0")} {selected.revision} · {localizedText(en, "pairs.m_b980704b1fa89cd9")}</p>
        {selected.revision > 1 && <button disabled={locked} onClick={async () => { const request = new AbortController(); c.current = request; setBusy(true); try {
                const row = await documentRevision(vessel, selected.id, selected.revision - 1, request.signal);
                setSelection({ key, row });
            }
            catch {
                setError(true);
            }
            finally {
                setBusy(false);
            } }}>{localizedText(en, "pairs.m_a11eb50d2eed1aae")}</button>}
        {owner && <VesselFiles key={`${owner.id}:${owner.revision}`} vessel={vessel} owner={owner} language={language} csrf={csrf} onDirty={v => { setAttachmentDirty(v); onDirty(v); }} onSaving={v => { setAttachmentBusy(v); onSaving(v); }}/>}
      </article>}
    </>}
  </section>;
}
