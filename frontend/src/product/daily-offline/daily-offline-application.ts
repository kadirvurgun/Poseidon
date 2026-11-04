import { ApiError, request, session } from '../api'
import { isGuid } from '../identifiers'
import { base64, hash, vault, prepareOfflineShell, type KeySlot } from '../offline-application'
import { openOfflineRecord, sealOfflineRecord } from '../offline-cipher'
import { OfflineEncryptedRecordStore } from '../offline-record-store'
import { offlineKeys as exact } from '../offline-capture-contract'
import type { DailyKind, DailyFields } from '../daily-report-fields'
import { badDailyOffline as bad, decodeDailyOfflineContext, decodeDailyCapture, decodeDailyOutcome, decodeDailyConflict, utc,
  type DailyOfflineContext, type DailyPending, type DailyConflict, type DailyInventoryItem, type DailyOfflineOutcome } from './daily-offline-contract'

export const dailyCipherContext = (context: DailyOfflineContext, commandId: string) => ({
  tenantId: context.tenantId, actorId: context.actorId, vesselId: context.vesselId, deviceId: context.deviceId,
  sessionBinding: `daily/${context.kind}/${context.recordId}/${context.id}`,
  policyVersion: `${context.policyVersion}/${context.policyDigest}/${context.authorityDigest}/${context.sourceVersion}/${context.schemaHash}`, commandId,
})
const limits = { plaintextBytes: 131072, contextBytes: 4096 }
const database = 'poseidon.offline.daily.v1'
export function dailyOfflineRoot(kind: DailyKind, vessel: string, record: string, suffix = '') {
  if (!['Noon', 'Deck', 'Engine'].includes(kind) || !isGuid(vessel) || !isGuid(record)) return bad()
  return `daily-offline/${kind}/${record}${suffix}?vesselId=${vessel}`
}
const slotOf = (c: Pick<DailyOfflineContext, 'tenantId' | 'actorId' | 'vesselId' | 'kind' | 'recordId' | 'id' | 'deviceId' | 'keyId' | 'policyDigest' | 'authorityDigest' | 'sourceVersion' | 'schemaHash'>) =>
  hash(JSON.stringify(['daily-draft-v1', c.tenantId, c.actorId, c.vesselId, c.kind, c.recordId, c.id, c.deviceId, c.keyId, c.policyDigest, c.authorityDigest, c.sourceVersion, c.schemaHash]))
export async function dailyOfflineInventory(kind: DailyKind, vessel: string, record: string, signal: AbortSignal): Promise<DailyInventoryItem[]> {
  const value = await request<unknown>(dailyOfflineRoot(kind, vessel, record), signal)
  if (!exact(value, ['items']) || !Array.isArray(value.items) || value.items.length > 32 || value.items.some(e => !exact(e, ['id', 'expiresAt', 'retainUntil', 'revokedAt', 'policyVersion']) || !isGuid(e.id) || !utc(e.expiresAt) || !utc(e.retainUntil) || e.revokedAt !== null && !utc(e.revokedAt) || typeof e.policyVersion !== 'string') || new Set(value.items.map(e => e.id)).size !== value.items.length) return bad()
  return value.items as DailyInventoryItem[]
}
// Only explicitly authorized, in-memory handles can use retained nonextractable
// keys. A restart exposes no plaintext until current online renewal and key proof.
export class DailyOfflineApplication {
  private closed = false
  private channel: BroadcastChannel
  private withdrawn?: () => void
  private constructor(readonly context: DailyOfflineContext, private keys: KeySlot, private slot: string,
    private store: OfflineEncryptedRecordStore, private started: number, private boundary: string) {
    this.channel = new BroadcastChannel('poseidon.daily-offline.authorization.v1')
    this.channel.onmessage = e => { if (e.data?.boundary === boundary && e.data?.type === 'withdraw') { this.close(false); this.withdrawn?.() } }
    this.channel.postMessage({ boundary, type: 'withdraw' })
  }
  static async authorize(kind: DailyKind, vessel: string, record: string, enrollment: string | null, signal: AbortSignal) {
    const user = await session(signal); if (!user.authenticated || !isGuid(user.actorId) || !isGuid(user.tenantId) || !user.csrfToken || enrollment !== null && !isGuid(enrollment)) throw new ApiError(401)
    const started = performance.now()
    const ctx = decodeDailyOfflineContext(await request<unknown>(dailyOfflineRoot(kind, vessel, record, enrollment ? `/${enrollment}/renew` : ''), signal,
      { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } }), kind, vessel, record)
    if (ctx.actorId !== user.actorId || ctx.tenantId !== user.tenantId || signal.aborted) throw new ApiError(403)
    // Prepare only the public static shell, before any local key/ciphertext
    // access. This never activates a new worker forcibly or proves an older
    // already-active worker implements this build's daily route contract.
    await prepareOfflineShell(signal)
    if (signal.aborted) throw new Error('authorization_withdrawn')
    const slot = await slotOf(ctx)
    let keys = await vault<KeySlot | null>((s, done) => { const q = s.get(slot); q.onsuccess = () => done(q.result ?? null) })
    if (!keys && enrollment) throw new Error('key_lost_data_retained')
    if (!keys) {
      const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
      const signing = await crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign', 'verify'])
      keys = await vault<KeySlot>((s, done) => {
        const q = s.get(slot); q.onsuccess = () => { if (q.result) { done(q.result); return } const n = s.count(); n.onsuccess = () => {
          if (n.result >= 512) { s.transaction.abort(); return } const row = { id: slot, key, signing, invocations: 0 }; s.add(row); done(row)
        } }
      })
    }
    if (!keys.key || keys.key.extractable || keys.key.type !== 'secret' || keys.key.algorithm.name !== 'AES-GCM' ||
      !keys.signing?.privateKey || keys.signing.privateKey.extractable || keys.signing.privateKey.type !== 'private' || keys.signing.privateKey.algorithm.name !== 'ECDSA' || (keys.signing.privateKey.algorithm as EcKeyAlgorithm).namedCurve !== 'P-256') throw new Error('key_lost_data_retained')
    const store = await OfflineEncryptedRecordStore.open(indexedDB, slot, { maxRecords: ctx.maxRecords, maxEncryptedBytes: ctx.maxEncryptedBytes, maxDeviceRecords: ctx.maxDeviceRecords, maxDeviceEncryptedBytes: ctx.maxDeviceEncryptedBytes }, database)
    const app = new DailyOfflineApplication(ctx, keys, slot, store, started, await hash(JSON.stringify(['daily', ctx.tenantId, ctx.actorId, vessel, kind, record])))
    try {
      await app.reserve()
      const publicKey = base64(await crypto.subtle.exportKey('spki', keys.signing.publicKey))
      const proof = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, keys.signing.privateKey, new TextEncoder().encode(`daily/${kind}/${record}/${ctx.id}/${ctx.keyId}/${ctx.challenge}`)))
      const status = await request<unknown>(dailyOfflineRoot(kind, vessel, record, `/${ctx.id}/key`), signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken }, body: JSON.stringify({ publicKey, proof }) })
      if (!exact(status, ['state']) || status.state !== 'KeyEnrolled') return bad()
      if (signal.aborted) throw new Error('authorization_withdrawn')
      if (!await store.read(ctx.id)) await app.write(ctx.id, JSON.stringify({ baseVersion: ctx.baseVersion, baseRevision: ctx.baseRevision, fields: ctx.baseFields }), null)
      app.requireActive(); return app
    } catch (error) { app.close(); throw error }
  }
  active() { return !this.closed && performance.now() >= this.started && this.remaining() > 0 }
  remaining() { return Math.max(0, Math.min(Date.parse(this.context.expiresAt), Date.parse(this.context.retainUntil)) - Date.parse(this.context.serverTime) - (performance.now() - this.started)) }
  private requireActive() { if (!this.active()) throw new Error('reauthorization_required_data_retained') }
  private aad(commandId: string) { return dailyCipherContext(this.context, commandId) }
  private async reserve() {
    this.requireActive()
    await vault<void>((s, done) => { const q = s.get(this.slot); q.onsuccess = () => { const row = q.result as KeySlot | undefined
      if (!row || row.invocations >= 1024) { s.transaction.abort(); return } s.put({ ...row, invocations: row.invocations + 1 }); done(undefined)
    } })
    this.requireActive()
  }
  private async write(id: string, body: string, revision: number | null, outcome?: DailyOfflineOutcome) {
    await this.reserve()
    const sealed = await sealOfflineRecord(crypto, this.keys.key, this.aad(id), new TextEncoder().encode(JSON.stringify({ body, ...(outcome ? { outcome } : {}) })), limits)
    this.requireActive(); await this.store.put(id, sealed, revision)
  }
  async captures(): Promise<DailyPending[]> {
    this.requireActive(); const values: DailyPending[] = []
    // Include every retained record at the protocol ceiling even if a later local
    // write quota is lower. Never hide pending envelopes behind a first page.
    for (const row of await this.store.listRetained(128)) {
      if (row.commandId === this.context.id) continue
      const stored = await this.store.read(row.commandId); if (!stored) continue
      const value: unknown = JSON.parse(new TextDecoder().decode(await openOfflineRecord(crypto, this.keys.key, this.aad(row.commandId), stored.sealed, limits)))
      if (!exact(value, ['body'], ['outcome']) || typeof value.body !== 'string') return bad()
      const command = decodeDailyCapture(JSON.parse(value.body), this.context, row.commandId)
      values.push({ commandId: row.commandId, revision: row.revision, body: value.body, ...(value.outcome === undefined ? {} : { outcome: decodeDailyOutcome(value.outcome, this.context, command) }) })
      this.requireActive()
    }
    this.requireActive(); return values
  }
  async capture(fields: DailyFields, resolution?: { conflict: DailyConflict; choice: 'keepServer' | 'useCaptured' }) {
    this.requireActive()
    if (resolution && (resolution.conflict.enrollmentId !== this.context.id || resolution.conflict.recordId !== this.context.recordId || resolution.conflict.kind !== this.context.kind || !resolution.conflict.choices.includes(resolution.choice))) return bad()
    const commandId = crypto.randomUUID()
    const selected = resolution ? {} : Object.fromEntries(this.context.allowedFields.map(key => [key, fields[key] ?? { state: 'Missing', value: null }]))
    const body = JSON.stringify({ commandId, recordId: this.context.recordId, kind: this.context.kind, keyId: this.context.keyId, deviceId: this.context.deviceId,
      policyVersion: this.context.policyVersion, policyDigest: this.context.policyDigest, sourceVersion: this.context.sourceVersion, schemaHash: this.context.schemaHash, operation: this.context.operation,
      baseVersion: resolution?.conflict.currentVersion ?? this.context.baseVersion, capturedAt: new Date().toISOString(), offsetMinutes: -new Date().getTimezoneOffset(), fields: selected,
      ...(resolution ? { parentCommandId: resolution.conflict.commandId, expectedServerVersion: resolution.conflict.currentVersion, choice: resolution.choice, reasonCode: resolution.choice === 'keepServer' ? 'CURRENT_SERVER_RETAINED' : 'INPUT_CORRECTED' } : {}) })
    decodeDailyCapture(JSON.parse(body), this.context, commandId); await this.write(commandId, body, null); return this.captures()
  }
  async replay(capture: DailyPending, signal: AbortSignal) {
    this.requireActive(); const stored = (await this.captures()).find(c => c.commandId === capture.commandId)
    if (!stored || stored.revision !== capture.revision || stored.body !== capture.body) return bad()
    const user = await session(signal)
    if (!user.authenticated || user.actorId !== this.context.actorId || user.tenantId !== this.context.tenantId || !user.csrfToken) { this.close(); throw new ApiError(403) }
    const digest = (await hash(stored.body)).toUpperCase(); await this.reserve()
    const signature = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, this.keys.signing.privateKey, new TextEncoder().encode(digest)))
    this.requireActive()
    const value = decodeDailyOutcome(await request<unknown>(dailyOfflineRoot(this.context.kind, this.context.vesselId, this.context.recordId, `/${this.context.id}/replay`), signal,
      { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': stored.commandId, 'X-Offline-Hash': digest, 'X-Offline-Signature': signature }, body: stored.body }), this.context, decodeDailyCapture(JSON.parse(stored.body), this.context, stored.commandId))
    this.requireActive(); if (signal.aborted) throw new Error('response_withdrawn')
    await this.write(stored.commandId, stored.body, stored.revision, value); return value
  }
  async conflict(commandId: string, signal: AbortSignal) {
    this.requireActive(); if (!isGuid(commandId)) return bad()
    const value = decodeDailyConflict(await request<unknown>(dailyOfflineRoot(this.context.kind, this.context.vesselId, this.context.recordId, `/${this.context.id}/conflicts/${commandId}`), signal), this.context, commandId)
    this.requireActive(); if (signal.aborted) throw new Error('response_withdrawn'); return value
  }
  async discard(capture: DailyPending, confirmed: boolean) {
    this.requireActive(); if (!confirmed) throw new Error('discard_confirmation_required')
    await this.store.remove(capture.commandId, capture.revision)
  }
  async revoke(lost: boolean, signal: AbortSignal) {
    await dailyOfflineLifecycle(this.context.kind, this.context.vesselId, this.context.recordId, this.context.id, lost ? 'lost' : 'revoke', signal)
    this.close()
  }
  onWithdraw(callback: () => void) { this.withdrawn = callback }
  close(announce = true) {
    if (this.closed) return
    this.closed = true; this.store.close(); if (announce) this.channel.postMessage({ boundary: this.boundary, type: 'withdraw' }); this.channel.close()
  }
}
export async function dailyOfflineLifecycle(kind: DailyKind, vessel: string, record: string, enrollment: string, action: 'lost' | 'revoke', signal: AbortSignal) {
  if (!isGuid(enrollment)) return bad()
  const user = await session(signal); if (!user.authenticated || !user.csrfToken) throw new ApiError(401)
  return request(dailyOfflineRoot(kind, vessel, record, `/${enrollment}/${action}`), signal, { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } })
}
export async function discardDailyLostCaptures(kind: DailyKind, vessel: string, record: string, enrollment: string, confirmed: boolean, signal: AbortSignal) {
  if (!confirmed || !isGuid(enrollment)) throw new Error('discard_confirmation_required')
  const user = await session(signal); if (!user.authenticated || !user.csrfToken) throw new ApiError(401)
  const value = await request<unknown>(dailyOfflineRoot(kind, vessel, record, `/${enrollment}/cleanup`), signal, { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } })
  if (!exact(value, ['id', 'tenantId', 'actorId', 'vesselId', 'kind', 'recordId', 'deviceId', 'keyId', 'policyVersion', 'policyDigest', 'authorityDigest', 'sourceVersion', 'schemaHash']) || value.id !== enrollment || value.actorId !== user.actorId || value.tenantId !== user.tenantId || value.vesselId !== vessel || value.recordId !== record || value.kind !== kind || ['id', 'tenantId', 'actorId', 'deviceId', 'keyId'].some(k => !isGuid(value[k])) || typeof value.policyVersion !== 'string' || typeof value.policyDigest !== 'string' || typeof value.authorityDigest !== 'string' || typeof value.sourceVersion !== 'string' || typeof value.schemaHash !== 'string') return bad()
  if (signal.aborted) throw new Error('authorization_withdrawn')
  const slot = await slotOf(value as DailyOfflineContext)
  await dailyOfflineLifecycle(kind, vessel, record, enrollment, 'lost', signal)
  const channel = new BroadcastChannel('poseidon.daily-offline.authorization.v1')
  channel.postMessage({ boundary: await hash(JSON.stringify(['daily', user.tenantId, user.actorId, vessel, kind, record])), type: 'withdraw' }); channel.close()
  const store = await OfflineEncryptedRecordStore.open(indexedDB, slot, { maxRecords: 128, maxEncryptedBytes: 1048576, maxDeviceRecords: 512, maxDeviceEncryptedBytes: 4194304 }, database)
  try {
    for (const row of await store.listRetained(128)) { if (signal.aborted) throw new Error('authorization_withdrawn'); await store.remove(row.commandId, row.revision) }
    await vault<void>((keys, done) => { keys.delete(slot); done(undefined) })
  } finally { store.close() }
}
