import { ApiError, request } from './api'
import type { components } from '../generated/api'
import { isGuid } from './identifiers'

// DEV-055 defect lifecycle, DEV-103 service & engineer reports, DEV-056 PMS command center.
// Closed wire decoding: a server shape that claims a closure, verification or a clean command
// center outside the closed vocabularies is rejected instead of being rendered as a green state.
type S = components['schemas']
export type DefectLifecycle = S['DefectLifecycleDto']
export type DefectLifecycleCommand = S['DefectLifecycleCommand']
export type ServiceReport = S['ServiceReportDto']
export type ServiceReportContent = S['ServiceReportContent']
export type ServiceReportLinks = S['ServiceReportLinks']
export type ServiceReportPage = S['ServiceReportPageDto']
export type ServiceReportCapabilities = S['ServiceReportCapabilitiesDto']
export type PmsCommandCenter = S['PmsCommandCenterDto']
export type PmsSection = S['PmsSection']
export type PmsItem = S['PmsCommandItem']

export const lifecycleStages = ['DETECT', 'ASSESS', 'SAFEGUARD', 'ACTION', 'VERIFY', 'EFFECTIVENESS', 'CLOSED'] as const
export const lifecycleKinds = ['START', 'ASSESS', 'SAFEGUARD', 'LIFT', 'ACTION', 'VERIFY', 'EFFECTIVENESS', 'CLOSE'] as const
export const criticalities = ['Critical', 'High', 'Medium', 'Low'] as const
export const measureStates = ['ACTIVE', 'EXPIRED', 'LIFTED', 'ENDED_AT_CLOSURE'] as const
export const serviceStates = ['DRAFT', 'SUBMITTED', 'VERIFIED', 'RETURNED'] as const
export const verificationStates = ['NOT_SUBMITTED', 'PENDING_VERIFICATION', 'VERIFIED', 'RETURNED', 'EARLIER_REVISION_VERIFIED'] as const
export const measurementResults = ['WITHIN_LIMITS', 'OUT_OF_LIMITS', 'INFORMATION'] as const
export const sectionKeys = ['critical_overdue_jobs', 'overdue_jobs', 'open_work_orders', 'condition_alerts', 'critical_spare_gaps'] as const
export const sectionStates = ['EXCEPTIONS', 'QUIET', 'UNKNOWN', 'ACCESS_REQUIRED'] as const
export const readinessStates = ['BLOCKED', 'REVIEW_REQUIRED', 'ATTENTION', 'NO_OPEN_EXCEPTIONS'] as const
export const freshnessStates = ['CURRENT', 'STALE', 'UNKNOWN'] as const
export const severities = ['CRITICAL', 'ATTENTION', 'REVIEW'] as const

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const oneOf = (v: unknown, values: readonly string[]) => typeof v === 'string' && values.includes(v)
const count = (v: unknown) => typeof v === 'number' && Number.isSafeInteger(v) && v >= 0
const hash = (v: unknown) => typeof v === 'string' && /^[A-Fa-f0-9]{64}$/.test(v)
function invalid(): never { throw new ApiError(502, 'invalid_technical_contract') }

export function decodeLifecycle(value: unknown, vessel: string, defect: string): DefectLifecycle {
  if (!object(value) || value.defectId !== defect || value.vesselId !== vessel || !oneOf(value.stage, lifecycleStages) || !count(value.revision) ||
    typeof value.started !== 'boolean' || !Array.isArray(value.records) || !Array.isArray(value.measures) || !Array.isArray(value.alerts) ||
    !Array.isArray(value.closeBlockers) || !object(value.can) ||
    !value.records.every(r => object(r) && oneOf(r.kind, lifecycleKinds) && oneOf(r.stageAfter, lifecycleStages)) ||
    !value.measures.every(m => object(m) && oneOf(m.state, measureStates)) ||
    // CLOSED always names the human who returned the equipment to service; never without the record.
    value.stage === 'CLOSED' && (!isGuid(value.closedBy) || !value.records.some(r => object(r) && r.kind === 'CLOSE')) ||
    value.stage !== 'CLOSED' && value.closedBy !== null ||
    !value.started && (value.stage !== 'DETECT' || value.records.length > 0)) invalid()
  return value as unknown as DefectLifecycle
}
export function decodeServiceReport(value: unknown, vessel: string, id?: string): ServiceReport {
  if (!object(value) || !isGuid(value.id) || id !== undefined && value.id !== id || value.vesselId !== vessel || !oneOf(value.state, serviceStates) ||
    !oneOf(value.verificationState, verificationStates) || !hash(value.hash) || !object(value.content) || !Array.isArray(value.missing) ||
    !Array.isArray(value.verifications) || !Array.isArray(value.evidence) ||
    value.state === 'VERIFIED' && (value.verifiedRevision !== value.revision || value.verificationState !== 'VERIFIED') ||
    value.state !== 'VERIFIED' && value.verificationState === 'VERIFIED' ||
    // Accepted file transport is never verified evidence.
    !value.evidence.every(e => object(e) && e.state === 'TRANSPORT_ACCEPTED_UNVERIFIED') ||
    value.canEdit === true && !['DRAFT', 'RETURNED'].includes(value.state as string) || value.canVerify === true && value.state !== 'SUBMITTED') invalid()
  return value as unknown as ServiceReport
}
export function decodeCommandCenter(value: unknown, vessel: string): PmsCommandCenter {
  if (!object(value) || value.vesselId !== vessel || !oneOf(value.readinessState, readinessStates) || !Array.isArray(value.sections) ||
    value.sections.length !== sectionKeys.length || !value.sections.every((s, index) => object(s) && s.key === sectionKeys[index] && oneOf(s.state, sectionStates) &&
      oneOf(s.freshness, freshnessStates) && Array.isArray(s.items) && count(s.quietCount) &&
      // An unknown or forbidden source has no count and no items: never zero/healthy.
      (s.state === 'UNKNOWN' || s.state === 'ACCESS_REQUIRED' ? s.count === null && s.items.length === 0 : s.count === s.items.length) &&
      (s.state === 'QUIET') === (s.items.length === 0 && s.state !== 'UNKNOWN' && s.state !== 'ACCESS_REQUIRED') &&
      s.items.every(i => object(i) && oneOf(i.severity, severities) && oneOf(i.freshness, freshnessStates) && Array.isArray(i.why) && i.why.length > 0)) ||
    value.readinessState === 'NO_OPEN_EXCEPTIONS' && value.sections.some(s => object(s) && s.state !== 'QUIET')) invalid()
  return value as unknown as PmsCommandCenter
}

// A frozen command keeps its Idempotency-Key and exact body until its outcome is known.
export type TechnicalAttempt = { method: 'POST' | 'PUT'; path: string; key: string; body: string }
export function freezeTechnicalCommand(method: 'POST' | 'PUT', path: string, body: unknown): TechnicalAttempt {
  if (!/^(defects\/[0-9a-f-]{36}\/lifecycle\/(start|assess|safeguards|safeguards\/[0-9a-f-]{36}\/lift|actions|verify|effectiveness|close)|service-reports(\/[0-9a-f-]{36}(\/(submit|verify|amend))?)?)\?vesselId=[0-9a-f-]{36}$/.test(path))
    throw new ApiError(400, 'invalid_context')
  return { method, path, key: crypto.randomUUID(), body: JSON.stringify(body) }
}
export function sendTechnicalCommand<T>(attempt: TechnicalAttempt, csrf: string, signal: AbortSignal): Promise<T> {
  return request<T>(attempt.path, signal, { method: attempt.method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body })
}
const scoped = (vessel: string, path: string, extra = '') => {
  if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context')
  return `${path}?vesselId=${vessel}${extra}`
}
export const lifecyclePath = (vessel: string, defect: string, suffix = '') => {
  if (!isGuid(defect)) throw new ApiError(400, 'invalid_context')
  return scoped(vessel, `defects/${defect}/lifecycle${suffix}`)
}
export const servicePath = (vessel: string, suffix = '') => scoped(vessel, `service-reports${suffix}`)
export const technicalApi = {
  lifecycle: async (vessel: string, defect: string, signal: AbortSignal) => decodeLifecycle(await request<unknown>(lifecyclePath(vessel, defect), signal), vessel, defect),
  serviceCapabilities: (vessel: string, signal: AbortSignal) => request<ServiceReportCapabilities>(servicePath(vessel, '/capabilities'), signal),
  serviceList: (vessel: string, offset: number, signal: AbortSignal) => request<ServiceReportPage>(scoped(vessel, 'service-reports', '&offset=' + Math.max(0, Math.trunc(offset))), signal),
  serviceRead: async (vessel: string, id: string, signal: AbortSignal) => {
    if (!isGuid(id)) throw new ApiError(400, 'invalid_context')
    return decodeServiceReport(await request<unknown>(servicePath(vessel, '/' + id), signal), vessel, id)
  },
  commandCenter: async (vessel: string, signal: AbortSignal) => decodeCommandCenter(await request<unknown>(scoped(vessel, 'pms/command-center'), signal), vessel),
}
export const emptyServiceContent = (): ServiceReportContent => ({ serviceProvider: null, engineerName: null, serviceDate: null, reasonScope: null, workPerformed: null,
  parts: [], measurements: [], defectsFound: null, recommendations: null, nextActionDue: null })
export const textOrNull = (value: string) => value.trim() === '' ? null : value
export type TechnicalIssue = 'offline' | 'conflict' | 'incomplete' | 'forbidden' | 'stale' | 'error' | null
export const technicalFailure = (error: unknown): Exclude<TechnicalIssue, null> =>
  typeof navigator !== 'undefined' && navigator.onLine === false ? 'offline' : error instanceof ApiError ?
    error.status === 409 ? (error.code === 'version_conflict' || error.code === 'idempotency_key_reused' ? 'conflict' : 'stale') :
    error.status === 422 ? 'incomplete' : error.status === 403 ? 'forbidden' : 'error' : error instanceof TypeError ? 'offline' : 'error'
