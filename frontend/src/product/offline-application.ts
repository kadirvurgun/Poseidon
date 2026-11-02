import { ApiError, request, session } from './api'
import { isGuid } from './identifiers'
import { openOfflineRecord, sealOfflineRecord, type OfflineCipherContext } from './offline-cipher'
import { OfflineEncryptedRecordStore } from './offline-record-store'
import type { OfflineConflict, OfflineConflictResolution } from './OfflineConflictReview'
import { decodeOfflineCapture, decodeOfflineSnapshot, offlineKeys, offlineOperation as operation, offlineSource as source,
  offlineInteger as integer, offlineTime as time, invalidOffline as bad, type OfflineCaptureBody } from './offline-capture-contract'

// Hand-authored transport: locked OpenAPI generation remains deferred to the bundle gate.
export type OfflineContext = {
  id: string; tenantId: string; actorId: string; vesselId: string; assignmentId: string; deviceId: string; keyId: string
  keyChallenge: string; sessionBinding: string; policyVersion: string; sourceVersion: string; baseVersion: number; baseRevision: number
  baseWatchGroup: string | null; watchGroups: string[]; serverTime: string; expiresAt: string
  maxRecords: number; maxEncryptedBytes: number; maxDeviceRecords: number; maxDeviceEncryptedBytes: number
}
export type OfflineOutcome = { commandId: string; result: 'Committed' | 'MaterialConflict'; baseVersion: number
  serverVersion: number; revision: number; capturedAt: string; offsetMinutes: number; receivedAt: string; parentCommandId: string | null }
export type OfflineInventory = { items: { id: string; revokedAt: string | null; expiresAt: string; policyVersion: string }[] }
export type OfflineHistory = { items: OfflineOutcome[]; offset: number; hasMore: boolean }
export type OfflineConflictDetail = OfflineConflict & { base: { kind: string; text?: string }; currentVersion: number; resolved: OfflineOutcome | null
  resolvedValue?: { kind: string; text?: string } | null; resolutionReason?: string | null; resolvedBy?: string | null }
export type PendingCapture = { commandId: string; revision: number; body: string; outcome?: OfflineOutcome }
const limits = { plaintextBytes: 4096, contextBytes: 2048 }
export async function prepareOfflineShell(signal: AbortSignal) {
  if (!('serviceWorker' in navigator)) throw new Error('offline_restart_unavailable')
  const registration = await navigator.serviceWorker.register('/offline-shell.js', { scope: '/app/', updateViaCache: 'none' })
  if (signal.aborted) throw new Error('authorization_withdrawn')
  if (registration.active) return
  await new Promise<void>((resolve, reject) => {
    const worker = registration.installing ?? registration.waiting
    if (!worker) { reject(new Error('offline_restart_unavailable')); return }
    const finish = () => {
      if (worker.state === 'activated') { cleanup(); resolve() }
      else if (worker.state === 'redundant' || signal.aborted) { cleanup(); reject(new Error('offline_restart_unavailable')) }
    }
    const timeout = setTimeout(() => { cleanup(); reject(new Error('offline_restart_unavailable')) }, 15000)
    const cleanup = () => { clearTimeout(timeout); worker.removeEventListener('statechange', finish); signal.removeEventListener('abort', finish) }
    worker.addEventListener('statechange', finish); signal.addEventListener('abort', finish); finish()
  })
}
export function decodeOfflineContext(value: unknown, vessel: string, assignment: string): OfflineContext {
  if (!offlineKeys(value, ['id', 'tenantId', 'actorId', 'vesselId', 'assignmentId', 'deviceId', 'keyId', 'keyChallenge',
    'sessionBinding', 'policyVersion', 'sourceVersion', 'baseVersion', 'baseRevision', 'baseWatchGroup', 'watchGroups',
    'serverTime', 'expiresAt', 'maxRecords', 'maxEncryptedBytes', 'maxDeviceRecords', 'maxDeviceEncryptedBytes']) ||
    ['id', 'tenantId', 'actorId', 'deviceId', 'keyId'].some(k => !isGuid(value[k])) ||
    value.vesselId !== vessel || value.assignmentId !== assignment || value.sessionBinding !== value.id ||
    typeof value.keyChallenge !== 'string' || !/^[A-Za-z0-9+/]{43}=$/.test(value.keyChallenge) ||
    typeof value.policyVersion !== 'string' || value.policyVersion.length < 1 || value.policyVersion.length > 80 || value.sourceVersion !== source ||
    !integer(value.baseVersion, 1, 4294967295) || !integer(value.baseRevision, 1, Number.MAX_SAFE_INTEGER) ||
    !Array.isArray(value.watchGroups) || value.watchGroups.length < 1 || value.watchGroups.length > 64 ||
    value.watchGroups.some(x => typeof x !== 'string' || x.length < 1 || x.length > 64 || /\p{Cc}/u.test(x)) ||
    new Set(value.watchGroups).size !== value.watchGroups.length || value.baseWatchGroup !== null && !value.watchGroups.includes(value.baseWatchGroup) ||
    !time(value.serverTime) || !time(value.expiresAt) || Date.parse(value.expiresAt) <= Date.parse(value.serverTime) ||
    !integer(value.maxRecords, 2, 128) || !integer(value.maxEncryptedBytes, 4096, 1048576) ||
    !integer(value.maxDeviceRecords, value.maxRecords, 512) || !integer(value.maxDeviceEncryptedBytes, value.maxEncryptedBytes, 4194304)) return bad()
  return value as OfflineContext
}
function decodeOutcome(v: unknown): OfflineOutcome {
  if (!offlineKeys(v, ['commandId', 'result', 'baseVersion', 'serverVersion', 'revision', 'capturedAt', 'offsetMinutes', 'receivedAt', 'parentCommandId']) ||
    !isGuid(v.commandId) || !['Committed', 'MaterialConflict'].includes(String(v.result)) ||
    !integer(v.baseVersion, 1, 4294967295) || !integer(v.serverVersion, 1, 4294967295) || !integer(v.revision, 1, Number.MAX_SAFE_INTEGER) ||
    !time(v.capturedAt) || !time(v.receivedAt) || !integer(v.offsetMinutes, -840, 840) || v.parentCommandId !== null && !isGuid(v.parentCommandId)) return bad()
  return v as OfflineOutcome
}
function captureOutcome(value: unknown, body: OfflineCaptureBody): OfflineOutcome {
  const outcome = decodeOutcome(value)
  if (outcome.commandId !== body.commandId || outcome.baseVersion !== body.baseVersion ||
    Date.parse(outcome.capturedAt) !== Date.parse(body.capturedAt) || outcome.offsetMinutes !== body.offsetMinutes ||
    outcome.parentCommandId !== (body.parentCommandId ?? null)) return bad()
  return outcome
}
export const offlineRoot = (vessel: string, assignment: string) => {
  if (!isGuid(vessel) || !isGuid(assignment)) return bad()
  return `vessels/${vessel}/crew/${assignment}/offline`
}
export async function inventory(vessel: string, assignment: string, signal: AbortSignal): Promise<OfflineInventory> {
  const value = await request<unknown>(offlineRoot(vessel, assignment), signal)
  if (!offlineKeys(value, ['items']) || !Array.isArray(value.items) || value.items.length > 32 ||
    value.items.some(x => !offlineKeys(x, ['id', 'revokedAt', 'expiresAt', 'policyVersion']) || !isGuid(x.id) ||
    !time(x.expiresAt) || x.revokedAt !== null && !time(x.revokedAt) || typeof x.policyVersion !== 'string')) return bad()
  return value as OfflineInventory
}
export async function hash(value: string) {
  return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value))), b => b.toString(16).padStart(2, '0')).join('')
}

// Keys are nonextractable structured-cloned CryptoKeys, not hardcoded/exported key bytes.
// Only opaque context hashes identify local slots. Online current rights precede lookup.
// Invocation reservations consume budget even when encryption/storage subsequently fails.
export type KeySlot = { id: string; key: CryptoKey; signing: CryptoKeyPair; invocations: number }
export const base64 = (bytes: ArrayBuffer) => btoa(String.fromCharCode(...new Uint8Array(bytes)))
export async function vault<T>(action: (store: IDBObjectStore, done: (value: T) => void) => void): Promise<T> {
  return new Promise((resolve, reject) => {
    const open = indexedDB.open('poseidon.offline.keys.v1', 1)
    let settled = false
    const fail = () => { if (!settled) { settled = true; reject(new Error('key_storage_unavailable')) } }
    open.onupgradeneeded = () => { if (!open.result.objectStoreNames.contains('keys')) open.result.createObjectStore('keys', { keyPath: 'id' }) }
    open.onerror = open.onblocked = fail
    open.onsuccess = () => {
      const db = open.result; if (settled) { db.close(); return }
      const tx = db.transaction('keys', 'readwrite'); let value: T
      tx.oncomplete = () => { db.close(); if (!settled) { settled = true; resolve(value) } }
      tx.onabort = tx.onerror = () => { db.close(); fail() }
      try { action(tx.objectStore('keys'), v => { value = v }) } catch { tx.abort() }
    }
  })
}
export class OfflineApplication {
  readonly context: OfflineContext
  private store: OfflineEncryptedRecordStore
  private key: CryptoKey
  private signing: CryptoKeyPair
  private slot: string
  private csrf: string
  private started: number
  private lifetime: number
  private closed = false
  private channel: BroadcastChannel
  private boundary: string
  private withdrawn?: () => void
  private constructor(context: OfflineContext, store: OfflineEncryptedRecordStore, keys: KeySlot, slot: string, boundary: string, csrf: string, started: number) {
    this.context = context; this.store = store; this.key = keys.key; this.signing = keys.signing; this.slot = slot; this.csrf = csrf; this.started = started
    this.lifetime = Date.parse(context.expiresAt) - Date.parse(context.serverTime)
    this.boundary = boundary; this.channel = new BroadcastChannel('poseidon.offline.authorization.v1')
    this.channel.onmessage = event => {
      if (event.data?.boundary === this.boundary && event.data?.type === 'withdraw') { this.close(false); this.withdrawn?.() }
    }
    // Explicit renewal/new-context authorization invalidates locally observed old
    // handles across tabs. Opaque notices can only withdraw, never grant authority.
    this.channel.postMessage({ boundary, type: 'withdraw' })
  }
  static async authorize(vessel: string, assignment: string, enrollment: string | null, signal: AbortSignal) {
    const user = await session(signal)
    if (!user.authenticated || !user.actorId || !user.tenantId) throw new ApiError(401)
    const started = performance.now()
    const ctx = decodeOfflineContext(await request<unknown>(`${offlineRoot(vessel, assignment)}${enrollment ? `/${enrollment}/renew` : ''}`, signal,
      { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } }), vessel, assignment)
    if (ctx.actorId !== user.actorId || ctx.tenantId !== user.tenantId || signal.aborted) throw new ApiError(403)
    const slot = await hash(JSON.stringify([ctx.tenantId, ctx.actorId, vessel, assignment, ctx.id, ctx.deviceId, ctx.keyId, ctx.policyVersion]))
    let keys = await vault<KeySlot | null>((store, done) => {
      const get = store.get(slot); get.onsuccess = () => done((get.result as KeySlot | undefined) ?? null)
    })
    if ((!keys?.key || !keys.signing?.privateKey) && enrollment) throw new Error('key_lost_data_retained')
    if (!keys) {
      const generated = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
      const signing = await crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign', 'verify'])
      keys = await vault<KeySlot>((store, done) => {
        const get = store.get(slot); get.onsuccess = () => {
          if (get.result) { done(get.result as KeySlot); return }
          const count = store.count(); count.onsuccess = () => {
            if (count.result >= 512) { store.transaction.abort(); return }
            const value = { id: slot, key: generated, signing, invocations: 0 } satisfies KeySlot
            store.add(value); done(value)
          }
        }
      })
    }
    if (!keys.signing?.privateKey || keys.signing.privateKey.extractable || keys.signing.privateKey.type !== 'private' ||
      keys.signing.privateKey.algorithm.name !== 'ECDSA' || (keys.signing.privateKey.algorithm as EcKeyAlgorithm).namedCurve !== 'P-256')
      throw new Error('key_lost_data_retained')
    await vault<void>((store, done) => {
      const get = store.get(slot); get.onsuccess = () => {
        const row = get.result as KeySlot | undefined
        if (!row || row.invocations >= 1024) { store.transaction.abort(); return }
        store.put({ ...row, invocations: row.invocations + 1 }); done(undefined)
      }
    })
    const publicKey = base64(await crypto.subtle.exportKey('spki', keys.signing.publicKey))
    const proof = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, keys.signing.privateKey,
      new TextEncoder().encode(`${ctx.id}/${ctx.keyId}/${ctx.keyChallenge}`)))
    await request(`${offlineRoot(vessel, assignment)}/${ctx.id}/key`, signal, { method: 'POST',
      headers: { 'X-CSRF-TOKEN': user.csrfToken, 'Content-Type': 'application/json' }, body: JSON.stringify({ publicKey, proof }) })
    const store = await OfflineEncryptedRecordStore.open(indexedDB, slot, {
      maxRecords: ctx.maxRecords, maxEncryptedBytes: ctx.maxEncryptedBytes,
      maxDeviceRecords: ctx.maxDeviceRecords, maxDeviceEncryptedBytes: ctx.maxDeviceEncryptedBytes,
    })
    if (signal.aborted) { store.close(); throw new Error('authorization_withdrawn') }
    const boundary = await hash(JSON.stringify([ctx.tenantId, ctx.actorId, vessel, assignment]))
    const app = new OfflineApplication(ctx, store, keys, slot, boundary, user.csrfToken, started)
    try {
      if (!await store.read(ctx.id)) await app.write(ctx.id, JSON.stringify({ kind: 'snapshot', watchGroup: ctx.baseWatchGroup,
        baseVersion: ctx.baseVersion, revision: ctx.baseRevision }), null)
      if (signal.aborted) { app.close(); throw new Error('authorization_withdrawn') }
      return app
    } catch (error) { app.close(); throw error }
  }
  // Elapsed monotonic time only bounds THIS explicit server authorization. It never
  // renews authority or survives restart. Client wall clock/offset cannot grant access.
  active() { return !this.closed && performance.now() >= this.started && performance.now() - this.started < this.lifetime }
  remaining() { return Math.max(0, this.lifetime - (performance.now() - this.started)) }
  private requireActive() { if (!this.active()) throw new Error('reauthorization_required_data_retained') }
  private aad(commandId: string): OfflineCipherContext { return { ...this.context, commandId } }
  private async reserve() {
    await vault<void>((store, done) => {
      const get = store.get(this.slot); get.onsuccess = () => {
        const row = get.result as KeySlot | undefined
        if (!row || row.invocations >= 1024) { store.transaction.abort(); return }
        store.put({ ...row, invocations: row.invocations + 1 }); done(undefined)
      }
    })
  }
  private async write(commandId: string, body: string, expected: number | null, outcome?: OfflineOutcome) {
    this.requireActive(); await this.reserve()
    const sealed = await sealOfflineRecord(crypto, this.key, this.aad(commandId), new TextEncoder().encode(JSON.stringify({ body, outcome })), limits)
    this.requireActive(); return this.store.put(commandId, sealed, expected)
  }
  async captures(): Promise<PendingCapture[]> {
    this.requireActive(); const rows = await this.store.list(this.context.maxRecords)
    const values: PendingCapture[] = []
    for (const row of rows) {
      if (row.commandId === this.context.id) continue // separately authenticated read snapshot
      const record = await this.store.read(row.commandId); if (!record) continue
      const bytes = await openOfflineRecord(crypto, this.key, this.aad(row.commandId), record.sealed, limits)
      this.requireActive(); const value: unknown = JSON.parse(new TextDecoder().decode(bytes))
      if (!offlineKeys(value, ['body'], ['outcome']) || typeof value.body !== 'string') return bad()
      const body = decodeOfflineCapture(JSON.parse(value.body), row.commandId, this.context)
      const outcome = value.outcome === undefined ? undefined : captureOutcome(value.outcome, body)
      values.push({ commandId: row.commandId, revision: row.revision, body: value.body, ...(outcome ? { outcome } : {}) })
    }
    return values
  }
  async snapshot(): Promise<{ watchGroup: string | null; baseVersion: number; revision: number }> {
    this.requireActive(); const record = await this.store.read(this.context.id); if (!record) return bad()
    const bytes = await openOfflineRecord(crypto, this.key, this.aad(this.context.id), record.sealed, limits)
    const envelope: unknown = JSON.parse(new TextDecoder().decode(bytes))
    if (!offlineKeys(envelope, ['body']) || typeof envelope.body !== 'string') return bad()
    const value: unknown = JSON.parse(envelope.body)
    const snapshot = decodeOfflineSnapshot(value, this.context)
    this.requireActive(); return snapshot
  }
  async capture(watchGroup: string | null, resolution?: OfflineConflictResolution) {
    this.requireActive()
    if (watchGroup !== null && !this.context.watchGroups.includes(watchGroup)) throw new Error('unsupported_capture_field')
    const commandId = crypto.randomUUID()
    // Date is untrusted captured provenance; the server validates received UTC and context.
    const body = JSON.stringify({ commandId, keyId: this.context.keyId, deviceId: this.context.deviceId,
      policyVersion: this.context.policyVersion, sourceVersion: source, operation,
      baseVersion: resolution ? Number(resolution.expectedRevision) : this.context.baseVersion,
      capturedAt: new Date().toISOString(), offsetMinutes: -new Date().getTimezoneOffset(), watchGroup,
      ...(resolution ? { parentCommandId: resolution.conflictId, expectedServerVersion: Number(resolution.expectedRevision),
        choice: resolution.decisions[0]?.choice, reason: resolution.reason } : {}) })
    decodeOfflineCapture(JSON.parse(body), commandId, this.context)
    await this.write(commandId, body, null)
    return commandId
  }
  async replay(capture: PendingCapture, signal: AbortSignal) {
    this.requireActive()
    // Sign only the exact already-persisted eligible command, including uncertain
    // retries. A caller-supplied object cannot widen the supported field contract.
    const stored = (await this.captures()).find(row => row.commandId === capture.commandId)
    if (!stored || stored.revision !== capture.revision || stored.body !== capture.body) return bad()
    // Every submission explicitly checks the CURRENT signed session. No background timer retries.
    const user = await session(signal)
    if (!user.authenticated || user.actorId !== this.context.actorId || user.tenantId !== this.context.tenantId) { this.close(); throw new ApiError(403) }
    const digest = (await hash(capture.body)).toUpperCase()
    await this.reserve() // signing invocations share the durable key budget
    const signature = base64(await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, this.signing.privateKey, new TextEncoder().encode(digest)))
    this.requireActive()
    const result = captureOutcome(await request<unknown>(`${offlineRoot(this.context.vesselId, this.context.assignmentId)}/${this.context.id}/replay`, signal, {
      method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': user.csrfToken, 'X-Offline-Hash': digest, 'X-Offline-Signature': signature }, body: capture.body,
    }), decodeOfflineCapture(JSON.parse(capture.body), capture.commandId, this.context))
    this.requireActive(); if (signal.aborted || result.commandId !== capture.commandId) throw new Error('response_withdrawn')
    await this.write(capture.commandId, capture.body, capture.revision, result)
    return result
  }
  async conflict(commandId: string, signal: AbortSignal): Promise<OfflineConflictDetail> {
    this.requireActive()
    const v = await request<unknown>(`${offlineRoot(this.context.vesselId, this.context.assignmentId)}/${this.context.id}/conflicts/${commandId}`, signal)
    const allowedValue = (value: unknown) => offlineKeys(value, ['kind'], ['text']) &&
      (value.kind === 'known' ? typeof value.text === 'string' && this.context.watchGroups.includes(value.text)
        : ['unknown', 'withheld'].includes(String(value.kind)) && !Object.hasOwn(value, 'text'))
    if (!offlineKeys(v, ['id', 'revision', 'contextKey', 'base', 'currentVersion', 'resolved', 'originalServerVersion',
      'resolvedValue', 'resolutionReason', 'resolvedBy', 'fields']) || v.id !== commandId || v.contextKey !== this.context.id ||
      v.revision !== String(v.currentVersion) || !integer(v.currentVersion, 1, 4294967295) ||
      !integer(v.originalServerVersion, 1, 4294967295) || !allowedValue(v.base) ||
      !Array.isArray(v.fields) || v.fields.length !== 1 || !offlineKeys(v.fields[0], ['key', 'label', 'captured', 'server', 'choices']) ||
      v.fields[0].key !== 'watch_group' || v.fields[0].label !== 'Watch group' ||
      !allowedValue(v.fields[0].captured) || !allowedValue(v.fields[0].server) ||
      !Array.isArray(v.fields[0].choices) || v.fields[0].choices.length > 2 ||
      new Set(v.fields[0].choices).size !== v.fields[0].choices.length ||
      v.fields[0].choices.some(choice => !['keepServer', 'useCaptured'].includes(String(choice))) ||
      (v.resolved === null ? v.resolvedValue !== null || v.resolutionReason !== null || v.resolvedBy !== null :
        !allowedValue(v.resolvedValue) || typeof v.resolutionReason !== 'string' || v.resolutionReason.length > 500 || !isGuid(v.resolvedBy))) return bad()
    if (v.resolved !== null) {
      const resolved = decodeOutcome(v.resolved)
      if (resolved.parentCommandId !== commandId || resolved.result !== 'Committed' || v.fields[0].choices.length !== 0) return bad()
    }
    this.requireActive(); if (signal.aborted) throw new Error('response_withdrawn')
    return v as OfflineConflictDetail
  }
  async history(signal: AbortSignal, offset = 0): Promise<OfflineHistory> {
    this.requireActive()
    if (!integer(offset, 0, 1000000)) return bad()
    const value = await request<unknown>(`${offlineRoot(this.context.vesselId, this.context.assignmentId)}/${this.context.id}/history?offset=${offset}`, signal)
    if (!offlineKeys(value, ['items', 'offset', 'hasMore']) || !Array.isArray(value.items) || value.items.length > 25 || value.offset !== offset || typeof value.hasMore !== 'boolean') return bad()
    this.requireActive(); return { items: value.items.map(decodeOutcome), offset, hasMore: value.hasMore }
  }
  async discard(capture: PendingCapture, confirmed: boolean) {
    this.requireActive(); if (!confirmed) throw new Error('discard_confirmation_required')
    await this.store.remove(capture.commandId, capture.revision)
  }
  async revoke(lost: boolean, signal: AbortSignal) {
    this.requireActive()
    await request(`${offlineRoot(this.context.vesselId, this.context.assignmentId)}/${this.context.id}/${lost ? 'lost' : 'revoke'}`, signal,
      { method: 'POST', headers: { 'X-CSRF-TOKEN': this.csrf } })
    this.close()
  }
  onWithdraw(callback: () => void) { this.withdrawn = callback }
  close(announce = true) {
    if (this.closed) return
    this.closed = true; this.store.close()
    if (announce) this.channel.postMessage({ boundary: this.boundary, type: 'withdraw' })
    this.channel.close()
  }
}

// Key loss never creates a replacement key over retained ciphertext. Report loss to
// the server after current authentication; enrolled context remains in history.
export async function reportLostKey(vessel: string, assignment: string, enrollment: string, signal: AbortSignal) {
  const user = await session(signal)
  return request(`${offlineRoot(vessel, assignment)}/${enrollment}/lost`, signal,
    { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } })
}

// Explicit online current-context recovery for an irretrievably lost local key.
// Nothing is decrypted or guessed; confirmation removes only this authorized context.
export async function discardLostCaptures(vessel: string, assignment: string, enrollment: string, confirmed: boolean, signal: AbortSignal) {
  if (!confirmed) throw new Error('discard_confirmation_required')
  const user = await session(signal)
  const value = await request<unknown>(`${offlineRoot(vessel, assignment)}/${enrollment}/cleanup`, signal,
    { method: 'POST', headers: { 'X-CSRF-TOKEN': user.csrfToken } })
  if (!offlineKeys(value, ['id', 'tenantId', 'actorId', 'vesselId', 'assignmentId', 'deviceId', 'keyId', 'policyVersion']) ||
    value.id !== enrollment || value.vesselId !== vessel || value.assignmentId !== assignment ||
    ['id', 'tenantId', 'actorId', 'deviceId', 'keyId'].some(k => !isGuid(value[k])) || typeof value.policyVersion !== 'string') return bad()
  const ctx = value as Pick<OfflineContext, 'id' | 'tenantId' | 'actorId' | 'vesselId' | 'assignmentId' | 'deviceId' | 'keyId' | 'policyVersion'>
  if (!user.authenticated || ctx.actorId !== user.actorId || ctx.tenantId !== user.tenantId || signal.aborted) throw new ApiError(403)
  const slot = await hash(JSON.stringify([ctx.tenantId, ctx.actorId, vessel, assignment, ctx.id, ctx.deviceId, ctx.keyId, ctx.policyVersion]))
  const store = await OfflineEncryptedRecordStore.open(indexedDB, slot, { maxRecords: 128, maxEncryptedBytes: 1048576,
    maxDeviceRecords: 512, maxDeviceEncryptedBytes: 4194304 })
  try {
    await reportLostKey(vessel, assignment, enrollment, signal)
    const boundary = await hash(JSON.stringify([ctx.tenantId, ctx.actorId, vessel, assignment]))
    const channel = new BroadcastChannel('poseidon.offline.authorization.v1')
    channel.postMessage({ boundary, type: 'withdraw' }); channel.close()
    for (const row of await store.list(128)) {
      if (signal.aborted) throw new Error('authorization_withdrawn')
      await store.remove(row.commandId, row.revision)
    }
    await vault<void>((keys, done) => { keys.delete(slot); done(undefined) })
  } finally { store.close() }
}
