import { ApiError, request, session } from './api'
import { isGuid } from './identifiers'
import { base64, hash, vault, prepareOfflineShell, type KeySlot } from './offline-application'
import { openOfflineRecord, sealOfflineRecord } from './offline-cipher'
import { OfflineEncryptedRecordStore } from './offline-record-store'
import { maxSyncOperations, reconcileOperations, type SyncState } from './sync-operations'
import { offlineKeys as exact, offlineInteger as integer, offlineTime as utc } from './offline-capture-contract'
import { validEvidence, executionStates, evidenceKinds, type Evidence, type EvidenceKind, type ExecutionState } from './work-order-execution-api'

// DEV-057 / T-S8-006 work order offline execution evidence. Same protocol as the shared offline
// application (DEV-033..035): server-owned enrollment, nonextractable AES-GCM sealed IndexedDB records,
// P-256 key proof, signed idempotent replay and DEV-034 reconnect reconciliation. Only evidence capture
// is offline; release/start/submit/verify/close remain online human/server commands.
export const workOrderOfflineOperation = 'work-order.execution-evidence.v1'
const bad = (): never => { throw new ApiError(502, 'invalid_work_order_offline_contract') }
const digest = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
export type SpareOption = { id: string; revision: number; partNumber: string; name: string; unit: string }
export type WorkOrderOfflineContext = { id: string; tenantId: string; actorId: string; vesselId: string; workOrderId: string; deviceId: string; keyId: string; challenge: string
  policyVersion: string; policyDigest: string; authorityDigest: string; sourceVersion: string; operation: string; allowedParts: string[]; baseVersion: number; baseRevision: number
  baseEvidence: Evidence; state: ExecutionState; requiredEvidence: EvidenceKind[] | null; workOrderFields: Record<string, { state: string; value: string | null }>; spareOptions: SpareOption[]
  serverTime: string; expiresAt: string; retainUntil: string; maxRecords: number; maxEncryptedBytes: number; maxDeviceRecords: number; maxDeviceEncryptedBytes: number }
export type WorkOrderCapture = { commandId: string; workOrderId: string; keyId: string; deviceId: string; policyVersion: string; policyDigest: string; sourceVersion: string; operation: string
  baseVersion: number; capturedAt: string; offsetMinutes: number; evidence: Evidence | null; parentCommandId?: string; expectedServerVersion?: number; choice?: 'keepServer' | 'useCaptured'
  reasonCode?: 'CURRENT_SERVER_RETAINED' | 'INPUT_CORRECTED' }
export type WorkOrderOfflineOutcome = { commandId: string; result: 'Committed' | 'MaterialConflict' | 'ServerRetained'; workOrderId: string; baseVersion: number; serverVersion: number
  revision: number; parentCommandId: string | null; receivedAt: string }
export type WorkOrderPending = { commandId: string; revision: number; body: string; outcome?: WorkOrderOfflineOutcome }
export type WorkOrderConflict = { commandId: string; enrollmentId: string; workOrderId: string; currentVersion: number; base: Evidence; captured: Evidence; server: Evidence
  choices: ('keepServer' | 'useCaptured')[]; resolved: WorkOrderOfflineOutcome | null }

export function decodeWorkOrderOfflineContext(v: unknown, vessel: string, workOrder: string): WorkOrderOfflineContext {
  if (!exact(v, ['id', 'tenantId', 'actorId', 'vesselId', 'workOrderId', 'deviceId', 'keyId', 'challenge', 'policyVersion', 'policyDigest', 'authorityDigest', 'sourceVersion', 'operation',
    'allowedParts', 'baseVersion', 'baseRevision', 'baseEvidence', 'state', 'requiredEvidence', 'workOrderFields', 'spareOptions', 'serverTime', 'expiresAt', 'retainUntil', 'maxRecords',
    'maxEncryptedBytes', 'maxDeviceRecords', 'maxDeviceEncryptedBytes']) || v.vesselId !== vessel || v.workOrderId !== workOrder ||
    ['id', 'tenantId', 'actorId', 'deviceId', 'keyId'].some(k => !isGuid(v[k])) || typeof v.challenge !== 'string' || !/^[A-Za-z0-9+/]{43}=$/.test(v.challenge) ||
    typeof v.policyVersion !== 'string' || v.policyVersion.length < 1 || v.policyVersion.length > 80 || !digest(v.policyDigest) || !digest(v.authorityDigest) ||
    typeof v.sourceVersion !== 'string' || v.operation !== workOrderOfflineOperation || !Array.isArray(v.allowedParts) || v.allowedParts.length !== 5 ||
    !integer(v.baseVersion, 1, 4294967295) || !integer(v.baseRevision, 1, Number.MAX_SAFE_INTEGER) || !validEvidence(v.baseEvidence) ||
    !executionStates.includes(v.state as ExecutionState) || !(v.requiredEvidence === null || Array.isArray(v.requiredEvidence) && v.requiredEvidence.every(k => evidenceKinds.includes(k as EvidenceKind))) ||
    !v.workOrderFields || typeof v.workOrderFields !== 'object' || !Array.isArray(v.spareOptions) || v.spareOptions.length > 200 ||
    v.spareOptions.some(s => !exact(s, ['id', 'revision', 'partNumber', 'name', 'unit']) || !isGuid(s.id) || !integer(s.revision, 1, Number.MAX_SAFE_INTEGER) ||
      typeof s.partNumber !== 'string' || typeof s.name !== 'string' || typeof s.unit !== 'string') ||
    !utc(v.serverTime) || !utc(v.expiresAt) || !utc(v.retainUntil) || Date.parse(v.expiresAt) <= Date.parse(v.serverTime) || Date.parse(v.retainUntil) < Date.parse(v.expiresAt) ||
    !integer(v.maxRecords, 2, 128) || !integer(v.maxEncryptedBytes, 131072, 4194304) || !integer(v.maxDeviceRecords, v.maxRecords, 512) || !integer(v.maxDeviceEncryptedBytes, v.maxEncryptedBytes, 16777216)) return bad()
  return v as WorkOrderOfflineContext
}
export function decodeWorkOrderCapture(v: unknown, c: WorkOrderOfflineContext, id: string): WorkOrderCapture {
  if (!exact(v, ['commandId', 'workOrderId', 'keyId', 'deviceId', 'policyVersion', 'policyDigest', 'sourceVersion', 'operation', 'baseVersion', 'capturedAt', 'offsetMinutes', 'evidence'],
    ['parentCommandId', 'expectedServerVersion', 'choice', 'reasonCode']) || v.commandId !== id || !isGuid(id) || v.workOrderId !== c.workOrderId || v.keyId !== c.keyId ||
    v.deviceId !== c.deviceId || v.policyVersion !== c.policyVersion || v.policyDigest !== c.policyDigest || v.sourceVersion !== c.sourceVersion || v.operation !== c.operation ||
    !utc(v.capturedAt) || !integer(v.offsetMinutes, -840, 840) ||
    (v.parentCommandId === undefined ? v.baseVersion !== c.baseVersion || !validEvidence(v.evidence) || ['expectedServerVersion', 'choice', 'reasonCode'].some(k => Object.hasOwn(v, k))
      : !isGuid(v.parentCommandId) || v.parentCommandId === id || !integer(v.baseVersion, 1, 4294967295) || v.expectedServerVersion !== v.baseVersion || v.evidence !== null ||
        !['keepServer', 'useCaptured'].includes(String(v.choice)) || v.reasonCode !== (v.choice === 'keepServer' ? 'CURRENT_SERVER_RETAINED' : 'INPUT_CORRECTED'))) return bad()
  return v as WorkOrderCapture
}
export function decodeWorkOrderOutcome(v: unknown, workOrder: string, capture?: WorkOrderCapture): WorkOrderOfflineOutcome {
  if (!exact(v, ['commandId', 'result', 'workOrderId', 'baseVersion', 'serverVersion', 'revision', 'parentCommandId', 'receivedAt']) || !isGuid(v.commandId) || v.workOrderId !== workOrder ||
    !['Committed', 'MaterialConflict', 'ServerRetained'].includes(String(v.result)) || !integer(v.baseVersion, 1, 4294967295) || !integer(v.serverVersion, 1, 4294967295) ||
    !integer(v.revision, 1, Number.MAX_SAFE_INTEGER) || v.result === 'ServerRetained' && v.parentCommandId === null || v.parentCommandId !== null && !isGuid(v.parentCommandId) || !utc(v.receivedAt) ||
    capture && (v.commandId !== capture.commandId || v.baseVersion !== capture.baseVersion || v.parentCommandId !== (capture.parentCommandId ?? null))) return bad()
  return v as WorkOrderOfflineOutcome
}
export function decodeWorkOrderConflict(v: unknown, c: WorkOrderOfflineContext, id: string): WorkOrderConflict {
  if (!exact(v, ['commandId', 'enrollmentId', 'workOrderId', 'currentVersion', 'base', 'captured', 'server', 'choices', 'resolved']) || v.commandId !== id || v.enrollmentId !== c.id ||
    v.workOrderId !== c.workOrderId || !integer(v.currentVersion, 1, 4294967295) || ![v.base, v.captured, v.server].every(validEvidence) || !Array.isArray(v.choices) || v.choices.length > 2 ||
    v.choices.some(x => !['keepServer', 'useCaptured'].includes(String(x))) || new Set(v.choices).size !== v.choices.length || v.resolved !== null && v.choices.length !== 0) return bad()
  if (v.resolved !== null) { const resolved = decodeWorkOrderOutcome(v.resolved, c.workOrderId); if (resolved.parentCommandId !== id || resolved.result === 'MaterialConflict') return bad() }
  return v as WorkOrderConflict
}
// Pending = captured and not yet terminally recorded. A conflict stays pending until a same-context
// child records Committed/ServerRetained; malformed rows conservatively remain pending.
export function projectWorkOrderQueue(rows: readonly WorkOrderPending[]) {
  const resolved = new Set(rows.flatMap(row => { try { const body = JSON.parse(row.body) as { parentCommandId?: string }
    return row.outcome && row.outcome.result !== 'MaterialConflict' && body.parentCommandId ? [body.parentCommandId] : [] } catch { return [] } }))
  return { resolved, pending: rows.filter(row => !row.outcome || row.outcome.result === 'MaterialConflict' && !resolved.has(row.commandId)).length,
    conflicts: rows.filter(row => row.outcome?.result === 'MaterialConflict' && !resolved.has(row.commandId)).map(row => row.commandId) }
}

const limits = { plaintextBytes: 1048576, contextBytes: 4096 }
const database = 'poseidon.offline.work-order.v1'
export const workOrderOfflineRoot = (vessel: string, workOrder: string, suffix = '') => {
  if (!isGuid(vessel) || !isGuid(workOrder)) return bad()
  return `work-order-offline/${workOrder}${suffix}?vesselId=${vessel}`
}
const slotOf = (c: Pick<WorkOrderOfflineContext, 'tenantId' | 'actorId' | 'vesselId' | 'workOrderId' | 'id' | 'deviceId' | 'keyId' | 'policyDigest' | 'authorityDigest' | 'sourceVersion'>) =>
  hash(JSON.stringify(['work-order-evidence-v1', c.tenantId, c.actorId, c.vesselId, c.workOrderId, c.id, c.deviceId, c.keyId, c.policyDigest, c.authorityDigest, c.sourceVersion]))

// Only explicitly authorized in-memory handles can use the retained nonextractable keys. A restart
// exposes no plaintext until current online renewal and key proof; captures persist encrypted first.
export class WorkOrderOfflineApplication {
  private closed = false
  private constructor(readonly context: WorkOrderOfflineContext, private keys: KeySlot, private slot: string, private store: OfflineEncryptedRecordStore, private started: number) {}
  static async authorize(vessel: string, workOrder: string, enrollment: string | null, signal: AbortSignal) {
    const user = await session(signal)
    if (!user.authenticated || !isGuid(user.actorId) || !isGuid(user.tenantId) || !user.csrfToken || enrollment !== null && !isGuid(enrollment)) throw new ApiError(401)
    const started = performance.now()
    const ctx = decodeWorkOrderOfflineContext(await request<unknown>(workOrderOfflineRoot(vessel, workOrder, enrollment ? `/${enrollment}/renew` : ''), signal,
      { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } }), vessel, workOrder)
    if (ctx.actorId !== user.actorId || ctx.tenantId !== user.tenantId || signal.aborted) throw new ApiError(403)
    await prepareOfflineShell(signal)
    const slot = await slotOf(ctx)
    let keys = await vault<KeySlot | null>((s, done) => { const q = s.get(slot); q.onsuccess = () => done(q.result ?? null) })
    if (!keys && enrollment) throw new Error('key_lost_data_retained')
    if (!keys) {
      const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
      const signing = await crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign', 'verify'])
      keys = await vault<KeySlot>((s, done) => { const row = { id: slot, key, signing, invocations: 0 }; s.put(row); done(row) })
    }
    if (!keys.key || keys.key.extractable || !keys.signing?.privateKey || keys.signing.privateKey.extractable) throw new Error('key_lost_data_retained')
    const store = await OfflineEncryptedRecordStore.open(indexedDB, slot, { maxRecords: ctx.maxRecords, maxEncryptedBytes: ctx.maxEncryptedBytes, maxDeviceRecords: ctx.maxDeviceRecords, maxDeviceEncryptedBytes: ctx.maxDeviceEncryptedBytes }, database)
    const app = new WorkOrderOfflineApplication(ctx, keys, slot, store, started)
    try {
      if (!enrollment) {
        const publicKey = base64(await crypto.subtle.exportKey('spki', keys.signing.publicKey))
        const proof = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, keys.signing.privateKey, new TextEncoder().encode(`work-order/${workOrder}/${ctx.id}/${ctx.keyId}/${ctx.challenge}`)))
        const status = await request<unknown>(workOrderOfflineRoot(vessel, workOrder, `/${ctx.id}/key`), signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken }, body: JSON.stringify({ publicKey, proof }) })
        if (!exact(status, ['state']) || status.state !== 'KeyEnrolled') return bad()
      }
      // The prefetched context (WO declaration, requirements, spare options) is retained sealed for offline use.
      await app.write(ctx.id, JSON.stringify({ context: ctx }), (await store.read(ctx.id))?.revision ?? null)
      return app
    } catch (error) { app.close(); throw error }
  }
  active() { return !this.closed && this.remaining() > 0 }
  remaining() { return Math.max(0, Math.min(Date.parse(this.context.expiresAt), Date.parse(this.context.retainUntil)) - Date.parse(this.context.serverTime) - (performance.now() - this.started)) }
  private requireActive() { if (!this.active()) throw new Error('reauthorization_required_data_retained') }
  private aad(commandId: string) {
    const c = this.context
    return { tenantId: c.tenantId, actorId: c.actorId, vesselId: c.vesselId, deviceId: c.deviceId, sessionBinding: `work-order/${c.workOrderId}/${c.id}`,
      policyVersion: `${c.policyVersion}/${c.policyDigest}/${c.authorityDigest}/${c.sourceVersion}`, commandId }
  }
  private async reserve() {
    this.requireActive()
    await vault<void>((s, done) => { const q = s.get(this.slot); q.onsuccess = () => { const row = q.result as KeySlot | undefined
      if (!row || row.invocations >= 4096) { s.transaction.abort(); return } s.put({ ...row, invocations: row.invocations + 1 }); done(undefined) } })
  }
  private async write(id: string, body: string, revision: number | null, outcome?: WorkOrderOfflineOutcome) {
    await this.reserve()
    const sealed = await sealOfflineRecord(crypto, this.keys.key, this.aad(id), new TextEncoder().encode(JSON.stringify({ body, ...(outcome ? { outcome } : {}) })), limits)
    this.requireActive(); await this.store.put(id, sealed, revision)
  }
  async captures(): Promise<WorkOrderPending[]> {
    this.requireActive(); const values: WorkOrderPending[] = []
    for (const row of await this.store.listRetained(128)) {
      if (row.commandId === this.context.id) continue
      const stored = await this.store.read(row.commandId); if (!stored) continue
      const value: unknown = JSON.parse(new TextDecoder().decode(await openOfflineRecord(crypto, this.keys.key, this.aad(row.commandId), stored.sealed, limits)))
      if (!exact(value, ['body'], ['outcome']) || typeof value.body !== 'string') return bad()
      const command = decodeWorkOrderCapture(JSON.parse(value.body), this.context, row.commandId)
      values.push({ commandId: row.commandId, revision: row.revision, body: value.body, ...(value.outcome === undefined ? {} : { outcome: decodeWorkOrderOutcome(value.outcome, this.context.workOrderId, command) }) })
    }
    return values
  }
  // Durable first: the frozen command is sealed in IndexedDB before any network attempt.
  async capture(evidence: Evidence | null, resolution?: { conflict: WorkOrderConflict; choice: 'keepServer' | 'useCaptured' }) {
    this.requireActive()
    if (resolution ? resolution.conflict.enrollmentId !== this.context.id || !resolution.conflict.choices.includes(resolution.choice) : !validEvidence(evidence)) return bad()
    const commandId = crypto.randomUUID(), c = this.context
    const body = JSON.stringify({ commandId, workOrderId: c.workOrderId, keyId: c.keyId, deviceId: c.deviceId, policyVersion: c.policyVersion, policyDigest: c.policyDigest,
      sourceVersion: c.sourceVersion, operation: c.operation, baseVersion: resolution?.conflict.currentVersion ?? c.baseVersion, capturedAt: new Date().toISOString(),
      offsetMinutes: -new Date().getTimezoneOffset(), evidence: resolution ? null : evidence,
      ...(resolution ? { parentCommandId: resolution.conflict.commandId, expectedServerVersion: resolution.conflict.currentVersion, choice: resolution.choice,
        reasonCode: resolution.choice === 'keepServer' ? 'CURRENT_SERVER_RETAINED' : 'INPUT_CORRECTED' } : {}) })
    decodeWorkOrderCapture(JSON.parse(body), c, commandId); await this.write(commandId, body, null); return commandId
  }
  async replay(capture: WorkOrderPending, signal: AbortSignal) {
    this.requireActive(); const stored = (await this.captures()).find(c => c.commandId === capture.commandId)
    if (!stored || stored.revision !== capture.revision || stored.body !== capture.body) return bad()
    const user = await session(signal)
    if (!user.authenticated || user.actorId !== this.context.actorId || user.tenantId !== this.context.tenantId || !user.csrfToken) { this.close(); throw new ApiError(403) }
    const bodyDigest = (await hash(stored.body)).toUpperCase(); await this.reserve()
    const signature = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, this.keys.signing.privateKey, new TextEncoder().encode(bodyDigest)))
    const value = decodeWorkOrderOutcome(await request<unknown>(workOrderOfflineRoot(this.context.vesselId, this.context.workOrderId, `/${this.context.id}/replay`), signal,
      { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': stored.commandId, 'X-Offline-Hash': bodyDigest, 'X-Offline-Signature': signature }, body: stored.body }),
      this.context.workOrderId, decodeWorkOrderCapture(JSON.parse(stored.body), this.context, stored.commandId))
    if (signal.aborted) throw new Error('response_withdrawn')
    await this.write(stored.commandId, stored.body, stored.revision, value); return value
  }
  // DEV-034 reconnect: recover lost replay receipts without re-sending; unproven captures stay pending.
  async reconcile(signal: AbortSignal): Promise<ReadonlyMap<string, SyncState>> {
    const pending = (await this.captures()).filter(row => !row.outcome).slice(0, maxSyncOperations), states = new Map<string, SyncState>()
    if (pending.length === 0) return states
    const refs = await Promise.all(pending.map(async row => ({ operationId: row.commandId, domain: 'WorkOrderEvidence' as const, resourceId: this.context.workOrderId, payloadDigest: await hash(row.body) })))
    const results = await reconcileOperations(this.context.vesselId, refs, signal)
    for (const [index, item] of results.entries()) {
      const row = pending[index]; states.set(row.commandId, item.state)
      if (item.state !== 'Recorded') continue
      await this.write(row.commandId, row.body, row.revision, decodeWorkOrderOutcome({ commandId: row.commandId, result: item.result, workOrderId: this.context.workOrderId, baseVersion: item.baseVersion,
        serverVersion: item.serverVersion, revision: item.revision, parentCommandId: item.parentCommandId, receivedAt: item.receivedAt }, this.context.workOrderId,
        decodeWorkOrderCapture(JSON.parse(row.body), this.context, row.commandId)))
    }
    return states
  }
  async conflict(commandId: string, signal: AbortSignal) {
    this.requireActive(); if (!isGuid(commandId)) return bad()
    return decodeWorkOrderConflict(await request<unknown>(workOrderOfflineRoot(this.context.vesselId, this.context.workOrderId, `/${this.context.id}/conflicts/${commandId}`), signal), this.context, commandId)
  }
  async discard(capture: WorkOrderPending, confirmed: boolean) {
    this.requireActive(); if (!confirmed) throw new Error('discard_confirmation_required')
    await this.store.remove(capture.commandId, capture.revision)
  }
  async revoke(lost: boolean, signal: AbortSignal) {
    const user = await session(signal); if (!user.authenticated || !user.csrfToken) throw new ApiError(401)
    await request(workOrderOfflineRoot(this.context.vesselId, this.context.workOrderId, `/${this.context.id}/${lost ? 'lost' : 'revoke'}`), signal, { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } })
    this.close()
  }
  close() { if (this.closed) return; this.closed = true; this.store.close() }
}
