import { ApiError } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// Closed plaintext contract for the ONE supported offline adapter. Encryption
// authenticates bytes; it does not make arbitrary encrypted fields eligible.
export const offlineOperation = 'crew.draft.watch-group.v1'
export const offlineSource = '3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018'
export const offlineObject = (value: unknown): value is Record<string, unknown> =>
  !!value && typeof value === 'object' && !Array.isArray(value)
export const offlineInteger = (value: unknown, min: number, max: number): value is number =>
  Number.isSafeInteger(value) && Number(value) >= min && Number(value) <= max
export const offlineTime = (value: unknown): value is string => timestamp(value) && (value.endsWith('Z') || value.endsWith('+00:00'))
export const invalidOffline = (): never => { throw new ApiError(502, 'invalid_offline_contract') }
export function offlineKeys(value: unknown, required: readonly string[], optional: readonly string[] = []): value is Record<string, unknown> {
  return offlineObject(value) && required.every(key => Object.hasOwn(value, key)) &&
    Object.keys(value).every(key => required.includes(key) || optional.includes(key))
}
type CaptureScope = {
  keyId: string; deviceId: string; policyVersion: string; baseVersion: number; watchGroups: readonly string[]
}
export type OfflineCaptureBody = {
  commandId: string; keyId: string; deviceId: string; policyVersion: string; sourceVersion: string; operation: string
  baseVersion: number; capturedAt: string; offsetMinutes: number; watchGroup: string | null
  parentCommandId?: string; expectedServerVersion?: number; choice?: 'keepServer' | 'useCaptured'; reason?: string
}
const commandKeys = ['commandId', 'keyId', 'deviceId', 'policyVersion', 'sourceVersion', 'operation',
  'baseVersion', 'capturedAt', 'offsetMinutes', 'watchGroup']
const resolutionKeys = ['parentCommandId', 'expectedServerVersion', 'choice', 'reason']
export function decodeOfflineCapture(value: unknown, commandId: string, scope: CaptureScope): OfflineCaptureBody {
  if (!offlineKeys(value, commandKeys, resolutionKeys) || !isGuid(commandId) || value.commandId !== commandId ||
    value.keyId !== scope.keyId || value.deviceId !== scope.deviceId || value.policyVersion !== scope.policyVersion ||
    value.sourceVersion !== offlineSource || value.operation !== offlineOperation ||
    !offlineInteger(value.baseVersion, 1, 4294967295) || !offlineTime(value.capturedAt) ||
    !offlineInteger(value.offsetMinutes, -840, 840) ||
    value.watchGroup !== null && !scope.watchGroups.includes(value.watchGroup as string)) return invalidOffline()
  const resolution = resolutionKeys.some(key => Object.hasOwn(value, key))
  if (resolution ? !resolutionKeys.every(key => Object.hasOwn(value, key)) || !isGuid(value.parentCommandId) || value.parentCommandId === commandId ||
    value.expectedServerVersion !== value.baseVersion || typeof value.choice !== 'string' || !['keepServer', 'useCaptured'].includes(value.choice) ||
    typeof value.reason !== 'string' || value.reason.trim().length < 1 || value.reason.length > 500 || /\p{Cc}/u.test(value.reason)
    : value.baseVersion !== scope.baseVersion) return invalidOffline()
  return value as OfflineCaptureBody
}
export function decodeOfflineSnapshot(value: unknown, scope: { baseVersion: number; baseRevision: number; watchGroups: readonly string[] }) {
  if (!offlineKeys(value, ['kind', 'watchGroup', 'baseVersion', 'revision']) || value.kind !== 'snapshot' ||
    value.baseVersion !== scope.baseVersion || value.revision !== scope.baseRevision ||
    value.watchGroup !== null && !scope.watchGroups.includes(value.watchGroup as string)) return invalidOffline()
  return { watchGroup: value.watchGroup as string | null, baseVersion: scope.baseVersion, revision: scope.baseRevision }
}
