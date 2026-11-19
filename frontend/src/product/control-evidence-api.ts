import { complianceKeys, validCompliance, type ComplianceContent } from './compliance-input-contract'
import { validOwning, type OwningContent } from './operation-equipment-contract'
import { validCapability, type CapabilityStatement } from './tug-capability-contract'
import { ApiError, request, session } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// Hand-authored closed consumer. Actual host OpenAPI generation is DEFERRED.
export type Kind = 'Control' | 'Evidence'
export type Content = ComplianceContent & OwningContent & { tugCapability?: CapabilityStatement | null; title: string | null; ownerId: string | null; ownerRole: string | null; frequency: string | null; evidencePolicy: string | null; verificationPolicy: string | null; critical: boolean | null; provenance: string | null; observedAt: string | null; validFrom: string | null; validUntil: string | null; freshUntil: string | null; acknowledgments?: { ruleId: string; qualificationHash: string; sourceHash: string }[] | null }
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
  if (!exact(v, [...Object.keys(blank()), ...['tugCapability', 'operation', 'serviceAuthorization', 'cargoPlan', 'winchFitment', ...complianceKeys].filter(k => v && typeof v === 'object' && Object.hasOwn(v, k))].filter(k => k !== 'acknowledgments' || !!v && typeof v === 'object' && Object.hasOwn(v, k)))) return false
  if (['operation', 'serviceAuthorization', 'cargoPlan', 'winchFitment'].some(k => Object.hasOwn(v, k) && !validOwning(k, v[k])) || ['operation', 'serviceAuthorization', 'cargoPlan', 'winchFitment', 'tugCapability', ...complianceKeys].filter(k => v[k] != null).length > 1) return false
  if (complianceKeys.some(k => Object.hasOwn(v, k) && !validCompliance(k, v[k]))) return false
  if (v.tugCapability != null && !validCapability(v.tugCapability)) return false
  if (v.acknowledgments !== undefined && v.acknowledgments !== null && (!Array.isArray(v.acknowledgments) || v.acknowledgments.length > 32 || v.acknowledgments.some(a => !exact(a, ['ruleId', 'qualificationHash', 'sourceHash']) || !isGuid(a.ruleId) || !hash(a.qualificationHash) || !hash(a.sourceHash)))) return false
  for (const k of ['title', 'frequency', 'evidencePolicy', 'verificationPolicy', 'provenance']) if (v[k] !== null && (typeof v[k] !== 'string' || !v[k].trim() || v[k] !== v[k].trim() || v[k].length > 2000 || Array.from(v[k]).some(c => c.charCodeAt(0) < 32 || c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159))) return false
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
  if (kind === 'Control' && (['tugCapability', 'operation', 'serviceAuthorization', 'cargoPlan', 'winchFitment', ...complianceKeys].some(k => (v.content as unknown as Obj)[k] != null) || v.file !== null || v.links.length > 0) || v.links.some(p => p.id === v.id) || new Set(v.links.map(p => `${p.kind}/${p.id}/${p.revision}`)).size !== v.links.length || new Set(v.requirements.map(p => `${p.requirementId}/${p.requirementRevision}`)).size !== v.requirements.length) return invalid()
  const codes = ['OWNING_INPUT_INCOMPLETE', 'OWNING_JOB_INCOMPLETE', 'OWNING_DOCUMENT_POLICY_REQUIRED', 'OWNING_INPUT_CHANGED', 'CIRCULAR_INPUT_LINEAGE','TUG_CAPABILITY_INCOMPLETE', 'TUG_CAPABILITY_TEST_FUTURE', 'APPLICABILITY_LINK_MISSING', 'TITLE_MISSING', 'OWNER_MISSING', 'REQUIREMENT_LINK_MISSING', 'FREQUENCY_UNSPECIFIED', 'EVIDENCE_POLICY_MISSING', 'VERIFICATION_POLICY_MISSING', 'CRITICALITY_UNKNOWN', 'CLEAN_FILE_MISSING', 'PROVENANCE_MISSING', 'OBSERVATION_TIME_MISSING', 'SOURCE_ADMISSION_UNAVAILABLE', 'VERIFICATION_NOT_PERFORMED', 'CONTROL_POLICY_MISSING', 'SOURCE_REVIEW_REQUIRED', 'EVIDENCE_MECHANISM_UNAVAILABLE', 'QUALIFIED_CONTROL_POLICY_MISMATCH', 'QUALIFIED_FRESHNESS_REQUIRED', 'SOURCE_ACKNOWLEDGMENT_REQUIRED', 'AUTHORITATIVE_EVALUATION_UNAVAILABLE', 'APPLICABILITY_REVIEW_REQUIRED', 'QUALIFICATION_LINEAGE_UNAVAILABLE', 'PROFILE_CHANGED', 'VESSEL_CHANGED', 'RULE_CHANGED', 'QUALIFICATION_CHANGED', 'SOURCE_NOT_CURRENT', 'ATTESTATION_EXPIRED', 'INPUT_STALE']
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
  const items = v.items.map(x => decodeRow(x, vessel, kind))
  if (id && items.some(row => row.id !== id)) return invalid()
  return { items, offset, hasMore: v.hasMore, canWrite: v.canWrite }
}
export async function currentRecord(vessel: string, kind: Kind, id: string, signal: AbortSignal, initial?: Page): Promise<Row> {
  // Versions may expose an older visible revision with the live editing token.
  // Establish the visible current row through the existing current-list route;
  // never promote an historical projection merely because it carries that token.
  const already = initial?.items.find(row => row.id === id && row.vesselId === vessel && row.kind === kind)
  if (already) return already
  for (let offset = 0; offset <= 100000; offset += 25) {
    const value = await page(vessel, kind, offset, signal), found = value.items.find(row => row.id === id)
    if (found) return found
    if (!value.hasMore) return invalid()
  }
  return invalid() // Bounded backend query range; absence/uncertainty is not a new draft.
}
export async function loadSession(signal: AbortSignal) {
  const value: unknown = await session(signal)
  if (!value || typeof value !== 'object' || Array.isArray(value)) return invalid()
  const user = value as Obj
  if (user.authenticated !== true) throw new ApiError(401, 'authentication_required')
  if (user.canRead !== true || user.setupRequired !== false) throw new ApiError(403, 'permission_denied')
  if (!isGuid(user.actorId) || !isGuid(user.tenantId) || typeof user.csrfToken !== 'string' || user.csrfToken.length < 16 ||
      user.csrfToken.length > 2048 || Array.from(user.csrfToken).some(character => character.charCodeAt(0) < 32 || character.charCodeAt(0) === 127)) return invalid()
  return Object.freeze({ owner: `${user.tenantId.toLowerCase()}/${user.actorId.toLowerCase()}`, csrf: user.csrfToken })
}
const instants = new Set(['observedAt', 'validFrom', 'validUntil', 'freshUntil', 'testAt', 'reviewAt'])
function canonical(value: unknown, key = ''): string {
  if (value === null || value === undefined) return 'null'
  if (Array.isArray(value)) return '[' + value.map(item => canonical(item)).join(',') + ']'
  if (typeof value === 'object') return '{' + Object.entries(value).sort(([a], [b]) => a.localeCompare(b, 'en')).map(([name, item]) => JSON.stringify(name)+':'+canonical(item, name)).join(',') + '}'
  if (typeof value === 'string' && instants.has(key)) {
    if (!timestamp(value)) return invalid()
    const fraction = /\.(\d+)(?=Z|[+-]\d{2}:\d{2}$)/.exec(value)?.[1] ?? ''
    const milliseconds = Date.parse(value.replace(/\.\d+(?=Z|[+-]\d{2}:\d{2}$)/, ''))
    return JSON.stringify('instant:'+(BigInt(milliseconds)*10000n+BigInt(fraction.padEnd(7, '0'))).toString())
  }
  if (typeof value === 'string' && (key === 'id' || key.endsWith('Id') || key.endsWith('Role')) && isGuid(value)) return JSON.stringify(value.toLowerCase())
  const encoded = JSON.stringify(value)
  if (encoded === undefined) return invalid()
  return encoded
}
function content(value: Content) {
  return { ...blank(), tugCapability: null, operation: null, serviceAuthorization: null, cargoPlan: null, winchFitment: null,
    ...Object.fromEntries(complianceKeys.map(key => [key, null])), ...value }
}
const pins = (value: unknown[]) => value.map(item => canonical(item)).sort().join('|')
export function receiptMatches(value: Row, command: Command, action?: 'submit' | 'complete', expectedRevision?: number) {
  return validContent(command.content) && value.lifecycle === (action === 'submit' ? 'SUBMITTED' : action === 'complete' ? 'COMPLETED' : 'DRAFT') &&
    (expectedRevision === undefined || value.revision === expectedRevision) && canonical(content(value.content)) === canonical(content(command.content)) &&
    canonical(value.file) === canonical(command.file) && pins(value.requirements) === pins(command.requirements) && pins(value.links) === pins(command.links)
}
export async function save(vessel: string, kind: Kind, id: string | null, frozen: { body: string; key: string; action?: 'submit' | 'complete'; expectedRevision?: number }, csrf: string, signal: AbortSignal): Promise<Row> {
  const value = decodeRow(await request<unknown>(`${route(kind)}${id ? `/${id}` : ''}${frozen.action ? `/${frozen.action}` : ''}?vesselId=${vessel}`, signal, { method: frozen.action ? 'POST' : id ? 'PUT' : 'POST', body: frozen.body, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': frozen.key, 'X-CSRF-TOKEN': csrf } }), vessel, kind)
  if (id !== null && value.id !== id) return invalid()
  const sent = JSON.parse(frozen.body) as Command
  if (!receiptMatches(value, sent, frozen.action, frozen.expectedRevision ?? (id === null ? 1 : undefined))) return invalid()
  return value
}
export async function owners(vessel: string, signal: AbortSignal): Promise<{ actorId: string; roleId: string }[]> {
  const v = await request<unknown>(`controls/owners?vesselId=${vessel}`, signal)
  if (!exact(v, ['items']) || !Array.isArray(v.items) || v.items.length > 1000 || v.items.some(x => !exact(x, ['actorId', 'roleId']) || !isGuid(x.actorId) || !isGuid(x.roleId))) return invalid()
  return v.items as { actorId: string; roleId: string }[]
}
export type OrphanReason = 'CRITICALITY_UNKNOWN' | 'REQUIREMENT_LINK_MISSING' | 'OWNER_MISSING'
export type ControlState = 'DRAFT' | 'IN_REVIEW' | 'APPROVED' | 'PUBLISHED' | 'RETIRED'
// revision/controlState: the effective revision judged (the published one while a successor is pending).
export type CriticalOrphan = { id: string; revision: number; controlState: ControlState; successorPending: boolean; reasons: OrphanReason[] }
const controlStates: readonly string[] = ['DRAFT', 'IN_REVIEW', 'APPROVED', 'PUBLISHED', 'RETIRED']
const orphanReasons: readonly string[] = ['CRITICALITY_UNKNOWN', 'REQUIREMENT_LINK_MISSING', 'OWNER_MISSING']
function criticalOrphan(v: unknown): v is CriticalOrphan {
  return exact(v, ['id', 'revision', 'controlState', 'successorPending', 'reasons']) && isGuid(v.id) && positive(v.revision) && typeof v.controlState === 'string' && controlStates.includes(v.controlState) && v.controlState !== 'RETIRED' &&
    typeof v.successorPending === 'boolean' && (!v.successorPending || v.controlState === 'PUBLISHED') &&
    Array.isArray(v.reasons) && v.reasons.length > 0 && v.reasons.length <= 3 && v.reasons.every(r => typeof r === 'string' && orphanReasons.includes(r)) && new Set(v.reasons).size === v.reasons.length
}
// DEV-028: criticalOrphans is null only when an older server omits it; absence is never "no orphans".
export async function orphans(vessel: string, offset: number, signal: AbortSignal): Promise<{ draftCriticalOrphans: string[]; criticalOrphans: CriticalOrphan[] | null; hasMore: boolean; available: boolean; reason: string; uncontrolledObligations: { ruleId: string; ruleRevision: number; requirementId: string; requirementRevision: number }[] }> {
  const v = await request<unknown>(`controls/orphans?vesselId=${vessel}&offset=${offset}`, signal)
  const keys = ['available', 'reason', 'draftCriticalOrphans', 'uncontrolledObligations', 'offset', 'hasMore']
  const extended = !!v && typeof v === 'object' && Object.hasOwn(v, 'criticalOrphans')
  if (!exact(v, extended ? [...keys, 'criticalOrphans'] : keys) || typeof v.available !== 'boolean' || typeof v.reason !== 'string' || v.available && v.reason !== 'QUALIFIED_CATALOG_SCOPE_ONLY' || !Array.isArray(v.uncontrolledObligations) || v.uncontrolledObligations.length > 25 || v.uncontrolledObligations.some(o => !exact(o, ['ruleId', 'ruleRevision', 'requirementId', 'requirementRevision']) || !isGuid(o.ruleId) || !positive(o.ruleRevision) || !isGuid(o.requirementId) || !positive(o.requirementRevision)) || v.offset !== offset || typeof v.hasMore !== 'boolean' || !Array.isArray(v.draftCriticalOrphans) || v.draftCriticalOrphans.length > 25 || !v.draftCriticalOrphans.every(isGuid)) return invalid()
  if (extended && (!Array.isArray(v.criticalOrphans) || v.criticalOrphans.length > 25 || !v.criticalOrphans.every(criticalOrphan))) return invalid()
  return { draftCriticalOrphans: v.draftCriticalOrphans as string[], criticalOrphans: extended ? v.criticalOrphans as CriticalOrphan[] : null, hasMore: v.hasMore, available: v.available, reason: v.reason, uncontrolledObligations: v.uncontrolledObligations as { ruleId: string; ruleRevision: number; requirementId: string; requirementRevision: number }[] }
}
export type LifecycleReason = 'SUBMIT_FOR_REVIEW' | 'RETURN_FOR_CORRECTION' | 'APPROVE' | 'PUBLISH' | 'RETIRE'
export type LifecycleEvent = { sequence: number; controlRevision: number; fromState: ControlState; toState: ControlState; reasonCode: LifecycleReason | 'CONTENT_REVISED'; supersedesRevision: number | null; actorId: string; rationale: string | null; savedAt: string }
// 62.01 section 6 dual pointer: revision/state describe the candidate (latest) revision; publishedRevision is the
// effective published revision, which stays effective while a successor is drafted, reviewed or approved.
export type Lifecycle = { id: string; vesselId: string; revision: number; version: number; state: ControlState; publishedRevision: number | null; successorPending: boolean; effectiveness: 'UNKNOWN'; events: LifecycleEvent[]; offset: number; hasMore: boolean; canWrite: boolean; canReview: boolean }
// The pointer must agree with the candidate state; anything else is an invalid response, never a guess.
function pointer(state: string, revision: number, published: unknown, pending: unknown): boolean {
  if (typeof pending !== 'boolean' || published !== null && (!positive(published) || published > revision)) return false
  if (pending !== (published !== null && published !== revision)) return false
  if (state === 'PUBLISHED') return published === revision
  if (state === 'RETIRED') return published === null
  return true
}
const lifecycleReasons: readonly string[] = ['SUBMIT_FOR_REVIEW', 'RETURN_FOR_CORRECTION', 'APPROVE', 'PUBLISH', 'RETIRE', 'CONTENT_REVISED']
function lifecycleEvent(v: unknown): v is LifecycleEvent {
  return exact(v, ['sequence', 'controlRevision', 'fromState', 'toState', 'reasonCode', 'supersedesRevision', 'actorId', 'rationale', 'savedAt']) && positive(v.sequence) && positive(v.controlRevision) &&
    (v.supersedesRevision === null || v.reasonCode === 'PUBLISH' && positive(v.supersedesRevision) && v.supersedesRevision < v.controlRevision) &&
    typeof v.fromState === 'string' && controlStates.includes(v.fromState) && v.fromState !== 'RETIRED' && typeof v.toState === 'string' && controlStates.includes(v.toState) &&
    typeof v.reasonCode === 'string' && lifecycleReasons.includes(v.reasonCode) && isGuid(v.actorId) && (v.rationale === null || typeof v.rationale === 'string' && v.rationale.length <= 2000) && timestamp(v.savedAt)
}
// The definition lifecycle is never completion, verification or effectiveness.
export async function lifecycle(vessel: string, id: string, offset: number, signal: AbortSignal): Promise<Lifecycle> {
  const v = await request<unknown>(`controls/${id}/lifecycle?vesselId=${vessel}&offset=${offset}`, signal)
  if (!exact(v, ['id', 'vesselId', 'revision', 'version', 'state', 'publishedRevision', 'successorPending', 'effectiveness', 'events', 'offset', 'hasMore', 'canWrite', 'canReview']) || v.id !== id || v.vesselId !== vessel || !positive(v.revision) || !version(v.version) ||
    typeof v.state !== 'string' || !controlStates.includes(v.state) || !pointer(v.state, v.revision, v.publishedRevision, v.successorPending) || v.effectiveness !== 'UNKNOWN' || !Array.isArray(v.events) || v.events.length > 25 || !v.events.every(lifecycleEvent) ||
    v.offset !== offset || typeof v.hasMore !== 'boolean' || v.hasMore && v.events.length !== 25 || typeof v.canWrite !== 'boolean' || typeof v.canReview !== 'boolean') return invalid()
  return v as Lifecycle
}
export type LifecycleCommand = { expectedVersion: number; revision: number; reasonCode: LifecycleReason; rationale: string }
export async function transition(vessel: string, id: string, frozen: { body: string; key: string }, csrf: string, signal: AbortSignal): Promise<{ state: ControlState; version: number; revision: number; publishedRevision: number | null; event: LifecycleEvent }> {
  const v = await request<unknown>(`controls/${id}/lifecycle?vesselId=${vessel}`, signal, { method: 'POST', body: frozen.body, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': frozen.key, 'X-CSRF-TOKEN': csrf } })
  const sent = JSON.parse(frozen.body) as LifecycleCommand
  // RETIRE names the effective published revision, which may precede a pending successor candidate.
  if (!exact(v, ['id', 'vesselId', 'revision', 'version', 'state', 'publishedRevision', 'successorPending', 'effectiveness', 'event']) || v.id !== id || v.vesselId !== vessel || !positive(v.revision) ||
    (sent.reasonCode === 'RETIRE' ? v.revision < sent.revision : v.revision !== sent.revision) || !version(v.version) ||
    typeof v.state !== 'string' || !controlStates.includes(v.state) || !pointer(v.state, v.revision, v.publishedRevision, v.successorPending) || v.effectiveness !== 'UNKNOWN' || !lifecycleEvent(v.event) || v.event.reasonCode !== sent.reasonCode || v.event.toState !== v.state || v.event.controlRevision !== sent.revision) return invalid()
  return { state: v.state as ControlState, version: v.version, revision: v.revision, publishedRevision: v.publishedRevision as number | null, event: v.event }
}
export type SupportingEvidence = { evidence: Row; pinnedControlRevision: number; pinCurrent: boolean }
// controlRevision is the effective control revision (published while a successor is pending); latestRevision the candidate.
export async function supportingEvidence(vessel: string, id: string, offset: number, signal: AbortSignal): Promise<{ controlRevision: number; latestRevision: number; successorPending: boolean; items: SupportingEvidence[]; hasMore: boolean }> {
  const v = await request<unknown>(`controls/${id}/supporting-evidence?vesselId=${vessel}&offset=${offset}`, signal)
  if (!exact(v, ['controlId', 'vesselId', 'controlRevision', 'latestRevision', 'successorPending', 'items', 'offset', 'hasMore']) || v.controlId !== id || v.vesselId !== vessel || !positive(v.controlRevision) ||
    !positive(v.latestRevision) || v.controlRevision > v.latestRevision || v.successorPending !== (v.controlRevision !== v.latestRevision) || v.offset !== offset ||
    typeof v.hasMore !== 'boolean' || !Array.isArray(v.items) || v.items.length > 25) return invalid()
  const controlRevision = v.controlRevision
  const items = v.items.map(item => {
    if (!exact(item, ['evidence', 'pinnedControlRevision', 'pinCurrent']) || !positive(item.pinnedControlRevision) || typeof item.pinCurrent !== 'boolean' || item.pinCurrent !== (item.pinnedControlRevision === controlRevision)) return invalid()
    const evidence = decodeRow(item.evidence, vessel, 'Evidence')
    if (!evidence.links.some(link => link.kind === 'Control' && link.id === id && link.revision === item.pinnedControlRevision)) return invalid()
    return { evidence, pinnedControlRevision: item.pinnedControlRevision, pinCurrent: item.pinCurrent }
  })
  return { controlRevision, latestRevision: v.latestRevision, successorPending: v.successorPending, items, hasMore: v.hasMore }
}
