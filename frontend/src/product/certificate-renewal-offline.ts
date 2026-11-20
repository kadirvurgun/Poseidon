import { ApiError, request, session } from './api'
import { isGuid } from './identifiers'
import { base64, hash, vault, prepareOfflineShell, type KeySlot } from './offline-application'
import { openOfflineRecord, sealOfflineRecord } from './offline-cipher'
import { OfflineEncryptedRecordStore } from './offline-record-store'
import { offlineKeys as exact, offlineInteger as integer, offlineTime as utc } from './offline-capture-contract'
import { decodeRenewalContent } from './certificates-api'
import type { CertificateRenewalContent } from './certificate-content'

// S10 WP-C DEV-066 (18.01 sheet:Backlog/row:67) pilot offline journey flow 1: certificate renewal draft.
// Same protocol as the shared offline application (DEV-033..035) and the DEV-057 work order namespace:
// server-owned enrollment, nonextractable AES-GCM sealed IndexedDB records, P-256 key proof and signed
// idempotent replay. Only DRAFT/PREPARING metadata is authored offline; replacement certificate/document
// links, transitions, verification and completion stay online. "Queued" is never "saved".
export const renewalOfflineOperation = 'certificate-renewal.draft.v1'
const bad = (): never => { throw new ApiError(502, 'invalid_renewal_offline_contract') }
const digest = (v: unknown) => typeof v === 'string' && /^[A-F0-9]{64}$/.test(v)
const editableStates = ['DRAFT', 'PREPARING'] as const
export type RenewalOfflineContext = { id: string; tenantId: string; actorId: string; vesselId: string; certificateId: string; renewalId: string; deviceId: string; keyId: string
  challenge: string; policyVersion: string; policyDigest: string; authorityDigest: string; sourceVersion: string; operation: string; allowedParts: string[]; baseVersion: number
  baseRevision: number; baseContent: CertificateRenewalContent; state: typeof editableStates[number] | string; ownerOptions: string[]; serverTime: string; expiresAt: string; retainUntil: string
  maxRecords: number; maxEncryptedBytes: number; maxDeviceRecords: number; maxDeviceEncryptedBytes: number }
export type RenewalOfflineOutcome = { commandId: string; result: 'Committed' | 'MaterialConflict' | 'ServerRetained'; renewalId: string; baseVersion: number; serverVersion: number
  revision: number; parentCommandId: string | null; receivedAt: string }
export type RenewalPending = { commandId: string; revision: number; body: string; outcome?: RenewalOfflineOutcome }
export type RenewalConflict = { commandId: string; enrollmentId: string; renewalId: string; currentVersion: number; state: string; base: CertificateRenewalContent
  captured: CertificateRenewalContent; server: CertificateRenewalContent; choices: ('keepServer' | 'useCaptured')[]; resolved: RenewalOfflineOutcome | null }

// Linked references are verified online only; an offline draft keeps exactly the base links.
export function sameRenewalLinks(a: CertificateRenewalContent, b: CertificateRenewalContent) {
  return a.newCertificateId === b.newCertificateId && a.newCertificateRevision === b.newCertificateRevision && a.newCertificateVersion === b.newCertificateVersion &&
    JSON.stringify(a.newDocument) === JSON.stringify(b.newDocument)
}
export function decodeRenewalOfflineContext(v: unknown, vessel: string, renewal: string): RenewalOfflineContext {
  if (!exact(v, ['id', 'tenantId', 'actorId', 'vesselId', 'certificateId', 'renewalId', 'deviceId', 'keyId', 'challenge', 'policyVersion', 'policyDigest', 'authorityDigest',
    'sourceVersion', 'operation', 'allowedParts', 'baseVersion', 'baseRevision', 'baseContent', 'state', 'ownerOptions', 'serverTime', 'expiresAt', 'retainUntil', 'maxRecords',
    'maxEncryptedBytes', 'maxDeviceRecords', 'maxDeviceEncryptedBytes']) || v.vesselId !== vessel || v.renewalId !== renewal ||
    ['id', 'tenantId', 'actorId', 'deviceId', 'keyId', 'certificateId'].some(k => !isGuid(v[k])) || typeof v.challenge !== 'string' || !/^[A-Za-z0-9+/]{43}=$/.test(v.challenge) ||
    typeof v.policyVersion !== 'string' || v.policyVersion.length < 1 || v.policyVersion.length > 80 || !digest(v.policyDigest) || !digest(v.authorityDigest) ||
    typeof v.sourceVersion !== 'string' || v.operation !== renewalOfflineOperation || !Array.isArray(v.allowedParts) || v.allowedParts.length !== 5 ||
    !integer(v.baseVersion, 1, 4294967295) || !integer(v.baseRevision, 1, Number.MAX_SAFE_INTEGER) || typeof v.state !== 'string' || !(editableStates as readonly string[]).includes(v.state) ||
    !Array.isArray(v.ownerOptions) || v.ownerOptions.length > 256 || v.ownerOptions.some(o => !isGuid(o)) ||
    !utc(v.serverTime) || !utc(v.expiresAt) || !utc(v.retainUntil) || Date.parse(v.expiresAt) <= Date.parse(v.serverTime) || Date.parse(v.retainUntil) < Date.parse(v.expiresAt) ||
    !integer(v.maxRecords, 2, 128) || !integer(v.maxEncryptedBytes, 131072, 4194304) || !integer(v.maxDeviceRecords, v.maxRecords, 512) || !integer(v.maxDeviceEncryptedBytes, v.maxEncryptedBytes, 16777216)) return bad()
  return { ...(v as RenewalOfflineContext), baseContent: decodeRenewalContent(v.baseContent) }
}
type Capture = { commandId: string; renewalId: string; keyId: string; deviceId: string; policyVersion: string; policyDigest: string; sourceVersion: string; operation: string
  baseVersion: number; capturedAt: string; offsetMinutes: number; content: CertificateRenewalContent | null; parentCommandId?: string; expectedServerVersion?: number
  choice?: 'keepServer' | 'useCaptured'; reasonCode?: 'CURRENT_SERVER_RETAINED' | 'INPUT_CORRECTED' }
export function decodeRenewalCapture(v: unknown, c: RenewalOfflineContext, id: string): Capture {
  if (!exact(v, ['commandId', 'renewalId', 'keyId', 'deviceId', 'policyVersion', 'policyDigest', 'sourceVersion', 'operation', 'baseVersion', 'capturedAt', 'offsetMinutes', 'content'],
    ['parentCommandId', 'expectedServerVersion', 'choice', 'reasonCode']) || v.commandId !== id || !isGuid(id) || v.renewalId !== c.renewalId || v.keyId !== c.keyId ||
    v.deviceId !== c.deviceId || v.policyVersion !== c.policyVersion || v.policyDigest !== c.policyDigest || v.sourceVersion !== c.sourceVersion || v.operation !== c.operation ||
    !utc(v.capturedAt) || !integer(v.offsetMinutes, -840, 840)) return bad()
  if (v.parentCommandId === undefined) {
    if (v.baseVersion !== c.baseVersion || ['expectedServerVersion', 'choice', 'reasonCode'].some(k => Object.hasOwn(v, k)) || !sameRenewalLinks(decodeRenewalContent(v.content), c.baseContent)) return bad()
  } else if (!isGuid(v.parentCommandId) || v.parentCommandId === id || !integer(v.baseVersion, 1, 4294967295) || v.expectedServerVersion !== v.baseVersion || v.content !== null ||
    !['keepServer', 'useCaptured'].includes(String(v.choice)) || v.reasonCode !== (v.choice === 'keepServer' ? 'CURRENT_SERVER_RETAINED' : 'INPUT_CORRECTED')) return bad()
  return v as Capture
}
export function decodeRenewalOutcome(v: unknown, renewal: string, capture?: Capture): RenewalOfflineOutcome {
  if (!exact(v, ['commandId', 'result', 'renewalId', 'baseVersion', 'serverVersion', 'revision', 'parentCommandId', 'receivedAt']) || !isGuid(v.commandId) || v.renewalId !== renewal ||
    !['Committed', 'MaterialConflict', 'ServerRetained'].includes(String(v.result)) || !integer(v.baseVersion, 1, 4294967295) || !integer(v.serverVersion, 1, 4294967295) ||
    !integer(v.revision, 1, Number.MAX_SAFE_INTEGER) || v.result === 'ServerRetained' && v.parentCommandId === null || v.parentCommandId !== null && !isGuid(v.parentCommandId) || !utc(v.receivedAt) ||
    capture && (v.commandId !== capture.commandId || v.baseVersion !== capture.baseVersion || v.parentCommandId !== (capture.parentCommandId ?? null))) return bad()
  return v as RenewalOfflineOutcome
}
export function decodeRenewalConflict(v: unknown, c: RenewalOfflineContext, id: string): RenewalConflict {
  if (!exact(v, ['commandId', 'enrollmentId', 'renewalId', 'currentVersion', 'state', 'base', 'captured', 'server', 'choices', 'resolved']) || v.commandId !== id || v.enrollmentId !== c.id ||
    v.renewalId !== c.renewalId || !integer(v.currentVersion, 1, 4294967295) || typeof v.state !== 'string' || !Array.isArray(v.choices) || v.choices.length > 2 ||
    v.choices.some(x => !['keepServer', 'useCaptured'].includes(String(x))) || new Set(v.choices).size !== v.choices.length || v.resolved !== null && v.choices.length !== 0) return bad()
  const value = { ...(v as RenewalConflict), base: decodeRenewalContent(v.base), captured: decodeRenewalContent(v.captured), server: decodeRenewalContent(v.server) }
  if (v.resolved !== null) { const resolved = decodeRenewalOutcome(v.resolved, c.renewalId); if (resolved.parentCommandId !== id || resolved.result === 'MaterialConflict') return bad() }
  return value
}
// Pending = captured and not yet terminally recorded. A conflict stays pending until a child records Committed/ServerRetained.
export function projectRenewalQueue(rows: readonly RenewalPending[]) {
  const resolved = new Set(rows.flatMap(row => { try { const body = JSON.parse(row.body) as { parentCommandId?: string }
    return row.outcome && row.outcome.result !== 'MaterialConflict' && body.parentCommandId ? [body.parentCommandId] : [] } catch { return [] } }))
  return { resolved, pending: rows.filter(row => !row.outcome || row.outcome.result === 'MaterialConflict' && !resolved.has(row.commandId)).length,
    conflicts: rows.filter(row => row.outcome?.result === 'MaterialConflict' && !resolved.has(row.commandId)).map(row => row.commandId),
    committed: rows.filter(row => row.outcome && row.outcome.result !== 'MaterialConflict').length }
}

const limits = { plaintextBytes: 262144, contextBytes: 4096 }
const database = 'poseidon.offline.certificate-renewal.v1'
export const renewalOfflineRoot = (vessel: string, renewal: string, suffix = '') => {
  if (!isGuid(vessel) || !isGuid(renewal)) return bad()
  return `certificate-renewal-offline/${renewal}${suffix}?vesselId=${vessel}`
}
const slotOf = (c: Pick<RenewalOfflineContext, 'tenantId' | 'actorId' | 'vesselId' | 'renewalId' | 'id' | 'deviceId' | 'keyId' | 'policyDigest' | 'authorityDigest' | 'sourceVersion'>) =>
  hash(JSON.stringify(['certificate-renewal-draft-v1', c.tenantId, c.actorId, c.vesselId, c.renewalId, c.id, c.deviceId, c.keyId, c.policyDigest, c.authorityDigest, c.sourceVersion]))

export class RenewalOfflineApplication {
  private closed = false
  private constructor(readonly context: RenewalOfflineContext, private keys: KeySlot, private slot: string, private store: OfflineEncryptedRecordStore, private started: number) {}
  static async authorize(vessel: string, renewal: string, enrollment: string | null, signal: AbortSignal) {
    const user = await session(signal)
    if (!user.authenticated || !isGuid(user.actorId) || !isGuid(user.tenantId) || !user.csrfToken || enrollment !== null && !isGuid(enrollment)) throw new ApiError(401)
    const started = performance.now()
    const ctx = decodeRenewalOfflineContext(await request<unknown>(renewalOfflineRoot(vessel, renewal, enrollment ? `/${enrollment}/renew` : ''), signal,
      { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } }), vessel, renewal)
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
    const app = new RenewalOfflineApplication(ctx, keys, slot, store, started)
    try {
      if (!enrollment) {
        const publicKey = base64(await crypto.subtle.exportKey('spki', keys.signing.publicKey))
        const proof = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, keys.signing.privateKey, new TextEncoder().encode(`certificate-renewal/${renewal}/${ctx.id}/${ctx.keyId}/${ctx.challenge}`)))
        const status = await request<unknown>(renewalOfflineRoot(vessel, renewal, `/${ctx.id}/key`), signal, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken }, body: JSON.stringify({ publicKey, proof }) })
        if (!exact(status, ['state']) || status.state !== 'KeyEnrolled') return bad()
      }
      // The prefetched renewal context is retained sealed for offline authoring.
      await app.write(ctx.id, JSON.stringify({ context: ctx }), (await store.read(ctx.id))?.revision ?? null)
      return app
    } catch (error) { app.close(); throw error }
  }
  active() { return !this.closed && this.remaining() > 0 }
  remaining() { return Math.max(0, Math.min(Date.parse(this.context.expiresAt), Date.parse(this.context.retainUntil)) - Date.parse(this.context.serverTime) - (performance.now() - this.started)) }
  private requireActive() { if (!this.active()) throw new Error('reauthorization_required_data_retained') }
  private aad(commandId: string) {
    const c = this.context
    return { tenantId: c.tenantId, actorId: c.actorId, vesselId: c.vesselId, deviceId: c.deviceId, sessionBinding: `certificate-renewal/${c.renewalId}/${c.id}`,
      policyVersion: `${c.policyVersion}/${c.policyDigest}/${c.authorityDigest}/${c.sourceVersion}`, commandId }
  }
  private async reserve() {
    this.requireActive()
    await vault<void>((s, done) => { const q = s.get(this.slot); q.onsuccess = () => { const row = q.result as KeySlot | undefined
      if (!row || row.invocations >= 4096) { s.transaction.abort(); return } s.put({ ...row, invocations: row.invocations + 1 }); done(undefined) } })
  }
  private async write(id: string, body: string, revision: number | null, outcome?: RenewalOfflineOutcome) {
    await this.reserve()
    const sealed = await sealOfflineRecord(crypto, this.keys.key, this.aad(id), new TextEncoder().encode(JSON.stringify({ body, ...(outcome ? { outcome } : {}) })), limits)
    this.requireActive(); await this.store.put(id, sealed, revision)
  }
  async captures(): Promise<RenewalPending[]> {
    this.requireActive(); const values: RenewalPending[] = []
    for (const row of await this.store.listRetained(128)) {
      if (row.commandId === this.context.id) continue
      const stored = await this.store.read(row.commandId); if (!stored) continue
      const value: unknown = JSON.parse(new TextDecoder().decode(await openOfflineRecord(crypto, this.keys.key, this.aad(row.commandId), stored.sealed, limits)))
      if (!exact(value, ['body'], ['outcome']) || typeof value.body !== 'string') return bad()
      const command = decodeRenewalCapture(JSON.parse(value.body), this.context, row.commandId)
      values.push({ commandId: row.commandId, revision: row.revision, body: value.body, ...(value.outcome === undefined ? {} : { outcome: decodeRenewalOutcome(value.outcome, this.context.renewalId, command) }) })
    }
    return values
  }
  // Durable first: the frozen command is sealed in IndexedDB before any network attempt.
  async capture(content: CertificateRenewalContent | null, resolution?: { conflict: RenewalConflict; choice: 'keepServer' | 'useCaptured' }) {
    this.requireActive()
    if (resolution ? resolution.conflict.enrollmentId !== this.context.id || !resolution.conflict.choices.includes(resolution.choice) : !content || !sameRenewalLinks(content, this.context.baseContent)) return bad()
    const commandId = crypto.randomUUID(), c = this.context
    const body = JSON.stringify({ commandId, renewalId: c.renewalId, keyId: c.keyId, deviceId: c.deviceId, policyVersion: c.policyVersion, policyDigest: c.policyDigest,
      sourceVersion: c.sourceVersion, operation: c.operation, baseVersion: resolution?.conflict.currentVersion ?? c.baseVersion, capturedAt: new Date().toISOString(),
      offsetMinutes: -new Date().getTimezoneOffset(), content: resolution ? null : content,
      ...(resolution ? { parentCommandId: resolution.conflict.commandId, expectedServerVersion: resolution.conflict.currentVersion, choice: resolution.choice,
        reasonCode: resolution.choice === 'keepServer' ? 'CURRENT_SERVER_RETAINED' : 'INPUT_CORRECTED' } : {}) })
    decodeRenewalCapture(JSON.parse(body), c, commandId); await this.write(commandId, body, null); return commandId
  }
  async replay(capture: RenewalPending, signal: AbortSignal) {
    this.requireActive(); const stored = (await this.captures()).find(c => c.commandId === capture.commandId)
    if (!stored || stored.revision !== capture.revision || stored.body !== capture.body) return bad()
    const user = await session(signal)
    if (!user.authenticated || user.actorId !== this.context.actorId || user.tenantId !== this.context.tenantId || !user.csrfToken) { this.close(); throw new ApiError(403) }
    const bodyDigest = (await hash(stored.body)).toUpperCase(); await this.reserve()
    const signature = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, this.keys.signing.privateKey, new TextEncoder().encode(bodyDigest)))
    const value = decodeRenewalOutcome(await request<unknown>(renewalOfflineRoot(this.context.vesselId, this.context.renewalId, `/${this.context.id}/replay`), signal,
      { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken, 'Idempotency-Key': stored.commandId, 'X-Offline-Hash': bodyDigest, 'X-Offline-Signature': signature }, body: stored.body }),
      this.context.renewalId, decodeRenewalCapture(JSON.parse(stored.body), this.context, stored.commandId))
    if (signal.aborted) throw new Error('response_withdrawn')
    await this.write(stored.commandId, stored.body, stored.revision, value); return value
  }
  async conflict(commandId: string, signal: AbortSignal) {
    this.requireActive(); if (!isGuid(commandId)) return bad()
    return decodeRenewalConflict(await request<unknown>(renewalOfflineRoot(this.context.vesselId, this.context.renewalId, `/${this.context.id}/conflicts/${commandId}`), signal), this.context, commandId)
  }
  async discard(capture: RenewalPending, confirmed: boolean) {
    this.requireActive(); if (!confirmed) throw new Error('discard_confirmation_required')
    await this.store.remove(capture.commandId, capture.revision)
  }
  async revoke(lost: boolean, signal: AbortSignal) {
    const user = await session(signal); if (!user.authenticated || !user.csrfToken) throw new ApiError(401)
    await request(renewalOfflineRoot(this.context.vesselId, this.context.renewalId, `/${this.context.id}/${lost ? 'lost' : 'revoke'}`), signal, { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } })
    this.close()
  }
  close() { if (this.closed) return; this.closed = true; this.store.close() }
}
