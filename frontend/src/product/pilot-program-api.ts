import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import type { Command } from './tug-readiness-api'

// Pilot program kit client contract (DEV-076/077/078/079 pilot weeks, DEV-095 dual-mode proof, DEV-096 tug value
// readout, DEV-121 v46.3 proof metrics). Closed server codes only; an unknown value or an impossible combination is a
// contract failure, never coerced to satisfied/accepted/expand. Software never completes a pilot: items are satisfied
// only by server-verified human links, values without a baseline are UNKNOWN, and only the appointed Product Owner
// closes a week or records CONTINUE/EXPAND/FIX/STOP.
export const weeks = ['W1', 'W2', 'W3', 'W4'] as const
export type WeekCode = typeof weeks[number]
export const areas = ['SHIP', 'TUG', 'SHARED'] as const
export const itemStates = ['SATISFIED', 'LINK_INVALID', 'MISSING'] as const
export const phases = ['NOT_STARTED', 'IN_PROGRESS', 'ENDED'] as const
export const weekRecommendations = ['READY_FOR_HUMAN_CLOSE', 'NOT_READY'] as const
export const closeOutcomes = ['ACCEPTED', 'NOT_ACCEPTED'] as const
export type CloseOutcome = typeof closeOutcomes[number]
export const programDecisions = ['CONTINUE', 'EXPAND', 'FIX', 'STOP'] as const
export type ProgramDecision = typeof programDecisions[number]
export const programRecommendations = ['EXPAND_ELIGIBLE_PENDING_HUMAN_DECISION', 'FIX_RECOMMENDED'] as const
export const issueSeverities = ['P0', 'P1', 'P2'] as const
export const issueStates = ['OPEN', 'TRIAGED', 'RESOLVED'] as const
export const issueCategories = ['ONBOARDING', 'CERTIFICATE', 'CREW', 'PMS', 'DEFECT', 'DEPARTURE', 'OFFLINE', 'SAFETY_EVENT', 'EVIDENCE', 'AUDIT', 'AI_ASSIST', 'TUG_JOB',
  'TOWAGE', 'UAT', 'VALUE', 'OTHER'] as const
export const timingStates = ['TRIAGED_SAME_DAY', 'TRIAGED_LATE', 'TRIAGE_DUE_TODAY', 'OVERDUE', 'NO_SOURCE_TARGET'] as const
export const feedbackCategories = ['USABILITY', 'DATA_QUALITY', 'PERFORMANCE', 'OFFLINE', 'AI_ASSIST', 'TRAINING', 'SAFETY_CONCERN', 'OTHER'] as const
export const sentiments = ['POSITIVE', 'NEUTRAL', 'NEGATIVE'] as const
export const feedbackRoles = ['MASTER', 'DPA', 'CHIEF_ENGINEER', 'CREW', 'TUG_MASTER', 'TUG_OPERATIONS_RESPONSIBLE', 'TUG_CREW', 'SHORE_OFFICE', 'OTHER'] as const
export const measurementPhases = ['BASELINE', 'PILOT'] as const
export const qualities = ['COMPLETE', 'UNKNOWN'] as const
export const comparisons = ['IMPROVED', 'WORSENED', 'UNCHANGED', 'NOT_COMPARABLE'] as const
export const units = ['SECOND', 'COUNT', 'PERCENT', 'PERSON'] as const
export const gateStates = ['PASS', 'FAIL', 'UNKNOWN', 'DEFERRED'] as const
export const dutyConflicts = ['LINK_AUTHOR', 'FEEDBACK_AUTHOR', 'MEASUREMENT_AUTHOR'] as const
export type DutyConflict = typeof dutyConflicts[number]

export type ChecklistItem = { code: string; week: WeekCode; requirement: string; area: typeof areas[number]; recordTypes: string[]; requiresAuditTrace: boolean; criterion: string; locator: string
  requiresCompletion: boolean }
export type WeekDefinition = { week: WeekCode; number: number; requirements: string[]; locator: string; items: ChecklistItem[] }
export type ItemOutcome = { code: string; state: typeof itemStates[number]; validLinks: number; reasons: string[] }
export type WeekOutcome = { week: WeekCode; phase: typeof phases[number]; recommendation: typeof weekRecommendations[number]; items: ItemOutcome[]; openP0: number; overdueP0: number
  lateP0: number; reasons: string[]; hash: string }
export type Link = { id: string; week: WeekCode; itemCode: string; recordType: string; recordId: string; auditTracedAtLink: boolean; note: string | null; actorId: string; linkedAt: string
  withdrawnBy: string | null; withdrawnAt: string | null; withdrawalReason: string | null; version: number; validity: 'VALID' | 'INVALID' | 'WITHDRAWN_OR_UNEVALUATED'; refusal: string | null }
export type WeekClose = { id: string; week: WeekCode; sequence: number; previousCloseId: string | null; outcome: CloseOutcome; recommendation: typeof weekRecommendations[number]
  weekHash: string; rationale: string; duty: 'ProductOwner'; actorId: string; recordedAt: string; validity: 'CURRENT' | 'STALE' }
export type WeekView = { definition: WeekDefinition; startDate: string; endDateExclusive: string; outcome: WeekOutcome; links: Link[]; closes: WeekClose[]; closeSequence: number
  feedbackCount: number; allowedOutcomes: CloseOutcome[]; closeDutyConflicts: DutyConflict[] }
export type Issue = { id: string; week: WeekCode; area: string; category: string; severity: typeof issueSeverities[number]; title: string; state: typeof issueStates[number]
  reportedBy: string; reportedAt: string; triagedBy: string | null; triagedAt: string | null; triageNote: string | null; resolvedBy: string | null; resolvedAt: string | null
  resolution: string | null; resolutionReference: string | null; timing: typeof timingStates[number]; reportedLocalDate: string; version: number }
export type Feedback = { id: string; week: WeekCode; area: string; category: string; sentiment: string; role: string; summary: string; participantPseudonym: string; actorId: string
  recordedAt: string; expiresAt: string }
export type Measurement = { id: string; metricId: string; mode: 'SHIP' | 'TUG'; phase: typeof measurementPhases[number]; sequence: number; previousMeasurementId: string | null
  value: number; unit: typeof units[number]; sampleSize: number; periodFrom: string; periodTo: string; evidenceReference: string; method: string; actorId: string; recordedAt: string }
export type Telemetry = { state: string; value: string | null; unitCode: string }
export type ValueRow = { metricId: string; mode: 'SHIP' | 'TUG'; unit: typeof units[number]; direction: string; quality: typeof qualities[number]; baseline: number | null
  current: number | null; delta: number | null; percentChange: number | null; comparison: typeof comparisons[number]; reasons: string[]; baselineId: string | null
  currentId: string | null; telemetry: Telemetry | null; locator: string }
export type GateInput = { gate: string; state: typeof gateStates[number]; reasons: string[]; source: string }
export type DefectSeverity = { mode: 'SHIP' | 'TUG'; state: typeof qualities[number]; critical: number; high: number; medium: number; low: number; unassessed: number; syntheticExcluded: number }
export type Readout = { metrics: ValueRow[]; telemetryState: string; telemetryBasis: string; telemetryRetention: string; telemetryWindowDays: number; uatShip: GateInput; uatTug: GateInput
  defects: DefectSeverity[]; openP0: number }
export type Review = { recommendation: typeof programRecommendations[number]; reasons: string[]; hash: string; authority: string }
export type Decision = { id: string; sequence: number; previousDecisionId: string | null; decision: ProgramDecision; recommendation: typeof programRecommendations[number]
  programHash: string; rationale: string; duty: 'ProductOwner'; actorId: string; recordedAt: string; validity: 'CURRENT' | 'STALE' }
export type ValueMetric = { id: string; mode: 'SHIP' | 'TUG'; unit: typeof units[number]; direction: string; requirements: string[]; telemetryMetricId: string | null; locator: string }
export type Program = { id: string; name: string; shipVesselId: string; tugVesselId: string | null; startDate: string; timeZone: string; catalogVersion: string; createdBy: string
  createdAt: string; provenance: 'UserDeclaredUnverified' | 'SyntheticUnverified' }
export type ProgramView = { program: Program; weeks: WeekView[]; issues: Issue[]; feedback: Feedback[]; measurements: Measurement[]; readout: Readout; review: Review
  decisions: Decision[]; decisionSequence: number; authorities: { id: string; duty: 'ProductOwner' }[]; canRecord: boolean; canTriage: boolean; canDecide: boolean
  allowedDecisions: ProgramDecision[]; feedbackRetention: 'CONFIGURED' | 'UNCONFIGURED'; metricCatalog: ValueMetric[]; recordTypes: string[]; assessedAt: string; authority: string
  sourceLocator: string; decisionDutyConflicts: DutyConflict[] }
export type VesselOption = { id: string; code: string; name: string; kind: 'SHIP' | 'TUG' }
export type ProgramList = { programs: Program[]; vessels: VesselOption[]; canCreate: boolean; catalogVersion: string }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
function fail(): never { throw new ApiError(502, 'invalid_pilot_program_contract') }
const text = (v: unknown, max = 4000) => typeof v === 'string' && v.length <= max
const opt = (v: unknown, max = 4000) => v === null || text(v, max)
const int = (v: unknown, min = 0) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const num = (v: unknown) => typeof v === 'number' && Number.isFinite(v)
const optNum = (v: unknown) => v === null || num(v)
const list = (v: unknown, max = 512): v is unknown[] => Array.isArray(v) && v.length <= max
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && values.includes(v)
const time = (v: unknown) => typeof v === 'string' && timestamp(v)
const optTime = (v: unknown) => v === null || time(v)
const optGuid = (v: unknown) => v === null || isGuid(v)
const date = (v: unknown) => typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v)
const hash = (v: unknown) => typeof v === 'string' && /^[0-9A-F]{64}$/.test(v)
const codes = (v: unknown, max = 512) => list(v, max) && v.every(x => text(x, 160))
const validity = (v: unknown) => oneOf(v, ['CURRENT', 'STALE'] as const)
const conflicts = (v: unknown) => list(v, 3) && v.every(x => oneOf(x, dutyConflicts))

function program(v: unknown): Program {
  if (!object(v) || !isGuid(v.id) || !text(v.name, 120) || !isGuid(v.shipVesselId) || !optGuid(v.tugVesselId) || !date(v.startDate) || !text(v.timeZone, 64) ||
    !text(v.catalogVersion, 40) || !isGuid(v.createdBy) || !time(v.createdAt) || !oneOf(v.provenance, ['UserDeclaredUnverified', 'SyntheticUnverified'] as const)) fail()
  return v as unknown as Program
}
function link(v: unknown): Link {
  if (!object(v) || !isGuid(v.id) || !oneOf(v.week, weeks) || !text(v.itemCode, 40) || !text(v.recordType, 40) || !isGuid(v.recordId) || typeof v.auditTracedAtLink !== 'boolean' ||
    !opt(v.note, 500) || !isGuid(v.actorId) || !time(v.linkedAt) || !optGuid(v.withdrawnBy) || !optTime(v.withdrawnAt) || !opt(v.withdrawalReason, 500) || !int(v.version) ||
    !oneOf(v.validity, ['VALID', 'INVALID', 'WITHDRAWN_OR_UNEVALUATED'] as const) || !opt(v.refusal, 80)) fail()
  // A valid link is active and refusal-free; an invalid one names the server refusal; withdrawal fields come together.
  if (v.validity === 'VALID' && (v.refusal !== null || v.withdrawnAt !== null) || v.validity === 'INVALID' && v.refusal === null) fail()
  if ((v.withdrawnAt === null) !== (v.withdrawnBy === null)) fail()
  return v as unknown as Link
}
function close(v: unknown): WeekClose {
  if (!object(v) || !isGuid(v.id) || !oneOf(v.week, weeks) || !int(v.sequence, 1) || !optGuid(v.previousCloseId) || !oneOf(v.outcome, closeOutcomes) ||
    !oneOf(v.recommendation, weekRecommendations) || !hash(v.weekHash) || !text(v.rationale, 2000) || v.duty !== 'ProductOwner' || !isGuid(v.actorId) || !time(v.recordedAt) ||
    !validity(v.validity)) fail()
  // ACCEPTED exists only against a ready week.
  if (v.outcome === 'ACCEPTED' && v.recommendation !== 'READY_FOR_HUMAN_CLOSE' || (v.sequence === 1) !== (v.previousCloseId === null)) fail()
  return v as unknown as WeekClose
}
function week(v: unknown): WeekView {
  if (!object(v) || !object(v.definition) || !date(v.startDate) || !date(v.endDateExclusive) || !object(v.outcome) || !list(v.links, 512) || !list(v.closes, 256) ||
    !int(v.closeSequence) || !int(v.feedbackCount) || !list(v.allowedOutcomes, 2) || !(v.allowedOutcomes as unknown[]).every(o => oneOf(o, closeOutcomes)) ||
    !conflicts(v.closeDutyConflicts)) fail()
  const d = v.definition, o = v.outcome
  if (!oneOf(d.week, weeks) || !int(d.number, 1) || !codes(d.requirements, 8) || !text(d.locator, 400) || !list(d.items, 32)) fail()
  if ((d.items as unknown[]).some(i => !object(i) || !text(i.code, 40) || i.week !== d.week || !text(i.requirement, 16) || !oneOf(i.area, areas) || !codes(i.recordTypes, 16) ||
    typeof i.requiresAuditTrace !== 'boolean' || typeof i.requiresCompletion !== 'boolean' || !text(i.criterion, 300) || !text(i.locator, 200))) fail()
  if (o.week !== d.week || !oneOf(o.phase, phases) || !oneOf(o.recommendation, weekRecommendations) || !list(o.items, 32) || !int(o.openP0) || !int(o.overdueP0) || !int(o.lateP0) ||
    !codes(o.reasons, 256) || !hash(o.hash)) fail()
  // A satisfied item needs at least one verified link; a ready week has every item satisfied and no reason.
  if ((o.items as unknown[]).some(i => !object(i) || !text(i.code, 40) || !oneOf(i.state, itemStates) || !int(i.validLinks) || !codes(i.reasons, 32) ||
    (i.state === 'SATISFIED') !== ((i.validLinks as number) > 0))) fail()
  const ready = (o.items as { state: string }[]).every(i => i.state === 'SATISFIED') && (o.reasons as unknown[]).length === 0
  if ((o.recommendation === 'READY_FOR_HUMAN_CLOSE') !== ready) fail()
  if ((v.allowedOutcomes as string[]).includes('ACCEPTED') && !ready) fail()
  ;(v.links as unknown[]).forEach(link); (v.closes as unknown[]).forEach(close)
  return v as unknown as WeekView
}
function issue(v: unknown): Issue {
  if (!object(v) || !isGuid(v.id) || !oneOf(v.week, weeks) || !oneOf(v.area, areas) || !oneOf(v.category, issueCategories) || !oneOf(v.severity, issueSeverities) || !text(v.title, 200) ||
    !oneOf(v.state, issueStates) || !isGuid(v.reportedBy) || !time(v.reportedAt) || !optGuid(v.triagedBy) || !optTime(v.triagedAt) || !opt(v.triageNote, 500) || !optGuid(v.resolvedBy) ||
    !optTime(v.resolvedAt) || !opt(v.resolution, 500) || !opt(v.resolutionReference, 200) || !oneOf(v.timing, timingStates) || !date(v.reportedLocalDate) || !int(v.version)) fail()
  if ((v.state === 'OPEN') !== (v.triagedAt === null) || (v.state === 'RESOLVED') !== (v.resolvedAt !== null)) fail()
  if (v.severity !== 'P0' && v.timing !== 'NO_SOURCE_TARGET') fail()
  return v as unknown as Issue
}
function feedback(v: unknown): Feedback {
  if (!object(v) || !isGuid(v.id) || !oneOf(v.week, weeks) || !oneOf(v.area, areas) || !oneOf(v.category, feedbackCategories) || !oneOf(v.sentiment, sentiments) ||
    !oneOf(v.role, feedbackRoles) || !text(v.summary, 500) || !text(v.participantPseudonym, 32) || !isGuid(v.actorId) || !time(v.recordedAt) || !time(v.expiresAt)) fail()
  return v as unknown as Feedback
}
function measurement(v: unknown): Measurement {
  if (!object(v) || !isGuid(v.id) || !text(v.metricId, 48) || !oneOf(v.mode, ['SHIP', 'TUG'] as const) || !oneOf(v.phase, measurementPhases) || !int(v.sequence, 1) ||
    !optGuid(v.previousMeasurementId) || !num(v.value) || (v.value as number) < 0 || !oneOf(v.unit, units) || !int(v.sampleSize, 1) || !date(v.periodFrom) || !date(v.periodTo) ||
    !text(v.evidenceReference, 200) || (v.evidenceReference as string).length === 0 || !text(v.method, 500) || !isGuid(v.actorId) || !time(v.recordedAt)) fail()
  return v as unknown as Measurement
}
function row(v: unknown): ValueRow {
  if (!object(v) || !text(v.metricId, 48) || !oneOf(v.mode, ['SHIP', 'TUG'] as const) || !oneOf(v.unit, units) || !text(v.direction, 20) || !oneOf(v.quality, qualities) ||
    !optNum(v.baseline) || !optNum(v.current) || !optNum(v.delta) || !optNum(v.percentChange) || !oneOf(v.comparison, comparisons) || !codes(v.reasons, 16) ||
    !optGuid(v.baselineId) || !optGuid(v.currentId) || !text(v.locator, 200)) fail()
  // UNKNOWN never carries a delta, a change or a comparison; COMPLETE has both measured sides. Zero is never invented.
  if (v.quality === 'UNKNOWN' && (v.delta !== null || v.percentChange !== null || v.comparison !== 'NOT_COMPARABLE')) fail()
  if (v.quality === 'COMPLETE' && (v.baseline === null || v.current === null || v.delta === null)) fail()
  if ((v.baseline === null) !== (v.baselineId === null) || (v.current === null) !== (v.currentId === null)) fail()
  if (v.telemetry !== null && (!object(v.telemetry) || !text(v.telemetry.state, 20) || !opt(v.telemetry.value, 40) || !text(v.telemetry.unitCode, 20) ||
    v.telemetry.state !== 'VALUE' && v.telemetry.value !== null)) fail()
  return v as unknown as ValueRow
}
function gate(v: unknown): GateInput {
  if (!object(v) || !text(v.gate, 20) || !oneOf(v.state, gateStates) || !codes(v.reasons, 256) || !text(v.source, 80)) fail()
  return v as unknown as GateInput
}
function decision(v: unknown): Decision {
  if (!object(v) || !isGuid(v.id) || !int(v.sequence, 1) || !optGuid(v.previousDecisionId) || !oneOf(v.decision, programDecisions) || !oneOf(v.recommendation, programRecommendations) ||
    !hash(v.programHash) || !text(v.rationale, 2000) || v.duty !== 'ProductOwner' || !isGuid(v.actorId) || !time(v.recordedAt) || !validity(v.validity)) fail()
  // EXPAND exists only against an expand-eligible review.
  if (v.decision === 'EXPAND' && v.recommendation !== 'EXPAND_ELIGIBLE_PENDING_HUMAN_DECISION' || (v.sequence === 1) !== (v.previousDecisionId === null)) fail()
  return v as unknown as Decision
}

export function decodeProgramView(v: unknown, id: string): ProgramView {
  if (!object(v) || !object(v.program) || !list(v.weeks, 4) || !list(v.issues, 1000) || !list(v.feedback, 200) || !list(v.measurements, 200) || !object(v.readout) ||
    !object(v.review) || !list(v.decisions, 32) || !int(v.decisionSequence) || !list(v.authorities, 64) || typeof v.canRecord !== 'boolean' || typeof v.canTriage !== 'boolean' ||
    typeof v.canDecide !== 'boolean' || !list(v.allowedDecisions, 4) || !oneOf(v.feedbackRetention, ['CONFIGURED', 'UNCONFIGURED'] as const) || !list(v.metricCatalog, 64) ||
    !codes(v.recordTypes, 64) || !time(v.assessedAt) || !text(v.sourceLocator, 400) || !conflicts(v.decisionDutyConflicts)) fail()
  if (program(v.program).id !== id) fail()
  if ((v.weeks as unknown[]).length !== 4) fail()
  ;(v.weeks as unknown[]).forEach(week)
  ;(v.issues as unknown[]).forEach(issue); (v.feedback as unknown[]).forEach(feedback); (v.measurements as unknown[]).forEach(measurement)
  const r = v.readout
  if (!list(r.metrics, 64) || !text(r.telemetryState, 20) || !text(r.telemetryBasis, 40) || !text(r.telemetryRetention, 20) || !int(r.telemetryWindowDays) || !list(r.defects, 2) || !int(r.openP0)) fail()
  ;(r.metrics as unknown[]).forEach(row); gate(r.uatShip); gate(r.uatTug)
  if ((r.defects as unknown[]).some(d => !object(d) || !oneOf(d.mode, ['SHIP', 'TUG'] as const) || !oneOf(d.state, qualities) || ![d.critical, d.high, d.medium, d.low, d.unassessed, d.syntheticExcluded].every(x => int(x)))) fail()
  const review = v.review
  if (!oneOf(review.recommendation, programRecommendations) || !codes(review.reasons, 256) || !hash(review.hash) || !text(review.authority, 200)) fail()
  // Never expand-eligible with an open reason; never offer EXPAND outside eligibility.
  const eligible = (review.reasons as unknown[]).length === 0
  if ((review.recommendation === 'EXPAND_ELIGIBLE_PENDING_HUMAN_DECISION') !== eligible) fail()
  if ((v.allowedDecisions as unknown[]).some(d => !oneOf(d, programDecisions)) || (v.allowedDecisions as string[]).includes('EXPAND') && !eligible) fail()
  if ((v.authorities as unknown[]).some(a => !object(a) || !isGuid(a.id) || a.duty !== 'ProductOwner')) fail()
  if (v.canDecide && (v.authorities as unknown[]).length === 0) fail()
  if ((v.metricCatalog as unknown[]).some(m => !object(m) || !text(m.id, 48) || !oneOf(m.mode, ['SHIP', 'TUG'] as const) || !oneOf(m.unit, units))) fail()
  ;(v.decisions as unknown[]).forEach(decision)
  return v as unknown as ProgramView
}

export function decodeProgramList(v: unknown): ProgramList {
  if (!object(v) || !list(v.programs, 100) || !list(v.vessels, 500) || typeof v.canCreate !== 'boolean' || !text(v.catalogVersion, 40)) fail()
  ;(v.programs as unknown[]).forEach(program)
  if ((v.vessels as unknown[]).some(x => !object(x) || !isGuid(x.id) || !text(x.code, 64) || !text(x.name, 200) || !oneOf(x.kind, ['SHIP', 'TUG'] as const))) fail()
  return v as unknown as ProgramList
}

const key = () => crypto.randomUUID()
const at = (id: string) => `pilot-programs/${id}`
export const pilotProgramApi = {
  list: async (signal: AbortSignal) => decodeProgramList(await request<unknown>('pilot-programs', signal)),
  read: async (id: string, signal: AbortSignal) => decodeProgramView(await request<unknown>(at(id), signal), id),
  create: (name: string, shipVesselId: string, tugVesselId: string | null, startDate: string, timeZone: string): Command =>
    ({ key: key(), method: 'POST', path: 'pilot-programs', body: { name, shipVesselId, tugVesselId, startDate, timeZone } }),
  link: (id: string, itemCode: string, recordType: string, recordId: string, note: string | null): Command =>
    ({ key: key(), method: 'POST', path: `${at(id)}/links`, body: { itemCode, recordType, recordId, note } }),
  withdraw: (id: string, l: Link, reason: string): Command => ({ key: key(), method: 'POST', path: `${at(id)}/links/${l.id}/withdraw`, body: { reason, expectedVersion: l.version } }),
  feedback: (id: string, week: string, area: string, category: string, sentiment: string, role: string, summary: string, participantPseudonym: string): Command =>
    ({ key: key(), method: 'POST', path: `${at(id)}/feedback`, body: { week, area, category, sentiment, role, summary, participantPseudonym } }),
  issue: (id: string, week: string, area: string, category: string, severity: string, title: string): Command =>
    ({ key: key(), method: 'POST', path: `${at(id)}/issues`, body: { week, area, category, severity, title } }),
  triage: (id: string, i: Issue, note: string): Command => ({ key: key(), method: 'POST', path: `${at(id)}/issues/${i.id}/triage`, body: { note, expectedVersion: i.version } }),
  resolve: (id: string, i: Issue, resolution: string, resolutionReference: string | null): Command =>
    ({ key: key(), method: 'POST', path: `${at(id)}/issues/${i.id}/resolve`, body: { resolution, resolutionReference, expectedVersion: i.version } }),
  measure: (id: string, metricId: string, phase: string, value: number, sampleSize: number, periodFrom: string, periodTo: string, evidenceReference: string, method: string,
    expectedSequence: number): Command =>
    ({ key: key(), method: 'POST', path: `${at(id)}/measurements`, body: { metricId, phase, value, sampleSize, periodFrom, periodTo, evidenceReference, method, expectedSequence } }),
  close: (id: string, w: WeekView, outcome: CloseOutcome, rationale: string, authorityId: string): Command =>
    ({ key: key(), method: 'POST', path: `${at(id)}/week-closes`,
      body: { week: w.definition.week, outcome, rationale, expectedWeekHash: w.outcome.hash, expectedSequence: w.closeSequence, authorityId } }),
  decide: (view: ProgramView, decisionCode: ProgramDecision, rationale: string, authorityId: string): Command =>
    ({ key: key(), method: 'POST', path: `${at(view.program.id)}/decisions`,
      body: { decision: decisionCode, rationale, expectedProgramHash: view.review.hash, expectedSequence: view.decisionSequence, authorityId } }),
}
