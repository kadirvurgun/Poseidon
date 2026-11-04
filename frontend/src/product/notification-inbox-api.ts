import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { calendar, timestamp } from './date-validation'

export type Notice = { id: string; vesselId: string; sourceId: string; sourceRevision: number; ruleKey: string; policyVersion: string; sourceState: 'Due' | 'ReviewRequired'; severity: 'Info' | 'Warning' | 'Critical'; mandatory: boolean; recipientRoleId: string; escalationRoleId: string | null; dueDate: string | null; createdAt: string; escalateAt: string | null; state: 'Open' | 'Acknowledged' | 'Escalated' | 'SourceChanged' | 'SourceUnavailable'; revision: number; version: number; provenance: 'ConfiguredUnverified' }
export type Inbox = { items: Notice[]; offset: number; hasMore: boolean; policyState: 'ConfiguredUnverified' | 'Unavailable'; delivery: 'InAppOnly'; canManage: boolean; canAcknowledge: boolean; canWritePreference: boolean }
export type NoticePreference = { mutedAdvisory: boolean; revision: number; version: number; mandatoryRouteMutable: false }
export type NoticeCommand = { kind: 'acknowledge' | 'preference'; id: string | null; key: string; body: string }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const integer = (v: unknown, min = 0, max = Number.MAX_SAFE_INTEGER) => typeof v === 'number' && Number.isSafeInteger(v) && v >= min && v <= max
const one = (v: unknown, choices: string[]) => typeof v === 'string' && choices.includes(v)
const text = (v: unknown, max: number) => typeof v === 'string' && v.length > 0 && v.length <= max && v.trim() === v && !/\p{Cc}/u.test(v)
const invalid = (): never => { throw new ApiError(502, 'invalid_notification_contract') }
const fields = ['id', 'vesselId', 'sourceId', 'sourceRevision', 'ruleKey', 'policyVersion', 'sourceState', 'severity', 'mandatory', 'recipientRoleId', 'escalationRoleId', 'dueDate', 'createdAt', 'escalateAt', 'state', 'revision', 'version', 'provenance']
export function decodeInbox(value: unknown, vessel: string, offset: number): Inbox {
  if (!isGuid(vessel) || !integer(offset, 0, 100000) || !exact(value, ['items', 'offset', 'hasMore', 'policyState', 'delivery', 'canManage', 'canAcknowledge', 'canWritePreference']) || value.offset !== offset || typeof value.hasMore !== 'boolean' || value.delivery !== 'InAppOnly' || !one(value.policyState, ['ConfiguredUnverified', 'Unavailable']) || !['canManage', 'canAcknowledge', 'canWritePreference'].every(k => typeof value[k] === 'boolean') || !Array.isArray(value.items) || value.items.length > 25 || value.hasMore && value.items.length !== 25) invalid()
  for (const row of value.items) {
    if (!exact(row, fields) || !isGuid(row.id) || row.vesselId !== vessel || !isGuid(row.sourceId) || !integer(row.sourceRevision, 1) || !text(row.ruleKey, 64) || !text(row.policyVersion, 80) || !one(row.sourceState, ['Due', 'ReviewRequired']) || !one(row.severity, ['Info', 'Warning', 'Critical']) || typeof row.mandatory !== 'boolean' || row.severity === 'Critical' && !row.mandatory || !isGuid(row.recipientRoleId) || !(row.escalationRoleId === null || isGuid(row.escalationRoleId)) || !(row.dueDate === null || typeof row.dueDate === 'string' && calendar(row.dueDate)) || !timestamp(row.createdAt) || !(row.escalateAt === null || timestamp(row.escalateAt)) || !one(row.state, ['Open', 'Acknowledged', 'Escalated', 'SourceChanged', 'SourceUnavailable']) || !integer(row.revision, 1) || !integer(row.version, 1, 4294967295) || row.provenance !== 'ConfiguredUnverified') invalid()
  }
  if (new Set(value.items.map(row => row.id.toLowerCase())).size !== value.items.length) invalid()
  return value as Inbox
}
export function decodePreference(value: unknown): NoticePreference {
  if (!exact(value, ['mutedAdvisory', 'revision', 'version', 'mandatoryRouteMutable']) || typeof value.mutedAdvisory !== 'boolean' || !integer(value.revision) || !integer(value.version, 0, 4294967295) || value.mandatoryRouteMutable !== false || (value.revision === 0) !== (value.version === 0)) invalid()
  return value as NoticePreference
}
const root = (vessel: string) => { if (!isGuid(vessel)) throw new ApiError(400, 'invalid_context'); return `notification-escalation` }
export const loadInbox = async (vessel: string, offset: number, signal: AbortSignal) => decodeInbox(await request<unknown>(`${root(vessel)}?vesselId=${vessel}&offset=${offset}`, signal), vessel, offset)
export const loadPreference = async (vessel: string, signal: AbortSignal) => decodePreference(await request<unknown>(`${root(vessel)}/preference?vesselId=${vessel}`, signal))
export async function sendNoticeCommand(vessel: string, command: NoticeCommand, csrf: string, signal: AbortSignal) {
  if (!isGuid(command.key) || command.kind === 'acknowledge' && !isGuid(command.id)) throw new ApiError(400, 'invalid_command')
  const path = command.kind === 'preference' ? '/preference' : `/${command.id}/acknowledge`
  const value = await request<unknown>(`${root(vessel)}${path}?vesselId=${vessel}`, signal, { method: command.kind === 'preference' ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf, 'Idempotency-Key': command.key }, body: command.body })
  if (command.kind === 'preference') return decodePreference(value)
  if (!exact(value, ['id', 'revision', 'version', 'state']) || value.id !== command.id || value.state !== 'Acknowledged' || !integer(value.revision, 1) || !integer(value.version, 1, 4294967295)) invalid()
  return value
}
