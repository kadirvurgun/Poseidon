import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import type { Command } from './tug-readiness-api'

// S9 WP-A DEV-058/059 client contract. Closed server codes only; an unknown state is a contract failure, never
// coerced to READY/PASS. The engine never releases: RELEASE/HOLD/DENY is the appointed Master's human record.
export const departureStates = ['READY', 'CONDITIONAL', 'NOT_READY', 'REVIEW_REQUIRED'] as const
export type DepartureState = typeof departureStates[number]
export const departureResults = ['PASS', 'FAIL', 'UNKNOWN', 'CONDITIONAL'] as const
export type DepartureResult = typeof departureResults[number]
export const departureCategories = ['crew', 'technical', 'statutory', 'emergency', 'safety'] as const
export const departureDecisions = ['RELEASE', 'HOLD', 'DENY'] as const
export type DepartureDecisionCode = typeof departureDecisions[number]
export const departureModes = ['CURRENT', 'FORECAST'] as const

export type DepartureWhy = { category: string; result: DepartureResult; reason: string; sourceKind: string | null; sourceId: string | null; sourceRevision: number | null }
export type DepartureDecision = { id: string; vesselId: string; sequence: number; previousDecisionId: string | null; decision: DepartureDecisionCode
  assessedState: DepartureState; assessmentMode: typeof departureModes[number]; assessmentHash: string; windowFrom: string; windowTo: string; reason: string
  duty: 'Master'; actorId: string; recordedAt: string; validity: 'CURRENT' | 'STALE'; replayed: boolean }
export type DepartureReadiness = { vesselId: string; from: string; to: string; mode: typeof departureModes[number]; state: DepartureState
  categories: { category: typeof departureCategories[number]; result: DepartureResult; reasons: string[] }[]; criticalBlockers: string[]; reviewReasons: string[]
  conditions: string[]; why: DepartureWhy[]; assessmentHash: string; assessedAt: string; validUntil: string; decisions: DepartureDecision[]; sequence: number
  authorities: { id: string; duty: 'Master' }[]; canDecide: boolean; allowedDecisions: DepartureDecisionCode[]; decisionAuthority: string; sourceLocator: string }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
function fail(): never { throw new ApiError(502, 'invalid_departure_readiness_contract') }
const text = (v: unknown, max = 4000) => typeof v === 'string' && v.length <= max
const int = (v: unknown, min = 0) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min
const list = (v: unknown, max = 512): v is unknown[] => Array.isArray(v) && v.length <= max
const oneOf = <T extends readonly string[]>(v: unknown, values: T): v is T[number] => typeof v === 'string' && values.includes(v)
const time = (v: unknown) => typeof v === 'string' && timestamp(v)
const date = (v: unknown): v is string => typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v)
const hash = (v: unknown) => typeof v === 'string' && /^[0-9A-F]{64}$/.test(v)
const strings = (v: unknown) => list(v, 2048) && v.every(x => text(x, 400))

function decision(v: unknown, vessel: string): DepartureDecision {
  if (!object(v) || !isGuid(v.id) || v.vesselId !== vessel || !int(v.sequence, 1) || !(v.previousDecisionId === null || isGuid(v.previousDecisionId)) ||
    !oneOf(v.decision, departureDecisions) || !oneOf(v.assessedState, departureStates) || !oneOf(v.assessmentMode, departureModes) || !hash(v.assessmentHash) ||
    !date(v.windowFrom) || !date(v.windowTo) || !text(v.reason, 2000) || v.duty !== 'Master' || !isGuid(v.actorId) || !time(v.recordedAt) ||
    !oneOf(v.validity, ['CURRENT', 'STALE'] as const) || typeof v.replayed !== 'boolean') fail()
  // A RELEASE can only exist against a CURRENT READY assessment; anything else is a contract breach.
  if (v.decision === 'RELEASE' && (v.assessedState !== 'READY' || v.assessmentMode !== 'CURRENT')) fail()
  if ((v.sequence === 1) !== (v.previousDecisionId === null)) fail()
  return v as unknown as DepartureDecision
}
export const decodeDepartureDecision = decision

export function decodeDepartureReadiness(v: unknown, vessel: string): DepartureReadiness {
  if (!object(v) || v.vesselId !== vessel || !date(v.from) || !date(v.to) || !oneOf(v.mode, departureModes) || !oneOf(v.state, departureStates) ||
    !list(v.categories, 8) || !strings(v.criticalBlockers) || !strings(v.reviewReasons) || !strings(v.conditions) || !list(v.why, 2048) || !hash(v.assessmentHash) ||
    !time(v.assessedAt) || !time(v.validUntil) || !list(v.decisions, 32) || !int(v.sequence, 0) || !list(v.authorities, 32) || typeof v.canDecide !== 'boolean' ||
    !list(v.allowedDecisions, 3) || !text(v.decisionAuthority, 200) || !text(v.sourceLocator, 300)) fail()
  if ((v.categories as unknown[]).some(c => !object(c) || !oneOf(c.category, departureCategories) || !oneOf(c.result, departureResults) || !strings(c.reasons))) fail()
  if (departureCategories.some(c => !(v.categories as { category: string }[]).some(x => x.category === c))) fail()
  if ((v.why as unknown[]).some(w => !object(w) || !oneOf(w.category, departureCategories) || !oneOf(w.result, departureResults) || w.result === 'PASS' || !text(w.reason, 160) ||
    !(w.sourceKind === null || text(w.sourceKind, 40)) || !(w.sourceId === null || isGuid(w.sourceId)) || !(w.sourceRevision === null || int(w.sourceRevision, 0)))) fail()
  if ((v.authorities as unknown[]).some(a => !object(a) || !isGuid(a.id) || a.duty !== 'Master')) fail()
  if ((v.allowedDecisions as unknown[]).some(d => !oneOf(d, departureDecisions))) fail()
  ;(v.decisions as unknown[]).forEach(d => decision(d, vessel))
  const results = (v.categories as { result: string }[]).map(c => c.result)
  // Defense in depth: never display READY with a non-PASS category, never anything but NOT_READY with a FAIL,
  // and never offer RELEASE outside a CURRENT READY assessment.
  if (v.state === 'READY' && results.some(r => r !== 'PASS')) fail()
  if (results.includes('FAIL') !== (v.state === 'NOT_READY')) fail()
  if ((v.allowedDecisions as string[]).includes('RELEASE') && (v.state !== 'READY' || v.mode !== 'CURRENT')) fail()
  return v as unknown as DepartureReadiness
}

export const departureApi = {
  readiness: async (vessel: string, from: string, to: string, signal: AbortSignal) =>
    decodeDepartureReadiness(await request<unknown>(`departure-readiness?vessel=${vessel}&from=${from}&to=${to}`, signal), vessel),
  signoff: (vessel: string, r: DepartureReadiness, decision: DepartureDecisionCode, authorityId: string, reason: string): Command =>
    ({ key: crypto.randomUUID(), method: 'POST', path: `departure-signoff?vessel=${vessel}`,
      body: { from: r.from, to: r.to, expectedAssessmentHash: r.assessmentHash, expectedSequence: r.sequence, decision, reason, authorityId } }),
}
