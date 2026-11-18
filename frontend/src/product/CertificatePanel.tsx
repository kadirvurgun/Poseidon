import { localizedText } from "../i18n/messages";
import { presentationLabel } from '../i18n/labels'
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useRef, useState } from 'react';
import { ApiError, request } from './api';
import { crewFileVersions, type CrewFileVersion } from './crew-files-api';
import { label } from './import-api';
type Declaration = {
    certificateTypeReference: string | null;
    certificateNumber: string | null;
    issuer: string | null;
    issueDate: string | null;
    expiryDate: string | null;
    limitations: string | null;
};
type Metadata = {
    declaration: Declaration;
    issuingCountryNumeric: string | null;
    endorsementRequired: boolean | null;
    endorsementNumber: string | null;
    endorsementExpiry: string | null;
    stcwReference: string | null;
    tankerRelevance: string | null;
    vesselTypeScope: string | null;
    documentReference: string | null;
    renewalLeadDaysNumeric: string | null;
    sourceSystem: string | null;
    notes: string | null;
};
type Credential = {
    id: string;
    version: number;
    revision: number;
    metadata: Metadata;
    verificationState: string;
    provenance: string;
    lookupState: string;
    referenceDate: string;
    dates: {
        issue: string | number;
        expiry: string | number;
        order: string | number;
    };
};
const blank = (): Metadata => ({ declaration: { certificateTypeReference: null, certificateNumber: null, issuer: null, issueDate: null, expiryDate: null, limitations: null },
    issuingCountryNumeric: null, endorsementRequired: null, endorsementNumber: null, endorsementExpiry: null, stcwReference: null, tankerRelevance: null,
    vesselTypeScope: null, documentReference: null, renewalLeadDaysNumeric: null, sourceSystem: null, notes: null });
const declarationFields: [
    keyof Declaration,
    string,
    string
][] = [['certificateTypeReference', 'certificate_type', 'text'], ['certificateNumber', 'certificate_no', 'text'], ['issuer', 'issuer', 'text'], ['issueDate', 'issue_date', 'date'], ['expiryDate', 'expiry_date', 'date'], ['limitations', 'restriction', 'text']];
const fields: [
    Exclude<keyof Metadata, 'declaration' | 'endorsementRequired'>,
    string,
    string
][] = [['issuingCountryNumeric', 'issuing_country', 'text'], ['endorsementNumber', 'endorsement_no', 'text'], ['endorsementExpiry', 'endorsement_expiry', 'date'], ['stcwReference', 'stcw_reference', 'text'], ['tankerRelevance', 'tanker_relevance', 'text'], ['vesselTypeScope', 'vessel_type_scope', 'text'], ['renewalLeadDaysNumeric', 'renewal_lead_days', 'text'], ['sourceSystem', 'source_system', 'text'], ['notes', 'notes', 'text']];
function calendarFact(value: string | number, order: boolean, en: boolean) {
    const keys = order ? ['Incomplete', 'ExpiryBeforeIssue', 'SameDate', 'IssueBeforeExpiry'] : ['NotDeclared', 'BeforeReferenceDate', 'OnReferenceDate', 'AfterReferenceDate'];
    const index = typeof value === 'number' ? value : keys.indexOf(value);
    const labels = order ? ['incomplete', 'expiryBeforeIssue', 'sameDate', 'issueBeforeExpiry'] as const
        : ['notDeclared', 'beforeObservationDate', 'onObservationDate', 'afterObservationDate'] as const;
    return Number.isInteger(index) && index >= 0 && index < labels.length ? presentationLabel(en, labels[index]) : (localizedText(en, "pairs.m_a5a7675bd7000dbd"));
}
// Mounted only inside the current authorized crew profile. No global cache/storage.
export function CertificatePanel({ vessel, assignment, language, csrf, onDirty, person }: {
    person?: string;
    vessel: string;
    assignment: string;
    language: AppLocale;
    csrf: string;
    onDirty: (value: boolean) => void;
}) {
    const en = language === 'en', path = `vessels/${vessel}/crew/${assignment}/certificates`;
    const [rows, setRows] = useState<Credential[] | null>(null), [error, setError] = useState(0), [version, reload] = useState(0);
    const [editor, setEditor] = useState<{
        id: string;
        version: number | null;
        metadata: Metadata;
    } | null>(null), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false);
    const [documentChoices, setDocumentChoices] = useState<CrewFileVersion[]>([]);
    const [documentGeneration, refreshDocuments] = useState(0);
    const resetScope1 = [person, vessel, assignment, csrf, documentGeneration] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], person) || !Object.is(previousScope1[1], vessel) || !Object.is(previousScope1[2], assignment) || !Object.is(previousScope1[3], csrf) || !Object.is(previousScope1[4], documentGeneration)) {
        setPreviousScope1(resetScope1);
        setDocumentChoices([]);
    }
    useEffect(() => {
        const c = new AbortController();
        if (person)
            crewFileVersions(vessel, assignment, person, c.signal).then(data => {
                if (!c.signal.aborted)
                    setDocumentChoices(data.items.filter(x => x.kind === 'CertificateDocument' && x.state === 'CleanTransport'));
            }).catch(() => { if (!c.signal.aborted)
                setDocumentChoices([]); });
        return () => c.abort();
    }, [person, vessel, assignment, csrf, documentGeneration]);
    const attempt = useRef<string | null>(null), requestController = useRef<AbortController | null>(null);
    const resetScope2 = [path, version, csrf] as const;
    const [previousScope2, setPreviousScope2] = useState(resetScope2);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope2[0], path) || !Object.is(previousScope2[1], version) || !Object.is(previousScope2[2], csrf)) {
        setPreviousScope2(resetScope2);
        setRows(null);
        setError(0);
    }
    useEffect(() => {
        const c = new AbortController();
        request<Credential[]>(path, c.signal).then(data => { if (!Array.isArray(data) || data.some(r => !r || typeof r.id !== 'string' || !r.metadata?.declaration || !r.dates))
            throw new ApiError(502); if (!c.signal.aborted)
            setRows(data); }).catch(e => { if (!c.signal.aborted)
            setError(e instanceof ApiError ? e.status : 503); });
        return () => { c.abort(); requestController.current?.abort(); };
    }, [path, version, csrf]);
    useEffect(() => () => onDirty(false), [onDirty]);
    async function save() {
        if (!editor || busy)
            return;
        const c = new AbortController();
        requestController.current = c;
        setBusy(true);
        setError(0);
        attempt.current ??= crypto.randomUUID();
        try {
            await request(path, c.signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.current },
                body: JSON.stringify({ id: editor.id, baseVersion: editor.version, metadata: editor.metadata }) });
            if (!c.signal.aborted) {
                setEditor(null);
                setUncertain(false);
                attempt.current = null;
                onDirty(false);
                reload(x => x + 1);
            }
        }
        catch (e) {
            if (!c.signal.aborted) {
                const status = e instanceof ApiError ? e.status : 0;
                setError(status || 503);
                if ([401, 403, 404].includes(status)) {
                    setRows(null);
                    setEditor(null);
                    onDirty(false);
                    attempt.current = null;
                }
                setUncertain(status === 0 || status >= 500);
                if (status > 0 && status < 500)
                    attempt.current = null;
            }
        }
        finally {
            if (!c.signal.aborted)
                setBusy(false);
        }
    }
    return <section aria-label={localizedText(en, "pairs.m_73d8d18ebec862e8")}>
    <h2>{localizedText(en, "pairs.m_d78cf63f6c42010f")}</h2>
    <p>{localizedText(en, "pairs.m_c03a31869ada6ab7")}</p>
    {error > 0 && <p role="alert">{error === 403 || error === 404 ? (localizedText(en, "pairs.m_12e2942182aa7ea7")) : error === 409 ? (localizedText(en, "pairs.m_b792c495240516f7")) : (localizedText(en, "pairs.m_7520500908b8d3eb"))}</p>}
    {!rows && !error && <p role="status">{localizedText(en, "pairs.m_36ecada1f3696f83")}</p>}
    {rows && <>
      {rows.length === 0 && <p>{localizedText(en, "pairs.m_58d30a297b01e84e")}</p>}
      {!editor && <button onClick={() => { setEditor({ id: crypto.randomUUID(), version: null, metadata: blank() }); onDirty(true); }}>{localizedText(en, "pairs.m_c0333bcdea432247")}</button>}
      <ul>{rows.map((r, i) => <li key={r.id}>{localizedText(en, "pairs.m_0f8b179e58b474b2")} {i + 1} · {localizedText(en, "pairs.m_b980704b1fa89cd9")} · {r.metadata.declaration.certificateNumber ?? '—'}
        {r.lookupState === 'LegacyDocumentReferenceUnavailable' && <span role="status">{localizedText(en, "pairs.m_dd2a1fa9566b87bb")}</span>}
        <span> · {localizedText(en, "pairs.m_7968c71442d3eb2d")}: {r.referenceDate}; {localizedText(en, "pairs.m_c23ba1443c8f2992")}: {calendarFact(r.dates.issue, false, en)} / {calendarFact(r.dates.expiry, false, en)} / {calendarFact(r.dates.order, true, en)}</span>
        <button disabled={!!editor} onClick={() => { setEditor({ id: r.id, version: r.version, metadata: structuredClone(r.metadata) }); onDirty(true); }}>{localizedText(en, "pairs.m_59e5e4b44fb02a49")}</button></li>)}</ul>
      <a href={`/app/excel-migration/${vessel}?assignment=${assignment}`}>{localizedText(en, "pairs.m_1825999a24d3a86b")}</a>
    </>}
    {editor && <form onSubmit={e => { e.preventDefault(); void save(); }}><fieldset disabled={busy || uncertain}>
      <legend>{localizedText(en, "pairs.m_c40a21c926ace158")}</legend>
      {declarationFields.map(([key, field, type]) => <label key={key}>{label(field, language)}<input type={type} value={editor.metadata.declaration[key] ?? ''} onChange={e => { const value = e.target.value || null; setEditor({ ...editor, metadata: { ...editor.metadata, declaration: { ...editor.metadata.declaration, [key]: value } } }); }}/></label>)}
      <label>{label('document_ref', language)}<select value={editor.metadata.documentReference ?? ''} onChange={e => setEditor({ ...editor, metadata: { ...editor.metadata, documentReference: e.target.value || null } })}>
        <option value="">{localizedText(en, "pairs.m_9aa28c27df900212")}</option>
        {editor.metadata.documentReference && !documentChoices.some(x => x.id === editor.metadata.documentReference) && <option value={editor.metadata.documentReference} disabled>{localizedText(en, "pairs.m_c51d77aec57e6c6b")}</option>}
        {documentChoices.map(x => <option key={x.id} value={x.id}>{localizedText(en, "pairs.m_eb241d06d2fabd35")} {x.revision} · {x.expiresOn ?? (localizedText(en, "pairs.m_e32290c3d5424d23"))}</option>)}
      </select></label>
      <button type="button" onClick={() => refreshDocuments(n => n + 1)}>{localizedText(en, "pairs.m_e3486e5436e0d57b")}</button>
      {fields.map(([key, field, type]) => <label key={key}>{label(field, language)}<input type={type} value={editor.metadata[key] ?? ''} onChange={e => { const value = e.target.value || null; setEditor({ ...editor, metadata: { ...editor.metadata, [key]: value } }); }}/></label>)}
      <label>{label('endorsement_required', language)}<select value={editor.metadata.endorsementRequired == null ? '' : String(editor.metadata.endorsementRequired)} onChange={e => setEditor({ ...editor, metadata: { ...editor.metadata, endorsementRequired: e.target.value === '' ? null : e.target.value === 'true' } })}><option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option><option value="true">{localizedText(en, "pairs.m_b0df697bd7b8694e")}</option><option value="false">{localizedText(en, "pairs.m_63083b11c5b758e1")}</option></select></label>
    </fieldset>
      {uncertain && <p role="alert">{localizedText(en, "pairs.m_8836c9831e0c9d9d")}</p>}
      <button disabled={busy} type="submit">{uncertain ? (localizedText(en, "pairs.m_171cd3e729d37709")) : (localizedText(en, "pairs.m_1057f80f39b6b640"))}</button>
      <button disabled={busy || uncertain} type="button" onClick={() => { setEditor(null); onDirty(false); attempt.current = null; }}>{localizedText(en, "pairs.m_dad8ecb8343080c2")}</button>
    </form>}
  </section>;
}
