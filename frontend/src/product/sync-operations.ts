import { ApiError, request, session } from './api'
import { isGuid } from './identifiers'

// DEV-033/DEV-034 (18.01 Backlog rows 34/35). Hand-authored strict decoder for
// POST /api/v1/sync/operations; generated types live in src/generated/api.d.ts.
// Reconciliation is read-only on the server: it never replays or re-signs a command.
export const syncDomains = ['CrewWatchGroup', 'TugPlanWindow', 'DailyNoon', 'DailyDeck', 'DailyEngine', 'WorkOrderEvidence'] as const
export type SyncDomain = typeof syncDomains[number]
export const syncStates = ['NotReceived', 'Recorded', 'PayloadMismatch', 'KeyReused', 'Unavailable'] as const
export type SyncState = typeof syncStates[number]
export const maxSyncOperations = 64
export type SyncOperationRef = Readonly<{ operationId: string; domain: SyncDomain; resourceId: string; payloadDigest: string }>
export type SyncOperationOutcome = Readonly<{
  operationId: string; domain: SyncDomain; resourceId: string; state: SyncState; result: string | null
  baseVersion: number | null; serverVersion: number | null; revision: number | null; parentCommandId: string | null; receivedAt: string | null
}>

const invalid = (): never => { throw new ApiError(502, 'invalid_contract') }
const keys = (value: unknown, expected: readonly string[]): value is Record<string, unknown> =>
  !!value && typeof value === 'object' && !Array.isArray(value) && Object.keys(value).length === expected.length && expected.every(k => Object.hasOwn(value, k))
const int = (value: unknown, min: number, max: number) => typeof value === 'number' && Number.isSafeInteger(value) && value >= min && value <= max
const isTime = (value: unknown) => typeof value === 'string' && value.length <= 40 && /^\d{4}-\d{2}-\d{2}T/.test(value) && !Number.isNaN(Date.parse(value))

export function validSyncRequest(refs: readonly SyncOperationRef[]) {
  return refs.length > 0 && refs.length <= maxSyncOperations && new Set(refs.map(r => r.operationId)).size === refs.length &&
    refs.every(r => isGuid(r.operationId) && isGuid(r.resourceId) && syncDomains.includes(r.domain) && /^[0-9a-fA-F]{64}$/.test(r.payloadDigest))
}

// The response must answer exactly the asked operations, in order. A Recorded item
// carries the complete authoritative receipt; every other state carries none.
export function decodeSyncResult(value: unknown, vesselId: string, refs: readonly SyncOperationRef[]): SyncOperationOutcome[] {
  if (!keys(value, ['vesselId', 'serverTime', 'items']) || value.vesselId !== vesselId || !isTime(value.serverTime) ||
    !Array.isArray(value.items) || value.items.length !== refs.length) return invalid()
  return value.items.map((item: unknown, index: number) => {
    const ref = refs[index]
    if (!keys(item, ['operationId', 'domain', 'resourceId', 'state', 'result', 'baseVersion', 'serverVersion', 'revision', 'parentCommandId', 'receivedAt']) ||
      item.operationId !== ref.operationId || item.domain !== ref.domain || item.resourceId !== ref.resourceId ||
      !syncStates.includes(item.state as SyncState)) return invalid()
    if (item.state === 'Recorded') {
      if (!['Committed', 'MaterialConflict', 'ServerRetained'].includes(String(item.result)) || !int(item.baseVersion, 0, 4294967295) ||
        !int(item.serverVersion, 0, 4294967295) || !int(item.revision, 0, Number.MAX_SAFE_INTEGER) || !isTime(item.receivedAt) ||
        item.parentCommandId !== null && !isGuid(item.parentCommandId)) return invalid()
    } else if (['result', 'baseVersion', 'serverVersion', 'revision', 'parentCommandId', 'receivedAt'].some(k => item[k] !== null)) return invalid()
    return item as SyncOperationOutcome
  })
}

export async function reconcileOperations(vesselId: string, refs: readonly SyncOperationRef[], signal: AbortSignal): Promise<SyncOperationOutcome[]> {
  if (!isGuid(vesselId) || !validSyncRequest(refs)) throw new Error('invalid_sync_request')
  const user = await session(signal)
  if (!user.authenticated) throw new ApiError(401)
  const value = await request<unknown>('sync/operations', signal, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken },
    body: JSON.stringify({ vesselId, operations: refs }),
  })
  if (signal.aborted) throw new Error('response_withdrawn')
  return decodeSyncResult(value, vesselId, refs)
}

// Device-visible queue state. "recorded" is a server receipt, never an operational
// approval; anything not proven by a receipt stays pending/failed/conflict.
export type QueueDisplayState = 'pending' | 'blocked' | 'recorded' | 'conflict' | 'rejected' | 'unavailable'
export type QueuedOperation = Readonly<{
  operationId: string; resourceKey: string; capturedAt: string
  outcome?: Readonly<{ result: string }> | null; sync?: SyncState | null
  // Explicit human resolution of a retained conflict (immutable parent lineage).
  parentOperationId?: string | null
}>

export function queueDisplayState(op: QueuedOperation): Exclude<QueueDisplayState, 'blocked'> {
  // ServerRetained is a terminal explicit human resolution receipt (Daily keep-server).
  if (op.outcome?.result === 'Committed' || op.outcome?.result === 'ServerRetained') return 'recorded'
  if (op.outcome?.result === 'MaterialConflict') return 'conflict'
  if (op.sync === 'PayloadMismatch' || op.sync === 'KeyReused') return 'rejected'
  if (op.sync === 'Unavailable') return 'unavailable'
  return 'pending'
}

// Ordered per resource: capture time then key (stable, never wall-clock reordered
// across resources). Only the first unresolved operation of each resource may be
// sent; later ones wait behind a pending, conflicting or rejected predecessor so a
// newer local change can never overtake (and silently overwrite) an older one.
export function orderReplayQueue<T extends QueuedOperation>(ops: readonly T[]): { op: T; state: QueueDisplayState; sendable: boolean }[] {
  const time = (op: T) => { const t = Date.parse(op.capturedAt); return Number.isNaN(t) ? Number.POSITIVE_INFINITY : t }
  const sorted = [...ops].sort((a, b) => a.resourceKey < b.resourceKey ? -1 : a.resourceKey > b.resourceKey ? 1 :
    time(a) - time(b) || (a.operationId < b.operationId ? -1 : a.operationId > b.operationId ? 1 : 0))
  const blocked = new Set<string>()
  const resolving = new Set(ops.map(op => op.parentOperationId).filter((id): id is string => typeof id === 'string'))
  return sorted.map(op => {
    const state = queueDisplayState(op)
    if (blocked.has(op.resourceKey) && state === 'pending') return { op, state: 'blocked' as const, sendable: false }
    // A conflict whose explicit resolution is queued no longer holds the resource;
    // the resolution itself is the next ordered operation.
    if (state === 'conflict' && resolving.has(op.operationId)) return { op, state, sendable: false }
    if (state !== 'recorded') {
      const first = !blocked.has(op.resourceKey)
      blocked.add(op.resourceKey)
      return { op, state, sendable: first && state === 'pending' }
    }
    return { op, state, sendable: false }
  })
}

// Panel helper: derive ordering inputs from the frozen body without rewriting it.
// Malformed bodies sort last and stay pending; nothing here supplies replay values.
export function replayGate(rows: readonly { commandId: string; body: string; outcome?: Readonly<{ result: string }> | null }[],
  resourceKey: string, states: ReadonlyMap<string, SyncState>): ReadonlyMap<string, { state: QueueDisplayState; sendable: boolean }> {
  const ops = rows.map(row => {
    let capturedAt = '', parentOperationId: string | null = null
    try {
      const value: unknown = row.body.length <= 65536 ? JSON.parse(row.body) : null
      if (value && typeof value === 'object' && !Array.isArray(value)) {
        const v = value as Record<string, unknown>
        if (typeof v.capturedAt === 'string') capturedAt = v.capturedAt
        if (isGuid(v.parentCommandId)) parentOperationId = v.parentCommandId
      }
    } catch { /* malformed body: remains pending, ordered last */ }
    return { operationId: row.commandId, resourceKey, capturedAt, outcome: row.outcome ?? null, sync: states.get(row.commandId) ?? null, parentOperationId }
  })
  return new Map(orderReplayQueue(ops).map(entry => [entry.op.operationId, { state: entry.state, sendable: entry.sendable }]))
}
