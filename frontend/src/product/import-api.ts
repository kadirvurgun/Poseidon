import type { components } from '../generated/api'
import { ApiError, request } from './api'
import type { ImportMappingTemplate, ImportMappingBinding } from './ImportMappingEditor'
import type { ImportValidationPage } from './ImportValidationResults'

export type ImportTemplate = components['schemas']['ImportTemplateDto']
export type ImportBatch = components['schemas']['ImportBatchDto']
export type Preview = components['schemas']['ImportPreviewPageDto']
export type Committed = components['schemas']['ImportCommittedDto']
export const base = (vessel: string) => `vessels/${encodeURIComponent(vessel)}/imports`
const json = (csrf: string, method: string, body?: unknown, key?: string): RequestInit => ({ method,
  headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, ...(key ? { 'Idempotency-Key': key } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) })
export const templates = (v: string, signal: AbortSignal) => request<ImportTemplate[]>(`${base(v)}/templates`, signal)
export const detail = (v: string, id: string, signal: AbortSignal) => request<ImportBatch>(`${base(v)}/${encodeURIComponent(id)}`, signal)
export const history = (v: string, offset: number, signal: AbortSignal) => request<{ items: { id: string; category: string; state: string; rowCount: number; createdAt: string }[]; hasMore: boolean }>(`${base(v)}?offset=${offset}`, signal)
export const upload = (v: string, category: string, assignment: string, file: File, csrf: string, signal: AbortSignal) => request<ImportBatch>(
  `${base(v)}/uploads/${encodeURIComponent(category)}${category === 'certificate' ? `?assignment=${encodeURIComponent(assignment)}` : ''}`, signal,
  { method: 'POST', headers: { 'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'X-CSRF-TOKEN': csrf }, body: file })
export const draft = (v: string, batch: ImportBatch, bindings: readonly ImportMappingBinding[], csrf: string, signal: AbortSignal) => {
  const command: components['schemas']['ImportMappingCommand'] = { version: batch.version, bindings: [...bindings] }
  return request<ImportBatch>(`${base(v)}/${batch.id}/mapping`, signal, json(csrf, 'PUT', command))
}
export const preview = (v: string, batch: string, csrf: string, signal: AbortSignal) => request<Preview>(`${base(v)}/${batch}/preview`, signal, json(csrf, 'POST'))
const exact = (p: Pick<Preview, 'previewId' | 'revision' | 'templateFingerprint'>) => `previews/${encodeURIComponent(p.previewId)}?revision=${encodeURIComponent(p.revision)}&fingerprint=${encodeURIComponent(p.templateFingerprint)}`
export const page = (v: string, batch: string, p: Preview, offset: number, signal: AbortSignal) => request<Preview>(`${base(v)}/${batch}/${exact(p)}&offset=${offset}`, signal)
export const commit = (v: string, batch: string, p: Preview, csrf: string, key: string, signal: AbortSignal) => {
  const command: components['schemas']['ImportCommitCommand'] = { previewId: p.previewId, revision: Number(p.revision), templateFingerprint: p.templateFingerprint }
  return request<Committed>(`${base(v)}/${batch}/commit`, signal, json(csrf, 'POST', command, key))
}
export function mapping(t: ImportTemplate, language: 'tr' | 'en'): ImportMappingTemplate {
  return { version: t.version, fingerprint: t.fingerprint, allowSourceColumnReuse: t.allowSourceColumnReuse,
    fields: t.fields.map(f => ({ key: f.key, label: label(f.key, language), required: f.requiredInput, writable: f.userWritable })) }
}
const labels: Record<string, [string, string]> = {
  record_key: ['Kayıt anahtarı (UUID)', 'Record key (UUID)'], base_version: ['Temel sürüm', 'Base version'], code: ['Kod', 'Code'], name: ['Ad', 'Name'],
  full_name: ['Ad soyad', 'Full name'], employee_no: ['Personel numarası', 'Employee number'], position_ref: ['Pozisyon referansı', 'Position reference'],
  from: ['Başlangıç tarihi', 'From date'], to: ['Bitiş tarihi (hariç)', 'To date (exclusive)'], contract_end_date: ['Sözleşme bitişi', 'Contract end'], watch_group: ['Vardiya grubu', 'Watch group'],
  certificate_type: ['Sertifika tür referansı (kontrollü çözüm bekler)', 'Certificate type reference (controlled resolution pending)'], certificate_no: ['Sertifika numarası', 'Certificate number'], issuer: ['Düzenleyen', 'Issuer'],
  issuing_country: ['Düzenleyen ülke: kaynak sayısal tür; hassasiyet açık', 'Issuing country: source numeric type; precision unresolved'], issue_date: ['Düzenleme tarihi', 'Issue date'], expiry_date: ['Bitiş tarihi', 'Expiry date'],
  endorsement_required: ['Endorsement gerekli mi (true/false/boş)', 'Endorsement required (true/false/blank)'], endorsement_no: ['Endorsement numarası', 'Endorsement number'], endorsement_expiry: ['Endorsement bitişi', 'Endorsement expiry'],
  stcw_reference: ['STCW referansı', 'STCW reference'], tanker_relevance: ['Tanker ilişkisi', 'Tanker relevance'], vessel_type_scope: ['Araç tür kapsam referansı', 'Vessel type scope reference'],
  document_ref: ['Personele ait taranmış belge sürümü', 'Scanned person document version'], renewal_lead_days: ['Yenileme ön gün sayısı (sayısal beyan)', 'Renewal lead days (numeric declaration)'], restriction: ['Kısıtlamalar', 'Limitations'], source_system: ['Kaynak sistem', 'Source system'], notes: ['Notlar', 'Notes'],
  parent_ref: ['Üst ekipman referansı', 'Parent equipment reference'], level: ['Seviye (System/Equipment/Component)', 'Level (System/Equipment/Component)'], system_family: ['Sistem ailesi', 'System family'], equipment_type: ['Ekipman türü', 'Equipment type'], criticality: ['Kritiklik beyanı', 'Criticality declaration'], maker: ['Üretici', 'Maker'], model: ['Model', 'Model'], serial_number: ['Seri numarası', 'Serial number'], location: ['Konum', 'Location'], commission_date: ['Devreye alma tarihi', 'Commission date'],
  person_id: ['Personel (yetkili bağlamdan)', 'Person (authorized context)'], verification_status: ['Doğrulama durumu', 'Verification status'], verified_by: ['Doğrulayan (eksik insan kapısı)', 'Verifier (missing human gate)'], verified_at: ['Doğrulama zamanı', 'Verification time'], alert_state: ['Uyarı durumu', 'Alert state'], sync_state: ['Eşitleme durumu', 'Sync state'], status: ['Durum', 'Status'],
}
export const label = (field: string, language: 'tr' | 'en') => Object.hasOwn(labels, field) ? labels[field][language === 'en' ? 1 : 0] : (language === 'en' ? 'Schema field' : 'Şema alanı')
const known = new Set(['required', 'invalid_format', 'duplicate', 'conflict', 'unresolved_reference', 'unsupported_value', 'domain_validation'])
export function results(p: Preview, t: ImportTemplate, contextKey: string): ImportValidationPage {
  if (p.templateVersion !== t.version || p.templateFingerprint !== t.fingerprint) throw new ApiError(409, 'version_conflict')
  return { ...p, contextKey, rows: p.rows.map(r => {
    if (r.outcome !== 'valid' && r.outcome !== 'rejected') throw new ApiError(502, 'invalid_contract')
    return { rowNumber: r.rowNumber, outcome: r.outcome, issues: r.issues.map(i => ({ fieldOrdinal: i.fieldOrdinal,
      code: known.has(i.code) ? i.code : 'unknown' })) }
  }) }
}
export async function download(v: string, category: string, batch: string | null, p: Preview | null, signal: AbortSignal) {
  const path = batch && p ? `${base(v)}/${batch}/previews/${encodeURIComponent(p.previewId)}/rejects?revision=${encodeURIComponent(p.revision)}&fingerprint=${encodeURIComponent(p.templateFingerprint)}` : `${base(v)}/templates/${encodeURIComponent(category)}/download`
  const response = await fetch(`/api/v1/${path}`, { signal, credentials: 'same-origin', cache: 'no-store' })
  if (!response.ok) throw new ApiError(response.status)
  const contentType = response.headers.get('Content-Type') ?? ''
  if (!(batch ? contentType.startsWith('text/csv') : contentType.startsWith('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'))) throw new ApiError(502)
  return response.blob()
}
