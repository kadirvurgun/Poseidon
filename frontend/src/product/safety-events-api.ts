import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import type { Command } from './tug-readiness-api'

// S9 WP-B DEV-060/061 client contract (/api/v1/safety-events). Closed server codes only: an unknown state is a
// contract failure, never coerced to CLOSED/EFFECTIVE. Triage, investigation review, verification, effectiveness
// and closure are human records decided by the server; the browser only displays them.
export const eventStates = ['DRAFT', 'SUBMITTED', 'RETURNED', 'TRIAGED', 'INVESTIGATING', 'ACTION_TRACKING', 'CLOSED', 'WITHDRAWN'] as const
export const categories = ['NEAR_MISS', 'INCIDENT', 'ACCIDENT', 'AUDIT_FINDING', 'DEFECT', 'FATIGUE', 'ENVIRONMENTAL', 'CYBER', 'OBSERVATION'] as const
export const reportingModes = ['NAMED', 'CONFIDENTIAL'] as const
export const precisions = ['EXACT', 'DATE_ONLY', 'UNKNOWN'] as const
export const contexts = ['AT_SEA', 'IN_PORT', 'AT_ANCHOR', 'MOORING', 'TOWAGE', 'CARGO', 'BUNKERING', 'MAINTENANCE', 'DRYDOCK', 'OTHER', 'UNKNOWN'] as const
export const yesNoUnknown = ['YES', 'NO', 'UNKNOWN'] as const
export const severities = ['UNASSESSED', 'LOW', 'MEDIUM', 'HIGH', 'CRITICAL'] as const
export const identityPurposes = ['INVESTIGATION_FOLLOW_UP', 'WELFARE_SUPPORT', 'LEGAL_OBLIGATION'] as const
export const factorKinds = ['FACT', 'ALLEGATION', 'INFERENCE', 'UNRESOLVED'] as const
export const factorCategories = ['PEOPLE', 'EQUIPMENT', 'PROCEDURE', 'ENVIRONMENT', 'ORGANISATION', 'EXTERNAL', 'UNKNOWN'] as const
export const verifications = ['UNVERIFIED', 'SUPPORTED', 'CONTRADICTED', 'INCONCLUSIVE'] as const
export const barrierFunctions = ['PREVENTIVE', 'MITIGATING', 'UNKNOWN'] as const
export const barrierPerformances = ['FAILED', 'MISSING', 'DEGRADED', 'WORKED', 'UNKNOWN'] as const
export const effectivenessStates = ['PENDING', 'EFFECTIVE', 'NOT_EFFECTIVE', 'INCONCLUSIVE', 'NO_ACTIONS', 'WITHHELD'] as const
export const blockerStates = ['UNAVAILABLE', 'BLOCKERS_PRESENT', 'REVIEW_REQUIRED', 'NO_OPEN_HIGH_SEVERITY_EVENT'] as const

export type FilePin = { id: string; revision: number; checksum: string }
// Shared draft DTO (also produced as candidate values by the DEV-062 voice-draft package; a human confirms them).
export type DraftFields = { category: string | null; occurrencePrecision: string | null; occurredAt: string | null; occurrenceDate: string | null
  occurrenceUnknownReason: string | null; timeZone: string | null; operationContext: string | null; locationText: string | null; summary: string | null
  narrative: string | null; immediateActionTaken: string | null; immediateActionText: string | null; severityClaim: string | null; evidence: FilePin[] | null }
export type Factor = { id: string; kind: string; category: string; statement: string }
export type RootCause = { id: string; statement: string; factorIds: string[]; verification: string }
export type Barrier = { id: string; name: string; function: string; performance: string; riskBarrierRef: string | null }
export type InvestigationContent = { scope: string | null; factors: Factor[]; rootCauses: RootCause[]; barriers: Barrier[]; uncertainties: string[]; evidence: FilePin[] }
export type Investigation = { revision: number; version: number; state: string; content: InvestigationContent; hash: string; approvedRevision: number | null
  missing: string[]; authors: string[]; decisions: { id: string; revision: number; decision: string; reason: string; actorId: string; decidedAt: string }[]
  canEdit: boolean; canSubmit: boolean; canReview: boolean }
export type ActionStep = { id: string; revision: number; kind: string; outcome: string | null; text: string; evidence: FilePin[]; actorId: string; recordedAt: string }
export type SafetyAction = { id: string; kind: string; title: string; description: string; ownerId: string; dueDate: string; overdue: boolean; rootCauseIds: string[]
  barrierIds: string[]; followUpOf: string | null; revision: number; version: number; state: string; effectiveness: string; steps: ActionStep[]; allowedSteps: string[] }
export type Message = { id: string; visibility: string; authorKind: string; actorId: string | null; body: string; createdAt: string }
export type SafetyEvent = { id: string; vesselId: string; eventNo: string; revision: number; version: number; state: typeof eventStates[number]; reportingMode: string
  fields: DraftFields; hash: string; missing: string[]; reporter: { mode: string; actorId: string | null; isCurrentActor: boolean }; severity: string | null
  severityClaim: string | null; investigationRequired: string | null; severityPolicy: string; origin: string; createdAt: string; savedAt: string; submittedAt: string | null
  closedBy: string | null; closedAt: string | null; closureReason: string | null
  triages: { id: string; revision: number; decision: string; severity: string | null; investigationRequired: string | null; reason: string; actorId: string; decidedAt: string }[]
  investigation: Investigation | null; actions: SafetyAction[]; effectiveness: typeof effectivenessStates[number]; closureBlockers: string[]; messages: Message[]
  can: { edit: boolean; submit: boolean; withdraw: boolean; triage: boolean; readIdentity: boolean; message: boolean; investigate: boolean; reviewInvestigation: boolean
    createAction: boolean; close: boolean }
  followUpSecret: string | null; provenance: string; sourceVersion: string }
export type ListItem = { id: string; eventNo: string; state: string; reportingMode: string; category: string | null; summary: string | null; occurrencePrecision: string | null
  occurredAt: string | null; occurrenceDate: string | null; severity: string | null; mine: boolean; origin: string; savedAt: string }
export type Blockers = { vesselId: string; state: typeof blockerStates[number]; items: { eventId: string; eventNo: string; category: string | null; severity: string | null; state: string
  reason: string; blocking: boolean }[]; coverage: string[]; evaluatedAt: string }
export type Capabilities = { vesselId: string; canRead: boolean; canReport: boolean; canTriage: boolean; canInvestigate: boolean; canReview: boolean; canWriteActions: boolean
  canVerifyActions: boolean; canReviewEffectiveness: boolean; canReadIdentity: boolean; anonymousChannel: string; offline: { available: boolean; state: string } }
export type Page = { items: ListItem[]; offset: number; limit: number; hasMore: boolean; blockers: Blockers; capabilities: Capabilities }
export type Identity = { accessId: string; eventId: string; reporterActorId: string; contactPreference: string; purpose: string; accessedAt: string }
export type FollowUp = { eventNo: string; state: string; submittedAt: string | null; messages: Message[] }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
function fail(): never { throw new ApiError(502, 'invalid_safety_event_contract') }
const oneOf = (v: unknown, values: readonly string[]) => typeof v === 'string' && values.includes(v)
const int = (v: unknown, min = 0) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min

export const emptyDraft = (): DraftFields => ({ category: null, occurrencePrecision: null, occurredAt: null, occurrenceDate: null, occurrenceUnknownReason: null, timeZone: null,
  operationContext: null, locationText: null, summary: null, narrative: null, immediateActionTaken: null, immediateActionText: null, severityClaim: null, evidence: null })
export const emptyInvestigation = (): InvestigationContent => ({ scope: null, factors: [], rootCauses: [], barriers: [], uncertainties: [], evidence: [] })

export function decodeEvent(v: unknown, vessel: string): SafetyEvent {
  if (!object(v) || !isGuid(v.id) || v.vesselId !== vessel || !oneOf(v.state, eventStates) || !oneOf(v.reportingMode, reportingModes) || !int(v.revision, 1) || !int(v.version) ||
    !object(v.fields) || !Array.isArray(v.missing) || !object(v.reporter) || !Array.isArray(v.actions) || !Array.isArray(v.messages) || !Array.isArray(v.closureBlockers) ||
    !oneOf(v.effectiveness, effectivenessStates) || !object(v.can) || typeof v.hash !== 'string') fail()
  const reporter = v.reporter as Record<string, unknown>
  // A confidential reporter is never named in a general read.
  if (v.reportingMode === 'CONFIDENTIAL' && reporter.actorId !== null) fail()
  for (const a of v.actions as unknown[]) if (!object(a) || !isGuid(a.id) || !oneOf(a.state, ['OPEN', 'COMPLETED', 'VERIFIED']) || !oneOf(a.effectiveness, effectivenessStates)) fail()
  return v as unknown as SafetyEvent
}
export function decodePage(v: unknown, vessel: string): Page {
  if (!object(v) || !Array.isArray(v.items) || !int(v.offset) || !int(v.limit, 1) || typeof v.hasMore !== 'boolean' || !object(v.blockers) || !object(v.capabilities) ||
    !oneOf(v.blockers.state, blockerStates) || v.capabilities.vesselId !== vessel) fail()
  for (const i of v.items as unknown[]) if (!object(i) || !isGuid(i.id) || !oneOf(i.state, eventStates) || typeof i.mine !== 'boolean') fail()
  return v as unknown as Page
}

const path = (vessel: string, suffix = '') => { if (!isGuid(vessel)) fail(); return `safety-events${suffix}?vesselId=${vessel}` }
const cmd = (method: 'POST' | 'PUT', p: string, body: Record<string, unknown>): Command => ({ key: crypto.randomUUID(), method, path: p, body })

export const safetyApi = {
  list: async (vessel: string, q: { mine?: boolean; state?: string; offset?: number }, signal: AbortSignal) => {
    const extra = `${q.mine ? '&mine=true' : ''}${q.state ? `&state=${q.state}` : ''}${q.offset ? `&offset=${q.offset}` : ''}`
    return decodePage(await request<unknown>(path(vessel) + extra, signal), vessel)
  },
  detail: async (vessel: string, id: string, signal: AbortSignal) => { if (!isGuid(id)) fail(); return decodeEvent(await request<unknown>(path(vessel, `/${id}`), signal), vessel) },
  // Pseudonymous follow-up: the token travels in a header, never in the URL or browser history.
  followUp: async (token: string, signal: AbortSignal) => {
    const v = await request<unknown>('safety-events/follow-up', signal, { headers: { 'X-Safety-Follow-Up': token } })
    if (!object(v) || typeof v.eventNo !== 'string' || !Array.isArray(v.messages)) fail()
    return v as unknown as FollowUp
  },
  followUpMessage: async (token: string, body: string, csrf: string, signal: AbortSignal) => {
    const id = crypto.randomUUID()
    const v = await request<unknown>('safety-events/follow-up/messages', signal, { method: 'POST', body: JSON.stringify({ id, body }),
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'X-Safety-Follow-Up': token, 'Idempotency-Key': id } })
    if (!object(v) || !Array.isArray(v.messages)) fail()
    return v as unknown as FollowUp
  },
  create: (vessel: string, id: string, reportingMode: string, fields: DraftFields, contactPreference: string | null) =>
    cmd('POST', path(vessel), { id, reportingMode, fields, ...(reportingMode === 'CONFIDENTIAL' ? { contactPreference } : {}) }),
  update: (vessel: string, e: SafetyEvent, fields: DraftFields) => cmd('PUT', path(vessel, `/${e.id}`), { expectedVersion: e.version, fields }),
  submit: (vessel: string, e: SafetyEvent) => cmd('POST', path(vessel, `/${e.id}/submit`), { expectedVersion: e.version, contentHash: e.hash }),
  withdraw: (vessel: string, e: SafetyEvent, reason: string) => cmd('POST', path(vessel, `/${e.id}/withdraw`), { expectedVersion: e.version, reason }),
  triage: (vessel: string, e: SafetyEvent, decision: 'TRIAGE' | 'RETURN', severity: string | null, investigationRequired: string | null, reason: string) =>
    cmd('POST', path(vessel, `/${e.id}/triage`), { expectedVersion: e.version, decision, severity, investigationRequired, reason }),
  identity: (vessel: string, e: SafetyEvent, purpose: string, reason: string) => cmd('POST', path(vessel, `/${e.id}/identity-access`), { id: crypto.randomUUID(), purpose, reason }),
  message: (vessel: string, e: SafetyEvent, visibility: string, body: string) => cmd('POST', path(vessel, `/${e.id}/messages`), { id: crypto.randomUUID(), visibility, body }),
  saveInvestigation: (vessel: string, e: SafetyEvent, content: InvestigationContent) =>
    cmd('PUT', path(vessel, `/${e.id}/investigation`), { expectedVersion: e.investigation?.version ?? 0, content }),
  submitInvestigation: (vessel: string, e: SafetyEvent) =>
    cmd('POST', path(vessel, `/${e.id}/investigation/submit`), { expectedVersion: e.investigation!.version, contentHash: e.investigation!.hash }),
  reviewInvestigation: (vessel: string, e: SafetyEvent, decision: string, reason: string) =>
    cmd('POST', path(vessel, `/${e.id}/investigation/review`), { expectedVersion: e.investigation!.version, contentHash: e.investigation!.hash, decision, reason }),
  createAction: (vessel: string, e: SafetyEvent, a: { kind: string; title: string; description: string; ownerId: string; dueDate: string; rootCauseIds: string[]; barrierIds: string[]; followUpOf: string | null }) =>
    cmd('POST', path(vessel, `/${e.id}/actions`), { id: crypto.randomUUID(), ...a }),
  step: (vessel: string, e: SafetyEvent, a: SafetyAction, kind: string, outcome: string | null, text: string) =>
    cmd('POST', path(vessel, `/${e.id}/actions/${a.id}/steps`), { expectedRevision: a.revision, kind, outcome, text }),
  close: (vessel: string, e: SafetyEvent, reason: string) => cmd('POST', path(vessel, `/${e.id}/close`), { expectedVersion: e.version, reason }),
}

// Mirrors SafetyEventPolicy.Missing so the form can show what blocks submission; the server stays the authority.
export function missingFields(f: DraftFields): string[] {
  const m: string[] = [], text = (v: string | null, min: number) => !!v && v.trim().length >= min
  if (!f.category) m.push('category')
  if (!f.occurrencePrecision) m.push('occurrence_precision')
  else if (f.occurrencePrecision === 'EXACT' && !f.occurredAt) m.push('occurred_at')
  else if (f.occurrencePrecision === 'DATE_ONLY' && !f.occurrenceDate) m.push('occurrence_date')
  else if (f.occurrencePrecision === 'UNKNOWN' && !text(f.occurrenceUnknownReason, 10)) m.push('occurrence_unknown_reason')
  if (!f.operationContext) m.push('operation_context')
  if (!text(f.summary, 10)) m.push('summary')
  if (!text(f.narrative, 10)) m.push('narrative')
  if (!f.immediateActionTaken) m.push('immediate_action_taken')
  else if (f.immediateActionTaken === 'YES' && !text(f.immediateActionText, 1)) m.push('immediate_action_text')
  return m
}
