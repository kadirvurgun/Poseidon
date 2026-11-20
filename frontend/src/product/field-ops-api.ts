import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import type { Command } from './tug-readiness-api'

// S9 WP-D DEV-089/090/111 client contracts. Closed server codes only: an unknown state is a contract failure,
// never coerced to a passing or verified state. Start/abort/review/verification are human records; nothing here
// is a GO, a survey result or an approval of cost/supplier data.
export const executionStates = ['ACTIVE', 'SUSPENDED', 'COMPLETED', 'ABORTED'] as const
export const verificationStates = ['PENDING', 'VERIFIED', 'REJECTED'] as const
export const executionActions = ['SUSPEND', 'RESUME', 'ABORT', 'COMPLETE'] as const
export const eventKinds = ['STARTED', 'SUSPENDED', 'RESUMED', 'ABORTED', 'COMPLETED', 'VERIFIED', 'VERIFICATION_REJECTED', 'TOWLINE_CONNECTED', 'TOWLINE_RELEASED',
  'LOAD_OBSERVED', 'COMMS_CHECK', 'WEATHER_OBSERVED', 'WINCH_STATE', 'EMERGENCY_RELEASE_TEST', 'POSITION', 'EVIDENCE', 'NOTE'] as const
export const recordableKinds = ['TOWLINE_CONNECTED', 'TOWLINE_RELEASED', 'LOAD_OBSERVED', 'COMMS_CHECK', 'WEATHER_OBSERVED', 'WINCH_STATE', 'EMERGENCY_RELEASE_TEST', 'POSITION', 'EVIDENCE', 'NOTE'] as const
export const loadUnits = ['t', 'kN'] as const
export const commsStates = ['GOOD', 'ATTENTION', 'LOST'] as const
export const winchStates = ['PASS', 'FAIL', 'UNKNOWN', 'CONDITIONAL'] as const
export const releaseResults = ['PASS', 'FAIL'] as const
export const evidencePurposes = ['PHOTO', 'AIS_OR_TIME', 'DOCUMENT'] as const
export const logStates = ['DRAFT', 'SUBMITTED', 'RETURNED', 'VERIFIED'] as const
export const watches = ['00-08', '08-16', '16-24', 'OTHER'] as const
export const fuelUnits = ['L', 'm3', 't'] as const
export const reconciliationStates = ['RECONCILED', 'REVIEW_REQUIRED', 'INCOMPLETE'] as const
export const masterReviews = ['PENDING', 'REVIEWED', 'ATTENTION'] as const
export const syncStates = ['SYNCED', 'LOCAL_PENDING', 'CONFLICT', 'FAILED'] as const
export const drydockStates = ['DRAFT', 'PLANNED', 'IN_PROGRESS', 'COMPLETED', 'VERIFIED', 'CANCELLED'] as const
export const drydockActions = ['PLAN', 'REPLAN', 'START', 'COMPLETE', 'CANCEL'] as const
export const categories = ['CLASS_SURVEY', 'STATUTORY_SURVEY', 'CERTIFICATE_RENEWAL', 'WORK_ORDER', 'DEFECT_REPAIR', 'OTHER'] as const
export const priorities = ['HIGH', 'NORMAL', 'LOW'] as const
export const linkKinds = ['WORK_ORDER', 'DEFECT', 'CERTIFICATE', 'SURVEY_CHECKLIST'] as const
export const linkStates = ['CURRENT', 'STALE', 'UNAVAILABLE', 'OVERDUE'] as const
export const itemStates = ['UNLINKED', 'LINKED', 'OVERDUE', 'REVIEW_REQUIRED'] as const
export const readinessStates = ['INCOMPLETE', 'REVIEW_REQUIRED', 'ATTENTION', 'LINKS_CURRENT'] as const

export type Authority = { id: string; duty: 'TugMaster' | 'Master' }
export type Weather = { windSpeedKn: string | null; windDirection: string | null; seaState: string | null; visibility: string | null; current: string | null }
export type EventInput = { kind: string; occurredAt: string; value: string | null; unit: string | null; result: string | null; text: string | null; reference: string | null; weather: Weather | null }
export type Execution = { id: string; vesselId: string; jobId: string; tugId: string; assignmentId: string; planId: string; planRevision: number; briefingId: string
  briefingRevision: number; readinessDecisionId: string; state: typeof executionStates[number]; verificationState: typeof verificationStates[number]; revision: number
  version: number; sequence: number; hash: string; startedAt: string; startedBy: string; startPosition: string | null; endedAt: string | null; endedBy: string | null
  endPosition: string | null; verifiedBy: string | null; verifiedAt: string | null; provenance: string }
export type TimelineEvent = { sequence: number; kind: typeof eventKinds[number]; occurredAt: string; recordedAt: string; actorId: string; duty: string | null; value: string | null
  unit: string | null; result: string | null; text: string | null; reference: string | null; weather: Weather | null; reason: string | null; position: string | null; hash: string }
export type Ledger = { towlineConnectedAt: string | null; towlineReleasedAt: string | null; towlineConnected: boolean; maxTowLoad: { value: string; unit: string; samples: number }[]
  communicationState: string; winchState: string; emergencyReleaseTested: string; activeSeconds: number }
export type ExecutionView = { execution: Execution; jobNumber: string | null; assistedVessel: string | null; ledger: Ledger; timeline: TimelineEvent[]; authorities: Authority[]
  canRecord: boolean; canTransition: boolean; canVerify: boolean; allowedActions: string[]; completeBlockers: string[]; recordableKinds: string[]; safetyEvents: string; authority: string }
export type StartGate = { tugId: string; assignmentId: string | null; readinessDecisionId: string | null; decision: 'GO' | 'NO_GO' | null; blockers: string[]; authorities: Authority[]
  canStart: boolean; executionId: string | null; executionState: string | null }
export type JobView = { vesselId: string; jobId: string; jobNumber: string; jobStatus: string; tugs: StartGate[]; executions: Execution[] }
export type Meter = { id: string; meter: string; opening: string | null; closing: string | null; resetEvidenceRef: string | null }
export type LogContent = { timeZone: string | null; meters: Meter[]; fuel: { quantity: string | null; unit: string | null; sourceRef: string | null }; executionIds: string[]
  defectIds: string[]; dailySummary: string | null }
export type DailyLog = { id: string; vesselId: string; logDate: string; watch: typeof watches[number]; revision: number; version: number; state: typeof logStates[number]
  content: LogContent; hash: string; submittedBy: string | null; verifiedRevision: number | null; capturedAt: string | null; syncedAt: string; syncState: 'SYNCED'; provenance: string }
export type Reconciliation = { state: typeof reconciliationStates[number]; reasons: { scope: string; code: string; subject: string | null }[]; jobsCompleted: number
  operatingHours: string | null; openInterval: boolean; meterHours: string | null }
export type LogView = { log: DailyLog; reconciliation: Reconciliation; masterReview: typeof masterReviews[number]; defectsReported: number; safetyEvents: string
  intervalFrom: string | null; intervalTo: string | null; executions: { id: string; jobId: string; state: string; startedAt: string; endedAt: string | null; linked: boolean }[]
  submitBlockers: string[]; reviews: { id: string; revision: number; decision: 'VERIFY' | 'RETURN'; reconciliation: string; duty: string; actorId: string; reason: string; decidedAt: string }[]
  revisions: { revision: number; state: string; reasonCode: string; actorId: string; capturedAt: string | null; syncedAt: string }[]; authorities: Authority[]
  canEdit: boolean; canSubmit: boolean; canReview: boolean; offlineCapture: string }
export type LogList = { vesselId: string; items: { id: string; logDate: string; watch: string; revision: number; state: string; masterReview: string; syncedAt: string }[]; canWrite: boolean }
export type Link = { kind: typeof linkKinds[number]; targetId: string; pinnedRevision: number }
export type ScopeItem = { id: string; category: typeof categories[number]; title: string; equipmentId: string | null; workPackage: string | null; classRequirement: string | null
  statutoryRequirement: string | null; priority: typeof priorities[number] | null; links: Link[] }
export type DrydockContent = { projectReference: string | null; yard: string | null; plannedStart: string | null; plannedFinish: string | null
  external: { estimatedCost: string | null; currency: string | null; vendor: string | null; budgetReference: string | null }; items: ScopeItem[]
  completionEvidence: { id: string; reference: string; note: string | null }[] }
export type Drydock = { id: string; vesselId: string; revision: number; version: number; state: typeof drydockStates[number]; content: DrydockContent; hash: string
  actualStart: string | null; actualFinish: string | null; completedBy: string | null; verifiedBy: string | null; verifiedAt: string | null; provenance: string; savedAt: string }
export type LinkStatus = { kind: string; targetId: string; pinnedRevision: number; currentRevision: number | null; state: typeof linkStates[number]; reason: string; due: string | null }
export type DrydockView = { package: Drydock; readiness: { state: typeof readinessStates[number]; items: { itemId: string; state: typeof itemStates[number]; links: LinkStatus[] }[]
  unlinked: number; overdue: number; stale: number; unavailable: number; meaning: string }; timeline: { revision: number; state: string; reasonCode: string; note: string | null; actorId: string; savedAt: string }[]
  planBlockers: string[]; completeBlockers: string[]; allowedActions: string[]; canEdit: boolean; canVerify: boolean; unreadableLinkKinds: string[]; external: string }
export type DrydockList = { vesselId: string; items: { id: string; revision: number; state: string; projectReference: string | null; yard: string | null; plannedStart: string | null
  plannedFinish: string | null; items: number }[]; canWrite: boolean }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
function fail(): never { throw new ApiError(502, 'invalid_field_ops_contract') }
const text = (v: unknown, max = 8000) => typeof v === 'string' && v.length <= max
const opt = (v: unknown, max = 8000) => v === null || text(v, max)
const int = (v: unknown, min = 0) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const list = (v: unknown, max = 2000): v is unknown[] => Array.isArray(v) && v.length <= max
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && values.includes(v)
const guid = (v: unknown): v is string => isGuid(v)
const optGuid = (v: unknown) => v === null || guid(v)
const time = (v: unknown) => typeof v === 'string' && timestamp(v)
const optTime = (v: unknown) => v === null || time(v)
const date = (v: unknown) => typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v)
const hash = (v: unknown) => typeof v === 'string' && /^[0-9A-F]{64}$/.test(v)
const strings = (v: unknown, max = 128) => list(v, max) && v.every(x => text(x, 300))
function authorities(v: unknown): Authority[] {
  if (!list(v, 32) || v.some(a => !object(a) || !guid(a.id) || !oneOf(a.duty, ['TugMaster', 'Master'] as const))) fail()
  return v as Authority[]
}

function execution(x: unknown, vessel: string): Execution {
  if (!object(x) || !guid(x.id) || x.vesselId !== vessel || !guid(x.jobId) || !guid(x.tugId) || !oneOf(x.state, executionStates) || !oneOf(x.verificationState, verificationStates) ||
    !int(x.revision, 1) || !int(x.version) || !int(x.sequence, 1) || !hash(x.hash) || !time(x.startedAt) || !guid(x.startedBy) || !optTime(x.endedAt) || !optGuid(x.endedBy) ||
    !optGuid(x.verifiedBy) || !opt(x.startPosition, 400) || !opt(x.endPosition, 400)) fail()
  // A terminal execution always names who ended it; a verified one names a different verifier.
  if ((x.state === 'COMPLETED' || x.state === 'ABORTED') && (x.endedAt === null || x.endedBy === null)) fail()
  if (x.verificationState !== 'PENDING' && (x.verifiedBy === null || x.verifiedBy === x.startedBy || x.verifiedBy === x.endedBy)) fail()
  return x as unknown as Execution
}
export function decodeExecutionView(v: unknown, vessel: string): ExecutionView {
  if (!object(v) || !object(v.ledger) || !list(v.timeline) || typeof v.canRecord !== 'boolean' || typeof v.canTransition !== 'boolean' || typeof v.canVerify !== 'boolean' ||
    !strings(v.allowedActions, 8) || !strings(v.completeBlockers, 16) || !strings(v.recordableKinds, 16) || !text(v.safetyEvents, 200) || !opt(v.jobNumber, 80)) fail()
  execution(v.execution, vessel)
  const l = v.ledger
  if (!optTime(l.towlineConnectedAt) || !optTime(l.towlineReleasedAt) || typeof l.towlineConnected !== 'boolean' || !list(l.maxTowLoad, 4) ||
    (l.maxTowLoad as unknown[]).some(m => !object(m) || !text(m.value, 12) || !oneOf(m.unit, loadUnits) || !int(m.samples, 1)) ||
    !oneOf(l.communicationState, ['UNKNOWN', ...commsStates] as const) || !oneOf(l.winchState, winchStates) || !oneOf(l.emergencyReleaseTested, ['UNKNOWN', ...releaseResults] as const) || !int(l.activeSeconds)) fail()
  for (const e of v.timeline as unknown[]) if (!object(e) || !int(e.sequence, 1) || !oneOf(e.kind, eventKinds) || !time(e.occurredAt) || !time(e.recordedAt) || !guid(e.actorId) || !hash(e.hash)) fail()
  if ((v.timeline as TimelineEvent[]).some((e, i) => e.sequence !== i + 1)) fail()
  authorities(v.authorities)
  return v as unknown as ExecutionView
}
export function decodeJobView(v: unknown, vessel: string, job: string): JobView {
  if (!object(v) || v.vesselId !== vessel || v.jobId !== job || !text(v.jobNumber, 80) || !text(v.jobStatus, 40) || !list(v.tugs, 12) || !list(v.executions, 12)) fail()
  for (const g of v.tugs as unknown[]) {
    if (!object(g) || !guid(g.tugId) || !optGuid(g.assignmentId) || !optGuid(g.readinessDecisionId) || !(g.decision === null || oneOf(g.decision, ['GO', 'NO_GO'] as const)) ||
      !strings(g.blockers, 64) || typeof g.canStart !== 'boolean' || !optGuid(g.executionId)) fail()
    // Defense in depth: a start is never offered while the server lists blockers.
    if (g.canStart && (g.blockers as string[]).length > 0) fail()
    authorities(g.authorities)
  }
  ;(v.executions as unknown[]).forEach(x => execution(x, vessel))
  return v as unknown as JobView
}
function logContent(c: unknown): LogContent {
  if (!object(c) || !opt(c.timeZone, 64) || !list(c.meters, 20) || !object(c.fuel) || !list(c.executionIds, 50) || !list(c.defectIds, 100) || !opt(c.dailySummary)) fail()
  if ((c.meters as unknown[]).some(m => !object(m) || !guid(m.id) || !text(m.meter, 60) || !opt(m.opening, 14) || !opt(m.closing, 14) || !opt(m.resetEvidenceRef, 400))) fail()
  if (!opt(c.fuel.quantity, 14) || !(c.fuel.unit === null || oneOf(c.fuel.unit, fuelUnits)) || !opt(c.fuel.sourceRef, 400)) fail()
  if (!(c.executionIds as unknown[]).every(guid) || !(c.defectIds as unknown[]).every(guid)) fail()
  return c as unknown as LogContent
}
export function decodeLogView(v: unknown, vessel: string): LogView {
  if (!object(v) || !object(v.log) || !object(v.reconciliation) || !oneOf(v.masterReview, masterReviews) || !int(v.defectsReported) || !text(v.safetyEvents, 200) ||
    !optTime(v.intervalFrom) || !optTime(v.intervalTo) || !list(v.executions, 100) || !strings(v.submitBlockers, 32) || !list(v.reviews, 32) || !list(v.revisions, 50) ||
    typeof v.canEdit !== 'boolean' || typeof v.canSubmit !== 'boolean' || typeof v.canReview !== 'boolean') fail()
  const l = v.log, r = v.reconciliation
  if (!guid(l.id) || l.vesselId !== vessel || !date(l.logDate) || !oneOf(l.watch, watches) || !int(l.revision, 1) || !int(l.version) || !oneOf(l.state, logStates) || !hash(l.hash) ||
    !optTime(l.capturedAt) || !time(l.syncedAt) || l.syncState !== 'SYNCED' || !(l.verifiedRevision === null || int(l.verifiedRevision, 1))) fail()
  if (l.state === 'VERIFIED' && l.verifiedRevision !== l.revision) fail()
  logContent(l.content)
  if (!oneOf(r.state, reconciliationStates) || !list(r.reasons, 400) || !int(r.jobsCompleted) || !opt(r.operatingHours, 20) || typeof r.openInterval !== 'boolean' || !opt(r.meterHours, 20) ||
    (r.reasons as unknown[]).some(x => !object(x) || !text(x.scope, 20) || !text(x.code, 60) || !opt(x.subject, 80))) fail()
  // Reasons never coexist with a reconciled state; safety events stay UNKNOWN until a source exists.
  if (r.state === 'RECONCILED' && (r.reasons as unknown[]).length > 0) fail()
  if (!String(v.safetyEvents).startsWith('UNKNOWN')) fail()
  for (const x of v.reviews as unknown[]) if (!object(x) || !oneOf(x.decision, ['VERIFY', 'RETURN'] as const) || !guid(x.actorId) || !text(x.reason, 1000) || !time(x.decidedAt)) fail()
  authorities(v.authorities)
  return v as unknown as LogView
}
export function decodeLogList(v: unknown, vessel: string): LogList {
  if (!object(v) || v.vesselId !== vessel || !list(v.items, 62) || typeof v.canWrite !== 'boolean') fail()
  for (const i of v.items as unknown[]) if (!object(i) || !guid(i.id) || !date(i.logDate) || !oneOf(i.watch, watches) || !oneOf(i.state, logStates) || !oneOf(i.masterReview, masterReviews)) fail()
  return v as unknown as LogList
}
function drydockContent(c: unknown): DrydockContent {
  if (!object(c) || !opt(c.projectReference, 200) || !opt(c.yard, 200) || !(c.plannedStart === null || date(c.plannedStart)) || !(c.plannedFinish === null || date(c.plannedFinish)) ||
    !object(c.external) || !list(c.items, 200) || !list(c.completionEvidence, 50)) fail()
  for (const i of c.items as unknown[]) if (!object(i) || !guid(i.id) || !oneOf(i.category, categories) || !text(i.title, 200) || !optGuid(i.equipmentId) ||
    !(i.priority === null || oneOf(i.priority, priorities)) || !list(i.links, 20) ||
    (i.links as unknown[]).some(l => !object(l) || !oneOf(l.kind, linkKinds) || !guid(l.targetId) || !int(l.pinnedRevision, 1))) fail()
  return c as unknown as DrydockContent
}
export function decodeDrydockView(v: unknown, vessel: string): DrydockView {
  if (!object(v) || !object(v.package) || !object(v.readiness) || !list(v.timeline, 200) || !strings(v.planBlockers, 32) || !strings(v.completeBlockers, 32) || !strings(v.allowedActions, 8) ||
    typeof v.canEdit !== 'boolean' || typeof v.canVerify !== 'boolean' || !strings(v.unreadableLinkKinds, 4) || !text(v.external, 200)) fail()
  const p = v.package, r = v.readiness
  if (!guid(p.id) || p.vesselId !== vessel || !int(p.revision, 1) || !int(p.version) || !oneOf(p.state, drydockStates) || !hash(p.hash) || !optGuid(p.completedBy) || !optGuid(p.verifiedBy)) fail()
  if (p.state === 'VERIFIED' && (p.verifiedBy === null || p.verifiedBy === p.completedBy)) fail()
  drydockContent(p.content)
  if (!oneOf(r.state, readinessStates) || !list(r.items, 200) || ![r.unlinked, r.overdue, r.stale, r.unavailable].every(x => int(x)) || !text(r.meaning, 200)) fail()
  for (const i of r.items as unknown[]) if (!object(i) || !guid(i.itemId) || !oneOf(i.state, itemStates) || !list(i.links, 20) ||
    (i.links as unknown[]).some(l => !object(l) || !oneOf(l.state, linkStates) || !guid(l.targetId) || !text(l.reason, 60))) fail()
  // A stale or unavailable link can never be shown as current linkage.
  if (r.state === 'LINKS_CURRENT' && ((r.stale as number) > 0 || (r.unavailable as number) > 0)) fail()
  return v as unknown as DrydockView
}
export function decodeDrydockList(v: unknown, vessel: string): DrydockList {
  if (!object(v) || v.vesselId !== vessel || !list(v.items, 50) || typeof v.canWrite !== 'boolean') fail()
  for (const i of v.items as unknown[]) if (!object(i) || !guid(i.id) || !oneOf(i.state, drydockStates) || !int(i.items)) fail()
  return v as unknown as DrydockList
}

const cmd = (method: 'POST' | 'PUT', path: string, body: Record<string, unknown>): Command => ({ key: crypto.randomUUID(), method, path, body })
const nowIso = () => new Date().toISOString()

export const fieldOpsApi = {
  job: async (vessel: string, job: string, signal: AbortSignal) => decodeJobView(await request<unknown>(`towage-executions?vessel=${vessel}&job=${job}`, signal), vessel, job),
  execution: async (vessel: string, id: string, signal: AbortSignal) => decodeExecutionView(await request<unknown>(`towage-executions/${id}?vessel=${vessel}`, signal), vessel),
  start: (vessel: string, id: string, job: string, gate: StartGate, authorityId: string, startPosition: string | null): Command =>
    cmd('POST', `towage-executions?vessel=${vessel}`, { id, jobId: job, tugId: gate.tugId, authorityId, readinessDecisionId: gate.readinessDecisionId, startPosition }),
  transition: (vessel: string, x: Execution, action: string, reason: string, authorityId: string, endPosition: string | null): Command =>
    cmd('POST', `towage-executions/${x.id}/transitions?vessel=${vessel}`, { expectedVersion: x.version, action, reason, occurredAt: nowIso(), authorityId, endPosition }),
  record: (vessel: string, x: Execution, event: EventInput): Command => cmd('POST', `towage-executions/${x.id}/events?vessel=${vessel}`, { event }),
  verify: (vessel: string, x: Execution, decision: 'VERIFIED' | 'REJECTED', reason: string): Command =>
    cmd('POST', `towage-executions/${x.id}/verification?vessel=${vessel}`, { expectedVersion: x.version, contentHash: x.hash, decision, reason }),
  logs: async (vessel: string, signal: AbortSignal) => decodeLogList(await request<unknown>(`tug-daily-logs?vessel=${vessel}`, signal), vessel),
  log: async (vessel: string, id: string, signal: AbortSignal) => decodeLogView(await request<unknown>(`tug-daily-logs/${id}?vessel=${vessel}`, signal), vessel),
  createLog: (vessel: string, id: string, logDate: string, watch: string, content: LogContent): Command =>
    cmd('POST', `tug-daily-logs?vessel=${vessel}`, { id, logDate, watch, capturedAt: nowIso(), content }),
  updateLog: (vessel: string, l: DailyLog, content: LogContent): Command => cmd('PUT', `tug-daily-logs/${l.id}?vessel=${vessel}`, { expectedVersion: l.version, capturedAt: nowIso(), content }),
  submitLog: (vessel: string, l: DailyLog): Command => cmd('POST', `tug-daily-logs/${l.id}/submit?vessel=${vessel}`, { expectedVersion: l.version, contentHash: l.hash }),
  reviewLog: (vessel: string, l: DailyLog, decision: 'VERIFY' | 'RETURN', reason: string, authorityId: string): Command =>
    cmd('POST', `tug-daily-logs/${l.id}/review?vessel=${vessel}`, { expectedVersion: l.version, contentHash: l.hash, decision, reason, authorityId }),
  drydocks: async (vessel: string, signal: AbortSignal) => decodeDrydockList(await request<unknown>(`drydock-packages?vessel=${vessel}`, signal), vessel),
  drydock: async (vessel: string, id: string, signal: AbortSignal) => decodeDrydockView(await request<unknown>(`drydock-packages/${id}?vessel=${vessel}`, signal), vessel),
  saveDrydock: (vessel: string, id: string, p: Drydock | null, content: DrydockContent): Command => p === null
    ? cmd('POST', `drydock-packages?vessel=${vessel}`, { id, content })
    : cmd('PUT', `drydock-packages/${p.id}?vessel=${vessel}`, { expectedVersion: p.version, content }),
  transitionDrydock: (vessel: string, p: Drydock, action: string, note: string | null): Command =>
    cmd('POST', `drydock-packages/${p.id}/transitions?vessel=${vessel}`, { expectedVersion: p.version, contentHash: p.hash, action, note }),
  verifyDrydock: (vessel: string, p: Drydock, decision: 'VERIFY' | 'REJECT', reason: string): Command =>
    cmd('POST', `drydock-packages/${p.id}/verification?vessel=${vessel}`, { expectedVersion: p.version, contentHash: p.hash, decision, reason }),
}

export const emptyLog = (): LogContent => ({ timeZone: null, meters: [], fuel: { quantity: null, unit: null, sourceRef: null }, executionIds: [], defectIds: [], dailySummary: null })
export const emptyDrydock = (): DrydockContent => ({ projectReference: null, yard: null, plannedStart: null, plannedFinish: null,
  external: { estimatedCost: null, currency: null, vendor: null, budgetReference: null }, items: [], completionEvidence: [] })
