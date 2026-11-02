import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

export type Anchor = { vesselId: string; evidenceId: string; evidenceRevision: number; controlId: string; controlRevision: number }
export type RecordingPolicy = { decisionType: string; subjectType: 'Control' | 'Evidence'; subjectId: string; authorityRole: 'Master' | 'Tug Master' | 'DPA'; semantics: 'HumanRecording'; sourceLocator: string; requiredGate: string | null; gateSourceLocator: string | null }
export type DecisionRecording = { decisionType: string; subjectType: 'Control' | 'Evidence'; subjectId: string; humanDecision: string; triggerSource: string; decisionContext: string }
export type RecordedDecision = { content: DecisionRecording; policy: RecordingPolicy; qualification: 'UNASSESSED'; profileRevision: number | null }
export type Authority = { id: string; operationId: string; roleId: string; capability: 'human.review.submit' | 'human.verify' | 'human.decide'; validUntil: string; recording: RecordingPolicy | null }
export type Review = Anchor & { id: string; revision: number; version: number; kind: 'Verification' | 'Decision'; state: string; currentState: string; evaluationId: string | null; verificationId: string | null; verificationRevision: number | null; actorId: string; roleId: string; authorityId: string; operationId: string; reasonCode: string; rationale: string; authorityHash: string; contextHash: string; savedAt: string; validUntil: string | null; asOf: string; recording: RecordedDecision | null }
export type Segment = { kind: string; id: string | null; revision: number | null; availability: 'Available' | 'Unavailable'; state: string; why: string; hash: string | null; savedAt: string | null }
export type AuditTraceEvent = { id: string; revision: number; kind: string; state: string; currentState: string; savedAt: string; validUntil: string | null; evaluationId: string | null; verificationId: string | null; verificationRevision: number | null; authorityId: string; roleId: string; authorityHash: string; contextHash: string }
export type Trace = Anchor & { evaluationId?: string | null; auditItems?: AuditTraceEvent[]; auditHasMore?: boolean; profileRevision: number | null; evidenceVersion: number; controlVersion: number; asOf: string; historical: boolean; disposition: 'REVIEW_REQUIRED' | 'RECORDED_CHAIN'; complete: boolean; segments: Segment[]; items: Review[]; offset: number; hasMore: boolean; blockers: string[]; authorities: Authority[]; evidenceContext: { provenance: string | null; observedAt: string | null; validUntil: string | null; freshUntil: string | null; timeState: string } }
type Obj = Record<string, unknown>
const exact = (v: unknown, keys: string[]): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const positive = (v: unknown): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const version = (v: unknown) => positive(v) && v <= 4294967295
const hash = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$(?![\s\S])/.test(v)
const text = (v: unknown): v is string => typeof v === 'string' && v.length > 0 && v.length <= 2000 && !/[\u0000-\u001f\u007f-\u009f]/.test(v)
const fail = (): never => { throw new ApiError(502, 'invalid_contract') }
const anchorKeys = ['vesselId', 'evidenceId', 'evidenceRevision', 'controlId', 'controlRevision']
const states = ['PENDING', 'VERIFIED', 'REJECTED', 'EXPIRED', 'SUPERSEDED', 'RECORDED']
const reasons = ['REQUEST_REVIEW', 'HUMAN_VERIFIED', 'HUMAN_REJECTED', 'VALIDITY_ELAPSED', 'INPUT_SUPERSEDED', 'HUMAN_DECIDED']
const same = (v: Obj, a: Anchor) => anchorKeys.every(k => v[k] === a[k as keyof Anchor])
const policy = (p: unknown, a: Anchor): p is RecordingPolicy => exact(p, ['decisionType', 'subjectType', 'subjectId', 'authorityRole', 'semantics', 'sourceLocator', 'requiredGate', 'gateSourceLocator']) && text(p.decisionType) &&
  (p.subjectType === 'Control' && p.subjectId === a.controlId || p.subjectType === 'Evidence' && p.subjectId === a.evidenceId) && ['Master', 'Tug Master', 'DPA'].includes(p.authorityRole as string) && p.semantics === 'HumanRecording' &&
  p.sourceLocator === '15.01/paragraph:99;18.01/Backlog/row:32' && (p.requiredGate === null || text(p.requiredGate)) && (p.gateSourceLocator === null || text(p.gateSourceLocator))
const recorded = (r: unknown, a: Anchor): r is RecordedDecision => {
  if (!exact(r, ['content', 'policy', 'qualification', 'profileRevision']) || r.qualification !== 'UNASSESSED' || r.profileRevision !== null && !positive(r.profileRevision) || !policy(r.policy, a) || r.policy.requiredGate !== null) return false
  const c = r.content
  return exact(c, ['decisionType', 'subjectType', 'subjectId', 'humanDecision', 'triggerSource', 'decisionContext']) && c.decisionType === r.policy.decisionType && c.subjectType === r.policy.subjectType && c.subjectId === r.policy.subjectId && [c.humanDecision, c.triggerSource, c.decisionContext].every(text)
}
export function decodeReview(v: unknown, a: Anchor): Review {
  // Immutable predecessor receipts predate the nullable recording extension.
  if (v && typeof v === 'object' && !Array.isArray(v) && !Object.hasOwn(v, 'recording') && (v as Obj).state !== 'RECORDED') v = { ...v, recording: null }
  if (!exact(v, [...anchorKeys, 'id', 'revision', 'version', 'kind', 'state', 'currentState', 'evaluationId', 'verificationId', 'verificationRevision', 'actorId', 'roleId', 'authorityId', 'operationId', 'reasonCode', 'rationale', 'authorityHash', 'contextHash', 'savedAt', 'validUntil', 'asOf', 'recording']) || !same(v, a) || !isGuid(v.id) || !positive(v.revision) || !version(v.version) || !['Verification', 'Decision'].includes(v.kind as string) || !states.includes(v.state as string) || ![...states, 'EXPIRED_REVIEW_REQUIRED', 'SUPERSEDED_REVIEW_REQUIRED'].includes(v.currentState as string) || !isGuid(v.actorId) || !isGuid(v.roleId) || !isGuid(v.authorityId) || !isGuid(v.operationId) || !reasons.includes(v.reasonCode as string) || !text(v.rationale) || !hash(v.authorityHash) || !hash(v.contextHash) || !timestamp(v.savedAt) || !timestamp(v.asOf) || Date.parse(v.savedAt) > Date.parse(v.asOf) || v.validUntil !== null && !timestamp(v.validUntil) || v.evaluationId !== null && !isGuid(v.evaluationId)) return fail()
  if (v.kind === 'Decision' ? !['PENDING', 'REJECTED', 'RECORDED'].includes(v.state as string) || !isGuid(v.verificationId) || !positive(v.verificationRevision) : v.verificationId !== null || v.verificationRevision !== null) return fail()
  if (v.state === 'RECORDED' ? v.kind !== 'Decision' || v.reasonCode !== 'HUMAN_DECIDED' || !recorded(v.recording, a) : v.recording !== null) return fail()
  if (v.currentState !== v.state && v.currentState !== 'SUPERSEDED_REVIEW_REQUIRED' && !(v.state === 'VERIFIED' && v.currentState === 'EXPIRED_REVIEW_REQUIRED')) return fail()
  if (v.state === 'VERIFIED' && v.validUntil === null) return fail()
  return v as Review
}
export function decodeTrace(v: unknown, a: Anchor, offset: number): Trace {
  if (!exact(v, [...anchorKeys, 'evidenceVersion', 'controlVersion', 'asOf', 'historical', 'disposition', 'complete', 'segments', 'items', 'offset', 'hasMore', 'blockers', 'authorities', 'evidenceContext', 'profileRevision', ...(v && typeof v === 'object' && Object.hasOwn(v, 'auditItems') ? ['auditItems', 'auditHasMore'] : []), ...(v && typeof v === 'object' && Object.hasOwn(v, 'evaluationId') ? ['evaluationId'] : [])]) || !same(v, a) || v.profileRevision !== null && !positive(v.profileRevision) || !version(v.evidenceVersion) || !version(v.controlVersion) || !timestamp(v.asOf) || typeof v.historical !== 'boolean' || typeof v.complete !== 'boolean' || v.disposition !== (v.complete ? 'RECORDED_CHAIN' : 'REVIEW_REQUIRED') || v.offset !== offset || typeof v.hasMore !== 'boolean' || !Array.isArray(v.items) || v.items.length > 25 || v.hasMore && v.items.length !== 25 || !Array.isArray(v.segments) || v.segments.length > 125 || !Array.isArray(v.blockers) || !Array.isArray(v.authorities) || v.authorities.length > 1000) return fail()
  if (!v.complete && v.blockers.length === 0) return fail()
  const context = v.evidenceContext
  if (!exact(context, ['provenance', 'observedAt', 'validUntil', 'freshUntil', 'timeState']) || context.provenance !== null && !text(context.provenance) || !['UNKNOWN', 'SUPERSEDED', 'DECLARED_NOT_YET_VALID', 'DECLARED_EXPIRED', 'DECLARED_STALE'].includes(context.timeState as string)) return fail()
  for (const k of ['observedAt', 'validUntil', 'freshUntil']) if (context[k] !== null && !timestamp(context[k])) return fail()
  const blockerCodes = ['SOURCE_ADMISSION_UNAVAILABLE', 'AUTHORITATIVE_EVALUATION_UNAVAILABLE', 'EXTERNAL_SIGNATURE_UNAVAILABLE', 'REQUIREMENT_LINK_MISSING', 'QUALIFICATION_LINEAGE_UNAVAILABLE', 'PROFILE_CHANGED', 'VESSEL_CHANGED', 'RULE_CHANGED', 'QUALIFICATION_CHANGED', 'SOURCE_REVIEW_REQUIRED', 'SOURCE_NOT_CURRENT', 'ATTESTATION_EXPIRED', 'INPUT_STALE', 'CONTROL_COMPLETION_PENDING', 'VERIFICATION_PENDING_OR_STALE', 'DECISION_RECORDING_PENDING']
  if (v.blockers.some(x => !blockerCodes.includes(x)) || v.historical && v.authorities.length > 0) return fail()
  for (const s of v.segments) {
    if (!exact(s, ['kind', 'id', 'revision', 'availability', 'state', 'why', 'hash', 'savedAt']) || !['Source', 'Requirement', 'Rule', 'Applicability', 'Control', 'Evidence', 'Evaluation'].includes(s.kind as string) || !['Available', 'Unavailable'].includes(s.availability as string) || !['DRAFT', 'ADOPTED', 'AWAITING', 'FUTURE_EFFECTIVE', 'IN_FORCE', 'SUPERSEDED', 'SUBMITTED', 'COMPLETED', 'UNKNOWN', 'REVIEW_REQUIRED', 'RETAINED'].includes(s.state as string) || !['REVISION_UNAVAILABLE', 'SOURCE_ADMISSION_UNAVAILABLE', 'AUTHORITATIVE_EVALUATION_UNAVAILABLE', 'COMPLETION_AND_EFFECTIVENESS_UNKNOWN', 'EXISTENCE_IS_NOT_VERIFICATION', 'HISTORICAL_INPUTS_ONLY', 'RECORDED_QUALIFICATION'].includes(s.why as string)) return fail()
    if (s.availability === 'Unavailable' ? s.id !== null || s.revision !== null || s.hash !== null || s.savedAt !== null : !isGuid(s.id) || s.revision !== null && !positive(s.revision) || !hash(s.hash) || !timestamp(s.savedAt)) return fail()
  }
  for (const g of v.authorities) if (!exact(g, ['id', 'operationId', 'roleId', 'capability', 'validUntil', 'recording']) || !isGuid(g.id) || !isGuid(g.operationId) || !isGuid(g.roleId) || !['human.review.submit', 'human.verify', 'human.decide'].includes(g.capability as string) || !timestamp(g.validUntil) || g.recording !== null && !policy(g.recording, a)) return fail()
  if (new Set((v.authorities as Authority[]).map(g => g.id)).size !== v.authorities.length) return fail()
  if (Object.hasOwn(v, 'evaluationId') && v.evaluationId !== null && !isGuid(v.evaluationId)) return fail()
  if (Object.hasOwn(v, 'auditItems')) {
    if (!Array.isArray(v.auditItems) || v.auditItems.length > 25 || typeof v.auditHasMore !== 'boolean') return fail()
    for (const e of v.auditItems) if (!exact(e, ['id', 'revision', 'kind', 'state', 'currentState', 'savedAt', 'validUntil', 'evaluationId', 'verificationId', 'verificationRevision', 'authorityId', 'roleId', 'authorityHash', 'contextHash']) || !isGuid(e.id) || !positive(e.revision) || !['Verification', 'Decision'].includes(e.kind as string) || !states.includes(e.state as string) || ![...states, 'EXPIRED_REVIEW_REQUIRED', 'SUPERSEDED_REVIEW_REQUIRED'].includes(e.currentState as string) || !timestamp(e.savedAt) || e.validUntil !== null && !timestamp(e.validUntil) || e.evaluationId !== null && !isGuid(e.evaluationId) || !(e.verificationId === null && e.verificationRevision === null || isGuid(e.verificationId) && positive(e.verificationRevision)) || !isGuid(e.authorityId) || !isGuid(e.roleId) || !hash(e.authorityHash) || !hash(e.contextHash)) return fail()
  }
  return { ...v, items: v.items.map(x => decodeReview(x, a)) } as Trace
}
export const tracePath = (a: Anchor, offset = 0, asOf = '') => `regulatory-trace?vesselId=${a.vesselId}&evidenceId=${a.evidenceId}&evidenceRevision=${a.evidenceRevision}&controlId=${a.controlId}&controlRevision=${a.controlRevision}&offset=${offset}${asOf ? `&asOf=${encodeURIComponent(asOf)}` : ''}`
export const trace = async (a: Anchor, offset: number, asOf: string, signal: AbortSignal) => decodeTrace(await request<unknown>(tracePath(a, offset, asOf), signal), a, offset)
export async function save(a: Anchor, attempt: { body: string; key: string; kind: 'Verification' | 'Decision'; id: string | null }, csrf: string, signal: AbortSignal) {
  const route = attempt.kind === 'Verification' ? 'verifications' : 'decisions'
  const row = decodeReview(await request<unknown>(`${route}${attempt.id ? `/${attempt.id}/events` : ''}?vesselId=${a.vesselId}`, signal, { method: 'POST', body: attempt.body, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key } }), a)
  if (row.kind !== attempt.kind || attempt.id !== null && row.id !== attempt.id) return fail()
  const command = JSON.parse(attempt.body) as Obj
  if (command.state === 'RECORDED') {
    for (const field of ['state', 'reasonCode', 'rationale', 'authorityId', 'operationId', 'verificationId', 'verificationRevision', 'evaluationId'] as const) if (row[field] !== command[field]) return fail()
    if (!row.recording || row.recording.profileRevision !== command.expectedProfileRevision || !exact(command.recording, ['decisionType', 'subjectType', 'subjectId', 'humanDecision', 'triggerSource', 'decisionContext'])) return fail()
    for (const field of ['decisionType', 'subjectType', 'subjectId', 'humanDecision', 'triggerSource', 'decisionContext'] as const) if (row.recording.content[field] !== command.recording[field]) return fail()
  }
  return row
}
