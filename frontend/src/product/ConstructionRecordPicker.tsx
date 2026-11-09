import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useLayoutEffect, useEffect, useRef, useState } from 'react';
import { ApiError } from './api';
import { filePage, type FileRecord } from './file-api';
import type { ProfileContent } from './profile-contract';
// Ordinary vessel files only. The server independently excludes person/document stores.
export function ConstructionRecordPicker({ vessel, language, value, disabled, onChange, onDenied }: {
    vessel: string;
    language: AppLocale;
    value: ProfileContent['constructionRecord'];
    disabled: boolean;
    onChange: (v: ProfileContent['constructionRecord']) => void;
    onDenied: (error: ApiError) => unknown;
}) {
    const [rows, setRows] = useState<FileRecord[]>([]), [offset, setOffset] = useState(0), [more, setMore] = useState(false);
    const [loading, setLoading] = useState(true), [failure, setFailure] = useState(false), [attempt, setAttempt] = useState(0);
    const [offline, setOffline] = useState(!navigator.onLine);
    const selected = useRef(value);
    useLayoutEffect(() => { selected.current = value; }, [value]);
    useEffect(() => {
        const online = () => setOffline(false), offline = () => setOffline(true);
        window.addEventListener('online', online);
        window.addEventListener('offline', offline);
        return () => { window.removeEventListener('online', online); window.removeEventListener('offline', offline); };
    }, []);
    const resetScope1 = [vessel, offset, attempt, offline, onDenied] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], offset) || !Object.is(previousScope1[2], attempt) || !Object.is(previousScope1[3], offline) || !Object.is(previousScope1[4], onDenied)) {
        setPreviousScope1(resetScope1);
        setRows([]);
        setMore(false);
        setFailure(false);
        setLoading(!offline);
    }
    useEffect(() => {
        if (offline)
            return;
        const controller = new AbortController();
        let active = true;
        const timer = setTimeout(() => controller.abort(), 10000);
        filePage(vessel, offset, controller.signal).then(page => {
            if (active) {
                setRows(page.items.filter(f => f.state === 'CleanTransport'));
                setMore(page.hasMore);
                setFailure(!page.available);
            }
        }).catch(error => {
            if (active) {
                setFailure(true);
                if (error instanceof ApiError && (error.status === 401 || selected.current && [403, 404].includes(error.status)))
                    onDenied(error);
            }
        }).finally(() => {
            if (active)
                setLoading(false);
        });
        return () => { active = false; controller.abort(); clearTimeout(timer); };
    }, [vessel, offset, attempt, offline, onDenied]);
    return <fieldset disabled={disabled || loading || offline}>
    <legend>{localizedText(language, "pairs.m_a63f84ca7e9e2df2")}</legend>
    <p>{localizedText(language, "pairs.m_9b08f8570085767c")}</p>
    {loading && <p role="status">{localizedText(language, "pairs.m_5da1602700598574")}</p>}
    {offline && <p role="status">{localizedText(language, "pairs.m_769f6d16b435b390")}</p>}
    {failure && <p role="alert">{localizedText(language, "pairs.m_e64301a7a5754e14")}</p>}
    {!loading && !failure && !offline && rows.length === 0 && <p>{localizedText(language, "pairs.m_8e23a27b7665c867")}</p>}
    <label>{localizedText(language, "pairs.m_c216bb5fc6c66f2d")}<select disabled={failure} value={value?.id ?? ''} onChange={e => {
            const file = rows.find(f => f.id === e.target.value);
            onChange(file?.revision && file.checksum ? { id: file.id, revision: file.revision, checksum: file.checksum } : null);
        }}><option value="">{localizedText(language, "pairs.m_7a24cfdd2944d39a")}</option>
      {value && !rows.some(f => f.id === value.id) && <option value={value.id}>{value.id} / {value.revision}</option>}
      {rows.map(f => <option key={f.id} value={f.id}>{f.name} / {f.revision}</option>)}</select></label>
    <button type="button" disabled={failure || offset === 0} onClick={() => setOffset(n => n - 25)}>{localizedText(language, "pairs.m_4935c0022d8aa09c")}</button>
    <button type="button" disabled={failure || !more} onClick={() => setOffset(n => n + 25)}>{localizedText(language, "pairs.m_b31fa61683841135")}</button>
    <button type="button" onClick={() => setAttempt(n => n + 1)}>{localizedText(language, "pairs.m_b4c7f9ff1a95577b")}</button>
  </fieldset>;
}
