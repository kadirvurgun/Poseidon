import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { decodeCertificate } from './certificates-api'

export const extractionSourceVersion = '18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:46-47;14.02:1634-1657:v46.3'
export const extractionFields = ['certificate_type', 'certificate_no', 'issuer', 'issue_date', 'expiry_date', 'vessel_reference', 'person_reference'] as const
export type ExtractionField = typeof extractionFields[number]
export type ConfirmableField = Exclude<ExtractionField, 'vessel_reference' | 'person_reference'>
export type ExtractionCandidate = { key: ExtractionField; value: string | null; confidence: number | null; page: number | null; lowConfidence: boolean; state: 'REVIEW_REQUIRED'; reasons: string[] }
export type CertificateExtraction = { certificateId: string; vesselId: string; scope: 'Vessel' | 'Crew'; version: number; state: 'REVIEW_REQUIRED'; candidates: ExtractionCandidate[]; modelVersion: string; promptVersion: string; policyVersion: string; sourceVersion: typeof extractionSourceVersion; lowConfidenceThreshold: number; reviewContext: string | null; expiresAt: string; canConfirm: boolean; blockers: string[] }
export type ExtractionFact = { key: ConfirmableField; value: string; confirmed: true }
export type ExtractionAttempt = Readonly<{ vessel: string; id: string; owner: string; key: string; body: string }>
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const exact = (value: unknown, keys: readonly string[]): value is Record<string, unknown> => object(value) && Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key))
const text = (value: unknown, max: number): value is string => typeof value === 'string' && value.length <= max && !/\p{Cc}/u.test(value)
const version = (value: unknown): value is number => typeof value === 'number' && Number.isInteger(value) && value > 0 && value <= 4294967295
const fraction = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= 1
const codes = (value: unknown): value is string[] => Array.isArray(value) && value.length <= 32 && value.every(code => text(code, 200) && code.length > 0)
function fail(): never { throw new ApiError(502, 'invalid_extraction_contract') }
export function decodeExtraction(value: unknown, vessel: string, id: string, expectedVersion: number, scope: 'Vessel' | 'Crew' = 'Vessel'): CertificateExtraction {
  if (!exact(value, ['certificateId', 'vesselId', 'scope', 'version', 'state', 'candidates', 'modelVersion', 'promptVersion', 'policyVersion', 'sourceVersion', 'lowConfidenceThreshold', 'reviewContext', 'expiresAt', 'canConfirm', 'blockers']) || value.certificateId !== id || value.vesselId !== vessel || value.scope !== scope || value.version !== expectedVersion || !version(value.version) || value.state !== 'REVIEW_REQUIRED' || !Array.isArray(value.candidates) || value.candidates.length !== extractionFields.length || !fraction(value.lowConfidenceThreshold) || value.sourceVersion !== extractionSourceVersion || !timestamp(value.expiresAt) || typeof value.canConfirm !== 'boolean' || !codes(value.blockers)) fail()
  if (!['modelVersion', 'promptVersion', 'policyVersion'].every(key => text(value[key], 128) && (value[key] as string).length > 0) || !(value.reviewContext === null || text(value.reviewContext, 65536) && value.reviewContext.length > 0) || value.canConfirm !== (value.reviewContext !== null) || scope === 'Crew' && value.canConfirm) fail()
  const candidates = value.candidates.map((field): ExtractionCandidate => {
    if (!exact(field, ['key', 'value', 'confidence', 'page', 'lowConfidence', 'state', 'reasons']) || !extractionFields.some(key => key === field.key) || !(field.value === null || text(field.value, 512)) || !(field.confidence === null || fraction(field.confidence)) || !(field.page === null || typeof field.page === 'number' && Number.isInteger(field.page) && field.page >= 1 && field.page <= 1000) || typeof field.lowConfidence !== 'boolean' || field.lowConfidence !== (field.confidence === null || field.confidence < (value.lowConfidenceThreshold as number)) || field.state !== 'REVIEW_REQUIRED' || !codes(field.reasons) || !field.reasons.includes('PROVIDER_CANDIDATE_NOT_VERIFICATION')) fail()
    return field as ExtractionCandidate
  })
  if (new Set(candidates.map(field => field.key)).size !== extractionFields.length) fail()
  return { ...value, candidates } as CertificateExtraction
}
function path(vessel: string, id: string) { if (!isGuid(vessel) || !isGuid(id)) throw new ApiError(400, 'invalid_context'); return `vessels/${vessel}/certificates/${id}/extractions` }
export function freezeExtraction(vessel: string, id: string, owner: string, source: CertificateExtraction, facts: ExtractionFact[], vesselContextConfirmed: boolean): ExtractionAttempt {
  path(vessel, id)
  if (!owner.split('/').every(isGuid) || owner.split('/').length !== 2 || !source.canConfirm || source.scope !== 'Vessel' || source.vesselId !== vessel || source.certificateId !== id || !source.reviewContext || !vesselContextConfirmed || facts.length < 1 || facts.length > 5 || new Set(facts.map(fact => fact.key)).size !== facts.length || facts.some(fact => !extractionFields.slice(0, 5).some(key => key === fact.key) || fact.confirmed !== true || !text(fact.value, 512) || !fact.value.trim())) throw new ApiError(400, 'invalid_confirmation')
  return Object.freeze({ vessel, id, owner, key: crypto.randomUUID(), body: JSON.stringify({ expectedVersion: source.version, reviewContext: source.reviewContext, facts, vesselContextConfirmed: true }) })
}
export const certificateExtractionApi = {
  extract: async (vessel: string, id: string, expectedVersion: number, csrf: string, signal: AbortSignal) => {
    if (!version(expectedVersion)) throw new ApiError(400, 'invalid_context')
    return decodeExtraction(await request<unknown>(path(vessel, id), signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf }, body: JSON.stringify({ expectedVersion }) }), vessel, id, expectedVersion)
  },
  confirm: async (command: ExtractionAttempt, csrf: string, signal: AbortSignal) => {
    const saved = decodeCertificate(await request<unknown>(`${path(command.vessel, command.id)}/confirm`, signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: command.body }), command.vessel, command.id)
    if (saved.scope !== 'Vessel' || saved.currentRevision || saved.canEdit) fail()
    return saved
  },
}
