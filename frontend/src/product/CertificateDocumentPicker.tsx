import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useState } from 'react';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePagination } from './EnterprisePatterns';
import { filePage, type FilePage } from './file-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
export type CertificateDocumentPin = {
    id: string;
    revision: number;
    checksum: string;
};
export function CertificateDocumentPicker({ vessel, owner, value, language, disabled, onChange }: {
    vessel: string;
    owner: string;
    value: CertificateDocumentPin | null;
    language: AppLocale;
    disabled: boolean;
    onChange: (value: CertificateDocumentPin | null) => void;
}) {
    const en = language === 'en', epoch = useWorkspaceRevalidation(), [offset, setOffset] = useState(0), [attempt, retry] = useState(0), [result, setResult] = useState<{
        scope: string;
        page?: FilePage;
        failed?: boolean;
    } | null>(null);
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = `${owner}/${vessel}/${offset}/${attempt}/${epoch}`;
    useEffect(() => {
        if (!online || !visible)
            return;
        const c = new AbortController();
        let active = true;
        const timer = setTimeout(() => c.abort(), 10000);
        filePage(vessel, offset, c.signal).then(page => { if (active)
            setResult({ scope, page }); }).catch(() => { if (active)
            setResult({ scope, failed: true }); }).finally(() => clearTimeout(timer));
        return () => { active = false; c.abort(); clearTimeout(timer); };
    }, [scope, vessel, offset, online, visible]);
    const current = result?.scope === scope && online && visible ? result : null, page = current?.page, rows = page?.items.filter(row => row.state === 'CleanTransport') ?? [], available = !!page?.available && !current?.failed;
    return <section className="certificate-document-picker" aria-label={localizedText(en, "pairs.m_94c711bab985b142")}><h3>{localizedText(en, "pairs.m_58e93b23f34a521c")}</h3><p>{localizedText(en, "pairs.m_486bfd964e33f30a")}</p>{!online || !visible ? <p role="status">{localizedText(en, "pairs.m_626015c25c9a96d9")}</p> : !current ? <p role="status">{localizedText(en, "pairs.m_b3a6a8835283b5ad")}</p> : !available ? <p role="alert">{localizedText(en, "pairs.m_dd51ceb3acad3094")}</p> : !rows.length ? <p>{localizedText(en, "pairs.m_ad4ce29854b192e6")}</p> : null}<label>{localizedText(en, "pairs.m_6e02f9cf3e540a24")}<select disabled={disabled || !online || !visible || !available} value={value?.id ?? ''} onChange={event => { const file = rows.find(row => row.id === event.target.value); onChange(file?.revision && file.checksum ? { id: file.id, revision: file.revision, checksum: file.checksum } : null); }}><option value="">{localizedText(en, "pairs.m_9aa28c27df900212")}</option>{value && !rows.some(row => row.id === value.id) && <option disabled value={value.id}>{localizedText(en, "pairs.m_9fa21a8acc5e44af")} · {localizedText(en, "pairs.m_58bb82efd11362a3")} {value.revision}</option>}{rows.map(file => <option key={file.id} value={file.id}>{file.name} · {localizedText(en, "pairs.m_58bb82efd11362a3")} {file.revision}</option>)}</select></label>{value && <details><summary>{localizedText(en, "pairs.m_f327aea6edbfae93")}</summary><p>{value.id} / {value.revision}</p><code>{value.checksum}</code></details>}<EnterprisePagination label={localizedText(en, "pairs.m_43346dcbeca4dade")} previous={localizedText(en, "pairs.m_4432d36f96a80dbd")} next={localizedText(en, "pairs.m_6c7c6f27b9fab8d6")} previousDisabled={!offset || disabled || !available} nextDisabled={!page?.hasMore || disabled || !available} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)}/><EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_34e33c2da7d32b77")}><Button variant="outline" disabled={!online || disabled} onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_e3486e5436e0d57b")}</Button></EnterpriseActions></section>;
}
