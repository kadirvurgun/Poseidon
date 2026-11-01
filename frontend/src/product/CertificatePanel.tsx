import { useEffect, useRef, useState } from 'react'
import { ApiError, request } from './api'
import { label } from './import-api'

type Declaration = { certificateTypeReference: string | null; certificateNumber: string | null; issuer: string | null; issueDate: string | null; expiryDate: string | null; limitations: string | null }
type Metadata = { declaration: Declaration; issuingCountryNumeric: string | null; endorsementRequired: boolean | null; endorsementNumber: string | null;
  endorsementExpiry: string | null; stcwReference: string | null; tankerRelevance: string | null; vesselTypeScope: string | null;
  documentReference: string | null; renewalLeadDaysNumeric: string | null; sourceSystem: string | null; notes: string | null }
type Credential = { id: string; version: number; revision: number; metadata: Metadata; verificationState: string; provenance: string; lookupState: string; referenceDate: string;
  dates: { issue: string | number; expiry: string | number; order: string | number } }
const blank = (): Metadata => ({ declaration: { certificateTypeReference: null, certificateNumber: null, issuer: null, issueDate: null, expiryDate: null, limitations: null },
  issuingCountryNumeric: null, endorsementRequired: null, endorsementNumber: null, endorsementExpiry: null, stcwReference: null, tankerRelevance: null,
  vesselTypeScope: null, documentReference: null, renewalLeadDaysNumeric: null, sourceSystem: null, notes: null })
const declarationFields: [keyof Declaration, string, string][] = [['certificateTypeReference', 'certificate_type', 'text'], ['certificateNumber', 'certificate_no', 'text'], ['issuer', 'issuer', 'text'], ['issueDate', 'issue_date', 'date'], ['expiryDate', 'expiry_date', 'date'], ['limitations', 'restriction', 'text']]
const fields: [Exclude<keyof Metadata, 'declaration' | 'endorsementRequired'>, string, string][] = [['issuingCountryNumeric', 'issuing_country', 'text'], ['endorsementNumber', 'endorsement_no', 'text'], ['endorsementExpiry', 'endorsement_expiry', 'date'], ['stcwReference', 'stcw_reference', 'text'], ['tankerRelevance', 'tanker_relevance', 'text'], ['vesselTypeScope', 'vessel_type_scope', 'text'], ['documentReference', 'document_ref', 'text'], ['renewalLeadDaysNumeric', 'renewal_lead_days', 'text'], ['sourceSystem', 'source_system', 'text'], ['notes', 'notes', 'text']]


function calendarFact(value: string | number, order: boolean, en: boolean) {
  const keys = order ? ['Incomplete', 'ExpiryBeforeIssue', 'SameDate', 'IssueBeforeExpiry'] : ['NotDeclared', 'BeforeReferenceDate', 'OnReferenceDate', 'AfterReferenceDate']
  const index = typeof value === 'number' ? value : keys.indexOf(value)
  const words = order ? (en ? ['Incomplete', 'Expiry before issue', 'Same date', 'Issue before expiry'] : ['Eksik', 'Bitiş düzenlemeden önce', 'Aynı tarih', 'Düzenleme bitişten önce'])
    : (en ? ['Not declared', 'Before observation date', 'On observation date', 'After observation date'] : ['Beyan edilmedi', 'Gözlem tarihinden önce', 'Gözlem tarihinde', 'Gözlem tarihinden sonra'])
  return Number.isInteger(index) && index >= 0 && index < words.length ? words[index] : (en ? 'Unknown' : 'Bilinmiyor')
}

// Mounted only inside the current authorized crew profile. No global cache/storage.
export function CertificatePanel({ vessel, assignment, language, csrf, onDirty }: { vessel: string; assignment: string; language: 'tr' | 'en'; csrf: string; onDirty: (value: boolean) => void }) {
  const en = language === 'en', path = `vessels/${vessel}/crew/${assignment}/certificates`
  const [rows, setRows] = useState<Credential[] | null>(null), [error, setError] = useState(0), [version, reload] = useState(0)
  const [editor, setEditor] = useState<{ id: string; version: number | null; metadata: Metadata } | null>(null), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false)
  const attempt = useRef<string | null>(null), requestController = useRef<AbortController | null>(null)
  useEffect(() => { const c = new AbortController(); setRows(null); setError(0)
    request<Credential[]>(path, c.signal).then(data => { if (!Array.isArray(data) || data.some(r => !r || typeof r.id !== 'string' || !r.metadata?.declaration || !r.dates)) throw new ApiError(502); if (!c.signal.aborted) setRows(data) }).catch(e => { if (!c.signal.aborted) setError(e instanceof ApiError ? e.status : 503) })
    return () => { c.abort(); requestController.current?.abort() }
  }, [path, version, csrf])
  useEffect(() => () => onDirty(false), [onDirty])
  async function save() {
    if (!editor || busy) return
    const c = new AbortController(); requestController.current = c; setBusy(true); setError(0); attempt.current ??= crypto.randomUUID()
    try { await request(path, c.signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.current },
      body: JSON.stringify({ id: editor.id, baseVersion: editor.version, metadata: editor.metadata }) })
      if (!c.signal.aborted) { setEditor(null); setUncertain(false); attempt.current = null; onDirty(false); reload(x => x + 1) }
    } catch (e) { if (!c.signal.aborted) { const status = e instanceof ApiError ? e.status : 0; setError(status || 503); if ([401, 403, 404].includes(status)) { setRows(null); setEditor(null); onDirty(false); attempt.current = null } setUncertain(status === 0 || status >= 500); if (status > 0 && status < 500) attempt.current = null } }
    finally { if (!c.signal.aborted) setBusy(false) }
  }
  return <section aria-label={en ? 'Private certificate declarations' : 'Özel sertifika beyanları'}>
    <h2>{en ? 'Certificate declarations' : 'Sertifika beyanları'}</h2>
    <p>{en ? 'Unverified draft metadata. Controlled lookup, document authenticity and authorized human signature remain separate gates. Calendar facts do not establish qualification, fitness or readiness.' : 'Doğrulanmamış taslak metadata. Kontrollü referans, belge gerçekliği ve yetkili insan imzası ayrı kapılardır. Takvim gözlemleri yetkinlik, sağlık veya hazır olma durumu oluşturmaz.'}</p>
    {error > 0 && <p role="alert">{error === 403 || error === 404 ? (en ? 'Private certificate access denied.' : 'Özel sertifika erişimi reddedildi.') : error === 409 ? (en ? 'The declaration changed. Reload before editing.' : 'Beyan değişti. Düzenlemeden önce yeniden yükleyin.') : (en ? 'Certificate request unavailable.' : 'Sertifika isteği kullanılamıyor.')}</p>}
    {!rows && !error && <p role="status">{en ? 'Loading declarations…' : 'Beyanlar yükleniyor…'}</p>}
    {rows && <>
      {rows.length === 0 && <p>{en ? 'No declarations.' : 'Beyan yok.'}</p>}
      {!editor && <button onClick={() => { setEditor({ id: crypto.randomUUID(), version: null, metadata: blank() }); onDirty(true) }}>{en ? 'Add certificate declaration' : 'Sertifika beyanı ekle'}</button>}
      <ul>{rows.map((r, i) => <li key={r.id}>{en ? 'Declaration' : 'Beyan'} {i + 1} · {en ? 'Unverified' : 'Doğrulanmamış'} · {r.metadata.declaration.certificateNumber ?? '—'}
        <span> · {en ? 'UTC observation date' : 'UTC gözlem tarihi'}: {r.referenceDate}; {en ? 'issue / expiry / order facts' : 'düzenleme / bitiş / sıra gözlemi'}: {calendarFact(r.dates.issue, false, en)} / {calendarFact(r.dates.expiry, false, en)} / {calendarFact(r.dates.order, true, en)}</span>
        <button disabled={!!editor} onClick={() => { setEditor({ id: r.id, version: r.version, metadata: structuredClone(r.metadata) }); onDirty(true) }}>{en ? 'Edit declaration' : 'Beyanı düzenle'}</button></li>)}</ul>
      <a href={`/app/excel-migration/${vessel}?assignment=${assignment}`}>{en ? 'Open controlled import' : 'Kontrollü içe aktarmayı aç'}</a>
    </>}
    {editor && <form onSubmit={e => { e.preventDefault(); void save() }}><fieldset disabled={busy || uncertain}>
      <legend>{en ? 'Unverified certificate draft' : 'Doğrulanmamış sertifika taslağı'}</legend>
      {declarationFields.map(([key, field, type]) => <label key={key}>{label(field, language)}<input type={type} value={editor.metadata.declaration[key] ?? ''} onChange={e => { const value = e.target.value || null; setEditor({ ...editor, metadata: { ...editor.metadata, declaration: { ...editor.metadata.declaration, [key]: value } } }) }} /></label>)}
      {fields.map(([key, field, type]) => <label key={key}>{label(field, language)}<input type={type} value={editor.metadata[key] ?? ''} onChange={e => { const value = e.target.value || null; setEditor({ ...editor, metadata: { ...editor.metadata, [key]: value } }) }} /></label>)}
      <label>{label('endorsement_required', language)}<select value={editor.metadata.endorsementRequired == null ? '' : String(editor.metadata.endorsementRequired)} onChange={e => setEditor({ ...editor, metadata: { ...editor.metadata, endorsementRequired: e.target.value === '' ? null : e.target.value === 'true' } })}><option value="">{en ? 'Unknown' : 'Bilinmiyor'}</option><option value="true">{en ? 'Yes' : 'Evet'}</option><option value="false">{en ? 'No' : 'Hayır'}</option></select></label>
    </fieldset>
      {uncertain && <p role="alert">{en ? 'Result uncertain. Retry the same declaration.' : 'Sonuç belirsiz. Aynı beyanı tekrar deneyin.'}</p>}
      <button disabled={busy} type="submit">{uncertain ? (en ? 'Retry same declaration' : 'Aynı beyanı tekrar dene') : (en ? 'Save unverified draft' : 'Doğrulanmamış taslağı kaydet')}</button>
      <button disabled={busy || uncertain} type="button" onClick={() => { setEditor(null); onDirty(false); attempt.current = null }}>{en ? 'Cancel' : 'Vazgeç'}</button>
    </form>}
  </section>
}
