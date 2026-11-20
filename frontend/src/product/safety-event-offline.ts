import { ApiError, request, session } from './api'
import { isGuid } from './identifiers'
import { base64, hash, vault, prepareOfflineShell, type KeySlot } from './offline-application'
import { openOfflineRecord, sealOfflineRecord } from './offline-cipher'
import { OfflineEncryptedRecordStore } from './offline-record-store'
import { offlineKeys as exact, offlineInteger as integer, offlineTime as utc } from './offline-capture-contract'
import type { DraftFields } from './safety-events-api'

// DEV-060 offline near-miss capture. Same protocol as the shared offline application (DEV-033..035): server-owned
// enrollment, nonextractable AES-GCM sealed IndexedDB records, P-256 key proof and signed idempotent replay. Only a
// NAMED draft is captured offline; confidential reports, submission, triage, investigation and closure stay online.
// "Queued" on this device is never "sent": the outcome is shown only after the server receipt.
export const safetyOfflineOperation = 'safety-event.capture.v1'
const bad = (): never => { throw new ApiError(502, 'invalid_safety_offline_contract') }
const digest = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
export type SafetyOfflineContext = { id: string; tenantId: string; actorId: string; vesselId: string; deviceId: string; keyId: string; challenge: string; policyVersion: string
  policyDigest: string; authorityDigest: string; sourceVersion: string; operation: string; allowedParts: string[]; categories: string[]; operationContexts: string[]; severities: string[]
  serverTime: string; expiresAt: string; retainUntil: string; maxRecords: number; maxEncryptedBytes: number; maxDeviceRecords: number; maxDeviceEncryptedBytes: number }
export type SafetyOfflineOutcome = { commandId: string; result: 'Committed'; eventId: string; eventNo: string; revision: number; receivedAt: string }
export type SafetyPending = { commandId: string; revision: number; body: string; outcome?: SafetyOfflineOutcome }

export function decodeSafetyOfflineContext(v: unknown, vessel: string): SafetyOfflineContext {
  if (!exact(v, ['id', 'tenantId', 'actorId', 'vesselId', 'deviceId', 'keyId', 'challenge', 'policyVersion', 'policyDigest', 'authorityDigest', 'sourceVersion', 'operation',
    'allowedParts', 'categories', 'operationContexts', 'severities', 'serverTime', 'expiresAt', 'retainUntil', 'maxRecords', 'maxEncryptedBytes', 'maxDeviceRecords', 'maxDeviceEncryptedBytes']) ||
    v.vesselId !== vessel || ['id', 'tenantId', 'actorId', 'deviceId', 'keyId'].some(k => !isGuid(v[k])) || typeof v.challenge !== 'string' || !/^[A-Za-z0-9+/]{43}=$/.test(v.challenge) ||
    typeof v.policyVersion !== 'string' || v.policyVersion.length < 1 || v.policyVersion.length > 80 || !digest(v.policyDigest) || !digest(v.authorityDigest) ||
    typeof v.sourceVersion !== 'string' || v.operation !== safetyOfflineOperation || !Array.isArray(v.allowedParts) || v.allowedParts.length !== 5 ||
    !Array.isArray(v.categories) || !Array.isArray(v.operationContexts) || !Array.isArray(v.severities) ||
    !utc(v.serverTime) || !utc(v.expiresAt) || !utc(v.retainUntil) || Date.parse(v.expiresAt) <= Date.parse(v.serverTime) || Date.parse(v.retainUntil) < Date.parse(v.expiresAt) ||
    !integer(v.maxRecords, 2, 128) || !integer(v.maxEncryptedBytes, 131072, 4194304) || !integer(v.maxDeviceRecords, v.maxRecords, 512) || !integer(v.maxDeviceEncryptedBytes, v.maxEncryptedBytes, 16777216)) return bad()
  return v as SafetyOfflineContext
}
export function decodeSafetyOutcome(v: unknown, commandId?: string): SafetyOfflineOutcome {
  if (!exact(v, ['commandId', 'result', 'eventId', 'eventNo', 'revision', 'receivedAt']) || !isGuid(v.commandId) || v.result !== 'Committed' || !isGuid(v.eventId) ||
    typeof v.eventNo !== 'string' || !integer(v.revision, 1, Number.MAX_SAFE_INTEGER) || !utc(v.receivedAt) || commandId !== undefined && v.commandId !== commandId) return bad()
  return v as SafetyOfflineOutcome
}
export const projectSafetyQueue = (rows: readonly SafetyPending[]) => ({ pending: rows.filter(r => !r.outcome).length, committed: rows.filter(r => !!r.outcome).map(r => r.outcome!) })

const limits = { plaintextBytes: 262144, contextBytes: 4096 }
const database = 'poseidon.offline.safety-event.v1'
export const safetyOfflineRoot = (vessel: string, suffix = '') => { if (!isGuid(vessel)) return bad(); return `safety-event-offline${suffix}?vesselId=${vessel}` }
const slotOf = (c: Pick<SafetyOfflineContext, 'tenantId' | 'actorId' | 'vesselId' | 'id' | 'deviceId' | 'keyId' | 'policyDigest' | 'authorityDigest' | 'sourceVersion'>) =>
  hash(JSON.stringify(['safety-event-capture-v1', c.tenantId, c.actorId, c.vesselId, c.id, c.deviceId, c.keyId, c.policyDigest, c.authorityDigest, c.sourceVersion]))

export class SafetyEventOfflineApplication {
  private closed = false
  private constructor(readonly context: SafetyOfflineContext, private keys: KeySlot, private slot: string, private store: OfflineEncryptedRecordStore, private started: number) {}
  static async authorize(vessel: string, enrollment: string | null, signal: AbortSignal) {
    const user = await session(signal)
    if (!user.authenticated || !isGuid(user.actorId) || !isGuid(user.tenantId) || !user.csrfToken || enrollment !== null && !isGuid(enrollment)) throw new ApiError(401)
    const started = performance.now()
    const ctx = decodeSafetyOfflineContext(await request<unknown>(safetyOfflineRoot(vessel, enrollment ? `/${enrollment}/renew` : ''), signal,
      { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } }), vessel)
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
    const app = new SafetyEventOfflineApplication(ctx, keys, slot, store, started)
    try {
      if (!enrollment) {
        const publicKey = base64(await crypto.subtle.exportKey('spki', keys.signing.publicKey))
        const proof = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, keys.signing.privateKey, new TextEncoder().encode(`safety-event/${vessel}/${ctx.id}/${ctx.keyId}/${ctx.challenge}`)))
        const status = await request<unknown>(safetyOfflineRoot(vessel, `/${ctx.id}/key`), signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken }, body: JSON.stringify({ publicKey, proof }) })
        if (!exact(status, ['state']) || status.state !== 'KeyEnrolled') return bad()
      }
      return app
    } catch (error) { app.close(); throw error }
  }
  active() { return !this.closed && this.remaining() > 0 }
  remaining() { return Math.max(0, Math.min(Date.parse(this.context.expiresAt), Date.parse(this.context.retainUntil)) - Date.parse(this.context.serverTime) - (performance.now() - this.started)) }
  private requireActive() { if (!this.active()) throw new Error('reauthorization_required_data_retained') }
  private aad(commandId: string) {
    const c = this.context
    return { tenantId: c.tenantId, actorId: c.actorId, vesselId: c.vesselId, deviceId: c.deviceId, sessionBinding: `safety-event/${c.vesselId}/${c.id}`,
      policyVersion: `${c.policyVersion}/${c.policyDigest}/${c.authorityDigest}/${c.sourceVersion}`, commandId }
  }
  private async reserve() {
    this.requireActive()
    await vault<void>((s, done) => { const q = s.get(this.slot); q.onsuccess = () => { const row = q.result as KeySlot | undefined
      if (!row || row.invocations >= 4096) { s.transaction.abort(); return } s.put({ ...row, invocations: row.invocations + 1 }); done(undefined) } })
  }
  private async write(id: string, body: string, revision: number | null, outcome?: SafetyOfflineOutcome) {
    await this.reserve()
    const sealed = await sealOfflineRecord(crypto, this.keys.key, this.aad(id), new TextEncoder().encode(JSON.stringify({ body, ...(outcome ? { outcome } : {}) })), limits)
    this.requireActive(); await this.store.put(id, sealed, revision)
  }
  async captures(): Promise<SafetyPending[]> {
    this.requireActive(); const values: SafetyPending[] = []
    for (const row of await this.store.listRetained(128)) {
      const stored = await this.store.read(row.commandId); if (!stored) continue
      const value: unknown = JSON.parse(new TextDecoder().decode(await openOfflineRecord(crypto, this.keys.key, this.aad(row.commandId), stored.sealed, limits)))
      if (!exact(value, ['body'], ['outcome']) || typeof value.body !== 'string') return bad()
      values.push({ commandId: row.commandId, revision: row.revision, body: value.body, ...(value.outcome === undefined ? {} : { outcome: decodeSafetyOutcome(value.outcome, row.commandId) }) })
    }
    return values
  }
  // Durable first: the frozen command is sealed in IndexedDB before any network attempt. Evidence files are online only.
  async capture(fields: DraftFields) {
    this.requireActive()
    const commandId = crypto.randomUUID(), c = this.context
    const body = JSON.stringify({ commandId, eventId: crypto.randomUUID(), keyId: c.keyId, deviceId: c.deviceId, policyVersion: c.policyVersion, policyDigest: c.policyDigest,
      sourceVersion: c.sourceVersion, operation: c.operation, capturedAt: new Date().toISOString(), offsetMinutes: -new Date().getTimezoneOffset(), fields: { ...fields, evidence: null } })
    await this.write(commandId, body, null); return commandId
  }
  async replay(capture: SafetyPending, signal: AbortSignal) {
    this.requireActive(); const stored = (await this.captures()).find(c => c.commandId === capture.commandId)
    if (!stored || stored.revision !== capture.revision || stored.body !== capture.body) return bad()
    const user = await session(signal)
    if (!user.authenticated || user.actorId !== this.context.actorId || user.tenantId !== this.context.tenantId || !user.csrfToken) { this.close(); throw new ApiError(403) }
    const bodyDigest = (await hash(stored.body)).toUpperCase(); await this.reserve()
    const signature = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, this.keys.signing.privateKey, new TextEncoder().encode(bodyDigest)))
    const value = decodeSafetyOutcome(await request<unknown>(safetyOfflineRoot(this.context.vesselId, `/${this.context.id}/replay`), signal,
      { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': stored.commandId, 'X-Offline-Hash': bodyDigest, 'X-Offline-Signature': signature }, body: stored.body }),
      stored.commandId)
    if (signal.aborted) throw new Error('response_withdrawn')
    await this.write(stored.commandId, stored.body, stored.revision, value); return value
  }
  async revoke(lost: boolean, signal: AbortSignal) {
    const user = await session(signal); if (!user.authenticated || !user.csrfToken) throw new ApiError(401)
    await request(safetyOfflineRoot(this.context.vesselId, `/${this.context.id}/${lost ? 'lost' : 'revoke'}`), signal, { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } })
    this.close()
  }
  close() { if (this.closed) return; this.closed = true; this.store.close() }
}
