import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// DEV-040 / 60.03 §10 strict decoders. The authority stays UNCONFIGURED until a published OVERRIDE_AUTHORITY
// policy and a current AuthorityGrant exist; the client never infers an approval or an ACTIVE override.
export const overrideActions = ['ACCEPT_INTERPRETATION', 'REQUIRE_ADDITIONAL_CONTROL'] as const
export type OverrideAction = typeof overrideActions[number]
export const overrideReasons: Record<OverrideAction, readonly string[]> = {
  ACCEPT_INTERPRETATION: ['VERIFIED_LEGAL_INTERPRETATION', 'FLAG_STATE_EXEMPTION_REFERENCE', 'CLASS_EXEMPTION_REFERENCE'],
  REQUIRE_ADDITIONAL_CONTROL: ['ADDITIONAL_PRECAUTION', 'SOURCE_CONFLICT_PRECAUTION'],
}
export const overrideStates = ['DRAFT', 'IN_REVIEW', 'ACTIVE', 'REVOKED', 'EXPIRED', 'SUPERSEDED'] as const
export const overrideValidities = ['NOT_ACTIVE', 'REQUIRES_REVIEW', 'EXPIRED', 'NOT_YET_VALID', 'CURRENT', 'REVOKED', 'SUPERSEDED'] as const
export const overrideDecisions = ['APPROVE', 'REJECT', 'WITHDRAW', 'REVOKE'] as const
export type OverrideDecision = typeof overrideDecisions[number]
const eventCodes = ['PROPOSE', ...overrideDecisions] as const
export type OverrideEvent = { sequence: number; fromState: string | null; toState: typeof overrideStates[number]; eventCode: typeof eventCodes[number]; actorId: string; rationale: string | null; savedAt: string }
export type Override = {
  id: string; vesselId: string; evaluationId: string; ruleId: string; ruleRevision: number; requirementId: string | null; requirementRevision: number | null
  sourceId: string | null; sourceRevision: number | null; baseResult: string; purpose: 'COMPLIANCE_REVIEW'; action: OverrideAction; reasonCode: string
  rationale: string | null; evidenceIds: string[]; validFrom: string; expiresAt: string; reviewAt: string; authorId: string; approverId: string | null
  state: typeof overrideStates[number]; currentValidity: typeof overrideValidities[number]; sequence: number; version: number; createdAt: string; events: OverrideEvent[]
}
export type OverrideList = { vesselId: string; authority: 'UNCONFIGURED'; authorityBlockers: string[]; canPropose: boolean; canDecide: boolean; canRevoke: boolean; currentEvaluationId: string | null; items: Override[]; offset: number; hasMore: boolean; source: string }
export type OverrideCommand = { evaluationId: string; inputHash: string; ruleSetHash: string; ruleId: string; ruleRevision: number; action: OverrideAction; reasonCode: string; purpose: 'COMPLIANCE_REVIEW'; rationale: string; evidenceIds: string[]; validFrom: string; expiresAt: string; reviewAt: string }
export type CandidateItem = { controlId: string; revision: number; version: number; ruleId: string; ruleRevision: number; requirementId: string; requirementRevision: number; sourceId: string; sourceRevision: number; title: string; lifecycle: 'DRAFT'; controlState: 'DRAFT'; blockers: string[] }
export type CandidateOutcome = { vesselId: string; evaluationId: string; created: CandidateItem[]; alreadyLinked: number; blockers: string[] }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const positive = (v: unknown): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const optionalPin = (id: unknown, revision: unknown) => id === null && revision === null || isGuid(id) && positive(revision)
const code = (v: unknown) => typeof v === 'string' && /^[A-Z][A-Z_]{1,63}$/.test(v)
const codes = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 64 && v.every(code)
const text = (v: unknown, max: number) => typeof v === 'string' && v.length > 0 && v.length <= max
function fail(): never { throw new ApiError(502, 'invalid_override_contract') }
const includes = <T extends string>(list: readonly T[], v: unknown): v is T => typeof v === 'string' && (list as readonly string[]).includes(v)

function decodeEvent(v: unknown): OverrideEvent {
  if (!exact(v, ['sequence', 'fromState', 'toState', 'eventCode', 'actorId', 'rationale', 'savedAt']) || !positive(v.sequence) || !(v.fromState === null || includes(overrideStates, v.fromState)) ||
    !includes(overrideStates, v.toState) || !includes(eventCodes, v.eventCode) || !isGuid(v.actorId) || !(v.rationale === null || text(v.rationale, 4000)) || !timestamp(v.savedAt)) fail()
  return v as OverrideEvent
}
export function decodeOverride(v: unknown, vessel: string): Override {
  if (!exact(v, ['id', 'vesselId', 'evaluationId', 'ruleId', 'ruleRevision', 'requirementId', 'requirementRevision', 'sourceId', 'sourceRevision', 'baseResult', 'purpose', 'action', 'reasonCode', 'rationale', 'evidenceIds', 'validFrom', 'expiresAt', 'reviewAt', 'authorId', 'approverId', 'state', 'currentValidity', 'sequence', 'version', 'createdAt', 'events']) ||
    !isGuid(v.id) || v.vesselId !== vessel || !isGuid(v.evaluationId) || !isGuid(v.ruleId) || !positive(v.ruleRevision) || !optionalPin(v.requirementId, v.requirementRevision) ||
    !optionalPin(v.sourceId, v.sourceRevision) || !code(v.baseResult) || v.purpose !== 'COMPLIANCE_REVIEW' || !includes(overrideActions, v.action) ||
    !overrideReasons[v.action].includes(String(v.reasonCode)) || !(v.rationale === null || text(v.rationale, 4000)) || !Array.isArray(v.evidenceIds) || v.evidenceIds.length > 10 || !v.evidenceIds.every(isGuid) ||
    !timestamp(v.validFrom) || !timestamp(v.expiresAt) || !timestamp(v.reviewAt) || !isGuid(v.authorId) || !(v.approverId === null || isGuid(v.approverId)) ||
    !includes(overrideStates, v.state) || !includes(overrideValidities, v.currentValidity) || !positive(v.sequence) || !(typeof v.version === 'number' && Number.isSafeInteger(v.version) && v.version >= 0) ||
    !timestamp(v.createdAt) || !Array.isArray(v.events) || v.events.length > 20) fail()
  // An ACTIVE override always names an independent approver; CURRENT validity is only possible while ACTIVE.
  if (v.state === 'ACTIVE' ? v.approverId === null || v.approverId === v.authorId : v.currentValidity === 'CURRENT' || v.currentValidity === 'NOT_YET_VALID') fail()
  return { ...v, events: v.events.map(decodeEvent) } as Override
}
export function decodeOverrideList(v: unknown, vessel: string, offset: number): OverrideList {
  if (!isGuid(vessel) || !exact(v, ['vesselId', 'authority', 'authorityBlockers', 'canPropose', 'canDecide', 'canRevoke', 'currentEvaluationId', 'items', 'offset', 'hasMore', 'source']) ||
    v.vesselId !== vessel || v.authority !== 'UNCONFIGURED' || !codes(v.authorityBlockers) || !v.authorityBlockers.includes('OVERRIDE_AUTHORITY_POLICY_UNAVAILABLE') ||
    typeof v.canPropose !== 'boolean' || typeof v.canDecide !== 'boolean' || typeof v.canRevoke !== 'boolean' || !(v.currentEvaluationId === null || isGuid(v.currentEvaluationId)) ||
    !Array.isArray(v.items) || v.items.length > 25 || v.offset !== offset || typeof v.hasMore !== 'boolean' || v.source !== '18.01/Backlog/row:41;60.03/paragraph:154-166,575-580') fail()
  const items = v.items.map(item => decodeOverride(item, vessel))
  // Without a configured authority no item can be ACTIVE.
  if (items.some(item => item.state === 'ACTIVE')) fail()
  return { ...v, items } as OverrideList
}
export function decodeCandidates(v: unknown, vessel: string, evaluation: string): CandidateOutcome {
  if (!exact(v, ['vesselId', 'evaluationId', 'created', 'alreadyLinked', 'blockers']) || v.vesselId !== vessel || v.evaluationId !== evaluation || !Array.isArray(v.created) ||
    v.created.length < 1 || v.created.length > 25 || !(typeof v.alreadyLinked === 'number' && Number.isSafeInteger(v.alreadyLinked) && v.alreadyLinked >= 0) || !codes(v.blockers) ||
    !v.blockers.includes('CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE')) fail()
  for (const c of v.created) if (!exact(c, ['controlId', 'revision', 'version', 'ruleId', 'ruleRevision', 'requirementId', 'requirementRevision', 'sourceId', 'sourceRevision', 'title', 'lifecycle', 'controlState', 'blockers']) ||
    !isGuid(c.controlId) || c.revision !== 1 || typeof c.version !== 'number' || !isGuid(c.ruleId) || !positive(c.ruleRevision) || !isGuid(c.requirementId) || !positive(c.requirementRevision) ||
    !isGuid(c.sourceId) || !positive(c.sourceRevision) || !text(c.title, 2000) || c.lifecycle !== 'DRAFT' || c.controlState !== 'DRAFT' || !codes(c.blockers) || !c.blockers.includes('VERIFICATION_NOT_PERFORMED')) fail()
  return v as CandidateOutcome
}
export async function loadOverrides(vessel: string, offset: number, signal: AbortSignal) {
  if (!isGuid(vessel) || !Number.isSafeInteger(offset) || offset < 0 || offset > 100000) throw new ApiError(400, 'invalid_context')
  return decodeOverrideList(await request<unknown>(`vessels/${vessel}/applicability-overrides?offset=${offset}`, signal), vessel, offset)
}
type Frozen = { body: string; key: string }
const post = (path: string, frozen: Frozen, csrf: string, signal: AbortSignal) =>
  request<unknown>(path, signal, { method: 'POST', body: frozen.body, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': frozen.key, 'X-CSRF-TOKEN': csrf } })
export async function proposeOverride(vessel: string, frozen: Frozen, csrf: string, signal: AbortSignal) {
  return decodeOverride(await post(`vessels/${vessel}/applicability-overrides`, frozen, csrf, signal), vessel)
}
export async function decideOverride(vessel: string, id: string, frozen: Frozen, csrf: string, signal: AbortSignal) {
  if (!isGuid(id)) throw new ApiError(400, 'invalid_context')
  const result = decodeOverride(await post(`vessels/${vessel}/applicability-overrides/${id}/decisions`, frozen, csrf, signal), vessel)
  if (result.id !== id) fail()
  return result
}
export async function generateCandidates(vessel: string, evaluation: string, frozen: Frozen, csrf: string, signal: AbortSignal) {
  return decodeCandidates(await post(`vessels/${vessel}/compliance-control-plan/candidates`, frozen, csrf, signal), vessel, evaluation)
}
