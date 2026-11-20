import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { calendar, timestamp } from './date-validation'
import type { CertificateFilePin } from './certificate-content'

// DEV-104 pre-survey checklist and DEV-112 survey planner client contract. Decoders are closed:
// an unknown state, a READY readiness or a FIT voyage fit is a contract breach, never rendered.
export const answers = ['OPEN', 'READY', 'ATTENTION', 'BLOCKED', 'NOT_APPLICABLE'] as const
export type Answer = typeof answers[number]
export const readinessStates = ['BLOCKED', 'INCOMPLETE', 'PREPARED_PENDING_HUMAN_CONFIRMATION'] as const
export type ReadinessState = typeof readinessStates[number]
export const fitStates = ['CONDITIONAL', 'NOT_FIT', 'UNKNOWN'] as const
export type FitState = typeof fitStates[number]
export const windowStates = ['UNKNOWN', 'UPCOMING', 'OPEN', 'OVERDUE'] as const
export type WindowState = typeof windowStates[number]
export const verificationStates = ['PENDING', 'VERIFIED', 'REJECTED'] as const
export type VerificationState = typeof verificationStates[number]
export const rejectReasons = ['EVIDENCE_INSUFFICIENT', 'ANSWER_NOT_SUPPORTED', 'NOT_APPLICABLE_NOT_JUSTIFIED', 'OTHER_SEE_COMMENT'] as const
export type RejectReason = typeof rejectReasons[number]
export type PreSurveyPlan = { plannedStart: string | null; plannedEnd: string | null; portCallId: string | null; portCallRevision: number | null; locationText: string | null }
export type PreSurveyItem = { itemId: string; itemCode: string; critical: boolean; required: boolean; evidenceRequired: boolean; allowNotApplicable: boolean; ownerRoleId: string | null; dueDate: string | null; answer: Answer; evidence: CertificateFilePin[]; finding: string | null; comment: string | null; itemHash: string; verificationState: VerificationState; verifiedAt: string | null; verificationReason: string | null; answeredByCurrentActor: boolean; verifiedByCurrentActor: boolean; blockers: string[] }
export type Readiness = { state: ReadinessState; qualification: 'ADVISORY_NOT_SURVEY_APPROVAL'; blockers: string[]; criticalUnresolved: number; requiredUnresolved: number; verified: number; total: number }
export type SurveyWindow = { state: WindowState; start: string | null; end: string | null; daysToOpen: number | null; daysToClose: number | null; daysOverdue: number | null; qualifiers: string[] }
export type SurveyFit = { state: FitState; reasons: string[]; basis: string[] }
export type PortCallReference = { id: string; revision: number; reference: 'CURRENT' | 'SUPERSEDED' | 'NOT_FOUND' | 'NOT_VISIBLE'; from: string | null; to: string | null; qualification: string }
export type PreSurveyChecklist = { id: string; vesselId: string; certificateId: string; certificateRevision: number; certificateVersion: number; revision: number; version: number; templateCode: string; templateRevision: string; templateStatus: 'STARTER_UNAPPROVED'; surveyType: string; plan: PreSurveyPlan; items: PreSurveyItem[]; readiness: Readiness; window: SurveyWindow; voyageFit: SurveyFit; portCall: PortCallReference | null; transition: 'CREATE' | 'UPDATE' | 'VERIFY'; sourceVersion: string; hash: string; savedAt: string; provenance: 'UserDeclaredUnverified' | 'SyntheticUnverified'; currentRevision: boolean; canEdit: boolean; canVerify: boolean; qualification: 'ADVISORY_NOT_SURVEY_APPROVAL'; voyageReference: 'UNKNOWN_NO_VOYAGE_MODULE' }
export type PreSurveyPage = { items: PreSurveyChecklist[]; offset: number; limit: 25; hasMore: boolean }
export type PreSurveyTemplate = { code: string; revision: string; surveyType: string; status: 'STARTER_UNAPPROVED'; items: { itemCode: string; critical: boolean; required: boolean; evidenceRequired: boolean; allowNotApplicable: boolean }[] }
export type PreSurveyCapabilities = { vesselId: string; canWrite: boolean; canVerify: boolean; ownerOptions: { roleId: string }[]; templates: PreSurveyTemplate[]; canReadPortCalls: boolean; sourceVersion: string; answers: Answer[]; rejectReasons: RejectReason[]; qualification: 'ADVISORY_NOT_SURVEY_APPROVAL' }
export type PortCallOption = { id: string; revision: number; from: string | null; to: string | null; label: string; qualification: string }
export type PlannerChecklist = { id: string; revision: number; readiness: ReadinessState; criticalUnresolved: number; requiredUnresolved: number; plannedStart: string | null; plannedEnd: string | null; portCallReference: string; href: string }
export type PlannerRow = { vesselId: string; vesselCode: string; vesselName: string; vesselKind: 'Ship' | 'Tug'; certificateId: string; certificateRevision: number; certificateVersion: number; certificateType: string | null; certificateNumber: string | null; issuer: string | null; declaredFlag: string | null; regulatoryBasis: string | null; expiryDate: string | null; window: SurveyWindow; fit: SurveyFit; checklist: PlannerChecklist | null; coordination: 'EXTERNAL_NOT_SIMULATED'; href: string }
export type Planner = { from: string; to: string; referenceDate: string; items: PlannerRow[]; truncated: boolean; vesselsTruncated: boolean; counts: Record<'windowUnknown' | 'windowUpcoming' | 'windowOpen' | 'windowOverdue' | 'fitConditional' | 'fitNotFit' | 'fitUnknown' | 'checklistBlocked' | 'checklistMissing', number>; qualification: 'ADVISORY_PLANNING_NOT_AUTHORITY_SCHEDULE'; fitStates: FitState[]; sourceVersion: string }
export type SurveyCommand = { key: string; id: string; vessel: string; body: string }
export type ItemInput = { itemId: string; ownerRoleId: string | null; dueDate: string | null; answer: Answer; evidence: CertificateFilePin[]; finding: string | null; comment: string | null }

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: readonly string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const int = (v: unknown, min = 0, max = Number.MAX_SAFE_INTEGER): v is number => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const text = (v: unknown, max = 4096) => v === null || typeof v === 'string' && v.length <= max
const date = (v: unknown) => v === null || calendar(v)
const one = <T extends string>(v: unknown, set: readonly T[]): v is T => typeof v === 'string' && (set as readonly string[]).includes(v)
const codes = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 64 && v.every(c => typeof c === 'string' && /^[A-Z0-9_]{1,80}$/.test(c))
const hash = (v: unknown) => typeof v === 'string' && /^[A-Fa-f0-9]{64}$/.test(v)
const pin = (v: unknown) => exact(v, ['id', 'revision', 'checksum']) && isGuid(v.id) && int(v.revision, 1) && hash(v.checksum)
function fail(): never { throw new ApiError(502, 'invalid_survey_contract') }

export function decodeWindow(v: unknown): SurveyWindow {
  if (!exact(v, ['state', 'start', 'end', 'daysToOpen', 'daysToClose', 'daysOverdue', 'qualifiers']) || !one(v.state, windowStates) || !date(v.start) || !date(v.end) || !codes(v.qualifiers) ||
    ![v.daysToOpen, v.daysToClose, v.daysOverdue].every(n => n === null || int(n, -3652059, 3652059))) fail()
  // UNKNOWN is the only state allowed without both declared dates.
  if (v.state !== 'UNKNOWN' && (v.start === null || v.end === null)) fail()
  return v as SurveyWindow
}
export function decodeFit(v: unknown): SurveyFit {
  // Never FIT: the server has no input set that establishes it; FIT is a contract breach.
  if (!exact(v, ['state', 'reasons', 'basis']) || !one(v.state, fitStates) || !codes(v.reasons) || v.reasons.length === 0 || !codes(v.basis)) fail()
  return v as SurveyFit
}
function decodeReadiness(v: unknown): Readiness {
  if (!exact(v, ['state', 'qualification', 'blockers', 'criticalUnresolved', 'requiredUnresolved', 'verified', 'total']) || !one(v.state, readinessStates) || v.qualification !== 'ADVISORY_NOT_SURVEY_APPROVAL' ||
    !codes(v.blockers) || !int(v.criticalUnresolved) || !int(v.requiredUnresolved) || !int(v.verified) || !int(v.total, 1, 500) || v.verified > v.total) fail()
  if (v.state === 'BLOCKED' ? v.criticalUnresolved === 0 : v.criticalUnresolved !== 0 || (v.state === 'INCOMPLETE') !== (v.requiredUnresolved > 0)) fail()
  return v as Readiness
}
function decodeItem(v: unknown): PreSurveyItem {
  if (!exact(v, ['itemId', 'itemCode', 'critical', 'required', 'evidenceRequired', 'allowNotApplicable', 'ownerRoleId', 'dueDate', 'answer', 'evidence', 'finding', 'comment', 'itemHash', 'verificationState', 'verifiedAt', 'verificationReason', 'answeredByCurrentActor', 'verifiedByCurrentActor', 'blockers']) ||
    !isGuid(v.itemId) || typeof v.itemCode !== 'string' || !/^[A-Z0-9_]{1,80}$/.test(v.itemCode) || !['critical', 'required', 'evidenceRequired', 'allowNotApplicable', 'answeredByCurrentActor', 'verifiedByCurrentActor'].every(k => typeof v[k] === 'boolean') ||
    !(v.ownerRoleId === null || isGuid(v.ownerRoleId)) || !date(v.dueDate) || !one(v.answer, answers) || !Array.isArray(v.evidence) || v.evidence.length > 10 || !v.evidence.every(pin) || !text(v.finding) || !text(v.comment) ||
    !hash(v.itemHash) || !one(v.verificationState, verificationStates) || !(v.verifiedAt === null || timestamp(v.verifiedAt)) || !(v.verificationReason === null || one(v.verificationReason, rejectReasons)) || !codes(v.blockers)) fail()
  if (v.answer === 'NOT_APPLICABLE' && !v.allowNotApplicable) fail()
  if (v.verificationState === 'VERIFIED' && (v.answer === 'OPEN' || v.answer === 'ATTENTION' || v.answer === 'BLOCKED')) fail()
  return v as PreSurveyItem
}
function decodePlan(v: unknown): PreSurveyPlan {
  if (!exact(v, ['plannedStart', 'plannedEnd', 'portCallId', 'portCallRevision', 'locationText']) || !date(v.plannedStart) || !date(v.plannedEnd) || !(v.portCallId === null || isGuid(v.portCallId)) ||
    !(v.portCallRevision === null || int(v.portCallRevision, 1)) || (v.portCallId === null) !== (v.portCallRevision === null) || !text(v.locationText, 200)) fail()
  return v as PreSurveyPlan
}
export function decodeChecklist(v: unknown, vessel: string, id?: string): PreSurveyChecklist {
  if (!exact(v, ['id', 'vesselId', 'certificateId', 'certificateRevision', 'certificateVersion', 'revision', 'version', 'templateCode', 'templateRevision', 'templateStatus', 'surveyType', 'plan', 'items', 'readiness', 'window', 'voyageFit', 'portCall', 'transition', 'sourceVersion', 'hash', 'savedAt', 'provenance', 'currentRevision', 'canEdit', 'canVerify', 'qualification', 'voyageReference']) ||
    !isGuid(v.id) || id !== undefined && v.id !== id || v.vesselId !== vessel || !isGuid(v.certificateId) || !int(v.certificateRevision, 1) || !int(v.certificateVersion, 1, 4294967295) || !int(v.revision, 1) || !int(v.version, 1, 4294967295) ||
    typeof v.templateCode !== 'string' || typeof v.templateRevision !== 'string' || v.templateStatus !== 'STARTER_UNAPPROVED' || typeof v.surveyType !== 'string' || !Array.isArray(v.items) || v.items.length === 0 || v.items.length > 500 ||
    !one(v.transition, ['CREATE', 'UPDATE', 'VERIFY'] as const) || typeof v.sourceVersion !== 'string' || !hash(v.hash) || !timestamp(v.savedAt) || !one(v.provenance, ['UserDeclaredUnverified', 'SyntheticUnverified'] as const) ||
    !['currentRevision', 'canEdit', 'canVerify'].every(k => typeof v[k] === 'boolean') || v.qualification !== 'ADVISORY_NOT_SURVEY_APPROVAL' || v.voyageReference !== 'UNKNOWN_NO_VOYAGE_MODULE') fail()
  const plan = decodePlan(v.plan), items = v.items.map(decodeItem), readiness = decodeReadiness(v.readiness), window = decodeWindow(v.window), voyageFit = decodeFit(v.voyageFit)
  if (new Set(items.map(i => i.itemId)).size !== items.length || readiness.total !== items.length) fail()
  let portCall: PortCallReference | null = null
  if (v.portCall !== null) {
    const p = v.portCall
    if (!exact(p, ['id', 'revision', 'reference', 'from', 'to', 'qualification']) || p.id !== plan.portCallId || p.revision !== plan.portCallRevision || !one(p.reference, ['CURRENT', 'SUPERSEDED', 'NOT_FOUND', 'NOT_VISIBLE'] as const) || !date(p.from) || !date(p.to) || typeof p.qualification !== 'string') fail()
    portCall = p as PortCallReference
  } else if (plan.portCallId !== null) fail()
  return { ...(v as PreSurveyChecklist), plan, items, readiness, window, voyageFit, portCall }
}
function decodePage(v: unknown, vessel: string, offset: number, id?: string): PreSurveyPage {
  if (!exact(v, ['items', 'offset', 'limit', 'hasMore']) || !Array.isArray(v.items) || v.items.length > 25 || v.offset !== offset || v.limit !== 25 || typeof v.hasMore !== 'boolean') fail()
  const items = v.items.map(row => decodeChecklist(row, vessel, id))
  if (new Set(items.map(i => id ? String(i.revision) : i.id)).size !== items.length) fail()
  return { items, offset, limit: 25, hasMore: v.hasMore }
}
export function decodeCapabilities(v: unknown, vessel: string): PreSurveyCapabilities {
  if (!exact(v, ['vesselId', 'canWrite', 'canVerify', 'ownerOptions', 'templates', 'canReadPortCalls', 'sourceVersion', 'answers', 'rejectReasons', 'qualification']) || v.vesselId !== vessel ||
    typeof v.canWrite !== 'boolean' || typeof v.canVerify !== 'boolean' || typeof v.canReadPortCalls !== 'boolean' || typeof v.sourceVersion !== 'string' || v.qualification !== 'ADVISORY_NOT_SURVEY_APPROVAL' ||
    !Array.isArray(v.ownerOptions) || v.ownerOptions.length > 256 || !v.ownerOptions.every(o => exact(o, ['roleId']) && isGuid(o.roleId)) ||
    !Array.isArray(v.answers) || v.answers.join('|') !== answers.join('|') || !Array.isArray(v.rejectReasons) || v.rejectReasons.join('|') !== rejectReasons.join('|') ||
    !Array.isArray(v.templates) || v.templates.length === 0 || v.templates.length > 32 || !v.templates.every(t => exact(t, ['code', 'revision', 'surveyType', 'status', 'items']) && typeof t.code === 'string' && typeof t.revision === 'string' &&
      typeof t.surveyType === 'string' && t.status === 'STARTER_UNAPPROVED' && Array.isArray(t.items) && t.items.length > 0 && t.items.every(i => exact(i, ['itemCode', 'critical', 'required', 'evidenceRequired', 'allowNotApplicable']) && typeof i.itemCode === 'string')))
    fail()
  return v as PreSurveyCapabilities
}
function decodePortCalls(v: unknown): { items: PortCallOption[]; truncated: boolean } {
  if (!exact(v, ['items', 'truncated']) || typeof v.truncated !== 'boolean' || !Array.isArray(v.items) || v.items.length > 50 ||
    !v.items.every(o => exact(o, ['id', 'revision', 'from', 'to', 'label', 'qualification']) && isGuid(o.id) && int(o.revision, 1) && date(o.from) && date(o.to) && typeof o.label === 'string' && o.label.length <= 400 && typeof o.qualification === 'string')) fail()
  return v as { items: PortCallOption[]; truncated: boolean }
}
export function decodePlanner(v: unknown): Planner {
  if (!exact(v, ['from', 'to', 'referenceDate', 'items', 'truncated', 'vesselsTruncated', 'counts', 'qualification', 'fitStates', 'sourceVersion']) || !calendar(v.from) || !calendar(v.to) || !calendar(v.referenceDate) ||
    !Array.isArray(v.items) || v.items.length > 200 || typeof v.truncated !== 'boolean' || typeof v.vesselsTruncated !== 'boolean' || v.qualification !== 'ADVISORY_PLANNING_NOT_AUTHORITY_SCHEDULE' ||
    !Array.isArray(v.fitStates) || v.fitStates.join('|') !== fitStates.join('|') || typeof v.sourceVersion !== 'string' ||
    !exact(v.counts, ['windowUnknown', 'windowUpcoming', 'windowOpen', 'windowOverdue', 'fitConditional', 'fitNotFit', 'fitUnknown', 'checklistBlocked', 'checklistMissing']) || !Object.values(v.counts).every(n => int(n, 0, 200))) fail()
  const items = v.items.map(row => {
    if (!exact(row, ['vesselId', 'vesselCode', 'vesselName', 'vesselKind', 'certificateId', 'certificateRevision', 'certificateVersion', 'certificateType', 'certificateNumber', 'issuer', 'declaredFlag', 'regulatoryBasis', 'expiryDate', 'window', 'fit', 'checklist', 'coordination', 'href']) ||
      !isGuid(row.vesselId) || typeof row.vesselCode !== 'string' || typeof row.vesselName !== 'string' || !one(row.vesselKind, ['Ship', 'Tug'] as const) || !isGuid(row.certificateId) || !int(row.certificateRevision, 1) || !int(row.certificateVersion, 1, 4294967295) ||
      !['certificateType', 'certificateNumber', 'issuer', 'declaredFlag', 'regulatoryBasis'].every(k => text(row[k])) || !date(row.expiryDate) || row.coordination !== 'EXTERNAL_NOT_SIMULATED' ||
      row.href !== `/app/certificate-command-center/${row.vesselId}?id=${row.certificateId}`) fail()
    const c = row.checklist
    if (c !== null && (!exact(c, ['id', 'revision', 'readiness', 'criticalUnresolved', 'requiredUnresolved', 'plannedStart', 'plannedEnd', 'portCallReference', 'href']) || !isGuid(c.id) || !int(c.revision, 1) || !one(c.readiness, readinessStates) ||
      !int(c.criticalUnresolved) || !int(c.requiredUnresolved) || !date(c.plannedStart) || !date(c.plannedEnd) || typeof c.portCallReference !== 'string' || c.href !== `/app/pre-survey-checklist/${row.vesselId}?id=${c.id}`)) fail()
    return { ...row, window: decodeWindow(row.window), fit: decodeFit(row.fit) } as PlannerRow
  })
  return { ...(v as Planner), items }
}

// 60.04 p226: a batch verification failure lists every failing item, one never hides another.
export class VerificationRejected extends ApiError {
  constructor(public items: { itemId: string; code: string }[], correlationId?: string) { super(422, 'verification_rejected', {}, correlationId) }
}
async function verifyRequest(path: string, signal: AbortSignal, init: RequestInit): Promise<unknown> {
  const response = await fetch(`/api/v1/${path}`, { credentials: 'same-origin', signal, ...init, headers: { Accept: 'application/json', ...init.headers } })
  if (response.ok) return response.json()
  let body: { code?: string; items?: unknown; errors?: Record<string, string[]>; correlationId?: string } = {}
  try { body = await response.json() } catch { /* Proxy errors need not be JSON. */ }
  if (response.status === 422 && body.code === 'verification_rejected' && Array.isArray(body.items) && body.items.length <= 100 &&
    body.items.every(i => exact(i, ['itemId', 'code']) && typeof i.itemId === 'string' && typeof i.code === 'string' && /^[a-z_]{1,64}$/.test(i.code)))
    throw new VerificationRejected(body.items as { itemId: string; code: string }[], body.correlationId)
  throw new ApiError(response.status, body.code, body.errors, body.correlationId)
}
const vesselQuery = (vessel: string) => { if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context'); return `vesselId=${vessel}` }
const write = (command: SurveyCommand, csrf: string, method: 'POST' | 'PUT'): RequestInit => ({ method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: command.body })
export const surveyApi = {
  capabilities: async (vessel: string, signal: AbortSignal) => decodeCapabilities(await request<unknown>(`pre-survey-checklists/capabilities?${vesselQuery(vessel)}`, signal), vessel),
  list: async (vessel: string, offset: number, signal: AbortSignal) => decodePage(await request<unknown>(`pre-survey-checklists?${vesselQuery(vessel)}&offset=${offset}`, signal), vessel, offset),
  read: async (vessel: string, id: string, signal: AbortSignal) => { if (!isGuid(id)) throw new ApiError(400, 'invalid_context'); return decodeChecklist(await request<unknown>(`pre-survey-checklists/${id}?${vesselQuery(vessel)}`, signal), vessel, id) },
  history: async (vessel: string, id: string, offset: number, signal: AbortSignal) => { if (!isGuid(id)) throw new ApiError(400, 'invalid_context'); return decodePage(await request<unknown>(`pre-survey-checklists/${id}/history?${vesselQuery(vessel)}&offset=${offset}`, signal), vessel, offset, id) },
  portCalls: async (vessel: string, signal: AbortSignal) => decodePortCalls(await request<unknown>(`pre-survey-checklists/port-calls?${vesselQuery(vessel)}`, signal)),
  create: async (command: SurveyCommand, csrf: string, signal: AbortSignal) => decodeChecklist(await request<unknown>(`pre-survey-checklists?${vesselQuery(command.vessel)}`, signal, write(command, csrf, 'POST')), command.vessel, command.id),
  update: async (command: SurveyCommand, csrf: string, signal: AbortSignal) => { if (!isGuid(command.id)) throw new ApiError(400, 'invalid_context'); return decodeChecklist(await request<unknown>(`pre-survey-checklists/${command.id}?${vesselQuery(command.vessel)}`, signal, write(command, csrf, 'PUT')), command.vessel, command.id) },
  verify: async (command: SurveyCommand, csrf: string, signal: AbortSignal) => { if (!isGuid(command.id)) throw new ApiError(400, 'invalid_context'); return decodeChecklist(await verifyRequest(`pre-survey-checklists/${command.id}/verifications?${vesselQuery(command.vessel)}`, signal, write(command, csrf, 'POST')), command.vessel, command.id) },
  planner: async (range: { from: string; to: string; vessel?: string | null }, signal: AbortSignal) => {
    if (!calendar(range.from) || !calendar(range.to) || range.vessel && !isGuid(range.vessel)) throw new ApiError(400, 'invalid_context')
    const query = new URLSearchParams({ from: range.from, to: range.to }); if (range.vessel) query.set('vesselId', range.vessel)
    return decodePlanner(await request<unknown>(`survey-planner?${query}`, signal))
  },
}
export const toInput = (item: PreSurveyItem): ItemInput => ({ itemId: item.itemId, ownerRoleId: item.ownerRoleId, dueDate: item.dueDate, answer: item.answer, evidence: item.evidence, finding: item.finding, comment: item.comment })
