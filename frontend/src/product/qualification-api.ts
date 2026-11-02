import { validCalculation, completeCalculation, type CalculationPolicy } from './tug-capability-contract'
import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

export const profileInputs = ['vessel.flag', 'vessel.type', 'vessel.gt', 'vessel.buildDate', 'vessel.classSociety', 'vessel.tradingArea', 'cargo.type', 'vessel.mainPropulsionKw', 'vessel.contractForConstructionDate']
export const verifiedProfileInputs = ['vessel.gt', 'vessel.buildDate', 'vessel.mainPropulsionKw', 'vessel.contractForConstructionDate']
export type InputPolicy = { field: string; basis: 'Declared' | 'HumanAttested' | 'ProviderVerified'; maxAgeHours: number | null }
export type ControlPolicy = { ownerRole: string | null; frequency: string | null; evidencePolicy: string | null; verificationPolicy: string | null; maxAgeHours: number | null; requireFile: boolean | null; requireAcknowledgment: boolean | null; acknowledgmentRole: string | null; requiredVerificationMechanism: string | null; verificationMechanismSourceLocator: string | null; sourceLocator: string | null }
export type Content = { tugCalculation?: CalculationPolicy | null; sourceBytesBase64: string | null; publisher: string | null; edition: string | null; ownerId: string | null; ownerRole: string | null; validationReview: string | null; inputs: InputPolicy[] | null; control: ControlPolicy | null; trigger: string | null; impactReview: string | null; supersedingRuleId: string | null; supersedingRuleRevision: number | null }
export type Qualification = { ruleId: string; revision: number; version: number; ruleRevision: number; state: string; content: Content; sourceHash: string; hash: string; priorHash: string | null; policyVersion: string; actorId: string; roleId: string; authorityId: string; authorityHash: string; contract: { sourceLocator: string; type: string; requiredMechanism: string | null } | null; savedAt: string; blockers: string[] }
export type Page = { hasMore: boolean; items: Qualification[]; actions: string[]; authorities: { action: string; authorityId: string }[]; owners: { actorId: string; roleId: string }[] }
type Obj = Record<string, unknown>
const exact = (v: unknown, keys: string[]): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const text = (v: unknown) => typeof v === 'string' && v.length > 0 && v.length <= 2000 && v === v.trim() && !/[\u0000-\u001f\u007f-\u009f]/.test(v)
const positive = (v: unknown): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const age = (v: unknown) => v === null || positive(v) && v <= 87600
const hash = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
const fail = (): never => { throw new ApiError(502, 'invalid_qualification_contract') }
export const blank = (): Content => ({ sourceBytesBase64: null, publisher: null, edition: null, ownerId: null, ownerRole: null, validationReview: null, inputs: [], control: null, trigger: null, impactReview: null, supersedingRuleId: null, supersedingRuleRevision: null })
export function validContent(v: unknown): v is Content {
  if (!exact(v, [...Object.keys(blank()), ...((v && typeof v === 'object' && Object.hasOwn(v, 'tugCalculation')) ? ['tugCalculation'] : [])]) || v.sourceBytesBase64 !== null) return false // bytes are never in a generic read projection
  if (v.tugCalculation != null && !validCalculation(v.tugCalculation)) return false
  if (['publisher', 'edition', 'validationReview', 'trigger', 'impactReview'].some(k => v[k] !== null && !text(v[k]))) return false
  if (!(v.ownerId === null && v.ownerRole === null || isGuid(v.ownerId) && isGuid(v.ownerRole)) || !(v.supersedingRuleId === null && v.supersedingRuleRevision === null || isGuid(v.supersedingRuleId) && positive(v.supersedingRuleRevision))) return false
  if (v.inputs !== null && (!Array.isArray(v.inputs) || v.inputs.length > 23 || v.inputs.some(p => !exact(p, ['field', 'basis', 'maxAgeHours']) || !text(p.field) || !['Declared', 'HumanAttested', 'ProviderVerified'].includes(p.basis as string) || !age(p.maxAgeHours)) || new Set(v.inputs.map(p => (p as Obj).field)).size !== v.inputs.length)) return false
  const p = v.control
  return p === null || exact(p, ['ownerRole', 'frequency', 'evidencePolicy', 'verificationPolicy', 'maxAgeHours', 'requireFile', 'requireAcknowledgment', 'sourceLocator', 'acknowledgmentRole', 'requiredVerificationMechanism', 'verificationMechanismSourceLocator']) && (p.ownerRole === null || isGuid(p.ownerRole)) && [p.frequency, p.evidencePolicy, p.verificationPolicy, p.sourceLocator].every(x => x === null || text(x)) && age(p.maxAgeHours) && (p.requireFile === null || typeof p.requireFile === 'boolean') && (p.requireAcknowledgment === null || typeof p.requireAcknowledgment === 'boolean') && (p.acknowledgmentRole === null || isGuid(p.acknowledgmentRole)) && (p.requiredVerificationMechanism === null || text(p.requiredVerificationMechanism)) && (p.verificationMechanismSourceLocator === null || text(p.verificationMechanismSourceLocator))
}
export function decodeQualification(v: unknown, rule: string): Qualification {
  if (!exact(v, ['ruleId', 'revision', 'version', 'ruleRevision', 'state', 'content', 'sourceHash', 'hash', 'priorHash', 'policyVersion', 'actorId', 'roleId', 'authorityId', 'authorityHash', 'contract', 'savedAt', 'blockers']) || v.ruleId !== rule || !positive(v.revision) || !positive(v.ruleRevision) || !positive(v.version) || v.version > 4294967295 || !['DRAFT', 'IN_REVIEW', 'ADOPTED', 'AWAITING', 'FUTURE_EFFECTIVE', 'IN_FORCE', 'SUPERSEDED'].includes(v.state as string) || !validContent(v.content) || !hash(v.hash) || v.priorHash !== null && !hash(v.priorHash) || v.policyVersion !== 'recorded-qualification-v1' || !timestamp(v.savedAt)) return fail()
  const synthetic = v.state === 'DRAFT' && v.contract === null && v.authorityHash === ''
  if ([v.actorId, v.roleId, v.authorityId].some(id => !isGuid(id) && !(synthetic && id === '00000000-0000-0000-0000-000000000000')) || !(hash(v.sourceHash) || v.state === 'DRAFT' && v.sourceHash === '') || !(hash(v.authorityHash) || synthetic)) return fail()
  if (v.contract !== null && (!exact(v.contract, ['sourceLocator', 'type', 'requiredMechanism']) || !text(v.contract.sourceLocator) || !text(v.contract.type) || v.contract.requiredMechanism !== null && !text(v.contract.requiredMechanism))) return fail()
  const codes = ['TUG_CALCULATION_POLICY_MISSING', 'EXACT_SOURCE_BYTES_MISSING', 'PUBLISHER_MISSING', 'EDITION_MISSING', 'SOURCE_OWNER_MISSING', 'VALIDATION_REVIEW_MISSING', 'INPUT_POLICY_MISSING', 'CONTROL_POLICY_MISSING']
  if (v.state !== 'DRAFT' && (v.contract === null || v.content.publisher === null || v.content.edition === null || v.content.ownerId === null || v.content.validationReview === null || v.content.inputs === null)) return fail()
  if (v.state !== 'DRAFT' && v.content.control && (!v.content.control.ownerRole || !v.content.control.frequency || !v.content.control.evidencePolicy || !v.content.control.verificationPolicy || !v.content.control.sourceLocator || v.content.control.requireFile === null || v.content.control.requireAcknowledgment === null || v.content.control.requireAcknowledgment && !v.content.control.acknowledgmentRole || v.content.control.requiredVerificationMechanism !== null && v.content.control.verificationMechanismSourceLocator === null)) return fail()
  if (v.state !== 'DRAFT' && v.content.tugCalculation && !completeCalculation(v.content.tugCalculation)) return fail()
  if (!Array.isArray(v.blockers) || v.blockers.some(x => !codes.includes(x)) || v.state !== 'DRAFT' && v.blockers.length > 0) return fail()
  return v as Qualification
}
export async function load(rule: string, vessel: string, signal: AbortSignal, before?: number): Promise<Page> {
  const v = await request<unknown>(`regulatory-source-governance/${rule}${vessel ? `?vesselId=${vessel}` : ''}${before ? `${vessel ? '&' : '?'}before=${before}` : ''}`, signal)
  if (!exact(v, ['items', 'hasMore', 'actions', 'authorities', 'owners']) || typeof v.hasMore !== 'boolean' || !Array.isArray(v.items) || v.items.length > 100 || !Array.isArray(v.actions) || v.actions.some(a => !['draft', 'review', 'qualify', 'attest'].includes(a)) || !Array.isArray(v.authorities) || v.authorities.length > 4 || v.authorities.some(g => !exact(g, ['action', 'authorityId']) || !(v.actions as unknown[]).includes(g.action) || !isGuid(g.authorityId)) || !Array.isArray(v.owners) || v.owners.length > 100 || v.owners.some(o => !exact(o, ['actorId', 'roleId']) || !isGuid(o.actorId) || !isGuid(o.roleId))) return fail()
  return { ...v, items: v.items.map(x => decodeQualification(x, rule)) } as Page
}
export async function send(rule: string, attempt: { body: string; key: string; action: 'events' | 'fact-attestations' }, csrf: string, signal: AbortSignal) {
  const v = await request<unknown>(`regulatory-source-governance/${rule}/${attempt.action}`, signal, { method: 'POST', body: attempt.body, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key } })
  if (attempt.action === 'events') return decodeQualification(v, rule)
  if (!exact(v, ['id', 'ruleId', 'qualificationRevision', 'vesselId', 'profileRevision', 'fields', 'hash', 'savedAt', 'validUntil']) || v.ruleId !== rule || !isGuid(v.id) || !isGuid(v.vesselId) || !positive(v.qualificationRevision) || !positive(v.profileRevision) || !Array.isArray(v.fields) || v.fields.some(f => !profileInputs.includes(f)) || !hash(v.hash) || !timestamp(v.savedAt) || !timestamp(v.validUntil)) return fail()
  return v
}
export async function current(id: string, signal: AbortSignal): Promise<{ current: boolean; causes: string[] }> {
  const v = await request<unknown>(`applicability/evaluations/${id}/current`, signal)
  const causes = ['EVALUATION_UNAVAILABLE', 'QUALIFICATION_LINEAGE_UNAVAILABLE', 'PROFILE_CHANGED', 'VESSEL_CHANGED', 'RULE_CHANGED', 'QUALIFICATION_CHANGED', 'SOURCE_REVIEW_REQUIRED', 'SOURCE_NOT_CURRENT', 'ATTESTATION_EXPIRED', 'ATTESTATION_AUTHORITY_CHANGED', 'INPUT_STALE', 'CONSTRUCTION_RECORD_CHANGED']
  if (!exact(v, ['id', 'current', 'causes']) || v.id !== id || typeof v.current !== 'boolean' || !Array.isArray(v.causes) || v.causes.some(c => !causes.includes(c)) || v.current !== (v.causes.length === 0)) return fail()
  return v as { current: boolean; causes: string[] }
}
