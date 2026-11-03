import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { bpNumber } from './tug-capability-contract'
import { decodeTugCrewCoverage } from './tug-crew-contract'
import type { TugCrewCoverage } from './tug-crew-contract'

export type PlanInputs = { plannedStart: string | null; plannedEnd: string | null; assignedTugs: string[] }
export type Plan = { jobId: string; revision: number; version: number | null; requestRevision: number; policyVersion: string; sourceVersion: string
  inputs: PlanInputs; operation: string; provenance: string; savedAt: string; replayed: boolean }
export type PlanChange = { operation: 'Window' | 'Assign' | 'Remove' | 'Reassign'; plannedStart?: string | null; plannedEnd?: string | null; tugId?: string; replacementTugId?: string }
export type PlanCommand = { baseVersion: number | null; requestVersion: number; policyVersion: string; change: PlanChange }
export type PlanAttempt = { key: string; body: PlanCommand }
export type PlanningEvidence = { availableBollardPull: string | null; capacityState: string; readiness: string; conflictState: string; coverage: string
  crew?: TugCrewCoverage[]
  ownedBp?: { tugId: string; state: string; tonnes: string | null; why: string; evidenceId: string | null; evidenceRevision: number | null; conditionId: string | null; conditionRevision: number | null; ruleId: string | null; qualificationRevision: number | null; qualificationHash: string | null; validUntil: string | null; observedAt: string | null }[]
  readinessFacts?: { tugId: string; source: string; id: string | null; revision: number | null; state: string; why: string; observedAt: string | null; expiresAt: string | null }[]
  operationalConflicts?: { tugId: string; kind: string; resourceId: string; revision: number; state: string; why: string; provenance: string; observedAt: string | null }[]
  bp: { tugId: string; state: string; declaredValue: string | null; source: string | null; policyVersion: string | null; sourceRevision: number | null; observationId: string | null; observedAt: string | null; freshness: string; provenance: string }[]
  conflicts: { jobId: string; revision: number; relation: string; source: string; observedAt: string }[] }
export type PlanningOptions = { state: string; policyVersion: string | null; canWrite: boolean; tugs: { id: string; code: string }[]; types: { code: string; tr: string; en: string }[]; offline: string }
export type PlanHistory = { items: Pick<Plan, 'revision' | 'requestRevision' | 'operation' | 'sourceVersion' | 'policyVersion' | 'provenance' | 'savedAt'>[]; coverage: string }
const root = (id: string, vessel: string, suffix = '') => `tug-jobs/${id}/plan${suffix}?vessel=${vessel}`
const invalid = (): never => { throw new ApiError(502, 'invalid_tug_planning_contract') }
const obj = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Record<string, unknown> => obj(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const positive = (v: unknown) => typeof v === 'number' && Number.isSafeInteger(v) && v > 0
const pair = (id: unknown, revision: unknown) => id === null && revision === null || isGuid(id) && positive(revision)
const time = (v: unknown) => v === null || timestamp(v)
export function decodePlanningEvidence(v: unknown, assigned: string[]): PlanningEvidence {
  const keys = ['availableBollardPull', 'capacityState', 'bp', 'conflictState', 'conflicts', 'coverage', 'readiness', 'ownedBp', 'readinessFacts', 'operationalConflicts']
  if (!exact(v, obj(v) && Object.hasOwn(v, 'crew') ? [...keys, 'crew'] : keys) ||
    !['Missing', 'ReviewRequired', 'CurrentPlanningCapacity'].includes(v.capacityState as string) || !['Blocked', 'ReviewRequired'].includes(v.readiness as string) ||
    !['Unknown', 'HumanReviewRequired', 'BoundaryReviewRequired'].includes(v.conflictState as string) || v.coverage !== 'CurrentAuthorizedOwningFacts;DraftGeometry;MasterDecisionSeparate' ||
    v.availableBollardPull !== null && !(typeof v.availableBollardPull === 'string' && /^(?:0|[1-9][0-9]{0,11})(?:\.[0-9]{1,6})?$/.test(v.availableBollardPull)) ||
    !Array.isArray(v.ownedBp) || !Array.isArray(v.readinessFacts) || !Array.isArray(v.bp) || !Array.isArray(v.conflicts) || !Array.isArray(v.operationalConflicts)) return invalid()
  for (const b of v.ownedBp) {
    if (!exact(b, ['tugId', 'state', 'tonnes', 'why', 'evidenceId', 'evidenceRevision', 'conditionId', 'conditionRevision', 'ruleId', 'qualificationRevision', 'qualificationHash', 'validUntil', 'observedAt']) ||
      !assigned.includes(b.tugId as string) || !['Unavailable', 'Unknown', 'Missing', 'ReviewRequired', 'ChangedInput', 'SourceReviewRequired', 'Expired', 'Unverified', 'UnsupportedContext', 'Future', 'CurrentPlanningCapacity'].includes(b.state as string) ||
      typeof b.why !== 'string' || !pair(b.evidenceId, b.evidenceRevision) || !pair(b.conditionId, b.conditionRevision) || !pair(b.ruleId, b.qualificationRevision) ||
      b.qualificationHash !== null && !(typeof b.qualificationHash === 'string' && /^[A-F0-9]{64}$/.test(b.qualificationHash)) || !time(b.validUntil) || !time(b.observedAt) ||
      (b.state === 'CurrentPlanningCapacity' ? !bpNumber(b.tonnes) || !b.evidenceId || !b.conditionId || !b.ruleId || !b.qualificationHash || !b.validUntil || !b.observedAt : b.tonnes !== null)) return invalid()
  }
  if (new Set(v.ownedBp.map(b => b.tugId)).size !== v.ownedBp.length) return invalid()
  const complete = assigned.length > 0 && v.ownedBp.length === assigned.length && v.ownedBp.every(b => b.state === 'CurrentPlanningCapacity') && new Set(v.ownedBp.map(b => b.qualificationHash)).size === 1
  if ((v.availableBollardPull !== null) !== complete || (v.capacityState === 'CurrentPlanningCapacity') !== complete) return invalid()
  if (complete) {
    const millionths = (raw: string) => { const [whole, fraction = ''] = raw.split('.'); return BigInt(whole) * 1000000n + BigInt(fraction.padEnd(6, '0')) }
    const sum = v.ownedBp.reduce((total, b) => total + millionths(b.tonnes as string), 0n)
    if (millionths(v.availableBollardPull as string) !== sum) return invalid()
  }
  for (const f of v.readinessFacts) if (!exact(f, ['tugId', 'source', 'id', 'revision', 'state', 'why', 'observedAt', 'expiresAt']) || !assigned.includes(f.tugId as string) ||
    !['OperationContext', 'Crew', 'CrewAssignment', 'CrewCertificate', 'CrewMinimumManning', 'Equipment', 'EquipmentPms', 'MaintenancePlan', 'BollardPullCertificate', 'CapabilityEquipmentCondition'].includes(f.source as string) || !pair(f.id, f.revision) || !['Unknown', 'Unavailable', 'Blocked', 'Current'].includes(f.state as string) || typeof f.why !== 'string' || !time(f.observedAt) || !time(f.expiresAt) ||
    f.state === 'Current' && (!['OperationContext', 'CrewMinimumManning', 'BollardPullCertificate', 'CapabilityEquipmentCondition'].includes(f.source as string) || !f.id || !f.expiresAt)) return invalid()
  const crew = Object.hasOwn(v, 'crew') ? decodeTugCrewCoverage(v.crew, assigned) : null
  const crewFacts = v.readinessFacts.filter(f => f.source === 'CrewMinimumManning')
  if (crew === null ? crewFacts.length !== 0 : crewFacts.length !== crew.length || crew.some(c => {
    const facts = crewFacts.filter(f => f.tugId === c.tugId)
    const state = c.state === 'CurrentMinimumRoleCoverage' ? 'Current' : c.state === 'Blocked' ? 'Blocked' : c.state === 'Unavailable' ? 'Unavailable' : 'Unknown'
    return facts.length !== 1 || facts[0].state !== state || facts[0].why !== c.reason || facts[0].id !== c.msmdId || facts[0].revision !== c.msmdRevision || facts[0].expiresAt !== c.validUntil
  })) return invalid()
  if ((v.readiness === 'Blocked') !== v.readinessFacts.some(f => f.state === 'Blocked')) return invalid()
  for (const b of v.bp) if (!exact(b, ['tugId', 'state', 'declaredValue', 'source', 'policyVersion', 'sourceRevision', 'observationId', 'observedAt', 'freshness', 'provenance']) || !assigned.includes(b.tugId as string) ||
    b.declaredValue !== null && !bpNumber(b.declaredValue) || !pair(b.observationId, b.sourceRevision) || !time(b.observedAt) || typeof b.state !== 'string' || typeof b.freshness !== 'string' || typeof b.provenance !== 'string') return invalid()
  for (const c of v.conflicts) if (!exact(c, ['jobId', 'revision', 'relation', 'source', 'observedAt']) || !isGuid(c.jobId) || !positive(c.revision) || !['Unknown', 'BoundaryTouch', 'Intersecting'].includes(c.relation as string) || c.source !== 'CurrentDraftPlanGeometry' || !timestamp(c.observedAt)) return invalid()
  for (const c of v.operationalConflicts) if (!exact(c, ['tugId', 'kind', 'resourceId', 'revision', 'state', 'why', 'provenance', 'observedAt']) || !assigned.includes(c.tugId as string) ||
    !['RecordedTugAssignment', 'CrewAssignment'].includes(c.kind as string) || !isGuid(c.resourceId) || !positive(c.revision) || c.state !== 'HumanReviewRequired' || typeof c.why !== 'string' || typeof c.provenance !== 'string' || !time(c.observedAt)) return invalid()
  if (v.operationalConflicts.length > 0 && v.conflictState !== 'HumanReviewRequired') return invalid()
  return v as PlanningEvidence
}
export const planningApi = {
  detail: async (id: string, vessel: string, signal: AbortSignal) => {
    const v = await request<{ state: string; requestState?: string; plan: Plan | null; evidence: unknown }>(root(id, vessel), signal)
    if (!v || !['Missing', 'DraftUnverified'].includes(v.state) || (v.state === 'Missing' ? v.plan !== null || v.evidence !== null : v.plan?.jobId !== id || !Array.isArray(v.plan.inputs.assignedTugs) || !v.plan.inputs.assignedTugs.every(isGuid))) return invalid()
    return { ...v, evidence: v.evidence === null ? null : decodePlanningEvidence(v.evidence, v.plan!.inputs.assignedTugs) }
  },
  options: (id: string, vessel: string, signal: AbortSignal) => request<PlanningOptions>(root(id, vessel, '/options'), signal),
  history: (id: string, vessel: string, signal: AbortSignal) => request<PlanHistory>(root(id, vessel, '/history'), signal),
  revision: (id: string, vessel: string, revision: number, signal: AbortSignal) => request<Plan>(root(id, vessel, `/history/${revision}`), signal),
  save: (id: string, vessel: string, attempt: PlanAttempt, csrf: string, signal: AbortSignal) => request<Plan>(root(id, vessel), signal, {
    method: 'PUT', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: JSON.stringify(attempt.body),
  }),
}
