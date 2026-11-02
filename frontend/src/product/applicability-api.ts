import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { calendar, timestamp } from './date-validation'
import { validDecimal } from './profile-api'

export const ruleContract = '09.03:309afa07b393fb96556a0ea4ec70098b1e2b8648ab6daec181f5a81ce9ac8825'
export const lifecycleStates = ['DRAFT', 'ADOPTED', 'AWAITING', 'FUTURE_EFFECTIVE', 'IN_FORCE', 'SUPERSEDED'] as const
type Obj = Record<string, unknown>
const obj = (v: unknown): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Obj => obj(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const integer = (v: unknown, min = 1) => Number.isSafeInteger(v) && Number(v) >= min
const hash = (v: unknown) => typeof v === 'string' && /^[0-9A-F]{64}$/.test(v)
const invalid = (): never => { throw new ApiError(502, 'invalid_applicability_contract') }
export type Capabilities = { canRead: boolean; canWrite: boolean; canReview: boolean; canEvaluate: boolean; canActivate: false; contract: string }
export type RuleVersion = { id: string; revision: number; contract: string; ruleJson: string | null; rule: Obj | null; hash: string; sourceId: string | null; sourceRevision: number | null; requirementId: string | null; requirementRevision: number | null; lifecycle: 'DRAFT'; requestedLifecycle: string; reviewStatus: 'NOT_REQUESTED' | 'BLOCKED_SOURCE_ADMISSION'; savedAt: string }
export type RuleDraft = { version: number; current: RuleVersion }
export type Page<T> = { items: T[]; offset: number; limit: number; hasMore: boolean }
export type Reason = { path: string; truth: 0 | 1 | 2; reason: string; field: string | null; operator: string | null; children: Reason[]; reviewRequired: boolean }
export type Prediction = { result: string; currentResult: string; authoritative: boolean; lifecycle: string; reason: Reason; predictedActivations: { type: string; id: string }[]; blockers: string[] }
export type EvaluationResult = { ruleId: string; revision: number; hash: string; sourceId: string | null; sourceRevision: number | null; requirementId: string | null; requirementRevision: number | null; prediction: Prediction }
export type Evaluation = { id: string; vesselId: string; savedAt: string; asOf: string; adapterVersion: string; profileRevision: number | null; snapshotHash: string; snapshot: Obj; ruleSetHash: string; results: EvaluationResult[]; conflicts: { code: string; target: string; ruleReferences: string[]; status: 'REVIEW_REQUIRED' }[]; preview: boolean; authoritative: boolean; currentResult: string; simulation: boolean }
const link = (id: unknown, rev: unknown) => id === null && rev === null || isGuid(id) && integer(rev)
const factTypes: Record<string, number> = {
  'context.mode': 0, evaluationDate: 2, 'vessel.flag': 0, 'vessel.type': 0, 'vessel.gt': 1, 'vessel.mainPropulsionKw': 1,
  'vessel.buildDate': 2, 'vessel.contractForConstructionDate': 2, 'vessel.classSociety': 0, 'vessel.tradingArea': 0, 'vessel.operatingArea': 0,
  'operation.jobType': 0, 'operation.profile': 0, 'operation.serviceContext': 0, 'cargo.packagedDangerousGoods': 3, 'cargo.type': 0,
  'equipment.towingWinchFitted': 3, 'class.rulePack.IACS_M79': 3, 'regulatory.ismApplicable': 3, 'regulatory.msmdVerified': 3,
  'crew.requiredRoleEligible': 3, 'evidence.sourceVerified': 3, 'source.lifecycle': 0,
}
function owningProvenance(v: unknown): boolean {
  if (!exact(v, ['id', 'revision', 'hash', 'fileId', 'fileRevision', 'checksum', 'documentKind', 'documentHeadRevision', 'job', 'plan', 'asset', 'assignmentHash', 'jobId', 'jobRevision', 'planRevision', 'assetId', 'assetRevision', 'serviceId', 'serviceRevision', 'reviews']) || !isGuid(v.id) || !integer(v.revision) || !hash(v.hash) || !integer(v.documentHeadRevision) || !hash(v.assignmentHash) || !['Operation', 'ServiceAuthorization', 'CargoPlan', 'WinchFitment'].includes(v.documentKind as string)) return false
  if (!(v.fileId === null && v.fileRevision === null && v.checksum === null || isGuid(v.fileId) && integer(v.fileRevision) && hash(v.checksum))) return false
  for (const k of ['job', 'plan', 'asset']) { const dependency = v[k]; if (dependency !== null && (!exact(dependency, ['revision', 'version']) || !integer(dependency.revision) || !integer(dependency.version) || Number(dependency.version) > 4294967295)) return false }
  for (const [id, revision] of [['jobId', 'jobRevision'], ['assetId', 'assetRevision'], ['serviceId', 'serviceRevision']]) if (!(v[id] === null && v[revision] === null || isGuid(v[id]) && integer(v[revision]))) return false
  if (v.planRevision !== null && (!integer(v.planRevision) || v.jobId === null)) return false
  return Array.isArray(v.reviews) && v.reviews.length <= 128 && v.reviews.every(e => exact(e, ['id', 'revision', 'state', 'authorityHash', 'contextHash', 'validUntil']) && isGuid(e.id) && integer(e.revision) && typeof e.state === 'string' && e.state.length <= 40 && hash(e.authorityHash) && hash(e.contextHash) && (e.validUntil === null || timestamp(e.validUntil)))
}
function snapshot(v: unknown, vessel: unknown, asOf: unknown, profile: unknown, authoritative = false): boolean {
  if (!exact(v, ['adapterVersion', 'vesselId', 'vesselVersion', 'profileRevision', 'asOf', 'facts', ...(authoritative ? ['qualifications', 'vesselContextHash'] : []), ...(obj(v) && Object.hasOwn(v, 'owningInputs') ? ['owningInputs'] : []), ...(obj(v) && Object.hasOwn(v, 'owningProvenance') ? ['owningProvenance'] : [])]) || v.adapterVersion !== 'controlled-input-catalog-v1' || v.vesselId !== vessel || v.asOf !== asOf || v.profileRevision !== profile || !integer(v.vesselVersion) || !exact(v.facts, Object.keys(factTypes))) return false
  if (v.owningProvenance !== undefined && (!Array.isArray(v.owningProvenance) || v.owningProvenance.length > 4 || !v.owningProvenance.every(owningProvenance))) return false
  if (v.owningInputs != null && (!exact(v.owningInputs, ['operationId', 'cargoPlanId', 'winchFitmentId']) || Object.values(v.owningInputs).some(id => id !== null && !isGuid(id)))) return false
  if (authoritative && (!hash(v.vesselContextHash) || !Array.isArray(v.qualifications) || v.qualifications.length < 1 || v.qualifications.length > 64 || v.qualifications.some(q => !exact(q, ['ruleId', 'revision', 'hash', 'sourceHash', 'policyVersion', 'attestationId', 'facts']) || !isGuid(q.ruleId) || !integer(q.revision) || !hash(q.hash) || !hash(q.sourceHash) || q.policyVersion !== 'recorded-qualification-v1' || q.attestationId !== null && !isGuid(q.attestationId) || !snapshot({ adapterVersion: v.adapterVersion, vesselId: vessel, vesselVersion: v.vesselVersion, profileRevision: profile, asOf, facts: q.facts }, vessel, asOf, profile)))) return false
  return Object.entries(v.facts).every(([key, f]) => {
    if (!exact(f, ['type', 'knowledge', 'value', 'provenance', 'revision', 'observedAt', 'freshUntil', 'authoritativeEmpty']) || f.type !== factTypes[key] ||
      typeof f.knowledge !== 'string' || !['Known', 'Missing', 'Unknown', 'Stale', 'Unverified', 'Conflicting'].includes(f.knowledge) ||
      typeof f.provenance !== 'string' || f.provenance.length > 100 || typeof f.revision !== 'string' || f.revision.length > 100 ||
      f.observedAt !== null && !timestamp(f.observedAt) || f.freshUntil !== null && !timestamp(f.freshUntil) || typeof f.authoritativeEmpty !== 'boolean') return false
    if (f.value === null) return f.knowledge !== 'Known'
    return f.type === 3 ? typeof f.value === 'boolean' : f.type === 1 ? validDecimal(f.value) : f.type === 2 ? typeof f.value === 'string' && calendar(f.value) : typeof f.value === 'string' && f.value.length <= 500
  })
}
export function decodeRule(value: unknown): RuleDraft {
  if (!exact(value, ['version', 'current']) || !integer(value.version) || Number(value.version) > 4294967295) return invalid()
  const r = value.current
  if (!exact(r, ['id', 'revision', 'contract', 'ruleJson', 'rule', 'hash', 'sourceId', 'sourceRevision', 'requirementId', 'requirementRevision', 'lifecycle', 'requestedLifecycle', 'reviewStatus', 'savedAt']) ||
    !isGuid(r.id) || !integer(r.revision) || r.contract !== ruleContract || r.rule !== null && (!obj(r.rule) || JSON.stringify(r.rule).length > 32768) || !hash(r.hash) ||
    !link(r.sourceId, r.sourceRevision) || !link(r.requirementId, r.requirementRevision) || r.lifecycle !== 'DRAFT' ||
    typeof r.requestedLifecycle !== 'string' || !(lifecycleStates as readonly string[]).includes(r.requestedLifecycle) ||
    r.reviewStatus !== 'NOT_REQUESTED' && r.reviewStatus !== 'BLOCKED_SOURCE_ADMISSION' || !timestamp(r.savedAt)) return invalid()
  if (r.rule === null ? r.ruleJson !== null : typeof r.ruleJson !== 'string' || r.ruleJson.length > 32768) return invalid()
  if (r.ruleJson !== null) {
    try { if (JSON.stringify(JSON.parse(r.ruleJson as string)) !== JSON.stringify(r.rule)) return invalid() } catch { return invalid() }
  }
  return value as RuleDraft
}
export function decodeReason(value: unknown, depth = 0, budget = { nodes: 0 }): Reason {
  if (depth > 12 || ++budget.nodes > 512 || !exact(value, ['path', 'truth', 'reason', 'field', 'operator', 'children', 'reviewRequired']) ||
    typeof value.path !== 'string' || value.path.length > 500 || ![0, 1, 2].includes(value.truth as number) || typeof value.reason !== 'string' || value.reason.length > 100 ||
    value.field !== null && typeof value.field !== 'string' || value.operator !== null && typeof value.operator !== 'string' ||
    typeof value.reviewRequired !== 'boolean' || !Array.isArray(value.children) || value.children.length > 40) return invalid()
  value.children.forEach(v => decodeReason(v, depth + 1, budget)); return value as Reason
}
export function decodeEvaluation(value: unknown): Evaluation {
  if (!exact(value, ['id', 'vesselId', 'savedAt', 'asOf', 'adapterVersion', 'profileRevision', 'snapshotHash', 'snapshot', 'ruleSetHash', 'results', 'conflicts', 'preview', 'authoritative', 'currentResult', 'simulation']) ||
    !isGuid(value.id) || !isGuid(value.vesselId) || !timestamp(value.savedAt) || !timestamp(value.asOf) || value.adapterVersion !== 'controlled-input-catalog-v1' ||
    value.profileRevision !== null && !integer(value.profileRevision) || !hash(value.snapshotHash) || !hash(value.ruleSetHash) || !obj(value.snapshot) ||
    JSON.stringify(value.snapshot).length > 65536 || !Array.isArray(value.results) || value.results.length > 64 || typeof value.authoritative !== 'boolean' || value.preview !== !value.authoritative || value.simulation !== !value.authoritative || (value.authoritative ? !['CURRENT', 'REVIEW_REQUIRED'].includes(value.currentResult as string) : value.currentResult !== 'REVIEW_REQUIRED')) return invalid()
  if (!snapshot(value.snapshot, value.vesselId, value.asOf, value.profileRevision, value.authoritative as boolean)) return invalid()
  if (!Array.isArray(value.conflicts) || value.conflicts.length > 4096 || value.conflicts.some(c => !exact(c, ['code', 'target', 'ruleReferences', 'status']) || !['CANDIDATE_SOURCE_CONTRADICTION', 'INPUT_SOURCE_CONFLICT'].includes(c.code as string) || typeof c.target !== 'string' || c.target.length > 500 || c.status !== 'REVIEW_REQUIRED' || !Array.isArray(c.ruleReferences) || c.ruleReferences.length > 64 || c.ruleReferences.some(r => typeof r !== 'string' || !/^[0-9a-f-]{36}\/[1-9][0-9]*$/.test(r)))) return invalid()
  for (const r of value.results) {
    if (!exact(r, ['ruleId', 'revision', 'hash', 'sourceId', 'sourceRevision', 'requirementId', 'requirementRevision', 'prediction']) || !isGuid(r.ruleId) || !integer(r.revision) || !hash(r.hash) || !link(r.sourceId, r.sourceRevision) || !link(r.requirementId, r.requirementRevision)) return invalid()
    const p = r.prediction
    if (!exact(p, ['result', 'currentResult', 'authoritative', 'lifecycle', 'reason', 'predictedActivations', 'blockers']) ||
      typeof p.result !== 'string' || !['APPLICABLE', 'NOT_APPLICABLE', 'REVIEW_REQUIRED', 'NOT_CURRENT'].includes(p.result) || p.authoritative !== value.authoritative || (value.authoritative ? p.currentResult !== p.result : p.currentResult !== 'REVIEW_REQUIRED') ||
      typeof p.lifecycle !== 'string' || !(lifecycleStates as readonly string[]).includes(p.lifecycle) || !Array.isArray(p.blockers) || p.blockers.length > 32 || p.blockers.some(v => typeof v !== 'string' || v.length > 100) ||
      !Array.isArray(p.predictedActivations) || p.predictedActivations.length > 64 || p.predictedActivations.some(a => !exact(a, ['type', 'id']) || typeof a.type !== 'string' || typeof a.id !== 'string')) return invalid()
    decodeReason(p.reason)
  }
  return value as Evaluation
}
export function decodePage<T>(value: unknown, decode: (v: unknown) => T): Page<T> {
  if (!exact(value, ['items', 'offset', 'limit', 'hasMore']) || !Array.isArray(value.items) || value.items.length > 25 || !integer(value.offset, 0) || value.limit !== 25 || typeof value.hasMore !== 'boolean') return invalid()
  return { items: value.items.map(decode), offset: value.offset as number, limit: 25, hasMore: value.hasMore }
}
export const capabilities = async (signal: AbortSignal) => {
  const v = await request<unknown>('rules/capabilities', signal)
  if (!exact(v, ['canRead', 'canWrite', 'canReview', 'canEvaluate', 'canActivate', 'contract']) || ['canRead', 'canWrite', 'canReview', 'canEvaluate'].some(k => typeof v[k] !== 'boolean') || v.canActivate !== false || v.contract !== ruleContract) return invalid()
  return v as Capabilities
}
export const rules = async (offset: number, signal: AbortSignal) => decodePage(await request<unknown>(`rules?offset=${offset}`, signal), decodeRule)
export const versions = async (id: string, offset: number, signal: AbortSignal) => {
  if (!isGuid(id)) return invalid()
  return decodePage(await request<unknown>(`rules/${id}/versions?offset=${offset}`, signal), decodeRule)
}
export type FrozenCommand = { key: string; path: string; method: 'POST' | 'PUT'; body: string; kind: 'author' | 'review' | 'evaluate' }
export const send = async (command: FrozenCommand, csrf: string, signal: AbortSignal) => {
  const v = await request<unknown>(command.path, signal, { method: command.method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: command.body })
  return command.kind === 'evaluate' ? decodeEvaluation(v) : decodeRule(v)
}
export const evaluation = async (id: string, signal: AbortSignal) => {
  if (!isGuid(id)) return invalid()
  const v = decodeEvaluation(await request<unknown>(`applicability/evaluations/${id}`, signal)); if (v.id !== id) return invalid(); return v
}
export type HistoryItem = { id: string; vesselId: string; savedAt: string; asOf: string; snapshotHash: string; ruleSetHash: string }
export const history = async (id: string, offset: number, signal: AbortSignal) => {
  if (!isGuid(id)) return invalid()
  return decodePage(await request<unknown>(`applicability/evaluations?vesselId=${id}&offset=${offset}`, signal), (v): HistoryItem => {
    if (!exact(v, ['id', 'vesselId', 'savedAt', 'asOf', 'snapshotHash', 'ruleSetHash']) || !isGuid(v.id) || v.vesselId !== id || !timestamp(v.savedAt) || !timestamp(v.asOf) || !hash(v.snapshotHash) || !hash(v.ruleSetHash)) return invalid()
    return v as HistoryItem
  })
}
