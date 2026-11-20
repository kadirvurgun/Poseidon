import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import type { Command } from './tug-readiness-api'

// S11 WP-D DEV-074/094 UAT records and DEV-075 pilot go/no-go client contract. Closed server codes only; an unknown
// value is a contract failure, never coerced to PASS/GO. The engine never accepts UAT and never emits GO: only the
// appointed Product Owner records GO/NO_GO/CONDITIONAL_GO, and GO only against a GO-eligible review.
export const uatScopes = ['SHIP', 'TUG'] as const
export type UatScope = typeof uatScopes[number]
export const uatOutcomes = ['PASS', 'FAIL', 'BLOCKED', 'NOT_RUN'] as const
export const uatSeverities = ['BLOCKER', 'MAJOR', 'MINOR'] as const
export const uatFindingStates = ['OPEN', 'RESOLVED', 'ACCEPTED_RISK'] as const
export const uatRunStates = ['OPEN', 'SUBMITTED', 'ATTESTED'] as const
export const uatStates = ['THRESHOLD_UNSET', 'CRITERIA_NOT_MET', 'EVIDENCE_INCOMPLETE', 'CRITERIA_MET_PENDING_HUMAN_GO'] as const
export const gateStates = ['PASS', 'FAIL', 'UNKNOWN', 'DEFERRED'] as const
export type GateState = typeof gateStates[number]
export const pilotGates = ['SECURITY', 'REGULATORY', 'OFFLINE', 'DR', 'UAT_SHIP', 'UAT_TUG', 'DATA', 'TRACE', 'SUPPORT'] as const
export const recommendations = ['GO_ELIGIBLE_PENDING_HUMAN_DECISION', 'NO_GO_RECOMMENDED'] as const
export const pilotDecisions = ['GO', 'NO_GO', 'CONDITIONAL_GO'] as const
export type PilotDecisionCode = typeof pilotDecisions[number]
export const supportItems = ['SUPPORT_CONTACTS', 'ONCALL_ROTA', 'ESCALATION_PATH', 'ROLLBACK_OWNER', 'INCIDENT_COMMUNICATION', 'MONITORING_ALERTING'] as const
export const supportStates = ['READY', 'NOT_READY'] as const

export type UatScenario = { id: string; scope: UatScope; journey: string; passProof: string; locator: string }
export type UatCatalog = { version: string; scenarios: UatScenario[]; shipRoles: string[]; tugRoles: string[]; environments: string[] }
export type UatThreshold = { id: string; scope: UatScope; sequence: number; minScenarioPassPercent: number; maxOpenMajorFindings: number; requireDesignConformance: boolean
  agreementReference: string; rationale: string; actorId: string; recordedAt: string }
export type UatRun = { id: string; scope: UatScope; role: string; environment: string; buildCommit: string; participantPseudonym: string; catalogVersion: string
  state: typeof uatRunStates[number]; facilitatorActorId: string; createdAt: string; submittedAt: string | null; attestedBy: string | null; attestedAt: string | null
  attestationStatement: string | null; provenance: string; version: number }
export type UatResult = { id: string; runId: string; scenarioId: string; attempt: number; outcome: typeof uatOutcomes[number]; durationSeconds: number | null; assisted: boolean
  automatedEvidenceReference: string | null; note: string | null; recordedBy: string; recordedAt: string }
export type UatFinding = { id: string; runId: string; scope: UatScope; scenarioId: string; severity: typeof uatSeverities[number]; isUx: boolean; designConformance: boolean
  title: string; state: typeof uatFindingStates[number]; reportedBy: string; reportedAt: string; resolvedBy: string | null; version: number }
export type UatScenarioOutcome = { scenarioId: string; attestedRuns: number; passed: number; failed: number; blocked: number; notRun: number; passPercent: number | null }
export type UatGate = { scope: UatScope; state: typeof uatStates[number]; gateState: GateState; reasons: string[]; scenarios: UatScenarioOutcome[]; openBlockers: number
  openMajors: number; openDesignConformance: number; attestedRuns: number; pendingAttestation: number }
export type UatSummary = { scope: UatScope; catalogVersion: string; threshold: UatThreshold | null; thresholdSequence: number; gate: UatGate; runs: UatRun[]
  openFindings: UatFinding[]; canRecord: boolean; canRecordThreshold: boolean; canAttest: boolean }
export type UatRunDetail = { run: UatRun; results: UatResult[]; findings: UatFinding[]; canRecord: boolean; canAttest: boolean }

export type PilotGateInput = { gate: typeof pilotGates[number]; state: GateState; reasons: string[]; source: string }
export type PilotReview = { recommendation: typeof recommendations[number]; gates: PilotGateInput[]; openP0: number | null; noGoReasons: string[]; hash: string; authority: string }
export type PilotSupportCheck = { id: string; item: string; sequence: number; state: typeof supportStates[number]; evidenceReference: string | null; note: string; actorId: string; recordedAt: string }
export type PilotDecision = { id: string; sequence: number; previousDecisionId: string | null; decision: PilotDecisionCode; recommendation: typeof recommendations[number]
  reviewHash: string; conditions: string[]; rationale: string; duty: 'ProductOwner'; actorId: string; recordedAt: string; validity: 'CURRENT' | 'STALE' }
export type PilotGateView = { review: PilotReview; support: { item: typeof supportItems[number]; latest: PilotSupportCheck | null }[]; decisions: PilotDecision[]; sequence: number
  authorities: { id: string; duty: 'ProductOwner' }[]; canDecide: boolean; canRecordSupport: boolean; allowedDecisions: PilotDecisionCode[]; assessedAt: string; sourceLocator: string }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
function fail(): never { throw new ApiError(502, 'invalid_pilot_gate_contract') }
const text = (v: unknown, max = 4000) => typeof v === 'string' && v.length <= max
const opt = (v: unknown, max = 4000) => v === null || text(v, max)
const int = (v: unknown, min = 0) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const list = (v: unknown, max = 512): v is unknown[] => Array.isArray(v) && v.length <= max
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && values.includes(v)
const time = (v: unknown) => typeof v === 'string' && timestamp(v)
const optTime = (v: unknown) => v === null || time(v)
const optGuid = (v: unknown) => v === null || isGuid(v)
const hash = (v: unknown) => typeof v === 'string' && /^[0-9A-F]{64}$/.test(v)
const codes = (v: unknown, max = 512) => list(v, max) && v.every(x => text(x, 160))

function run(v: unknown): UatRun {
  if (!object(v) || !isGuid(v.id) || !oneOf(v.scope, uatScopes) || !text(v.role, 40) || !text(v.environment, 20) || !text(v.buildCommit, 40) || !text(v.participantPseudonym, 32) ||
    !text(v.catalogVersion, 40) || !oneOf(v.state, uatRunStates) || !isGuid(v.facilitatorActorId) || !time(v.createdAt) || !optTime(v.submittedAt) || !optGuid(v.attestedBy) ||
    !optTime(v.attestedAt) || !opt(v.attestationStatement, 500) || !text(v.provenance, 32) || !int(v.version)) fail()
  // An attestation exists exactly in ATTESTED and never by the facilitator.
  if ((v.state === 'ATTESTED') !== (v.attestedBy !== null) || v.attestedBy === v.facilitatorActorId) fail()
  return v as unknown as UatRun
}
function finding(v: unknown): UatFinding {
  if (!object(v) || !isGuid(v.id) || !isGuid(v.runId) || !oneOf(v.scope, uatScopes) || !text(v.scenarioId, 12) || !oneOf(v.severity, uatSeverities) || typeof v.isUx !== 'boolean' ||
    typeof v.designConformance !== 'boolean' || !text(v.title, 200) || !oneOf(v.state, uatFindingStates) || !isGuid(v.reportedBy) || !time(v.reportedAt) || !optGuid(v.resolvedBy) || !int(v.version)) fail()
  if (v.severity === 'BLOCKER' && v.state === 'ACCEPTED_RISK') fail()
  return v as unknown as UatFinding
}
function result(v: unknown): UatResult {
  if (!object(v) || !isGuid(v.id) || !isGuid(v.runId) || !text(v.scenarioId, 12) || !int(v.attempt, 1) || !oneOf(v.outcome, uatOutcomes) || !(v.durationSeconds === null || int(v.durationSeconds)) ||
    typeof v.assisted !== 'boolean' || !opt(v.automatedEvidenceReference, 200) || !opt(v.note, 500) || !isGuid(v.recordedBy) || !time(v.recordedAt)) fail()
  return v as unknown as UatResult
}
function threshold(v: unknown): UatThreshold {
  if (!object(v) || !isGuid(v.id) || !oneOf(v.scope, uatScopes) || !int(v.sequence, 1) || !int(v.minScenarioPassPercent, 1) || (v.minScenarioPassPercent as number) > 100 ||
    !int(v.maxOpenMajorFindings) || typeof v.requireDesignConformance !== 'boolean' || !text(v.agreementReference, 200) || !text(v.rationale, 500) || !isGuid(v.actorId) || !time(v.recordedAt)) fail()
  return v as unknown as UatThreshold
}

export function decodeUatSummary(v: unknown, scope: UatScope): UatSummary {
  if (!object(v) || v.scope !== scope || !text(v.catalogVersion, 40) || !(v.threshold === null || object(v.threshold)) || !int(v.thresholdSequence) || !object(v.gate) ||
    !list(v.runs, 100) || !list(v.openFindings, 200) || typeof v.canRecord !== 'boolean' || typeof v.canRecordThreshold !== 'boolean' || typeof v.canAttest !== 'boolean') fail()
  const g = v.gate
  if (g.scope !== scope || !oneOf(g.state, uatStates) || !oneOf(g.gateState, gateStates) || !codes(g.reasons, 256) || !list(g.scenarios, 16) || !int(g.openBlockers) ||
    !int(g.openMajors) || !int(g.openDesignConformance) || !int(g.attestedRuns) || !int(g.pendingAttestation)) fail()
  // Defense in depth: no threshold is never a pass, an open blocker is always a FAIL, only CRITERIA_MET maps to PASS.
  if (v.threshold === null && g.gateState === 'PASS' || (g.openBlockers as number) > 0 && g.gateState !== 'FAIL' || (g.gateState === 'PASS') !== (g.state === 'CRITERIA_MET_PENDING_HUMAN_GO')) fail()
  if ((g.scenarios as unknown[]).some(s => !object(s) || !text(s.scenarioId, 12) || !int(s.attestedRuns) || !int(s.passed) || !int(s.failed) || !int(s.blocked) || !int(s.notRun) ||
    !(s.passPercent === null || int(s.passPercent) && (s.passPercent as number) <= 100))) fail()
  if (v.threshold !== null) threshold(v.threshold)
  ;(v.runs as unknown[]).forEach(run); (v.openFindings as unknown[]).forEach(finding)
  return v as unknown as UatSummary
}

export function decodeUatRunDetail(v: unknown, id: string): UatRunDetail {
  if (!object(v) || !object(v.run) || v.run.id !== id || !list(v.results, 512) || !list(v.findings, 256) || typeof v.canRecord !== 'boolean' || typeof v.canAttest !== 'boolean') fail()
  run(v.run); (v.results as unknown[]).forEach(result); (v.findings as unknown[]).forEach(finding)
  return v as unknown as UatRunDetail
}

export function decodeUatCatalog(v: unknown): UatCatalog {
  if (!object(v) || !text(v.version, 40) || !list(v.scenarios, 64) || !codes(v.shipRoles, 16) || !codes(v.tugRoles, 16) || !codes(v.environments, 8)) fail()
  if ((v.scenarios as unknown[]).some(s => !object(s) || !text(s.id, 12) || !oneOf(s.scope, uatScopes) || !text(s.journey, 80) || !text(s.passProof, 300) || !text(s.locator, 200))) fail()
  return v as unknown as UatCatalog
}

function decision(v: unknown): PilotDecision {
  if (!object(v) || !isGuid(v.id) || !int(v.sequence, 1) || !optGuid(v.previousDecisionId) || !oneOf(v.decision, pilotDecisions) || !oneOf(v.recommendation, recommendations) ||
    !hash(v.reviewHash) || !list(v.conditions, 20) || !(v.conditions as unknown[]).every(x => text(x, 300)) || !text(v.rationale, 2000) || v.duty !== 'ProductOwner' || !isGuid(v.actorId) || !time(v.recordedAt) ||
    !oneOf(v.validity, ['CURRENT', 'STALE'] as const)) fail()
  // GO exists only against a GO-eligible review.
  if (v.decision === 'GO' && v.recommendation !== 'GO_ELIGIBLE_PENDING_HUMAN_DECISION') fail()
  if ((v.sequence === 1) !== (v.previousDecisionId === null)) fail()
  return v as unknown as PilotDecision
}

export function decodePilotGate(v: unknown): PilotGateView {
  if (!object(v) || !object(v.review) || !list(v.support, 6) || !list(v.decisions, 32) || !int(v.sequence) || !list(v.authorities, 64) || typeof v.canDecide !== 'boolean' ||
    typeof v.canRecordSupport !== 'boolean' || !list(v.allowedDecisions, 3) || !time(v.assessedAt) || !text(v.sourceLocator, 300)) fail()
  const r = v.review
  if (!oneOf(r.recommendation, recommendations) || !list(r.gates, 9) || !(r.openP0 === null || int(r.openP0)) || !codes(r.noGoReasons, 64) || !hash(r.hash) || !text(r.authority, 200)) fail()
  if ((r.gates as unknown[]).some(g => !object(g) || !oneOf(g.gate, pilotGates) || !oneOf(g.state, gateStates) || !codes(g.reasons, 256) || !text(g.source, 80))) fail()
  if (pilotGates.some(k => (r.gates as { gate: string }[]).filter(g => g.gate === k).length !== 1)) fail()
  // Never GO-eligible with a non-PASS gate or an unknown/open P0; never offer GO outside GO-eligible.
  const eligible = (r.gates as { state: string }[]).every(g => g.state === 'PASS') && r.openP0 === 0
  if ((r.recommendation === 'GO_ELIGIBLE_PENDING_HUMAN_DECISION') !== eligible) fail()
  if ((v.allowedDecisions as unknown[]).some(d => !oneOf(d, pilotDecisions)) || (v.allowedDecisions as string[]).includes('GO') && !eligible) fail()
  if ((v.support as unknown[]).some(s => !object(s) || !oneOf(s.item, supportItems) || !(s.latest === null || object(s.latest) && oneOf(s.latest.state, supportStates) &&
    isGuid(s.latest.id) && int(s.latest.sequence, 1) && opt(s.latest.evidenceReference, 200) && text(s.latest.note, 500) && isGuid(s.latest.actorId) && time(s.latest.recordedAt)))) fail()
  if ((v.authorities as unknown[]).some(a => !object(a) || !isGuid(a.id) || a.duty !== 'ProductOwner')) fail()
  ;(v.decisions as unknown[]).forEach(decision)
  return v as unknown as PilotGateView
}

const key = () => crypto.randomUUID()
export const uatApi = {
  catalog: async (signal: AbortSignal) => decodeUatCatalog(await request<unknown>('uat/scenarios', signal)),
  summary: async (scope: UatScope, signal: AbortSignal) => decodeUatSummary(await request<unknown>(`uat/summary?scope=${scope}`, signal), scope),
  run: async (id: string, signal: AbortSignal) => decodeUatRunDetail(await request<unknown>(`uat/runs/${id}`, signal), id),
  threshold: (scope: UatScope, minScenarioPassPercent: number, maxOpenMajorFindings: number, requireDesignConformance: boolean, agreementReference: string, rationale: string, expectedSequence: number): Command =>
    ({ key: key(), method: 'POST', path: 'uat/thresholds', body: { scope, minScenarioPassPercent, maxOpenMajorFindings, requireDesignConformance, agreementReference, rationale, expectedSequence } }),
  createRun: (scope: UatScope, role: string, environment: string, buildCommit: string, participantPseudonym: string): Command =>
    ({ key: key(), method: 'POST', path: 'uat/runs', body: { scope, role, environment, buildCommit, participantPseudonym } }),
  result: (runId: string, scenarioId: string, outcome: string, durationSeconds: number | null, assisted: boolean, automatedEvidenceReference: string | null, note: string | null): Command =>
    ({ key: key(), method: 'POST', path: `uat/runs/${runId}/results`, body: { scenarioId, outcome, durationSeconds, assisted, automatedEvidenceReference, note } }),
  finding: (runId: string, scenarioId: string, severity: string, isUx: boolean, designConformance: boolean, title: string): Command =>
    ({ key: key(), method: 'POST', path: `uat/runs/${runId}/findings`, body: { scenarioId, severity, isUx, designConformance, title } }),
  submit: (r: UatRun): Command => ({ key: key(), method: 'POST', path: `uat/runs/${r.id}/submit`, body: { expectedVersion: r.version } }),
  attest: (r: UatRun, statement: string): Command => ({ key: key(), method: 'POST', path: `uat/runs/${r.id}/attest`, body: { statement, expectedVersion: r.version } }),
  resolve: (f: UatFinding, state: string, resolution: string, resolutionReference: string | null): Command =>
    ({ key: key(), method: 'POST', path: `uat/findings/${f.id}/resolve`, body: { state, resolution, resolutionReference, expectedVersion: f.version } }),
}
export const pilotGateApi = {
  read: async (signal: AbortSignal) => decodePilotGate(await request<unknown>('pilot-gate', signal)),
  support: (item: string, state: string, evidenceReference: string | null, note: string, expectedSequence: number): Command =>
    ({ key: key(), method: 'POST', path: 'pilot-gate/support-checks', body: { item, state, evidenceReference, note, expectedSequence } }),
  decide: (view: PilotGateView, decisionCode: PilotDecisionCode, rationale: string, conditions: string[], authorityId: string): Command =>
    ({ key: key(), method: 'POST', path: 'pilot-gate/decisions',
      body: { decision: decisionCode, rationale, conditions, expectedReviewHash: view.review.hash, expectedSequence: view.sequence, authorityId } }),
}
