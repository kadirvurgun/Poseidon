import { ApiError } from '../api'
import { dailySourceVersion } from '../daily-operations-api'
import { dailyFields, dailyKinds, type DailyKind, type DailyFields, type DailyValue } from '../daily-report-fields'
import { timestamp } from '../date-validation'
import { isGuid } from '../identifiers'
import { offlineKeys as exact, offlineInteger as integer } from '../offline-capture-contract'

export const badDailyOffline = (): never => { throw new ApiError(502, 'invalid_daily_offline_contract') }
export const dailyOfflineOperation = (kind: DailyKind) => `daily-operations.${kind.toLowerCase()}-draft.v1`
const hash = (v: unknown): v is string => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
export const utc = (v: unknown): v is string => typeof v === 'string' && timestamp(v) && /(?:Z|\+00:00)$/.test(v)
export type DailyOfflineContext = { id: string; tenantId: string; actorId: string; vesselId: string; kind: DailyKind; recordId: string;
  deviceId: string; keyId: string; challenge: string; policyVersion: string; policyDigest: string; authorityDigest: string; sourceVersion: string; schemaHash: string;
  operation: string; allowedFields: string[]; baseVersion: number; baseRevision: number; baseFields: DailyFields;
  serverTime: string; expiresAt: string; retainUntil: string; maxRecords: number; maxEncryptedBytes: number; maxDeviceRecords: number; maxDeviceEncryptedBytes: number }
export type DailyCapture = { commandId: string; recordId: string; kind: DailyKind; keyId: string; deviceId: string; policyVersion: string; policyDigest: string;
  sourceVersion: string; schemaHash: string; operation: string; baseVersion: number; capturedAt: string; offsetMinutes: number; fields: DailyFields;
  parentCommandId?: string; expectedServerVersion?: number; choice?: 'keepServer' | 'useCaptured'; reasonCode?: 'CURRENT_SERVER_RETAINED' | 'INPUT_CORRECTED' }
export type DailyOfflineOutcome = { commandId: string; result: 'Committed' | 'MaterialConflict' | 'ServerRetained'; recordId: string; kind: DailyKind; baseVersion: number;
  serverVersion: number; revision: number; parentCommandId: string | null; receivedAt: string }
export type DailyPending = { commandId: string; revision: number; body: string; outcome?: DailyOfflineOutcome }
export type DailyConflict = { commandId: string; enrollmentId: string; recordId: string; kind: DailyKind; currentVersion: number;
  fields: { key: string; base: DailyValue; captured: DailyValue; server: DailyValue }[]; choices: ('keepServer' | 'useCaptured')[]; resolved: DailyOfflineOutcome | null }
export type DailyInventoryItem = { id: string; expiresAt: string; retainUntil: string; revokedAt: string | null; policyVersion: string }
export function dailyValue(value: unknown): value is DailyValue {
  return exact(value, ['state', 'value']) && (value.state === 'Known' ? typeof value.value === 'string' && value.value.length > 0 && value.value.length <= 4000 && !/\p{Cc}/u.test(value.value.replace(/[\r\n\t]/g, '')) : ['Missing', 'Unknown'].includes(String(value.state)) && value.value === null)
}
export function allowedValues(value: unknown, keys: readonly string[]): value is DailyFields {
  return exact(value, keys) && keys.every(k => dailyValue(value[k]))
}
export function decodeDailyOfflineContext(value: unknown, kind: DailyKind, vessel: string, record: string): DailyOfflineContext {
  if (!exact(value, ['id', 'tenantId', 'actorId', 'vesselId', 'kind', 'recordId', 'deviceId', 'keyId', 'challenge', 'policyVersion', 'policyDigest', 'authorityDigest', 'sourceVersion', 'schemaHash', 'operation', 'allowedFields', 'baseVersion', 'baseRevision', 'baseFields', 'serverTime', 'expiresAt', 'retainUntil', 'maxRecords', 'maxEncryptedBytes', 'maxDeviceRecords', 'maxDeviceEncryptedBytes']) ||
    !dailyKinds.includes(kind) || value.kind !== kind || value.vesselId !== vessel || value.recordId !== record ||
    ['id', 'tenantId', 'actorId', 'deviceId', 'keyId'].some(k => !isGuid(value[k])) || !isGuid(vessel) || !isGuid(record) ||
    typeof value.challenge !== 'string' || !/^[A-Za-z0-9+/]{43}=$/.test(value.challenge) ||
    typeof value.policyVersion !== 'string' || value.policyVersion.length < 1 || value.policyVersion.length > 80 || /\p{Cc}/u.test(value.policyVersion) ||
    !hash(value.policyDigest) || !hash(value.authorityDigest) || !hash(value.schemaHash) || value.sourceVersion !== dailySourceVersion || value.operation !== dailyOfflineOperation(kind) ||
    !Array.isArray(value.allowedFields) || value.allowedFields.length < 1 || value.allowedFields.length > 18 ||
    !value.allowedFields.every(k => typeof k === 'string' && dailyFields(kind).some(f => f.key === k)) || new Set(value.allowedFields).size !== value.allowedFields.length ||
    !integer(value.baseVersion, 0, 4294967295) || !integer(value.baseRevision, 0, Number.MAX_SAFE_INTEGER) || (value.baseVersion === 0) !== (value.baseRevision === 0) ||
    !allowedValues(value.baseFields, value.allowedFields as string[]) || !utc(value.serverTime) || !utc(value.expiresAt) || !utc(value.retainUntil) ||
    Date.parse(value.expiresAt) <= Date.parse(value.serverTime) || Date.parse(value.retainUntil) < Date.parse(value.expiresAt) ||
    !integer(value.maxRecords, 2, 128) || !integer(value.maxEncryptedBytes, 131072, 1048576) || !integer(value.maxDeviceRecords, value.maxRecords, 512) || !integer(value.maxDeviceEncryptedBytes, value.maxEncryptedBytes, 4194304)) return badDailyOffline()
  return value as DailyOfflineContext
}
export function decodeDailyCapture(value: unknown, context: DailyOfflineContext, id: string): DailyCapture {
  if (!exact(value, ['commandId', 'recordId', 'kind', 'keyId', 'deviceId', 'policyVersion', 'policyDigest', 'sourceVersion', 'schemaHash', 'operation', 'baseVersion', 'capturedAt', 'offsetMinutes', 'fields'], ['parentCommandId', 'expectedServerVersion', 'choice', 'reasonCode']) ||
    value.commandId !== id || !isGuid(id) || value.recordId !== context.recordId || value.kind !== context.kind || value.keyId !== context.keyId || value.deviceId !== context.deviceId ||
    value.policyVersion !== context.policyVersion || value.policyDigest !== context.policyDigest || value.sourceVersion !== context.sourceVersion || value.schemaHash !== context.schemaHash || value.operation !== context.operation || !utc(value.capturedAt) || !integer(value.offsetMinutes, -840, 840) ||
    (value.parentCommandId === undefined ? value.baseVersion !== context.baseVersion || !allowedValues(value.fields, context.allowedFields) || ['expectedServerVersion', 'choice', 'reasonCode'].some(k => Object.hasOwn(value, k)) :
      !isGuid(value.parentCommandId) || value.parentCommandId === id || !integer(value.baseVersion, 0, 4294967295) || value.expectedServerVersion !== value.baseVersion || !exact(value.fields, []) || !['keepServer', 'useCaptured'].includes(String(value.choice)) || value.reasonCode !== (value.choice === 'keepServer' ? 'CURRENT_SERVER_RETAINED' : 'INPUT_CORRECTED'))) return badDailyOffline()
  return value as DailyCapture
}
export function decodeDailyOutcome(value: unknown, context: Pick<DailyOfflineContext, 'recordId' | 'kind'>, capture?: DailyCapture): DailyOfflineOutcome {
  if (!exact(value, ['commandId', 'result', 'recordId', 'kind', 'baseVersion', 'serverVersion', 'revision', 'parentCommandId', 'receivedAt']) ||
    !isGuid(value.commandId) || value.recordId !== context.recordId || value.kind !== context.kind || !['Committed', 'MaterialConflict', 'ServerRetained'].includes(String(value.result)) ||
    !integer(value.baseVersion, 0, 4294967295) || !integer(value.serverVersion, 0, 4294967295) || !integer(value.revision, 0, Number.MAX_SAFE_INTEGER) ||
    (value.serverVersion === 0) !== (value.revision === 0) || value.result === 'Committed' && (!value.serverVersion || !value.revision) || value.result === 'ServerRetained' && value.parentCommandId === null || value.parentCommandId !== null && !isGuid(value.parentCommandId) || !utc(value.receivedAt) ||
    capture && (value.commandId !== capture.commandId || value.baseVersion !== capture.baseVersion || value.parentCommandId !== (capture.parentCommandId ?? null))) return badDailyOffline()
  return value as DailyOfflineOutcome
}
export function decodeDailyConflict(value: unknown, context: DailyOfflineContext, id: string): DailyConflict {
  if (!exact(value, ['commandId', 'enrollmentId', 'recordId', 'kind', 'currentVersion', 'fields', 'choices', 'resolved']) || value.commandId !== id || value.enrollmentId !== context.id || value.recordId !== context.recordId || value.kind !== context.kind ||
    !integer(value.currentVersion, 0, 4294967295) || !Array.isArray(value.fields) || value.fields.length !== context.allowedFields.length ||
    value.fields.some(f => !exact(f, ['key', 'base', 'captured', 'server']) || typeof f.key !== 'string' || !context.allowedFields.includes(f.key) || ![f.base, f.captured, f.server].every(dailyValue)) || new Set(value.fields.map(f => (f as { key: string }).key)).size !== value.fields.length ||
    !Array.isArray(value.choices) || value.choices.length > 2 || new Set(value.choices).size !== value.choices.length || value.choices.some(c => !['keepServer', 'useCaptured'].includes(String(c))) || value.resolved !== null && value.choices.length !== 0) return badDailyOffline()
  if (value.resolved !== null) { const resolved = decodeDailyOutcome(value.resolved, context); if (resolved.parentCommandId !== id || !['Committed', 'ServerRetained'].includes(resolved.result)) return badDailyOffline() }
  return value as DailyConflict
}

// Retained parents are immutable history. Only an authenticated same-context
// terminal child can settle their UI status; unknown or malformed children
// conservatively remain pending. This never rewrites an outcome or body.
export function projectDailyQueue(rows: readonly DailyPending[], context: DailyOfflineContext | null) {
  const resolved = new Set<string>()
  if (!context) return { resolved, pending: rows.length }
  const duplicates = new Set(rows.filter((row, index) => rows.findIndex(other => other.commandId === row.commandId) !== index).map(row => row.commandId))
  const admitted = rows.map(row => {
    if (duplicates.has(row.commandId)) return null
    try {
      const body = decodeDailyCapture(JSON.parse(row.body), context, row.commandId)
      return { row, body, outcome: row.outcome ? decodeDailyOutcome(row.outcome, context, body) : null }
    } catch { return null }
  })
  const byId = new Map(admitted.flatMap(value => value ? [[value.row.commandId, value] as const] : []))
  for (const value of admitted) {
    if (!value?.outcome || !['Committed', 'ServerRetained'].includes(value.outcome.result) || !value.body.parentCommandId) continue
    const parent = byId.get(value.body.parentCommandId)
    if (parent?.outcome?.result === 'MaterialConflict' && value.outcome.parentCommandId === parent.row.commandId) resolved.add(parent.row.commandId)
  }
  return { resolved, pending: admitted.filter(value => !value?.outcome || value.outcome.result === 'MaterialConflict' && !resolved.has(value.row.commandId)).length }
}
