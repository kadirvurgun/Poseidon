import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { ApiError } from './api';
import { useEffectEvent, useEffect, useRef, useState } from 'react';
import { downloadFile, fileDetail, filePage, stageFile, uploadFile, mimeForName, type FileOwner, type FileAttempt, type FilePage, type FileRecord } from './file-api';
export function VesselFiles({ vessel, language, csrf, onDirty, onSaving, owner, onCommitted, offline = false }: {
    offline?: boolean;
    owner?: FileOwner;
    onCommitted?: () => void;
    vessel: string;
    language: AppLocale;
    csrf: string;
    onDirty: (v: boolean) => void;
    onSaving: (v: boolean) => void;
}) {
    const personal = owner && 'type' in owner ? owner : null;
    const en = language === 'en';
    const stateLabel = (state: FileRecord['state']) => ({ Pending: localizedText(en, "pairs.m_4968ee13c2d616dc"), Receiving: localizedText(en, "pairs.m_24f12fc5f8493db3"), CleanTransport: localizedText(en, "pairs.m_56746d61651f76af"), Abandoned: localizedText(en, "pairs.m_2d48e847fc09e31b"), SyntheticUnverified: localizedText(en, "pairs.m_73f0c18c876933d8") })[state];
    const [loaded, setLoaded] = useState<{
        key: string;
        page: FilePage;
    } | null>(null);
    const [offset, setOffset] = useState(0);
    const [reload, setReload] = useState(0);
    const [error, setError] = useState(false);
    const [file, setFile] = useState<File | null>(null);
    const attempt = useRef<FileAttempt | null>(null);
    const [frozen, setFrozen] = useState(false);
    const [busy, setBusy] = useState(false);
    const [progress, setProgress] = useState(0);
    const [selection, setSelection] = useState<{
        key: string;
        row: FileRecord;
    } | null>(null);
    const [notice, setNotice] = useState('');
    const controller = useRef<AbortController | null>(null);
    const pageKey = `${vessel}:${JSON.stringify(owner)}:${offset}:${reload}`;
    const selected = selection?.key === pageKey ? selection.row : null;
    const setSelected = (row: FileRecord | null) => setSelection(row ? { key: pageKey, row } : null);
    const page = loaded?.key === pageKey ? loaded.page : null;
    const denied = (error: unknown) => {
        if (error instanceof ApiError && [401, 403, 404].includes(error.status)) {
            setLoaded(null);
            setSelection(null);
            setFile(null);
            attempt.current = null;
            setFrozen(false);
            setNotice('');
            onDirty(false);
        }
    };
    const deniedInEffect = useEffectEvent(denied);
    useEffect(() => { if (offline)
        return; const c = new AbortController(); let active = true; filePage(vessel, offset, c.signal, owner).then(value => { if (active) {
        setLoaded({ key: pageKey, page: value });
        setSelection(null);
        setError(false);
    } }).catch(e => { if (active && !c.signal.aborted) {
        deniedInEffect(e);
        setError(true);
    } }); return () => { active = false; c.abort(); }; }, [vessel, owner, offset, reload, pageKey, offline]);
    useEffect(() => () => controller.current?.abort(), []);
    useEffect(() => { if (offline)
        controller.current?.abort(); }, [offline]);
    const start = async () => {
        if (offline || !file || busy || !page?.available || !page.canWrite)
            return;
        setBusy(true);
        onSaving(true);
        setError(false);
        setProgress(0);
        const c = new AbortController();
        controller.current = c;
        try {
            if (!attempt.current) {
                if (file.name.normalize('NFC') !== file.name || file.name.length > 120 || file.size === 0 || file.size > page.maxBytes || !page.allowedTypes.includes(file.type) || mimeForName(file.name) !== file.type || !/^[\p{L}\p{N}_. -]+\.(png|jpe?g|pdf)$(?![\s\S])/iu.test(file.name) || (file.name.match(/\./g)?.length ?? 0) !== 1)
                    throw Error();
                const checksum = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', await file.arrayBuffer())), x => x.toString(16).padStart(2, '0')).join('').toUpperCase();
                attempt.current = { key: crypto.randomUUID(), file, checksum };
                setFrozen(true);
            }
            const staged = await stageFile(vessel, attempt.current, csrf, c.signal, owner);
            const row = staged.state === 'Pending' ? await uploadFile(vessel, staged.id, attempt.current.file, csrf, c.signal, setProgress, owner) : await fileDetail(vessel, staged.id, c.signal, owner);
            if (c.signal.aborted)
                return;
            setSelected(row);
            if (row.state !== 'CleanTransport') {
                setNotice(localizedText(en, "pairs.m_377a2e84300ffc70"));
                return;
            }
            attempt.current = null;
            setFile(null);
            setFrozen(false);
            onDirty(false);
            setNotice(localizedText(en, "pairs.m_f8a03d6801f871a1"));
            setReload(x => x + 1);
            onCommitted?.();
        }
        catch (e) {
            if (c.signal.aborted)
                return;
            denied(e);
            setError(true);
            setNotice(localizedText(en, "pairs.m_a3c08cc10a039f76"));
        }
        finally {
            setBusy(false);
            onSaving(false);
        }
    };
    const download = async () => {
        if (offline || !selected)
            return;
        setBusy(true);
        onSaving(true);
        const c = new AbortController();
        controller.current = c;
        try {
            const blob = await downloadFile(vessel, selected, csrf, c.signal, owner);
            if (c.signal.aborted)
                return;
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = selected.name;
            a.click();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        }
        catch (e) {
            if (!c.signal.aborted) {
                denied(e);
                setError(true);
            }
        }
        finally {
            setBusy(false);
            onSaving(false);
        }
    };
    return <section className="vessel-files" aria-label={personal ? (localizedText(en, "pairs.m_b62faa8eace32292")) : (localizedText(en, "pairs.m_cf579e19e2fbe540"))}>
    <h2>{personal ? (localizedText(en, "pairs.m_b62faa8eace32292")) : (localizedText(en, "pairs.m_cf579e19e2fbe540"))}</h2>
    <p>{localizedText(en, "pairs.m_7cc4e0a444bd1bc8")}</p>
    {offline && <p role="status">{localizedText(en, "pairs.m_caa83b468ffd9d46")}</p>}
    {error && <p role="alert">{localizedText(en, "pairs.m_4d013976b23bc0f5")}</p>}
    {notice && <p role="status">{notice}</p>}
    {!page && !error && <p role="status">{localizedText(en, "pairs.m_0c4d53b12b542c98")}</p>}
    {!busy && <button disabled={frozen || offline} onClick={() => setReload(x => x + 1)}>{localizedText(en, "pairs.m_c22142c3b891c66d")}</button>}
    {page && <>
      {!page.available && <p role="status">{localizedText(en, "pairs.m_503fb2ae3993e500")}</p>}
      {page.canWrite && <form onSubmit={e => { e.preventDefault(); void start(); }}>
        <label>{localizedText(en, "pairs.m_5189ff9d38e64637")} <input type="file" accept={page.allowedTypes.join(',')} disabled={offline || frozen || busy || !page.available} onChange={e => { setFile(e.target.files?.[0] ?? null); onDirty(!!e.target.files?.length); }}/></label>
        <p>{localizedText(en, "pairs.m_900d9d93578c8d87")} {page.allowedTypes.join(', ')} · {page.maxBytes}</p>
        <button disabled={offline || !file || busy || !page.available}>{frozen ? (localizedText(en, "pairs.m_1e32349e01e7e112")) : (localizedText(en, "pairs.m_1d3398159be3248f"))}</button>
        {busy && <><progress aria-label={localizedText(en, "pairs.m_08436671f27d9957")} max={100} value={progress}/><button type="button" onClick={() => controller.current?.abort()}>{localizedText(en, "pairs.m_420b03a10ed6b931")}</button></>}
        {!busy && (file || frozen) && <button type="button" onClick={() => { attempt.current = null; setFrozen(false); setFile(null); onDirty(false); setNotice(localizedText(en, "pairs.m_68f21ddba52eaa07")); }}>{localizedText(en, "pairs.m_b1e4adc501088705")}</button>}
      </form>}
      {page.items.length === 0 ? <p>{localizedText(en, "pairs.m_393b1cd2a6db17d7")}</p> : <ul>{page.items.map(row => <li key={row.id}><button disabled={offline || busy || frozen} onClick={async () => { controller.current?.abort(); const c = new AbortController(); controller.current = c; try {
            const detail = await fileDetail(vessel, row.id, c.signal, owner);
            if (!c.signal.aborted)
                setSelected(detail);
        }
        catch (e) {
            if (!c.signal.aborted) {
                denied(e);
                setSelected(null);
                setError(true);
            }
        } }}>{row.name}</button> · {stateLabel(row.state)}</li>)}</ul>}
      <button disabled={offline || offset === 0 || busy || frozen} onClick={() => setOffset(x => Math.max(0, x - 25))}>{localizedText(en, "pairs.m_8c0a2e5c9d161c34")}</button><button disabled={offline || !page.hasMore || busy || frozen} onClick={() => setOffset(x => x + 25)}>{localizedText(en, "pairs.m_9e1a256dbab8df8f")}</button>
    </>}
    {selected && <article aria-label={localizedText(en, "pairs.m_06974cdad22193c2")}><h3>{selected.name}</h3><dl><dt>{localizedText(en, "pairs.m_9e5bece03749f18e")}</dt><dd>{stateLabel(selected.state)}</dd><dt>SHA-256</dt><dd>{selected.checksum ?? '—'}</dd><dt>{localizedText(en, "pairs.m_9063738166a73fc0")}</dt><dd>{selected.revision ?? '—'}</dd></dl>{selected.state === 'CleanTransport' && <button disabled={offline || busy || !page?.available} onClick={() => void download()}>{localizedText(en, "pairs.m_4d37c7c916863368")}</button>}</article>}
  </section>;
}
