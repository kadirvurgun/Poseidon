import { ApiError, request, type Session } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

export type ObservationScope = { owner: string; vessel: string; asset: string }
export type ObservationSource = { source: string; version: string; kind: 'Meter' | 'Condition'; unit: string; freshnessSeconds: number; maxScale: number }
export type ObservationRow = { id: string; source: string; policyVersion: string; sourceRevision: number; kind: 'Meter' | 'Condition'; unit: string;
  value: string | null; observedAt: string | null; provenance: string; revoked: boolean }
export type ObservationSnapshot = { sources: ObservationSource[]; items: ObservationRow[]; canWrite: boolean; state: 'PolicyUnavailable' | 'ConfiguredDeclarationsOnly' }
export type ObservationAttempt = ObservationScope & { key: string; id: string; kind: 'capture' | 'revoke'; path: string; body: string }

export const observationOwner = (user: Session | null) => user?.authenticated === true && isGuid(user.tenantId) && isGuid(user.actorId)
  ? `${user.tenantId.toLowerCase()}/${user.actorId.toLowerCase()}` : null
export function validObservationScope(scope: ObservationScope) {
  const parts = scope.owner.split('/')
  return parts.length === 2 && parts.every(isGuid) && isGuid(scope.vessel) && isGuid(scope.asset)
}
export const observationPath = (scope: ObservationScope) => `equipment-hierarchy/${encodeURIComponent(scope.vessel)}/${encodeURIComponent(scope.asset)}/observations`
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const text = (value: unknown): value is string => typeof value === 'string' && value.length > 0 && value.length <= 2000 && !/[\p{Cc}]/u.test(value)
const sameId = (value: unknown, expected: string) => isGuid(value) && value.toLowerCase() === expected.toLowerCase()
const positive = (value: unknown) => typeof value === 'number' && Number.isSafeInteger(value) && value > 0

export function decodeObservationSnapshot(value: unknown, scope: ObservationScope): ObservationSnapshot {
  const invalid = (): never => { throw new ApiError(502, 'invalid_observation_snapshot') }
  if (!validObservationScope(scope) || !object(value) || !Array.isArray(value.sources) || !Array.isArray(value.items) || typeof value.canWrite !== 'boolean' ||
      !['PolicyUnavailable', 'ConfiguredDeclarationsOnly'].includes(value.state as string)) return invalid()
  const sourceIds = new Set<string>(), ids = new Set<string>()
  for (const rule of value.sources) {
    if (!object(rule) || !sameId(rule.tenantId, scope.owner.split('/')[0]) || !sameId(rule.vesselId, scope.vessel) || !sameId(rule.assetId, scope.asset) ||
        !text(rule.source) || sourceIds.has(rule.source) || !text(rule.version) || !['Meter', 'Condition'].includes(rule.kind as string) || !text(rule.unit) ||
        !positive(rule.freshnessSeconds) || typeof rule.maxScale !== 'number' || !Number.isSafeInteger(rule.maxScale) || rule.maxScale < 0 || rule.maxScale > 28) return invalid()
    sourceIds.add(rule.source)
  }
  for (const row of value.items) {
    if (!object(row) || !isGuid(row.id) || ids.has(row.id.toLowerCase()) || !text(row.source) || !text(row.policyVersion) || !positive(row.sourceRevision) ||
        !['Meter', 'Condition'].includes(row.kind as string) || !text(row.unit) || !(row.value === null || typeof row.value === 'string') ||
        !(row.observedAt === null || timestamp(row.observedAt)) || !text(row.provenance) || typeof row.revoked !== 'boolean') return invalid()
    ids.add(row.id.toLowerCase())
  }
  if ((value.sources.length === 0) !== (value.state === 'PolicyUnavailable')) return invalid()
  return value as ObservationSnapshot
}

export const readObservations = async (scope: ObservationScope, signal: AbortSignal) =>
  decodeObservationSnapshot(await request<unknown>(observationPath(scope), signal), scope)

// These endpoints use immutable observation UUID/body equality and separate
// revocation rows. They do not expose an actor-scoped generic command receipt.
export async function sendObservation(attempt: ObservationAttempt, csrf: string, signal: AbortSignal) {
  const value = await request<unknown>(attempt.path, signal, { method: 'POST', headers: {
    'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': attempt.key,
  }, body: attempt.body })
  if (!object(value) || !sameId(value.id, attempt.id) ||
      (attempt.kind === 'capture' ? typeof value.replayed !== 'boolean' : value.state !== 'Revoked'))
    throw new ApiError(502, 'invalid_observation_outcome')
  return { id: value.id as string }
}
