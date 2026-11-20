import { ApiError, request } from './api'
import type { components } from '../generated/api'
import { isGuid } from './identifiers'

// DEV-105 / DEV-106 (18.01 rows 106-107; 50.01 Rev3 §3-6). Closed wire decoding: a server
// shape that claims an approval, rating or publication outside the closed vocabularies is
// rejected instead of being rendered as an approved/green state.
type S = components['schemas']
export type RiskAssessment = S['RiskAssessmentDto']
export type RiskAssessmentContent = S['RiskAssessmentContent']
export type RiskHazard = S['RiskHazard']
export type RiskControl = S['RiskControl']
export type RiskRating = S['RiskRating']
export type RiskResult = S['RiskResult']
export type RiskCapabilities = S['RiskCapabilitiesDto']
export type RiskAssessmentPage = S['RiskAssessmentPageDto']
export type RiskTemplateDetail = S['RiskTemplateDetailDto']
export type RiskTemplateRevision = S['RiskTemplateRevisionDto']
export type RiskTemplateContent = S['RiskTemplateContent']
export type RiskTemplatePage = S['RiskTemplatePageDto']
export type RiskApplicableTemplates = S['RiskApplicableTemplatesDto']
export type RiskMethod = S['RiskMethodDto']
export type RiskMethodList = S['RiskMethodListDto']
export type RiskMethodContent = S['RiskMethodContent']
export type RiskLinkRequest = S['RiskLinkRequest']

export const riskAssessmentStates = ['DRAFT', 'IN_REVIEW', 'RETURNED', 'APPROVED'] as const
export const riskTemplateStates = ['DRAFT', 'IN_REVIEW', 'RETURNED', 'PUBLISHED', 'SUPERSEDED'] as const
export const riskResultStates = ['ASSESSED', 'NOT_ASSESSED', 'METHOD_UNCONFIGURED'] as const
export const riskAckStates = ['NOT_APPLICABLE', 'PENDING', 'COMPLETE', 'DISPUTED'] as const
export const riskEffectiveStates = ['NOT_EFFECTIVE', 'SCHEDULED', 'CURRENT', 'SUPERSEDED'] as const
export const riskWorkTypes = ['MAINTENANCE', 'HOT_WORK', 'ENCLOSED_SPACE', 'BUNKERING', 'CARGO', 'NAVIGATION', 'TOWAGE', 'OTHER'] as const
export const riskLinkKinds = ['TugJob', 'WorkOrder', 'Defect', 'OperationalEvent'] as const

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const hash = (v: unknown) => typeof v === 'string' && /^[A-Fa-f0-9]{64}$/.test(v)
const oneOf = (v: unknown, values: readonly string[]) => typeof v === 'string' && values.includes(v)
const positive = (v: unknown) => typeof v === 'number' && Number.isSafeInteger(v) && v >= 1
function invalid(): never { throw new ApiError(502, 'invalid_risk_contract') }

function result(v: unknown): v is RiskResult {
  if (!object(v) || !oneOf(v.state, riskResultStates)) return false
  // Only an exact published-method lookup carries a result; otherwise it must stay empty.
  return v.state === 'ASSESSED' ? typeof v.result === 'string' && typeof v.rank === 'number' : v.result === null && v.rank === null
}
export function decodeRiskAssessment(value: unknown, vessel: string, id?: string): RiskAssessment {
  if (!isGuid(vessel) || !object(value) || !isGuid(value.id) || id !== undefined && value.id !== id || value.vesselId !== vessel ||
    !oneOf(value.state, riskAssessmentStates) || !positive(value.revision) || !positive(value.version) || !hash(value.hash) ||
    !object(value.content) || !Array.isArray(value.results) || !value.results.every(r => object(r) && result(r.initial) && result(r.residual)) ||
    !oneOf(value.acknowledgementState, riskAckStates) || !Array.isArray(value.decisions) || !Array.isArray(value.acknowledgements) ||
    !Array.isArray(value.blockers) || !Array.isArray(value.links) || !Array.isArray(value.revisions) ||
    value.state === 'APPROVED' && value.approvedRevision !== value.revision ||
    value.state !== 'APPROVED' && value.acknowledgementState !== 'NOT_APPLICABLE' ||
    value.canEdit === true && !['DRAFT', 'RETURNED'].includes(value.state as string) ||
    value.canReview === true && value.state !== 'IN_REVIEW' || value.canAcknowledge === true && value.state !== 'APPROVED') invalid()
  return value as unknown as RiskAssessment
}
export function decodeRiskTemplate(value: unknown, id?: string): RiskTemplateDetail {
  if (!object(value) || !isGuid(value.id) || id !== undefined && value.id !== id || !positive(value.latestRevision) || !Array.isArray(value.revisions) ||
    !value.revisions.every(r => object(r) && oneOf(r.status, riskTemplateStates) && oneOf(r.effectiveState, riskEffectiveStates) && hash(r.hash) &&
      // A published or superseded revision always names its human publisher.
      (!['PUBLISHED', 'SUPERSEDED'].includes(r.status as string) || isGuid(r.publishedBy)) && (r.canEdit !== true || ['DRAFT', 'RETURNED'].includes(r.status as string))) ||
    !Array.isArray(value.suggestions) || typeof value.suggestionCount !== 'number') invalid()
  return value as unknown as RiskTemplateDetail
}

// A frozen command keeps its Idempotency-Key and exact body until its outcome is known, so a
// lost reply is retried as the same command instead of a second business effect.
export type RiskAttempt = { method: 'POST' | 'PUT'; path: string; key: string; body: string; scope: string }
export function freezeRiskCommand(method: 'POST' | 'PUT', path: string, body: unknown, scope: string): RiskAttempt {
  if (!/^hse\/[a-z-]+(\/[0-9a-f-]{36}(\/[a-z-]+(\/[0-9a-f-]{36}\/review)?)?)?(\?vesselId=[0-9a-f-]{36})?$/.test(path)) throw new ApiError(400, 'invalid_context')
  return { method, path, key: crypto.randomUUID(), body: JSON.stringify(body), scope }
}
export async function sendRiskCommand<T>(attempt: RiskAttempt, csrf: string, signal: AbortSignal): Promise<T> {
  return request<T>(attempt.path, signal, { method: attempt.method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body })
}
export const assessmentPath = (vessel: string, suffix = '') => {
  if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context')
  return `hse/risk-assessments${suffix}?vesselId=${vessel}`
}
export const riskApi = {
  capabilities: (vessel: string, signal: AbortSignal) => request<RiskCapabilities>(assessmentPath(vessel, '/capabilities'), signal),
  list: (vessel: string, offset: number, signal: AbortSignal) => request<RiskAssessmentPage>(assessmentPath(vessel) + '&offset=' + Math.max(0, Math.trunc(offset)), signal),
  read: async (vessel: string, id: string, signal: AbortSignal) => decodeRiskAssessment(await request<unknown>(assessmentPath(vessel, '/' + id), signal), vessel, id),
  applicable: (vessel: string, workType: string | null, signal: AbortSignal) => request<RiskApplicableTemplates>('hse/risk-templates/applicable?vesselId=' + vessel +
    (workType && riskWorkTypes.includes(workType as typeof riskWorkTypes[number]) ? '&workType=' + workType : ''), signal),
  templates: (offset: number, signal: AbortSignal) => request<RiskTemplatePage>('hse/risk-templates?offset=' + Math.max(0, Math.trunc(offset)), signal),
  template: async (id: string, signal: AbortSignal) => { if (!isGuid(id)) throw new ApiError(400, 'invalid_context'); return decodeRiskTemplate(await request<unknown>('hse/risk-templates/' + id, signal), id) },
  methods: (signal: AbortSignal) => request<RiskMethodList>('hse/risk-methods', signal),
}

export const emptyAssessment = (): RiskAssessmentContent => ({ title: null, operationContext: null, workType: null, workScope: null, methodId: null,
  hazards: [], stopWorkRequired: null, stopWorkConditions: [], participants: [] })
export const emptyHazard = (): RiskHazard => ({ id: crypto.randomUUID(), step: null, description: '', consequences: [], initial: { likelihood: null, severity: null, reason: null },
  controls: [], barrierRefs: [], residual: { likelihood: null, severity: null, reason: null } })
export const emptyControl = (): RiskControl => ({ id: crypto.randomUUID(), description: '', kind: 'PROPOSED', owner: null, required: false, barrierRefs: [] })
export const emptyTemplate = (): RiskTemplateContent => ({ title: null, workType: null, vesselKinds: [], requiredBarriers: [], hazards: [], sourceRefs: [], approvalOwner: null, changeReason: null })
// Text helpers: blank optional text is null (UNKNOWN/missing), never an empty string.
export const textOrNull = (value: string) => value.trim() === '' ? null : value
export const splitList = (value: string) => value.split(/\r?\n/).map(x => x.trim()).filter(Boolean)
export const failureKind = (error: unknown): 'offline' | 'conflict' | 'incomplete' | 'forbidden' | 'stale' | 'error' =>
  typeof navigator !== 'undefined' && navigator.onLine === false ? 'offline' : error instanceof ApiError ?
    error.status === 409 ? (error.code === 'version_conflict' || error.code === 'idempotency_key_reused' ? 'conflict' : 'stale') :
    error.status === 422 ? 'incomplete' : error.status === 403 ? 'forbidden' : 'error' : error instanceof TypeError ? 'offline' : 'error'
