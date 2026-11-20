import { ApiError, request } from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

// DEV-035 / T-S4-003: strict consumer of GET /api/v1/sync/conflicts (SyncConflictCenterEndpoints).
// The index carries command metadata only; captured/server values are read from each owning
// source's protected conflict detail. An extra member, a value field or a foreign vessel is a
// contract breach, never ignored.
export type SyncConflictSource = 'crewAssignment' | 'tugPlan' | 'dailyRecord' | 'workOrderEvidence' | 'tugOperations' | 'certificateRenewal'
export type SyncConflictState = 'Open' | 'Superseded' | 'Resolved'
export type SyncConflictItem = {
  source: SyncConflictSource; resourceId: string; resourceKind: string | null; enrollmentId: string; commandId: string
  baseVersion: number; serverVersionAtConflict: number; capturedAt: string | null; receivedAt: string; state: SyncConflictState
  resolutionCommandId: string | null; resolutionResult: string | null
}
export type SyncConflictPage = {
  vesselId: string; readAt: string; sources: { source: SyncConflictSource; state: 'Included' | 'Partial' | 'NotPermitted' }[]
  items: SyncConflictItem[]; offset: number; hasMore: boolean
}
export const conflictPageSize = 25
const sources: readonly string[] = ['crewAssignment', 'tugPlan', 'dailyRecord', 'workOrderEvidence', 'tugOperations', 'certificateRenewal']
// S10 WP-B DEV-092: tug operation captures carry their closed operation code; a create capture has base version 0.
const tugOperations: readonly string[] = ['tug.briefing.ack.v1', 'tug.execution.event.v1', 'tug.daily.entry.v1']
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const version = (v: unknown) => typeof v === 'number' && Number.isSafeInteger(v) && v >= 1 && v <= 4294967295
const fail = (): never => { throw new ApiError(502, 'invalid_conflict_center_contract') }

function decodeItem(v: unknown): SyncConflictItem {
  if (!exact(v, ['source', 'resourceId', 'resourceKind', 'enrollmentId', 'commandId', 'baseVersion', 'serverVersionAtConflict',
    'capturedAt', 'receivedAt', 'state', 'resolutionCommandId', 'resolutionResult']) ||
    typeof v.source !== 'string' || !sources.includes(v.source) || !isGuid(v.resourceId) || !isGuid(v.enrollmentId) || !isGuid(v.commandId) ||
    (v.source === 'dailyRecord' ? typeof v.resourceKind !== 'string' || !['Noon', 'Deck', 'Engine'].includes(v.resourceKind)
      : v.source === 'tugOperations' ? typeof v.resourceKind !== 'string' || !tugOperations.includes(v.resourceKind) : v.resourceKind !== null) ||
    (v.source === 'tugOperations' ? !Number.isSafeInteger(v.baseVersion) || (v.baseVersion as number) < 0 || (v.baseVersion as number) > 4294967295 ||
      !Number.isSafeInteger(v.serverVersionAtConflict) || (v.serverVersionAtConflict as number) < 0 || (v.serverVersionAtConflict as number) > 4294967295
      : !version(v.baseVersion) || !version(v.serverVersionAtConflict)) ||
    v.capturedAt !== null && !timestamp(v.capturedAt) || !timestamp(v.receivedAt) ||
    typeof v.state !== 'string' || !['Open', 'Superseded', 'Resolved'].includes(v.state)) return fail()
  // Open has no successor; Superseded only by a new MaterialConflict; Resolved only by a non-conflict outcome.
  if (v.state === 'Open' ? v.resolutionCommandId !== null || v.resolutionResult !== null
    : !isGuid(v.resolutionCommandId) || v.resolutionCommandId === v.commandId || typeof v.resolutionResult !== 'string' ||
      (v.state === 'Superseded' ? v.resolutionResult !== 'MaterialConflict' : !['Committed', 'ServerRetained'].includes(v.resolutionResult))) return fail()
  return v as SyncConflictItem
}

export function decodeSyncConflictPage(value: unknown, vessel: string, offset: number): SyncConflictPage {
  if (!isGuid(vessel) || !exact(value, ['vesselId', 'readAt', 'sources', 'items', 'offset', 'hasMore']) || value.vesselId !== vessel ||
    !timestamp(value.readAt) || value.offset !== offset || typeof value.hasMore !== 'boolean' ||
    !Array.isArray(value.sources) || value.sources.length !== sources.length ||
    value.sources.some((s, i) => !exact(s, ['source', 'state']) || s.source !== sources[i] || typeof s.state !== 'string' || !['Included', 'Partial', 'NotPermitted'].includes(s.state)) ||
    !Array.isArray(value.items) || value.items.length > conflictPageSize) return fail()
  const items = (value.items as unknown[]).map(decodeItem)
  const page = value.sources as SyncConflictPage['sources']
  if (new Set(items.map(i => `${i.enrollmentId}/${i.commandId}`)).size !== items.length ||
    items.some(i => page.find(s => s.source === i.source)?.state === 'NotPermitted')) return fail()
  return { ...(value as SyncConflictPage), items }
}

export async function loadSyncConflicts(vessel: string, offset: number, signal: AbortSignal): Promise<SyncConflictPage> {
  if (!isGuid(vessel) || !Number.isSafeInteger(offset) || offset < 0 || offset > 1000 || offset % conflictPageSize !== 0) throw new ApiError(400, 'invalid_conflict_center_context')
  const query = `vesselId=${encodeURIComponent(vessel)}${offset ? `&offset=${offset}` : ''}`
  return decodeSyncConflictPage(await request<unknown>(`sync/conflicts?${query}`, signal), vessel, offset)
}
