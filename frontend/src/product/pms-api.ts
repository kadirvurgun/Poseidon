import { ApiError, request } from './api'
import type { components } from '../generated/api'
import { isGuid } from './identifiers'

// DEV-108 / DEV-109 (18.01 rows 109-110; 59.04 Rev2 §9, §12). Closed wire decoding: a band,
// state or readiness impact outside the closed vocabularies is rejected rather than rendered,
// so an unknown value can never appear as a green/approved state.
type S = components['schemas']
export type MeasurementPoint = S['MeasurementPointDto']
export type MeasurementReading = S['MeasurementReadingDto']
export type MeasurementPage = S['MeasurementPage']
export type MeasurementReadingPage = S['MeasurementReadingPage']
export type MeasurementPointCommand = S['MeasurementPointSaveCommand']
export type MeasurementReadingCommand = S['MeasurementReadingCommand']
export type Postponement = S['MaintenancePostponementDto']
export type PostponementPage = S['MaintenancePostponementPage']
export type PostponementRequest = S['MaintenancePostponementRequestCommand']
export type PostponementDecision = S['MaintenancePostponementDecisionCommand']

export const measurementBands = ['NORMAL', 'CAUTION', 'WARNING', 'ALARM', 'UNKNOWN', 'UNCONFIGURED', 'COUNTER'] as const
export const postponementStates = ['REQUESTED', 'APPROVED', 'REJECTED', 'EXPIRED'] as const
export const readinessImpacts = ['REVIEW_REQUIRED', 'CONDITIONAL_DEFERRED', 'PENDING_DECISION', 'UNCHANGED'] as const
export const criticalities = ['CRITICAL', 'IMPORTANT', 'STANDARD', 'UNKNOWN'] as const

const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const invalid = (): never => { throw new ApiError(502, 'invalid_pms_contract') }
const oneOf = (value: unknown, values: readonly string[]) => typeof value === 'string' && values.includes(value)

export function decodeMeasurementPage(value: unknown): MeasurementPage {
  if (!object(value) || !Array.isArray(value.items) || typeof value.canWrite !== 'boolean' || !Array.isArray(value.kinds) || !object(value.units)) return invalid()
  for (const p of value.items) if (!object(p) || !isGuid(p.id) || !isGuid(p.assetId) || typeof p.code !== 'string' || !oneOf(p.band, measurementBands) ||
    !oneOf(p.policyState, ['CONFIGURED', 'UNCONFIGURED']) || typeof p.version !== 'number') return invalid()
  return value as unknown as MeasurementPage
}
export function decodePostponementPage(value: unknown): PostponementPage {
  if (!object(value) || !isGuid(value.planId) || !Array.isArray(value.items) || typeof value.canRequest !== 'boolean' || typeof value.canDecide !== 'boolean' ||
    !oneOf(value.policyState, ['CONFIGURED', 'UNCONFIGURED']) || !oneOf(value.criticality, criticalities)) return invalid()
  for (const p of value.items) if (!object(p) || !isGuid(p.id) || !oneOf(p.state, postponementStates) || !oneOf(p.effectiveState, postponementStates) ||
    !oneOf(p.readinessImpact, readinessImpacts) || !Array.isArray(p.decisionBlockers)) return invalid()
  return value as unknown as PostponementPage
}

export const pointsPath = (vessel: string) => `equipment-hierarchy/${encodeURIComponent(vessel)}/measurement-points`
export const postponementPath = (vessel: string, plan: string) => `maintenance-plan/${encodeURIComponent(vessel)}/${encodeURIComponent(plan)}`
export const readPoints = async (vessel: string, signal: AbortSignal) => decodeMeasurementPage(await request<unknown>(pointsPath(vessel), signal))
export const readReadings = (vessel: string, point: string, signal: AbortSignal) =>
  request<MeasurementReadingPage>(`${pointsPath(vessel)}/${encodeURIComponent(point)}/readings`, signal)
export const readPostponements = async (vessel: string, plan: string, signal: AbortSignal) =>
  decodePostponementPage(await request<unknown>(`${postponementPath(vessel, plan)}/postponements`, signal))

// One command attempt keeps its key and body until the outcome is known (retry reuses both).
export type PmsAttempt = { path: string; key: string; body: string }
export const sendPms = <T>(attempt: PmsAttempt, csrf: string, signal: AbortSignal) => request<T>(attempt.path, signal, {
  method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key }, body: attempt.body })
