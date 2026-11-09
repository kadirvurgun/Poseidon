import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useState } from 'react';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterprisePagination } from './EnterprisePatterns';
import { certificatesApi, type CertificatePage } from './certificates-api';
import { useWorkspaceRevalidation } from './use-workspace-revalidation';
export type CertificateReplacementPin = {
    id: string;
    revision: number;
    version: number;
};
export function CertificateReplacementPicker({ vessel, owner, originalId, value, language, disabled, onChange }: {
    vessel: string;
    owner: string;
    originalId: string;
    value: CertificateReplacementPin | null;
    language: AppLocale;
    disabled: boolean;
    onChange: (pin: CertificateReplacementPin | null) => void;
}) {
    const en = language === 'en', epoch = useWorkspaceRevalidation(), [offset, setOffset] = useState(0), [attempt, retry] = useState(0), [result, setResult] = useState<{
        scope: string;
        page?: CertificatePage;
        failed?: boolean;
    } | null>(null);
    const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = `${owner}/${vessel}/${offset}/${attempt}/${epoch}`;
    useEffect(() => { if (!online || !visible)
        return; const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000); certificatesApi.center(vessel, 'Vessel', offset, null, null, c.signal).then(page => { if (active)
        setResult({ scope, page }); }).catch(() => { if (active)
        setResult({ scope, failed: true }); }).finally(() => clearTimeout(timer)); return () => { active = false; c.abort(); clearTimeout(timer); }; }, [scope, vessel, offset, online, visible]);
    const current = result?.scope === scope && online && visible ? result : null, rows = current?.page?.items.filter(row => row.scope === 'Vessel' && row.id !== originalId && row.currentRevision && row.state !== 'SUPERSEDED') ?? [];
    return <section className="certificate-document-picker" aria-label={localizedText(en, "pairs.m_7c3b0fe70b22d4cb")}><h3>{localizedText(en, "pairs.m_d45c9bc57022f4d6")}</h3><p>{localizedText(en, "pairs.m_f0264cc9b9fd621e")}</p>{!online || !visible ? <p role="status">{localizedText(en, "pairs.m_7bbff4fb3f3a4c98")}</p> : !current ? <p role="status">{localizedText(en, "pairs.m_5550f64b4c47d696")}</p> : current.failed ? <p role="alert">{localizedText(en, "pairs.m_985daf77942c2221")}</p> : !rows.length ? <p>{localizedText(en, "pairs.m_9c9143abc9a2deb9")}</p> : null}<label>{localizedText(en, "pairs.m_cd2023d86ce35c62")}<select disabled={disabled || !current?.page || !online || !visible} value={value?.id ?? ''} onChange={event => { const row = rows.find(candidate => candidate.id === event.target.value); onChange(row ? { id: row.id, revision: row.revision, version: row.version } : null); }}><option value="">{localizedText(en, "pairs.m_004c49c6653cc70b")}</option>{value && !rows.some(row => row.id === value.id) && <option disabled value={value.id}>{localizedText(en, "pairs.m_2b5d780f3ee79378")} {value.revision}</option>}{rows.map(row => <option key={row.id} value={row.id}>{row.content.documentName ?? row.content.metadata.declaration.certificateNumber ?? (localizedText(en, "pairs.m_c40a21c926ace158"))} · r{row.revision}</option>)}</select></label><EnterprisePagination label={localizedText(en, "pairs.m_5ff938fe9b0cbf9d")} previous={localizedText(en, "pairs.m_44391239c783db17")} next={localizedText(en, "pairs.m_3e16bdf375740c8b")} previousDisabled={!offset || disabled || !!current?.failed} nextDisabled={!current?.page?.hasMore || disabled} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)}/><EnterpriseActions align="start"><Button variant="outline" disabled={!online || disabled} onClick={() => retry(n => n + 1)}>{localizedText(en, "pairs.m_04887d16cdebaddc")}</Button></EnterpriseActions></section>;
}
