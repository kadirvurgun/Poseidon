import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// S7 WP-B DEV-083..086 client contracts. Closed server codes only; an unknown state is a contract
// failure, never coerced to READY/PASS. The engine never issues GO: decisions are human records.
export const readinessStates = ['READY', 'CONDITIONAL', 'NOT_READY', 'REVIEW_REQUIRED'] as const
export type ReadinessState = typeof readinessStates[number]
export const checkResults = ['PASS', 'FAIL', 'UNKNOWN', 'CONDITIONAL'] as const
export type CheckResult = typeof checkResults[number]
export const readinessCategories = ['crew', 'crew_competence', 'equipment', 'tow_equipment', 'pms', 'certificate', 'communication', 'weather', 'bollard_pull_margin', 'assignment', 'briefing'] as const
export const assignmentRoles = ['Lead', 'Support', 'Escort'] as const
export const towKinds = ['Winch', 'TowHook', 'Towline', 'EmergencyRelease', 'FiFi'] as const
export const towConditions = ['Serviceable', 'Degraded', 'Failed', 'Unknown'] as const

export type ReadinessCheck = { category: string; result: CheckResult; reason: string; critical: boolean; sourceId: string | null; sourceRevision: number | null }
export type ConflictReason = { kind: string; result: CheckResult; reason: string; sourceId: string | null }
export type Authority = { id: string; duty: 'TugMaster' | 'Master' | 'ChiefEngineer' }
export type Assignment = { id: string; vesselId: string; jobId: string; jobRevision: number; tugId: string; plannedStart: string; plannedEnd: string; role: string
  state: 'Proposed' | 'Cancelled'; masterAck: 'Pending' | 'Accepted' | 'Rejected'; scopeHash: string; revision: number; version: number; proposedAt: string
  cancelledAt: string | null; cancelReason: string | null; replayed: boolean; sourceVersion: string }
export type AssignmentItem = { assignment: Assignment; response: { id: string; decision: 'ACCEPT' | 'REJECT'; reason: string | null; duty: string; recordedAt: string } | null
  conflicts: ConflictReason[]; conflictState: string; canRespond: boolean; canWithdraw: boolean; authorities: Authority[]; requestChanged: boolean }
export type AssignmentList = { jobId: string; jobRevision: number; jobVersion: number; jobState: string; canPropose: boolean; items: AssignmentItem[]
  tugs: { id: string; code: string }[]; roles: string[]; boundary: string }
export type Decision = { id: string; assignmentId: string; assignmentRevision: number; decision: 'GO' | 'NO_GO'; assessedState: ReadinessState; assessmentHash: string
  reason: string; duty: string; recordedAt: string; replayed: boolean }
export type Readiness = { assignment: Assignment; state: ReadinessState; categories: { category: string; result: CheckResult; reasons: string[] }[]
  criticalBlockers: string[]; reviewReasons: string[]; conditions: string[]; assessmentHash: string; requiredBollardPull: string | null
  availableBollardPull: string | null; bollardPullMargin: string | null; checks: ReadinessCheck[]; conflicts: ConflictReason[]; decisions: Decision[]
  authorities: Authority[]; canDecide: boolean; assessedAt: string; decisionAuthority: string; sourceLocator: string }
export type CrewReadiness = { tugId: string; assignmentId: string; state: ReadinessState; evaluatorState: string; evaluatorSource: string | null; checks: ReadinessCheck[]
  assessedAt: string; validUntil: string | null; coverage: string }
export type TowInputs = { kind: string; label: string | null; swlTonnes: string | null; lastTestDate: string | null; nextTestDue: string | null; condition: string
  critical: boolean | null; assetId: string | null; certificateId: string | null; notes: string | null }
export type TowRow = { id: string; revision: number; version: number; inputs: TowInputs; provenance: string; savedAt: string; check: ReadinessCheck
  verifications: { id: string; itemRevision: number; outcome: 'VERIFIED' | 'NOT_VERIFIED'; reason: string | null; duty: string; recordedAt: string }[] }
export type TowEquipment = { assessment: { tugId: string; state: ReadinessState; items: TowRow[]; checks: ReadinessCheck[]; assessedAt: string }
  canWrite: boolean; canVerify: boolean; authorities: Authority[]; kinds: string[]; conditions: string[]; sourceLocator: string }
export type Command = { key: string; path: string; method: 'POST' | 'PUT'; body: Record<string, unknown> }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
function fail(): never { throw new ApiError(502, 'invalid_tug_readiness_contract') }
const text = (v: unknown, max = 4000) => typeof v === 'string' && v.length <= max
const nullable = <T,>(v: unknown, ok: (x: unknown) => x is T | boolean) => v === null || ok(v)
const int = (v: unknown, min = 0) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const list = (v: unknown, max = 512): v is unknown[] => Array.isArray(v) && v.length <= max
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && values.includes(v)
const guid = (v: unknown): v is string => isGuid(v)
const time = (v: unknown) => typeof v === 'string' && timestamp(v)
const hash = (v: unknown) => typeof v === 'string' && /^[0-9A-F]{64}$/.test(v)

function check(v: unknown): ReadinessCheck {
  if (!object(v) || !text(v.category, 40) || !oneOf(v.result, checkResults) || !text(v.reason, 300) || typeof v.critical !== 'boolean' ||
    !nullable(v.sourceId, guid) || !(v.sourceRevision === null || int(v.sourceRevision, 1))) fail()
  return v as ReadinessCheck
}
function conflict(v: unknown): ConflictReason {
  if (!object(v) || !text(v.kind, 40) || !oneOf(v.result, checkResults) || !text(v.reason, 300) || !nullable(v.sourceId, guid)) fail()
  return v as ConflictReason
}
function authorities(v: unknown): Authority[] {
  if (!list(v, 32) || v.some(a => !object(a) || !guid(a.id) || !oneOf(a.duty, ['TugMaster', 'Master', 'ChiefEngineer'] as const))) fail()
  return v as Authority[]
}
export function decodeAssignment(v: unknown, vessel?: string): Assignment {
  if (!object(v) || !guid(v.id) || !guid(v.vesselId) || vessel && v.vesselId !== vessel || !guid(v.jobId) || !int(v.jobRevision, 1) || !guid(v.tugId) ||
    !time(v.plannedStart) || !time(v.plannedEnd) || !oneOf(v.role, assignmentRoles) || !oneOf(v.state, ['Proposed', 'Cancelled'] as const) ||
    !oneOf(v.masterAck, ['Pending', 'Accepted', 'Rejected'] as const) || !hash(v.scopeHash) || !int(v.revision, 1) || !int(v.version, 1) ||
    !time(v.proposedAt) || !(v.cancelledAt === null || time(v.cancelledAt)) || !(v.cancelReason === null || text(v.cancelReason, 2000)) ||
    typeof v.replayed !== 'boolean' || !text(v.sourceVersion, 200)) fail()
  return v as unknown as Assignment
}
export function decodeAssignments(v: unknown, vessel: string, job: string): AssignmentList {
  if (!object(v) || v.jobId !== job || !int(v.jobRevision, 1) || !int(v.jobVersion, 1) || !text(v.jobState, 20) || typeof v.canPropose !== 'boolean' ||
    !list(v.items, 128) || !list(v.tugs, 128) || !list(v.roles, 8) || !text(v.boundary, 200)) fail()
  const items = v.items as unknown[]
  for (const i of items) {
    if (!object(i) || !list(i.conflicts, 512) || !text(i.conflictState, 60) || typeof i.canRespond !== 'boolean' || typeof i.canWithdraw !== 'boolean' || typeof i.requestChanged !== 'boolean') fail()
    const item = i as Record<string, unknown>
    decodeAssignment(item.assignment, vessel); (item.conflicts as unknown[]).forEach(conflict); authorities(item.authorities)
    const r = item.response
    if (r !== null && (!object(r) || !guid(r.id) || !oneOf(r.decision, ['ACCEPT', 'REJECT'] as const) || !(r.reason === null || text(r.reason, 2000)) || !text(r.duty, 20) || !time(r.recordedAt))) fail()
  }
  if ((v.tugs as unknown[]).some(t => !object(t) || !guid(t.id) || !text(t.code, 80))) fail()
  return v as unknown as AssignmentList
}
function decision(v: unknown): Decision {
  if (!object(v) || !guid(v.id) || !guid(v.assignmentId) || !int(v.assignmentRevision, 1) || !oneOf(v.decision, ['GO', 'NO_GO'] as const) ||
    !oneOf(v.assessedState, readinessStates) || !hash(v.assessmentHash) || !text(v.reason, 2000) || !text(v.duty, 20) || !time(v.recordedAt) || typeof v.replayed !== 'boolean') fail()
  // A GO can only exist against a READY/CONDITIONAL assessment; anything else is a contract breach.
  if (v.decision === 'GO' && !['READY', 'CONDITIONAL'].includes(v.assessedState as string)) fail()
  return v as unknown as Decision
}
const strings = (v: unknown) => list(v, 512) && v.every(x => text(x, 400))
export function decodeReadiness(v: unknown, vessel: string, assignment: string): Readiness {
  if (!object(v) || !oneOf(v.state, readinessStates) || !list(v.categories, 16) || !strings(v.criticalBlockers) || !strings(v.reviewReasons) || !strings(v.conditions) ||
    !hash(v.assessmentHash) || !list(v.checks, 2048) || !list(v.conflicts, 512) || !list(v.decisions, 32) || typeof v.canDecide !== 'boolean' || !time(v.assessedAt) ||
    !text(v.decisionAuthority, 200) || !text(v.sourceLocator, 200) ||
    ![v.requiredBollardPull, v.availableBollardPull, v.bollardPullMargin].every(x => x === null || typeof x === 'string' && /^-?\d+(?:\.\d+)?$/.test(x))) fail()
  const a = decodeAssignment(v.assignment, vessel)
  if (a.id !== assignment) fail()
  if ((v.categories as unknown[]).some(c => !object(c) || !oneOf(c.category, readinessCategories) || !oneOf(c.result, checkResults) || !strings(c.reasons))) fail()
  ;(v.checks as unknown[]).forEach(check); (v.conflicts as unknown[]).forEach(conflict); (v.decisions as unknown[]).forEach(decision); authorities(v.authorities)
  // Defense in depth: a READY projection with any FAIL/UNKNOWN input is never displayed as ready.
  if (v.state === 'READY' && (v.checks as ReadinessCheck[]).some(c => c.result !== 'PASS')) fail()
  if ((v.checks as ReadinessCheck[]).some(c => c.result === 'FAIL') && v.state !== 'NOT_READY') fail()
  return v as unknown as Readiness
}
export function decodeCrewReadiness(v: unknown, assignment: string): CrewReadiness {
  if (!object(v) || !guid(v.tugId) || v.assignmentId !== assignment || !oneOf(v.state, readinessStates) || !text(v.evaluatorState, 40) ||
    !(v.evaluatorSource === null || text(v.evaluatorSource, 80)) || !list(v.checks, 256) || !time(v.assessedAt) || !(v.validUntil === null || time(v.validUntil)) || !text(v.coverage, 200)) fail()
  ;(v.checks as unknown[]).forEach(check)
  if (v.evaluatorState !== 'Observed' && v.state === 'READY') fail()
  return v as unknown as CrewReadiness
}
export function decodeTowEquipment(v: unknown, tug: string): TowEquipment {
  if (!object(v) || !object(v.assessment) || v.assessment.tugId !== tug || !oneOf(v.assessment.state, readinessStates) || !list(v.assessment.items, 128) ||
    !list(v.assessment.checks, 1024) || typeof v.canWrite !== 'boolean' || typeof v.canVerify !== 'boolean' || !list(v.kinds, 8) || !list(v.conditions, 8)) fail()
  const assessment = v.assessment as Record<string, unknown>
  for (const row of assessment.items as unknown[]) {
    if (!object(row) || !guid(row.id) || !int(row.revision, 1) || !int(row.version, 0) || !object(row.inputs) || !oneOf(row.inputs.kind, towKinds) ||
      !oneOf(row.inputs.condition, towConditions) || !text(row.provenance, 40) || !time(row.savedAt) || !list(row.verifications, 8)) fail()
    check((row as Record<string, unknown>).check)
  }
  ;(assessment.checks as unknown[]).forEach(check); authorities(v.authorities)
  return v as unknown as TowEquipment
}

// Sends one exact retained command; the same key and body are reused after an uncertain outcome.
export async function send(command: Command, csrf: string, signal: AbortSignal): Promise<unknown> {
  const response = await fetch(`/api/v1/${command.path}`, { credentials: 'same-origin', signal, method: command.method,
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: JSON.stringify(command.body) })
  let body: unknown = null
  try { body = await response.json() } catch { /* Proxy errors need not be JSON. */ }
  if (!response.ok) {
    const failure = object(body) ? body : {}
    throw new ApiError(response.status, typeof failure.code === 'string' ? failure.code : 'request_failed', {}, typeof failure.correlationId === 'string' ? failure.correlationId : undefined)
  }
  return body
}

export const tugReadinessApi = {
  assignments: async (vessel: string, job: string, signal: AbortSignal) => decodeAssignments(await request<unknown>(`tug-assignments?vessel=${vessel}&job=${job}`, signal), vessel, job),
  readiness: async (vessel: string, assignment: string, signal: AbortSignal) => decodeReadiness(await request<unknown>(`tug-readiness?vessel=${vessel}&assignment=${assignment}`, signal), vessel, assignment),
  crew: async (vessel: string, assignment: string, signal: AbortSignal) => decodeCrewReadiness(await request<unknown>(`tug-crew-readiness?vessel=${vessel}&assignment=${assignment}`, signal), assignment),
  equipment: async (tug: string, signal: AbortSignal) => decodeTowEquipment(await request<unknown>(`tug-equipment-readiness?vessel=${tug}`, signal), tug),
  propose: (vessel: string, body: { clientId: string; jobId: string; expectedJobVersion: number; tugId: string; plannedStart: string; plannedEnd: string; role: string }): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: `tug-assignments?vessel=${vessel}`, body }),
  respond: (vessel: string, a: Assignment, decision: 'ACCEPT' | 'REJECT', authorityId: string, reason: string | null): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: `tug-assignments/${a.id}/respond?vessel=${vessel}`, body: { expectedVersion: a.version, scopeHash: a.scopeHash, decision, authorityId, ...(reason ? { reason } : {}) } }),
  withdraw: (vessel: string, a: Assignment, reason: string): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: `tug-assignments/${a.id}/withdraw?vessel=${vessel}`, body: { expectedVersion: a.version, reason } }),
  decide: (vessel: string, r: Readiness, decision: 'GO' | 'NO_GO', authorityId: string, reason: string): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: `tug-readiness/decisions?vessel=${vessel}`, body: { assignmentId: r.assignment.id, expectedAssessmentHash: r.assessmentHash, decision, reason, authorityId } }),
  saveTow: (tug: string, id: string, baseVersion: number | null, inputs: TowInputs): Command =>
    ({ key: crypto.randomUUID(), method: baseVersion === null ? 'POST' : 'PUT', path: baseVersion === null ? `tug-tow-equipment?vessel=${tug}` : `tug-tow-equipment/${id}?vessel=${tug}`, body: { clientId: id, baseVersion, inputs } }),
  verifyTow: (tug: string, row: TowRow, outcome: 'VERIFIED' | 'NOT_VERIFIED', authorityId: string, reason: string | null): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: `tug-tow-equipment/${row.id}/verifications?vessel=${tug}`, body: { expectedVersion: row.version, expectedRevision: row.revision, outcome, authorityId, ...(reason ? { reason } : {}) } }),
}
