import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import { checkResults, type Command, type ReadinessCheck } from './tug-readiness-api'

// S8 WP-D DEV-087/088/107 client contracts. Closed server codes only; an unknown state is a contract
// failure, never coerced to READY/PASS. Plan approval and briefing issue are human records; GO is not here.
export const planStates = ['DRAFT', 'IN_REVIEW', 'RETURNED', 'APPROVED'] as const
export const briefingStates = ['DRAFT', 'ISSUED'] as const
export const ackStates = ['NOT_APPLICABLE', 'PENDING', 'COMPLETE', 'DISPUTED'] as const
export const riskStates = ['READY', 'ATTENTION', 'BLOCKED', 'REVIEW_REQUIRED'] as const
export const positions = ['CENTRE_LEAD_FORWARD', 'CENTRE_LEAD_AFT', 'PORT_BOW', 'STARBOARD_BOW', 'PORT_QUARTER', 'STARBOARD_QUARTER', 'ALONGSIDE_PORT', 'ALONGSIDE_STARBOARD', 'ESCORT', 'STANDBY'] as const
export const methods = ['TOWING_ON_LINE', 'PUSH_PULL', 'PUSHING_ONLY', 'MADE_FAST_ALONGSIDE', 'ESCORT'] as const
export const lineSources = ['TUG_LINE', 'SHIP_LINE'] as const
export const limitParameters = ['WIND_SPEED', 'SIGNIFICANT_WAVE_HEIGHT', 'CURRENT', 'VISIBILITY', 'SPEED_THROUGH_WATER', 'MIN_BOLLARD_PULL_MARGIN'] as const
export const scenarios = ['TUG_PROPULSION_LOSS', 'SHIP_PROPULSION_OR_STEERING_LOSS', 'TOWLINE_PARTING', 'COMMUNICATION_LOSS', 'GIRTING_OR_CAPSIZE_RISK', 'LIMITS_EXCEEDED', 'PERSON_OVERBOARD', 'FIRE_OR_FLOODING', 'OTHER'] as const
export const participantRoles = ['TUG_MASTER', 'MASTER', 'PILOT', 'MATE', 'DECK_CREW', 'ENGINEER', 'OTHER'] as const
export const highRiskTypes = ['HOT_WORK', 'ENCLOSED_SPACE', 'WORKING_ALOFT', 'ELECTRICAL_ISOLATION', 'CRITICAL_MAINTENANCE', 'TOWAGE', 'BUNKERING', 'OTHER', 'UNCLASSIFIED'] as const
export const nextActions = ['RESOLVE_STOP', 'RENEW_EXPIRED', 'COMPLETE_REQUIRED', 'REASSESS', 'WAIT_SOURCE', 'OPEN_OWNER', 'NONE'] as const

export type TugRole = { tugId: string; position: string | null; method: string | null; coordinating: boolean; lineSource: string | null; connectionPoint: string | null
  towlineItemId: string | null; emergencyReleaseItemId: string | null }
export type Limit = { parameter: string; value: string | null; sourceRef: string | null }
export type Item = { id: string; description: string }
export type PlanContent = { tugs: TugRole[]; informationExchange: string | null; limits: Limit[]
  communication: { primaryChannel: string | null; standbyChannel: string | null; workingLanguage: string | null; lostCommunicationProcedure: string | null }
  emergencyRelease: { procedure: string | null; orderedBy: string | null }; contingencies: { id: string; scenario: string; action: string }[]
  snapBack: { zonesMarked: boolean | null; controls: Item[] } }
export type Authority = { id: string; duty: 'TugMaster' | 'Master' }
export type Revision = { revision: number; state: string; reasonCode: string; hash: string; actorId: string; savedAt: string }
export type Plan = { id: string; vesselId: string; jobId: string; jobRevision: number; revision: number; version: number; state: typeof planStates[number]; content: PlanContent
  hash: string; approvedRevision: number | null; submittedBy: string | null; provenance: string; savedAt: string }
export type PlanView = { vesselId: string; jobId: string; jobRevision: number; requestState: string; plan: Plan | null; blockers: string[]; assignedTugs: string[]
  decisions: { id: string; revision: number; resultRevision: number; decision: 'APPROVE' | 'RETURN'; duty: string; tugId: string; actorId: string; reason: string; decidedAt: string }[]
  revisions: Revision[]; authorities: Authority[]; canEdit: boolean; canSubmit: boolean; canDecide: boolean; canAmend: boolean; limitUnits: Record<string, string>
  approvalAuthority: string; sourceVersion: string }
export type Participant = { actorId: string; role: string; tugId: string | null }
export type BriefingContent = { weather: string | null; current: string | null; visibility: string | null; hazards: Item[]; preventiveBarriers: Item[]; mitigatingBarriers: Item[]
  stopWorkAuthority: string | null; stopWorkTriggers: Item[]; emergencyArrangements: string | null; riskAssessmentId: string | null; participants: Participant[] }
export type Briefing = { id: string; vesselId: string; jobId: string; revision: number; version: number; state: typeof briefingStates[number]; content: BriefingContent; hash: string
  planRevision: number | null; riskRevision: number | null; provenance: string; savedAt: string }
export type BriefingView = { vesselId: string; jobId: string; briefing: Briefing | null; blockers: string[]; acknowledgementState: typeof ackStates[number]
  acknowledgements: { actorId: string; revision: number; choice: 'ACK' | 'DISPUTE'; reason: string | null; recordedAt: string }[]
  risk: { assessmentId: string | null; state: typeof riskStates[number]; reason: string; revision: number | null; approvedRevision: number | null; acknowledgementState: string | null }
  planState: string | null; planRevision: number | null; readinessChecks: ReadinessCheck[]; revisions: Revision[]; authorities: Authority[]
  canEdit: boolean; canIssue: boolean; canAmend: boolean; canAcknowledge: boolean; readinessEffect: string; sourceVersion: string }
export type Dimension = { dimension: string; state: string; reason: string; sourceId: string | null }
export type HighRiskRow = { workKind: 'TugJob' | 'RiskAssessment'; workId: string; workRef: string; vesselId: string; vesselCode: string; vesselName: string; vesselKind: string
  workType: typeof highRiskTypes[number]; plannedStart: string | null; responsibleRole: string | null; riskState: string; permitState: string; criticalBarrierState: string
  crewReadiness: string; equipmentReadiness: string; nextAction: { kind: typeof nextActions[number]; dimension: string | null; reason: string | null }
  ownerDue: { ownerRole: string | null; dueAt: string | null; sourceKind: string | null }; why: Dimension[]; criticalBlockers: number; reviewItems: number; link: string }
export type HighRiskPage = { items: HighRiskRow[]; offset: number; limit: number; hasMore: boolean; coverage: string[]; workTypes: string[]
  vessels: { id: string; code: string; name: string; kind: string; companyId: string }[]; fleets: { id: string; name: string }[]; evaluatedAt: string; scoring: string }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
function fail(): never { throw new ApiError(502, 'invalid_towage_contract') }
const text = (v: unknown, max = 4000) => typeof v === 'string' && v.length <= max
const opt = (v: unknown, max = 4000) => v === null || text(v, max)
const int = (v: unknown, min = 0) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const list = (v: unknown, max = 512): v is unknown[] => Array.isArray(v) && v.length <= max
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && values.includes(v)
const guid = (v: unknown): v is string => isGuid(v)
const optGuid = (v: unknown) => v === null || guid(v)
const time = (v: unknown) => typeof v === 'string' && timestamp(v)
const hash = (v: unknown) => typeof v === 'string' && /^[0-9A-F]{64}$/.test(v)
const strings = (v: unknown, max = 128) => list(v, max) && v.every(x => text(x, 200))
const items = (v: unknown) => list(v, 30) && v.every(x => object(x) && guid(x.id) && text(x.description))

function authorities(v: unknown): Authority[] {
  if (!list(v, 32) || v.some(a => !object(a) || !guid(a.id) || !oneOf(a.duty, ['TugMaster', 'Master'] as const))) fail()
  return v as Authority[]
}
function revisions(v: unknown): Revision[] {
  if (!list(v, 50) || v.some(r => !object(r) || !int(r.revision, 1) || !text(r.state, 16) || !text(r.reasonCode, 32) || !hash(r.hash) || !guid(r.actorId) || !time(r.savedAt))) fail()
  return v as Revision[]
}
function check(v: unknown): ReadinessCheck {
  if (!object(v) || !text(v.category, 40) || !oneOf(v.result, checkResults) || !text(v.reason, 300) || typeof v.critical !== 'boolean' || !optGuid(v.sourceId)) fail()
  return v as ReadinessCheck
}
function planContent(c: unknown): PlanContent {
  if (!object(c) || !list(c.tugs, 12) || !opt(c.informationExchange) || !list(c.limits, 12) || !object(c.communication) || !object(c.emergencyRelease) ||
    !list(c.contingencies, 30) || !object(c.snapBack) || !(c.snapBack.zonesMarked === null || typeof c.snapBack.zonesMarked === 'boolean') || !items(c.snapBack.controls)) fail()
  for (const t of c.tugs as unknown[]) if (!object(t) || !guid(t.tugId) || !(t.position === null || oneOf(t.position, positions)) || !(t.method === null || oneOf(t.method, methods)) ||
    typeof t.coordinating !== 'boolean' || !(t.lineSource === null || oneOf(t.lineSource, lineSources)) || !opt(t.connectionPoint, 200) || !optGuid(t.towlineItemId) || !optGuid(t.emergencyReleaseItemId)) fail()
  for (const l of c.limits as unknown[]) if (!object(l) || !oneOf(l.parameter, limitParameters) || !opt(l.value, 12) || !opt(l.sourceRef, 200)) fail()
  for (const x of c.contingencies as unknown[]) if (!object(x) || !guid(x.id) || !oneOf(x.scenario, scenarios) || !text(x.action)) fail()
  const m = c.communication, e = c.emergencyRelease
  if (![m.primaryChannel, m.standbyChannel, m.workingLanguage].every(x => opt(x, 200)) || !opt(m.lostCommunicationProcedure) || !opt(e.procedure) || !opt(e.orderedBy, 200)) fail()
  return c as unknown as PlanContent
}
export function decodePlanView(v: unknown, vessel: string, job: string): PlanView {
  if (!object(v) || v.vesselId !== vessel || v.jobId !== job || !int(v.jobRevision, 1) || !text(v.requestState, 40) || !strings(v.blockers) || !list(v.assignedTugs, 128) ||
    !(v.assignedTugs as unknown[]).every(guid) || !list(v.decisions, 32) || typeof v.canEdit !== 'boolean' || typeof v.canSubmit !== 'boolean' || typeof v.canDecide !== 'boolean' ||
    typeof v.canAmend !== 'boolean' || !object(v.limitUnits) || !text(v.approvalAuthority, 200) || !text(v.sourceVersion, 200)) fail()
  if (v.plan !== null) {
    const p = v.plan
    if (!object(p) || !guid(p.id) || p.vesselId !== vessel || p.jobId !== job || !int(p.jobRevision, 1) || !int(p.revision, 1) || !int(p.version, 0) || !oneOf(p.state, planStates) ||
      !hash(p.hash) || !(p.approvedRevision === null || int(p.approvedRevision, 1)) || !optGuid(p.submittedBy) || !text(p.provenance, 40) || !time(p.savedAt)) fail()
    // An APPROVED head must point at its own current revision; anything else is never shown as approved.
    if (p.state === 'APPROVED' && p.approvedRevision !== p.revision) fail()
    planContent(p.content)
  }
  for (const d of v.decisions as unknown[]) if (!object(d) || !guid(d.id) || !oneOf(d.decision, ['APPROVE', 'RETURN'] as const) || !text(d.duty, 20) || !guid(d.actorId) || !text(d.reason, 1000) || !time(d.decidedAt)) fail()
  revisions(v.revisions); authorities(v.authorities)
  return v as unknown as PlanView
}
function briefingContent(c: unknown): BriefingContent {
  if (!object(c) || ![c.weather, c.current, c.visibility, c.stopWorkAuthority, c.emergencyArrangements].every(x => opt(x)) || !items(c.hazards) || !items(c.preventiveBarriers) ||
    !items(c.mitigatingBarriers) || !items(c.stopWorkTriggers) || !optGuid(c.riskAssessmentId) || !list(c.participants, 50) ||
    (c.participants as unknown[]).some(p => !object(p) || !guid(p.actorId) || !oneOf(p.role, participantRoles) || !optGuid(p.tugId))) fail()
  return c as unknown as BriefingContent
}
export function decodeBriefingView(v: unknown, vessel: string, job: string): BriefingView {
  if (!object(v) || v.vesselId !== vessel || v.jobId !== job || !strings(v.blockers) || !oneOf(v.acknowledgementState, ackStates) || !list(v.acknowledgements, 100) ||
    !object(v.risk) || !oneOf(v.risk.state, riskStates) || !text(v.risk.reason, 80) || !optGuid(v.risk.assessmentId) || !opt(v.planState, 16) || !list(v.readinessChecks, 32) ||
    typeof v.canEdit !== 'boolean' || typeof v.canIssue !== 'boolean' || typeof v.canAmend !== 'boolean' || typeof v.canAcknowledge !== 'boolean' || !text(v.readinessEffect, 200)) fail()
  if (v.briefing !== null) {
    const b = v.briefing
    if (!object(b) || !guid(b.id) || b.vesselId !== vessel || b.jobId !== job || !int(b.revision, 1) || !int(b.version, 0) || !oneOf(b.state, briefingStates) || !hash(b.hash) ||
      !(b.planRevision === null || int(b.planRevision, 1)) || !(b.riskRevision === null || int(b.riskRevision, 1)) || !text(b.provenance, 40) || !time(b.savedAt)) fail()
    if (b.state === 'ISSUED' && (b.planRevision === null || b.riskRevision === null)) fail()
    briefingContent(b.content)
  }
  for (const k of v.acknowledgements as unknown[]) if (!object(k) || !guid(k.actorId) || !int(k.revision, 1) || !oneOf(k.choice, ['ACK', 'DISPUTE'] as const) || !opt(k.reason, 1000) || !time(k.recordedAt)) fail()
  ;(v.readinessChecks as unknown[]).forEach(check)
  // An incomplete briefing is never presented as a passing readiness input.
  if ((v.readinessChecks as ReadinessCheck[]).some(c => c.result === 'PASS') && v.acknowledgementState !== 'COMPLETE') fail()
  revisions(v.revisions); authorities(v.authorities)
  return v as unknown as BriefingView
}
export function decodeHighRiskPage(v: unknown): HighRiskPage {
  if (!object(v) || !list(v.items, 20) || !int(v.offset) || !int(v.limit, 1) || typeof v.hasMore !== 'boolean' || !strings(v.coverage, 32) || !list(v.vessels, 500) ||
    !list(v.fleets, 500) || !time(v.evaluatedAt) || !text(v.scoring, 200)) fail()
  for (const r of v.items as unknown[]) {
    if (!object(r) || !oneOf(r.workKind, ['TugJob', 'RiskAssessment'] as const) || !guid(r.workId) || !text(r.workRef, 80) || !guid(r.vesselId) || !text(r.vesselCode, 80) ||
      !text(r.vesselName, 200) || !oneOf(r.workType, highRiskTypes) || !(r.plannedStart === null || time(r.plannedStart)) || !opt(r.responsibleRole, 40) ||
      ![r.riskState, r.permitState, r.criticalBarrierState, r.crewReadiness, r.equipmentReadiness].every(x => text(x, 20)) || !object(r.nextAction) ||
      !oneOf(r.nextAction.kind, nextActions) || !object(r.ownerDue) || !(r.ownerDue.dueAt === null || time(r.ownerDue.dueAt)) || !list(r.why, 64) ||
      !int(r.criticalBlockers) || !int(r.reviewItems) || !text(r.link, 300) || !String(r.link).startsWith('/app/')) fail()
    const why = r.why as unknown[]
    if (why.some(d => !object(d) || !text(d.dimension, 40) || !text(d.state, 20) || !text(d.reason, 120) || !optGuid(d.sourceId))) fail()
    // Defense in depth: a critical blocker can never coexist with "no action", and permit stays UNKNOWN until a PTW source exists.
    if ((r.criticalBlockers as number) > 0 && r.nextAction.kind === 'NONE') fail()
    if (r.permitState !== 'UNKNOWN') fail()
  }
  return v as unknown as HighRiskPage
}

const planPath = (vessel: string, suffix = '') => `towage-plans${suffix}?vessel=${vessel}`
const briefingPath = (vessel: string, suffix = '') => `towage-risk-briefings${suffix}?vessel=${vessel}`
const cmd = (method: 'POST' | 'PUT', path: string, body: Record<string, unknown>): Command => ({ key: crypto.randomUUID(), method, path, body })

export const towageApi = {
  plan: async (vessel: string, job: string, signal: AbortSignal) => decodePlanView(await request<unknown>(`towage-plans?vessel=${vessel}&job=${job}`, signal), vessel, job),
  briefing: async (vessel: string, job: string, signal: AbortSignal) => decodeBriefingView(await request<unknown>(`towage-risk-briefings?vessel=${vessel}&job=${job}`, signal), vessel, job),
  highRisk: async (filter: { companyId?: string; vesselId?: string; workType?: string; offset: number }, signal: AbortSignal) => {
    const q = new URLSearchParams()
    if (filter.companyId) q.set('companyId', filter.companyId)
    if (filter.vesselId) q.set('vesselId', filter.vesselId)
    if (filter.workType) q.set('workType', filter.workType)
    if (filter.offset > 0) q.set('offset', String(filter.offset))
    const s = q.toString()
    return decodeHighRiskPage(await request<unknown>(`hse/high-risk-work${s ? `?${s}` : ''}`, signal))
  },
  savePlan: (vessel: string, job: string, plan: Plan | null, content: PlanContent): Command => plan === null
    ? cmd('POST', planPath(vessel), { id: crypto.randomUUID(), jobId: job, content })
    : cmd('PUT', planPath(vessel, `/${plan.id}`), { expectedVersion: plan.version, content }),
  submitPlan: (vessel: string, plan: Plan): Command => cmd('POST', planPath(vessel, `/${plan.id}/submit`), { expectedVersion: plan.version, contentHash: plan.hash }),
  decidePlan: (vessel: string, plan: Plan, decision: 'APPROVE' | 'RETURN', reason: string, authorityId: string): Command =>
    cmd('POST', planPath(vessel, `/${plan.id}/decisions`), { expectedVersion: plan.version, contentHash: plan.hash, decision, reason, authorityId }),
  amendPlan: (vessel: string, plan: Plan, reason: string): Command => cmd('POST', planPath(vessel, `/${plan.id}/amend`), { expectedVersion: plan.version, reason }),
  saveBriefing: (vessel: string, job: string, briefing: Briefing | null, content: BriefingContent): Command => briefing === null
    ? cmd('POST', briefingPath(vessel), { id: crypto.randomUUID(), jobId: job, content })
    : cmd('PUT', briefingPath(vessel, `/${briefing.id}`), { expectedVersion: briefing.version, content }),
  issueBriefing: (vessel: string, b: Briefing, authorityId: string): Command => cmd('POST', briefingPath(vessel, `/${b.id}/issue`), { expectedVersion: b.version, contentHash: b.hash, authorityId }),
  amendBriefing: (vessel: string, b: Briefing, reason: string): Command => cmd('POST', briefingPath(vessel, `/${b.id}/amend`), { expectedVersion: b.version, reason }),
  acknowledge: (vessel: string, b: Briefing, choice: 'ACK' | 'DISPUTE', reason: string | null): Command =>
    cmd('POST', briefingPath(vessel, `/${b.id}/acknowledgements`), { revision: b.revision, contentHash: b.hash, choice, ...(reason ? { reason } : {}) }),
}

export const emptyPlan = (): PlanContent => ({ tugs: [], informationExchange: null, limits: [], communication: { primaryChannel: null, standbyChannel: null, workingLanguage: null, lostCommunicationProcedure: null },
  emergencyRelease: { procedure: null, orderedBy: null }, contingencies: [], snapBack: { zonesMarked: null, controls: [] } })
export const emptyBriefing = (): BriefingContent => ({ weather: null, current: null, visibility: null, hazards: [], preventiveBarriers: [], mitigatingBarriers: [], stopWorkAuthority: null,
  stopWorkTriggers: [], emergencyArrangements: null, riskAssessmentId: null, participants: [] })
