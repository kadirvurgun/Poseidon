import { ApiError, request, session } from './api'
import { isGuid } from './identifiers'
import { hash, vault, prepareOfflineShell } from './offline-application'
import { openOfflineRecord, sealOfflineRecord } from './offline-cipher'
import { OfflineEncryptedRecordStore } from './offline-record-store'
import { offlineKeys as exact, offlineInteger as integer, offlineTime as utc } from './offline-capture-contract'
import { timestamp } from './date-validation'
import type { EventInput, LogContent } from './field-ops-api'

// S10 WP-B client contracts. DEV-091 GET /api/v1/tug-operation-history and DEV-092 /api/v1/tug-operations-offline.
// Closed server codes only: an unknown outcome/state is a contract failure, never coerced to a completed or verified
// state. Offline captures are sealed in IndexedDB (nonextractable AES-GCM key) BEFORE any network attempt; "queued"
// is never "sent". Master release, readiness GO and execution start/transitions are never captured offline.
export const historyOutcomes = ['CANCELLED', 'NOT_STARTED', 'IN_PROGRESS', 'COMPLETED', 'ABORTED', 'MIXED'] as const
export const evidenceStates = ['VERIFIED_WITH_EVIDENCE', 'VERIFIED_NO_EVIDENCE', 'VERIFICATION_REJECTED', 'UNVERIFIED_EVIDENCE', 'UNKNOWN'] as const
export const offlineOperations = ['tug.briefing.ack.v1', 'tug.execution.event.v1', 'tug.daily.entry.v1'] as const
export const offlineResults = ['Committed', 'MaterialConflict', 'ServerRetained'] as const
export type HistoryOutcome = typeof historyOutcomes[number]
export type OfflineOperation = typeof offlineOperations[number]

export type HistoryExecution = { id: string; tugId: string; state: string; verificationState: string; startedAt: string; endedAt: string | null; durationMinutes: number | null
  startedBy: string; endedBy: string | null; verifiedBy: string | null; sequence: number; evidenceEvents: number; lastEventHash: string }
export type HistoryDailyLog = { id: string; tugId: string; logDate: string; watch: string; state: string; linkedToExecution: boolean }
export type HistoryItem = { jobId: string; vesselId: string; jobNumber: string; jobStatus: string; jobType: string | null; assistedVessel: string | null; operationArea: string | null
  at: string; cancelledAt: string | null; outcome: HistoryOutcome; evidence: typeof evidenceStates[number]; planState: string | null; briefingState: string | null
  executions: HistoryExecution[]; dailyLogs: HistoryDailyLog[]; safetyEvents: string }
export type HistoryPage = { items: HistoryItem[]; nextCursor: string | null; outcomes: string[]; readAt: string; deletion: string; sourceVersion: string }
export type HistoryFilter = { vesselId?: string; tugId?: string; jobId?: string; from?: string; to?: string; outcome?: HistoryOutcome | ''; cursor?: string }

const bad = (): never => { throw new ApiError(502, 'invalid_tug_operations_contract') }
const text = (v: unknown, nullable = false) => typeof v === 'string' || nullable && v === null
const day = (v: unknown) => typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v)
const hex = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$/i.test(v)
const ids = (v: unknown) => Array.isArray(v) && v.every(isGuid)

function decodeExecution(v: unknown): HistoryExecution {
  if (!exact(v, ['id', 'tugId', 'state', 'verificationState', 'startedAt', 'endedAt', 'durationMinutes', 'startedBy', 'endedBy', 'verifiedBy', 'sequence', 'evidenceEvents', 'lastEventHash']) ||
    !isGuid(v.id) || !isGuid(v.tugId) || !['ACTIVE', 'SUSPENDED', 'COMPLETED', 'ABORTED'].includes(v.state as string) ||
    !['PENDING', 'VERIFIED', 'REJECTED'].includes(v.verificationState as string) || !timestamp(v.startedAt) || v.endedAt !== null && !timestamp(v.endedAt) ||
    v.durationMinutes !== null && !integer(v.durationMinutes, 0, Number.MAX_SAFE_INTEGER) || !isGuid(v.startedBy) || v.endedBy !== null && !isGuid(v.endedBy) ||
    v.verifiedBy !== null && !isGuid(v.verifiedBy) || !integer(v.sequence, 1, Number.MAX_SAFE_INTEGER) || !integer(v.evidenceEvents, 0, 100000) || !hex(v.lastEventHash)) return bad()
  return v as HistoryExecution
}
function decodeItem(v: unknown): HistoryItem {
  if (!exact(v, ['jobId', 'vesselId', 'jobNumber', 'jobStatus', 'jobType', 'assistedVessel', 'operationArea', 'at', 'cancelledAt', 'outcome', 'evidence', 'planState', 'briefingState',
    'executions', 'dailyLogs', 'safetyEvents']) || !isGuid(v.jobId) || !isGuid(v.vesselId) || !text(v.jobNumber) || !['Draft', 'Submitted', 'Cancelled'].includes(v.jobStatus as string) ||
    !text(v.jobType, true) || !text(v.assistedVessel, true) || !text(v.operationArea, true) || !timestamp(v.at) || v.cancelledAt !== null && !timestamp(v.cancelledAt) ||
    !historyOutcomes.includes(v.outcome as HistoryOutcome) || !evidenceStates.includes(v.evidence as typeof evidenceStates[number]) || !text(v.planState, true) ||
    !text(v.briefingState, true) || !Array.isArray(v.executions) || !Array.isArray(v.dailyLogs) || typeof v.safetyEvents !== 'string') return bad()
  // A cancelled job is visible with its cancellation; it is never silently dropped or relabelled.
  if ((v.outcome === 'CANCELLED') !== (v.jobStatus === 'Cancelled')) return bad()
  const daily = (v.dailyLogs as unknown[]).map(d => {
    if (!exact(d, ['id', 'tugId', 'logDate', 'watch', 'state', 'linkedToExecution']) || !isGuid(d.id) || !isGuid(d.tugId) || !day(d.logDate) || typeof d.watch !== 'string' ||
      !['DRAFT', 'SUBMITTED', 'RETURNED', 'VERIFIED'].includes(d.state as string) || typeof d.linkedToExecution !== 'boolean') return bad()
    return d as HistoryDailyLog
  })
  return { ...(v as HistoryItem), executions: (v.executions as unknown[]).map(decodeExecution), dailyLogs: daily }
}
export function decodeHistoryPage(v: unknown): HistoryPage {
  if (!exact(v, ['items', 'nextCursor', 'outcomes', 'readAt', 'deletion', 'sourceVersion']) || !Array.isArray(v.items) || v.items.length > 25 ||
    v.nextCursor !== null && (typeof v.nextCursor !== 'string' || !/^[A-Za-z0-9_-]{1,96}$/.test(v.nextCursor)) || !Array.isArray(v.outcomes) ||
    !timestamp(v.readAt) || typeof v.deletion !== 'string' || typeof v.sourceVersion !== 'string') return bad()
  const items = (v.items as unknown[]).map(decodeItem)
  if (new Set(items.map(i => i.jobId)).size !== items.length) return bad()
  return { ...(v as HistoryPage), items }
}
export function historyQuery(f: HistoryFilter) {
  const q = new URLSearchParams()
  for (const k of ['vesselId', 'tugId', 'jobId'] as const) if (f[k]) { if (!isGuid(f[k])) throw new ApiError(400, 'invalid_history_filter'); q.set(k, f[k]!) }
  for (const k of ['from', 'to'] as const) if (f[k]) { if (!day(f[k])) throw new ApiError(400, 'invalid_history_filter'); q.set(k, f[k]!) }
  if (f.outcome) q.set('outcome', f.outcome)
  if (f.cursor) q.set('cursor', f.cursor)
  return q.toString()
}
export const loadHistory = async (f: HistoryFilter, signal: AbortSignal) => {
  const q = historyQuery(f)
  return decodeHistoryPage(await request<unknown>(`tug-operation-history${q ? `?${q}` : ''}`, signal))
}

// ---- DEV-092 offline tug operations ----
export type SnapshotExecution = { id: string; tugId: string; state: string; sequence: number; hash: string; canRecord: boolean; towlineConnected: boolean; recordableKinds: string[] }
export type SnapshotDailyLog = { id: string; tugId: string; logDate: string; watch: string; state: string; version: number; revision: number; content: LogContent }
export type TugSnapshot = { id: string; vesselId: string; jobId: string; policyVersion: string; issuedAt: string; expiresAt: string; snapshotHash: string
  job: { id: string; jobNumber: string; status: string; revision: number; assistedVessel: string | null; jobType: string | null; operationArea: string | null; requestedStart: string | null; requestedEnd: string | null }
  briefing: { id: string; revision: number; state: string; contentHash: string; participant: boolean; acknowledged: boolean; acknowledgementState: string } | null
  executions: SnapshotExecution[]; dailyLogs: SnapshotDailyLog[]; writableTugs: string[]; operations: string[]; onlineOnly: string[]; watches: string[]; classification: string; sourceVersion: string }
export type OfflineCapability = { vesselId: string; state: 'CONFIGURED_UNVERIFIED' | 'UNAVAILABLE_POLICY'; operations: string[]; onlineOnly: string[]
  snapshots: { id: string; jobId: string; issuedAt: string; expiresAt: string }[] }
export type OfflineResult = { commandId: string; operation: OfflineOperation; result: typeof offlineResults[number]; resourceId: string; baseVersion: number; serverVersion: number
  conflictFields: string[]; choices: ('keepServer' | 'useCaptured')[]; parentCommandId: string | null; receivedAt: string }
export type BriefingAck = { briefingId: string; revision: number; contentHash: string; choice: 'ACK' | 'DISPUTE'; reason: string | null }
export type ExecutionCapture = { executionId: string; baseState: string; baseSequence: number; event: EventInput }
export type DailyCapture = { logId: string; tugId: string; baseVersion: number | null; logDate: string; watch: string; content: LogContent }
export type CapturePayload = { briefingAck: BriefingAck } | { executionEvent: ExecutionCapture } | { dailyEntry: DailyCapture }
export type QueuedCapture = { commandId: string; revision: number; body: string; operation: OfflineOperation; outcome?: OfflineResult; failure?: string }

export function decodeCapability(v: unknown, vessel: string): OfflineCapability {
  if (!exact(v, ['vesselId', 'state', 'operations', 'onlineOnly', 'snapshots']) || v.vesselId !== vessel || !['CONFIGURED_UNVERIFIED', 'UNAVAILABLE_POLICY'].includes(v.state as string) ||
    !Array.isArray(v.operations) || !Array.isArray(v.onlineOnly) || !Array.isArray(v.snapshots) ||
    v.snapshots.some(s => !exact(s, ['id', 'jobId', 'issuedAt', 'expiresAt']) || !isGuid(s.id) || !isGuid(s.jobId) || !timestamp(s.issuedAt) || !timestamp(s.expiresAt))) return bad()
  return v as OfflineCapability
}
export function decodeSnapshot(v: unknown, vessel: string, job: string): TugSnapshot {
  if (!exact(v, ['id', 'vesselId', 'jobId', 'policyVersion', 'issuedAt', 'expiresAt', 'snapshotHash', 'job', 'briefing', 'executions', 'dailyLogs', 'writableTugs', 'operations',
    'onlineOnly', 'watches', 'classification', 'sourceVersion']) || !isGuid(v.id) || v.vesselId !== vessel || v.jobId !== job || typeof v.policyVersion !== 'string' ||
    !timestamp(v.issuedAt) || !timestamp(v.expiresAt) || Date.parse(v.expiresAt as string) <= Date.parse(v.issuedAt as string) || !hex(v.snapshotHash) ||
    !exact(v.job, ['id', 'jobNumber', 'status', 'revision', 'assistedVessel', 'jobType', 'operationArea', 'requestedStart', 'requestedEnd']) || v.job.id !== job ||
    v.briefing !== null && (!exact(v.briefing, ['id', 'revision', 'state', 'contentHash', 'participant', 'acknowledged', 'acknowledgementState']) || !isGuid(v.briefing.id) ||
      !integer(v.briefing.revision, 1, Number.MAX_SAFE_INTEGER) || !hex(v.briefing.contentHash)) ||
    !Array.isArray(v.executions) || v.executions.some(x => !exact(x, ['id', 'tugId', 'state', 'sequence', 'hash', 'canRecord', 'towlineConnected', 'recordableKinds']) ||
      !isGuid(x.id) || !isGuid(x.tugId) || !integer(x.sequence, 1, Number.MAX_SAFE_INTEGER) || typeof x.canRecord !== 'boolean' || !Array.isArray(x.recordableKinds)) ||
    !Array.isArray(v.dailyLogs) || v.dailyLogs.some(l => !exact(l, ['id', 'tugId', 'logDate', 'watch', 'state', 'version', 'revision', 'content']) || !isGuid(l.id) || !isGuid(l.tugId) ||
      !day(l.logDate) || !integer(l.version, 1, 4294967295)) || !ids(v.writableTugs) || !Array.isArray(v.onlineOnly) ||
    !(v.onlineOnly as unknown[]).includes('MASTER_RELEASE') || !(v.onlineOnly as unknown[]).includes('EXECUTION_START')) return bad()
  return v as TugSnapshot
}
export function decodeOfflineResult(v: unknown, commandId?: string): OfflineResult {
  if (!exact(v, ['commandId', 'operation', 'result', 'resourceId', 'baseVersion', 'serverVersion', 'conflictFields', 'choices', 'parentCommandId', 'receivedAt']) ||
    !isGuid(v.commandId) || commandId !== undefined && v.commandId !== commandId || !offlineOperations.includes(v.operation as OfflineOperation) ||
    !offlineResults.includes(v.result as typeof offlineResults[number]) || !isGuid(v.resourceId) || !integer(v.baseVersion, 0, 4294967295) ||
    !integer(v.serverVersion, 0, 4294967295) || !Array.isArray(v.conflictFields) || !Array.isArray(v.choices) ||
    v.choices.some(c => c !== 'keepServer' && c !== 'useCaptured') || v.parentCommandId !== null && !isGuid(v.parentCommandId) || !utc(v.receivedAt) ||
    (v.result === 'MaterialConflict') !== (v.conflictFields.length > 0) || v.result === 'ServerRetained' && v.parentCommandId === null) return bad()
  return v as OfflineResult
}

const root = (vessel: string, suffix: string, extra = '') => { if (!isGuid(vessel)) return bad(); return `tug-operations-offline${suffix}?vessel=${vessel}${extra}` }
export const loadCapability = async (vessel: string, signal: AbortSignal) => decodeCapability(await request<unknown>(root(vessel, ''), signal), vessel)
export const loadConflict = async (vessel: string, command: string, signal: AbortSignal) => {
  if (!isGuid(command)) return bad()
  const v = await request<unknown>(root(vessel, `/conflicts/${command}`), signal)
  if (!exact(v, ['commandId', 'operation', 'resourceId', 'conflictFields', 'choices', 'resolved', 'captured', 'atConflict', 'current']) || v.commandId !== command ||
    typeof v.resolved !== 'boolean' || !Array.isArray(v.choices) || !Array.isArray(v.conflictFields)) return bad()
  return v as { commandId: string; operation: OfflineOperation; resourceId: string; conflictFields: string[]; choices: string[]; resolved: boolean; captured: unknown
    atConflict: Record<string, unknown>; current: Record<string, unknown> }
}

const limits = { plaintextBytes: 524288, contextBytes: 4096 }
const database = 'poseidon.offline.tug-operations.v1'
type Slot = { id: string; key: CryptoKey; invocations: number }
type Sealed = { kind: 'snapshot'; body: string } | { kind: 'capture'; body: string; operation: OfflineOperation; outcome?: OfflineResult; failure?: string }
const operationOf = (payload: CapturePayload): OfflineOperation =>
  'briefingAck' in payload ? 'tug.briefing.ack.v1' : 'executionEvent' in payload ? 'tug.execution.event.v1' : 'tug.daily.entry.v1'

// One durable queue per actor + tug vessel + job. Snapshots and captures are sealed records in the same queue; a
// capture carries the snapshot id it was made on, so a later lease never rebases an earlier capture.
export class TugOperationsOfflineQueue {
  private closed = false
  private constructor(readonly tenantId: string, readonly actorId: string, readonly vessel: string, readonly job: string, private slot: Slot, private store: OfflineEncryptedRecordStore) {}
  static async open(vessel: string, job: string, signal: AbortSignal) {
    if (!isGuid(vessel) || !isGuid(job)) return bad()
    const user = await session(signal)
    if (!user.authenticated || !isGuid(user.actorId) || !isGuid(user.tenantId)) throw new ApiError(401)
    await prepareOfflineShell(signal)
    const id = await hash(JSON.stringify(['tug-operations-v1', user.tenantId, user.actorId, vessel, job]))
    let slot = await vault<Slot | null>((s, done) => { const q = s.get(id); q.onsuccess = () => done((q.result as Slot | undefined) ?? null) })
    if (!slot) {
      const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
      slot = await vault<Slot>((s, done) => { const row = { id, key, invocations: 0 }; s.put(row); done(row) })
    }
    if (!slot.key || slot.key.extractable) throw new Error('key_lost_data_retained')
    const store = await OfflineEncryptedRecordStore.open(indexedDB, id, { maxRecords: 64, maxEncryptedBytes: 4194304, maxDeviceRecords: 512, maxDeviceEncryptedBytes: 16777216 }, database)
    return new TugOperationsOfflineQueue(user.tenantId!, user.actorId!, vessel, job, slot, store)
  }
  private aad(commandId: string) {
    return { tenantId: this.tenantId, actorId: this.actorId, vesselId: this.vessel, deviceId: this.slot.id, sessionBinding: `tug-operations/${this.vessel}/${this.job}`,
      policyVersion: 'tug-operations-offline-v1', commandId }
  }
  private requireOpen() { if (this.closed) throw new Error('queue_closed') }
  private async write(id: string, value: Sealed, revision: number | null) {
    this.requireOpen()
    await vault<void>((s, done) => { const q = s.get(this.slot.id); q.onsuccess = () => { const row = q.result as Slot | undefined
      if (!row || row.invocations >= 4096) { s.transaction.abort(); return } s.put({ ...row, invocations: row.invocations + 1 }); done(undefined) } })
    const sealed = await sealOfflineRecord(crypto, this.slot.key, this.aad(id), new TextEncoder().encode(JSON.stringify(value)), limits)
    return this.store.put(id, sealed, revision)
  }
  private async records() {
    this.requireOpen(); const rows: { commandId: string; revision: number; value: Sealed }[] = []
    for (const header of await this.store.listRetained(64)) {
      const stored = await this.store.read(header.commandId); if (!stored) continue
      const value: unknown = JSON.parse(new TextDecoder().decode(await openOfflineRecord(crypto, this.slot.key, this.aad(header.commandId), stored.sealed, limits)))
      if (!exact(value, ['kind', 'body'], ['operation', 'outcome', 'failure']) || typeof value.body !== 'string' || value.kind !== 'snapshot' && value.kind !== 'capture') return bad()
      rows.push({ commandId: header.commandId, revision: header.revision, value: value as Sealed })
    }
    return rows
  }
  // Prefetch (online): the server records the lease and hash; the snapshot is sealed on this device.
  async prefetch(signal: AbortSignal): Promise<TugSnapshot> {
    const user = await session(signal)
    if (!user.authenticated || user.actorId !== this.actorId || user.tenantId !== this.tenantId || !user.csrfToken) throw new ApiError(403)
    const snapshot = decodeSnapshot(await request<unknown>(root(this.vessel, '/snapshots', `&job=${this.job}`), signal,
      { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken }, body: '{}' }), this.vessel, this.job)
    await this.write(snapshot.id, { kind: 'snapshot', body: JSON.stringify(snapshot) }, null)
    return snapshot
  }
  async latestSnapshot(): Promise<TugSnapshot | null> {
    const snapshots = (await this.records()).filter(r => r.value.kind === 'snapshot').map(r => decodeSnapshot(JSON.parse(r.value.body), this.vessel, this.job))
    return snapshots.sort((a, b) => Date.parse(b.issuedAt) - Date.parse(a.issuedAt))[0] ?? null
  }
  async captures(): Promise<QueuedCapture[]> {
    return (await this.records()).filter(r => r.value.kind === 'capture').map(r => {
      const v = r.value as Extract<Sealed, { kind: 'capture' }>
      return { commandId: r.commandId, revision: r.revision, body: v.body, operation: v.operation, ...(v.outcome ? { outcome: decodeOfflineResult(v.outcome, r.commandId) } : {}),
        ...(v.failure ? { failure: v.failure } : {}) }
    })
  }
  // Durable first: the frozen command is sealed before any network attempt; works with no connection.
  async capture(snapshot: TugSnapshot, payload: CapturePayload, resolution?: { parentCommandId: string; choice: 'keepServer' | 'useCaptured'; reason: string }) {
    const commandId = crypto.randomUUID(), operation = operationOf(payload)
    const body = JSON.stringify({ commandId, snapshotId: snapshot.id, operation, capturedAt: new Date().toISOString(), offsetMinutes: -new Date().getTimezoneOffset(), ...payload,
      ...(resolution ?? {}) })
    await this.write(commandId, { kind: 'capture', body, operation }, null)
    return commandId
  }
  // Same command id and identical bytes on every retry: the server returns the first outcome.
  async replay(capture: QueuedCapture, signal: AbortSignal): Promise<OfflineResult> {
    const stored = (await this.captures()).find(c => c.commandId === capture.commandId)
    if (!stored || stored.revision !== capture.revision || stored.body !== capture.body) return bad()
    if (stored.outcome) return stored.outcome
    const user = await session(signal)
    if (!user.authenticated || user.actorId !== this.actorId || user.tenantId !== this.tenantId || !user.csrfToken) { this.close(); throw new ApiError(403) }
    try {
      const value = decodeOfflineResult(await request<unknown>(root(this.vessel, '/replay'), signal, { method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': stored.commandId }, body: stored.body }), stored.commandId)
      if (signal.aborted) throw new Error('response_withdrawn')
      await this.write(stored.commandId, { kind: 'capture', body: stored.body, operation: stored.operation, outcome: value }, stored.revision)
      return value
    } catch (error) {
      // A definite refusal is kept with its code for a person to read; the capture is never silently dropped.
      if (error instanceof ApiError && [400, 403, 404, 409, 422].includes(error.status))
        await this.write(stored.commandId, { kind: 'capture', body: stored.body, operation: stored.operation, failure: error.code }, stored.revision)
      throw error
    }
  }
  close() { if (this.closed) return; this.closed = true; this.store.close() }
}
export const queuedState = (c: QueuedCapture) => c.outcome ? c.outcome.result : c.failure ? 'Rejected' : 'Queued'
