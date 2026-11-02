import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// Hand-authored closed consumer. Actual host OpenAPI generation is DEFERRED.
export type Kind = 'Control' | 'Evidence'
export type Content = { title: string | null; ownerId: string | null; ownerRole: string | null; frequency: string | null; evidencePolicy: string | null; verificationPolicy: string | null; critical: boolean | null; provenance: string | null; observedAt: string | null; validFrom: string | null; validUntil: string | null; freshUntil: string | null; acknowledgments?: { ruleId: string; qualificationHash: string; sourceHash: string }[] | null }
export type RequirementPin = { requirementId: string; requirementRevision: number; sourceId: string; sourceRevision: number; ruleId: string | null; ruleRevision: number | null }
export type GraphPin = { kind: Kind; id: string; revision: number; expectedVersion: number }
export type FilePin = { id: string; revision: number; checksum: string }
export type Command = { expectedVersion: number; content: Content; requirements: RequirementPin[]; links: GraphPin[]; file: FilePin | null; reasonCode: 'INITIAL_AUTHORING' | 'AUTHORING_CORRECTION' | 'SUBMIT' | 'COMPLETE' }
export type Row = { id: string; vesselId: string; kind: Kind; revision: number; version: number; content: Content; hash: string; lifecycle: 'DRAFT' | 'SUBMITTED' | 'COMPLETED'; completion: 'UNKNOWN' | 'COMPLETED'; verification: 'UNVERIFIED' | 'VERIFIED' | 'REVIEW_REQUIRED'; effectiveness: 'UNKNOWN'; timeState: 'SUPERSEDED' | 'DECLARED_NOT_YET_VALID' | 'DECLARED_EXPIRED' | 'DECLARED_STALE' | 'UNKNOWN'; evidenceExists: boolean; file: FilePin | null; requirements: RequirementPin[]; links: GraphPin[]; blockers: string[]; savedAt: string; asOf: string }
export type Page = { items: Row[]; offset: number; hasMore: boolean; canWrite: boolean }
type Obj = Record<string, unknown>
const exact = (v: unknown, keys: string[]): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const positive = (v: unknown): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const version = (v: unknown): v is number => positive(v) && v <= 4294967295
const hash = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$(?![\s\S])/.test(v)
const invalid = (): never => { throw new ApiError(502, 'invalid_contract') }
export const blank = (): Content => ({ title: null, ownerId: null, ownerRole: null, frequency: null, evidencePolicy: null, verificationPolicy: null, critical: null, provenance: null, observedAt: null, validFrom: null, validUntil: null, freshUntil: null, acknowledgments: null })
export function validContent(v: unknown): v is Content {
  if (!exact(v, Object.keys(blank()).filter(k => k !== 'acknowledgments' || !!v && typeof v === 'object' && Object.hasOwn(v, k)))) return false
  if (v.acknowledgments !== undefined && v.acknowledgments !== null && (!Array.isArray(v.acknowledgments) || v.acknowledgments.length > 32 || v.acknowledgments.some(a => !exact(a, ['ruleId', 'qualificationHash', 'sourceHash']) || !isGuid(a.ruleId) || !hash(a.qualificationHash) || !hash(a.sourceHash)))) return false
  for (const k of ['title', 'frequency', 'evidencePolicy', 'verificationPolicy', 'provenance']) if (v[k] !== null && (typeof v[k] !== 'string' || !v[k].trim() || v[k] !== v[k].trim() || v[k].length > 2000 || /[\u0000-\u001f\u007f-\u009f]/.test(v[k]))) return false
  if (!(v.ownerId === null && v.ownerRole === null || isGuid(v.ownerId) && isGuid(v.ownerRole)) || v.critical !== null && typeof v.critical !== 'boolean') return false
  for (const k of ['observedAt', 'validFrom', 'validUntil', 'freshUntil']) if (v[k] !== null && !timestamp(v[k])) return false
  return !(v.validFrom && v.validUntil && Date.parse(v.validUntil as string) < Date.parse(v.validFrom as string) || v.observedAt && v.freshUntil && Date.parse(v.freshUntil as string) < Date.parse(v.observedAt as string))
}
function requirement(v: unknown): v is RequirementPin {
  return exact(v, ['requirementId', 'requirementRevision', 'sourceId', 'sourceRevision', 'ruleId', 'ruleRevision']) && isGuid(v.requirementId) && positive(v.requirementRevision) && isGuid(v.sourceId) && positive(v.sourceRevision) && (v.ruleId === null && v.ruleRevision === null || isGuid(v.ruleId) && positive(v.ruleRevision))
}
function edge(v: unknown): v is GraphPin { return exact(v, ['kind', 'id', 'revision', 'expectedVersion']) && (v.kind === 'Control' || v.kind === 'Evidence') && isGuid(v.id) && positive(v.revision) && version(v.expectedVersion) }
function file(v: unknown): v is FilePin { return exact(v, ['id', 'revision', 'checksum']) && isGuid(v.id) && positive(v.revision) && hash(v.checksum) }
export function decodeRow(v: unknown, vessel: string, kind: Kind): Row {
  if (!exact(v, ['id', 'vesselId', 'kind', 'revision', 'version', 'content', 'hash', 'lifecycle', 'completion', 'verification', 'effectiveness', 'timeState', 'evidenceExists', 'file', 'requirements', 'links', 'blockers', 'savedAt', 'asOf']) || !isGuid(v.id) || v.vesselId !== vessel || v.kind !== kind || !positive(v.revision) || !version(v.version) || !validContent(v.content) || !hash(v.hash) || !['DRAFT', 'SUBMITTED', 'COMPLETED'].includes(v.lifecycle as string) || v.completion !== (v.lifecycle === 'COMPLETED' ? 'COMPLETED' : 'UNKNOWN') || !['UNVERIFIED', 'VERIFIED', 'REVIEW_REQUIRED'].includes(v.verification as string) || v.effectiveness !== 'UNKNOWN' || typeof v.timeState !== 'string' || !['SUPERSEDED', 'DECLARED_NOT_YET_VALID', 'DECLARED_EXPIRED', 'DECLARED_STALE', 'UNKNOWN'].includes(v.timeState) || typeof v.evidenceExists !== 'boolean' || v.file !== null && !file(v.file) || v.evidenceExists !== (v.file !== null) || !Array.isArray(v.requirements) || v.requirements.length > 32 || !v.requirements.every(requirement) || !Array.isArray(v.links) || v.links.length > 32 || !v.links.every(edge) || !timestamp(v.savedAt) || !timestamp(v.asOf) || Date.parse(v.savedAt) > Date.parse(v.asOf)) return invalid()
  if (kind === 'Control' && (v.file !== null || v.links.length > 0) || v.links.some(p => p.id === v.id) || new Set(v.links.map(p => `${p.kind}/${p.id}/${p.revision}`)).size !== v.links.length || new Set(v.requirements.map(p => `${p.requirementId}/${p.requirementRevision}`)).size !== v.requirements.length) return invalid()
  const codes = ['APPLICABILITY_LINK_MISSING', 'TITLE_MISSING', 'OWNER_MISSING', 'REQUIREMENT_LINK_MISSING', 'FREQUENCY_UNSPECIFIED', 'EVIDENCE_POLICY_MISSING', 'VERIFICATION_POLICY_MISSING', 'CRITICALITY_UNKNOWN', 'CLEAN_FILE_MISSING', 'PROVENANCE_MISSING', 'OBSERVATION_TIME_MISSING', 'SOURCE_ADMISSION_UNAVAILABLE', 'VERIFICATION_NOT_PERFORMED', 'CONTROL_POLICY_MISSING', 'SOURCE_REVIEW_REQUIRED', 'EVIDENCE_MECHANISM_UNAVAILABLE', 'QUALIFIED_CONTROL_POLICY_MISMATCH', 'QUALIFIED_FRESHNESS_REQUIRED', 'SOURCE_ACKNOWLEDGMENT_REQUIRED', 'AUTHORITATIVE_EVALUATION_UNAVAILABLE', 'APPLICABILITY_REVIEW_REQUIRED', 'QUALIFICATION_LINEAGE_UNAVAILABLE', 'PROFILE_CHANGED', 'VESSEL_CHANGED', 'RULE_CHANGED', 'QUALIFICATION_CHANGED', 'SOURCE_NOT_CURRENT', 'ATTESTATION_EXPIRED', 'INPUT_STALE']
  if (!Array.isArray(v.blockers) || v.blockers.length > codes.length || v.blockers.some(b => typeof b !== 'string' || !codes.includes(b))) return invalid()
  const content = v.content as Content
  const required = [!content.title ? 'TITLE_MISSING' : null, !content.ownerId ? 'OWNER_MISSING' : null,
    v.requirements.length === 0 ? 'REQUIREMENT_LINK_MISSING' : null,
    kind === 'Control' && !content.frequency ? 'FREQUENCY_UNSPECIFIED' : null,
    kind === 'Control' && !content.evidencePolicy ? 'EVIDENCE_POLICY_MISSING' : null,
    kind === 'Control' && !content.verificationPolicy ? 'VERIFICATION_POLICY_MISSING' : null,
    kind === 'Control' && content.critical === null ? 'CRITICALITY_UNKNOWN' : null].filter(Boolean)
  if (required.some(code => !(v.blockers as unknown[]).includes(code)) || v.verification === 'VERIFIED' && (v.blockers.length > 0 || v.requirements.some(r => r.ruleId === null))) return invalid()
  return v as Row
}
const route = (kind: Kind) => kind === 'Control' ? 'controls' : 'evidence'
export async function page(vessel: string, kind: Kind, offset: number, signal: AbortSignal, id?: string, asOf?: string): Promise<Page> {
  const v = await request<unknown>(`${route(kind)}${id ? `/${id}/versions` : ''}?vesselId=${vessel}&offset=${offset}${asOf ? `&asOf=${encodeURIComponent(asOf)}` : ''}`, signal)
  if (!exact(v, ['items', 'offset', 'hasMore', 'canWrite']) || v.offset !== offset || typeof v.hasMore !== 'boolean' || typeof v.canWrite !== 'boolean' || !Array.isArray(v.items) || v.items.length > 25 || v.hasMore && v.items.length !== 25) return invalid()
  return { items: v.items.map(x => decodeRow(x, vessel, kind)), offset, hasMore: v.hasMore, canWrite: v.canWrite }
}
export async function save(vessel: string, kind: Kind, id: string | null, frozen: { body: string; key: string; action?: 'submit' | 'complete' }, csrf: string, signal: AbortSignal): Promise<Row> {
  const value = decodeRow(await request<unknown>(`${route(kind)}${id ? `/${id}` : ''}${frozen.action ? `/${frozen.action}` : ''}?vesselId=${vessel}`, signal, { method: frozen.action ? 'POST' : id ? 'PUT' : 'POST', body: frozen.body, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': frozen.key, 'X-CSRF-TOKEN': csrf } }), vessel, kind)
  if (id !== null && value.id !== id) return invalid()
  return value
}
export async function owners(vessel: string, signal: AbortSignal): Promise<{ actorId: string; roleId: string }[]> {
  const v = await request<unknown>(`controls/owners?vesselId=${vessel}`, signal)
  if (!exact(v, ['items']) || !Array.isArray(v.items) || v.items.length > 1000 || v.items.some(x => !exact(x, ['actorId', 'roleId']) || !isGuid(x.actorId) || !isGuid(x.roleId))) return invalid()
  return v.items as { actorId: string; roleId: string }[]
}
export async function orphans(vessel: string, offset: number, signal: AbortSignal): Promise<{ draftCriticalOrphans: string[]; hasMore: boolean; available: boolean; reason: string; uncontrolledObligations: { ruleId: string; ruleRevision: number; requirementId: string; requirementRevision: number }[] }> {
  const v = await request<unknown>(`controls/orphans?vesselId=${vessel}&offset=${offset}`, signal)
  if (!exact(v, ['available', 'reason', 'draftCriticalOrphans', 'uncontrolledObligations', 'offset', 'hasMore']) || typeof v.available !== 'boolean' || typeof v.reason !== 'string' || v.available && v.reason !== 'QUALIFIED_CATALOG_SCOPE_ONLY' || !Array.isArray(v.uncontrolledObligations) || v.uncontrolledObligations.length > 25 || v.uncontrolledObligations.some(o => !exact(o, ['ruleId', 'ruleRevision', 'requirementId', 'requirementRevision']) || !isGuid(o.ruleId) || !positive(o.ruleRevision) || !isGuid(o.requirementId) || !positive(o.requirementRevision)) || v.offset !== offset || typeof v.hasMore !== 'boolean' || !Array.isArray(v.draftCriticalOrphans) || v.draftCriticalOrphans.length > 25 || !v.draftCriticalOrphans.every(isGuid)) return invalid()
  return { draftCriticalOrphans: v.draftCriticalOrphans as string[], hasMore: v.hasMore, available: v.available, reason: v.reason, uncontrolledObligations: v.uncontrolledObligations as { ruleId: string; ruleRevision: number; requirementId: string; requirementRevision: number }[] }
}
